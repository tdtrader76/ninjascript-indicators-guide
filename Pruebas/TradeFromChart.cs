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
		
		private PropTraderz.TradeFromChart[] cacheTradeFromChart;

		
		public PropTraderz.TradeFromChart TradeFromChart()
		{
			return TradeFromChart(Input);
		}


		
		public PropTraderz.TradeFromChart TradeFromChart(ISeries<double> input)
		{
			if (cacheTradeFromChart != null)
				for (int idx = 0; idx < cacheTradeFromChart.Length; idx++)
					if ( cacheTradeFromChart[idx].EqualsInput(input))
						return cacheTradeFromChart[idx];
			return CacheIndicator<PropTraderz.TradeFromChart>(new PropTraderz.TradeFromChart(), input, ref cacheTradeFromChart);
		}

	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		
		public Indicators.PropTraderz.TradeFromChart TradeFromChart()
		{
			return indicator.TradeFromChart(Input);
		}


		
		public Indicators.PropTraderz.TradeFromChart TradeFromChart(ISeries<double> input )
		{
			return indicator.TradeFromChart(input);
		}
	
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		
		public Indicators.PropTraderz.TradeFromChart TradeFromChart()
		{
			return indicator.TradeFromChart(Input);
		}


		
		public Indicators.PropTraderz.TradeFromChart TradeFromChart(ISeries<double> input )
		{
			return indicator.TradeFromChart(input);
		}

	}
}

#endregion
