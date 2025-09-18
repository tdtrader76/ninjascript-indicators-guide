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
		
		private ChartSpots.UntestedLevels[] cacheUntestedLevels;

		
		public ChartSpots.UntestedLevels UntestedLevels(DateTime openingTime, DateTime closeTime)
		{
			return UntestedLevels(Input, openingTime, closeTime);
		}


		
		public ChartSpots.UntestedLevels UntestedLevels(ISeries<double> input, DateTime openingTime, DateTime closeTime)
		{
			if (cacheUntestedLevels != null)
				for (int idx = 0; idx < cacheUntestedLevels.Length; idx++)
					if (cacheUntestedLevels[idx].OpeningTime == openingTime && cacheUntestedLevels[idx].CloseTime == closeTime && cacheUntestedLevels[idx].EqualsInput(input))
						return cacheUntestedLevels[idx];
			return CacheIndicator<ChartSpots.UntestedLevels>(new ChartSpots.UntestedLevels(){ OpeningTime = openingTime, CloseTime = closeTime }, input, ref cacheUntestedLevels);
		}

	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		
		public Indicators.ChartSpots.UntestedLevels UntestedLevels(DateTime openingTime, DateTime closeTime)
		{
			return indicator.UntestedLevels(Input, openingTime, closeTime);
		}


		
		public Indicators.ChartSpots.UntestedLevels UntestedLevels(ISeries<double> input , DateTime openingTime, DateTime closeTime)
		{
			return indicator.UntestedLevels(input, openingTime, closeTime);
		}
	
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		
		public Indicators.ChartSpots.UntestedLevels UntestedLevels(DateTime openingTime, DateTime closeTime)
		{
			return indicator.UntestedLevels(Input, openingTime, closeTime);
		}


		
		public Indicators.ChartSpots.UntestedLevels UntestedLevels(ISeries<double> input , DateTime openingTime, DateTime closeTime)
		{
			return indicator.UntestedLevels(input, openingTime, closeTime);
		}

	}
}

#endregion
