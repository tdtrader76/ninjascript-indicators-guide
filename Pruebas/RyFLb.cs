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
    public class RyFLb : Indicator
    {
		#region Enums
		public enum LabelAlignment
		{
			Left,
			Center,
			Right
		}
		#endregion

        private int labelFontSize = 16;
        private double lastAD = 0;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"";
                Name = "RyFLb";
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
                LabelFontSize = 16;
                Alignment = LabelAlignment.Center;
                XOffset = 0;
                YOffset = 0;
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

            }
            catch (Exception ex)
            {
                Log($"RyFLb OnBarUpdate error: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (Bars == null || ChartControl == null || CurrentBar < 1)
                return;

            // Get the chart panel
            ChartPanel panel = chartControl.ChartPanels[chartScale.PanelIndex];

            // Calculate base Y position at the bottom of the panel, then apply offset
            float baseY = panel.Y + panel.H;
            float finalY = baseY - YOffset; // Subtract because Y=0 is at the top

            // Format the label text and choose color
            string labelText = lastAD.ToString("F0");
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
                // Set alignment for the TextFormat object itself
                switch (Alignment)
                {
                    case LabelAlignment.Left:
                        textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
                        break;
                    case LabelAlignment.Center:
                        textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
                        break;
                    case LabelAlignment.Right:
                        textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Trailing;
                        break;
                }

                // Calculate the full layout box of the text
                using (SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(
                    Core.Globals.DirectWriteFactory, labelText, textFormat, panel.W, fontSize))
                {
                    // Calculate base X based on panel width and alignment
                    float baseX = 0;
                    switch (Alignment)
                    {
                        case LabelAlignment.Left:
                            baseX = panel.X;
                            break;
                        case LabelAlignment.Center:
                            baseX = panel.X + (panel.W / 2);
                            break;
                        case LabelAlignment.Right:
                            baseX = panel.X + panel.W;
                            break;
                    }

                    // Apply X offset
                    float finalX = baseX + XOffset;

                    // Adjust Y position to be just above the bottom of the panel
                    finalY = finalY - textLayout.Metrics.Height;

                    // Draw the text
                    using (SharpDX.Direct2D1.SolidColorBrush brush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, labelColor))
                    {
                        RenderTarget.DrawTextLayout(
                            new SharpDX.Vector2(finalX, finalY),
                            textLayout,
                            brush,
                            SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                    }
                }
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(8, 72)]
        [Display(Name = "Label Font Size", Description = "Font size for the volume label", Order = 1, GroupName = "Visual")]
        public int LabelFontSize
        {
            get { return labelFontSize; }
            set { labelFontSize = Math.Max(8, Math.Min(72, value)); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Alignment", Description = "Horizontal alignment of the label", Order = 2, GroupName = "Visual")]
        public LabelAlignment Alignment
        {
            get; set;
        }

        [NinjaScriptProperty]
        [Range(-1000, 1000)]
        [Display(Name = "X Offset", Description = "Horizontal offset in pixels", Order = 3, GroupName = "Visual")]
        public int XOffset
        {
            get; set;
        }

        [NinjaScriptProperty]
        [Range(-1000, 1000)]
        [Display(Name = "Y Offset", Description = "Vertical offset in pixels", Order = 4, GroupName = "Visual")]
        public int YOffset
        {
            get; set;
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private RyFLb[] cacheRyFLb;
		public RyFLb RyFLb(int labelFontSize, NinjaTrader.NinjaScript.Indicators.RyFLb.LabelAlignment alignment, int xOffset, int yOffset)
		{
			return RyFLb(Input, labelFontSize, alignment, xOffset, yOffset);
		}

		public RyFLb RyFLb(ISeries<double> input, int labelFontSize, NinjaTrader.NinjaScript.Indicators.RyFLb.LabelAlignment alignment, int xOffset, int yOffset)
		{
			if (cacheRyFLb != null)
				for (int idx = 0; idx < cacheRyFLb.Length; idx++)
					if (cacheRyFLb[idx] != null && cacheRyFLb[idx].LabelFontSize == labelFontSize && cacheRyFLb[idx].Alignment == alignment && cacheRyFLb[idx].XOffset == xOffset && cacheRyFLb[idx].YOffset == yOffset && cacheRyFLb[idx].EqualsInput(input))
						return cacheRyFLb[idx];
			return CacheIndicator<RyFLb>(new RyFLb(){ LabelFontSize = labelFontSize, Alignment = alignment, XOffset = xOffset, YOffset = yOffset }, input, ref cacheRyFLb);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RyFLb RyFLb(int labelFontSize, NinjaTrader.NinjaScript.Indicators.RyFLb.LabelAlignment alignment, int xOffset, int yOffset)
		{
			return indicator.RyFLb(Input, labelFontSize, alignment, xOffset, yOffset);
		}

		public Indicators.RyFLb RyFLb(ISeries<double> input , int labelFontSize, NinjaTrader.NinjaScript.Indicators.RyFLb.LabelAlignment alignment, int xOffset, int yOffset)
		{
			return indicator.RyFLb(input, labelFontSize, alignment, xOffset, yOffset);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RyFLb RyFLb(int labelFontSize, NinjaTrader.NinjaScript.Indicators.RyFLb.LabelAlignment alignment, int xOffset, int yOffset)
		{
			return indicator.RyFLb(Input, labelFontSize, alignment, xOffset, yOffset);
		}

		public Indicators.RyFLb RyFLb(ISeries<double> input , int labelFontSize, NinjaTrader.NinjaScript.Indicators.RyFLb.LabelAlignment alignment, int xOffset, int yOffset)
		{
			return indicator.RyFLb(input, labelFontSize, alignment, xOffset, yOffset);
		}
	}
}

#endregion
