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

#endregion



#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		
		private CustomVolume[] cacheCustomVolume;

		
		public CustomVolume CustomVolume(int mAPeriod)
		{
			return CustomVolume(Input, mAPeriod);
		}


		
		public CustomVolume CustomVolume(ISeries<double> input, int mAPeriod)
		{
			if (cacheCustomVolume != null)
				for (int idx = 0; idx < cacheCustomVolume.Length; idx++)
					if (cacheCustomVolume[idx].MAPeriod == mAPeriod && cacheCustomVolume[idx].EqualsInput(input))
						return cacheCustomVolume[idx];
			return CacheIndicator<CustomVolume>(new CustomVolume(){ MAPeriod = mAPeriod }, input, ref cacheCustomVolume);
		}

	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		
		public Indicators.CustomVolume CustomVolume(int mAPeriod)
		{
			return indicator.CustomVolume(Input, mAPeriod);
		}


		
		public Indicators.CustomVolume CustomVolume(ISeries<double> input , int mAPeriod)
		{
			return indicator.CustomVolume(input, mAPeriod);
		}
	
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		
		public Indicators.CustomVolume CustomVolume(int mAPeriod)
		{
			return indicator.CustomVolume(Input, mAPeriod);
		}


		
		public Indicators.CustomVolume CustomVolume(ISeries<double> input , int mAPeriod)
		{
			return indicator.CustomVolume(input, mAPeriod);
		}

	}
}

#endregion
