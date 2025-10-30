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
    public class RyFVolPro : Indicator
    {
        #region Nested Helper Classes
        private interface IVolumeCalculator
        {
            VolumeCalculationResult CalculateEffectiveVolume(double currentClose, double previousClose,
                                                           double currentHigh, double currentLow, double volume);
        }

        private class VolumeCalculationResult
        {
            public double Volume { get; set; }
            public double EffectiveVolume { get; set; }
            public double AccumulationDistribution { get; set; }
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
        }

        private class VolumeCalculatorService : IVolumeCalculator
        {
            public VolumeCalculationResult CalculateEffectiveVolume(double currentClose, double previousClose,
                                                                   double currentHigh, double currentLow, double volume)
            {
                try
                {
                    if (volume <= 0)
                        return new VolumeCalculationResult { IsValid = false, ErrorMessage = "Invalid volume data" };

                    if (double.IsNaN(currentClose) || double.IsNaN(previousClose) ||
                        double.IsNaN(currentHigh) || double.IsNaN(currentLow))
                    {
                        return new VolumeCalculationResult { IsValid = false, ErrorMessage = "Invalid price data" };
                    }

                    double hi = Math.Max(previousClose, currentHigh);
                    double lo = Math.Min(previousClose, currentLow);
                    double ad = 0;
                    double range = hi - lo;

                    if (range > 0.000001)
                    {
                        ad = ((currentClose - previousClose) / range) * volume;
                    }

                    return new VolumeCalculationResult
                    {
                        Volume = volume,
                        EffectiveVolume = Math.Abs(ad),
                        AccumulationDistribution = ad,
                        IsValid = true
                    };
                }
                catch (Exception ex)
                {
                    return new VolumeCalculationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"Error calculating effective volume: {ex.Message}"
                    };
                }
            }
        }
        #endregion

        #region Constants
        private const int DEFAULT_LABEL_FONT_SIZE = 16;
        private const int DEFAULT_TOTAL_VOLUME_OPACITY = 50;
        private const double DEFAULT_THRESHOLD = 0.3;
        #endregion

        #region Variables
        private readonly IVolumeCalculator volumeCalculator;
        private int totalVolumeOpacity;
        private double threshold;
        private int labelFontSize;
        private double lastAccumulationDistribution;
        private int referencePeriodDays;
        private double highVolumeThresholdRatio;
        private double[,,] historicalVolume;
        private int[,] count;
        private SessionIterator sessionIterator;
        private bool isIntraday;
        private TimeZoneInfo instrumentTimeZone;

        // Cumulated Ratio
        private bool showCumulatedRatio;
        private Brush highRatioBrush;
        private Brush lowRatioBrush;
        private Brush textBrush;
        private Brush textBoxBrush;
        private int textBoxOpacity;
        private Series<double> cumulVolume;
        private Series<double> cumulReferenceVolume;
        private Series<double> averageVolume;
        #endregion

        public RyFVolPro()
        {
            volumeCalculator = new VolumeCalculatorService();
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Combines Effective Volume with Relative Volume analysis, coloring bars based on a historical average.";
                Name = "RyFVolPro";
                Calculate = Calculate.OnBarClose;
                IsOverlay = false;
                DisplayInDataBox = true;
                DrawOnPricePanel = false;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = false;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                totalVolumeOpacity = DEFAULT_TOTAL_VOLUME_OPACITY;
                threshold = DEFAULT_THRESHOLD;
                labelFontSize = DEFAULT_LABEL_FONT_SIZE;
                referencePeriodDays = 40;
                highVolumeThresholdRatio = 1.75;

                AddPlot(new Stroke(Brushes.CornflowerBlue, 8), PlotStyle.Bar, "TotalVolume");
                AddPlot(new Stroke(Brushes.ForestGreen, 3), PlotStyle.Bar, "EffectiveVolume");
                AddPlot(new Stroke(Brushes.Orange, 2), PlotStyle.Line, "CumulatedRatio");
                AddLine(Brushes.Gray, 100, "Zeroline");

                showCumulatedRatio = true;
                highRatioBrush = Brushes.PaleGreen;
                lowRatioBrush = Brushes.Yellow;
                textBrush = Brushes.Black;
                textBoxBrush = Brushes.Lavender;
                textBoxOpacity = 70;
            }
            else if (State == State.Configure)
            {
                byte alpha = (byte)((totalVolumeOpacity / 100.0) * 255);
                Plots[0].Brush = new SolidColorBrush(Color.FromArgb(alpha, Colors.CornflowerBlue.R, Colors.CornflowerBlue.G, Colors.CornflowerBlue.B));
                Plots[0].Brush.Freeze();
            }
            else if (State == State.DataLoaded)
            {
                cumulVolume = new Series<double>(this, MaximumBarsLookBack.Infinite);
                cumulReferenceVolume = new Series<double>(this, MaximumBarsLookBack.Infinite);
                averageVolume = new Series<double>(this, MaximumBarsLookBack.Infinite);
            }
            else if (State == State.Historical)
            {
                historicalVolume = new double[1, 1440, referencePeriodDays];
                count = new int[1, 1440];
                for (int i = 0; i < 1440; i++)
                {
                    for (int k = 0; k < referencePeriodDays; k++)
                        historicalVolume[0, i, k] = 0.0;
                    count[0, i] = 0;
                }
                sessionIterator = new SessionIterator(Bars);
                instrumentTimeZone = Instrument.MasterInstrument.TradingHours.TimeZoneInfo;
                isIntraday = Bars.BarsPeriod.BarsPeriodType == BarsPeriodType.Minute;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar == 0) return;

            DateTime exchangeTime = TimeZoneInfo.ConvertTime(Time[1], Core.Globals.GeneralOptions.TimeZoneInfo, instrumentTimeZone);
            int timeIndex = isIntraday ? 60 * exchangeTime.Hour + exchangeTime.Minute : 0;

            double volSum = 0;
            for (int i = referencePeriodDays - 1; i > 0; i--)
            {
                historicalVolume[0, timeIndex, i] = historicalVolume[0, timeIndex, i - 1];
                volSum += historicalVolume[0, timeIndex, i];
            }
            historicalVolume[0, timeIndex, 0] = Volume[1];
            volSum += historicalVolume[0, timeIndex, 0];
            count[0, timeIndex] += 1;

            double avgVolume = volSum / Math.Min(referencePeriodDays, count[0, timeIndex]);

            var volumeResult = volumeCalculator.CalculateEffectiveVolume(
                Close[0], Close[1], High[0], Low[0], Volume[0]);

            if (!volumeResult.IsValid)
            {
                Log($"RyFVolPro calculation error: {volumeResult.ErrorMessage}", LogLevel.Error);
                return;
            }

            lastAccumulationDistribution = volumeResult.AccumulationDistribution;

            if (avgVolume > 0)
            {
                double relativeVolume = 100 * volumeResult.Volume / avgVolume;
                TotalVolume[0] = relativeVolume;
                EffectiveVolume[0] = 100 * volumeResult.EffectiveVolume / avgVolume;
            }
            else
            {
                TotalVolume[0] = 0;
                EffectiveVolume[0] = 0;
            }

            averageVolume[0] = avgVolume;

            sessionIterator.GetNextSession(Time[0], true);
            if (sessionIterator.IsNewSession(Time[0], true))
            {
                cumulVolume[0] = volumeResult.Volume;
                cumulReferenceVolume[0] = avgVolume;
            }
            else
            {
                cumulVolume[0] = (cumulVolume.IsValidDataPoint(1) ? cumulVolume[1] : 0) + volumeResult.Volume;
                cumulReferenceVolume[0] = (cumulReferenceVolume.IsValidDataPoint(1) ? cumulReferenceVolume[1] : 0) + avgVolume;
            }

            if (cumulReferenceVolume[0] > 0)
            {
                CumulatedRatio[0] = 100 * cumulVolume[0] / cumulReferenceVolume[0];
                PlotBrushes[2][0] = CumulatedRatio[0] > 100 ? highRatioBrush : lowRatioBrush;
            }

            ApplyVolumeColoring(volumeResult, avgVolume);
        }

        private void ApplyVolumeColoring(VolumeCalculationResult volumeResult, double avgVolume)
        {
            if (avgVolume > 0 && (volumeResult.Volume / avgVolume) > highVolumeThresholdRatio)
            {
                PlotBrushes[0][0] = Brushes.RoyalBlue;
            }
            else
            {
                PlotBrushes[0][0] = Brushes.DimGray;
            }

            ApplyEffectiveVolumeColoring(volumeResult, avgVolume);
        }

        private void ApplyEffectiveVolumeColoring(VolumeCalculationResult volumeResult, double avgVolume)
        {
            double volumeThreshold = volumeResult.Volume * threshold;

            if (avgVolume > 0)
                Values[1][0] = 100 * volumeResult.EffectiveVolume / avgVolume;
            else
                Values[1][0] = 0;

            if (Math.Abs(volumeResult.AccumulationDistribution) > volumeThreshold)
            {
                if (volumeResult.AccumulationDistribution > 0)
                {
                    PlotBrushes[1][0] = Brushes.ForestGreen;
                }
                else
                {
                    PlotBrushes[1][0] = Brushes.IndianRed;
                }
            }
            else
            {
                PlotBrushes[1][0] = Brushes.DimGray;
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (Bars == null || ChartControl == null || CurrentBar < 1 || Core.Globals.DirectWriteFactory == null || RenderTarget == null)
                return;

            RenderVolumeLabel(chartControl, chartScale);

            if (showCumulatedRatio && CumulatedRatio.IsValidDataPoint(0))
            {
                RenderCumulatedRatioLabel(chartControl, chartScale);
            }
        }

        private void RenderCumulatedRatioLabel(ChartControl chartControl, ChartScale chartScale)
        {
            int lastBarIndex = Bars.Count - 1;
            double lastValue = CumulatedRatio[0];
            string labelText = $"{lastValue:F0}%";

            Brush highRatioBrushOpaque = highRatioBrush.Clone();
            highRatioBrushOpaque.Opacity = (float)textBoxOpacity / 100.0;
            highRatioBrushOpaque.Freeze();

            Brush lowRatioBrushOpaque = lowRatioBrush.Clone();
            lowRatioBrushOpaque.Opacity = (float)textBoxOpacity / 100.0;
            lowRatioBrushOpaque.Freeze();

            using (var textFormat = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial", SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, (float)labelFontSize))
            {
                using (var textLayout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, labelText, textFormat, 200, (float)labelFontSize))
                {
                    double x = chartControl.GetXByBarIndex(ChartBars, lastBarIndex) + 25;
                    double y = chartScale.GetYByValue(lastValue) - textLayout.Metrics.Height / 2;

                    SharpDX.RectangleF rect = new SharpDX.RectangleF((float)x, (float)y, textLayout.Metrics.Width, textLayout.Metrics.Height);

                    using (var backgroundBrush = (lastValue > 100 ? highRatioBrushOpaque : lowRatioBrushOpaque).ToDxBrush(RenderTarget))
                    using (var textDXBrush = textBrush.ToDxBrush(RenderTarget))
                    {
                        RenderTarget.FillRectangle(rect, backgroundBrush);
                        RenderTarget.DrawTextLayout(new SharpDX.Vector2((float)x, (float)y), textLayout, textDXBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                    }
                }
            }
        }

        private void RenderVolumeLabel(ChartControl chartControl, ChartScale chartScale)
        {
            double lastBarX = chartControl.GetXByBarIndex(ChartBars, ChartBars.ToIndex);
            double labelX = lastBarX + 25;

            double yMid = (chartScale.MaxValue + chartScale.MinValue) / 2;
            double labelY = chartScale.GetYByValue(yMid);

            string labelText = lastAccumulationDistribution.ToString("F0");
            SharpDX.Color labelColor = lastAccumulationDistribution > 0 ? SharpDX.Color.ForestGreen : SharpDX.Color.IndianRed;

            using (var textFormat = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial", SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, (float)labelFontSize))
            using (var textLayout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, labelText, textFormat, 200, (float)labelFontSize))
            using (var brush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, labelColor))
            {
                RenderTarget.DrawTextLayout(new SharpDX.Vector2((float)labelX, (float)labelY), textLayout, brush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
            }
        }

        #region Properties
        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Total Volume Opacity", Description = "Opacity of the total volume bar (0-100%)", Order = 1, GroupName = "Visual")]
        public int TotalVolumeOpacity
        {
            get => totalVolumeOpacity;
            set => totalVolumeOpacity = Math.Max(0, Math.Min(100, value));
        }

        [NinjaScriptProperty]
        [Range(0.0, 1.0)]
        [Display(Name = "Threshold", Description = "Threshold percentage for filtering effective volume (0.3 = 30%)", Order = 2, GroupName = "Parameters")]
        public double Threshold
        {
            get => threshold;
            set => threshold = Math.Max(0.0, Math.Min(1.0, value));
        }

        [NinjaScriptProperty]
        [Range(8, 72)]
        [Display(Name = "Label Font Size", Description = "Font size for the volume label", Order = 3, GroupName = "Visual")]
        public int LabelFontSize
        {
            get => labelFontSize;
            set => labelFontSize = Math.Max(8, Math.Min(72, value));
        }

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "Reference Period (Days)", Description = "Number of past days to calculate the average volume.", Order = 9, GroupName = "Parameters")]
        public int ReferencePeriodDays
        {
            get => referencePeriodDays;
            set => referencePeriodDays = Math.Max(1, value);
        }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "High Volume Threshold (Ratio)", Description = "Volume ratio threshold to color the bar RoyalBlue (e.g., 1.75 for 175%).", Order = 10, GroupName = "Parameters")]
        public double HighVolumeThresholdRatio
        {
            get => highVolumeThresholdRatio;
            set => highVolumeThresholdRatio = Math.Max(1.0, value);
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> TotalVolume => Values[0];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> EffectiveVolume => Values[1];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> CumulatedRatio => Values[2];

        [NinjaScriptProperty]
        [Display(Name = "Show Cumulated Ratio", Description = "Show the cumulated volume ratio line and text box.", Order = 11, GroupName = "Visual")]
        public bool ShowCumulatedRatio
        {
            get => showCumulatedRatio;
            set => showCumulatedRatio = value;
        }

        [XmlIgnore]
        [Display(Name = "High Ratio Color", Description = "Color for the cumulated ratio line when above 100%.", Order = 12, GroupName = "Visual")]
        public Brush HighRatioBrush
        {
            get => highRatioBrush;
            set => highRatioBrush = value;
        }

        [Browsable(false)]
        public string HighRatioBrushSerializable
        {
            get => Serialize.BrushToString(highRatioBrush);
            set => highRatioBrush = Serialize.StringToBrush(value);
        }

        [XmlIgnore]
        [Display(Name = "Low Ratio Color", Description = "Color for the cumulated ratio line when at or below 100%.", Order = 13, GroupName = "Visual")]
        public Brush LowRatioBrush
        {
            get => lowRatioBrush;
            set => lowRatioBrush = value;
        }

        [Browsable(false)]
        public string LowRatioBrushSerializable
        {
            get => Serialize.BrushToString(lowRatioBrush);
            set => lowRatioBrush = Serialize.StringToBrush(value);
        }

        [XmlIgnore]
        [Display(Name = "Text Color", Description = "Color for the text in the cumulated ratio box.", Order = 14, GroupName = "Visual")]
        public Brush TextBrush
        {
            get => textBrush;
            set => textBrush = value;
        }

        [Browsable(false)]
        public string TextBrushSerializable
        {
            get => Serialize.BrushToString(textBrush);
            set => textBrush = Serialize.StringToBrush(value);
        }

        [XmlIgnore]
        [Display(Name = "Text Box Color", Description = "Color for the background of the cumulated ratio box.", Order = 15, GroupName = "Visual")]
        public Brush TextBoxBrush
        {
            get => textBoxBrush;
            set => textBoxBrush = value;
        }

        [Browsable(false)]
        public string TextBoxBrushSerializable
        {
            get => Serialize.BrushToString(textBoxBrush);
            set => textBoxBrush = Serialize.StringToBrush(value);
        }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Text Box Opacity", Description = "Opacity for the cumulated ratio text box.", Order = 16, GroupName = "Visual")]
        public int TextBoxOpacity
        {
            get => textBoxOpacity;
            set => textBoxOpacity = Math.Max(0, Math.Min(100, value));
        }

        [XmlIgnore]
        [Display(Name = "Zeroline Color", Description = "Color for the 100% reference line.", Order = 17, GroupName = "Visual")]
        public Brush ZerolineBrush
        {
            get => Lines[0].Brush;
            set => Lines[0].Brush = value;
        }

        [Browsable(false)]
        public string ZerolineBrushSerializable
        {
            get => Serialize.BrushToString(Lines[0].Brush);
            set => Lines[0].Brush = Serialize.StringToBrush(value);
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private RyFVolPro[] cacheRyFVolPro;
		public RyFVolPro RyFVolPro(int totalVolumeOpacity, double threshold, int labelFontSize, int referencePeriodDays, double highVolumeThresholdRatio, bool showCumulatedRatio, int textBoxOpacity)
		{
			return RyFVolPro(Input, totalVolumeOpacity, threshold, labelFontSize, referencePeriodDays, highVolumeThresholdRatio, showCumulatedRatio, textBoxOpacity);
		}

		public RyFVolPro RyFVolPro(ISeries<double> input, int totalVolumeOpacity, double threshold, int labelFontSize, int referencePeriodDays, double highVolumeThresholdRatio, bool showCumulatedRatio, int textBoxOpacity)
		{
			if (cacheRyFVolPro != null)
				for (int idx = 0; idx < cacheRyFVolPro.Length; idx++)
					if (cacheRyFVolPro[idx] != null && cacheRyFVolPro[idx].TotalVolumeOpacity == totalVolumeOpacity && cacheRyFVolPro[idx].Threshold == threshold && cacheRyFVolPro[idx].LabelFontSize == labelFontSize && cacheRyFVolPro[idx].ReferencePeriodDays == referencePeriodDays && cacheRyFVolPro[idx].HighVolumeThresholdRatio == highVolumeThresholdRatio && cacheRyFVolPro[idx].ShowCumulatedRatio == showCumulatedRatio && cacheRyFVolPro[idx].TextBoxOpacity == textBoxOpacity && cacheRyFVolPro[idx].EqualsInput(input))
						return cacheRyFVolPro[idx];
			return CacheIndicator<RyFVolPro>(new RyFVolPro(){ TotalVolumeOpacity = totalVolumeOpacity, Threshold = threshold, LabelFontSize = labelFontSize, ReferencePeriodDays = referencePeriodDays, HighVolumeThresholdRatio = highVolumeThresholdRatio, ShowCumulatedRatio = showCumulatedRatio, TextBoxOpacity = textBoxOpacity }, input, ref cacheRyFVolPro);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RyFVolPro RyFVolPro(int totalVolumeOpacity, double threshold, int labelFontSize, int referencePeriodDays, double highVolumeThresholdRatio, bool showCumulatedRatio, int textBoxOpacity)
		{
			return indicator.RyFVolPro(Input, totalVolumeOpacity, threshold, labelFontSize, referencePeriodDays, highVolumeThresholdRatio, showCumulatedRatio, textBoxOpacity);
		}

		public Indicators.RyFVolPro RyFVolPro(ISeries<double> input , int totalVolumeOpacity, double threshold, int labelFontSize, int referencePeriodDays, double highVolumeThresholdRatio, bool showCumulatedRatio, int textBoxOpacity)
		{
			return indicator.RyFVolPro(input, totalVolumeOpacity, threshold, labelFontSize, referencePeriodDays, highVolumeThresholdRatio, showCumulatedRatio, textBoxOpacity);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RyFVolPro RyFVolPro(int totalVolumeOpacity, double threshold, int labelFontSize, int referencePeriodDays, double highVolumeThresholdRatio, bool showCumulatedRatio, int textBoxOpacity)
		{
			return indicator.RyFVolPro(Input, totalVolumeOpacity, threshold, labelFontSize, referencePeriodDays, highVolumeThresholdRatio, showCumulatedRatio, textBoxOpacity);
		}

		public Indicators.RyFVolPro RyFVolPro(ISeries<double> input , int totalVolumeOpacity, double threshold, int labelFontSize, int referencePeriodDays, double highVolumeThresholdRatio, bool showCumulatedRatio, int textBoxOpacity)
		{
			return indicator.RyFVolPro(input, totalVolumeOpacity, threshold, labelFontSize, referencePeriodDays, highVolumeThresholdRatio, showCumulatedRatio, textBoxOpacity);
		}
	}
}

#endregion
