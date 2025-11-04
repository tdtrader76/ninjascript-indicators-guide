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

	/// <summary>
	/// Coded by bcomas January 2021. Email:  bcomasSoftware@gmail.com
    /// http://ninjatrader-programming-strategies.mozello.com/
    /// Displays the major transactions that have taken place during
    /// short period of time and at the same price (Limit buyer / seller)
    /// </summary>

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class LargeTradesNT8 : Indicator
	{
		private int entryBar;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Large Trades";
				Name										= "LargeTradesNT8";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= false;
				DrawVerticalGridLines						= false;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
				LargeVolume									= 500;
				TimeFix										= 1;
				TradeColor									= Brushes.Yellow;
				TextColor									= Brushes.White;
				TradeShadowColor							= Brushes.Red;
				ColorOpacity								= 1;
				TradeAlert									= true;
				TradeDot									= true;
				TradeBarWidth								= 1;
				TradeTime									= false;
				AlertSound									= @"alert4.wav";
				
				AddPlot(new Stroke(Brushes.Yellow, 3), PlotStyle.Bar, "LargeVolumePlot");
				
				Displacement								=1;
			}
			else if (State == State.Configure)
			{	
				AddDataSeries(BarsPeriodType.Second,1);
			}
		}
		
	   /// <summary>
       /// Called on each bar update event (incoming tick)
       /// </summary>

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 2) 
			{
				return;
			}
			
			if (BarsInProgress == 1)				
			{			
				TimeSpan ts1 = Time[0] - new DateTime(1970,1,1,0,0,0);
			    TimeSpan ts2 = Time[1] - new DateTime(1970,1,1,0,0,0);

				int timeGap=(int)ts1.TotalSeconds-(int)ts2.TotalSeconds;
				
				if (Volume[0]>=LargeVolume && Math.Abs(timeGap)<=TimeFix)
			 	{				
					if (CurrentBars[0] != entryBar)  
				 	{ 
						LargeVolumePlot[0] = 1;
						
					 	//Print (Time[0]+ "  LVP: "+LargeVolumePlot[0]);  
						entryBar = CurrentBars[0];   
					}
	
				 	if(TradeDot)

					Draw.Diamond(this,"Diamond"+CurrentBar, true, -1, (High[0] +Low[0]) /2, TradeColor);
					
					if(TradeTime)	
					{
						Draw.Text(this,"TFx"+CurrentBar, true, Convert.ToString(Time[0]),-1, Median[0], 0,TextColor, 
						new SimpleFont("Small Fonts", 12), TextAlignment.Right,	Brushes.Transparent,Brushes.Transparent, 0);
					}
					else
					{
						// Calculate position 25 pixels below the bar
						double textPrice = Low[0] - (25 * TickSize);
						string tradeInfo = Volume[0].ToString() + " @ " + Close[0].ToString("F2");
						
						Draw.Text(this,"TFx"+CurrentBar, true, tradeInfo, -1, textPrice, 0, TextColor, 
						new SimpleFont("Small Fonts", 12), TextAlignment.Right, Brushes.Transparent, Brushes.Transparent, 0);
					}
					if(TradeAlert)
					{
						PlaySound(NinjaTrader.Core.Globals.InstallDir + @"\sounds\alert4.wav");
						
					}	
				}
        	}	
			
			if (BarsInProgress == 0)
			{
				if (LargeVolumePlot[0] != 1)   
					LargeVolumePlot[0] = 0;   
			}
			
		}		

		#region Properties
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="LargeVolume", Description="Minimum Large Volume", Order=1, GroupName="Parameters")]
		public int LargeVolume
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="TimeFix", Description="Time in Seconds", Order=2, GroupName="Parameters")]
		public int TimeFix
		{ get; set; }

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="TradeColor", Description="Color for Large Trades", Order=3, GroupName="Parameters")]
		public Brush TradeColor
		{ get; set; }

		[Browsable(false)]
		public string TradeColorSerializable
		{
			get { return Serialize.BrushToString(TradeColor); }
			set { TradeColor = Serialize.StringToBrush(value); }
		}			

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="TextColor", Description="Color for Text", Order=4, GroupName="Parameters")]
		public Brush TextColor
		{ get; set; }

		[Browsable(false)]
		public string TextColorSerializable
		{
			get { return Serialize.BrushToString(TextColor); }
			set { TextColor = Serialize.StringToBrush(value); }
		}			

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="TradeShadowColor", Description="Shadow Color", Order=5, GroupName="Parameters")]
		public Brush TradeShadowColor
		{ get; set; }

		[Browsable(false)]
		public string TradeShadowColorSerializable
		{
			get { return Serialize.BrushToString(TradeShadowColor); }
			set { TradeShadowColor = Serialize.StringToBrush(value); }
		}			

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="ColorOpacity", Description="Opacity", Order=6, GroupName="Parameters")]
		public int ColorOpacity
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="TradeAlert", Description="Trade Alerts", Order=7, GroupName="Parameters")]
		public bool TradeAlert
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="TradeDot", Description="Large Trade Image", Order=8, GroupName="Parameters")]
		public bool TradeDot
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name="TradeBarWidth", Description="Bar Width", Order=9, GroupName="Parameters")]
		public double TradeBarWidth
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="TradeTime", Description="Show Time for Large Trades", Order=10, GroupName="Parameters")]
		public bool TradeTime
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="AlertSound", Description="Sound File for Trade Alerts", Order=11, GroupName="Parameters")]
		public string AlertSound
		{ get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> LargeVolumePlot
		{
			get { return Values[0]; }
		}
		#endregion

	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private LargeTradesNT8[] cacheLargeTradesNT8;
		public LargeTradesNT8 LargeTradesNT8(int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			return LargeTradesNT8(Input, largeVolume, timeFix, tradeColor, textColor, tradeShadowColor, colorOpacity, tradeAlert, tradeDot, tradeBarWidth, tradeTime, alertSound);
		}

		public LargeTradesNT8 LargeTradesNT8(ISeries<double> input, int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			if (cacheLargeTradesNT8 != null)
				for (int idx = 0; idx < cacheLargeTradesNT8.Length; idx++)
					if (cacheLargeTradesNT8[idx] != null && cacheLargeTradesNT8[idx].LargeVolume == largeVolume && cacheLargeTradesNT8[idx].TimeFix == timeFix && cacheLargeTradesNT8[idx].TradeColor == tradeColor && cacheLargeTradesNT8[idx].TextColor == textColor && cacheLargeTradesNT8[idx].TradeShadowColor == tradeShadowColor && cacheLargeTradesNT8[idx].ColorOpacity == colorOpacity && cacheLargeTradesNT8[idx].TradeAlert == tradeAlert && cacheLargeTradesNT8[idx].TradeDot == tradeDot && cacheLargeTradesNT8[idx].TradeBarWidth == tradeBarWidth && cacheLargeTradesNT8[idx].TradeTime == tradeTime && cacheLargeTradesNT8[idx].AlertSound == alertSound && cacheLargeTradesNT8[idx].EqualsInput(input))
						return cacheLargeTradesNT8[idx];
			return CacheIndicator<LargeTradesNT8>(new LargeTradesNT8(){ LargeVolume = largeVolume, TimeFix = timeFix, TradeColor = tradeColor, TextColor = textColor, TradeShadowColor = tradeShadowColor, ColorOpacity = colorOpacity, TradeAlert = tradeAlert, TradeDot = tradeDot, TradeBarWidth = tradeBarWidth, TradeTime = tradeTime, AlertSound = alertSound }, input, ref cacheLargeTradesNT8);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.LargeTradesNT8 LargeTradesNT8(int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			return indicator.LargeTradesNT8(Input, largeVolume, timeFix, tradeColor, textColor, tradeShadowColor, colorOpacity, tradeAlert, tradeDot, tradeBarWidth, tradeTime, alertSound);
		}

		public Indicators.LargeTradesNT8 LargeTradesNT8(ISeries<double> input , int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			return indicator.LargeTradesNT8(input, largeVolume, timeFix, tradeColor, textColor, tradeShadowColor, colorOpacity, tradeAlert, tradeDot, tradeBarWidth, tradeTime, alertSound);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.LargeTradesNT8 LargeTradesNT8(int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			return indicator.LargeTradesNT8(Input, largeVolume, timeFix, tradeColor, textColor, tradeShadowColor, colorOpacity, tradeAlert, tradeDot, tradeBarWidth, tradeTime, alertSound);
		}

		public Indicators.LargeTradesNT8 LargeTradesNT8(ISeries<double> input , int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			return indicator.LargeTradesNT8(input, largeVolume, timeFix, tradeColor, textColor, tradeShadowColor, colorOpacity, tradeAlert, tradeDot, tradeBarWidth, tradeTime, alertSound);
		}
	}
}

#endregion
