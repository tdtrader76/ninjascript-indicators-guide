#region Using declarations
using System;
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

// This namespace holds all indicators and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
    public class EffectiveVolume : Indicator
	{
       	#region Variables
	 	private Series<double> myDataSeries;
      	#endregion

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Calculates effective volume based on price movement direction relative to bar range.";
				Name										= "Effective Volume";
				Calculate									= Calculate.OnPriceChange;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= false;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;

				AddPlot(Brushes.Orange, "EV");
			}
			else if (State == State.DataLoaded)
			{
				myDataSeries = new Series<double>(this);
			}
		}
        
		protected override void OnBarUpdate()
		{
			if(CurrentBar < 1)return;
			// official trading hours in the US is from 9.30am to 4.00pm
      		// 390 minutes == 390 bars, outside pre-market and after-hours
      		double high = High[0] > Close[1] ? High[0] : Close[1];
      		double low = Low[0] < Close[1] ? Low[0] : Close[1];
     		double PI = 0.01;
      		double closeDiff = Close[1] - Close[0] + PI;
      		double spread = high - low + PI;
      		double effectiveVol = closeDiff / spread * Volume[0];
      		myDataSeries[0] = effectiveVol + (CurrentBar > 1 ? myDataSeries[1] : 0);
      		Values[0][0] = myDataSeries[0];
		}
		
		#region Properties
     	[Browsable(false)]
        [XmlIgnore()]
        public Series<double> EV
        {
        	get { return Values[0]; }
		}
        #endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private EffectiveVolume[] cacheEffectiveVolume;
		public EffectiveVolume EffectiveVolume()
		{
			return EffectiveVolume(Input);
		}

		public EffectiveVolume EffectiveVolume(ISeries<double> input)
		{
			if (cacheEffectiveVolume != null)
				for (int idx = 0; idx < cacheEffectiveVolume.Length; idx++)
					if (cacheEffectiveVolume[idx] != null && cacheEffectiveVolume[idx].EqualsInput(input))
						return cacheEffectiveVolume[idx];
			return CacheIndicator<EffectiveVolume>(new EffectiveVolume(), input, ref cacheEffectiveVolume);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.EffectiveVolume EffectiveVolume()
		{
			return indicator.EffectiveVolume(Input);
		}

		public Indicators.EffectiveVolume EffectiveVolume(ISeries<double> input )
		{
			return indicator.EffectiveVolume(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.EffectiveVolume EffectiveVolume()
		{
			return indicator.EffectiveVolume(Input);
		}

		public Indicators.EffectiveVolume EffectiveVolume(ISeries<double> input )
		{
			return indicator.EffectiveVolume(input);
		}
	}
}

#endregion