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
		
		private pjsOpeningRangeV2[] cachepjsOpeningRangeV2;

		
		public pjsOpeningRangeV2 pjsOpeningRangeV2(string productKey, double oRstarts, double oRends, double oRendsDuration, double extension1, double extension2, double extension3, bool showMidPoint, bool showHighLow, bool showRangeOpen, bool showRangeClose, bool showPrice, bool showRangeLabel, bool showRangeOpenCloseLabel, string label, bool hideUntilFormed, bool timesFromMktData, DashStyleHelper detailDashStyle, bool use24x7dataseries)
		{
			return pjsOpeningRangeV2(Input, productKey, oRstarts, oRends, oRendsDuration, extension1, extension2, extension3, showMidPoint, showHighLow, showRangeOpen, showRangeClose, showPrice, showRangeLabel, showRangeOpenCloseLabel, label, hideUntilFormed, timesFromMktData, detailDashStyle, use24x7dataseries);
		}


		
		public pjsOpeningRangeV2 pjsOpeningRangeV2(ISeries<double> input, string productKey, double oRstarts, double oRends, double oRendsDuration, double extension1, double extension2, double extension3, bool showMidPoint, bool showHighLow, bool showRangeOpen, bool showRangeClose, bool showPrice, bool showRangeLabel, bool showRangeOpenCloseLabel, string label, bool hideUntilFormed, bool timesFromMktData, DashStyleHelper detailDashStyle, bool use24x7dataseries)
		{
			if (cachepjsOpeningRangeV2 != null)
				for (int idx = 0; idx < cachepjsOpeningRangeV2.Length; idx++)
					if (cachepjsOpeningRangeV2[idx].productKey == productKey && cachepjsOpeningRangeV2[idx].ORstarts == oRstarts && cachepjsOpeningRangeV2[idx].ORends == oRends && cachepjsOpeningRangeV2[idx].ORendsDuration == oRendsDuration && cachepjsOpeningRangeV2[idx].Extension1 == extension1 && cachepjsOpeningRangeV2[idx].Extension2 == extension2 && cachepjsOpeningRangeV2[idx].Extension3 == extension3 && cachepjsOpeningRangeV2[idx].ShowMidPoint == showMidPoint && cachepjsOpeningRangeV2[idx].ShowHighLow == showHighLow && cachepjsOpeningRangeV2[idx].ShowRangeOpen == showRangeOpen && cachepjsOpeningRangeV2[idx].ShowRangeClose == showRangeClose && cachepjsOpeningRangeV2[idx].ShowPrice == showPrice && cachepjsOpeningRangeV2[idx].ShowRangeLabel == showRangeLabel && cachepjsOpeningRangeV2[idx].ShowRangeOpenCloseLabel == showRangeOpenCloseLabel && cachepjsOpeningRangeV2[idx].Label == label && cachepjsOpeningRangeV2[idx].hideUntilFormed == hideUntilFormed && cachepjsOpeningRangeV2[idx].timesFromMktData == timesFromMktData && cachepjsOpeningRangeV2[idx].DetailDashStyle == detailDashStyle && cachepjsOpeningRangeV2[idx].use24x7dataseries == use24x7dataseries && cachepjsOpeningRangeV2[idx].EqualsInput(input))
						return cachepjsOpeningRangeV2[idx];
			return CacheIndicator<pjsOpeningRangeV2>(new pjsOpeningRangeV2(){ productKey = productKey, ORstarts = oRstarts, ORends = oRends, ORendsDuration = oRendsDuration, Extension1 = extension1, Extension2 = extension2, Extension3 = extension3, ShowMidPoint = showMidPoint, ShowHighLow = showHighLow, ShowRangeOpen = showRangeOpen, ShowRangeClose = showRangeClose, ShowPrice = showPrice, ShowRangeLabel = showRangeLabel, ShowRangeOpenCloseLabel = showRangeOpenCloseLabel, Label = label, hideUntilFormed = hideUntilFormed, timesFromMktData = timesFromMktData, DetailDashStyle = detailDashStyle, use24x7dataseries = use24x7dataseries }, input, ref cachepjsOpeningRangeV2);
		}

	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		
		public Indicators.pjsOpeningRangeV2 pjsOpeningRangeV2(string productKey, double oRstarts, double oRends, double oRendsDuration, double extension1, double extension2, double extension3, bool showMidPoint, bool showHighLow, bool showRangeOpen, bool showRangeClose, bool showPrice, bool showRangeLabel, bool showRangeOpenCloseLabel, string label, bool hideUntilFormed, bool timesFromMktData, DashStyleHelper detailDashStyle, bool use24x7dataseries)
		{
			return indicator.pjsOpeningRangeV2(Input, productKey, oRstarts, oRends, oRendsDuration, extension1, extension2, extension3, showMidPoint, showHighLow, showRangeOpen, showRangeClose, showPrice, showRangeLabel, showRangeOpenCloseLabel, label, hideUntilFormed, timesFromMktData, detailDashStyle, use24x7dataseries);
		}


		
		public Indicators.pjsOpeningRangeV2 pjsOpeningRangeV2(ISeries<double> input , string productKey, double oRstarts, double oRends, double oRendsDuration, double extension1, double extension2, double extension3, bool showMidPoint, bool showHighLow, bool showRangeOpen, bool showRangeClose, bool showPrice, bool showRangeLabel, bool showRangeOpenCloseLabel, string label, bool hideUntilFormed, bool timesFromMktData, DashStyleHelper detailDashStyle, bool use24x7dataseries)
		{
			return indicator.pjsOpeningRangeV2(input, productKey, oRstarts, oRends, oRendsDuration, extension1, extension2, extension3, showMidPoint, showHighLow, showRangeOpen, showRangeClose, showPrice, showRangeLabel, showRangeOpenCloseLabel, label, hideUntilFormed, timesFromMktData, detailDashStyle, use24x7dataseries);
		}
	
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		
		public Indicators.pjsOpeningRangeV2 pjsOpeningRangeV2(string productKey, double oRstarts, double oRends, double oRendsDuration, double extension1, double extension2, double extension3, bool showMidPoint, bool showHighLow, bool showRangeOpen, bool showRangeClose, bool showPrice, bool showRangeLabel, bool showRangeOpenCloseLabel, string label, bool hideUntilFormed, bool timesFromMktData, DashStyleHelper detailDashStyle, bool use24x7dataseries)
		{
			return indicator.pjsOpeningRangeV2(Input, productKey, oRstarts, oRends, oRendsDuration, extension1, extension2, extension3, showMidPoint, showHighLow, showRangeOpen, showRangeClose, showPrice, showRangeLabel, showRangeOpenCloseLabel, label, hideUntilFormed, timesFromMktData, detailDashStyle, use24x7dataseries);
		}


		
		public Indicators.pjsOpeningRangeV2 pjsOpeningRangeV2(ISeries<double> input , string productKey, double oRstarts, double oRends, double oRendsDuration, double extension1, double extension2, double extension3, bool showMidPoint, bool showHighLow, bool showRangeOpen, bool showRangeClose, bool showPrice, bool showRangeLabel, bool showRangeOpenCloseLabel, string label, bool hideUntilFormed, bool timesFromMktData, DashStyleHelper detailDashStyle, bool use24x7dataseries)
		{
			return indicator.pjsOpeningRangeV2(input, productKey, oRstarts, oRends, oRendsDuration, extension1, extension2, extension3, showMidPoint, showHighLow, showRangeOpen, showRangeClose, showPrice, showRangeLabel, showRangeOpenCloseLabel, label, hideUntilFormed, timesFromMktData, detailDashStyle, use24x7dataseries);
		}

	}
}

#endregion
