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
		
		private Prop_Trader_Tools.FreeVWAP[] cacheFreeVWAP;

		
		public Prop_Trader_Tools.FreeVWAP FreeVWAP(string thisTool, bool showStep1, bool showStep2, bool showStep3, double std1, double std2, double std3, int setStd1Opacity, int setStd2Opacity, int setStd3Opacity, bool showWeeklyWAP, bool showOnlyWellklyWapMidLine, bool roundToTick, bool resetOnNewYorkOpenHour, int nYhour, int nYminute, bool showHourWap)
		{
			return FreeVWAP(Input, thisTool, showStep1, showStep2, showStep3, std1, std2, std3, setStd1Opacity, setStd2Opacity, setStd3Opacity, showWeeklyWAP, showOnlyWellklyWapMidLine, roundToTick, resetOnNewYorkOpenHour, nYhour, nYminute, showHourWap);
		}


		
		public Prop_Trader_Tools.FreeVWAP FreeVWAP(ISeries<double> input, string thisTool, bool showStep1, bool showStep2, bool showStep3, double std1, double std2, double std3, int setStd1Opacity, int setStd2Opacity, int setStd3Opacity, bool showWeeklyWAP, bool showOnlyWellklyWapMidLine, bool roundToTick, bool resetOnNewYorkOpenHour, int nYhour, int nYminute, bool showHourWap)
		{
			if (cacheFreeVWAP != null)
				for (int idx = 0; idx < cacheFreeVWAP.Length; idx++)
					if (cacheFreeVWAP[idx].ThisTool == thisTool && cacheFreeVWAP[idx].ShowStep1 == showStep1 && cacheFreeVWAP[idx].ShowStep2 == showStep2 && cacheFreeVWAP[idx].ShowStep3 == showStep3 && cacheFreeVWAP[idx].std1 == std1 && cacheFreeVWAP[idx].std2 == std2 && cacheFreeVWAP[idx].std3 == std3 && cacheFreeVWAP[idx].SetStd1Opacity == setStd1Opacity && cacheFreeVWAP[idx].SetStd2Opacity == setStd2Opacity && cacheFreeVWAP[idx].SetStd3Opacity == setStd3Opacity && cacheFreeVWAP[idx].ShowWeeklyWAP == showWeeklyWAP && cacheFreeVWAP[idx].ShowOnlyWellklyWapMidLine == showOnlyWellklyWapMidLine && cacheFreeVWAP[idx].RoundToTick == roundToTick && cacheFreeVWAP[idx].ResetOnNewYorkOpenHour == resetOnNewYorkOpenHour && cacheFreeVWAP[idx].NYhour == nYhour && cacheFreeVWAP[idx].NYminute == nYminute && cacheFreeVWAP[idx].ShowHourWap == showHourWap && cacheFreeVWAP[idx].EqualsInput(input))
						return cacheFreeVWAP[idx];
			return CacheIndicator<Prop_Trader_Tools.FreeVWAP>(new Prop_Trader_Tools.FreeVWAP(){ ThisTool = thisTool, ShowStep1 = showStep1, ShowStep2 = showStep2, ShowStep3 = showStep3, std1 = std1, std2 = std2, std3 = std3, SetStd1Opacity = setStd1Opacity, SetStd2Opacity = setStd2Opacity, SetStd3Opacity = setStd3Opacity, ShowWeeklyWAP = showWeeklyWAP, ShowOnlyWellklyWapMidLine = showOnlyWellklyWapMidLine, RoundToTick = roundToTick, ResetOnNewYorkOpenHour = resetOnNewYorkOpenHour, NYhour = nYhour, NYminute = nYminute, ShowHourWap = showHourWap }, input, ref cacheFreeVWAP);
		}

	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		
		public Indicators.Prop_Trader_Tools.FreeVWAP FreeVWAP(string thisTool, bool showStep1, bool showStep2, bool showStep3, double std1, double std2, double std3, int setStd1Opacity, int setStd2Opacity, int setStd3Opacity, bool showWeeklyWAP, bool showOnlyWellklyWapMidLine, bool roundToTick, bool resetOnNewYorkOpenHour, int nYhour, int nYminute, bool showHourWap)
		{
			return indicator.FreeVWAP(Input, thisTool, showStep1, showStep2, showStep3, std1, std2, std3, setStd1Opacity, setStd2Opacity, setStd3Opacity, showWeeklyWAP, showOnlyWellklyWapMidLine, roundToTick, resetOnNewYorkOpenHour, nYhour, nYminute, showHourWap);
		}


		
		public Indicators.Prop_Trader_Tools.FreeVWAP FreeVWAP(ISeries<double> input , string thisTool, bool showStep1, bool showStep2, bool showStep3, double std1, double std2, double std3, int setStd1Opacity, int setStd2Opacity, int setStd3Opacity, bool showWeeklyWAP, bool showOnlyWellklyWapMidLine, bool roundToTick, bool resetOnNewYorkOpenHour, int nYhour, int nYminute, bool showHourWap)
		{
			return indicator.FreeVWAP(input, thisTool, showStep1, showStep2, showStep3, std1, std2, std3, setStd1Opacity, setStd2Opacity, setStd3Opacity, showWeeklyWAP, showOnlyWellklyWapMidLine, roundToTick, resetOnNewYorkOpenHour, nYhour, nYminute, showHourWap);
		}
	
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		
		public Indicators.Prop_Trader_Tools.FreeVWAP FreeVWAP(string thisTool, bool showStep1, bool showStep2, bool showStep3, double std1, double std2, double std3, int setStd1Opacity, int setStd2Opacity, int setStd3Opacity, bool showWeeklyWAP, bool showOnlyWellklyWapMidLine, bool roundToTick, bool resetOnNewYorkOpenHour, int nYhour, int nYminute, bool showHourWap)
		{
			return indicator.FreeVWAP(Input, thisTool, showStep1, showStep2, showStep3, std1, std2, std3, setStd1Opacity, setStd2Opacity, setStd3Opacity, showWeeklyWAP, showOnlyWellklyWapMidLine, roundToTick, resetOnNewYorkOpenHour, nYhour, nYminute, showHourWap);
		}


		
		public Indicators.Prop_Trader_Tools.FreeVWAP FreeVWAP(ISeries<double> input , string thisTool, bool showStep1, bool showStep2, bool showStep3, double std1, double std2, double std3, int setStd1Opacity, int setStd2Opacity, int setStd3Opacity, bool showWeeklyWAP, bool showOnlyWellklyWapMidLine, bool roundToTick, bool resetOnNewYorkOpenHour, int nYhour, int nYminute, bool showHourWap)
		{
			return indicator.FreeVWAP(input, thisTool, showStep1, showStep2, showStep3, std1, std2, std3, setStd1Opacity, setStd2Opacity, setStd3Opacity, showWeeklyWAP, showOnlyWellklyWapMidLine, roundToTick, resetOnNewYorkOpenHour, nYhour, nYminute, showHourWap);
		}

	}
}

#endregion
