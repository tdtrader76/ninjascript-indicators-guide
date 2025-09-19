
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
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class ChartTime : Indicator
	{
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Indicator displays in top right corner showing the Core Ninjatrader time, the Chart's Market time, and the seconds and milliseconds difference. ";
				Name										= "ChartTime";
				Calculate									= Calculate.OnEachTick;
				IsOverlay									= true;
				DisplayInDataBox							= false;
				DrawOnPricePanel							= false;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
				
				TimeDiffTolerance1   = .300;  //Milliseconds
                TimeDiffTolerance2   = 1;  //1 second = 1
                //BidOrAskDiffTolerance = 2; //one tick = 1                                                
            }
        }

             
        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {   ///Evaluates if there was an update to the chart
            
			
				
			 ///Evaluates if there was an update to the chart such as a bid, ask, last price or other activity
			if (marketDataUpdate.MarketDataType == MarketDataType.Ask || marketDataUpdate.MarketDataType == MarketDataType.Bid
                || marketDataUpdate.MarketDataType == MarketDataType.Last || marketDataUpdate.MarketDataType == MarketDataType.Unknown)
            {
                                                                                                                
         		DateTime dt1           = Core.Globals.Now;    //Core Ninjatrader Time
                DateTime dt2           = marketDataUpdate.Time;  //Chart Time
                TimeSpan TimeDiff       = dt2 - dt1 ; //The difference between the times. dt2 is chart time; dt1 is Core time
                double TimeDiffParsed = TimeDiff.TotalSeconds; // displays as a negative value if Chart Time lags Core Time
				double marketDataUpdateBid = 0;
				double marketDataUpdateAsk = 0;
				
				if (marketDataUpdate.MarketDataType == MarketDataType.Bid)
				marketDataUpdateBid = marketDataUpdate.Price;
				
				if (marketDataUpdate.MarketDataType == MarketDataType.Ask)
				marketDataUpdateAsk = marketDataUpdate.Price;
				
				double bidDiff = GetCurrentBid() - marketDataUpdateBid;
				
				double askDiff = GetCurrentAsk() - marketDataUpdateAsk;
				
			
				/// Coloring Text based on Time tolerance parameters set by user
               if(TimeDiffParsed < 0) // Chart time lags Core Time 
			   {
				if( (TimeDiffParsed >= TimeDiffTolerance1 && TimeDiffParsed < TimeDiffTolerance2) || (TimeDiffParsed <= (TimeDiffTolerance1*-1)  &&  TimeDiffParsed > (TimeDiffTolerance2*-1)) )
                                                Draw.TextFixed(this, "NinjaScriptInfo", dt1.ToString("hh:mm:ss:fff")+"   "+dt2.ToString("hh:mm:ss:fff")+"   "+ TimeDiffParsed.ToString("0,0.000"), TextPosition.TopRight, Brushes.Orange,ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 0);
                                                                                     // Core Time                          Chart Time                          Sec.Millsec Difference 
                else if( (TimeDiffParsed >= TimeDiffTolerance2) || (TimeDiffParsed <= (TimeDiffTolerance2*-1)) )
                                                Draw.TextFixed(this, "NinjaScriptInfo", dt1.ToString("hh:mm:ss:fff")+"   "+dt2.ToString("hh:mm:ss:fff")+"   "+ TimeDiffParsed.ToString("0,0.000"), TextPosition.TopRight, Brushes.Red,ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 0);
                                                                                     // Core Time                         Chart Time                           Sec.Millsec Difference 
               }
			   
			   /// Coloring Text if Chart Time exceeds Core time.    
			   if(TimeDiffParsed > 0) // Chart time greater than Core Time
			   {
				Draw.TextFixed(this, "NinjaScriptInfo", dt1.ToString("hh:mm:ss:fff")+"   "+dt2.ToString("hh:mm:ss:fff")+"   "+ TimeDiffParsed.ToString("0,0.000"), TextPosition.TopRight, Brushes.Yellow,ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 0);
               }									 // Core Time                          Chart Time                          Sec.Millsec Difference 
			 
				///LOG via Ninjascript Output Window			
			   /// Print some bar data to the Output window WHEN there is a lag, not necessarily every bar
			  
			    if (askDiff == null ) return;
				else if (marketDataUpdate.MarketDataType == MarketDataType.Ask && (askDiff != 0 || TimeDiffParsed >= TimeDiffTolerance1 || TimeDiffParsed >= TimeDiffTolerance2) 
					)
			        Print("BAR,  CoreTime,  ChartTime:    "+CurrentBar+"   "+dt1.ToString("hh:mm:ss:fff")+"   "+dt2.ToString("hh:mm:ss:fff")+"   "+ TimeDiffParsed.ToString("0,0.000")); /// comment out this line if you do not want to LOG the data
			  
				if (bidDiff == null ) return;
			   else if (marketDataUpdate.MarketDataType == MarketDataType.Bid && (bidDiff != 0 || TimeDiffParsed >= TimeDiffTolerance1 || TimeDiffParsed >= TimeDiffTolerance2) 
					)
			        Print("BAR,  CoreTime,  ChartTime:    "+CurrentBar+"   "+dt1.ToString("hh:mm:ss:fff")+"   "+dt2.ToString("hh:mm:ss:fff")+"   "+ TimeDiffParsed.ToString("0,0.000"));  // comment out this line if you do not want to LOG the data
				
            }
			
        }
		
		
		
	    #region Properties
	    [NinjaScriptProperty]
	    [Range(-100, double.MaxValue)]
	    [Display(Name="Time Low Tolerance", Description="Enter the first level time difference when you want to be alerted with Yellow text.", Order=1, GroupName="Parameters")]
	    public double TimeDiffTolerance1
	    { get; set; }

	    [NinjaScriptProperty]
	    [Range(-100, double.MaxValue)]
	    [Display(Name="Time High Tolerance", Description="Enter the second level time difference when you want to be alerted with red text.", Order=2, GroupName="Parameters")]
	    public double TimeDiffTolerance2
	    { get; set; }

	   // [NinjaScriptProperty]
	   // [Range(-100, double.MaxValue)]
	   // [Display(Name="Bid or Ask Tolerance", Description="Enter the tick difference when you want to be alerted.", Order=3, GroupName="Parameters")]
	   // public double BidOrAskDiffTolerance
	   // { get; set; }
		
	    #endregion

	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ChartTime[] cacheChartTime;
		public ChartTime ChartTime(double timeDiffTolerance1, double timeDiffTolerance2)
		{
			return ChartTime(Input, timeDiffTolerance1, timeDiffTolerance2);
		}

		public ChartTime ChartTime(ISeries<double> input, double timeDiffTolerance1, double timeDiffTolerance2)
		{
			if (cacheChartTime != null)
				for (int idx = 0; idx < cacheChartTime.Length; idx++)
					if (cacheChartTime[idx] != null && cacheChartTime[idx].TimeDiffTolerance1 == timeDiffTolerance1 && cacheChartTime[idx].TimeDiffTolerance2 == timeDiffTolerance2 && cacheChartTime[idx].EqualsInput(input))
						return cacheChartTime[idx];
			return CacheIndicator<ChartTime>(new ChartTime(){ TimeDiffTolerance1 = timeDiffTolerance1, TimeDiffTolerance2 = timeDiffTolerance2 }, input, ref cacheChartTime);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ChartTime ChartTime(double timeDiffTolerance1, double timeDiffTolerance2)
		{
			return indicator.ChartTime(Input, timeDiffTolerance1, timeDiffTolerance2);
		}

		public Indicators.ChartTime ChartTime(ISeries<double> input , double timeDiffTolerance1, double timeDiffTolerance2)
		{
			return indicator.ChartTime(input, timeDiffTolerance1, timeDiffTolerance2);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ChartTime ChartTime(double timeDiffTolerance1, double timeDiffTolerance2)
		{
			return indicator.ChartTime(Input, timeDiffTolerance1, timeDiffTolerance2);
		}

		public Indicators.ChartTime ChartTime(ISeries<double> input , double timeDiffTolerance1, double timeDiffTolerance2)
		{
			return indicator.ChartTime(input, timeDiffTolerance1, timeDiffTolerance2);
		}
	}
}

#endregion
