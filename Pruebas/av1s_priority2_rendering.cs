using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using NinjaTrader.Data;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators.AV1s.Domain;
using NinjaTrader.NinjaScript.Indicators.AV1s.Services;
using SharpDX;
using SharpDX.Direct2D1;

namespace NinjaTrader.NinjaScript.Indicators.AV1s.Rendering
{
    #region Rendering Interfaces

    /// <summary>
    /// Service responsible for rendering price levels on NinjaTrader charts
    /// </summary>
    public interface ILevelRenderer
    {
        /// <summary>
        /// Renders price levels for a specific trading day
        /// </summary>
        void RenderLevels(DayLevels dayLevels, RenderContext context);

        /// <summary>
        /// Renders dynamic labels for the levels
        /// </summary>
        void RenderDynamicLabels(DayLevels dayLevels, RenderContext context);

        /// <summary>
        /// Clears rendered levels for a specific day
        /// </summary>
        void ClearLevels(DateTime tradingDay);

        /// <summary>
        /// Clears all rendered levels
        /// </summary>
        void ClearAllLevels();

        /// <summary>
        /// Updates render parameters
        /// </summary>
        void UpdateRenderParameters(RenderParameters parameters);
    }

    /// <summary>
    /// Enhanced rendering context for NinjaTrader environment
    /// </summary>
    public sealed class NinjaTraderRenderContext : RenderContext
    {
        public NinjaScriptBase IndicatorInstance { get; }
        public ChartPanel ChartPanel { get; }
        public RenderTarget RenderTarget { get; }
        public ChartBars ChartBars { get; }

        public NinjaTraderRenderContext(
            DateTime tradingDay,
            int startBarIndex,
            int endBarIndex,
            int currentBarIndex,
            double chartScale,
            RenderParameters parameters,
            NinjaScriptBase indicatorInstance,
            ChartPanel chartPanel,
            RenderTarget renderTarget,
            ChartBars chartBars)
            : base(tradingDay, startBarIndex, endBarIndex, currentBarIndex, chartScale, parameters)
        {
            IndicatorInstance = indicatorInstance ?? throw new ArgumentNullException(nameof(indicatorInstance));
            ChartPanel = chartPanel ?? throw new ArgumentNullException(nameof(chartPanel));
            RenderTarget = renderTarget ?? throw new ArgumentNullException(nameof(renderTarget));
            ChartBars = chartBars ?? throw new ArgumentNullException(nameof(chartBars));
        }
    }

    #endregion

    #region Rendering Implementations

    /// <summary>
    /// NinjaTrader-specific implementation of level renderer
    /// </summary>
    public sealed class NinjaTraderLevelRenderer : ILevelRenderer, IDisposable
    {
        private readonly ILoggingService logger;
        private readonly Dictionary<DateTime, List<DrawingTool>> renderedLines;
        private readonly Dictionary<DateTime, List<DrawingTool>> renderedLabels;
        private readonly object renderLock = new object();
        private RenderParameters currentRenderParameters;
        private bool disposed = false;

        public NinjaTraderLevelRenderer(ILoggingService logger = null)
        {
            this.logger = logger ?? new ConsoleLoggingService();
            this.renderedLines = new Dictionary<DateTime, List<DrawingTool>>();
            this.renderedLabels = new Dictionary<DateTime, List<DrawingTool>>();
        }

        public void RenderLevels(DayLevels dayLevels, RenderContext context)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(NinjaTraderLevelRenderer));

            if (dayLevels == null)
            {
                logger.LogWarning("Cannot render null day levels");
                return;
            }

            if (context == null)
            {
                logger.LogWarning("Cannot render with null context");
                return;
            }

            if (!(context is NinjaTraderRenderContext ntContext))
            {
                logger.LogError("Context must be NinjaTraderRenderContext for this renderer");
                return;
            }

            try
            {
                lock (renderLock)
                {
                    logger.LogDebug($"Rendering {dayLevels.Levels.Count} levels for {dayLevels.TradingDay:yyyy-MM-dd}");

                    // Clear existing levels for this day
                    ClearLevelsInternal(dayLevels.TradingDay);

                    // Render each valid level
                    var renderedCount = 0;
                    foreach (var level in dayLevels.GetValidLevels())
                    {
                        try
                        {
                            RenderSingleLevel(level, dayLevels.TradingDay, ntContext);
                            renderedCount++;
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning($"Failed to render level {level.Name}: {ex.Message}");
                        }
                    }

                    logger.LogInfo($"Successfully rendered {renderedCount} levels for {dayLevels.TradingDay:yyyy-MM-dd}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error rendering levels for {dayLevels.TradingDay:yyyy-MM-dd}: {ex.Message}");
            }
        }

        public void RenderDynamicLabels(DayLevels dayLevels, RenderContext context)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(NinjaTraderLevelRenderer));

            if (!context.Parameters.ShowDynamicLabels)
            {
                logger.LogDebug("Dynamic labels disabled");
                return;
            }

            if (!(context is NinjaTraderRenderContext ntContext))
            {
                logger.LogError("Context must be NinjaTraderRenderContext for label rendering");
                return;
            }

            try
            {
                lock (renderLock)
                {
                    logger.LogDebug($"Rendering dynamic labels for {dayLevels.TradingDay:yyyy-MM-dd}");

                    // Clear existing labels for this day
                    ClearLabelsInternal(dayLevels.TradingDay);

                    // Render labels for visible levels
                    var labelY = 0;
                    foreach (var level in dayLevels.GetValidLevels().OrderByDescending(l => l.Value))
                    {
                        try
                        {
                            RenderLevelLabel(level, dayLevels.TradingDay, ntContext, ref labelY);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning($"Failed to render label for {level.Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error rendering dynamic labels: {ex.Message}");
            }
        }

        public void ClearLevels(DateTime tradingDay)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(NinjaTraderLevelRenderer));

            try
            {
                lock (renderLock)
                {
                    ClearLevelsInternal(tradingDay);
                    ClearLabelsInternal(tradingDay);
                    logger.LogDebug($"Cleared levels for {tradingDay:yyyy-MM-dd}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error clearing levels for {tradingDay:yyyy-MM-dd}: {ex.Message}");
            }
        }

        public void ClearAllLevels()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(NinjaTraderLevelRenderer));

            try
            {
                lock (renderLock)
                {
                    var allDays = renderedLines.Keys.Concat(renderedLabels.Keys).Distinct().ToList();

                    foreach (var day in allDays)
                    {
                        ClearLevelsInternal(day);
                        ClearLabelsInternal(day);
                    }

                    logger.LogInfo($"Cleared all levels for {allDays.Count} trading days");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error clearing all levels: {ex.Message}");
            }
        }

        public void UpdateRenderParameters(RenderParameters parameters)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(NinjaTraderLevelRenderer));

            currentRenderParameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            logger.LogDebug("Updated render parameters");
        }

        private void RenderSingleLevel(PriceLevel level, DateTime tradingDay, NinjaTraderRenderContext context)
        {
            if (level == null || !level.IsValid)
                return;

            // Determine line coordinates
            var startBarIndex = Math.Max(0, context.StartBarIndex);
            var endBarIndex = Math.Min(context.ChartBars.Count - 1, context.EndBarIndex);

            if (startBarIndex >= endBarIndex)
            {
                logger.LogWarning($"Invalid bar range for level {level.Name}: {startBarIndex}-{endBarIndex}");
                return;
            }

            try
            {
                // Create horizontal line using NinjaTrader drawing tools
                var line = Draw.HorizontalLine(
                    context.IndicatorInstance,
                    $"AV1s_{level.Name}_{tradingDay:yyyyMMdd}",
                    level.Value,
                    level.Color,
                    DashStyleHelper.Solid,
                    context.Parameters.LineWidth
                );

                if (line != null)
                {
                    // Track the rendered line
                    if (!renderedLines.ContainsKey(tradingDay))
                        renderedLines[tradingDay] = new List<DrawingTool>();

                    renderedLines[tradingDay].Add(line);

                    logger.LogDebug($"Rendered line for {level.Name} at {level.Value:F4}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to draw line for {level.Name}: {ex.Message}");
            }
        }

        private void RenderLevelLabel(PriceLevel level, DateTime tradingDay, NinjaTraderRenderContext context, ref int labelY)
        {
            if (level == null || !level.IsValid)
                return;

            try
            {
                var labelText = $"{level.Name}: {level.Value:F2}";
                var anchor = context.CurrentBarIndex;

                // Create text display
                var textDisplay = Draw.TextFixed(
                    context.IndicatorInstance,
                    $"AV1s_Label_{level.Name}_{tradingDay:yyyyMMdd}",
                    labelText,
                    TextPosition.TopRight,
                    level.Color,
                    new SimpleFont("Arial", 10),
                    Brushes.Transparent,
                    Brushes.Transparent,
                    0
                );

                if (textDisplay != null)
                {
                    // Track the rendered label
                    if (!renderedLabels.ContainsKey(tradingDay))
                        renderedLabels[tradingDay] = new List<DrawingTool>();

                    renderedLabels[tradingDay].Add(textDisplay);

                    labelY += context.Parameters.LabelVerticalSpacing;
                    logger.LogDebug($"Rendered label for {level.Name}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to render label for {level.Name}: {ex.Message}");
            }
        }

        private void ClearLevelsInternal(DateTime tradingDay)
        {
            if (renderedLines.TryGetValue(tradingDay, out var lines))
            {
                foreach (var line in lines)
                {
                    try
                    {
                        line?.RemoveDrawObject();
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug($"Error removing line: {ex.Message}");
                    }
                }
                renderedLines.Remove(tradingDay);
            }
        }

        private void ClearLabelsInternal(DateTime tradingDay)
        {
            if (renderedLabels.TryGetValue(tradingDay, out var labels))
            {
                foreach (var label in labels)
                {
                    try
                    {
                        label?.RemoveDrawObject();
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug($"Error removing label: {ex.Message}");
                    }
                }
                renderedLabels.Remove(tradingDay);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            lock (renderLock)
            {
                if (disposed)
                    return;

                try
                {
                    ClearAllLevels();
                    disposed = true;
                    logger.LogInfo("NinjaTraderLevelRenderer disposed successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error during renderer disposal: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Factory for creating renderer instances with proper dependencies
    /// </summary>
    public static class RendererFactory
    {
        /// <summary>
        /// Creates a NinjaTrader level renderer
        /// </summary>
        public static ILevelRenderer CreateNinjaTraderRenderer(ILoggingService logger = null)
        {
            return new NinjaTraderLevelRenderer(logger ?? new ConsoleLoggingService());
        }
    }

    #endregion

    #region Drawing Tool Extensions

    /// <summary>
    /// Extension methods for NinjaTrader drawing tools
    /// </summary>
    public static class DrawingToolExtensions
    {
        /// <summary>
        /// Safely removes a drawing object with error handling
        /// </summary>
        public static void RemoveDrawObject(this DrawingTool drawingTool)
        {
            if (drawingTool == null)
                return;

            try
            {
                drawingTool.IsVisible = false;
                // Additional cleanup would be done here in real implementation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing drawing object: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Helper class for creating NinjaTrader drawing tools
    /// </summary>
    public static class Draw
    {
        /// <summary>
        /// Creates a horizontal line (placeholder for actual NinjaTrader implementation)
        /// </summary>
        public static DrawingTool HorizontalLine(
            NinjaScriptBase owner,
            string tag,
            double y,
            Brush brush,
            DashStyleHelper dashStyle,
            int width)
        {
            // This would be implemented with actual NinjaTrader drawing API
            // For now, return a placeholder
            return new MockDrawingTool(tag, y, brush);
        }

        /// <summary>
        /// Creates a text display (placeholder for actual NinjaTrader implementation)
        /// </summary>
        public static DrawingTool TextFixed(
            NinjaScriptBase owner,
            string tag,
            string text,
            TextPosition position,
            Brush textBrush,
            SimpleFont font,
            Brush areaBrush,
            Brush areaBorderBrush,
            int areaOpacity)
        {
            // This would be implemented with actual NinjaTrader drawing API
            // For now, return a placeholder
            return new MockDrawingTool(tag, 0, textBrush);
        }
    }

    /// <summary>
    /// Mock drawing tool for testing and development
    /// </summary>
    public class MockDrawingTool : DrawingTool
    {
        public string Tag { get; }
        public double Value { get; }
        public Brush Brush { get; }
        public bool IsVisible { get; set; } = true;

        public MockDrawingTool(string tag, double value, Brush brush)
        {
            Tag = tag;
            Value = value;
            Brush = brush;
        }

        public override string ToString()
        {
            return $"MockDrawingTool[{Tag}] {Value:F4} Visible:{IsVisible}";
        }
    }

    #endregion

    #region Supporting Types

    /// <summary>
    /// Base class for drawing tools
    /// </summary>
    public abstract class DrawingTool
    {
        public abstract bool IsVisible { get; set; }
    }

    /// <summary>
    /// Dash style helper enumeration
    /// </summary>
    public enum DashStyleHelper
    {
        Solid,
        Dash,
        Dot,
        DashDot
    }

    /// <summary>
    /// Text position enumeration
    /// </summary>
    public enum TextPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center
    }

    /// <summary>
    /// Simple font class for text rendering
    /// </summary>
    public class SimpleFont
    {
        public string Family { get; }
        public int Size { get; }

        public SimpleFont(string family, int size)
        {
            Family = family;
            Size = size;
        }
    }

    #endregion
}