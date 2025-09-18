using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;

namespace NinjaTrader.NinjaScript.Indicators
{
    public class CVOL : Indicator
    {
        #region Variables
        private const int volumePeriod = 20; // 20-period for session volume average
        private const int dailyVolumePeriod = 21; // 21-period for chart timeframe volume average
        private List<double> sessionVolumes = new List<double>(); // Store volumes for the selected session
        private List<double> dailyVolumes = new List<double>(); // Store per-bar volumes for average
        private Dictionary<DateTime, double> americanSessionFirstBarVolumes = new Dictionary<DateTime, double>(); // Store first bar volumes of American sessions
        private double currentDayVolume = 0; // Track volume for the current day
        private DateTime lastBarDate = DateTime.MinValue; // Track the date of the last bar
        private bool isFirstBarOfSession = false; // Flag for first bar of session
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Melvin E. Dickover : 'Evidence-Based Support & Resistance' - TASC April 2014";
                Name = "CVOL";
                Calculate = Calculate.OnBarClose;
                IsOverlay = false;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // Default session times
                EuropeanSessionStart = new TimeSpan(8, 0, 0); // 08:00
                EuropeanSessionEnd = new TimeSpan(15, 30, 0); // 15:30
                AmericanSessionStart = new TimeSpan(15, 30, 0); // 15:30
                AmericanSessionEnd = new TimeSpan(22, 15, 0); // 22:15
                SelectedSession = SessionType.European; // Default session
                IncludeFirstBar = true; // Default to include first bar
            }
            else if (State == State.Configure)
            {
                AddPlot(new Stroke(Brushes.White, 6), PlotStyle.Bar, "CVOL");
            }
        }

        protected override void OnBarUpdate()
        {
            // Skip if not enough bars
            if (CurrentBar == 0)
            {
                PlotHist[0] = 0;
                PlotBrushes[0][0] = Brushes.White;
                return;
            }

            // Get current bar's time and volume
            DateTime currentBarTime = Time[0];
            double currentVolume = VOL()[0];
            TimeSpan currentTimeOfDay = currentBarTime.TimeOfDay;

            // Determine if current bar is within the selected session
            bool isInSession = false;
            bool isInAmericanSession = currentTimeOfDay >= AmericanSessionStart && currentTimeOfDay <= AmericanSessionEnd;
            if (SelectedSession == SessionType.European)
            {
                isInSession = currentTimeOfDay >= EuropeanSessionStart && currentTimeOfDay <= EuropeanSessionEnd;
            }
            else if (SelectedSession == SessionType.American)
            {
                isInSession = isInAmericanSession;
            }
            else if (SelectedSession == SessionType.Both)
            {
                isInSession = (currentTimeOfDay >= EuropeanSessionStart && currentTimeOfDay <= EuropeanSessionEnd) ||
                              isInAmericanSession;
            }

            if (!isInSession)
            {
                PlotHist[0] = 0; // Don't plot if outside session
                return;
            }

            // Check if this is the first bar of the session
            isFirstBarOfSession = false;
            if (CurrentBar > 0)
            {
                DateTime previousBarTime = Time[1];
                TimeSpan previousTimeOfDay = previousBarTime.TimeOfDay;
                bool wasPreviousInSession = false;
                if (SelectedSession == SessionType.European)
                {
                    wasPreviousInSession = previousTimeOfDay >= EuropeanSessionStart && previousTimeOfDay <= EuropeanSessionEnd;
                }
                else if (SelectedSession == SessionType.American)
                {
                    wasPreviousInSession = previousTimeOfDay >= AmericanSessionStart && previousTimeOfDay <= AmericanSessionEnd;
                }
                else if (SelectedSession == SessionType.Both)
                {
                    wasPreviousInSession = (previousTimeOfDay >= EuropeanSessionStart && previousTimeOfDay <= EuropeanSessionEnd) ||
                                           (previousTimeOfDay >= AmericanSessionStart && previousTimeOfDay <= AmericanSessionEnd);
                }

                if (!wasPreviousInSession && isInSession)
                {
                    isFirstBarOfSession = true;
                }
            }
            else if (isInSession)
            {
                isFirstBarOfSession = true;
            }

            // Handle American session first bar volume tracking
            if (isInAmericanSession && isFirstBarOfSession)
            {
                americanSessionFirstBarVolumes[currentBarTime.Date] = currentVolume;
                // Limit stored volumes to avoid excessive memory use
                if (americanSessionFirstBarVolumes.Count > dailyVolumePeriod)
                {
                    var oldestDate = americanSessionFirstBarVolumes.Keys.Min();
                    americanSessionFirstBarVolumes.Remove(oldestDate);
                }
            }

            // Handle per-bar volume tracking for 21-period average
            if (lastBarDate != currentBarTime.Date && lastBarDate != DateTime.MinValue)
            {
                // New day started, reset current day volume
                currentDayVolume = 0;
            }
            currentDayVolume += currentVolume;
            lastBarDate = currentBarTime.Date;

            // Store current bar's volume for 21-period average
            dailyVolumes.Add(currentVolume);
            if (dailyVolumes.Count > dailyVolumePeriod)
            {
                dailyVolumes.RemoveAt(0);
            }

            // Calculate 21-period average volume
            double averageDailyVolume = dailyVolumes.Count > 0 ? dailyVolumes.Average() : 0;

            // Only proceed if current bar's volume is at least the 21-period average
            if (averageDailyVolume > 0 && currentVolume < averageDailyVolume)
            {
                PlotHist[0] = 0; // Don't plot if current bar's volume is below 21-period average
                return;
            }

            // Add current volume to session volumes if in session and not excluded first bar
            if (isInSession && (IncludeFirstBar || !isFirstBarOfSession))
            {
                sessionVolumes.Add(currentVolume);
                // Keep only the last 20 periods' worth of volumes
                if (sessionVolumes.Count > volumePeriod * 100) // Approximate, assuming multiple bars per period
                {
                    sessionVolumes.RemoveAt(0);
                }
            }

            // Calculate average volume for the same session time over the last 20 periods
            double averageVolume = 0;
            int validBars = 0;
            for (int i = 1; i <= Math.Min(CurrentBar, volumePeriod * 100); i++)
            {
                if (CurrentBar - i < 0) break;
                DateTime pastBarTime = Time[i];
                TimeSpan pastTimeOfDay = pastBarTime.TimeOfDay;

                bool isPastInSession = false;
                if (SelectedSession == SessionType.European)
                {
                    isPastInSession = pastTimeOfDay >= EuropeanSessionStart && pastTimeOfDay <= EuropeanSessionEnd;
                }
                else if (SelectedSession == SessionType.American)
                {
                    isPastInSession = pastTimeOfDay >= AmericanSessionStart && pastTimeOfDay <= AmericanSessionEnd;
                }
                else if (SelectedSession == SessionType.Both)
                {
                    isPastInSession = (pastTimeOfDay >= EuropeanSessionStart && pastTimeOfDay <= EuropeanSessionEnd) ||
                                      (pastTimeOfDay >= AmericanSessionStart && pastTimeOfDay <= AmericanSessionEnd);
                }

                // Check if past bar is first bar of its session
                bool isPastFirstBar = false;
                if (i < CurrentBar)
                {
                    DateTime previousPastBarTime = Time[i + 1];
                    TimeSpan previousPastTimeOfDay = previousPastBarTime.TimeOfDay;
                    bool wasPreviousPastInSession = false;
                    if (SelectedSession == SessionType.European)
                    {
                        wasPreviousPastInSession = previousPastTimeOfDay >= EuropeanSessionStart && previousPastTimeOfDay <= EuropeanSessionEnd;
                    }
                    else if (SelectedSession == SessionType.American)
                    {
                        wasPreviousPastInSession = previousPastTimeOfDay >= AmericanSessionStart && previousPastTimeOfDay <= AmericanSessionEnd;
                    }
                    else if (SelectedSession == SessionType.Both)
                    {
                        wasPreviousPastInSession = (previousPastTimeOfDay >= EuropeanSessionStart && previousPastTimeOfDay <= EuropeanSessionEnd) ||
                                                  (previousPastTimeOfDay >= AmericanSessionStart && previousPastTimeOfDay <= AmericanSessionEnd);
                    }

                    if (!wasPreviousPastInSession && isPastInSession)
                    {
                        isPastFirstBar = true;
                    }
                }
                else if (isPastInSession)
                {
                    isPastFirstBar = true;
                }

                if (isPastInSession && (IncludeFirstBar || !isPastFirstBar))
                {
                    averageVolume += VOL()[i];
                    validBars++;
                }
            }

            // Calculate average
            if (validBars > 0)
            {
                averageVolume /= validBars;
            }

            // Only plot if current volume is at least 1.5 times the average volume
            if (validBars == 0 || averageVolume == 0 || currentVolume < 1.5 * averageVolume)
            {
                PlotHist[0] = 0; // Don't plot if volume condition not met
                return;
            }

            // Set plot value to current volume
            PlotHist[0] = currentVolume;

            // Check if current bar is the first bar of the American session
            bool isSpecialBar = isInAmericanSession && isFirstBarOfSession;

            // Compare with previous day's American session first bar volume if special bar
            if (isSpecialBar)
            {
                double previousAmericanSessionFirstBarVolume = 0;
                DateTime previousDay = currentBarTime.Date.AddDays(-1);
                if (americanSessionFirstBarVolumes.ContainsKey(previousDay))
                {
                    previousAmericanSessionFirstBarVolume = americanSessionFirstBarVolumes[previousDay];
                }

                if (previousAmericanSessionFirstBarVolume > 0 && currentVolume >= previousAmericanSessionFirstBarVolume * 1.02) // 2% higher
                {
                    PlotBrushes[0][0] = Brushes.Green;
                }
                else
                {
                    // Apply existing color logic
                    if (currentVolume <= averageVolume)
                    {
                        PlotBrushes[0][0] = Brushes.RoyalBlue;
                    }
                    else if (currentVolume >= 3 * averageVolume)
                    {
                        PlotBrushes[0][0] = Brushes.Orange;
                    }
                    else
                    {
                        PlotBrushes[0][0] = Brushes.White;
                    }
                }
            }
            else
            {
                // Apply existing color logic
                if (currentVolume <= averageVolume)
                {
                    PlotBrushes[0][0] = Brushes.RoyalBlue;
                }
                else if (currentVolume >= 3 * averageVolume)
                {
                    PlotBrushes[0][0] = Brushes.Orange;
                }
                else
                {
                    PlotBrushes[0][0] = Brushes.White;
                }
            }
        }

        #region Properties
        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> PlotHist
        {
            get { return Values[0]; }
        }

        [Description("Start time for European session (HH:mm:ss)")]
        [Category("Session Parameters")]
        [Display(Name = "European Session Start")]
        public TimeSpan EuropeanSessionStart { get; set; }

        [Description("End time for European session (HH:mm:ss)")]
        [Category("Session Parameters")]
        [Display(Name = "European Session End")]
        public TimeSpan EuropeanSessionEnd { get; set; }

        [Description("Start time for American session (HH:mm:ss)")]
        [Category("Session Parameters")]
        [Display(Name = "American Session Start")]
        public TimeSpan AmericanSessionStart { get; set; }

        [Description("End time for American session (HH:mm:ss)")]
        [Category("Session Parameters")]
        [Display(Name = "American Session End")]
        public TimeSpan AmericanSessionEnd { get; set; }

        [Description("Session to compare")]
        [Category("Session Parameters")]
        [Display(Name = "Selected Session")]
        public SessionType SelectedSession { get; set; }

        [Description("Include the first bar of the session in calculations")]
        [Category("Parameters")]
        [Display(Name = "Include First Bar")]
        public bool IncludeFirstBar { get; set; }
        #endregion
    }

    public enum SessionType
    {
        European,
        American,
        Both
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private CVOL[] cacheCVOL;
		public CVOL CVOL()
		{
			return CVOL(Input);
		}

		public CVOL CVOL(ISeries<double> input)
		{
			if (cacheCVOL != null)
				for (int idx = 0; idx < cacheCVOL.Length; idx++)
					if (cacheCVOL[idx] != null &&  cacheCVOL[idx].EqualsInput(input))
						return cacheCVOL[idx];
			return CacheIndicator<CVOL>(new CVOL(), input, ref cacheCVOL);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.CVOL CVOL()
		{
			return indicator.CVOL(Input);
		}

		public Indicators.CVOL CVOL(ISeries<double> input )
		{
			return indicator.CVOL(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.CVOL CVOL()
		{
			return indicator.CVOL(Input);
		}

		public Indicators.CVOL CVOL(ISeries<double> input )
		{
			return indicator.CVOL(input);
		}
	}
}

#endregion
