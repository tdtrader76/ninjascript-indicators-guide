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
    #region Constants
    /// <summary>
    /// Centralized constants for volume calculations and display
    /// </summary>
    internal static class VolumeConstants
    {
        // Historical data management
        public const int MAX_HISTORICAL_DAYS = 5;
        public const int MAX_DICTIONARY_SIZE = 5000;
        public const int TIME_MATCH_TOLERANCE_MINUTES = 30;
        public const int CLEANUP_FREQUENCY_BARS = 100;

        // Mathematical limits
        public const double DIVISION_LIMIT = 0.000001;

        // Volume thresholds
        public const double DEFAULT_HIGH_VOLUME_THRESHOLD = 3.0;
        public const double DEFAULT_MEDIUM_VOLUME_THRESHOLD = 2.0;

        // Rendering
        public const int DEFAULT_LABEL_OFFSET_X = 25;
        public const int LABEL_MAX_WIDTH = 200;
        public const string DEFAULT_FONT_FAMILY = "Arial";

        // Validation limits
        public const int MIN_OPACITY = 0;
        public const int MAX_OPACITY = 100;
        public const double MIN_THRESHOLD = 0.0;
        public const double MAX_THRESHOLD = 1.0;
        public const int MIN_FONT_SIZE = 8;
        public const int MAX_FONT_SIZE = 72;
        public const int MIN_EMA_PERIOD = 1;
        public const int MAX_EMA_PERIOD = 200;
        public const double MIN_VOLUME_THRESHOLD = 1.0;
        public const double MAX_VOLUME_THRESHOLD = 10.0;
    }
    #endregion

    #region Data Models
    /// <summary>
    /// Represents volume data with timestamp
    /// </summary>
    public class VolumeData
    {
        public DateTime Timestamp { get; set; }
        public double Volume { get; set; }
        public double EffectiveVolume { get; set; }
        public double AccumulationDistribution { get; set; }

        public VolumeData(DateTime timestamp, double volume, double effectiveVolume = 0, double ad = 0)
        {
            Timestamp = timestamp;
            Volume = volume;
            EffectiveVolume = effectiveVolume;
            AccumulationDistribution = ad;
        }
    }

    /// <summary>
    /// Represents volume calculation results
    /// </summary>
    public class VolumeCalculationResult
    {
        public double Volume { get; set; }
        public double EffectiveVolume { get; set; }
        public double AccumulationDistribution { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Represents volume comparison results
    /// </summary>
    public class VolumeComparisonResult
    {
        public double CurrentVolume { get; set; }
        public double PreviousVolume { get; set; }
        public double VolumeRatio { get; set; }
        public VolumeCategory Category { get; set; }
        public bool HasPreviousData { get; set; }
    }

    public enum VolumeCategory
    {
        Low,
        Medium,
        High,
        VeryHigh,
        NoData
    }
    #endregion

    #region Interfaces
    /// <summary>
    /// Interface for volume calculations
    /// </summary>
    public interface IVolumeCalculator
    {
        VolumeCalculationResult CalculateEffectiveVolume(double currentClose, double previousClose,
                                                       double currentHigh, double currentLow, double volume);
        VolumeComparisonResult CompareWithPreviousDay(VolumeData current, IEnumerable<VolumeData> historicalData,
                                                    double highThreshold, double mediumThreshold);
    }

    /// <summary>
    /// Interface for managing historical volume data
    /// </summary>
    public interface IHistoricalVolumeManager
    {
        void AddVolumeData(VolumeData data);
        VolumeData GetVolumeAt(DateTime timestamp);
        IEnumerable<VolumeData> GetAllVolumeData(); // Refactor: Add method to get all data
        VolumeData GetClosestVolume(DateTime targetTime, TimeSpan tolerance);
        void CleanupOldData();
        int GetCount();
        void Clear();
    }

    #endregion

    #region Services
    /// <summary>
    /// Service for calculating volume metrics
    /// </summary>
    public class VolumeCalculatorService : IVolumeCalculator
    {
        public VolumeCalculationResult CalculateEffectiveVolume(double currentClose, double previousClose,
                                                               double currentHigh, double currentLow, double volume)
        {
            try
            {
                // Validate inputs
                if (volume <= 0)
                    return new VolumeCalculationResult { IsValid = false, ErrorMessage = "Invalid volume data" };

                if (double.IsNaN(currentClose) || double.IsNaN(previousClose) ||
                    double.IsNaN(currentHigh) || double.IsNaN(currentLow))
                {
                    return new VolumeCalculationResult { IsValid = false, ErrorMessage = "Invalid price data" };
                }

                // Calculate hi: max between previous close and current high
                double hi = Math.Max(previousClose, currentHigh);

                // Calculate lo: min between previous close and current low
                double lo = Math.Min(previousClose, currentLow);

                // Calculate effective volume (accumulation/distribution)
                double ad = 0;
                double range = hi - lo;

                if (range > VolumeConstants.DIVISION_LIMIT)
                {
                    // Formula: When price rises (Close[0] > Close[1]), AD is positive (accumulation)
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

        public VolumeComparisonResult CompareWithPreviousDay(VolumeData current, IEnumerable<VolumeData> historicalData,
                                                           double highThreshold, double mediumThreshold)
        {
            if (current == null)
                return new VolumeComparisonResult { Category = VolumeCategory.NoData };

            DateTime previousDayTime = current.Timestamp.AddDays(-1);
            double previousVolume = 0;

            if (historicalData != null)
            {
                // Search for the closest bar in the previous day that matches the current bar's time
                var closestData = historicalData
                    .Where(d => d.Timestamp.Date == previousDayTime.Date &&
                                Math.Abs((d.Timestamp.TimeOfDay - current.Timestamp.TimeOfDay).TotalMinutes) <= VolumeConstants.TIME_MATCH_TOLERANCE_MINUTES)
                    .OrderBy(d => Math.Abs((d.Timestamp.TimeOfDay - current.Timestamp.TimeOfDay).TotalMinutes))
                    .FirstOrDefault();

                if (closestData != null)
                {
                    previousVolume = closestData.Volume;
                }
            }

            double volumeRatio = previousVolume > 0 ? current.Volume / previousVolume : 1.0;
            VolumeCategory category = ClassifyVolumeCategory(volumeRatio, highThreshold, mediumThreshold);

            return new VolumeComparisonResult
            {
                CurrentVolume = current.Volume,
                PreviousVolume = previousVolume,
                VolumeRatio = volumeRatio,
                Category = category,
                HasPreviousData = previousVolume > 0
            };
        }

        private VolumeCategory ClassifyVolumeCategory(double volumeRatio, double highThreshold, double mediumThreshold)
        {
            if (volumeRatio >= highThreshold)
                return VolumeCategory.VeryHigh;
            else if (volumeRatio >= mediumThreshold)
                return VolumeCategory.High;
            else if (volumeRatio >= 1.0)
                return VolumeCategory.Medium;
            else
                return VolumeCategory.Low;
        }
    }

    /// <summary>
    /// Service for managing historical volume data with efficient cleanup
    /// </summary>
    public class HistoricalVolumeManager : IHistoricalVolumeManager
    {
        private readonly Dictionary<DateTime, VolumeData> _volumeData = new Dictionary<DateTime, VolumeData>();
        private readonly object _lockObject = new object();

        public void AddVolumeData(VolumeData data)
        {
            if (data == null) return;

            lock (_lockObject)
            {
                _volumeData[data.Timestamp] = data;
            }
        }

        public VolumeData GetVolumeAt(DateTime timestamp)
        {
            lock (_lockObject)
            {
                return _volumeData.TryGetValue(timestamp, out VolumeData data) ? data : null;
            }
        }

        public IEnumerable<VolumeData> GetAllVolumeData()
        {
            lock (_lockObject)
            {
                return _volumeData.Values.ToList();
            }
        }

        public VolumeData GetClosestVolume(DateTime targetTime, TimeSpan tolerance)
        {
            lock (_lockObject)
            {
                return _volumeData.Values
                    .Where(d => d.Timestamp.Date == targetTime.Date &&
                                Math.Abs((d.Timestamp - targetTime).TotalMinutes) <= tolerance.TotalMinutes)
                    .OrderBy(d => Math.Abs((d.Timestamp - targetTime).TotalMinutes))
                    .FirstOrDefault();
            }
        }

        public void CleanupOldData()
        {
            lock (_lockObject)
            {
                DateTime cutoffDate = DateTime.Now.AddDays(-VolumeConstants.MAX_HISTORICAL_DAYS);

                // Remove data older than MAX_HISTORICAL_DAYS
                var keysToRemove = _volumeData.Keys
                    .Where(k => k < cutoffDate)
                    .ToList();

                foreach (var key in keysToRemove)
                    _volumeData.Remove(key);

                // If dictionary is too large, keep only last MAX_DICTIONARY_SIZE items
                if (_volumeData.Count > VolumeConstants.MAX_DICTIONARY_SIZE * 2)
                {
                    var oldKeysToRemove = _volumeData.Keys
                        .OrderBy(k => k)
                        .Take(_volumeData.Count - VolumeConstants.MAX_DICTIONARY_SIZE)
                        .ToList();

                    foreach (var key in oldKeysToRemove)
                        _volumeData.Remove(key);
                }
            }
        }

        public int GetCount()
        {
            lock (_lockObject)
            {
                return _volumeData.Count;
            }
        }

        public void Clear()
        {
            lock (_lockObject)
            {
                _volumeData.Clear();
            }
        }
    }

    #endregion

    public class RyFVol : Indicator
    {
        #region Constants
        private const int DEFAULT_LABEL_FONT_SIZE = 16;
        private const int DEFAULT_EMA_PERIOD = 21;
        private const double DEFAULT_THRESHOLD = 0.3;
        private const int DEFAULT_TOTAL_VOLUME_OPACITY = 50;
        #endregion

        #region Variables
        // Core services
        private readonly IVolumeCalculator volumeCalculator;
        private readonly IHistoricalVolumeManager historicalVolumeManager;

        // Configuration
        private int totalVolumeOpacity;
        private double threshold;
        private int labelFontSize;
        private int emaPeriod;
        private double lastAccumulationDistribution;

        // EMA indicator
        private EMA emaIndicator;

        // SMA indicator for white volume logic
        private SMA smaIndicator;

        // Backing fields for volume thresholds to prevent recursion
        private double highVolumeThreshold;
        private double mediumVolumeThreshold;

        // Backing fields for white volume logic
        private double whiteVolumeMultiplier;
        private int whiteVolumeSmaPeriod;
        #endregion

        public RyFVol()
        {
            // Initialize services
            volumeCalculator = new VolumeCalculatorService();
            historicalVolumeManager = new HistoricalVolumeManager();
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                SetDefaultValues();
            }
            else if (State == State.Configure)
            {
                ConfigurePlots();
            }
            else if (State == State.DataLoaded)
            {
                InitializeServices();
            }
            else if (State == State.Terminated)
            {
                CleanupResources();
            }
        }

        private void SetDefaultValues()
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

            // Set default configuration values
            totalVolumeOpacity = DEFAULT_TOTAL_VOLUME_OPACITY;
            threshold = DEFAULT_THRESHOLD;
            labelFontSize = DEFAULT_LABEL_FONT_SIZE;
            emaPeriod = DEFAULT_EMA_PERIOD;
            highVolumeThreshold = VolumeConstants.DEFAULT_HIGH_VOLUME_THRESHOLD;
            mediumVolumeThreshold = VolumeConstants.DEFAULT_MEDIUM_VOLUME_THRESHOLD;
            whiteVolumeMultiplier = 1.75;
            whiteVolumeSmaPeriod = 10;

            // Add plots
            AddPlot(new Stroke(Brushes.CornflowerBlue, 8), PlotStyle.Bar, "TotalVolume");
            AddPlot(new Stroke(Brushes.ForestGreen, 3), PlotStyle.Bar, "EffectiveVolume");
            AddPlot(new Stroke(Brushes.Orange, 2), PlotStyle.Line, "VolumeEMA");
        }

        private void ConfigurePlots()
        {
            // Apply opacity to total volume plot (Plot 0)
            byte alpha = (byte)((totalVolumeOpacity / 100.0) * 255);
            Plots[0].Brush = new SolidColorBrush(Color.FromArgb(alpha, Colors.CornflowerBlue.R, Colors.CornflowerBlue.G, Colors.CornflowerBlue.B));
            Plots[0].Brush.Freeze();
        }

        private void InitializeServices()
        {
            // Initialize EMA indicator on the volume series
            emaIndicator = EMA(VOL(), emaPeriod);

            // Initialize SMA indicator for white volume logic
            smaIndicator = SMA(VOL(), whiteVolumeSmaPeriod);
        }

        private void CleanupResources()
        {
            // Cleanup managed resources
            historicalVolumeManager?.Clear();
        }

        protected override void OnBarUpdate()
        {
            try
            {
                if (!ShouldProcessBar())
                    return;

                // Calculate effective volume
                var volumeResult = volumeCalculator.CalculateEffectiveVolume(
                    Close[0], Close[1], High[0], Low[0], Volume[0]);

                if (!volumeResult.IsValid)
                {
                    Log($"RyFVol calculation error: {volumeResult.ErrorMessage}", LogLevel.Error);
                    return;
                }

                // Store for label display
                lastAccumulationDistribution = volumeResult.AccumulationDistribution;

                // Update historical data
                UpdateHistoricalData(volumeResult);

                // Set plot values
                SetPlotValues(volumeResult);

                // Apply coloring
                ApplyVolumeColoring(volumeResult);
            }
            catch (Exception ex)
            {
                Log($"RyFVol OnBarUpdate error: {ex.Message}", LogLevel.Error);
            }
        }

        private bool ShouldProcessBar()
        {
            return CurrentBar >= 1;
        }

        private void UpdateHistoricalData(VolumeCalculationResult volumeResult)
        {
            var currentVolumeData = new VolumeData(
                Time[0],
                volumeResult.Volume,
                volumeResult.EffectiveVolume,
                volumeResult.AccumulationDistribution);

            historicalVolumeManager.AddVolumeData(currentVolumeData);

            // Periodic cleanup
            if (CurrentBar % VolumeConstants.CLEANUP_FREQUENCY_BARS == 0)
            {
                historicalVolumeManager.CleanupOldData();
            }
        }

        private void SetPlotValues(VolumeCalculationResult volumeResult)
        {
            // Plot 0: Total volume
            Values[0][0] = volumeResult.Volume;

            // Plot 2: EMA calculation (if we have enough bars)
            if (CurrentBar >= emaPeriod)
            {
                Values[2][0] = emaIndicator[0];
            }
        }

        private void ApplyVolumeColoring(VolumeCalculationResult volumeResult)
        {
            // Get historical comparison
            var currentVolumeData = new VolumeData(Time[0], volumeResult.Volume, volumeResult.EffectiveVolume, volumeResult.AccumulationDistribution);

            // Refactor: Get all historical data at once for better performance
            var historicalData = historicalVolumeManager.GetAllVolumeData();

            var comparison = volumeCalculator.CompareWithPreviousDay(currentVolumeData, historicalData, highVolumeThreshold, mediumVolumeThreshold);

            // Apply color to total volume bar
            double emaValue = CurrentBar >= emaPeriod ? emaIndicator[0] : 0;
            bool hasEma = CurrentBar >= emaPeriod;
            double smaValue = CurrentBar >= whiteVolumeSmaPeriod ? smaIndicator[0] : 0;
            bool hasSma = CurrentBar >= whiteVolumeSmaPeriod;

            // Directly apply coloring logic based on comparison
            if (comparison.Category == VolumeCategory.VeryHigh)
            {
                PlotBrushes[0][0] = Brushes.Orange;
            }
            else if (hasSma && volumeResult.Volume > smaValue * whiteVolumeMultiplier)
            {
                PlotBrushes[0][0] = Brushes.White;
            }
            else if (comparison.Category == VolumeCategory.Medium && hasEma && volumeResult.Volume >= emaValue)
            {
                PlotBrushes[0][0] = Brushes.DodgerBlue;
            }
            else
            {
                PlotBrushes[0][0] = Brushes.DimGray;
            }

            // Plot 1: Effective Volume with threshold filter
            ApplyEffectiveVolumeColoring(volumeResult);
        }

        private void ApplyEffectiveVolumeColoring(VolumeCalculationResult volumeResult)
        {
            double volumeThreshold = volumeResult.Volume * threshold;

            // Always plot effective volume to avoid empty bars
            Values[1][0] = volumeResult.EffectiveVolume;

            if (Math.Abs(volumeResult.AccumulationDistribution) > volumeThreshold)
            {
                // If effective volume is significant, color it by direction
                if (volumeResult.AccumulationDistribution > 0)
                {
                    // Accumulation (buying) - Green
                    PlotBrushes[1][0] = Brushes.ForestGreen;
                }
                else
                {
                    // Distribution (selling) - Red
                    PlotBrushes[1][0] = Brushes.IndianRed;
                }
            }
            else
            {
                // If effective volume is not significant, use a neutral color
                PlotBrushes[1][0] = Brushes.DimGray;
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (!ShouldRenderLabel(chartControl))
                return;

            RenderVolumeLabel(chartControl, chartScale);
        }

        private bool ShouldRenderLabel(ChartControl chartControl)
        {
            return Bars != null && ChartControl != null && CurrentBar >= 1 &&
                   Core.Globals.DirectWriteFactory != null && RenderTarget != null;
        }

        private void RenderVolumeLabel(ChartControl chartControl, ChartScale chartScale)
        {
            // Calculate position using constants
            double lastBarX = chartControl.GetXByBarIndex(ChartBars, ChartBars.ToIndex);
            double labelX = lastBarX + VolumeConstants.DEFAULT_LABEL_OFFSET_X;

            // Position label at middle of Y axis range
            double yMid = (chartScale.MaxValue + chartScale.MinValue) / 2;
            double labelY = chartScale.GetYByValue(yMid);

            // Format the label text (positive = accumulation, negative = distribution)
            string labelText = lastAccumulationDistribution.ToString("F0");

            // Choose color based on sign
            SharpDX.Color labelColor = lastAccumulationDistribution > 0 ?
                                      SharpDX.Color.ForestGreen : SharpDX.Color.IndianRed;

            // Create and render text layout
            using (var textFormat = CreateTextFormat())
            using (var textLayout = CreateTextLayout(labelText, textFormat))
            using (var brush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, labelColor))
            {
                RenderTarget.DrawTextLayout(
                    new SharpDX.Vector2((float)labelX, (float)labelY),
                    textLayout,
                    brush,
                    SharpDX.Direct2D1.DrawTextOptions.NoSnap);
            }
        }

        private SharpDX.DirectWrite.TextFormat CreateTextFormat()
        {
            var textFormat = new SharpDX.DirectWrite.TextFormat(
                Core.Globals.DirectWriteFactory,
                VolumeConstants.DEFAULT_FONT_FAMILY,
                SharpDX.DirectWrite.FontWeight.Bold,
                SharpDX.DirectWrite.FontStyle.Normal,
                (float)labelFontSize);

            textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
            textFormat.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

            return textFormat;
        }

        private SharpDX.DirectWrite.TextLayout CreateTextLayout(string text, SharpDX.DirectWrite.TextFormat textFormat)
        {
            return new SharpDX.DirectWrite.TextLayout(
                Core.Globals.DirectWriteFactory,
                text,
                textFormat,
                VolumeConstants.LABEL_MAX_WIDTH,
                (float)labelFontSize);
        }

        #region Properties
        [NinjaScriptProperty]
        [Range(VolumeConstants.MIN_OPACITY, VolumeConstants.MAX_OPACITY)]
        [Display(Name = "Total Volume Opacity", Description = "Opacity of the total volume bar (0-100%)", Order = 1, GroupName = "Visual")]
        public int TotalVolumeOpacity
        {
            get => totalVolumeOpacity;
            set
            {
                if (value >= VolumeConstants.MIN_OPACITY && value <= VolumeConstants.MAX_OPACITY)
                    totalVolumeOpacity = value;
            }
        }

        [NinjaScriptProperty]
        [Range(VolumeConstants.MIN_THRESHOLD, VolumeConstants.MAX_THRESHOLD)]
        [Display(Name = "Threshold", Description = "Threshold percentage for filtering effective volume (0.3 = 30%)", Order = 2, GroupName = "Parameters")]
        public double Threshold
        {
            get => threshold;
            set
            {
                if (value >= VolumeConstants.MIN_THRESHOLD && value <= VolumeConstants.MAX_THRESHOLD)
                    threshold = value;
            }
        }

        [NinjaScriptProperty]
        [Range(VolumeConstants.MIN_FONT_SIZE, VolumeConstants.MAX_FONT_SIZE)]
        [Display(Name = "Label Font Size", Description = "Font size for the volume label", Order = 3, GroupName = "Visual")]
        public int LabelFontSize
        {
            get => labelFontSize;
            set
            {
                if (value >= VolumeConstants.MIN_FONT_SIZE && value <= VolumeConstants.MAX_FONT_SIZE)
                    labelFontSize = value;
            }
        }

        [NinjaScriptProperty]
        [Range(VolumeConstants.MIN_EMA_PERIOD, VolumeConstants.MAX_EMA_PERIOD)]
        [Display(Name = "EMA Period", Description = "Period for the volume EMA", Order = 4, GroupName = "Parameters")]
        public int EmaPeriod
        {
            get => emaPeriod;
            set
            {
                if (value >= VolumeConstants.MIN_EMA_PERIOD && value <= VolumeConstants.MAX_EMA_PERIOD)
                    emaPeriod = value;
            }
        }

        [NinjaScriptProperty]
        [Range(VolumeConstants.MIN_VOLUME_THRESHOLD, VolumeConstants.MAX_VOLUME_THRESHOLD)]
        [Display(Name = "High Volume Threshold", Description = "Ratio threshold for high volume (orange color)", Order = 5, GroupName = "Parameters")]
        public double HighVolumeThreshold
        {
            get => highVolumeThreshold;
            set
            {
                if (value >= VolumeConstants.MIN_VOLUME_THRESHOLD && value <= VolumeConstants.MAX_VOLUME_THRESHOLD)
                    highVolumeThreshold = value;
            }
        }

        [NinjaScriptProperty]
        [Range(VolumeConstants.MIN_VOLUME_THRESHOLD, VolumeConstants.MAX_VOLUME_THRESHOLD)]
        [Display(Name = "Medium Volume Threshold", Description = "Ratio threshold for medium volume (blue color)", Order = 6, GroupName = "Parameters")]
        public double MediumVolumeThreshold
        {
            get => mediumVolumeThreshold;
            set
            {
                if (value >= VolumeConstants.MIN_VOLUME_THRESHOLD && value <= VolumeConstants.MAX_VOLUME_THRESHOLD)
                    mediumVolumeThreshold = value;
            }
        }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "White Volume Multiplier", Description = "Multiplier for the SMA to color the volume bar white", Order = 7, GroupName = "Parameters")]
        public double WhiteVolumeMultiplier
        {
            get => whiteVolumeMultiplier;
            set
            {
                if (value >= 1.0 && value <= 10.0)
                    whiteVolumeMultiplier = value;
            }
        }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "White Volume SMA Period", Description = "Period for the SMA to color the volume bar white", Order = 8, GroupName = "Parameters")]
        public int WhiteVolumeSmaPeriod
        {
            get => whiteVolumeSmaPeriod;
            set
            {
                if (value >= 1 && value <= 100)
                    whiteVolumeSmaPeriod = value;
            }
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
		public RyFVol RyFVol(int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double whiteVolumeMultiplier, int whiteVolumeSmaPeriod)
		{
			return RyFVol(Input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, whiteVolumeMultiplier, whiteVolumeSmaPeriod);
		}

		public RyFVol RyFVol(ISeries<double> input, int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double whiteVolumeMultiplier, int whiteVolumeSmaPeriod)
		{
			if (cacheRyFVol != null)
				for (int idx = 0; idx < cacheRyFVol.Length; idx++)
					if (cacheRyFVol[idx] != null && cacheRyFVol[idx].TotalVolumeOpacity == totalVolumeOpacity && cacheRyFVol[idx].Threshold == threshold && cacheRyFVol[idx].LabelFontSize == labelFontSize && cacheRyFVol[idx].EmaPeriod == emaPeriod && cacheRyFVol[idx].HighVolumeThreshold == highVolumeThreshold && cacheRyFVol[idx].MediumVolumeThreshold == mediumVolumeThreshold && cacheRyFVol[idx].WhiteVolumeMultiplier == whiteVolumeMultiplier && cacheRyFVol[idx].WhiteVolumeSmaPeriod == whiteVolumeSmaPeriod && cacheRyFVol[idx].EqualsInput(input))
						return cacheRyFVol[idx];
			return CacheIndicator<RyFVol>(new RyFVol(){ TotalVolumeOpacity = totalVolumeOpacity, Threshold = threshold, LabelFontSize = labelFontSize, EmaPeriod = emaPeriod, HighVolumeThreshold = highVolumeThreshold, MediumVolumeThreshold = mediumVolumeThreshold, WhiteVolumeMultiplier = whiteVolumeMultiplier, WhiteVolumeSmaPeriod = whiteVolumeSmaPeriod }, input, ref cacheRyFVol);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RyFVol RyFVol(int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double whiteVolumeMultiplier, int whiteVolumeSmaPeriod)
		{
			return indicator.RyFVol(Input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, whiteVolumeMultiplier, whiteVolumeSmaPeriod);
		}

		public Indicators.RyFVol RyFVol(ISeries<double> input , int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double whiteVolumeMultiplier, int whiteVolumeSmaPeriod)
		{
			return indicator.RyFVol(input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, whiteVolumeMultiplier, whiteVolumeSmaPeriod);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RyFVol RyFVol(int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double whiteVolumeMultiplier, int whiteVolumeSmaPeriod)
		{
			return indicator.RyFVol(Input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, whiteVolumeMultiplier, whiteVolumeSmaPeriod);
		}

		public Indicators.RyFVol RyFVol(ISeries<double> input , int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double whiteVolumeMultiplier, int whiteVolumeSmaPeriod)
		{
			return indicator.RyFVol(input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, whiteVolumeMultiplier, whiteVolumeSmaPeriod);
		}
	}
}

#endregion
