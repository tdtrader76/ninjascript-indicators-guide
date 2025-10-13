using NinjaTrader.Gui.Chart;
using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Indicators.RyF
{
    public class DirectXLevelRenderer : IDisposable
    {
        private readonly RenderTarget renderTarget;
        private readonly ChartControl chartControl;
        private readonly ChartScale chartScale;
        private readonly int lineWidth;

        // Resource Management
        private readonly DisposableResourceManager resourceManager = new DisposableResourceManager();
        private readonly Dictionary<Brush, SharpDX.Direct2D1.Brush> dxBrushes = new Dictionary<Brush, SharpDX.Direct2D1.Brush>();

        public DirectXLevelRenderer(RenderTarget rt, ChartControl cc, ChartScale cs, int width)
        {
            renderTarget = rt;
            chartControl = cc;
            chartScale = cs;
            lineWidth = width;
        }

        public void RenderLevels(IEnumerable<PriceLevel> levels, int startBarIndex, int endBarIndex)
        {
            if (levels == null || startBarIndex < 0)
                return;

            float lineStartX = (float)chartControl.GetXByBarIndex(chartControl.ChartBars, startBarIndex);
            float lineEndX = endBarIndex != -1
                ? (float)chartControl.GetXByBarIndex(chartControl.ChartBars, endBarIndex)
                : (float)chartControl.CanvasRight;

            foreach (var level in levels)
            {
                if (double.IsNaN(level.Value) || level.Value <= 0) continue;

                float y = chartScale.GetYByValue(level.Value);
                if (float.IsNaN(lineStartX) || float.IsNaN(lineEndX)) continue;

                SharpDX.Direct2D1.Brush dxBrush = GetDxBrush(level.LineBrush);
                if (dxBrush == null) continue;

                renderTarget.DrawLine(new SharpDX.Vector2(lineStartX, y), new SharpDX.Vector2(lineEndX, y), dxBrush, lineWidth);
            }
        }

        private SharpDX.Direct2D1.Brush GetDxBrush(Brush wpfBrush)
        {
            if (dxBrushes.TryGetValue(wpfBrush, out SharpDX.Direct2D1.Brush dxBrush))
                return dxBrush;

            // Create the brush and add it to the resource manager for safe disposal
            dxBrush = resourceManager.AddResource(wpfBrush.ToDxBrush(renderTarget));
            dxBrushes.Add(wpfBrush, dxBrush);
            return dxBrush;
        }

        public void Dispose()
        {
            // Delegate disposal to the resource manager
            resourceManager.Dispose();
            dxBrushes.Clear();
        }
    }
}