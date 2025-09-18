#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Core;
using NinjaTrader.NinjaScript;

#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    /// <summary>
    /// Session selection for the opening range calculation.
    /// </summary>
    public enum OprSessionType
    {
        UseChartSession = 0,
        RegularTradingHours = 1,
        ExtendedTradingHours = 2,
        Custom = 3
    }

    /// <summary>
    /// Session selection for the strategy friendly opening range class.
    /// </summary>
    public enum OprSessionTypeS
    {
        UseChartSession = 0,
        RegularTradingHours = 1,
        ExtendedTradingHours = 2,
        Custom = 3
    }

    /// <summary>
    /// Time zone options used to translate the session anchors.
    /// </summary>
    public enum OprTimeZones
    {
        Exchange = 0,
        Eastern = 1,
        Central = 2,
        Mountain = 3,
        Pacific = 4,
        UTC = 5
    }

    /// <summary>
    /// Time zone options for the strategy friendly class.
    /// </summary>
    public enum OprTimeZonesS
    {
        Exchange = 0,
        Eastern = 1,
        Central = 2,
        Mountain = 3,
        Pacific = 4,
        UTC = 5
    }

    /// <summary>
    /// Modes available for pre-session detection.
    /// </summary>
    public enum OprPreSessionType
    {
        None = 0,
        MinutesBeforeOpen = 1,
        CustomRange = 2
    }

    /// <summary>
    /// Strategy friendly duplicate of the pre-session enumeration.
    /// </summary>
    public enum OprPreSessionTypeS
    {
        None = 0,
        MinutesBeforeOpen = 1,
        CustomRange = 2
    }

    /// <summary>
    /// Defines how the percentage bands are projected.
    /// </summary>
    public enum OprBandType
    {
        None = 0,
        RangePercent = 1,
        MidpointPercent = 2,
        ClosePercent = 3
    }

    /// <summary>
    /// Strategy friendly duplicate of the band enumeration.
    /// </summary>
    public enum OprBandTypeS
    {
        None = 0,
        RangePercent = 1,
        MidpointPercent = 2,
        ClosePercent = 3
    }

    /// <summary>
    /// Base class that hosts the full opening range calculation so that different front ends
    /// (chart friendly and strategy friendly) can reuse the logic without external dependencies.
    /// </summary>
    public abstract class OprStandaloneBase : Indicator
    {
        #region configurable values shared by derived classes
        protected int sessionTypeValue;
        protected int customTimeZoneValue;
        protected int preSessionTypeValue;
        protected int preSessionTimeZoneValue;
        protected int bandTypeValue;

        protected string customSessionStart;
        protected string openingRangePeriod;
        protected string preSessionStart;
        protected string preSessionEnd;
        protected readonly double[] percentages = new double[4];
        #endregion

        #region runtime state
        private TimeZoneInfo chartTimeZone;
        private TimeZoneInfo sessionTimeZone;
        private TimeZoneInfo preSessionTimeZone;

        private DateTime currentSessionDate = DateTime.MinValue;
        private DateTime openingRangeStart = DateTime.MinValue;
        private DateTime openingRangeEnd = DateTime.MinValue;
        private DateTime preSessionRangeStart = DateTime.MinValue;
        private DateTime preSessionRangeEnd = DateTime.MinValue;
        private bool preSessionUsesSessionTZ = true;

        private double orHigh = double.NaN;
        private double orLow = double.NaN;
        private double rangeClosePrice = double.NaN;
        private bool hasOpeningRange;

        private double preHigh = double.NaN;
        private double preLow = double.NaN;
        private bool hasPreSession;

        protected readonly double[] upperBands = new double[4];
        protected readonly double[] lowerBands = new double[4];
        #endregion

        #region abstract hooks
        /// <summary>
        /// Derived classes set their defaults (name, overlays and plots) here.
        /// </summary>
        protected abstract void ConfigureIndicatorDefaults();

        /// <summary>
        /// Writes the calculated values to the indicator outputs.
        /// </summary>
        protected abstract void UpdatePlots();
        #endregion

        #region exposed values for consumers
        [Browsable(false), XmlIgnore]
        public double OpeningRangeHigh => hasOpeningRange ? orHigh : double.NaN;

        [Browsable(false), XmlIgnore]
        public double OpeningRangeLow => hasOpeningRange ? orLow : double.NaN;

        [Browsable(false), XmlIgnore]
        public double OpeningRangeMid => hasOpeningRange ? (orHigh + orLow) * 0.5 : double.NaN;

        [Browsable(false), XmlIgnore]
        public double OpeningRangeClose => hasOpeningRange ? rangeClosePrice : double.NaN;

        [Browsable(false), XmlIgnore]
        public double OpeningRangeWidth => hasOpeningRange ? orHigh - orLow : double.NaN;

        [Browsable(false), XmlIgnore]
        public double PreSessionHigh => hasPreSession ? preHigh : double.NaN;

        [Browsable(false), XmlIgnore]
        public double PreSessionLow => hasPreSession ? preLow : double.NaN;

        [Browsable(false), XmlIgnore]
        public double PreSessionMid => hasPreSession ? (preHigh + preLow) * 0.5 : double.NaN;

        [Browsable(false), XmlIgnore]
        public double Band1Upper => upperBands[0];

        [Browsable(false), XmlIgnore]
        public double Band1Lower => lowerBands[0];

        [Browsable(false), XmlIgnore]
        public double Band2Upper => upperBands[1];

        [Browsable(false), XmlIgnore]
        public double Band2Lower => lowerBands[1];

        [Browsable(false), XmlIgnore]
        public double Band3Upper => upperBands[2];

        [Browsable(false), XmlIgnore]
        public double Band3Lower => lowerBands[2];

        [Browsable(false), XmlIgnore]
        public double Band4Upper => upperBands[3];

        [Browsable(false), XmlIgnore]
        public double Band4Lower => lowerBands[3];
        #endregion

        #region life-cycle
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                SetBaseDefaults();
                ConfigureIndicatorDefaults();
            }
            else if (State == State.Configure)
            {
                OnConfigure();
            }
            else if (State == State.DataLoaded)
            {
                chartTimeZone = ResolveChartTimeZone();
            }
        }

        protected virtual void OnConfigure()
        {
        }
        #endregion

        #region OnBarUpdate workflow
        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToPlot)
            {
                UpdatePlots();
                return;
            }

            if (chartTimeZone == null)
                chartTimeZone = ResolveChartTimeZone();

            DateTime instrumentTime = Time[0];
            DateTime sessionTime = ConvertToTimeZone(instrumentTime, SessionTimeZone);

            if (currentSessionDate == DateTime.MinValue || Bars.IsFirstBarOfSession || sessionTime.Date != currentSessionDate)
            {
                currentSessionDate = sessionTime.Date;
                InitializeSession(instrumentTime, sessionTime);
            }

            DateTime preComparisonTime = preSessionUsesSessionTZ ? sessionTime : ConvertToTimeZone(instrumentTime, PreSessionTimeZone);

            UpdatePreSession(preComparisonTime);
            UpdateOpeningRange(sessionTime);
            UpdateBands();
            UpdatePlots();
        }
        #endregion

        #region initialization helpers
        private void SetBaseDefaults()
        {
            Description = "Standalone opening range implementation without external dependencies.";
            Name = "OprStandalone";
            Calculate = Calculate.OnPriceChange;
            IsOverlay = true;
            DisplayInDataBox = true;
            IsSuspendedWhileInactive = true;

            sessionTypeValue = (int)OprSessionType.UseChartSession;
            customTimeZoneValue = (int)OprTimeZones.Exchange;
            preSessionTypeValue = (int)OprPreSessionType.None;
            preSessionTimeZoneValue = (int)OprTimeZones.Exchange;
            bandTypeValue = (int)OprBandType.None;

            customSessionStart = "08:30";
            openingRangePeriod = "00:30:00";
            preSessionStart = "07:30";
            preSessionEnd = "08:30";

            percentages[0] = 25;
            percentages[1] = 50;
            percentages[2] = 75;
            percentages[3] = 100;

            ResetRuntimeState();
        }

        private void InitializeSession(DateTime instrumentTime, DateTime sessionTime)
        {
            sessionTimeZone = ResolveTimeZone((OprTimeZones)customTimeZoneValue);
            preSessionTimeZone = ResolveTimeZone((OprTimeZones)preSessionTimeZoneValue);

            hasOpeningRange = false;
            hasPreSession = false;
            orHigh = double.NaN;
            orLow = double.NaN;
            rangeClosePrice = double.NaN;
            preHigh = double.NaN;
            preLow = double.NaN;

            TimeSpan startSpan = DetermineRangeStart(sessionTime);
            openingRangeStart = AlignToSessionDate(sessionTime.Date, startSpan, sessionTime);
            TimeSpan duration = ParseDuration(openingRangePeriod, TimeSpan.FromMinutes(30));
            openingRangeEnd = openingRangeStart + duration;
            if (openingRangeEnd <= openingRangeStart)
                openingRangeEnd = openingRangeEnd.AddDays(1);

            SetupPreSession(instrumentTime, sessionTime);
            ResetBands();
        }

        private void ResetRuntimeState()
        {
            hasOpeningRange = false;
            hasPreSession = false;
            orHigh = double.NaN;
            orLow = double.NaN;
            rangeClosePrice = double.NaN;
            preHigh = double.NaN;
            preLow = double.NaN;
            ResetBands();
        }

        private void ResetBands()
        {
            for (int i = 0; i < upperBands.Length; i++)
            {
                upperBands[i] = double.NaN;
                lowerBands[i] = double.NaN;
            }
        }

        private TimeSpan DetermineRangeStart(DateTime sessionTime)
        {
            OprSessionType mode = (OprSessionType)sessionTypeValue;
            switch (mode)
            {
                case OprSessionType.Custom:
                    return ParseTimeOfDay(customSessionStart, sessionTime.TimeOfDay);
                default:
                    return sessionTime.TimeOfDay;
            }
        }

        private void SetupPreSession(DateTime instrumentTime, DateTime sessionTime)
        {
            OprPreSessionType mode = (OprPreSessionType)preSessionTypeValue;
            if (mode == OprPreSessionType.None)
            {
                preSessionRangeStart = DateTime.MinValue;
                preSessionRangeEnd = DateTime.MinValue;
                preSessionUsesSessionTZ = true;
                return;
            }

            if (mode == OprPreSessionType.MinutesBeforeOpen)
            {
                preSessionUsesSessionTZ = true;
                TimeSpan offset = ParseDuration(preSessionStart, TimeSpan.FromMinutes(60));
                preSessionRangeStart = openingRangeStart - offset;
                preSessionRangeEnd = openingRangeStart;
                if (preSessionRangeEnd <= preSessionRangeStart)
                    preSessionRangeEnd = preSessionRangeEnd.AddMinutes(1);
                return;
            }

            preSessionUsesSessionTZ = false;
            DateTime preTime = ConvertToTimeZone(instrumentTime, PreSessionTimeZone);
            DateTime baseDate = preTime.Date;
            TimeSpan startSpan = ParseTimeOfDay(preSessionStart, TimeSpan.Zero);
            TimeSpan endSpan = ParseTimeOfDay(preSessionEnd, startSpan + TimeSpan.FromMinutes(30));
            preSessionRangeStart = baseDate + startSpan;
            preSessionRangeEnd = baseDate + endSpan;
            if (preSessionRangeEnd <= preSessionRangeStart)
                preSessionRangeEnd = preSessionRangeEnd.AddDays(1);
        }
        #endregion

        #region update helpers
        private void UpdatePreSession(DateTime comparisonTime)
        {
            if (preSessionRangeStart == DateTime.MinValue || preSessionRangeEnd == DateTime.MinValue)
                return;

            if (comparisonTime >= preSessionRangeStart && comparisonTime < preSessionRangeEnd)
            {
                if (!hasPreSession)
                {
                    hasPreSession = true;
                    preHigh = High[0];
                    preLow = Low[0];
                }
                else
                {
                    preHigh = Math.Max(preHigh, High[0]);
                    preLow = Math.Min(preLow, Low[0]);
                }
            }
        }

        private void UpdateOpeningRange(DateTime sessionTime)
        {
            if (sessionTime >= openingRangeStart && sessionTime < openingRangeEnd)
            {
                if (!hasOpeningRange)
                {
                    hasOpeningRange = true;
                    orHigh = High[0];
                    orLow = Low[0];
                }
                else
                {
                    orHigh = Math.Max(orHigh, High[0]);
                    orLow = Math.Min(orLow, Low[0]);
                }
                rangeClosePrice = Close[0];
            }
        }

        private void UpdateBands()
        {
            double rangeHigh = OpeningRangeHigh;
            double rangeLow = OpeningRangeLow;
            if (double.IsNaN(rangeHigh) || double.IsNaN(rangeLow))
            {
                ResetBands();
                return;
            }

            double rangeWidth = rangeHigh - rangeLow;
            OprBandType bandMode = (OprBandType)bandTypeValue;

            for (int i = 0; i < upperBands.Length; i++)
            {
                double percentage = percentages[i];
                if (bandMode == OprBandType.None || double.IsNaN(percentage) || Math.Abs(percentage) < double.Epsilon)
                {
                    upperBands[i] = double.NaN;
                    lowerBands[i] = double.NaN;
                    continue;
                }

                double pct = percentage / 100.0;
                switch (bandMode)
                {
                    case OprBandType.RangePercent:
                        upperBands[i] = rangeHigh + rangeWidth * pct;
                        lowerBands[i] = rangeLow - rangeWidth * pct;
                        break;
                    case OprBandType.MidpointPercent:
                        double mid = OpeningRangeMid;
                        if (double.IsNaN(mid))
                        {
                            upperBands[i] = double.NaN;
                            lowerBands[i] = double.NaN;
                        }
                        else
                        {
                            upperBands[i] = mid + rangeWidth * pct;
                            lowerBands[i] = mid - rangeWidth * pct;
                        }
                        break;
                    case OprBandType.ClosePercent:
                        double closeBase = double.IsNaN(rangeClosePrice) ? Close[0] : rangeClosePrice;
                        upperBands[i] = closeBase + rangeWidth * pct;
                        lowerBands[i] = closeBase - rangeWidth * pct;
                        break;
                    default:
                        upperBands[i] = double.NaN;
                        lowerBands[i] = double.NaN;
                        break;
                }
            }
        }
        #endregion

        #region helper methods
        protected void AddStandardPlots()
        {
            AddPlot(Brushes.DodgerBlue, "OR-High");
            AddPlot(Brushes.OrangeRed, "OR-Low");
            AddPlot(Brushes.Gray, "OR-Mid");
            AddPlot(Brushes.SlateBlue, "Pre-High");
            AddPlot(Brushes.SlateBlue, "Pre-Low");
            AddPlot(Brushes.SlateBlue, "Pre-Mid");
            AddPlot(Brushes.ForestGreen, "Band1-Upper");
            AddPlot(Brushes.Firebrick, "Band1-Lower");
            AddPlot(Brushes.ForestGreen, "Band2-Upper");
            AddPlot(Brushes.Firebrick, "Band2-Lower");
            AddPlot(Brushes.ForestGreen, "Band3-Upper");
            AddPlot(Brushes.Firebrick, "Band3-Lower");
            AddPlot(Brushes.ForestGreen, "Band4-Upper");
            AddPlot(Brushes.Firebrick, "Band4-Lower");
        }

        protected void WriteStandardOutputs()
        {
            if (Values == null || Values.Length < 14)
                return;

            Values[0][0] = OpeningRangeHigh;
            Values[1][0] = OpeningRangeLow;
            Values[2][0] = OpeningRangeMid;
            Values[3][0] = PreSessionHigh;
            Values[4][0] = PreSessionLow;
            Values[5][0] = PreSessionMid;

            for (int i = 0; i < 4; i++)
            {
                Values[6 + i * 2][0] = upperBands[i];
                Values[7 + i * 2][0] = lowerBands[i];
            }
        }

        protected OprSessionType SessionTypeCore => (OprSessionType)sessionTypeValue;
        protected OprTimeZones CustomTimeZoneCore => (OprTimeZones)customTimeZoneValue;
        protected OprPreSessionType PreSessionTypeCore => (OprPreSessionType)preSessionTypeValue;
        protected OprTimeZones PreSessionTimeZoneCore => (OprTimeZones)preSessionTimeZoneValue;
        protected OprBandType BandTypeCore => (OprBandType)bandTypeValue;

        protected void InvalidateSessionTimeZone()
        {
            sessionTimeZone = null;
        }

        protected void InvalidatePreSessionTimeZone()
        {
            preSessionTimeZone = null;
        }

        private TimeZoneInfo SessionTimeZone => sessionTimeZone ?? (sessionTimeZone = ResolveTimeZone(CustomTimeZoneCore));
        private TimeZoneInfo PreSessionTimeZone => preSessionTimeZone ?? (preSessionTimeZone = ResolveTimeZone(PreSessionTimeZoneCore));

        private TimeZoneInfo ResolveChartTimeZone()
        {
            TimeZoneInfo tz = null;
            try
            {
                tz = Bars?.TradingHours?.TimeZoneInfo;
            }
            catch
            {
            }

            if (tz == null)
            {
                try
                {
                    tz = Core.Globals.GeneralOptions.TimeZoneInfo;
                }
                catch
                {
                }
            }

            return tz ?? TimeZoneInfo.Local;
        }

        private TimeZoneInfo ResolveTimeZone(OprTimeZones selector)
        {
            switch (selector)
            {
                case OprTimeZones.Eastern:
                    return FindTimeZone("Eastern Standard Time", "America/New_York");
                case OprTimeZones.Central:
                    return FindTimeZone("Central Standard Time", "America/Chicago");
                case OprTimeZones.Mountain:
                    return FindTimeZone("Mountain Standard Time", "America/Denver");
                case OprTimeZones.Pacific:
                    return FindTimeZone("Pacific Standard Time", "America/Los_Angeles");
                case OprTimeZones.UTC:
                    return TimeZoneInfo.Utc;
                default:
                    return chartTimeZone ?? ResolveChartTimeZone();
            }
        }

        private static TimeZoneInfo FindTimeZone(string windowsId, string ianaId)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }

        private DateTime AlignToSessionDate(DateTime sessionDate, TimeSpan startSpan, DateTime reference)
        {
            DateTime candidate = sessionDate.Date + startSpan;
            if (reference - candidate > TimeSpan.FromHours(18))
                candidate = candidate.AddDays(1);
            else if (candidate - reference > TimeSpan.FromHours(18))
                candidate = candidate.AddDays(-1);
            return candidate;
        }

        private DateTime ConvertToTimeZone(DateTime time, TimeZoneInfo target)
        {
            if (target == null || chartTimeZone == null || target.Equals(chartTimeZone))
                return time;

            return TimeZoneInfo.ConvertTime(time, chartTimeZone, target);
        }

        private static TimeSpan ParseDuration(string value, TimeSpan fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out TimeSpan span))
                return span;

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes))
                return TimeSpan.FromMinutes(minutes);

            return fallback;
        }

        private static TimeSpan ParseTimeOfDay(string value, TimeSpan fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out TimeSpan span))
                return span;

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                return dt.TimeOfDay;

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes))
                return TimeSpan.FromMinutes(minutes);

            return fallback;
        }
        #endregion

        #region shared properties
        [NinjaScriptProperty]
        [Display(Name = "Custom session start", GroupName = "Opening Range", Order = 2, Description = "HH:mm formatted start time used when the session type is Custom.")]
        public string S_CustomSessionStart
        {
            get => customSessionStart;
            set => customSessionStart = value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Opening range period", GroupName = "Opening Range", Order = 3, Description = "Duration of the opening range. Accepts minutes or HH:mm formats.")]
        public string S_OpeningRangePeriod
        {
            get => openingRangePeriod;
            set => openingRangePeriod = value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Pre-session start", GroupName = "Pre Session", Order = 1, Description = "Start time for the custom pre-session range or offset minutes when MinutesBeforeOpen is selected.")]
        public string S_PreSessionStart
        {
            get => preSessionStart;
            set => preSessionStart = value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Pre-session end", GroupName = "Pre Session", Order = 2, Description = "End time for the custom pre-session range.")]
        public string S_PreSessionEnd
        {
            get => preSessionEnd;
            set => preSessionEnd = value;
        }

        [NinjaScriptProperty]
        [Range(-1000, 1000)]
        [Display(Name = "Percentage 1", GroupName = "Bands", Order = 0)]
        public double Percentage1
        {
            get => percentages[0];
            set => percentages[0] = value;
        }

        [NinjaScriptProperty]
        [Range(-1000, 1000)]
        [Display(Name = "Percentage 2", GroupName = "Bands", Order = 1)]
        public double Percentage2
        {
            get => percentages[1];
            set => percentages[1] = value;
        }

        [NinjaScriptProperty]
        [Range(-1000, 1000)]
        [Display(Name = "Percentage 3", GroupName = "Bands", Order = 2)]
        public double Percentage3
        {
            get => percentages[2];
            set => percentages[2] = value;
        }

        [NinjaScriptProperty]
        [Range(-1000, 1000)]
        [Display(Name = "Percentage 4", GroupName = "Bands", Order = 3)]
        public double Percentage4
        {
            get => percentages[3];
            set => percentages[3] = value;
        }
        #endregion
    }

    /// <summary>
    /// Chart friendly implementation of the opening range that renders the levels on the panel.
    /// </summary>
    public class Opr : OprStandaloneBase
    {
        protected override void ConfigureIndicatorDefaults()
        {
            Name = "Opr";
            Description = "Opening range and pre-session bands calculated without external dependencies.";
            Calculate = Calculate.OnPriceChange;
            IsOverlay = true;
            IsSuspendedWhileInactive = true;

            AddStandardPlots();
        }

        protected override void UpdatePlots()
        {
            WriteStandardOutputs();
        }

        #region indicator output helpers
        [Browsable(false), XmlIgnore]
        public Series<double> ORHighSeries => Values[0];

        [Browsable(false), XmlIgnore]
        public Series<double> ORLowSeries => Values[1];

        [Browsable(false), XmlIgnore]
        public Series<double> ORMidSeries => Values[2];

        [Browsable(false), XmlIgnore]
        public Series<double> PreHighSeries => Values[3];

        [Browsable(false), XmlIgnore]
        public Series<double> PreLowSeries => Values[4];

        [Browsable(false), XmlIgnore]
        public Series<double> PreMidSeries => Values[5];
        #endregion

        #region configuration properties
        [NinjaScriptProperty]
        [Display(Name = "Session type", GroupName = "Opening Range", Order = 0)]
        public OprSessionType SessionType
        {
            get => (OprSessionType)sessionTypeValue;
            set => sessionTypeValue = (int)value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Session time zone", GroupName = "Opening Range", Order = 1)]
        public OprTimeZones CustomTZSelector
        {
            get => (OprTimeZones)customTimeZoneValue;
            set
            {
                customTimeZoneValue = (int)value;
                InvalidateSessionTimeZone();
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Pre-session type", GroupName = "Pre Session", Order = 0)]
        public OprPreSessionType PreSessionType
        {
            get => (OprPreSessionType)preSessionTypeValue;
            set => preSessionTypeValue = (int)value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Pre-session time zone", GroupName = "Pre Session", Order = 3)]
        public OprTimeZones PreSessionTZSelector
        {
            get => (OprTimeZones)preSessionTimeZoneValue;
            set
            {
                preSessionTimeZoneValue = (int)value;
                InvalidatePreSessionTimeZone();
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Band type", GroupName = "Bands", Order = -1)]
        public OprBandType BandType
        {
            get => (OprBandType)bandTypeValue;
            set => bandTypeValue = (int)value;
        }
        #endregion
    }

    /// <summary>
    /// Strategy friendly variant that exposes the same calculations without drawing on the chart.
    /// </summary>
    public class Opr_S : OprStandaloneBase
    {
        protected override void ConfigureIndicatorDefaults()
        {
            Name = "Opr_S";
            Description = "Opening range calculation tailored for strategies, market analyzer columns or other scripts.";
            Calculate = Calculate.OnBarClose;
            IsOverlay = false;
            IsSuspendedWhileInactive = true;
            DisplayInDataBox = false;

            AddStandardPlots();

            foreach (var plot in Plots)
                plot.Brush = Brushes.Transparent;
        }

        protected override void UpdatePlots()
        {
            WriteStandardOutputs();
        }

        #region configuration properties
        [NinjaScriptProperty]
        [Display(Name = "Session type", GroupName = "Opening Range", Order = 0)]
        public OprSessionTypeS SessionType
        {
            get => (OprSessionTypeS)sessionTypeValue;
            set => sessionTypeValue = (int)value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Session time zone", GroupName = "Opening Range", Order = 1)]
        public OprTimeZonesS CustomTZSelector
        {
            get => (OprTimeZonesS)customTimeZoneValue;
            set
            {
                customTimeZoneValue = (int)value;
                InvalidateSessionTimeZone();
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Pre-session type", GroupName = "Pre Session", Order = 0)]
        public OprPreSessionTypeS PreSessionType
        {
            get => (OprPreSessionTypeS)preSessionTypeValue;
            set => preSessionTypeValue = (int)value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Pre-session time zone", GroupName = "Pre Session", Order = 3)]
        public OprTimeZonesS PreSessionTZSelector
        {
            get => (OprTimeZonesS)preSessionTimeZoneValue;
            set
            {
                preSessionTimeZoneValue = (int)value;
                InvalidatePreSessionTimeZone();
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Band type", GroupName = "Bands", Order = -1)]
        public OprBandTypeS BandType
        {
            get => (OprBandTypeS)bandTypeValue;
            set => bandTypeValue = (int)value;
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private Opr[] cacheOpr;
        private Opr_S[] cacheOpr_S;

        public Opr Opr(
            OprSessionType sessionType,
            OprTimeZones customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            OprPreSessionType preSessionType,
            OprTimeZones preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            OprBandType bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            return Opr(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
        }

        public Opr_S Opr_S(
            OprSessionTypeS sessionType,
            OprTimeZonesS customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            OprPreSessionTypeS preSessionType,
            OprTimeZonesS preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            OprBandTypeS bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            return Opr_S(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
        }

        public Opr Opr(
            ISeries<double> input,
            OprSessionType sessionType,
            OprTimeZones customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            OprPreSessionType preSessionType,
            OprTimeZones preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            OprBandType bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            if (cacheOpr != null)
                for (int idx = 0; idx < cacheOpr.Length; idx++)
                    if (cacheOpr[idx] != null
                        && cacheOpr[idx].SessionType == sessionType
                        && cacheOpr[idx].CustomTZSelector == customTZSelector
                        && cacheOpr[idx].S_CustomSessionStart == s_CustomSessionStart
                        && cacheOpr[idx].S_OpeningRangePeriod == s_OpeningRangePeriod
                        && cacheOpr[idx].PreSessionType == preSessionType
                        && cacheOpr[idx].PreSessionTZSelector == preSessionTZSelector
                        && cacheOpr[idx].S_PreSessionStart == s_PreSessionStart
                        && cacheOpr[idx].S_PreSessionEnd == s_PreSessionEnd
                        && cacheOpr[idx].BandType == bandType
                        && cacheOpr[idx].Percentage1.Equals(percentage1)
                        && cacheOpr[idx].Percentage2.Equals(percentage2)
                        && cacheOpr[idx].Percentage3.Equals(percentage3)
                        && cacheOpr[idx].Percentage4.Equals(percentage4)
                        && cacheOpr[idx].EqualsInput(input))
                        return cacheOpr[idx];

            return CacheIndicator<Opr>(new Opr
            {
                SessionType = sessionType,
                CustomTZSelector = customTZSelector,
                S_CustomSessionStart = s_CustomSessionStart,
                S_OpeningRangePeriod = s_OpeningRangePeriod,
                PreSessionType = preSessionType,
                PreSessionTZSelector = preSessionTZSelector,
                S_PreSessionStart = s_PreSessionStart,
                S_PreSessionEnd = s_PreSessionEnd,
                BandType = bandType,
                Percentage1 = percentage1,
                Percentage2 = percentage2,
                Percentage3 = percentage3,
                Percentage4 = percentage4
            }, input, ref cacheOpr);
        }

        public Opr_S Opr_S(
            ISeries<double> input,
            OprSessionTypeS sessionType,
            OprTimeZonesS customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            OprPreSessionTypeS preSessionType,
            OprTimeZonesS preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            OprBandTypeS bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            if (cacheOpr_S != null)
                for (int idx = 0; idx < cacheOpr_S.Length; idx++)
                    if (cacheOpr_S[idx] != null
                        && cacheOpr_S[idx].SessionType == sessionType
                        && cacheOpr_S[idx].CustomTZSelector == customTZSelector
                        && cacheOpr_S[idx].S_CustomSessionStart == s_CustomSessionStart
                        && cacheOpr_S[idx].S_OpeningRangePeriod == s_OpeningRangePeriod
                        && cacheOpr_S[idx].PreSessionType == preSessionType
                        && cacheOpr_S[idx].PreSessionTZSelector == preSessionTZSelector
                        && cacheOpr_S[idx].S_PreSessionStart == s_PreSessionStart
                        && cacheOpr_S[idx].S_PreSessionEnd == s_PreSessionEnd
                        && cacheOpr_S[idx].BandType == bandType
                        && cacheOpr_S[idx].Percentage1.Equals(percentage1)
                        && cacheOpr_S[idx].Percentage2.Equals(percentage2)
                        && cacheOpr_S[idx].Percentage3.Equals(percentage3)
                        && cacheOpr_S[idx].Percentage4.Equals(percentage4)
                        && cacheOpr_S[idx].EqualsInput(input))
                        return cacheOpr_S[idx];

            return CacheIndicator<Opr_S>(new Opr_S
            {
                SessionType = sessionType,
                CustomTZSelector = customTZSelector,
                S_CustomSessionStart = s_CustomSessionStart,
                S_OpeningRangePeriod = s_OpeningRangePeriod,
                PreSessionType = preSessionType,
                PreSessionTZSelector = preSessionTZSelector,
                S_PreSessionStart = s_PreSessionStart,
                S_PreSessionEnd = s_PreSessionEnd,
                BandType = bandType,
                Percentage1 = percentage1,
                Percentage2 = percentage2,
                Percentage3 = percentage3,
                Percentage4 = percentage4
            }, input, ref cacheOpr_S);
        }
    }
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.Opr Opr(
            Indicators.OprSessionType sessionType,
            Indicators.OprTimeZones customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            Indicators.OprPreSessionType preSessionType,
            Indicators.OprTimeZones preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            Indicators.OprBandType bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            return indicator.Opr(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
        }

        public Indicators.Opr_S Opr_S(
            Indicators.OprSessionTypeS sessionType,
            Indicators.OprTimeZonesS customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            Indicators.OprPreSessionTypeS preSessionType,
            Indicators.OprTimeZonesS preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            Indicators.OprBandTypeS bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            return indicator.Opr_S(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
        }

        public Indicators.Opr Opr(
            ISeries<double> input,
            Indicators.OprSessionType sessionType,
            Indicators.OprTimeZones customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            Indicators.OprPreSessionType preSessionType,
            Indicators.OprTimeZones preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            Indicators.OprBandType bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            return indicator.Opr(input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
        }

        public Indicators.Opr_S Opr_S(
            ISeries<double> input,
            Indicators.OprSessionTypeS sessionType,
            Indicators.OprTimeZonesS customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            Indicators.OprPreSessionTypeS preSessionType,
            Indicators.OprTimeZonesS preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            Indicators.OprBandTypeS bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            return indicator.Opr_S(input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
        }
    }
}

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.Opr Opr(
            Indicators.OprSessionType sessionType,
            Indicators.OprTimeZones customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            Indicators.OprPreSessionType preSessionType,
            Indicators.OprTimeZones preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            Indicators.OprBandType bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            return indicator.Opr(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
        }

        public Indicators.Opr_S Opr_S(
            Indicators.OprSessionTypeS sessionType,
            Indicators.OprTimeZonesS customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            Indicators.OprPreSessionTypeS preSessionType,
            Indicators.OprTimeZonesS preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            Indicators.OprBandTypeS bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            return indicator.Opr_S(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
        }

        public Indicators.Opr Opr(
            ISeries<double> input,
            Indicators.OprSessionType sessionType,
            Indicators.OprTimeZones customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            Indicators.OprPreSessionType preSessionType,
            Indicators.OprTimeZones preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            Indicators.OprBandType bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            return indicator.Opr(input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
        }

        public Indicators.Opr_S Opr_S(
            ISeries<double> input,
            Indicators.OprSessionTypeS sessionType,
            Indicators.OprTimeZonesS customTZSelector,
            string s_CustomSessionStart,
            string s_OpeningRangePeriod,
            Indicators.OprPreSessionTypeS preSessionType,
            Indicators.OprTimeZonesS preSessionTZSelector,
            string s_PreSessionStart,
            string s_PreSessionEnd,
            Indicators.OprBandTypeS bandType,
            double percentage1,
            double percentage2,
            double percentage3,
            double percentage4)
        {
            return indicator.Opr_S(input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
        }
    }
}

#endregion
