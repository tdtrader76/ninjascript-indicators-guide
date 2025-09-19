#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class Opr : Indicator
    {
        #region Variables
        private DateTime currentSessionDate = DateTime.MinValue;
        private DateTime openingRangeStart = DateTime.MinValue;
        private DateTime openingRangeEnd = DateTime.MinValue;
        private DateTime preSessionStart = DateTime.MinValue;
        private DateTime preSessionEnd = DateTime.MinValue;

        private double orHigh = double.NaN;
        private double orLow = double.NaN;
        private double rangeClosePrice = double.NaN;
        private bool hasOpeningRange;

        private double preHigh = double.NaN;
        private double preLow = double.NaN;
        private bool hasPreSession;

        private double[] upperBands = new double[4];
        private double[] lowerBands = new double[4];

        // Configuration
        private int sessionType = 0; // 0=UseChart, 1=RTH, 2=ETH, 3=Custom
        private int preSessionType = 0; // 0=None, 1=MinutesBeforeOpen, 2=CustomRange
        private int bandType = 0; // 0=None, 1=RangePercent, 2=MidpointPercent, 3=ClosePercent
        private string customSessionStart = "09:30";
        private string openingRangePeriod = "30";
        private string preSessionStartTime = "60";
        private string preSessionEndTime = "09:30";
        private double[] percentages = new double[] { 25, 50, 75, 100 };
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Opening Range indicator without external dependencies";
                Name = "Opr";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;

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

                ResetBands();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 1)
                return;

            DateTime barTime = Time[0];

            // Check for new session
            if (currentSessionDate == DateTime.MinValue ||
                Bars.IsFirstBarOfSession ||
                barTime.Date != currentSessionDate)
            {
                currentSessionDate = barTime.Date;
                InitializeSession(barTime);
            }

            // Update pre-session if active
            UpdatePreSession(barTime);

            // Update opening range if active
            UpdateOpeningRange(barTime);

            // Calculate bands
            UpdateBands();

            // Update plot values
            UpdatePlots();
        }

        private void InitializeSession(DateTime sessionTime)
        {
            hasOpeningRange = false;
            hasPreSession = false;
            orHigh = double.NaN;
            orLow = double.NaN;
            rangeClosePrice = double.NaN;
            preHigh = double.NaN;
            preLow = double.NaN;

            // Determine opening range start time
            TimeSpan startTime;
            if (sessionType == 3) // Custom
            {
                startTime = ParseTimeString(customSessionStart);
            }
            else
            {
                startTime = new TimeSpan(9, 30, 0); // Default 9:30 AM
            }

            openingRangeStart = sessionTime.Date + startTime;

            // Parse opening range duration (in minutes)
            int durationMinutes = ParseIntString(openingRangePeriod, 30);
            openingRangeEnd = openingRangeStart.AddMinutes(durationMinutes);

            // Setup pre-session
            SetupPreSession(sessionTime);

            ResetBands();
        }

        private void SetupPreSession(DateTime sessionTime)
        {
            if (preSessionType == 0) // None
            {
                preSessionStart = DateTime.MinValue;
                preSessionEnd = DateTime.MinValue;
                return;
            }

            if (preSessionType == 1) // Minutes before open
            {
                int offsetMinutes = ParseIntString(preSessionStartTime, 60);
                preSessionStart = openingRangeStart.AddMinutes(-offsetMinutes);
                preSessionEnd = openingRangeStart;
            }
            else if (preSessionType == 2) // Custom range
            {
                TimeSpan startTime = ParseTimeString(preSessionStartTime);
                TimeSpan endTime = ParseTimeString(preSessionEndTime);
                preSessionStart = sessionTime.Date + startTime;
                preSessionEnd = sessionTime.Date + endTime;
            }
        }

        private void UpdatePreSession(DateTime barTime)
        {
            if (preSessionStart == DateTime.MinValue || preSessionEnd == DateTime.MinValue)
                return;

            if (barTime >= preSessionStart && barTime < preSessionEnd)
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

        private void UpdateOpeningRange(DateTime barTime)
        {
            if (barTime >= openingRangeStart && barTime < openingRangeEnd)
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
            if (!hasOpeningRange || double.IsNaN(orHigh) || double.IsNaN(orLow))
            {
                ResetBands();
                return;
            }

            double rangeWidth = orHigh - orLow;

            for (int i = 0; i < 4; i++)
            {
                double percentage = percentages[i];
                if (bandType == 0 || Math.Abs(percentage) < double.Epsilon) // None
                {
                    upperBands[i] = double.NaN;
                    lowerBands[i] = double.NaN;
                    continue;
                }

                double pct = percentage / 100.0;

                switch (bandType)
                {
                    case 1: // Range Percent
                        upperBands[i] = orHigh + rangeWidth * pct;
                        lowerBands[i] = orLow - rangeWidth * pct;
                        break;

                    case 2: // Midpoint Percent
                        double mid = (orHigh + orLow) * 0.5;
                        upperBands[i] = mid + rangeWidth * pct;
                        lowerBands[i] = mid - rangeWidth * pct;
                        break;

                    case 3: // Close Percent
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

        private void UpdatePlots()
        {
            // Opening Range plots
            Values[0][0] = hasOpeningRange ? orHigh : double.NaN;
            Values[1][0] = hasOpeningRange ? orLow : double.NaN;
            Values[2][0] = hasOpeningRange ? (orHigh + orLow) * 0.5 : double.NaN;

            // Pre-session plots
            Values[3][0] = hasPreSession ? preHigh : double.NaN;
            Values[4][0] = hasPreSession ? preLow : double.NaN;
            Values[5][0] = hasPreSession ? (preHigh + preLow) * 0.5 : double.NaN;

            // Band plots
            for (int i = 0; i < 4; i++)
            {
                Values[6 + i * 2][0] = upperBands[i];
                Values[7 + i * 2][0] = lowerBands[i];
            }
        }

        private void ResetBands()
        {
            for (int i = 0; i < 4; i++)
            {
                upperBands[i] = double.NaN;
                lowerBands[i] = double.NaN;
            }
        }

        private TimeSpan ParseTimeString(string timeStr)
        {
            if (string.IsNullOrEmpty(timeStr))
                return new TimeSpan(9, 30, 0);

            if (TimeSpan.TryParse(timeStr, out TimeSpan result))
                return result;

            if (DateTime.TryParse(timeStr, out DateTime dt))
                return dt.TimeOfDay;

            return new TimeSpan(9, 30, 0);
        }

        private int ParseIntString(string intStr, int defaultValue)
        {
            if (string.IsNullOrEmpty(intStr))
                return defaultValue;

            if (int.TryParse(intStr, out int result))
                return result;

            return defaultValue;
        }

        #region Properties
        [NinjaScriptProperty]
        [Range(0, 3)]
        [Display(Name = "Session Type", Order = 1, GroupName = "Parameters")]
        public int SessionType
        {
            get { return sessionType; }
            set { sessionType = Math.Max(0, Math.Min(3, value)); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Custom Session Start", Order = 2, GroupName = "Parameters")]
        public string CustomSessionStart
        {
            get { return customSessionStart; }
            set { customSessionStart = value ?? "09:30"; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Opening Range Period (minutes)", Order = 3, GroupName = "Parameters")]
        public string OpeningRangePeriod
        {
            get { return openingRangePeriod; }
            set { openingRangePeriod = value ?? "30"; }
        }

        [NinjaScriptProperty]
        [Range(0, 2)]
        [Display(Name = "Pre-Session Type", Order = 4, GroupName = "Parameters")]
        public int PreSessionType
        {
            get { return preSessionType; }
            set { preSessionType = Math.Max(0, Math.Min(2, value)); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Pre-Session Start", Order = 5, GroupName = "Parameters")]
        public string PreSessionStartTime
        {
            get { return preSessionStartTime; }
            set { preSessionStartTime = value ?? "60"; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Pre-Session End", Order = 6, GroupName = "Parameters")]
        public string PreSessionEndTime
        {
            get { return preSessionEndTime; }
            set { preSessionEndTime = value ?? "09:30"; }
        }

        [NinjaScriptProperty]
        [Range(0, 3)]
        [Display(Name = "Band Type", Order = 7, GroupName = "Parameters")]
        public int BandType
        {
            get { return bandType; }
            set { bandType = Math.Max(0, Math.Min(3, value)); }
        }

        [NinjaScriptProperty]
        [Range(-1000, 1000)]
        [Display(Name = "Percentage 1", Order = 8, GroupName = "Parameters")]
        public double Percentage1
        {
            get { return percentages[0]; }
            set { percentages[0] = value; }
        }

        [NinjaScriptProperty]
        [Range(-1000, 1000)]
        [Display(Name = "Percentage 2", Order = 9, GroupName = "Parameters")]
        public double Percentage2
        {
            get { return percentages[1]; }
            set { percentages[1] = value; }
        }

        [NinjaScriptProperty]
        [Range(-1000, 1000)]
        [Display(Name = "Percentage 3", Order = 10, GroupName = "Parameters")]
        public double Percentage3
        {
            get { return percentages[2]; }
            set { percentages[2] = value; }
        }

        [NinjaScriptProperty]
        [Range(-1000, 1000)]
        [Display(Name = "Percentage 4", Order = 11, GroupName = "Parameters")]
        public double Percentage4
        {
            get { return percentages[3]; }
            set { percentages[3] = value; }
        }
        #endregion

        #region Public Properties for External Access
        [Browsable(false)]
        [XmlIgnore]
        public double OpeningRangeHigh => hasOpeningRange ? orHigh : double.NaN;

        [Browsable(false)]
        [XmlIgnore]
        public double OpeningRangeLow => hasOpeningRange ? orLow : double.NaN;

        [Browsable(false)]
        [XmlIgnore]
        public double OpeningRangeMid => hasOpeningRange ? (orHigh + orLow) * 0.5 : double.NaN;

        [Browsable(false)]
        [XmlIgnore]
        public double PreSessionHigh => hasPreSession ? preHigh : double.NaN;

        [Browsable(false)]
        [XmlIgnore]
        public double PreSessionLow => hasPreSession ? preLow : double.NaN;

        [Browsable(false)]
        [XmlIgnore]
        public double PreSessionMid => hasPreSession ? (preHigh + preLow) * 0.5 : double.NaN;
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Opr[] cacheOpr;
		public Opr Opr(int sessionType, string customSessionStart, string openingRangePeriod, int preSessionType, string preSessionStartTime, string preSessionEndTime, int bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return Opr(Input, sessionType, customSessionStart, openingRangePeriod, preSessionType, preSessionStartTime, preSessionEndTime, bandType, percentage1, percentage2, percentage3, percentage4);
		}

		public Opr Opr(ISeries<double> input, int sessionType, string customSessionStart, string openingRangePeriod, int preSessionType, string preSessionStartTime, string preSessionEndTime, int bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			if (cacheOpr != null)
				for (int idx = 0; idx < cacheOpr.Length; idx++)
					if (cacheOpr[idx] != null && cacheOpr[idx].SessionType == sessionType && cacheOpr[idx].CustomSessionStart == customSessionStart && cacheOpr[idx].OpeningRangePeriod == openingRangePeriod && cacheOpr[idx].PreSessionType == preSessionType && cacheOpr[idx].PreSessionStartTime == preSessionStartTime && cacheOpr[idx].PreSessionEndTime == preSessionEndTime && cacheOpr[idx].BandType == bandType && cacheOpr[idx].Percentage1 == percentage1 && cacheOpr[idx].Percentage2 == percentage2 && cacheOpr[idx].Percentage3 == percentage3 && cacheOpr[idx].Percentage4 == percentage4 && cacheOpr[idx].EqualsInput(input))
						return cacheOpr[idx];
			return CacheIndicator<Opr>(new Opr(){ SessionType = sessionType, CustomSessionStart = customSessionStart, OpeningRangePeriod = openingRangePeriod, PreSessionType = preSessionType, PreSessionStartTime = preSessionStartTime, PreSessionEndTime = preSessionEndTime, BandType = bandType, Percentage1 = percentage1, Percentage2 = percentage2, Percentage3 = percentage3, Percentage4 = percentage4 }, input, ref cacheOpr);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Opr Opr(int sessionType, string customSessionStart, string openingRangePeriod, int preSessionType, string preSessionStartTime, string preSessionEndTime, int bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.Opr(Input, sessionType, customSessionStart, openingRangePeriod, preSessionType, preSessionStartTime, preSessionEndTime, bandType, percentage1, percentage2, percentage3, percentage4);
		}

		public Indicators.Opr Opr(ISeries<double> input, int sessionType, string customSessionStart, string openingRangePeriod, int preSessionType, string preSessionStartTime, string preSessionEndTime, int bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.Opr(input, sessionType, customSessionStart, openingRangePeriod, preSessionType, preSessionStartTime, preSessionEndTime, bandType, percentage1, percentage2, percentage3, percentage4);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Opr Opr(int sessionType, string customSessionStart, string openingRangePeriod, int preSessionType, string preSessionStartTime, string preSessionEndTime, int bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.Opr(Input, sessionType, customSessionStart, openingRangePeriod, preSessionType, preSessionStartTime, preSessionEndTime, bandType, percentage1, percentage2, percentage3, percentage4);
		}

		public Indicators.Opr Opr(ISeries<double> input, int sessionType, string customSessionStart, string openingRangePeriod, int preSessionType, string preSessionStartTime, string preSessionEndTime, int bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.Opr(input, sessionType, customSessionStart, openingRangePeriod, preSessionType, preSessionStartTime, preSessionEndTime, bandType, percentage1, percentage2, percentage3, percentage4);
		}
	}
}

#endregion