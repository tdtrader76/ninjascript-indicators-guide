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
		
		private EPyF.EPyFCandles[] cacheEPyFCandles;
		private EPyF.EPyFOneClickOrders[] cacheEPyFOneClickOrders;
		private EPyF.EPyFPriorDayLevels[] cacheEPyFPriorDayLevels;

		
		public EPyF.EPyFCandles EPyFCandles()
		{
			return EPyFCandles(Input);
		}

		public EPyF.EPyFOneClickOrders EPyFOneClickOrders(EPyFOrderTypeSelector myOrderTypeSelection, double offset, bool toggle, bool drawRR, int stop)
		{
			return EPyFOneClickOrders(Input, myOrderTypeSelection, offset, toggle, drawRR, stop);
		}

		public EPyF.EPyFPriorDayLevels EPyFPriorDayLevels()
		{
			return EPyFPriorDayLevels(Input);
		}


		
		public EPyF.EPyFCandles EPyFCandles(ISeries<double> input)
		{
			if (cacheEPyFCandles != null)
				for (int idx = 0; idx < cacheEPyFCandles.Length; idx++)
					if ( cacheEPyFCandles[idx].EqualsInput(input))
						return cacheEPyFCandles[idx];
			return CacheIndicator<EPyF.EPyFCandles>(new EPyF.EPyFCandles(), input, ref cacheEPyFCandles);
		}

		public EPyF.EPyFOneClickOrders EPyFOneClickOrders(ISeries<double> input, EPyFOrderTypeSelector myOrderTypeSelection, double offset, bool toggle, bool drawRR, int stop)
		{
			if (cacheEPyFOneClickOrders != null)
				for (int idx = 0; idx < cacheEPyFOneClickOrders.Length; idx++)
					if (cacheEPyFOneClickOrders[idx].MyOrderTypeSelection == myOrderTypeSelection && cacheEPyFOneClickOrders[idx].Offset == offset && cacheEPyFOneClickOrders[idx].Toggle == toggle && cacheEPyFOneClickOrders[idx].DrawRR == drawRR && cacheEPyFOneClickOrders[idx].Stop == stop && cacheEPyFOneClickOrders[idx].EqualsInput(input))
						return cacheEPyFOneClickOrders[idx];
			return CacheIndicator<EPyF.EPyFOneClickOrders>(new EPyF.EPyFOneClickOrders(){ MyOrderTypeSelection = myOrderTypeSelection, Offset = offset, Toggle = toggle, DrawRR = drawRR, Stop = stop }, input, ref cacheEPyFOneClickOrders);
		}

		public EPyF.EPyFPriorDayLevels EPyFPriorDayLevels(ISeries<double> input)
		{
			if (cacheEPyFPriorDayLevels != null)
				for (int idx = 0; idx < cacheEPyFPriorDayLevels.Length; idx++)
					if ( cacheEPyFPriorDayLevels[idx].EqualsInput(input))
						return cacheEPyFPriorDayLevels[idx];
			return CacheIndicator<EPyF.EPyFPriorDayLevels>(new EPyF.EPyFPriorDayLevels(), input, ref cacheEPyFPriorDayLevels);
		}

	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		
		public Indicators.EPyF.EPyFCandles EPyFCandles()
		{
			return indicator.EPyFCandles(Input);
		}

		public Indicators.EPyF.EPyFOneClickOrders EPyFOneClickOrders(EPyFOrderTypeSelector myOrderTypeSelection, double offset, bool toggle, bool drawRR, int stop)
		{
			return indicator.EPyFOneClickOrders(Input, myOrderTypeSelection, offset, toggle, drawRR, stop);
		}

		public Indicators.EPyF.EPyFPriorDayLevels EPyFPriorDayLevels()
		{
			return indicator.EPyFPriorDayLevels(Input);
		}


		
		public Indicators.EPyF.EPyFCandles EPyFCandles(ISeries<double> input )
		{
			return indicator.EPyFCandles(input);
		}

		public Indicators.EPyF.EPyFOneClickOrders EPyFOneClickOrders(ISeries<double> input , EPyFOrderTypeSelector myOrderTypeSelection, double offset, bool toggle, bool drawRR, int stop)
		{
			return indicator.EPyFOneClickOrders(input, myOrderTypeSelection, offset, toggle, drawRR, stop);
		}

		public Indicators.EPyF.EPyFPriorDayLevels EPyFPriorDayLevels(ISeries<double> input )
		{
			return indicator.EPyFPriorDayLevels(input);
		}
	
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		
		public Indicators.EPyF.EPyFCandles EPyFCandles()
		{
			return indicator.EPyFCandles(Input);
		}

		public Indicators.EPyF.EPyFOneClickOrders EPyFOneClickOrders(EPyFOrderTypeSelector myOrderTypeSelection, double offset, bool toggle, bool drawRR, int stop)
		{
			return indicator.EPyFOneClickOrders(Input, myOrderTypeSelection, offset, toggle, drawRR, stop);
		}

		public Indicators.EPyF.EPyFPriorDayLevels EPyFPriorDayLevels()
		{
			return indicator.EPyFPriorDayLevels(Input);
		}


		
		public Indicators.EPyF.EPyFCandles EPyFCandles(ISeries<double> input )
		{
			return indicator.EPyFCandles(input);
		}

		public Indicators.EPyF.EPyFOneClickOrders EPyFOneClickOrders(ISeries<double> input , EPyFOrderTypeSelector myOrderTypeSelection, double offset, bool toggle, bool drawRR, int stop)
		{
			return indicator.EPyFOneClickOrders(input, myOrderTypeSelection, offset, toggle, drawRR, stop);
		}

		public Indicators.EPyF.EPyFPriorDayLevels EPyFPriorDayLevels(ISeries<double> input )
		{
			return indicator.EPyFPriorDayLevels(input);
		}

	}
}

#endregion
