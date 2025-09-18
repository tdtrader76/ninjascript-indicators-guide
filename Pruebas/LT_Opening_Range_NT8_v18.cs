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
		
		private LizardTrader.LT_Opening_Range[] cacheLT_Opening_Range;
		private LizardTrader.LT_Opening_Range_S[] cacheLT_Opening_Range_S;

		
		public LizardTrader.LT_Opening_Range LT_Opening_Range(ltSessionTypeOR sessionType, ltTimeZonesOR customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeOR preSessionType, ltTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeOR bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return LT_Opening_Range(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
		}

		public LizardTrader.LT_Opening_Range_S LT_Opening_Range_S(ltSessionTypeORS sessionType, ltTimeZonesORS customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeORS preSessionType, ltTimeZonesORS preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeORS bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return LT_Opening_Range_S(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
		}


		
		public LizardTrader.LT_Opening_Range LT_Opening_Range(ISeries<double> input, ltSessionTypeOR sessionType, ltTimeZonesOR customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeOR preSessionType, ltTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeOR bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			if (cacheLT_Opening_Range != null)
				for (int idx = 0; idx < cacheLT_Opening_Range.Length; idx++)
					if (cacheLT_Opening_Range[idx].SessionType == sessionType && cacheLT_Opening_Range[idx].CustomTZSelector == customTZSelector && cacheLT_Opening_Range[idx].S_CustomSessionStart == s_CustomSessionStart && cacheLT_Opening_Range[idx].S_OpeningRangePeriod == s_OpeningRangePeriod && cacheLT_Opening_Range[idx].PreSessionType == preSessionType && cacheLT_Opening_Range[idx].PreSessionTZSelector == preSessionTZSelector && cacheLT_Opening_Range[idx].S_PreSessionStart == s_PreSessionStart && cacheLT_Opening_Range[idx].S_PreSessionEnd == s_PreSessionEnd && cacheLT_Opening_Range[idx].BandType == bandType && cacheLT_Opening_Range[idx].Percentage1 == percentage1 && cacheLT_Opening_Range[idx].Percentage2 == percentage2 && cacheLT_Opening_Range[idx].Percentage3 == percentage3 && cacheLT_Opening_Range[idx].Percentage4 == percentage4 && cacheLT_Opening_Range[idx].EqualsInput(input))
						return cacheLT_Opening_Range[idx];
			return CacheIndicator<LizardTrader.LT_Opening_Range>(new LizardTrader.LT_Opening_Range(){ SessionType = sessionType, CustomTZSelector = customTZSelector, S_CustomSessionStart = s_CustomSessionStart, S_OpeningRangePeriod = s_OpeningRangePeriod, PreSessionType = preSessionType, PreSessionTZSelector = preSessionTZSelector, S_PreSessionStart = s_PreSessionStart, S_PreSessionEnd = s_PreSessionEnd, BandType = bandType, Percentage1 = percentage1, Percentage2 = percentage2, Percentage3 = percentage3, Percentage4 = percentage4 }, input, ref cacheLT_Opening_Range);
		}

		public LizardTrader.LT_Opening_Range_S LT_Opening_Range_S(ISeries<double> input, ltSessionTypeORS sessionType, ltTimeZonesORS customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeORS preSessionType, ltTimeZonesORS preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeORS bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			if (cacheLT_Opening_Range_S != null)
				for (int idx = 0; idx < cacheLT_Opening_Range_S.Length; idx++)
					if (cacheLT_Opening_Range_S[idx].SessionType == sessionType && cacheLT_Opening_Range_S[idx].CustomTZSelector == customTZSelector && cacheLT_Opening_Range_S[idx].S_CustomSessionStart == s_CustomSessionStart && cacheLT_Opening_Range_S[idx].S_OpeningRangePeriod == s_OpeningRangePeriod && cacheLT_Opening_Range_S[idx].PreSessionType == preSessionType && cacheLT_Opening_Range_S[idx].PreSessionTZSelector == preSessionTZSelector && cacheLT_Opening_Range_S[idx].S_PreSessionStart == s_PreSessionStart && cacheLT_Opening_Range_S[idx].S_PreSessionEnd == s_PreSessionEnd && cacheLT_Opening_Range_S[idx].BandType == bandType && cacheLT_Opening_Range_S[idx].Percentage1 == percentage1 && cacheLT_Opening_Range_S[idx].Percentage2 == percentage2 && cacheLT_Opening_Range_S[idx].Percentage3 == percentage3 && cacheLT_Opening_Range_S[idx].Percentage4 == percentage4 && cacheLT_Opening_Range_S[idx].EqualsInput(input))
						return cacheLT_Opening_Range_S[idx];
			return CacheIndicator<LizardTrader.LT_Opening_Range_S>(new LizardTrader.LT_Opening_Range_S(){ SessionType = sessionType, CustomTZSelector = customTZSelector, S_CustomSessionStart = s_CustomSessionStart, S_OpeningRangePeriod = s_OpeningRangePeriod, PreSessionType = preSessionType, PreSessionTZSelector = preSessionTZSelector, S_PreSessionStart = s_PreSessionStart, S_PreSessionEnd = s_PreSessionEnd, BandType = bandType, Percentage1 = percentage1, Percentage2 = percentage2, Percentage3 = percentage3, Percentage4 = percentage4 }, input, ref cacheLT_Opening_Range_S);
		}

	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		
		public Indicators.LizardTrader.LT_Opening_Range LT_Opening_Range(ltSessionTypeOR sessionType, ltTimeZonesOR customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeOR preSessionType, ltTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeOR bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.LT_Opening_Range(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
		}

		public Indicators.LizardTrader.LT_Opening_Range_S LT_Opening_Range_S(ltSessionTypeORS sessionType, ltTimeZonesORS customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeORS preSessionType, ltTimeZonesORS preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeORS bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.LT_Opening_Range_S(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
		}


		
		public Indicators.LizardTrader.LT_Opening_Range LT_Opening_Range(ISeries<double> input , ltSessionTypeOR sessionType, ltTimeZonesOR customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeOR preSessionType, ltTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeOR bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.LT_Opening_Range(input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
		}

		public Indicators.LizardTrader.LT_Opening_Range_S LT_Opening_Range_S(ISeries<double> input , ltSessionTypeORS sessionType, ltTimeZonesORS customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeORS preSessionType, ltTimeZonesORS preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeORS bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.LT_Opening_Range_S(input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
		}
	
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		
		public Indicators.LizardTrader.LT_Opening_Range LT_Opening_Range(ltSessionTypeOR sessionType, ltTimeZonesOR customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeOR preSessionType, ltTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeOR bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.LT_Opening_Range(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
		}

		public Indicators.LizardTrader.LT_Opening_Range_S LT_Opening_Range_S(ltSessionTypeORS sessionType, ltTimeZonesORS customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeORS preSessionType, ltTimeZonesORS preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeORS bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.LT_Opening_Range_S(Input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
		}


		
		public Indicators.LizardTrader.LT_Opening_Range LT_Opening_Range(ISeries<double> input , ltSessionTypeOR sessionType, ltTimeZonesOR customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeOR preSessionType, ltTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeOR bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.LT_Opening_Range(input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
		}

		public Indicators.LizardTrader.LT_Opening_Range_S LT_Opening_Range_S(ISeries<double> input , ltSessionTypeORS sessionType, ltTimeZonesORS customTZSelector, string s_CustomSessionStart, string s_OpeningRangePeriod, ltPreSessionTypeORS preSessionType, ltTimeZonesORS preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd, ltBandTypeORS bandType, double percentage1, double percentage2, double percentage3, double percentage4)
		{
			return indicator.LT_Opening_Range_S(input, sessionType, customTZSelector, s_CustomSessionStart, s_OpeningRangePeriod, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd, bandType, percentage1, percentage2, percentage3, percentage4);
		}

	}
}

#endregion
