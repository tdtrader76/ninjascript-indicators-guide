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
		
		private LizardTrader.LT_Auction_Bars[] cacheLT_Auction_Bars;

		
		public LizardTrader.LT_Auction_Bars LT_Auction_Bars(ltAuctionBarsDataInput dataInput, int periodMinuteBars, int minBars1, double extensionFactor, int maxRangeSizePercent, int swingStrength, double multiplierMD, double multiplierDTB, ltAuctionBarsReversalType reversalType, int reversalPeriod, double percentageClose, ltAuctionBarsConfirmationType confirmationType, int confirmationPeriod, double multiplierShadow, double multiplierSpike, int spikePeriod, bool applyAuctionRangeFilter, ltAuctionBarsFilterType filterType, bool applyTrendFilter, bool confirmationByCloseRequired, bool applyRangeFilter, double multiplierRange, bool applyVolumeFilter, double multiplierVolume, int filterPeriod)
		{
			return LT_Auction_Bars(Input, dataInput, periodMinuteBars, minBars1, extensionFactor, maxRangeSizePercent, swingStrength, multiplierMD, multiplierDTB, reversalType, reversalPeriod, percentageClose, confirmationType, confirmationPeriod, multiplierShadow, multiplierSpike, spikePeriod, applyAuctionRangeFilter, filterType, applyTrendFilter, confirmationByCloseRequired, applyRangeFilter, multiplierRange, applyVolumeFilter, multiplierVolume, filterPeriod);
		}


		
		public LizardTrader.LT_Auction_Bars LT_Auction_Bars(ISeries<double> input, ltAuctionBarsDataInput dataInput, int periodMinuteBars, int minBars1, double extensionFactor, int maxRangeSizePercent, int swingStrength, double multiplierMD, double multiplierDTB, ltAuctionBarsReversalType reversalType, int reversalPeriod, double percentageClose, ltAuctionBarsConfirmationType confirmationType, int confirmationPeriod, double multiplierShadow, double multiplierSpike, int spikePeriod, bool applyAuctionRangeFilter, ltAuctionBarsFilterType filterType, bool applyTrendFilter, bool confirmationByCloseRequired, bool applyRangeFilter, double multiplierRange, bool applyVolumeFilter, double multiplierVolume, int filterPeriod)
		{
			if (cacheLT_Auction_Bars != null)
				for (int idx = 0; idx < cacheLT_Auction_Bars.Length; idx++)
					if (cacheLT_Auction_Bars[idx].DataInput == dataInput && cacheLT_Auction_Bars[idx].PeriodMinuteBars == periodMinuteBars && cacheLT_Auction_Bars[idx].MinBars1 == minBars1 && cacheLT_Auction_Bars[idx].ExtensionFactor == extensionFactor && cacheLT_Auction_Bars[idx].MaxRangeSizePercent == maxRangeSizePercent && cacheLT_Auction_Bars[idx].SwingStrength == swingStrength && cacheLT_Auction_Bars[idx].MultiplierMD == multiplierMD && cacheLT_Auction_Bars[idx].MultiplierDTB == multiplierDTB && cacheLT_Auction_Bars[idx].ReversalType == reversalType && cacheLT_Auction_Bars[idx].ReversalPeriod == reversalPeriod && cacheLT_Auction_Bars[idx].PercentageClose == percentageClose && cacheLT_Auction_Bars[idx].ConfirmationType == confirmationType && cacheLT_Auction_Bars[idx].ConfirmationPeriod == confirmationPeriod && cacheLT_Auction_Bars[idx].MultiplierShadow == multiplierShadow && cacheLT_Auction_Bars[idx].MultiplierSpike == multiplierSpike && cacheLT_Auction_Bars[idx].SpikePeriod == spikePeriod && cacheLT_Auction_Bars[idx].ApplyAuctionRangeFilter == applyAuctionRangeFilter && cacheLT_Auction_Bars[idx].FilterType == filterType && cacheLT_Auction_Bars[idx].ApplyTrendFilter == applyTrendFilter && cacheLT_Auction_Bars[idx].ConfirmationByCloseRequired == confirmationByCloseRequired && cacheLT_Auction_Bars[idx].ApplyRangeFilter == applyRangeFilter && cacheLT_Auction_Bars[idx].MultiplierRange == multiplierRange && cacheLT_Auction_Bars[idx].ApplyVolumeFilter == applyVolumeFilter && cacheLT_Auction_Bars[idx].MultiplierVolume == multiplierVolume && cacheLT_Auction_Bars[idx].FilterPeriod == filterPeriod && cacheLT_Auction_Bars[idx].EqualsInput(input))
						return cacheLT_Auction_Bars[idx];
			return CacheIndicator<LizardTrader.LT_Auction_Bars>(new LizardTrader.LT_Auction_Bars(){ DataInput = dataInput, PeriodMinuteBars = periodMinuteBars, MinBars1 = minBars1, ExtensionFactor = extensionFactor, MaxRangeSizePercent = maxRangeSizePercent, SwingStrength = swingStrength, MultiplierMD = multiplierMD, MultiplierDTB = multiplierDTB, ReversalType = reversalType, ReversalPeriod = reversalPeriod, PercentageClose = percentageClose, ConfirmationType = confirmationType, ConfirmationPeriod = confirmationPeriod, MultiplierShadow = multiplierShadow, MultiplierSpike = multiplierSpike, SpikePeriod = spikePeriod, ApplyAuctionRangeFilter = applyAuctionRangeFilter, FilterType = filterType, ApplyTrendFilter = applyTrendFilter, ConfirmationByCloseRequired = confirmationByCloseRequired, ApplyRangeFilter = applyRangeFilter, MultiplierRange = multiplierRange, ApplyVolumeFilter = applyVolumeFilter, MultiplierVolume = multiplierVolume, FilterPeriod = filterPeriod }, input, ref cacheLT_Auction_Bars);
		}

	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		
		public Indicators.LizardTrader.LT_Auction_Bars LT_Auction_Bars(ltAuctionBarsDataInput dataInput, int periodMinuteBars, int minBars1, double extensionFactor, int maxRangeSizePercent, int swingStrength, double multiplierMD, double multiplierDTB, ltAuctionBarsReversalType reversalType, int reversalPeriod, double percentageClose, ltAuctionBarsConfirmationType confirmationType, int confirmationPeriod, double multiplierShadow, double multiplierSpike, int spikePeriod, bool applyAuctionRangeFilter, ltAuctionBarsFilterType filterType, bool applyTrendFilter, bool confirmationByCloseRequired, bool applyRangeFilter, double multiplierRange, bool applyVolumeFilter, double multiplierVolume, int filterPeriod)
		{
			return indicator.LT_Auction_Bars(Input, dataInput, periodMinuteBars, minBars1, extensionFactor, maxRangeSizePercent, swingStrength, multiplierMD, multiplierDTB, reversalType, reversalPeriod, percentageClose, confirmationType, confirmationPeriod, multiplierShadow, multiplierSpike, spikePeriod, applyAuctionRangeFilter, filterType, applyTrendFilter, confirmationByCloseRequired, applyRangeFilter, multiplierRange, applyVolumeFilter, multiplierVolume, filterPeriod);
		}


		
		public Indicators.LizardTrader.LT_Auction_Bars LT_Auction_Bars(ISeries<double> input , ltAuctionBarsDataInput dataInput, int periodMinuteBars, int minBars1, double extensionFactor, int maxRangeSizePercent, int swingStrength, double multiplierMD, double multiplierDTB, ltAuctionBarsReversalType reversalType, int reversalPeriod, double percentageClose, ltAuctionBarsConfirmationType confirmationType, int confirmationPeriod, double multiplierShadow, double multiplierSpike, int spikePeriod, bool applyAuctionRangeFilter, ltAuctionBarsFilterType filterType, bool applyTrendFilter, bool confirmationByCloseRequired, bool applyRangeFilter, double multiplierRange, bool applyVolumeFilter, double multiplierVolume, int filterPeriod)
		{
			return indicator.LT_Auction_Bars(input, dataInput, periodMinuteBars, minBars1, extensionFactor, maxRangeSizePercent, swingStrength, multiplierMD, multiplierDTB, reversalType, reversalPeriod, percentageClose, confirmationType, confirmationPeriod, multiplierShadow, multiplierSpike, spikePeriod, applyAuctionRangeFilter, filterType, applyTrendFilter, confirmationByCloseRequired, applyRangeFilter, multiplierRange, applyVolumeFilter, multiplierVolume, filterPeriod);
		}
	
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		
		public Indicators.LizardTrader.LT_Auction_Bars LT_Auction_Bars(ltAuctionBarsDataInput dataInput, int periodMinuteBars, int minBars1, double extensionFactor, int maxRangeSizePercent, int swingStrength, double multiplierMD, double multiplierDTB, ltAuctionBarsReversalType reversalType, int reversalPeriod, double percentageClose, ltAuctionBarsConfirmationType confirmationType, int confirmationPeriod, double multiplierShadow, double multiplierSpike, int spikePeriod, bool applyAuctionRangeFilter, ltAuctionBarsFilterType filterType, bool applyTrendFilter, bool confirmationByCloseRequired, bool applyRangeFilter, double multiplierRange, bool applyVolumeFilter, double multiplierVolume, int filterPeriod)
		{
			return indicator.LT_Auction_Bars(Input, dataInput, periodMinuteBars, minBars1, extensionFactor, maxRangeSizePercent, swingStrength, multiplierMD, multiplierDTB, reversalType, reversalPeriod, percentageClose, confirmationType, confirmationPeriod, multiplierShadow, multiplierSpike, spikePeriod, applyAuctionRangeFilter, filterType, applyTrendFilter, confirmationByCloseRequired, applyRangeFilter, multiplierRange, applyVolumeFilter, multiplierVolume, filterPeriod);
		}


		
		public Indicators.LizardTrader.LT_Auction_Bars LT_Auction_Bars(ISeries<double> input , ltAuctionBarsDataInput dataInput, int periodMinuteBars, int minBars1, double extensionFactor, int maxRangeSizePercent, int swingStrength, double multiplierMD, double multiplierDTB, ltAuctionBarsReversalType reversalType, int reversalPeriod, double percentageClose, ltAuctionBarsConfirmationType confirmationType, int confirmationPeriod, double multiplierShadow, double multiplierSpike, int spikePeriod, bool applyAuctionRangeFilter, ltAuctionBarsFilterType filterType, bool applyTrendFilter, bool confirmationByCloseRequired, bool applyRangeFilter, double multiplierRange, bool applyVolumeFilter, double multiplierVolume, int filterPeriod)
		{
			return indicator.LT_Auction_Bars(input, dataInput, periodMinuteBars, minBars1, extensionFactor, maxRangeSizePercent, swingStrength, multiplierMD, multiplierDTB, reversalType, reversalPeriod, percentageClose, confirmationType, confirmationPeriod, multiplierShadow, multiplierSpike, spikePeriod, applyAuctionRangeFilter, filterType, applyTrendFilter, confirmationByCloseRequired, applyRangeFilter, multiplierRange, applyVolumeFilter, multiplierVolume, filterPeriod);
		}

	}
}

#endregion
