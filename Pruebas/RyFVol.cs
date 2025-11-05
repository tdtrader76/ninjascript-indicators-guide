#region Using declarations
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
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class RyFVol : Indicator
    {
        // Constants for Dictionary cleanup and time matching
        private const int MAX_HISTORICAL_DAYS = 5;
        private const int MAX_DICTIONARY_SIZE = 5000;
        private const int TIME_MATCH_TOLERANCE_MINUTES = 30;
        private const int CLEANUP_FREQUENCY_BARS = 100;

        private int totalVolumeOpacity;
        private double threshold = 0.3;
        private int labelFontSize = 16;
        private int emaPeriod = 21;
        private double lastAD = 0;
        private double highVolumeThreshold = 3.0;
        private double mediumVolumeThreshold = 2.0;

        // CVOL2 integration
        private Dictionary<DateTime, double> historicalVolumes = new Dictionary<DateTime, double>();

        // EMA indicator
        private EMA emaIndicator;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Volume indicator with Effective Volume and comparative volume analysis (CVOL2). Colors bars based on volume comparison with previous day.";
                Name = "RyFVol";
                Calculate = Calculate.OnBarClose;
                IsOverlay = false;
                DisplayInDataBox = true;
                DrawOnPricePanel = false;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = false;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // Default values
                TotalVolumeOpacity = 50;
                Threshold = 0.3;
                LabelFontSize = 16;
                EmaPeriod = 21;
                HighVolumeThreshold = 3.0;
                MediumVolumeThreshold = 2.0;
				GreenDotThreshold = 1.5;
				WhiteDotThreshold = 2.0;

                // Add plots
                AddPlot(new Stroke(Brushes.CornflowerBlue, 8), PlotStyle.Bar, "TotalVolume");
                AddPlot(new Stroke(Brushes.ForestGreen, 3), PlotStyle.Bar, "EffectiveVolume");
                AddPlot(new Stroke(Brushes.Orange, 2), PlotStyle.Line, "VolumeEMA");
            }
            else if (State == State.Configure)
            {
                // Apply opacity to total volume plot (Plot 0)
                byte alpha = (byte)((TotalVolumeOpacity / 100.0) * 255);
                Plots[0].Brush = new SolidColorBrush(Color.FromArgb(alpha, Colors.CornflowerBlue.R, Colors.CornflowerBlue.G, Colors.CornflowerBlue.B));
                Plots[0].Brush.Freeze();
            }
            else if (State == State.DataLoaded)
            {
                // Initialize EMA indicator on the volume series
                emaIndicator = EMA(VOL(), EmaPeriod);
            }
            else if (State == State.Terminated)
            {
                // Cleanup resources
                if (historicalVolumes != null)
                {
                    historicalVolumes.Clear();
                    historicalVolumes = null;
                }
            }
        }

        protected override void OnBarUpdate()
        {
            try
            {
                if (CurrentBar < 1)
                    return;

                double volume = Volume[0];

                // Calculate hi: max between previous close and current high
                double hi = Math.Max(Close[1], High[0]);

                // Calculate lo: min between previous close and current low
                double lo = Math.Min(Close[1], Low[0]);

                // Calculate effective volume (accumulation/distribution)
                double ad = 0;
                double range = hi - lo;
                if (range > double.Epsilon)
                {
                    // Formula: When price rises (Close[0] > Close[1]), AD is positive (accumulation)
                    ad = ((Close[0] - Close[1]) / range) * volume;
                }

                // Store for label display
                lastAD = ad;

            // CVOL2 logic: Compare with previous day volume
            DateTime currentTime = Time[0];

            // Store current volume
            historicalVolumes[currentTime] = volume;

            // Periodic cleanup of historical data (every CLEANUP_FREQUENCY_BARS bars)
            if (CurrentBar % CLEANUP_FREQUENCY_BARS == 0)
            {
                DateTime cutoffDate = currentTime.AddDays(-MAX_HISTORICAL_DAYS);

                // Remove data older than MAX_HISTORICAL_DAYS
                var keysToRemove = historicalVolumes.Keys
                    .Where(k => k < cutoffDate)
                    .ToList();

                foreach (var key in keysToRemove)
                    historicalVolumes.Remove(key);

                // If dictionary is too large, keep only last MAX_DICTIONARY_SIZE items
                if (historicalVolumes.Count > MAX_DICTIONARY_SIZE * 2)
                {
                    var oldKeysToRemove = historicalVolumes.Keys
                        .OrderBy(k => k)
                        .Take(historicalVolumes.Count - MAX_DICTIONARY_SIZE)
                        .ToList();

                    foreach (var key in oldKeysToRemove)
                        historicalVolumes.Remove(key);
                }
            }

            // Find volume at same time on previous day
            DateTime previousDayTime = currentTime.AddDays(-1);
            double previousVolume = 0;

            if (historicalVolumes.ContainsKey(previousDayTime))
            {
                previousVolume = historicalVolumes[previousDayTime];
            }
            else
            {
                // Search for closest bar within TIME_MATCH_TOLERANCE_MINUTES
                DateTime closestTime = FindClosestVolumeTime(previousDayTime, TIME_MATCH_TOLERANCE_MINUTES);

                if (closestTime != DateTime.MinValue)
                {
                    previousVolume = historicalVolumes[closestTime];
                }
            }

            // Calculate volume ratio
            double volumeRatio = 1.0;
            if (previousVolume > 0)
            {
                volumeRatio = volume / previousVolume;
            }

            // Set plot values
            // Plot 0: Total volume with color based on EMA comparison
            Values[0][0] = volume;

            // Plot 2: EMA calculation (if we have enough bars)
            if (CurrentBar >= EmaPeriod)
            {
                Values[2][0] = emaIndicator[0];
            }

            // Color volume bars: Priority to previous day comparison, then EMA
            if (volumeRatio > HighVolumeThreshold)
            {
                // More than HighVolumeThreshold times volume compared to previous day: Orange
                PlotBrushes[0][0] = Brushes.Orange;
            }
            else if (volumeRatio > MediumVolumeThreshold)
            {
                // Between MediumVolumeThreshold and HighVolumeThreshold times volume: White
                PlotBrushes[0][0] = Brushes.White;
            }
            else if (CurrentBar >= EmaPeriod && Values[0][0] >= emaIndicator[0])
            {
                // Less than MediumVolumeThreshold but volume is above or equal to EMA: DodgerBlue
                PlotBrushes[0][0] = Brushes.DodgerBlue;
            }
            else
            {
                // Less than MediumVolumeThreshold and below EMA (or not enough bars for EMA): DimGray
                PlotBrushes[0][0] = Brushes.DimGray;
            }

            // Plot 1: Effective Volume with threshold filter
            double volumeThreshold = volume * Threshold;

            if (Math.Abs(ad) > volumeThreshold)
                {
                    // If ad is positive, price went up - accumulation (buying) - ForestGreen
                    // If ad is negative, price went down - distribution (selling) - IndianRed
                    // Both bars go in same direction (positive), so we use absolute value

                    if (ad > 0)
                    {
                        // Accumulation (buying) - ForestGreen
                        Values[1][0] = Math.Abs(ad);
                        PlotBrushes[1][0] = Brushes.ForestGreen;
                    }
                    else
                    {
                        // Distribution (selling) - IndianRed
                        Values[1][0] = Math.Abs(ad);
                        PlotBrushes[1][0] = Brushes.IndianRed;
                    }
                }
                else
                {
                    Values[1][0] = 0;
                }

				// --- Lógica de puntos mejorada ---
				if (CurrentBar >= EmaPeriod)
				{
					double emaValue = emaIndicator[1];
					double volumeValue = Volume[0];
					double offset = 10 * TickSize;

					double higherThreshold = Math.Max(GreenDotThreshold, WhiteDotThreshold);
					double lowerThreshold = Math.Min(GreenDotThreshold, WhiteDotThreshold);

					Brush higherBrush = GreenDotThreshold > WhiteDotThreshold ? Brushes.Green : Brushes.White;
					Brush lowerBrush = GreenDotThreshold < WhiteDotThreshold ? Brushes.Green : Brushes.White;

					if (volumeValue > emaValue * higherThreshold)
					{
						Draw.Dot(this, "VolumeSpike" + CurrentBar, false, 0, volumeValue + offset, higherBrush);
					}
					else if (volumeValue > emaValue * lowerThreshold)
					{
						Draw.Dot(this, "VolumeSpike" + CurrentBar, false, 0, volumeValue + offset, lowerBrush);
					}
				}
            }
            catch (Exception ex)
            {
                Log($"RyFVol OnBarUpdate error: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (Bars == null || ChartControl == null || CurrentBar < 1)
                return;

            // Get the chart panel
            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];

            // Calculate position: 25 pixels to the right of last bar
            double lastBarX = chartControl.GetXByBarIndex(ChartBars, ChartBars.ToIndex);
            double labelX = lastBarX + 25;

            // Position label at middle of Y axis range
            double yMax = chartScale.MaxValue;
            double yMin = chartScale.MinValue;
            double yMid = (yMax + yMin) / 2;
            double labelY = chartScale.GetYByValue(yMid);

            // Format the label text (positive = accumulation, negative = distribution)
            string labelText = lastAD.ToString("F0");

            // Choose color based on sign (positive ad = accumulation = green, negative ad = distribution = red)
            SharpDX.Color labelColor = lastAD > 0 ? SharpDX.Color.ForestGreen : SharpDX.Color.IndianRed;

            // Set font properties
            if (Core.Globals.DirectWriteFactory == null || RenderTarget == null)
                return;

            string fontFamily = "Arial";
            float fontSize = (float)LabelFontSize;

            using (SharpDX.DirectWrite.TextFormat textFormat = new SharpDX.DirectWrite.TextFormat(
                Core.Globals.DirectWriteFactory, fontFamily,
                SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, fontSize))
            {
                textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
                textFormat.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

                using (SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(
                    Core.Globals.DirectWriteFactory, labelText, textFormat, 200, fontSize))
                {
                    using (SharpDX.Direct2D1.SolidColorBrush brush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, labelColor))
                    {
                        RenderTarget.DrawTextLayout(
                            new SharpDX.Vector2((float)labelX, (float)labelY),
                            textLayout,
                            brush,
                            SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to find the closest volume time within tolerance
        /// </summary>
        private DateTime FindClosestVolumeTime(DateTime targetTime, int toleranceMinutes)
        {
            return historicalVolumes.Keys
                .Where(k => k.Date == targetTime.Date &&
                            Math.Abs((k - targetTime).TotalMinutes) <= toleranceMinutes)
                .OrderBy(k => Math.Abs((k - targetTime).TotalMinutes))
                .FirstOrDefault();
        }

        #region Properties
        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Total Volume Opacity", Description = "Opacity of the total volume bar (0-100%)", Order = 1, GroupName = "Visual")]
        public int TotalVolumeOpacity
        {
            get { return totalVolumeOpacity; }
            set { totalVolumeOpacity = Math.Max(0, Math.Min(100, value)); }
        }

        [NinjaScriptProperty]
        [Range(0.0, 1.0)]
        [Display(Name = "Threshold", Description = "Threshold percentage for filtering effective volume (0.3 = 30%)", Order = 2, GroupName = "Parameters")]
        public double Threshold
        {
            get { return threshold; }
            set { threshold = Math.Max(0.0, Math.Min(1.0, value)); }
        }

        [NinjaScriptProperty]
        [Range(8, 72)]
        [Display(Name = "Label Font Size", Description = "Font size for the volume label", Order = 3, GroupName = "Visual")]
        public int LabelFontSize
        {
            get { return labelFontSize; }
            set { labelFontSize = Math.Max(8, Math.Min(72, value)); }
        }

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "EMA Period", Description = "Period for the volume EMA", Order = 3, GroupName = "Parameters")]
        public int EmaPeriod
        {
            get { return emaPeriod; }
            set { emaPeriod = Math.Max(1, Math.Min(200, value)); }
        }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "High Volume Threshold", Description = "Ratio threshold for high volume (orange color)", Order = 4, GroupName = "Parameters")]
        public double HighVolumeThreshold
        {
            get { return highVolumeThreshold; }
            set { highVolumeThreshold = Math.Max(1.0, Math.Min(10.0, value)); }
        }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "Medium Volume Threshold", Description = "Ratio threshold for medium volume (white color)", Order = 5, GroupName = "Parameters")]
        public double MediumVolumeThreshold
        {
            get { return mediumVolumeThreshold; }
            set { mediumVolumeThreshold = Math.Max(1.0, Math.Min(10.0, value)); }
        }

        [NinjaScriptProperty]
        [Range(0.1, 10.0)]
        [Display(Name = "Green Dot Threshold", Description = "Multiplier for EMA to draw a green dot", Order = 6, GroupName = "Parameters")]
        public double GreenDotThreshold
        {
            get; set;
        }

        [NinjaScriptProperty]
        [Range(0.1, 10.0)]
        [Display(Name = "White Dot Threshold", Description = "Multiplier for EMA to draw a white dot", Order = 7, GroupName = "Parameters")]
        public double WhiteDotThreshold
        {
            get; set;
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> TotalVolume
        {
            get { return Values[0]; }
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> EffectiveVolume
        {
            get { return Values[1]; }
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> VolumeEMA
        {
            get { return Values[2]; }
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private RyFVol[] cacheRyFVol;
		public RyFVol RyFVol(int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double greenDotThreshold, double whiteDotThreshold)
		{
			return RyFVol(Input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, greenDotThreshold, whiteDotThreshold);
		}

		public RyFVol RyFVol(ISeries<double> input, int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double greenDotThreshold, double whiteDotThreshold)
		{
			if (cacheRyFVol != null)
				for (int idx = 0; idx < cacheRyFVol.Length; idx++)
					if (cacheRyFVol[idx] != null && cacheRyFVol[idx].TotalVolumeOpacity == totalVolumeOpacity && cacheRyFVol[idx].Threshold == threshold && cacheRyFVol[idx].LabelFontSize == labelFontSize && cacheRyFVol[idx].EmaPeriod == emaPeriod && cacheRyFVol[idx].HighVolumeThreshold == highVolumeThreshold && cacheRyFVol[idx].MediumVolumeThreshold == mediumVolumeThreshold && cacheRyFVol[idx].GreenDotThreshold == greenDotThreshold && cacheRyFVol[idx].WhiteDotThreshold == whiteDotThreshold && cacheRyFVol[idx].EqualsInput(input))
						return cacheRyFVol[idx];
			return CacheIndicator<RyFVol>(new RyFVol(){ TotalVolumeOpacity = totalVolumeOpacity, Threshold = threshold, LabelFontSize = labelFontSize, EmaPeriod = emaPeriod, HighVolumeThreshold = highVolumeThreshold, MediumVolumeThreshold = mediumVolumeThreshold, GreenDotThreshold = greenDotThreshold, WhiteDotThreshold = whiteDotThreshold }, input, ref cacheRyFVol);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RyFVol RyFVol(int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double greenDotThreshold, double whiteDotThreshold)
		{
			return indicator.RyFVol(Input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, greenDotThreshold, whiteDotThreshold);
		}

		public Indicators.RyFVol RyFVol(ISeries<double> input , int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double greenDotThreshold, double whiteDotThreshold)
		{
			return indicator.RyFVol(input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, greenDotThreshold, whiteDotThreshold);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RyFVol RyFVol(int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double greenDotThreshold, double whiteDotThreshold)
		{
			return indicator.RyFVol(Input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, greenDotThreshold, whiteDotThreshold);
		}

		public Indicators.RyFVol RyFVol(ISeries<double> input , int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double greenDotThreshold, double whiteDotThreshold)
		{
			return indicator.RyFVol(input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, greenDotThreshold, whiteDotThreshold);
		}
	}
}

#endregion
