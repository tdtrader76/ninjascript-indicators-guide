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
            }
        }

        protected override void OnBarUpdate()
        {
            try
            {
                if (Bars.IsFirstBarOfSession)
                {
                    lastAD = 0;
                }

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

                // --- Nueva lógica de dibujo ---
                Brush textColor = lastAD > 0 ? Brushes.Green : Brushes.Red;
                string labelText = lastAD.ToString("F0");
                Draw.TextFixed(this, "EffectiveVolumeLabel", labelText, TextPosition.BottomCenter, textColor, new SimpleFont("Arial", LabelFontSize), Brushes.Transparent, Brushes.Transparent, 0);
            }
            catch (Exception ex)
            {
                Log($"RyFLb OnBarUpdate error: {ex.Message}", LogLevel.Error);
            }
        }


        #region Properties

        [NinjaScriptProperty]
        [Range(8, 72)]
        [Display(Name = "Label Font Size", Description = "Font size for the volume label", Order = 3, GroupName = "Visual")]
        public int LabelFontSize
        {
            get { return labelFontSize; }
            set { labelFontSize = Math.Max(8, Math.Min(72, value)); }
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
		public RyFLb RyFLb(int labelFontSize)
		{
			return RyFLb(Input, labelFontSize);
		}

		public RyFLb RyFLb(ISeries<double> input, int labelFontSize)
		{
			if (cacheRyFLb != null)
				for (int idx = 0; idx < cacheRyFLb.Length; idx++)
					if (cacheRyFLb[idx] != null && cacheRyFLb[idx].LabelFontSize == labelFontSize && cacheRyFLb[idx].EqualsInput(input))
						return cacheRyFLb[idx];
			return CacheIndicator<RyFLb>(new RyFLb(){ LabelFontSize = labelFontSize }, input, ref cacheRyFLb);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RyFLb RyFLb(int labelFontSize)
		{
			return indicator.RyFLb(Input, labelFontSize);
		}

		public Indicators.RyFLb RyFLb(ISeries<double> input , int labelFontSize)
		{
			return indicator.RyFLb(input, labelFontSize);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RyFLb RyFLb(int labelFontSize)
		{
			return indicator.RyFLb(Input, labelFontSize);
		}

		public Indicators.RyFLb RyFLb(ISeries<double> input , int labelFontSize)
		{
			return indicator.RyFLb(input, labelFontSize);
		}
	}
}

#endregion
