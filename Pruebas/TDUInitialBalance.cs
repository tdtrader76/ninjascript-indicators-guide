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
		
		private TDU.TDUInitialBalance[] cacheTDUInitialBalance;

		
		public TDU.TDUInitialBalance TDUInitialBalance()
		{
			return TDUInitialBalance(Input);
		}


		
		public TDU.TDUInitialBalance TDUInitialBalance(ISeries<double> input)
		{
			if (cacheTDUInitialBalance != null)
				for (int idx = 0; idx < cacheTDUInitialBalance.Length; idx++)
					if ( cacheTDUInitialBalance[idx].EqualsInput(input))
						return cacheTDUInitialBalance[idx];
			return CacheIndicator<TDU.TDUInitialBalance>(new TDU.TDUInitialBalance(), input, ref cacheTDUInitialBalance);
		}

	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		
		public Indicators.TDU.TDUInitialBalance TDUInitialBalance()
		{
			return indicator.TDUInitialBalance(Input);
		}


		
		public Indicators.TDU.TDUInitialBalance TDUInitialBalance(ISeries<double> input )
		{
			return indicator.TDUInitialBalance(input);
		}
	
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		
		public Indicators.TDU.TDUInitialBalance TDUInitialBalance()
		{
			return indicator.TDUInitialBalance(Input);
		}


		
		public Indicators.TDU.TDUInitialBalance TDUInitialBalance(ISeries<double> input )
		{
			return indicator.TDUInitialBalance(input);
		}

	}
}

#endregion
