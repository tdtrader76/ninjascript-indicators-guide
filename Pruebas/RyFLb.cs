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

public enum LabelAlignment
		{
			Left,
			Center,
			Right
		}

namespace NinjaTrader.NinjaScript.Indicators
{
    public class RyFLb : Indicator
    {
		#region Enums
		
		#endregion

        private int labelFontSize = 16;
        private double lastAD = 0;

		// Variables para cálculos multi-timeframe
		private double sum30Min = 0;
		private int count30Min = 0;
		private double avg30Min = 0;

		private double sumDaily = 0;
		private int countDaily = 0;
		private double avgDaily = 0;

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
        
                // Reset daily accumulators at the start of a new session
                if (IsFirstBarOfSession)
                {
                    sumDaily = 0;
                    countDaily = 0;
					avgDaily = 0;
					
					// Also reset 30-min accumulator at session start
					sum30Min = 0;
					count30Min = 0;
					avg30Min = 0;
                }
				else // Not the first bar of the session, so check for 30-min boundary
				{
					// Robustly check for crossing a 30-minute boundary, works for any timeframe
					var currentBlock = Math.Floor(Time[0].TimeOfDay.TotalMinutes / 30);
					var previousBlock = Math.Floor(Time[1].TimeOfDay.TotalMinutes / 30);

					if (currentBlock != previousBlock)
					{
						sum30Min = 0;
						count30Min = 0;
						avg30Min = 0;
					}
				}

                double volume = Volume[0];
                double hi = Math.Max(Close[1], High[0]);
                double lo = Math.Min(Close[1], Low[0]);
                double ad = 0;
                double range = hi - lo;

                if (range > double.Epsilon)
                {
                    ad = ((Close[0] - Close[1]) / range) * volume;
                }

                // --- MTF Accumulations ---
                // Add current bar's effective volume to accumulators
                sum30Min += ad;
                count30Min++;
                sumDaily += ad;
                countDaily++;

                // Calculate averages, avoiding division by zero
                avg30Min = (count30Min > 0) ? sum30Min / count30Min : 0;
                avgDaily = (countDaily > 0) ? sumDaily / countDaily : 0;
				
                // Store current value for label display
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
			
			// --- Construir la etiqueta ---
			StringBuilder labelBuilder = new StringBuilder();
			
			// 1. Valor actual
			labelBuilder.Append($"({lastAD:F0})");

			// 2. Valor de 30 minutos (si el timeframe es < 30 min)
			if (Bars.BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && Bars.BarsPeriod.Value < 30)
			{
				labelBuilder.Append($" ({avg30Min:F0})");
			}

			// 3. Valor diario (si el timeframe es < 1 día)
			if (Bars.BarsPeriod.BarsPeriodType < BarsPeriodType.Day)
			{
				labelBuilder.Append($" ({avgDaily:F0})");
			}
			
			string labelText = labelBuilder.ToString();
			
            // --- Lógica de dibujado ---
            ChartPanel panel = chartControl.ChartPanels[chartScale.PanelIndex];
            float baseY = panel.Y + panel.H;
            float finalY = baseY - YOffset;

            SharpDX.Color labelColor = lastAD > 0 ? SharpDX.Color.ForestGreen : SharpDX.Color.IndianRed;
            
            if (Core.Globals.DirectWriteFactory == null || RenderTarget == null)
                return;

            string fontFamily = "Arial";
            float fontSize = (float)LabelFontSize;

            using (SharpDX.DirectWrite.TextFormat textFormat = new SharpDX.DirectWrite.TextFormat(
                Core.Globals.DirectWriteFactory, fontFamily,
                SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, fontSize))
            {
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

                using (SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(
                    Core.Globals.DirectWriteFactory, labelText, textFormat, panel.W, fontSize))
                {
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

                    float finalX = baseX + XOffset;
                    finalY = finalY - textLayout.Metrics.Height;

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
		public RyFLb RyFLb(int labelFontSize, LabelAlignment alignment, int xOffset, int yOffset)
		{
			return RyFLb(Input, labelFontSize, alignment, xOffset, yOffset);
		}

		public RyFLb RyFLb(ISeries<double> input, int labelFontSize, LabelAlignment alignment, int xOffset, int yOffset)
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
		public Indicators.RyFLb RyFLb(int labelFontSize, LabelAlignment alignment, int xOffset, int yOffset)
		{
			return indicator.RyFLb(Input, labelFontSize, alignment, xOffset, yOffset);
		}

		public Indicators.RyFLb RyFLb(ISeries<double> input , int labelFontSize, LabelAlignment alignment, int xOffset, int yOffset)
		{
			return indicator.RyFLb(input, labelFontSize, alignment, xOffset, yOffset);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RyFLb RyFLb(int labelFontSize, LabelAlignment alignment, int xOffset, int yOffset)
		{
			return indicator.RyFLb(Input, labelFontSize, alignment, xOffset, yOffset);
		}

		public Indicators.RyFLb RyFLb(ISeries<double> input , int labelFontSize, LabelAlignment alignment, int xOffset, int yOffset)
		{
			return indicator.RyFLb(input, labelFontSize, alignment, xOffset, yOffset);
		}
	}
}

#endregion
