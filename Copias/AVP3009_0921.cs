#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
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
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    // Enums at namespace level to avoid conflicts
    public enum AVPLabelAlignment
    {
        Left,
        Center,
        Right
    }

    public enum AVPRangeType
    {
        OneDay,
        ThreeDays
    }

    public class AVP : Indicator
    {

        #region Variables

        // A class to hold all data related to a single day's levels
        private class DayLevels
        {
            public Dictionary<string, PriceLevel> Levels { get; }
            public int StartBarIndex { get; set; }
            public int EndBarIndex { get; set; }

            public DayLevels()
            {
                Levels = new Dictionary<string, PriceLevel>();
                StartBarIndex = -1;
                EndBarIndex = -1;
            }
        }

        // Input Parameters
        private DateTime selectedDate;
        private double manualPrice;
        private AVPRangeType rangeType;

        // Session management variables for automatic daily updates
        private SessionIterator sessionIterator;
        private DateTime currentDate = Core.Globals.MinDate;
        private double currentDayHigh;
        private double currentDayLow;
        private double currentDayOpen;
        private double priorDayHigh;
        private double priorDayLow;
        private double priorDayOpen;

        // For 3-day calculation - store last 3 days of data
        private readonly Queue<DayData> threeDayHistory = new Queue<DayData>();

        private class DayData
        {
            public DateTime Date { get; set; }
            public double High { get; set; }
            public double Low { get; set; }
            public double Open { get; set; }
        }

        // Data structures for managing current and historical price levels
        private DayLevels currentDayLevels;
        private readonly Queue<DayLevels> historicalLevels = new Queue<DayLevels>();

        // Represents a calculated price level
        private class PriceLevel : IDisposable
        {
            public string Name { get; }
            public System.Windows.Media.Brush LineBrush { get; }
            public double Value { get; set; }
            public TextLayout LabelLayout { get; set; }
            public string Modifier { get; set; }

            public PriceLevel(string name, System.Windows.Media.Brush brush, string modifier = "")
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
                LineBrush = brush ?? throw new ArgumentNullException(nameof(brush));
                Value = double.NaN;
                Modifier = modifier;
            }

            public void Dispose()
            {
                LabelLayout?.Dispose();
            }
        }

        // Cached brushes for performance
        private readonly Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush> dxBrushes = new Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush>();

        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "AVP - Advanced Volume/Price Levels with configurable range calculation";
                Name = "AVP";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // Default parameter values
                UseAutomaticDate = true;
                DaysToDraw = 5;
                RangeType = AVPRangeType.OneDay;
                UseGapCalculation = false;
                SelectedDate = DateTime.Today.AddDays(-1);
                ManualPrice = 0.0;
                Width = 1;
                ShowDynamicLabels = true;
                LabelOffsetX = 15;
                LabelVerticalSpacing = 20;
                LineBufferPixels = 125;
            }
            else if (State == State.DataLoaded)
            {
                sessionIterator = new SessionIterator(Bars);
                currentDayLevels = new DayLevels();
            }
            else if (State == State.Terminated)
            {
                ClearAllLevels();

                // Clean up cached brushes
                foreach (var brush in dxBrushes.Values)
                    brush?.Dispose();
                dxBrushes.Clear();
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0) return;
            if (CurrentBar < 1) return;

            DateTime barTime = Times[0][0];
            DateTime tradingDay = sessionIterator.GetTradingDay(barTime);

            if (UseAutomaticDate)
            {
                if (tradingDay != currentDate)
                {
                    if (currentDate != Core.Globals.MinDate)
                    {
                        FinalizeCurrentDay();
                    }

                    StartNewTradingDay(tradingDay);
                }

                UpdateDayHighLowOpen();
            }
        }

        private void StartNewTradingDay(DateTime tradingDay)
        {
            currentDate = tradingDay;

            // Add previous day to 3-day history if it has valid data
            if (currentDayHigh > 0 && currentDayLow > 0)
            {
                threeDayHistory.Enqueue(new DayData
                {
                    Date = currentDate,
                    High = currentDayHigh,
                    Low = currentDayLow,
                    Open = currentDayOpen
                });

                // Keep only last 3 days
                while (threeDayHistory.Count > 3)
                {
                    threeDayHistory.Dequeue();
                }
            }

            priorDayHigh = currentDayHigh;
            priorDayLow = currentDayLow;
            priorDayOpen = currentDayOpen;

            currentDayHigh = High[0];
            currentDayLow = Low[0];
            currentDayOpen = Open[0];

            currentDayLevels = new DayLevels
            {
                StartBarIndex = CurrentBar
            };

            // Calculate levels based on selected range type
            if (CanCalculateLevels())
            {
                CalculateAndCreateLevels(currentDayLevels);
            }
        }

        private bool CanCalculateLevels()
        {
            if (RangeType == AVPRangeType.OneDay)
            {
                return priorDayHigh > 0 && priorDayLow > 0;
            }
            else // ThreeDays
            {
                return threeDayHistory.Count >= 3;
            }
        }

        private void UpdateDayHighLowOpen()
        {
            if (High[0] > currentDayHigh) currentDayHigh = High[0];
            if (Low[0] < currentDayLow) currentDayLow = Low[0];
        }

        private void FinalizeCurrentDay()
        {
            if (currentDayLevels != null)
            {
                currentDayLevels.EndBarIndex = CurrentBar - 1;
                historicalLevels.Enqueue(currentDayLevels);

                while (historicalLevels.Count > DaysToDraw)
                {
                    var oldDay = historicalLevels.Dequeue();
                    foreach (var level in oldDay.Levels.Values)
                        level?.Dispose();
                }
            }
        }

        private void CalculateAndCreateLevels(DayLevels dayLevels)
        {
            // Clear existing levels
            foreach (var level in dayLevels.Levels.Values)
                level?.Dispose();
            dayLevels.Levels.Clear();

            double q1, q4, dayRange;
            string rangeDescription;

            if (RangeType == AVPRangeType.OneDay)
            {
                // Use previous day's high and low
                q1 = priorDayHigh;  // "Máximo día anterior"
                q4 = priorDayLow;   // "Mínimo día anterior"
                dayRange = q1 - q4;
                rangeDescription = "1 día anterior";
            }
            else // ThreeDays
            {
                // Calculate 3-day range using stored history
                if (threeDayHistory.Count >= 3)
                {
                    q1 = threeDayHistory.Max(d => d.High);  // Máximo de 3 días
                    q4 = threeDayHistory.Min(d => d.Low);   // Mínimo de 3 días
                    dayRange = q1 - q4;
                    rangeDescription = "3 días anteriores";
                }
                else
                {
                    // Fallback to 1-day if not enough history
                    q1 = priorDayHigh;
                    q4 = priorDayLow;
                    dayRange = q1 - q4;
                    rangeDescription = "1 día anterior (insuficiente historial para 3 días)";
                }
            }

            // NR2 (base) = Q1 - (Rango/2) - always calculated this way
            double nr2 = q1 - (dayRange / 2);

            Print($"AVP Calculations - Date: {currentDate:yyyy-MM-dd}");
            Print($"Modo: {rangeDescription}");
            Print($"Q1 (Máximo): {q1:F2}");
            Print($"Q4 (Mínimo): {q4:F2}");
            Print($"Rango: {dayRange:F4}");
            Print($"NR2 (Q1 - Rango/2): {nr2:F2}");

            // Calculate all levels based on AVP.md specifications
            CalculateAllLevels(dayRange, q1, q4, nr2, dayLevels.Levels);
        }

        private void CalculateAllLevels(double dayRange, double q1, double q4, double nr2, Dictionary<string, PriceLevel> levels)
        {
            // Main levels from Q1 (downward)
            AddLevel(levels, "Q1", q1, Brushes.Yellow);
            AddLevel(levels, "TC", q1 - (dayRange * 0.0625), Brushes.ForestGreen);
            AddLevel(levels, "ZSell", q1 - (dayRange * 0.125), Brushes.Green);
            AddLevel(levels, "NR1", q1 - (dayRange * 0.159), Brushes.DarkOrchid);
            AddLevel(levels, "Q2", q1 - (dayRange * 0.25), Brushes.Plum);
            AddLevel(levels, "M+", q1 - (dayRange * 0.375), Brushes.ForestGreen);

            // NR2 (Base level)
            AddLevel(levels, "NR2", nr2, Brushes.Orange);

            // Main levels from Q4 (upward)
            AddLevel(levels, "M-", q4 + (dayRange * 0.375), Brushes.ForestGreen);
            AddLevel(levels, "Q3", q4 + (dayRange * 0.25), Brushes.Plum);
            AddLevel(levels, "ZBuy", q4 + (dayRange * 0.125), Brushes.IndianRed);
            AddLevel(levels, "TV", q4 + (dayRange * 0.0625), Brushes.IndianRed);
            AddLevel(levels, "NR3", q4 + (dayRange * 0.159), Brushes.DarkOrchid);
            AddLevel(levels, "Q4", q4, Brushes.Yellow);

            // Extension levels upward from Q1
            AddLevel(levels, "Std1+", q1 + (dayRange * 0.0855), Brushes.ForestGreen);
            AddLevel(levels, "Std2+", q1 + (dayRange * 0.125), Brushes.ForestGreen);
            AddLevel(levels, "Std3+", q1 + (dayRange * 0.25), Brushes.ForestGreen);
            AddLevel(levels, "Std4+", q1 + (dayRange * 0.375), Brushes.ForestGreen);
            AddLevel(levels, "R+", q1 + (dayRange * 0.5), Brushes.Gold);

            // Extension levels downward from Q4
            AddLevel(levels, "Std1-", q4 - (dayRange * 0.0855), Brushes.IndianRed);
            AddLevel(levels, "Std2-", q4 - (dayRange * 0.125), Brushes.IndianRed);
            AddLevel(levels, "Std3-", q4 - (dayRange * 0.25), Brushes.IndianRed);
            AddLevel(levels, "Std4-", q4 - (dayRange * 0.375), Brushes.IndianRed);
            AddLevel(levels, "R-", q4 - (dayRange * 0.50), Brushes.Gold);
        }

        private void AddLevel(Dictionary<string, PriceLevel> levels, string name, double value, System.Windows.Media.Brush brush, string modifier = "")
        {
            if (!double.IsNaN(value) && !double.IsInfinity(value))
            {
                levels[name] = new PriceLevel(name, brush, modifier)
                {
                    Value = value
                };

                Print($"{name}: {value:F2} {modifier}");
            }
        }

        private void ClearAllLevels()
        {
            currentDayLevels?.Levels?.Values?.ToList()?.ForEach(level => level?.Dispose());

            while (historicalLevels.Count > 0)
            {
                var day = historicalLevels.Dequeue();
                foreach (var level in day.Levels.Values)
                    level?.Dispose();
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (Bars == null || ChartControl == null) return;

            // Draw historical levels
            foreach (var day in historicalLevels)
            {
                DrawLevelsForRange(chartControl, chartScale, day.Levels, day.StartBarIndex, day.EndBarIndex, false);
            }

            // Draw current day levels
            if (currentDayLevels?.Levels?.Count > 0)
            {
                DrawLevelsForRange(chartControl, chartScale, currentDayLevels.Levels, currentDayLevels.StartBarIndex, -1, true);
            }

            // Draw dynamic labels for all visible levels
            if (ShowDynamicLabels)
            {
                DrawDynamicLabels(chartControl, chartScale);
            }
        }

        private void DrawLevelsForRange(ChartControl chartControl, ChartScale chartScale, Dictionary<string, PriceLevel> levels, int startBarIndex, int endBarIndex, bool isCurrentDay)
        {
            if (levels == null || levels.Count == 0) return;

            float lineStartX = startBarIndex >= 0 ? (float)chartControl.GetXByBarIndex(ChartBars, startBarIndex) : 0;
            float lineEndX;

            if (isCurrentDay)
            {
                // For current day: extend 25 pixels beyond the last bar
                int lastBarIndex = ChartBars.ToIndex;
                float lastBarX = (float)chartControl.GetXByBarIndex(ChartBars, lastBarIndex);
                lineEndX = lastBarX + 25;
            }
            else
            {
                // For historical days: use the end bar
                lineEndX = endBarIndex >= 0 ? (float)chartControl.GetXByBarIndex(ChartBars, endBarIndex) : (float)chartControl.CanvasRight;
            }

            foreach (var level in levels.Values)
            {
                if (double.IsNaN(level.Value)) continue;

                float y = (float)chartScale.GetYByValue(level.Value);
                if (y < 0 || y > ChartPanel.H) continue;

                // Get or create DirectX brush
                var dxBrush = GetOrCreateBrush(RenderTarget, level.LineBrush);
                if (dxBrush == null) continue;

                // Draw line
                RenderTarget.DrawLine(new SharpDX.Vector2(lineStartX, y), new SharpDX.Vector2(lineEndX, y), dxBrush, Width);

                // Draw label if enabled
                if (ShowDynamicLabels)
                {
                    // Solo mostrar etiquetas para el día actual
                    if (isCurrentDay)
                    {
                        DrawLabel(chartControl, level, y, lineEndX, startBarIndex, endBarIndex, isCurrentDay);
                    }
                }
            }
        }

        private void DrawDynamicLabels(ChartControl chartControl, ChartScale chartScale)
        {
            // Obtener la última barra visible (similar al ejemplo)
            int lastVisibleBarIndex = ChartBars.ToIndex;
            if (lastVisibleBarIndex < 0 || lastVisibleBarIndex >= Bars.Count)
                return;

            // Calcular posición X base (15 píxeles desde la última barra visible)
            float baseXPosition = (float)chartControl.GetXByBarIndex(ChartBars, lastVisibleBarIndex) + LabelOffsetX;

            // Verificar que la posición base esté dentro del área visible
            if (baseXPosition < 0 || baseXPosition > chartControl.CanvasRight)
                return;

            // Dibujar etiquetas del día actual
            if (currentDayLevels?.Levels?.Count > 0)
            {
                foreach (var level in currentDayLevels.Levels.Values)
                {
                    if (double.IsNaN(level.Value)) continue;

                    // La posición Y debe coincidir exactamente con la línea
                    float y = (float)chartScale.GetYByValue(level.Value);
                    if (y < 0 || y > ChartPanel.H) continue;

                    // NO aplicar offset vertical - mantener la etiqueta en la línea
                    DrawSingleLabel(baseXPosition, y, level);
                }
            }
        }

        private void DrawSingleLabel(float x, float y, PriceLevel level)
        {
            string labelText = !string.IsNullOrEmpty(level.Modifier) ? $"{level.Name} {level.Value:F2} {level.Modifier}" : $"{level.Name} {level.Value:F2}";

            // Dispose existing layout if it exists and recreate with current price
            level.LabelLayout?.Dispose();
            level.LabelLayout = new TextLayout(Core.Globals.DirectWriteFactory,
                labelText,
                new TextFormat(Core.Globals.DirectWriteFactory, "Arial", 9),
                250, 50);

            RenderTarget.DrawTextLayout(new SharpDX.Vector2(x, y - 8), level.LabelLayout,
                GetOrCreateBrush(RenderTarget, Brushes.White), SharpDX.Direct2D1.DrawTextOptions.None);
        }

        private void DrawLabel(ChartControl chartControl, PriceLevel level, float y, float labelX, int startBarIndex, int endBarIndex, bool isCurrentDay)
        {
            // Esta función ya no se usa, las etiquetas se manejan en DrawDynamicLabels
        }

        private bool ShouldDrawLabel(float labelX, ChartControl chartControl, int startBarIndex, int endBarIndex, bool isCurrentDay)
        {
            // No dibujar si la etiqueta está fuera de pantalla
            if (labelX < 0 || labelX > chartControl.CanvasRight)
                return false;

            if (isCurrentDay)
            {
                // Para día actual: ocultar cuando el texto toque la primera barra del día
                if (startBarIndex < ChartBars.FromIndex || startBarIndex > ChartBars.ToIndex)
                    return true; // Si la primera barra no está visible, mostrar etiquetas

                float startBarX = (float)chartControl.GetXByBarIndex(ChartBars, startBarIndex);
                if (labelX <= startBarX)
                    return false;
            }
            else
            {
                // Para días históricos: ocultar cuando el texto toque la última barra del día
                if (endBarIndex < ChartBars.FromIndex || endBarIndex > ChartBars.ToIndex)
                    return true; // Si la última barra no está visible, mostrar etiquetas

                float endBarX = (float)chartControl.GetXByBarIndex(ChartBars, endBarIndex);
                if (labelX <= endBarX)
                    return false;
            }

            return true;
        }

        private SharpDX.Direct2D1.Brush GetOrCreateBrush(SharpDX.Direct2D1.RenderTarget renderTarget, System.Windows.Media.Brush mediaBrush)
        {
            if (mediaBrush == null || renderTarget == null) return null;

            if (dxBrushes.TryGetValue(mediaBrush, out var existingBrush) && existingBrush != null)
                return existingBrush;

            SharpDX.Direct2D1.Brush dxBrush = null;

            if (mediaBrush is System.Windows.Media.SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                dxBrush = new SharpDX.Direct2D1.SolidColorBrush(renderTarget,
                    new SharpDX.Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f));
            }

            if (dxBrush != null)
            {
                dxBrushes[mediaBrush] = dxBrush;
            }

            return dxBrush;
        }

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Use Automatic Date", Description = "Use automatic date or manual date selection", Order = 1, GroupName = "Parameters")]
        public bool UseAutomaticDate { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Days to Draw", Description = "Number of historical days to draw", Order = 2, GroupName = "Parameters")]
        public int DaysToDraw { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Range Type", Description = "Use 1 day or 3 days for range calculation", Order = 3, GroupName = "Parameters")]
        public AVPRangeType RangeType
        {
            get { return rangeType; }
            set { rangeType = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Use Gap Calculation", Description = "Include gap calculation in the levels", Order = 4, GroupName = "Parameters")]
        public bool UseGapCalculation { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Selected Date", Description = "Manual date selection when not using automatic", Order = 5, GroupName = "Parameters")]
        public DateTime SelectedDate
        {
            get { return selectedDate; }
            set { selectedDate = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Manual Price", Description = "Manual price override (0 = automatic)", Order = 6, GroupName = "Parameters")]
        public double ManualPrice
        {
            get { return manualPrice; }
            set { manualPrice = value; }
        }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Line Width", Description = "Width of the level lines", Order = 7, GroupName = "Visual")]
        public int Width { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Dynamic Labels", Description = "Show labels for the price levels", Order = 8, GroupName = "Visual")]
        public bool ShowDynamicLabels { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Label Offset X", Description = "Horizontal offset for labels from line end", Order = 9, GroupName = "Visual")]
        public int LabelOffsetX { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Label Vertical Spacing", Description = "Vertical spacing between labels", Order = 10, GroupName = "Visual")]
        public int LabelVerticalSpacing { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Line Buffer Pixels", Description = "Buffer space for lines in pixels", Order = 11, GroupName = "Visual")]
        public int LineBufferPixels { get; set; }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private AVP[] cacheAVP;
        public AVP AVP(bool useAutomaticDate, int daysToDraw, AVPRangeType rangeType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, bool showDynamicLabels, int labelOffsetX, int labelVerticalSpacing, int lineBufferPixels)
        {
            return AVP(Input, useAutomaticDate, daysToDraw, rangeType, useGapCalculation, selectedDate, manualPrice, width, showDynamicLabels, labelOffsetX, labelVerticalSpacing, lineBufferPixels);
        }

        public AVP AVP(ISeries<double> input, bool useAutomaticDate, int daysToDraw, AVPRangeType rangeType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, bool showDynamicLabels, int labelOffsetX, int labelVerticalSpacing, int lineBufferPixels)
        {
            if (cacheAVP != null)
                for (int idx = 0; idx < cacheAVP.Length; idx++)
                    if (cacheAVP[idx] != null && cacheAVP[idx].UseAutomaticDate == useAutomaticDate && cacheAVP[idx].DaysToDraw == daysToDraw && cacheAVP[idx].RangeType == rangeType && cacheAVP[idx].UseGapCalculation == useGapCalculation && cacheAVP[idx].SelectedDate == selectedDate && cacheAVP[idx].ManualPrice == manualPrice && cacheAVP[idx].Width == width && cacheAVP[idx].ShowDynamicLabels == showDynamicLabels && cacheAVP[idx].LabelOffsetX == labelOffsetX && cacheAVP[idx].LabelVerticalSpacing == labelVerticalSpacing && cacheAVP[idx].LineBufferPixels == lineBufferPixels && cacheAVP[idx].EqualsInput(input))
                        return cacheAVP[idx];
            return CacheIndicator<AVP>(new AVP() { UseAutomaticDate = useAutomaticDate, DaysToDraw = daysToDraw, RangeType = rangeType, UseGapCalculation = useGapCalculation, SelectedDate = selectedDate, ManualPrice = manualPrice, Width = width, ShowDynamicLabels = showDynamicLabels, LabelOffsetX = labelOffsetX, LabelVerticalSpacing = labelVerticalSpacing, LineBufferPixels = lineBufferPixels }, input, ref cacheAVP);
        }
    }
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.AVP AVP(bool useAutomaticDate, int daysToDraw, AVPRangeType rangeType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, bool showDynamicLabels, int labelOffsetX, int labelVerticalSpacing, int lineBufferPixels)
        {
            return indicator.AVP(Input, useAutomaticDate, daysToDraw, rangeType, useGapCalculation, selectedDate, manualPrice, width, showDynamicLabels, labelOffsetX, labelVerticalSpacing, lineBufferPixels);
        }

        public Indicators.AVP AVP(ISeries<double> input, bool useAutomaticDate, int daysToDraw, AVPRangeType rangeType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, bool showDynamicLabels, int labelOffsetX, int labelVerticalSpacing, int lineBufferPixels)
        {
            return indicator.AVP(input, useAutomaticDate, daysToDraw, rangeType, useGapCalculation, selectedDate, manualPrice, width, showDynamicLabels, labelOffsetX, labelVerticalSpacing, lineBufferPixels);
        }
    }
}

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.AVP AVP(bool useAutomaticDate, int daysToDraw, AVPRangeType rangeType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, bool showDynamicLabels, int labelOffsetX, int labelVerticalSpacing, int lineBufferPixels)
        {
            return indicator.AVP(Input, useAutomaticDate, daysToDraw, rangeType, useGapCalculation, selectedDate, manualPrice, width, showDynamicLabels, labelOffsetX, labelVerticalSpacing, lineBufferPixels);
        }

        public Indicators.AVP AVP(ISeries<double> input, bool useAutomaticDate, int daysToDraw, AVPRangeType rangeType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, bool showDynamicLabels, int labelOffsetX, int labelVerticalSpacing, int lineBufferPixels)
        {
            return indicator.AVP(input, useAutomaticDate, daysToDraw, rangeType, useGapCalculation, selectedDate, manualPrice, width, showDynamicLabels, labelOffsetX, labelVerticalSpacing, lineBufferPixels);
        }
    }
}

#endregion