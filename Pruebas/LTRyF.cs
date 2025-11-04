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
	public class LTRyF : Indicator
	{
		private int entryBar;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Large Trades";
				Name										= "LTRyF";
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

					// Determine if the bar is bullish or bearish
					bool isBullish = Close[0] >= Open[0];

					// Calculate the base position for the dot
					double dotYPosition;
					if (isBullish)
					{
						dotYPosition = High[0] + (25 * TickSize);
					}
					else
					{
						dotYPosition = Low[0] - (25 * TickSize);
					}

					// Draw the dot if enabled
					if (TradeDot)
					{
						Draw.Dot(this, "LargeTradeDot" + CurrentBar, true, 0, dotYPosition, TradeColor);
					}

					// Draw the appropriate text (time or price)
					if (TradeTime)
					{
						// The user's request was to adjust the price label, not the time label.
						// We keep the original positioning for the time label to avoid unintended changes.
						Draw.Text(this, "TFx" + CurrentBar, true, Convert.ToString(Time[0]), 0, Median[0], 0, TextColor,
						new SimpleFont("Small Fonts", 12), TextAlignment.Right, Brushes.Transparent, Brushes.Transparent, 0);
					}
					else
					{
						// Calculate the position for the price label, adding separation from the dot's position
						double textYPosition;
						if (isBullish)
						{
							// Place the text further away from the bar than the dot
							textYPosition = dotYPosition + (10 * TickSize);
						}
						else
						{
							// Place the text further away from the bar than the dot
							textYPosition = dotYPosition - (10 * TickSize);
						}

						double priceToShow = (High[0] + Low[0]) / 2;
						string tradeInfo = Instrument.MasterInstrument.FormatPrice(priceToShow);

						// Draw the price label at its calculated position
						Draw.Text(this, "LargeTradePrice" + CurrentBar, true, tradeInfo, 0, textYPosition, 0, TextColor,
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
		private LTRyF[] cacheLTRyF;
		public LTRyF LTRyF(int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			return LTRyF(Input, largeVolume, timeFix, tradeColor, textColor, tradeShadowColor, colorOpacity, tradeAlert, tradeDot, tradeBarWidth, tradeTime, alertSound);
		}

		public LTRyF LTRyF(ISeries<double> input, int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			if (cacheLTRyF != null)
				for (int idx = 0; idx < cacheLTRyF.Length; idx++)
					if (cacheLTRyF[idx] != null && cacheLTRyF[idx].LargeVolume == largeVolume && cacheLTRyF[idx].TimeFix == timeFix && cacheLTRyF[idx].TradeColor == tradeColor && cacheLTRyF[idx].TextColor == textColor && cacheLTRyF[idx].TradeShadowColor == tradeShadowColor && cacheLTRyF[idx].ColorOpacity == colorOpacity && cacheLTRyF[idx].TradeAlert == tradeAlert && cacheLTRyF[idx].TradeDot == tradeDot && cacheLTRyF[idx].TradeBarWidth == tradeBarWidth && cacheLTRyF[idx].TradeTime == tradeTime && cacheLTRyF[idx].AlertSound == alertSound && cacheLTRyF[idx].EqualsInput(input))
						return cacheLTRyF[idx];
			return CacheIndicator<LTRyF>(new LTRyF(){ LargeVolume = largeVolume, TimeFix = timeFix, TradeColor = tradeColor, TextColor = textColor, TradeShadowColor = tradeShadowColor, ColorOpacity = colorOpacity, TradeAlert = tradeAlert, TradeDot = tradeDot, TradeBarWidth = tradeBarWidth, TradeTime = tradeTime, AlertSound = alertSound }, input, ref cacheLTRyF);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.LTRyF LTRyF(int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			return indicator.LTRyF(Input, largeVolume, timeFix, tradeColor, textColor, tradeShadowColor, colorOpacity, tradeAlert, tradeDot, tradeBarWidth, tradeTime, alertSound);
		}

		public Indicators.LTRyF LTRyF(ISeries<double> input , int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			return indicator.LTRyF(input, largeVolume, timeFix, tradeColor, textColor, tradeShadowColor, colorOpacity, tradeAlert, tradeDot, tradeBarWidth, tradeTime, alertSound);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.LTRyF LTRyF(int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			return indicator.LTRyF(Input, largeVolume, timeFix, tradeColor, textColor, tradeShadowColor, colorOpacity, tradeAlert, tradeDot, tradeBarWidth, tradeTime, alertSound);
		}

		public Indicators.LTRyF LTRyF(ISeries<double> input , int largeVolume, int timeFix, Brush tradeColor, Brush textColor, Brush tradeShadowColor, int colorOpacity, bool tradeAlert, bool tradeDot, double tradeBarWidth, bool tradeTime, string alertSound)
		{
			return indicator.LTRyF(input, largeVolume, timeFix, tradeColor, textColor, tradeShadowColor, colorOpacity, tradeAlert, tradeDot, tradeBarWidth, tradeTime, alertSound);
		}
	}
}

#endregion