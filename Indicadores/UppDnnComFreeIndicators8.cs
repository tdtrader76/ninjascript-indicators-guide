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
		
		private UppDnnFree.udFreeATR[] cacheudFreeATR;
		private UppDnnFree.udFreeColorTimeRegions[] cacheudFreeColorTimeRegions;
		private UppDnnFree.udFreeCurrentDayOHL[] cacheudFreeCurrentDayOHL;
		private UppDnnFree.udFreeCurrentPriceLine[] cacheudFreeCurrentPriceLine;
		private UppDnnFree.udFreeCursorOHLC[] cacheudFreeCursorOHLC;
		private UppDnnFree.udFreeDayOfWeekLabel[] cacheudFreeDayOfWeekLabel;
		private UppDnnFree.udFreeDrawingToolbarShortcut[] cacheudFreeDrawingToolbarShortcut;
		private UppDnnFree.udFreeFractalHighLow[] cacheudFreeFractalHighLow;
		private UppDnnFree.udFreeOpeningRange[] cacheudFreeOpeningRange;
		private UppDnnFree.udFreePriorDayOHLCLabel[] cacheudFreePriorDayOHLCLabel;
		private UppDnnFree.udFreePriorWeekOHLCLabel[] cacheudFreePriorWeekOHLCLabel;
		private UppDnnFree.udFreeVolume[] cacheudFreeVolume;

		
		public UppDnnFree.udFreeATR udFreeATR(int period)
		{
			return udFreeATR(Input, period);
		}

		public UppDnnFree.udFreeColorTimeRegions udFreeColorTimeRegions(int backgroundOpacity1, int backgroundOpacity2, int backgroundOpacity3, int backgroundOpacity4, int backgroundOpacity5)
		{
			return udFreeColorTimeRegions(Input, backgroundOpacity1, backgroundOpacity2, backgroundOpacity3, backgroundOpacity4, backgroundOpacity5);
		}

		public UppDnnFree.udFreeCurrentDayOHL udFreeCurrentDayOHL(int labelOffset, double hLOffsetPct)
		{
			return udFreeCurrentDayOHL(Input, labelOffset, hLOffsetPct);
		}

		public UppDnnFree.udFreeCurrentPriceLine udFreeCurrentPriceLine()
		{
			return udFreeCurrentPriceLine(Input);
		}

		public UppDnnFree.udFreeCursorOHLC udFreeCursorOHLC()
		{
			return udFreeCursorOHLC(Input);
		}

		public UppDnnFree.udFreeDayOfWeekLabel udFreeDayOfWeekLabel()
		{
			return udFreeDayOfWeekLabel(Input);
		}

		public UppDnnFree.udFreeDrawingToolbarShortcut udFreeDrawingToolbarShortcut(int toolbarOffset)
		{
			return udFreeDrawingToolbarShortcut(Input, toolbarOffset);
		}

		public UppDnnFree.udFreeFractalHighLow udFreeFractalHighLow(int ticksOffset)
		{
			return udFreeFractalHighLow(Input, ticksOffset);
		}

		public UppDnnFree.udFreeOpeningRange udFreeOpeningRange()
		{
			return udFreeOpeningRange(Input);
		}

		public UppDnnFree.udFreePriorDayOHLCLabel udFreePriorDayOHLCLabel(int labelOffset)
		{
			return udFreePriorDayOHLCLabel(Input, labelOffset);
		}

		public UppDnnFree.udFreePriorWeekOHLCLabel udFreePriorWeekOHLCLabel(int labelOffset)
		{
			return udFreePriorWeekOHLCLabel(Input, labelOffset);
		}

		public UppDnnFree.udFreeVolume udFreeVolume()
		{
			return udFreeVolume(Input);
		}


		
		public UppDnnFree.udFreeATR udFreeATR(ISeries<double> input, int period)
		{
			if (cacheudFreeATR != null)
				for (int idx = 0; idx < cacheudFreeATR.Length; idx++)
					if (cacheudFreeATR[idx].Period == period && cacheudFreeATR[idx].EqualsInput(input))
						return cacheudFreeATR[idx];
			return CacheIndicator<UppDnnFree.udFreeATR>(new UppDnnFree.udFreeATR(){ Period = period }, input, ref cacheudFreeATR);
		}

		public UppDnnFree.udFreeColorTimeRegions udFreeColorTimeRegions(ISeries<double> input, int backgroundOpacity1, int backgroundOpacity2, int backgroundOpacity3, int backgroundOpacity4, int backgroundOpacity5)
		{
			if (cacheudFreeColorTimeRegions != null)
				for (int idx = 0; idx < cacheudFreeColorTimeRegions.Length; idx++)
					if (cacheudFreeColorTimeRegions[idx].BackgroundOpacity1 == backgroundOpacity1 && cacheudFreeColorTimeRegions[idx].BackgroundOpacity2 == backgroundOpacity2 && cacheudFreeColorTimeRegions[idx].BackgroundOpacity3 == backgroundOpacity3 && cacheudFreeColorTimeRegions[idx].BackgroundOpacity4 == backgroundOpacity4 && cacheudFreeColorTimeRegions[idx].BackgroundOpacity5 == backgroundOpacity5 && cacheudFreeColorTimeRegions[idx].EqualsInput(input))
						return cacheudFreeColorTimeRegions[idx];
			return CacheIndicator<UppDnnFree.udFreeColorTimeRegions>(new UppDnnFree.udFreeColorTimeRegions(){ BackgroundOpacity1 = backgroundOpacity1, BackgroundOpacity2 = backgroundOpacity2, BackgroundOpacity3 = backgroundOpacity3, BackgroundOpacity4 = backgroundOpacity4, BackgroundOpacity5 = backgroundOpacity5 }, input, ref cacheudFreeColorTimeRegions);
		}

		public UppDnnFree.udFreeCurrentDayOHL udFreeCurrentDayOHL(ISeries<double> input, int labelOffset, double hLOffsetPct)
		{
			if (cacheudFreeCurrentDayOHL != null)
				for (int idx = 0; idx < cacheudFreeCurrentDayOHL.Length; idx++)
					if (cacheudFreeCurrentDayOHL[idx].LabelOffset == labelOffset && cacheudFreeCurrentDayOHL[idx].HLOffsetPct == hLOffsetPct && cacheudFreeCurrentDayOHL[idx].EqualsInput(input))
						return cacheudFreeCurrentDayOHL[idx];
			return CacheIndicator<UppDnnFree.udFreeCurrentDayOHL>(new UppDnnFree.udFreeCurrentDayOHL(){ LabelOffset = labelOffset, HLOffsetPct = hLOffsetPct }, input, ref cacheudFreeCurrentDayOHL);
		}

		public UppDnnFree.udFreeCurrentPriceLine udFreeCurrentPriceLine(ISeries<double> input)
		{
			if (cacheudFreeCurrentPriceLine != null)
				for (int idx = 0; idx < cacheudFreeCurrentPriceLine.Length; idx++)
					if ( cacheudFreeCurrentPriceLine[idx].EqualsInput(input))
						return cacheudFreeCurrentPriceLine[idx];
			return CacheIndicator<UppDnnFree.udFreeCurrentPriceLine>(new UppDnnFree.udFreeCurrentPriceLine(), input, ref cacheudFreeCurrentPriceLine);
		}

		public UppDnnFree.udFreeCursorOHLC udFreeCursorOHLC(ISeries<double> input)
		{
			if (cacheudFreeCursorOHLC != null)
				for (int idx = 0; idx < cacheudFreeCursorOHLC.Length; idx++)
					if ( cacheudFreeCursorOHLC[idx].EqualsInput(input))
						return cacheudFreeCursorOHLC[idx];
			return CacheIndicator<UppDnnFree.udFreeCursorOHLC>(new UppDnnFree.udFreeCursorOHLC(), input, ref cacheudFreeCursorOHLC);
		}

		public UppDnnFree.udFreeDayOfWeekLabel udFreeDayOfWeekLabel(ISeries<double> input)
		{
			if (cacheudFreeDayOfWeekLabel != null)
				for (int idx = 0; idx < cacheudFreeDayOfWeekLabel.Length; idx++)
					if ( cacheudFreeDayOfWeekLabel[idx].EqualsInput(input))
						return cacheudFreeDayOfWeekLabel[idx];
			return CacheIndicator<UppDnnFree.udFreeDayOfWeekLabel>(new UppDnnFree.udFreeDayOfWeekLabel(), input, ref cacheudFreeDayOfWeekLabel);
		}

		public UppDnnFree.udFreeDrawingToolbarShortcut udFreeDrawingToolbarShortcut(ISeries<double> input, int toolbarOffset)
		{
			if (cacheudFreeDrawingToolbarShortcut != null)
				for (int idx = 0; idx < cacheudFreeDrawingToolbarShortcut.Length; idx++)
					if (cacheudFreeDrawingToolbarShortcut[idx].ToolbarOffset == toolbarOffset && cacheudFreeDrawingToolbarShortcut[idx].EqualsInput(input))
						return cacheudFreeDrawingToolbarShortcut[idx];
			return CacheIndicator<UppDnnFree.udFreeDrawingToolbarShortcut>(new UppDnnFree.udFreeDrawingToolbarShortcut(){ ToolbarOffset = toolbarOffset }, input, ref cacheudFreeDrawingToolbarShortcut);
		}

		public UppDnnFree.udFreeFractalHighLow udFreeFractalHighLow(ISeries<double> input, int ticksOffset)
		{
			if (cacheudFreeFractalHighLow != null)
				for (int idx = 0; idx < cacheudFreeFractalHighLow.Length; idx++)
					if (cacheudFreeFractalHighLow[idx].TicksOffset == ticksOffset && cacheudFreeFractalHighLow[idx].EqualsInput(input))
						return cacheudFreeFractalHighLow[idx];
			return CacheIndicator<UppDnnFree.udFreeFractalHighLow>(new UppDnnFree.udFreeFractalHighLow(){ TicksOffset = ticksOffset }, input, ref cacheudFreeFractalHighLow);
		}

		public UppDnnFree.udFreeOpeningRange udFreeOpeningRange(ISeries<double> input)
		{
			if (cacheudFreeOpeningRange != null)
				for (int idx = 0; idx < cacheudFreeOpeningRange.Length; idx++)
					if ( cacheudFreeOpeningRange[idx].EqualsInput(input))
						return cacheudFreeOpeningRange[idx];
			return CacheIndicator<UppDnnFree.udFreeOpeningRange>(new UppDnnFree.udFreeOpeningRange(), input, ref cacheudFreeOpeningRange);
		}

		public UppDnnFree.udFreePriorDayOHLCLabel udFreePriorDayOHLCLabel(ISeries<double> input, int labelOffset)
		{
			if (cacheudFreePriorDayOHLCLabel != null)
				for (int idx = 0; idx < cacheudFreePriorDayOHLCLabel.Length; idx++)
					if (cacheudFreePriorDayOHLCLabel[idx].LabelOffset == labelOffset && cacheudFreePriorDayOHLCLabel[idx].EqualsInput(input))
						return cacheudFreePriorDayOHLCLabel[idx];
			return CacheIndicator<UppDnnFree.udFreePriorDayOHLCLabel>(new UppDnnFree.udFreePriorDayOHLCLabel(){ LabelOffset = labelOffset }, input, ref cacheudFreePriorDayOHLCLabel);
		}

		public UppDnnFree.udFreePriorWeekOHLCLabel udFreePriorWeekOHLCLabel(ISeries<double> input, int labelOffset)
		{
			if (cacheudFreePriorWeekOHLCLabel != null)
				for (int idx = 0; idx < cacheudFreePriorWeekOHLCLabel.Length; idx++)
					if (cacheudFreePriorWeekOHLCLabel[idx].LabelOffset == labelOffset && cacheudFreePriorWeekOHLCLabel[idx].EqualsInput(input))
						return cacheudFreePriorWeekOHLCLabel[idx];
			return CacheIndicator<UppDnnFree.udFreePriorWeekOHLCLabel>(new UppDnnFree.udFreePriorWeekOHLCLabel(){ LabelOffset = labelOffset }, input, ref cacheudFreePriorWeekOHLCLabel);
		}

		public UppDnnFree.udFreeVolume udFreeVolume(ISeries<double> input)
		{
			if (cacheudFreeVolume != null)
				for (int idx = 0; idx < cacheudFreeVolume.Length; idx++)
					if ( cacheudFreeVolume[idx].EqualsInput(input))
						return cacheudFreeVolume[idx];
			return CacheIndicator<UppDnnFree.udFreeVolume>(new UppDnnFree.udFreeVolume(), input, ref cacheudFreeVolume);
		}

	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		
		public Indicators.UppDnnFree.udFreeATR udFreeATR(int period)
		{
			return indicator.udFreeATR(Input, period);
		}

		public Indicators.UppDnnFree.udFreeColorTimeRegions udFreeColorTimeRegions(int backgroundOpacity1, int backgroundOpacity2, int backgroundOpacity3, int backgroundOpacity4, int backgroundOpacity5)
		{
			return indicator.udFreeColorTimeRegions(Input, backgroundOpacity1, backgroundOpacity2, backgroundOpacity3, backgroundOpacity4, backgroundOpacity5);
		}

		public Indicators.UppDnnFree.udFreeCurrentDayOHL udFreeCurrentDayOHL(int labelOffset, double hLOffsetPct)
		{
			return indicator.udFreeCurrentDayOHL(Input, labelOffset, hLOffsetPct);
		}

		public Indicators.UppDnnFree.udFreeCurrentPriceLine udFreeCurrentPriceLine()
		{
			return indicator.udFreeCurrentPriceLine(Input);
		}

		public Indicators.UppDnnFree.udFreeCursorOHLC udFreeCursorOHLC()
		{
			return indicator.udFreeCursorOHLC(Input);
		}

		public Indicators.UppDnnFree.udFreeDayOfWeekLabel udFreeDayOfWeekLabel()
		{
			return indicator.udFreeDayOfWeekLabel(Input);
		}

		public Indicators.UppDnnFree.udFreeDrawingToolbarShortcut udFreeDrawingToolbarShortcut(int toolbarOffset)
		{
			return indicator.udFreeDrawingToolbarShortcut(Input, toolbarOffset);
		}

		public Indicators.UppDnnFree.udFreeFractalHighLow udFreeFractalHighLow(int ticksOffset)
		{
			return indicator.udFreeFractalHighLow(Input, ticksOffset);
		}

		public Indicators.UppDnnFree.udFreeOpeningRange udFreeOpeningRange()
		{
			return indicator.udFreeOpeningRange(Input);
		}

		public Indicators.UppDnnFree.udFreePriorDayOHLCLabel udFreePriorDayOHLCLabel(int labelOffset)
		{
			return indicator.udFreePriorDayOHLCLabel(Input, labelOffset);
		}

		public Indicators.UppDnnFree.udFreePriorWeekOHLCLabel udFreePriorWeekOHLCLabel(int labelOffset)
		{
			return indicator.udFreePriorWeekOHLCLabel(Input, labelOffset);
		}

		public Indicators.UppDnnFree.udFreeVolume udFreeVolume()
		{
			return indicator.udFreeVolume(Input);
		}


		
		public Indicators.UppDnnFree.udFreeATR udFreeATR(ISeries<double> input , int period)
		{
			return indicator.udFreeATR(input, period);
		}

		public Indicators.UppDnnFree.udFreeColorTimeRegions udFreeColorTimeRegions(ISeries<double> input , int backgroundOpacity1, int backgroundOpacity2, int backgroundOpacity3, int backgroundOpacity4, int backgroundOpacity5)
		{
			return indicator.udFreeColorTimeRegions(input, backgroundOpacity1, backgroundOpacity2, backgroundOpacity3, backgroundOpacity4, backgroundOpacity5);
		}

		public Indicators.UppDnnFree.udFreeCurrentDayOHL udFreeCurrentDayOHL(ISeries<double> input , int labelOffset, double hLOffsetPct)
		{
			return indicator.udFreeCurrentDayOHL(input, labelOffset, hLOffsetPct);
		}

		public Indicators.UppDnnFree.udFreeCurrentPriceLine udFreeCurrentPriceLine(ISeries<double> input )
		{
			return indicator.udFreeCurrentPriceLine(input);
		}

		public Indicators.UppDnnFree.udFreeCursorOHLC udFreeCursorOHLC(ISeries<double> input )
		{
			return indicator.udFreeCursorOHLC(input);
		}

		public Indicators.UppDnnFree.udFreeDayOfWeekLabel udFreeDayOfWeekLabel(ISeries<double> input )
		{
			return indicator.udFreeDayOfWeekLabel(input);
		}

		public Indicators.UppDnnFree.udFreeDrawingToolbarShortcut udFreeDrawingToolbarShortcut(ISeries<double> input , int toolbarOffset)
		{
			return indicator.udFreeDrawingToolbarShortcut(input, toolbarOffset);
		}

		public Indicators.UppDnnFree.udFreeFractalHighLow udFreeFractalHighLow(ISeries<double> input , int ticksOffset)
		{
			return indicator.udFreeFractalHighLow(input, ticksOffset);
		}

		public Indicators.UppDnnFree.udFreeOpeningRange udFreeOpeningRange(ISeries<double> input )
		{
			return indicator.udFreeOpeningRange(input);
		}

		public Indicators.UppDnnFree.udFreePriorDayOHLCLabel udFreePriorDayOHLCLabel(ISeries<double> input , int labelOffset)
		{
			return indicator.udFreePriorDayOHLCLabel(input, labelOffset);
		}

		public Indicators.UppDnnFree.udFreePriorWeekOHLCLabel udFreePriorWeekOHLCLabel(ISeries<double> input , int labelOffset)
		{
			return indicator.udFreePriorWeekOHLCLabel(input, labelOffset);
		}

		public Indicators.UppDnnFree.udFreeVolume udFreeVolume(ISeries<double> input )
		{
			return indicator.udFreeVolume(input);
		}
	
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		
		public Indicators.UppDnnFree.udFreeATR udFreeATR(int period)
		{
			return indicator.udFreeATR(Input, period);
		}

		public Indicators.UppDnnFree.udFreeColorTimeRegions udFreeColorTimeRegions(int backgroundOpacity1, int backgroundOpacity2, int backgroundOpacity3, int backgroundOpacity4, int backgroundOpacity5)
		{
			return indicator.udFreeColorTimeRegions(Input, backgroundOpacity1, backgroundOpacity2, backgroundOpacity3, backgroundOpacity4, backgroundOpacity5);
		}

		public Indicators.UppDnnFree.udFreeCurrentDayOHL udFreeCurrentDayOHL(int labelOffset, double hLOffsetPct)
		{
			return indicator.udFreeCurrentDayOHL(Input, labelOffset, hLOffsetPct);
		}

		public Indicators.UppDnnFree.udFreeCurrentPriceLine udFreeCurrentPriceLine()
		{
			return indicator.udFreeCurrentPriceLine(Input);
		}

		public Indicators.UppDnnFree.udFreeCursorOHLC udFreeCursorOHLC()
		{
			return indicator.udFreeCursorOHLC(Input);
		}

		public Indicators.UppDnnFree.udFreeDayOfWeekLabel udFreeDayOfWeekLabel()
		{
			return indicator.udFreeDayOfWeekLabel(Input);
		}

		public Indicators.UppDnnFree.udFreeDrawingToolbarShortcut udFreeDrawingToolbarShortcut(int toolbarOffset)
		{
			return indicator.udFreeDrawingToolbarShortcut(Input, toolbarOffset);
		}

		public Indicators.UppDnnFree.udFreeFractalHighLow udFreeFractalHighLow(int ticksOffset)
		{
			return indicator.udFreeFractalHighLow(Input, ticksOffset);
		}

		public Indicators.UppDnnFree.udFreeOpeningRange udFreeOpeningRange()
		{
			return indicator.udFreeOpeningRange(Input);
		}

		public Indicators.UppDnnFree.udFreePriorDayOHLCLabel udFreePriorDayOHLCLabel(int labelOffset)
		{
			return indicator.udFreePriorDayOHLCLabel(Input, labelOffset);
		}

		public Indicators.UppDnnFree.udFreePriorWeekOHLCLabel udFreePriorWeekOHLCLabel(int labelOffset)
		{
			return indicator.udFreePriorWeekOHLCLabel(Input, labelOffset);
		}

		public Indicators.UppDnnFree.udFreeVolume udFreeVolume()
		{
			return indicator.udFreeVolume(Input);
		}


		
		public Indicators.UppDnnFree.udFreeATR udFreeATR(ISeries<double> input , int period)
		{
			return indicator.udFreeATR(input, period);
		}

		public Indicators.UppDnnFree.udFreeColorTimeRegions udFreeColorTimeRegions(ISeries<double> input , int backgroundOpacity1, int backgroundOpacity2, int backgroundOpacity3, int backgroundOpacity4, int backgroundOpacity5)
		{
			return indicator.udFreeColorTimeRegions(input, backgroundOpacity1, backgroundOpacity2, backgroundOpacity3, backgroundOpacity4, backgroundOpacity5);
		}

		public Indicators.UppDnnFree.udFreeCurrentDayOHL udFreeCurrentDayOHL(ISeries<double> input , int labelOffset, double hLOffsetPct)
		{
			return indicator.udFreeCurrentDayOHL(input, labelOffset, hLOffsetPct);
		}

		public Indicators.UppDnnFree.udFreeCurrentPriceLine udFreeCurrentPriceLine(ISeries<double> input )
		{
			return indicator.udFreeCurrentPriceLine(input);
		}

		public Indicators.UppDnnFree.udFreeCursorOHLC udFreeCursorOHLC(ISeries<double> input )
		{
			return indicator.udFreeCursorOHLC(input);
		}

		public Indicators.UppDnnFree.udFreeDayOfWeekLabel udFreeDayOfWeekLabel(ISeries<double> input )
		{
			return indicator.udFreeDayOfWeekLabel(input);
		}

		public Indicators.UppDnnFree.udFreeDrawingToolbarShortcut udFreeDrawingToolbarShortcut(ISeries<double> input , int toolbarOffset)
		{
			return indicator.udFreeDrawingToolbarShortcut(input, toolbarOffset);
		}

		public Indicators.UppDnnFree.udFreeFractalHighLow udFreeFractalHighLow(ISeries<double> input , int ticksOffset)
		{
			return indicator.udFreeFractalHighLow(input, ticksOffset);
		}

		public Indicators.UppDnnFree.udFreeOpeningRange udFreeOpeningRange(ISeries<double> input )
		{
			return indicator.udFreeOpeningRange(input);
		}

		public Indicators.UppDnnFree.udFreePriorDayOHLCLabel udFreePriorDayOHLCLabel(ISeries<double> input , int labelOffset)
		{
			return indicator.udFreePriorDayOHLCLabel(input, labelOffset);
		}

		public Indicators.UppDnnFree.udFreePriorWeekOHLCLabel udFreePriorWeekOHLCLabel(ISeries<double> input , int labelOffset)
		{
			return indicator.udFreePriorWeekOHLCLabel(input, labelOffset);
		}

		public Indicators.UppDnnFree.udFreeVolume udFreeVolume(ISeries<double> input )
		{
			return indicator.udFreeVolume(input);
		}

	}
}

#endregion
