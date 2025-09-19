
#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using System.Globalization;

using NinjaTrader.Core.FloatingPoint;

using SharpDX.DirectWrite;
//using System.Net.NetworkInformation;
using System.Net.Security;
using System.Collections.ObjectModel;

using System.Timers;
#endregion
#region Notes - YOU MUST TURN OFF PRICE MARKER VISIBILITY IN DATA SERIES IF YOU WANT TO USE PRICE AXIS FEATURE
/*
November 3, 2024 v6
Fixed:
	-	few minor formatting bugs
	- 	added single figure format ie last 10 seconds

Added features:

	-	reorganized the properties within the indicator properties window
	-	immediately show/hide certain options in the indicator properties window based on other options
	-   added new price Axis feature to show simple counter on top of price marker
	-   added ability to go clockwise and anticlockwise via key stroke ( Ctrl + 0 or 1)
	-   amended coloring on indicator so that counter can be a separate color scheme to the Clock
	-	Converted charts to time based or non time based ONLY


*/
#endregion

namespace NinjaTrader.NinjaScript.Indicators.Mindset
{
		#region Enums 
	
		public enum TheDisplayType
		{
			Alternative_Clock,
			Counter_Only,
			Chart_Time
		}
		
		public enum TheFlashType
		{
			None,
			WholePanel,
			ClockFaceOnly,
			TextOnly
		}
				public enum MyObjectPosition 
		{
					BottomRight,
					BottomLeft,
					TopRight,
					TopLeft,
					Centre,
					MidLeft,
					TopCentre,
					BottomCentre,
					Close,
					PriceAxis,
		}
					public enum MyTimeZones 
		{	
					Beijing,	
					CentralEurope,	
					HongKong,
					London,
					EST,
					EDT,
					NZ,	
					Singapore,
					Sydney,
					Tokyo,			
					Spare
		}
		#endregion
		
		#region Categories
		
		[Gui.CategoryOrder("Input", 1)]
		[TypeConverter("NinjaTrader.NinjaScript.Indicators.Mindset.BarTimerHybridv6Converter")]
		
		[CategoryExpanded(typeof(Custom.Resource), "Data Series", false)]
		[CategoryExpanded(typeof(Custom.Resource), "Setup", false)]
		[CategoryExpanded(typeof(Custom.Resource), "Visual", false)]
		[CategoryExpanded(typeof(Custom.Resource), "Plots", false)]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]

		#endregion

    public class BarTimerHybridv6 : Indicator
	{	

		#region Variables
		#region Strings
		private string					  Clock 					= string.Empty;
		private string			   Clock_String						= String.Empty;
		private string				 counterstr						= string.Empty;
		private string 			   altClockZone						= string.Empty;
		private static string 			youtube						= "https://youtu.be/sTFNeggvZ3Y?si=IRHPDi9kTQTkA5H3";
		private static string 		   youtube1						= "https://www.youtube.com/watch?v=L29IqEpS74I";
		private static string 			 author 					= "Mindset v6a December 2024";
		String newPriceMarkerValue;

		private SimpleFont 			   textFont;

		#endregion
	
		#region Doubles
		private double 		 TextSize,NonTimeBasedCount,volume,bp;
		#endregion
		
		#region Ints
		
		private int activeBar, priorRange, enumint,Trigger,
					 barType, ChkLevel = 0, actualRange,flashon;
		#endregion
		
		#region Enums & DateTime
		
		private 	DateTime			now		 					= Core.Globals.Now;
		private     DateTime 			Alternative_Clock_Now;

		private TheDisplayType			myDisplayType				= TheDisplayType.Chart_Time;
		private TheFlashType			myFlashType					= TheFlashType.WholePanel;	
		private MyObjectPosition		myClockPosition				= MyObjectPosition.Centre;
		private MyTimeZones 	        altTimeZone 				= MyTimeZones.EST;
		private TimeZoneInfo  			alternateZone;
	
		private TimeSpan 				barTimeLeft;
		
		private System.Windows.Threading.DispatcherTimer timer;	
		
		#endregion
		
		#region Bools
		private bool					showPercent, showTriggerLevels,	showDataLagVideo,
										countDown,countUp,connected,hasRealTimeData,
			 							isRangeDerivate,FlashOn,isVolume, isVolumeBase,
										supportsRange,isAdvancedType,isOtherType;
		#endregion
		
		#region Brushes(4)
		private SharpDX.Direct2D1.Brush dxBrush 					= null; //used for rendering the counter face
		private SharpDX.Direct2D1.Brush outlineBrush				= null; //used for rendering the counter face
		private SharpDX.Direct2D1.Brush counteroutlineBrush			= null; //used for rendering the counter face
		private SharpDX.Direct2D1.Brush dxBrushCounter 				= null; //used for rendering the counter face
		#endregion
		
		#region Controls,Misc
		private System.Windows.Controls.Button youtubeButton;
		private System.Windows.Controls.Button youtubeButton1;
		private Chart  					chartWindow;
		private System.Windows.Controls.Grid myGridYT;
		
		private SessionIterator	sessionIterator;

		#endregion
		#endregion		
		
		#region StateChanges
		protected override void OnStateChange()
		{
		#region State.SetDefaults
			if (State == State.SetDefaults)
			{	
				Name							= "BarTimerHybridv6";
				Calculate						= Calculate.OnEachTick;//can be changed if not using tick or volume charts

			#region User Inputs

				PrintTimeZones 					= false;
				
				TextFont 						= new SimpleFont("Impact", 30);
				TextBrush1 						= Brushes.Goldenrod;
				TextBrush2 						= Brushes.Crimson;
				AlterFontColour					= true;

                ClockBrushOutline           	= Brushes.Black;
				CounterBrushOutline				= Brushes.Black;
                TextBrushForSound				= Brushes.White;
				Gradient_Mixer_Colour 			= Brushes.DarkBlue;
				Gradient_Mixer_Colour1 			= Brushes.Cyan;
				BasePlotColor					= Brushes.Black;
				FlashPlotColor					= Brushes.Magenta;

				NonTimeBasedLevel				= 50;
				TimeBasedLevel					= 10;
				ShowMaxRange					= true;
				CountDown						= true;
				CountUp							= false;
				ShowPercent 					= false;
				ShowTriggerLevels				= true;
				
				ShowGuide 						= true;
				ShowFlashOptions 				= false;
				ShowCounterOptions 				= false;
				#endregion
			#region dummy plot - used for PriceAxis option only
				AddPlot(new Stroke(Brushes.Black, DashStyleHelper.Dot, 1), PlotStyle.Hash,  "DummyPlot");
				#endregion
			}
			#endregion
			
		#region State.Configure
			else if (State == State.Configure)
			{	
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;		
				IsChartOnly									= true;
				IsOverlay									= true;
				IsAutoScale									= false;
				DisplayInDataBox							= false;
				DrawOnPricePanel							= true;
				PaintPriceMarkers							= true;		
				ArePlotsConfigurable						= true;
				ShowTransparentPlotsInDataBox				= false;
				IsSuspendedWhileInactive					= true;				
			}
			#endregion

		#region Data Loaded - outline BarTypes and set check levels
			else if (State == State.DataLoaded)
			{
				if ((BarsPeriod.BarsPeriodType == BarsPeriodType.Tick 
					|| (BarsPeriod.BarsPeriodType == BarsPeriodType.HeikenAshi && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Tick)
					|| (BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Tick)))
				{
					barType = 1;/// tick based

					{
						ChkLevel = (BarsPeriod.BarsPeriodType == BarsPeriodType.HeikenAshi
								|| BarsPeriod.BarsPeriodType == BarsPeriodType.LineBreak
								|| BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric)
								? BarsPeriod.BaseBarsPeriodValue : BarsPeriod.Value;		
					}
				}
				
				else if ((BarsPeriod.BarsPeriodType == BarsPeriodType.Volume 
					|| (BarsPeriod.BarsPeriodType == BarsPeriodType.HeikenAshi && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Volume)
					|| (BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Volume)))
				{
					barType = 2;/// Volume based
					{
					
						ChkLevel = (BarsPeriod.BarsPeriodType == BarsPeriodType.HeikenAshi && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Volume
									|| BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Volume) 
									? BarsPeriod.BaseBarsPeriodValue : BarsPeriod.Value;	
									isVolume 		= BarsPeriod.BarsPeriodType == BarsPeriodType.Volume;
									isVolumeBase 	= (BarsPeriod.BarsPeriodType == BarsPeriodType.HeikenAshi || BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric) && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Volume;

					
					}
				}
				else if ((BarsPeriod.BarsPeriodType == BarsPeriodType.Minute 
					|| BarsPeriod.BarsPeriodType == BarsPeriodType.Second 
					|| (BarsPeriod.BarsPeriodType == BarsPeriodType.HeikenAshi && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Minute) 
					|| (BarsPeriod.BarsPeriodType == BarsPeriodType.HeikenAshi && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Second)
					|| (BarsPeriod.BarsPeriodType == BarsPeriodType.LineBreak && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Minute) 
					|| (BarsPeriod.BarsPeriodType == BarsPeriodType.LineBreak && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Second)
					|| (BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Minute)
					|| (BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Second)))
				{
					barType = 3;  /// time based

					ChkLevel = (BarsPeriod.BarsPeriodType == BarsPeriodType.HeikenAshi 
								|| BarsPeriod.BarsPeriodType == BarsPeriodType.LineBreak
								|| BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric)  
								? BarsPeriod.BaseBarsPeriodValue : BarsPeriod.Value;
					
					if ((BarsPeriod.BarsPeriodType == BarsPeriodType.Minute 
						|| BarsPeriod.BarsPeriodType == BarsPeriodType.HeikenAshi && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Minute
						|| BarsPeriod.BarsPeriodType == BarsPeriodType.LineBreak && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Minute)
						|| BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric && BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Minute)
						ChkLevel = ChkLevel * 60;
					
				}
				else if (BarsPeriod.BarsPeriodType == BarsPeriodType.Range|| BarsPeriod.BarsPeriodType == BarsPeriodType.Renko ||
					BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Range && isAdvancedType ||
					BarsArray[0].BarsType.BuiltFrom == BarsPeriodType.Tick && isOtherType||
					BarsArray[0].BarsType.BuiltFrom == BarsPeriodType.Tick && BarsArray[0].BarsPeriod.ToString().IndexOf("Range") >= 0)
				{
					
					barType = 4;/// Range
					supportsRange = true;
					isAdvancedType = BarsPeriod.BarsPeriodType == BarsPeriodType.HeikenAshi || BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric;
					isOtherType	   = BarsPeriod.ToString().IndexOf("Range") >= 0 || BarsPeriod.ToString().IndexOf(NinjaTrader.Custom.Resource.BarsPeriodTypeNameRange) >= 0;

					ChkLevel = BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Range && isAdvancedType ||
					BarsArray[0].BarsType.BuiltFrom == BarsPeriodType.Tick && isOtherType||//BarsArray[0].BarsType.BuiltFrom == BarsPeriodType.Range && isOtherType||
					(BarsArray[0].BarsType.BuiltFrom == BarsPeriodType.Tick && BarsArray[0].BarsPeriod.ToString().IndexOf("Range") >= 0) //|| (BarsArray[0].BarsType.BuiltFrom == BarsPeriodType.Tick && BarsArray[0].BarsPeriod.ToString().IndexOf("Brick Size") >= 0)
						? BarsPeriod.BaseBarsPeriodValue : BarsPeriod.Value;
//					if(BarsPeriod.BarsPeriodType == BarsPeriodType.Renko)
//						ChkLevel /= TickSize;
				}	
				else
					barType = 5;
				
			}//Data loaded
		#endregion
				
		#region Historical
			else if (State == State.Historical)
			{
				if(myFlashType == TheFlashType.WholePanel)	
					SetZOrder(int.MinValue);///Flash Panel object is behind the bars - ensures you can still see the Data Series
					else
					SetZOrder(int.MaxValue);/// Make sure our Clock object plots behind the chart bars in all other cases	

				#region YouTube Guides
			
			if (ShowGuide)
			{
				if (UserControlCollection.Contains(myGridYT))
					return;
				
				Dispatcher.InvokeAsync((() =>
				{
					myGridYT = new System.Windows.Controls.Grid
					{
						Name = "MyCustomGrid", HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom
					};
					
					System.Windows.Controls.ColumnDefinition column1 = new System.Windows.Controls.ColumnDefinition();
					System.Windows.Controls.ColumnDefinition column2 = new System.Windows.Controls.ColumnDefinition();

					myGridYT.ColumnDefinitions.Add(column1);
					myGridYT.ColumnDefinitions.Add(column2);

				
					youtubeButton = new System.Windows.Controls.Button
						
					{
						Name = "YoutubeButton", Content = "Indicator Guide", Foreground = Brushes.Black, Background = Brushes.Red, ToolTip =("Remove by unchecking Guides section in parameters")
					};
					
						youtubeButton1 = new System.Windows.Controls.Button
					{
						Name = "YoutubeButton1", Content = "Auto Sync Clock", Foreground = Brushes.Black, Background = Brushes.Gold
					};
					youtubeButton.Click += OnButtonClick;
					youtubeButton1.Click += OnButtonClick;
					System.Windows.Controls.Grid.SetColumn(youtubeButton, 0);
					System.Windows.Controls.Grid.SetColumn(youtubeButton1, 1);

					myGridYT.Children.Add(youtubeButton);
					myGridYT.Children.Add(youtubeButton1);

					
					UserControlCollection.Add(myGridYT);
				}));
			}
		#endregion

				 ChartControl.Dispatcher.InvokeAsync(() =>
                 {
				chartWindow = Window.GetWindow(this.ChartControl.Parent) as Chart;

				   if (chartWindow != null)
                {
					chartWindow.KeyDown += OnKeyDown;
                }
				});
			}
			#endregion

		#region State.Realtime
			else if (State == State.Realtime)
			{
		if (timer == null)
				{
					if (timer == null && IsVisible)
				{
					if (Bars.BarsType.IsTimeBased && Bars.BarsType.IsIntraday)
					{
						lock (Connection.Connections)
						{
							if (Connection.Connections.ToList().FirstOrDefault(c => c.Status == ConnectionStatus.Connected && c.InstrumentTypes.Contains(Instrument.MasterInstrument.InstrumentType)) == null)
								Draw.TextFixed(this, "NinjaScriptInfo", NinjaTrader.Custom.Resource.BarTimerDisconnectedError, TextPosition.BottomRight, ChartControl.Properties.ChartText, ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 0);
							else
							{
								if (!SessionIterator.IsInSession(Now, false, true))
									Draw.TextFixed(this, "NinjaScriptInfo", NinjaTrader.Custom.Resource.BarTimerSessionTimeError, TextPosition.BottomRight, ChartControl.Properties.ChartText, ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 0);
								else
									Draw.TextFixed(this, "NinjaScriptInfo", NinjaTrader.Custom.Resource.BarTimerWaitingOnDataError, TextPosition.BottomRight, ChartControl.Properties.ChartText, ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 0);
							}
						}
					}
					else
						Draw.TextFixed(this, "NinjaScriptInfo", NinjaTrader.Custom.Resource.BarTimerTimeBasedError, TextPosition.BottomRight, ChartControl.Properties.ChartText, ChartControl.Properties.LabelFont, Brushes.Transparent, Brushes.Transparent, 0);
				}
				}
				#region dummy plot
			//if(ChartBars != null)
			//ChartBars.Properties.PriceMarker.Background = Brushes.Black;
				#endregion
			}
			#endregion
			
		#region State.Terminate
			
		else if (State == State.Terminated)
			{
			#region Terminate You Tube buttons
			
			if (ShowGuide)
			{
				Dispatcher.InvokeAsync((() =>
				{
					if (myGridYT != null)
					{
						if (youtubeButton != null)
						{
							myGridYT.Children.Remove(youtubeButton);
							youtubeButton.Click -= OnButtonClick;
							youtubeButton = null;
						}
						if (youtubeButton1 != null)
						{
							myGridYT.Children.Remove(youtubeButton1);
							youtubeButton1.Click -= OnButtonClick;
							youtubeButton1 = null;
						}			
					}
				}));
			}
		#endregion
			
				if (timer == null)
					return;
				timer.IsEnabled = false;
				timer = null;			
			
			    	if (chartWindow != null)
					chartWindow.KeyDown -= OnKeyDown; 
			}
			#endregion
		}
		#endregion	
		
		#region FormatPriceMarker()
		public override string FormatPriceMarker(double price)
	{		
				newPriceMarkerValue = price.ToString();	///set default
				if(Clock_Position == MyObjectPosition.PriceAxis)
					//if(PriceMarker.Visibility)
		{
				newPriceMarkerValue = String.Format("{1}{0}{2}{0}", Environment.NewLine,counterstr,price);
				//newPriceMarkerValue = String.Format("{1}{0}{2}{0}", Environment.NewLine, price, counterstr);///Alternative

			if(myFlashType !=TheFlashType.None)
			{
				if(Trigger >0)
				{
				if(FlashSwitch() > 0)
				//{
				//newPriceMarkerValue = price.ToString();
					
					Plots[0].Brush = BasePlotColor;//Brushes.Black;
				//}
				else
					//Plots[0].Pen.Brush = Brushes.Magenta;// read only state
					Plots[0].Brush = FlashPlotColor;//Brushes.Magenta;//IsOpacityVisible = false;
				
				}
			}
		} 
				return newPriceMarkerValue;
	}
		#endregion

		#region OnKeyDown
		public void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
			if(Keyboard.Modifiers == ModifierKeys.Control)
		{
			if (e.Key == Key.D0|| e.Key == Key.NumPad0 )
			{
			///Scroll through Clock Positions Clockwise 
				if(enumint < 9)
				enumint += 1;
				else
				enumint = 0;
				}
			
			if (e.Key == Key.D1 || e.Key == Key.NumPad1 )
			{
			///Scroll through Clock Positions Anti Clockwise 
				if(enumint > 0)
				enumint -= 1;
				else
				enumint = 9;			
			}
				
			switch (enumint)
			{		
						case 0:
						Clock_Position = MyObjectPosition.TopLeft;
						enumint = 0;			
						break;
					
						case 1:	
						Clock_Position = MyObjectPosition.TopCentre;
						enumint = 1;
						break;	
					
						case 2:
						Clock_Position = MyObjectPosition.TopRight;				
						enumint = 2;
						break;
					
						case 3:
						Clock_Position = MyObjectPosition.Close;
						enumint = 3;
						break;
					
						case 4:
						Clock_Position = MyObjectPosition.BottomRight;
						enumint = 4;
						break;
					
						case 5:
						Clock_Position = MyObjectPosition.BottomCentre;	
						enumint = 5;
						break;
					
						case 6:
						Clock_Position = MyObjectPosition.BottomLeft;									
						enumint = 6;
						break;
					
						case 7:
						Clock_Position = MyObjectPosition.Centre;
						enumint = 7;
						break;
					
						case 8:
						Clock_Position = MyObjectPosition.MidLeft;
						enumint = 8;
						break;
					
						case 9:
						Clock_Position = MyObjectPosition.PriceAxis;
						enumint = 9;
						break;
				break;									
		break;			
			}
	
	}		
}
	#endregion
	
		#region SetNonTimeBasedTriggers
				private int SetNonTimeBasedTriggers( bool Ascending, bool percentbasis)
			{
				if(myFlashType != TheFlashType.None)
				{			
				if(percentbasis && Ascending)	//user uses nos > 1
						Trigger = barPercentage() >= NonTimeBasedLevel*.01 ? 1 : 0;	
				if(percentbasis && !Ascending)
						Trigger = barPercentage() <= NonTimeBasedLevel*.01 ? 1 : 0;
				
				if (!percentbasis  && Ascending)	
						Trigger = NonTimeBasedCount >= NonTimeBasedLevel ? 1 : 0;
				if(!percentbasis && !Ascending)			
						Trigger = NonTimeBasedCount <= NonTimeBasedLevel ? 1 : 0;	
				}
				return Trigger;

			}
				
		#endregion
				
		#region BarPercentage()
		
		private double barPercentage()
		{
		if(barType == 4)///range	
		{
		bp = isAdvancedType ? BarsPeriod.BaseBarsPeriodValue : BarsPeriod.Value;
		bp = CountDown ? 1 -( Math.Abs(bp - NonTimeBasedCount ) / bp): (Math.Abs((bp - NonTimeBasedCount) / bp - 1));
		}
		else
		bp = CountDown ? (1 - Bars.PercentComplete): Bars.PercentComplete ;	

		bp = Math.Round(bp, 2, MidpointRounding.AwayFromZero)  ;
			return bp;
		}
				#endregion
				
		#region OnBarUpdate
		protected override void OnBarUpdate()
		{
		#region Print Available time zones to user computer
			if(PrintTimeZones && CurrentBar == 0)
			ShowTimeZones();
			#endregion
			
		#region Check run State and set up Global defaults and variables
			
		if (BarsArray == null || 
			 State < State.Realtime )
		return;
	
		if (State == State.Realtime)
		{
			hasRealTimeData = true;
			connected = true;
			
			if(IsFirstTickOfBar)
				Plots[0].Brush = BasePlotColor;//reset to base in case alert is set to even number

		///Set Global variables/defaults
		if(myDisplayType == TheDisplayType.Alternative_Clock)					
		Clock = AlternativeClock().ToString("HH:mm:ss")+"\n";
		if(myDisplayType == TheDisplayType.Chart_Time)			
		Clock = Now.ToString("HH:mm:ss")+"\n";
		
		DisplayTime();

		if(CurrentBar != activeBar && SoundsOn)
		{
		PlaySound(Sound_File);
		activeBar = CurrentBar;
		}
				
		#region Warnings
				//Draw.TextFixed(this,"D{D}", "Trigger value "+Trigger.ToString()+"\n BarPercentage = "+(barPercentage()).ToString() +"\n ChkLevel =  "+ChkLevel.ToString(),TextPosition.BottomRight);	

			if(ShowPercent)
			{
				if( NonTimeBasedLevel > 99 || TimeBasedLevel > 99)
			
				Draw.TextFixed(this,"Warning","Error1 - Alert level is > 100 % at  "+NonTimeBasedLevel.ToString(),TextPosition.Center);
			}

#endregion

				
		if(barType !=3)///any Chart NOT time based
		{
			if(myDisplayType == TheDisplayType.Counter_Only)
			{	
			if(Clock_Position != MyObjectPosition.PriceAxis)///switch and nullify counterstr as we use Clock to measure spacing in OnRender
					{
					Clock = counterstr;
					counterstr = String.Empty;
					}
			}
			if(!ShowPercent  && !CountDown)/// Counting up in number format
			SetNonTimeBasedTriggers(true,false);
			
			if(!ShowPercent && CountDown)/// Counting Down in number format
			SetNonTimeBasedTriggers(false,false);
			
			if(ShowPercent && !CountDown)/// Counting up in % format
			Trigger = SetNonTimeBasedTriggers(true,true);
			
			if(ShowPercent && CountDown)/// Counting Down in % format
			SetNonTimeBasedTriggers(false,true);
		}
			

		#endregion
				
		#region BarType = Tick	
			
			if (barType == 1)///==========================TICK=============================
			{
				RemoveDrawObject ("NinjaScriptInfo");
			//if (hasRealTimeData)
							
			double periodValue 	= (BarsPeriod.BarsPeriodType == BarsPeriodType.Tick) ? BarsPeriod.Value : BarsPeriod.BaseBarsPeriodValue;
			//TickCount  			= ShowPercent ? CountDown ? (1 - Bars.PercentComplete) : Bars.PercentComplete : CountDown ? periodValue - Bars.TickCount : Bars.TickCount;
			//NonTimeBasedCount 	= ShowPercent ?  barPercentage() : CountDown ? periodValue - TickCount : TickCount;
					NonTimeBasedCount = ShowPercent ? CountDown ? barPercentage() : barPercentage() :CountDown ? periodValue - Bars.TickCount : Bars.TickCount;
				counterstr = ShowPercent ? barPercentage().ToString("P0") : NonTimeBasedCount.ToString();//Default for volume	

				if (myDisplayType == TheDisplayType.Counter_Only)
				{
					if(Clock_Position != MyObjectPosition.PriceAxis)//switch and nullify counterstr as we use Clock to measure spacing
							{
							Clock = counterstr;
							counterstr = String.Empty;
							}
				}	
			}  // tick counter	
	#endregion
			
		#region BarType = Volume Based Charts
			if (barType == 2 )//NB Clock set in Globals
			{
				RemoveDrawObject ("NinjaScriptInfo");// would be nice to find a way just to suppress this 
				volume = Instrument.MasterInstrument.InstrumentType == InstrumentType.CryptoCurrency ? Core.Globals.ToCryptocurrencyVolume((long)Volume[0]) : Volume[0];
				// NonTimeBasedCount = ShowPercent ? barPercentage() : CountDown ? BarsPeriod.Value - volume : volume;//use ChkLevel here?
NonTimeBasedCount = CountDown ? (isVolumeBase? BarsPeriod.BaseBarsPeriodValue: BarsPeriod.Value) - volume: volume;
						
				counterstr = ShowPercent ? barPercentage().ToString("P0") : NonTimeBasedCount.ToString();//Default for volume		

				if (myDisplayType == TheDisplayType.Counter_Only)
				{
					if(Clock_Position != MyObjectPosition.PriceAxis)//switch and nullify counterstr as we use Clock to measure spacing
							{
							Clock = counterstr;
							counterstr = String.Empty;
							}
				}		
			}
			#endregion
				
		#region BarType = Time Based Charts	
						
			if ( barType == 3 ) ///Seconds
			{				
			barTimeLeft = CountDown ? Bars.GetTime(Bars.Count - 1).Subtract(Now):barTimeLeft = (Bars.GetTime(Bars.Count - 2).Subtract(Now)).Duration();
							
				#region Set Time Triggers
				
				if (!ShowPercent  && !CountDown)/// Counting up in number format	
					Trigger = barTimeLeft.TotalSeconds >= TimeBasedLevel ? 1 : 0;
			
				if(ShowPercent && !CountDown)	/// Counting up in % format 							
					Trigger =  barPercentage() >= TimeBasedLevel *.01 ? 1 : 0;
		
				if(!ShowPercent && CountDown)	///counting Down in Number format
					Trigger = barTimeLeft.TotalSeconds <= TimeBasedLevel  ? 1 : 0;

				if(ShowPercent && CountDown) ///counting Down in % format
					Trigger = (barPercentage()) < TimeBasedLevel * .01 ? 1 : 0;

				#endregion
						
				#region Display Counter only Formats - Time Based Charts	
				
				if (myDisplayType == TheDisplayType.Counter_Only)
					{	
						if(barTimeLeft.Hours > 0)					
							counterstr =  ShowPercent ? barPercentage().ToString("P0"):barTimeLeft.Hours.ToString("00") + ":" + barTimeLeft.Minutes.ToString("00") + ":" + barTimeLeft.Seconds.ToString("00");			
						
	 					if(barTimeLeft.Hours < 1 && barTimeLeft.Minutes > 0)
							counterstr =  ShowPercent ? barPercentage().ToString("P0"):barTimeLeft.Minutes.ToString("00") + ":"  + barTimeLeft.Seconds.ToString("00");
							
						if(barTimeLeft.Minutes < 1)
							counterstr =  ShowPercent ? barPercentage().ToString("P0"): barTimeLeft.Seconds.ToString("00");
						
						if(barTimeLeft.Seconds <10)
							counterstr =  ShowPercent ? barPercentage().ToString("P0"): barTimeLeft.Seconds.ToString("0");						

						if(Clock_Position != MyObjectPosition.PriceAxis)//switch and nullify counterstr as we use Clock to measure spacing
						{
						Clock = counterstr;
						counterstr = String.Empty;
						}
					}///counter only
					#endregion
					
				#region Display Alternative Clock Formats - Time Based Charts
					if (myDisplayType == TheDisplayType.Alternative_Clock)
					{
						 if(barTimeLeft.Hours > 0)
							counterstr = ShowPercent ? (barPercentage().ToString("P0")).ToString() :barTimeLeft.Hours.ToString("00") + ":"+barTimeLeft.Minutes.ToString("00") +(barTimeLeft.Ticks < 0 ?"00:00":  ":" + barTimeLeft.Seconds.ToString("00"));							
															
						 if(barTimeLeft.Hours < 1 && barTimeLeft.Minutes > 0)
							counterstr = ShowPercent ? (barPercentage().ToString("P0")).ToString():  barTimeLeft.Minutes.ToString("00") + (barTimeLeft.Ticks < 0 ?"00:00":  ":" + barTimeLeft.Seconds.ToString("00"));					
						  	 					 
						 if(barTimeLeft.Minutes < 1) 
							counterstr = ShowPercent ? (barPercentage().ToString("P0")).ToString() : (barTimeLeft.Ticks < 0 ?"00:00": barTimeLeft.Seconds.ToString("00"));				
	
						 if(barTimeLeft.Seconds <10)
							counterstr = ShowPercent ? (barPercentage().ToString("P0")).ToString() : (barTimeLeft.Ticks < 0 ?"00:00": barTimeLeft.Seconds.ToString("0"));		  
					}
			#endregion
					
				#region Display Chart Time Formats - Time Based Charts
					
				 if (myDisplayType == TheDisplayType.Chart_Time)
					{					
						if(barTimeLeft.Hours > 0)
						{
							counterstr = ShowPercent ? (barPercentage().ToString("P0")).ToString() : counterstr =  barTimeLeft.Hours.ToString("00") + ":"+barTimeLeft.Minutes.ToString("00") +(barTimeLeft.Ticks < 0 ?"00:00":  ":" + barTimeLeft.Seconds.ToString("00"));					
						}
						 if(barTimeLeft.Hours < 1 && barTimeLeft.Minutes > 0)						
							counterstr = ShowPercent ? (barPercentage().ToString("P0")).ToString() : barTimeLeft.Minutes.ToString("00") +(barTimeLeft.Ticks < 0 ?"00:00":  ":" + barTimeLeft.Seconds.ToString("00"));
								
						if(barTimeLeft.Minutes < 1)	
							counterstr = ShowPercent ? (barPercentage().ToString("P0")).ToString() : (barTimeLeft.Ticks < 0 ?"00:00": barTimeLeft.Seconds.ToString("00"));
								
						if(barTimeLeft.Seconds <10)
							counterstr = ShowPercent ? (barPercentage().ToString("P0")).ToString() : (barTimeLeft.Ticks < 0 ?"00:00": barTimeLeft.Seconds.ToString("0"));
					
					}///local clock only
					#endregion
					
				#region Price Axis - Time Based Charts
					
					if(Clock_Position == MyObjectPosition.PriceAxis)
						FormatPriceMarker(Close[0]);
					#endregion
			}
			#endregion			
			
		#region BarType = Range

					if(barType == 4)
	{
				RemoveDrawObject ("NinjaScriptInfo");	
				if (supportsRange)
			{
				double	high		= High.GetValueAt(Bars.Count - 1 - (Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 1 : 0));
				double	low			= Low.GetValueAt(Bars.Count - 1 - (Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 1 : 0));
				double	close		= Close.GetValueAt(Bars.Count - 1 - (Calculate == NinjaTrader.NinjaScript.Calculate.OnBarClose ? 1 : 0));
				actualRange			= (int)Math.Round(Math.Max(close - low, high - close) / Bars.Instrument.MasterInstrument.TickSize);
				NonTimeBasedCount	= CountDown ? (isAdvancedType ? BarsPeriod.BaseBarsPeriodValue : BarsPeriod.Value) - actualRange : actualRange;
				
				    priorRange = Math.Max(priorRange,actualRange);/// regardless of countdown or countup
					
					if(IsFirstTickOfBar)
					priorRange = CountDown ? BarsPeriod.Value : 1;
					
					if(ShowMaxRange)
						NonTimeBasedCount = CountDown ? Math.Min(priorRange,NonTimeBasedCount) : Math.Max(priorRange,NonTimeBasedCount);
					
					counterstr = ShowPercent ? barPercentage().ToString("P0") : NonTimeBasedCount.ToString();	
					//Draw.TextFixed(this,"D{D}", "NTBC "+NonTimeBasedCount.ToString()+"\n BarPercentage = "+(barPercentage()).ToString() +"\n Range  =  "+actualRange.ToString()+"\nBars Value = "+BarsPeriod.Value.ToString(),TextPosition.TopRight);	

					if (myDisplayType == TheDisplayType.Counter_Only)
				{
					if(Clock_Position != MyObjectPosition.PriceAxis)//switch and nullify counterstr as we use Clock to measure spacing
					{
					Clock = counterstr;
					counterstr = String.Empty;
					}
				}
				RemoveDrawObject ("NinjaScriptInfo1");	
//						if (NonTimeBasedCount <= NonTimeBasedLevel)
//				{
//					RemoveDrawObject ("NinjaScriptInfo");
//				}
//				else
//				{
//					RemoveDrawObject ("NinjaScriptInfo1");
//				}	
			}
	
	}
	#endregion
		
		#region BarType = Any other type 
	if( barType >= 5 )///Other bar types that are not time,tick,volume or range based
	{
	Trigger = -1;// prevent Flashing
	}
	#endregion

		#region dummy plot
			DummyPlot[0] = Close[0];
			#endregion
		}/// real time		
	}///On Bar Update
		#endregion

	    #region OnRender nov24
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);
			{
			float halfpanelH 	= ChartPanel.H/2;
			float LowerY 		= ChartPanel.Y + ChartPanel.H;
			NinjaTrader.Gui.Tools.SimpleFont simpleFont 				= TextFont;	
			SharpDX.Direct2D1.Brush dxBrush = TextBrush1.ToDxBrush(RenderTarget);
			SharpDX.Direct2D1.Brush dxBrushCounter = TextBrush2.ToDxBrush(RenderTarget);

            SharpDX.Direct2D1.Brush outlineBrush = ClockBrushOutline.ToDxBrush(RenderTarget);
		    SharpDX.Direct2D1.Brush counteroutlineBrush = CounterBrushOutline.ToDxBrush(RenderTarget);
	
            if (SoundsOn && AlterFontColour)
			{
				dxBrush =   TextBrushForSound.ToDxBrush(RenderTarget);
			}
			SharpDX.Color sharpColor = new SharpDX.Color(Gradient_Mixer_Colour.Color.R,Gradient_Mixer_Colour.Color.G,Gradient_Mixer_Colour.Color.B);//gradient colour​
			SharpDX.Color sharpColorbase = new SharpDX.Color(Gradient_Mixer_Colour1.Color.R,Gradient_Mixer_Colour1.Color.G,Gradient_Mixer_Colour1.Color.B);//gradient colour​
			
			SharpDX.DirectWrite.TextFormat textFormat1 					= textFont.ToDirectWriteTextFormat();//changed from simpleFont
			SharpDX.DirectWrite.TextFormat textFormat2 					= textFont.ToDirectWriteTextFormat();

			//on counter only the Clock is empty so it only returns a width of 1
			SharpDX.DirectWrite.TextLayout textLayout1 	= new SharpDX.DirectWrite.TextLayout(NinjaTrader.Core.Globals.DirectWriteFactory,
			Clock, textFormat1, ChartPanel.X + ChartPanel.W, textFormat1.FontSize);
 			textFormat1.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;

			SharpDX.DirectWrite.TextLayout textLayout2 	= new SharpDX.DirectWrite.TextLayout(NinjaTrader.Core.Globals.DirectWriteFactory,
			counterstr, textFormat2, textLayout1.Metrics.Width ,textFormat2.FontSize);
		
			//Input two Text Points for our two lines of text (these are just dummy co-ordinates and are not used)
			SharpDX.Vector2 textPoint2 	= new SharpDX.Vector2(ChartPanel.X, ChartPanel.Y);
			SharpDX.Vector2 textPoint1 	= new SharpDX.Vector2(ChartPanel.X, ChartPanel.Y);
			
			#region My Text Placement co-ordinate variables
			
			float halfwidth1 	= textLayout1.Metrics.Width/2;
			float halfheight1 	= textLayout1.Metrics.Height/2;
			float halfheight2 	= textLayout2.Metrics.Height/2;
			float centrepoint 	= ChartPanel.X + ChartPanel.W/2 - halfwidth1;
			float midTextPoint1 = halfwidth1 - Clock.Length/2;
			float textHeight	= textLayout1.Metrics.Height;
		
			#endregion							
			
			#region Rotate Clock Positions					
			switch(myClockPosition)
			{	
				case MyObjectPosition.TopLeft:
				textPoint1 = new SharpDX.Vector2(ChartPanel.X + 10, ChartPanel.Y);
				textPoint2 	= new SharpDX.Vector2(textPoint1.X+textLayout1.Metrics.Width/2- textLayout2.Metrics.Width/2, ChartPanel.Y + textLayout1.Metrics.Height/2);

 				enumint = 0;
				break;
					
				case MyObjectPosition.TopCentre:
				textPoint1 = new SharpDX.Vector2(ChartPanel.X + ChartPanel.W/2 - textLayout1.Metrics.Width/2, ChartPanel.Y);
				textPoint2 	= new SharpDX.Vector2(textPoint1.X+textLayout1.Metrics.Width/2- textLayout2.Metrics.Width/2, ChartPanel.Y + textLayout1.Metrics.Height/2);

					enumint = 1;
				break;		
				
				case MyObjectPosition.TopRight:
					
				textPoint1 = new SharpDX.Vector2(ChartPanel.X + ChartPanel.W - textLayout1.Metrics.Width, ChartPanel.Y );
				textPoint2 	= new SharpDX.Vector2(textPoint1.X+textLayout1.Metrics.Width/2- textLayout2.Metrics.Width/2, ChartPanel.Y+ textLayout1.Metrics.Height/2);

				enumint = 2;
				break;	
					
				case MyObjectPosition.Close:
				if (ChartBars != null)
				{
				double close_px = Close.GetValueAt(ChartBars.ToIndex-1);
				float y_Close = chartScale.GetYByValue(close_px)- textLayout1.Metrics.Height;
				float y_CloseClock = chartScale.GetYByValue(close_px)-textLayout1.Metrics.Height/2;
	
		        float xpos = chartControl.GetXByBarIndex(ChartBars,ChartBars.ToIndex)+(ChartPanel.W - chartControl.GetXByBarIndex(ChartBars,ChartBars.ToIndex)-textLayout1.Metrics.Width)-5;
			
				textPoint1 	= new SharpDX.Vector2(xpos,y_Close);
				textPoint2 	= new SharpDX.Vector2(textPoint1.X+textLayout1.Metrics.Width/2- textLayout2.Metrics.Width/2, y_Close + textLayout1.Metrics.Height/2);

				enumint = 3;
				}
				break;
				
				case MyObjectPosition.BottomRight:
					textPoint1 	= new SharpDX.Vector2(ChartPanel.W- textLayout1.Metrics.Width, ChartPanel.Y + ChartPanel.H -textLayout1.Metrics.Height);
					textPoint2 	= new SharpDX.Vector2(ChartPanel.W- textLayout1.Metrics.Width/2-textLayout2.Metrics.Width/2, ChartPanel.Y + ChartPanel.H -textLayout1.Metrics.Height/2);
		
					enumint = 4;
				break;
					
				case MyObjectPosition.BottomCentre:
					textPoint1 	= new SharpDX.Vector2(ChartPanel.X +ChartPanel.W/2-textLayout1.Metrics.Width/2,
					ChartPanel.Y + ChartPanel.H -textLayout1.Metrics.Height);	
					textPoint2 	= new SharpDX.Vector2(textPoint1.X+
					(textLayout1.Metrics.Width/2)-(textLayout2.Metrics.Width/2),
					ChartPanel.Y + ChartPanel.H - (textLayout1.Metrics.Height/2));
					
					enumint = 5;
				break;
					
				case MyObjectPosition.BottomLeft:
			 		textPoint1 	= new SharpDX.Vector2(ChartPanel.X+5 , ChartPanel.Y + ChartPanel.H -textLayout1.Metrics.Height);
			 		textPoint2 	= new SharpDX.Vector2(textPoint1.X+textLayout1.Metrics.Width/2- textLayout2.Metrics.Width/2, ChartPanel.Y + ChartPanel.H -textLayout1.Metrics.Height/2);
					
					enumint = 6;
				break;
		
				case MyObjectPosition.Centre:
					textPoint1  = new SharpDX.Vector2(ChartPanel.X + ChartPanel.W/2-textLayout1.Metrics.Width/2,
					ChartPanel.Y + ChartPanel.H/2 -(textLayout1.Metrics.Height));
 					textPoint2 	= new SharpDX.Vector2(ChartPanel.X + ChartPanel.W/2-textLayout2.Metrics.Width/2,
					ChartPanel.Y + ChartPanel.H/2 -(textLayout1.Metrics.Height/2));

					enumint = 7;
				break;	
					
				case MyObjectPosition.MidLeft:
			 		textPoint1 	= new SharpDX.Vector2(ChartPanel.X , ChartPanel.Y + halfpanelH -halfheight2);
			 		textPoint2 	= new SharpDX.Vector2(textPoint1.X + midTextPoint1,textPoint1.Y  + halfheight1);

					enumint = 8;
				break;
			break;			
			}
#endregion

			#region Gradients
			SharpDX.Vector2 startPoint 									= new SharpDX.Vector2(ChartPanel.X, ChartPanel.Y); 
			SharpDX.Vector2 endPoint 									= new SharpDX.Vector2(ChartPanel.X + ChartPanel.W, ChartPanel.Y + ChartPanel.H);
			SharpDX.Direct2D1.LinearGradientBrush linearGradientBrush 	= new SharpDX.Direct2D1.LinearGradientBrush(RenderTarget, new SharpDX.Direct2D1.LinearGradientBrushProperties()
			{
				StartPoint = new SharpDX.Vector2(0, startPoint.Y),
				EndPoint = new SharpDX.Vector2(0, endPoint.Y),		
			},
			
			new SharpDX.Direct2D1.GradientStopCollection(RenderTarget, new SharpDX.Direct2D1.GradientStop[]
			{
				new	SharpDX.Direct2D1. GradientStop()
				{
					/// blue/cyan is nice, green yellow is sunset / black and white v good
					Color =  sharpColor,
					Position = 0,
				},
				new SharpDX.Direct2D1. GradientStop()
				{
					Color =  sharpColorbase,
					Position = 1,
				}
			}));
			if(myFlashType != TheFlashType.WholePanel && Clock_Position != MyObjectPosition.PriceAxis)// text disappears under Whole Panel flashing so render it elsewhere
			{
			textFormat2.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;// moves text to far right of panel
            // Draw text outline
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx != 0 || dy != 0) // Skip the center point
                    {
                        var outlinePosition = new SharpDX.Vector2(textPoint1.X + dx, textPoint1.Y + dy);
                        RenderTarget.DrawTextLayout(outlinePosition, textLayout1, outlineBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                        outlinePosition = new SharpDX.Vector2(textPoint2.X + dx, textPoint2.Y + dy);
						RenderTarget.DrawTextLayout(outlinePosition, textLayout2, counteroutlineBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                    }
                }
            }
            // Draw the main text
            RenderTarget.DrawTextLayout(textPoint1, textLayout1, dxBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);	
			RenderTarget.DrawTextLayout(textPoint2, textLayout2, dxBrushCounter, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
			}		
			#endregion
			
			#region FlashTypes
			
			#region Render Flash Targets
			if(myFlashType != TheFlashType.None)
			{
				if(Trigger > 0)
				{
						if(FlashSwitch() > 0)
				{					
					if(myFlashType == TheFlashType.WholePanel)
					{						
						SharpDX.RectangleF rect = new SharpDX.RectangleF(startPoint.X, startPoint.Y, endPoint.X - startPoint.X, endPoint.Y - startPoint.Y);
						RenderTarget.FillRectangle(rect, linearGradientBrush);
					}
				 else if(myFlashType == TheFlashType.ClockFaceOnly && Clock_Position != MyObjectPosition.PriceAxis)
					{						
						SharpDX.Vector2 lowerTextPoint = new SharpDX.Vector2(ChartPanel.X + 5, ChartPanel.Y + (ChartPanel.H - textLayout1.Metrics.Height));
						SharpDX.RectangleF rect = new SharpDX.RectangleF(textPoint1.X, textPoint1.Y, textLayout1.Metrics.Width, textLayout1.Metrics.Height);
						
						RenderTarget.FillRectangle(rect, linearGradientBrush);
                            // Draw text outline
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    if (dx != 0 || dy != 0) // Skip the center point
                                    {
                                        var outlinePosition = new SharpDX.Vector2(textPoint1.X + dx, textPoint1.Y + dy);
                                        RenderTarget.DrawTextLayout(outlinePosition, textLayout1, outlineBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                                        outlinePosition = new SharpDX.Vector2(textPoint2.X + dx, textPoint2.Y + dy);
                                        RenderTarget.DrawTextLayout(outlinePosition, textLayout2, counteroutlineBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                                    }
                                }
                            }
						// Draw the main text
                        RenderTarget.DrawTextLayout(textPoint1, textLayout1, dxBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
						RenderTarget.DrawTextLayout(textPoint2, textLayout2, dxBrushCounter, SharpDX.Direct2D1.DrawTextOptions.NoSnap);

					}///Flash Type Clock Face Only
					else if(myFlashType == TheFlashType.TextOnly && Clock_Position != MyObjectPosition.PriceAxis)
					{
                        // Draw text outline
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx != 0 || dy != 0) // Skip the center point
                                {
                                    var outlinePosition = new SharpDX.Vector2(textPoint1.X + dx, textPoint1.Y + dy);
                                    RenderTarget.DrawTextLayout(outlinePosition, textLayout1, outlineBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                                    outlinePosition = new SharpDX.Vector2(textPoint2.X + dx, textPoint2.Y + dy);
                                    RenderTarget.DrawTextLayout(outlinePosition, textLayout2, counteroutlineBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                                }
                            }
                        }
						// Draw the main text
                        RenderTarget.DrawTextLayout(textPoint1, textLayout1, linearGradientBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
						RenderTarget.DrawTextLayout(textPoint2, textLayout2, linearGradientBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
	
					}// Text only FlashType
				}// Flash Switcher
			}// Trigger > 0
		}//Flash Type != none
			#endregion
							
			#region Whole Panel Flashing - Render Text
				
			if(myFlashType == TheFlashType.WholePanel && Clock_Position != MyObjectPosition.PriceAxis)
				{
			textFormat2.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
            // Draw text outline
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx != 0 || dy != 0) // Skip the center point
                    {
                        var outlinePosition = new SharpDX.Vector2(textPoint1.X + dx, textPoint1.Y + dy);
                        RenderTarget.DrawTextLayout(outlinePosition, textLayout1, outlineBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                        outlinePosition = new SharpDX.Vector2(textPoint2.X + dx, textPoint2.Y + dy);
                        RenderTarget.DrawTextLayout(outlinePosition, textLayout2, counteroutlineBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                    }
                }
            }
            // Draw the main text
            RenderTarget.DrawTextLayout(textPoint1, textLayout1, dxBrush, SharpDX.Direct2D1.DrawTextOptions.NoSnap);	
			RenderTarget.DrawTextLayout(textPoint2, textLayout2, dxBrushCounter, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
				}
			#endregion
				
			#endregion
						
			#region Dispose DX
		
			linearGradientBrush.Dispose();
			dxBrush.Dispose();
			dxBrushCounter.Dispose();
			outlineBrush.Dispose();
			counteroutlineBrush.Dispose();
			textFormat1.Dispose();
			textLayout1.Dispose();
			textLayout2.Dispose();
				#endregion
				
			}
	}
		#endregion
		
		#region FlashSwitcher
		private int FlashSwitch()
		{
 			flashon = 0;
				if (Now.Second % 2 < 1)//not now
				flashon= 1;
				return flashon;
		}
		#endregion
		
		#region Button Click Events for You tube guides
		
		private void OnButtonClick(object sender, RoutedEventArgs rea)
		{
			System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
			
			#region YT videos
			
			if (ShowGuide)
			{
				if (button == youtubeButton )
				{
					System.Diagnostics.Process.Start(youtube);
					return;
				}
				if (button == youtubeButton1)
				{
					System.Diagnostics.Process.Start(youtube1);
					return;
				}
			}	
			#endregion
		}
		
		#endregion
		
		#region Timer & Connection Internals
		
		#region DateTime Now
		private DateTime Now
		{
			get
			{
				now = (Cbi.Connection.PlaybackConnection != null ? Cbi.Connection.PlaybackConnection.Now : Core.Globals.Now);
				if (now.Millisecond > 0)
					now = Core.Globals.MinDate.AddSeconds((long)Math.Floor(now.Subtract(Core.Globals.MinDate).TotalSeconds));
		
				return now;
			}
		}
		#endregion
			
		#region OnTimerTick

		private void OnTimerTick(object sender, EventArgs e)
		{
			this.OnBarUpdate();
			ForceRefresh();

			if (DisplayTime())
			{
				if (timer != null && !timer.IsEnabled)
					timer.IsEnabled = true;

				if (connected)
				{				
					if (SessionIterator.IsInSession(Now, false, true))
					{
						if (hasRealTimeData)
						{

			
							RemoveDrawObject("NinjaScriptInfo");
						}
						else
							Draw.TextFixed(this, "NinjaScriptInfo", NinjaTrader.Custom.Resource.BarTimerWaitingOnDataError, TextPosition.BottomRight);
					}
				}///if connected
				else
				{	
					Draw.TextFixed(this, "NinjaScriptInfo", NinjaTrader.Custom.Resource.BarTimerDisconnectedError, TextPosition.BottomRight);

					if (timer != null)
						timer.IsEnabled = false;
				}
					ForceRefresh();		
			}///Display Time		
		}
			#endregion
		
		#region  OnConnected Status Update
		protected override void OnConnectionStatusUpdate(ConnectionStatusEventArgs connectionStatusUpdate)
		{
			if (connectionStatusUpdate.PriceStatus == ConnectionStatus.Connected
				&& connectionStatusUpdate.Connection.InstrumentTypes.Contains(Instrument.MasterInstrument.InstrumentType)
			&& Bars.BarsType.IsTimeBased 
			&& Bars.BarsType.IsIntraday)
			{
				connected = true;

				if (DisplayTime() && timer == null)
				{
					ChartControl.Dispatcher.InvokeAsync(() =>
					{
						timer = new System.Windows.Threading.DispatcherTimer { Interval = new TimeSpan(0, 0, 1), IsEnabled = false };
						timer.Tick += OnTimerTick;
						timer.Stop();
                        SynchronizeTimerAtFullSecond(timer);
                    });
				}

			}
			else if (connectionStatusUpdate.PriceStatus == ConnectionStatus.Disconnected)
			connected = false;
		}
		#endregion
		
		#region Synchronize
		
		protected void SynchronizeTimerAtFullSecond(System.Windows.Threading.DispatcherTimer timer)
        {
            if (timer != null)
            {	
				DispatcherTimer dispatcherTimer;
				Timer startTimer;

                dispatcherTimer = new DispatcherTimer();
                dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
                dispatcherTimer.Tick += DispatcherTimer_Tick;

				// Specify the target start time Now +1 s and no milliseconds
				DateTime targetTime = DateTime.Now;
				
				if (timer.IsEnabled)
                    targetTime = targetTime.AddSeconds(5);
				else
                    targetTime = targetTime.AddSeconds(1);
				
				targetTime = targetTime.AddMilliseconds(-targetTime.Millisecond);

                // Calculate the interval to wait until the target time
                TimeSpan waitTime = targetTime - DateTime.Now;

                if (waitTime > TimeSpan.Zero)
                {
                    // Set up the timer to start the DispatcherTimer at the specified time
                    startTimer = new Timer(waitTime.TotalMilliseconds);
                    startTimer.Elapsed += StartTimer_Elapsed;
                    startTimer.AutoReset = false; // Only fire the event once
                    startTimer.Start();
                }
                else
                {
                  //  Console.WriteLine("Target time is in the past. Please specify a future time.");
                }

                void StartTimer_Elapsed(object sender, ElapsedEventArgs e)
                {
                    // Start the DispatcherTimer
					timer.IsEnabled = true;
					timer.Start();
                    dispatcherTimer.Start();
                    Console.WriteLine("DispatcherTimer started at: " + DateTime.Now);
                };
				void DispatcherTimer_Tick(object sender, EventArgs e)
				{
					//Console.WriteLine("DispatcherTimer tick at: " + DateTime.Now);
				};
            }
        }
		#endregion
		
		#region DisplayTime Bool
		private bool DisplayTime()
		{
			return ChartControl != null
					&& Bars != null
					&& Bars.Instrument.MarketData != null;
		}
		#endregion
		
		#region Session Iterator
		private SessionIterator SessionIterator
		{
			get
			{
				if (sessionIterator == null)
					sessionIterator = new SessionIterator(Bars);
				return sessionIterator;
			}
		}
#endregion
		
		#endregion
		
		#region Alternative Clock method

		private DateTime AlternativeClock()
		{
								if(altTimeZone == MyTimeZones.EST)
								 altClockZone = "Eastern Standard Time";
							if(altTimeZone == MyTimeZones.EDT)
								 altClockZone = "US Eastern Standard Time";// allegedly will auto change for Daylight Savings - untested
						    if(altTimeZone == MyTimeZones.Sydney)
								 altClockZone = "AUS Eastern Standard Time";
							if(altTimeZone == MyTimeZones.CentralEurope)
								 altClockZone = "Central Europe Standard Time";
							if(altTimeZone == MyTimeZones.Beijing)
								 altClockZone = "China Standard Time";
 							if(altTimeZone == MyTimeZones.NZ)
								 altClockZone = "New Zealand Standard Time";
							if(altTimeZone == MyTimeZones.Singapore)
								 altClockZone = "Singapore Standard Time";
							if(altTimeZone == MyTimeZones.Tokyo)
								 altClockZone = "Tokyo Standard Time";
							if(altTimeZone == MyTimeZones.London)
								 altClockZone = "GMT Standard Time";
							if(altTimeZone == MyTimeZones.HongKong)
								 altClockZone = "Hong Kong Standard Time";
							
							
///nb dont'forget to change Enum name "spare" to your new zone abbreviation here AND in the Enum itself above		
							if(altTimeZone == MyTimeZones.Spare)
								 altClockZone = "Spare Time";
			alternateZone = TimeZoneInfo.FindSystemTimeZoneById(altClockZone);//"GMT Standard Time");										
			Alternative_Clock_Now = TimeZoneInfo.ConvertTime(Now, TimeZoneInfo.Local,alternateZone);
		//Draw.TextFixed(this,";;DKDK","Alt clock = " +Alternative_Clock_Now.ToString(),TextPosition.BottomLeft);	
							return Alternative_Clock_Now;
		}
		#endregion
	
		#region Show available TimeZones Method - only use if you need to find another alternative time zone
		private void ShowTimeZones()
{
	   	ReadOnlyCollection<TimeZoneInfo> timeZones = TimeZoneInfo.GetSystemTimeZones(); 
      		foreach (TimeZoneInfo timeZone in timeZones)
   		 Print(timeZone.StandardName+ Environment.NewLine);		
}	
#endregion
	
		#region Properties

		#region Counter Setup
		
		[Browsable(false)]
		[XmlIgnore]
		public Series<double> DummyPlot
		{
			get { return Values[0]; }
		}	
	
		[RefreshProperties(RefreshProperties.All)]
		[NinjaScriptProperty]
		[Display(ResourceType = typeof (Custom.Resource),Name = "SHOW COUNTER SETUP",GroupName = "Input",Order = 1)]
		public bool ShowCounterOptions
		{ get; set; }


				
		[RefreshProperties(RefreshProperties.All)]// only used for Type Converter		
		[NinjaScriptProperty]
		[Display(ResourceType = typeof (Custom.Resource), Name = "Count Up", Order = 2,
		Description="", GroupName = "Input")]
		public bool CountUp
				{
		get { return countUp;} 
			set{ 
				countUp = value;
				countDown = false;
			if(!value)
			{
				countUp = false;
				countDown = true;
			}
			}
		}	
					
		[RefreshProperties(RefreshProperties.All)]// only used for Type Converter
		[NinjaScriptProperty]
		[Display(ResourceType = typeof (Custom.Resource), Name = "Count Down", Order = 3,
		Description="", GroupName = "Input")]
		public bool CountDown
				{
		get { return countDown;} 
			set{ 
				countDown = value;
				countUp = false;
			if(!value)
			{
				countDown = false;
				countUp = true;
			}
			}
		}
		
		[Display(Name="Display Type", GroupName="Input",Description="", Order=4)]		
		public TheDisplayType MyDisplayType
		{
			get { return myDisplayType; }
			set { myDisplayType = value; }
		}	
		
		[Display(Name="Alt Time Zones", Description="", GroupName="Input", Order=5)]		
		public MyTimeZones AltTimeZone
		{
			get { return altTimeZone; }
			set { altTimeZone = value; }
		}
		
		[Display(Name = "Counter location", GroupName = "Input", 
		Description="Position can be moved around screen by Ctrl + Zero,Ctrl + 1 to reverse",Order = 6)]
		public MyObjectPosition Clock_Position
		{
			get { return myClockPosition; }
		    set { myClockPosition = value; }
		}	
				
		[XmlIgnore]
		[Display(Name = "Clock Text Color", GroupName = "Input", Order = 7)]	
		public System.Windows.Media.Brush TextBrush1
		{ get; set; } 
		
		[Browsable(false)]
		public string TextBrush1Serialize 
		{
		  get { return Serialize.BrushToString(TextBrush1); }
		  set { TextBrush1 = Serialize.StringToBrush(value); } 
		}

		[XmlIgnore]
		[Display(Name = "Clock Outline Text Colour", GroupName = "Input", Order = 8)]
		public System.Windows.Media.Brush ClockBrushOutline
		{ get; set; }

		[Browsable(false)]
		public string ClockBrushOutlineSerialize
		{
			get { return Serialize.BrushToString(ClockBrushOutline); }
			set { ClockBrushOutline = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(Name = "Counter Text Color", GroupName = "Input", Order = 9)]	
		public System.Windows.Media.Brush TextBrush2
		{ get; set; } 
		
		[Browsable(false)]
		public string TextBrush2Serialize 
		{
		  get { return Serialize.BrushToString(TextBrush2); }
		  set { TextBrush2 = Serialize.StringToBrush(value); } 
		}
		
		[XmlIgnore]
		[Display(Name = "Counter Outline Text Colour", GroupName = "Input", Order = 10)]
		public System.Windows.Media.Brush CounterBrushOutline
		{ get; set; }

		[Browsable(false)]
		public string CounterBrushOutlineSerialize
		{
			get { return Serialize.BrushToString(CounterBrushOutline); }
			set { CounterBrushOutline = Serialize.StringToBrush(value); }
		}
		
		[Display(GroupName = "Input", Description="Counter Font.",Order = 11)]
		public SimpleFont TextFont
		{
			get { return textFont; }
			set { textFont = value; }
		}

    #endregion
		
		#region Levels	
		[RefreshProperties(RefreshProperties.All)]// only used for Type Converter
		[NinjaScriptProperty]
		[Display(ResourceType = typeof (Custom.Resource), Name = "SHOW TRIGGER LEVELS", Order = 12, GroupName = "Input")]
		public bool ShowTriggerLevels
			{
		get { return showTriggerLevels;} 
			set{ 
				showTriggerLevels = value;
			if(!value)
			{
				showTriggerLevels = false;
			}
			}
		}
			
		[RefreshProperties(RefreshProperties.All)]
		[NinjaScriptProperty]
		[Display(ResourceType = typeof (Custom.Resource), Name = "Use Percentages", Order = 13, GroupName = "Input")]
		public bool ShowPercent
		{
		get { return showPercent;} 
			set{ 
				showPercent = value;
			if(!value)
			{
				showPercent = false;
			}
			}
		}
					
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(Name = "Time Based Chart Levels",Order=14 ,GroupName = "Input",
		Description=" Flashing would start in seconds eg 10 would initiate 10s from end in CountDown and after first 10s in CountUp")]
		public int TimeBasedLevel
		{ get; set; }
		
		[Range (1, int.MaxValue)]
		[Display(ResourceType = typeof (Custom.Resource), Name = "Non Time Based Chart Levels",// replacing volume bum level
		Description="Flashing Level eg 15 or 15%", Order = 16, GroupName = "Input")]
		public int NonTimeBasedLevel
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(ResourceType = typeof (Custom.Resource), Name = "Show Max Range",
		Description="Only Highest Range level shown",Order = 18, GroupName = "Input")]
		public bool ShowMaxRange
		{ get; set; }
		
		#endregion

 	    #region Flashing
		[RefreshProperties(RefreshProperties.All)]
		[NinjaScriptProperty]
		[Display(ResourceType = typeof (Custom.Resource), Name = "SHOW FLASHING OPTIONS",
		 Order = 19, GroupName = "Input")]
		public bool ShowFlashOptions
		{ get; set; }
		

    	[Display(Name="Flash Type", Description="",GroupName = "Input", Order=20)]
		public TheFlashType MyFlashType
		{
			get { return myFlashType; }
			set { myFlashType = value; }
		}
				
		[XmlIgnore]
		[Display(GroupName = "Input",
		 Name = "Gradient Base",Description = "Mix Colour for Gradient.",Order = 21)]
		public SolidColorBrush Gradient_Mixer_Colour1 { get; set; } 
		
		[Browsable(false)]
		public string gradient_Mixer_Colour1_Serialize
    {
			get { return Serialize.BrushToString(Gradient_Mixer_Colour1); }
			set {if (Gradient_Mixer_Colour1 != null)
						{
							if (Gradient_Mixer_Colour1.IsFrozen)
								Gradient_Mixer_Colour1 = Gradient_Mixer_Colour1.Clone();
								Gradient_Mixer_Colour1.Freeze();
						}
				Gradient_Mixer_Colour1 = (SolidColorBrush)Serialize.StringToBrush(value);
			  }
		}
		
		[XmlIgnore]
		[Display(GroupName = "Input",  Name = "Gradient Mix",Order = 22,
		Description = "Mix Colour for Gradient.")]
		public SolidColorBrush Gradient_Mixer_Colour {get; set; }
		
		[Browsable(false)]
		public string gradient_Mixer_Colour_Serialize
		{
			get { return Serialize.BrushToString(Gradient_Mixer_Colour); }
			set {if (Gradient_Mixer_Colour != null)
						{
							if (Gradient_Mixer_Colour.IsFrozen)
								Gradient_Mixer_Colour = Gradient_Mixer_Colour.Clone();
								Gradient_Mixer_Colour.Freeze();
						}
				Gradient_Mixer_Colour = (SolidColorBrush)Serialize.StringToBrush(value);
			}
		}
		
			[XmlIgnore()]
		[Display(Description = "Base Plot Colour",
		ResourceType = typeof(Custom.Resource), Name = "Price Axis Background", GroupName = "Input", Order = 23)]
		public Brush BasePlotColor
		{ get; set; }
		
		[Browsable(false)]
		public string BasePlotColorSerialize
		{
			get { return Serialize.BrushToString(BasePlotColor); }
			set { BasePlotColor = Serialize.StringToBrush(value); }
		}	
		
					[XmlIgnore()]
		[Display(Description = "Flash Plot Colour",
		ResourceType = typeof(Custom.Resource), Name = "Price Axis Flash Background", GroupName = "Input", Order = 24)]
		public Brush FlashPlotColor
		{ get; set; }
		
		[Browsable(false)]
		public string FlashPlotColorSerialize
		{
			get { return Serialize.BrushToString(FlashPlotColor); }
			set { FlashPlotColor = Serialize.StringToBrush(value); }
		}	
		#endregion	
					
		#region Sounds	

		[RefreshProperties(RefreshProperties.All)]
		[Display(Name="TURN ON SOUND", Description="Play sounds on first alert.", GroupName="Input", Order=25)]
		public bool SoundsOn
		{ get; set; }				
										
		[Display(Name="Alert Sound file", Description="Enter sound file path/name. Alert2 is default sound", GroupName="Input", Order=26)]
		[PropertyEditor("NinjaTrader.Gui.Tools.FilePathPicker", Filter="Wav Files (*.wav)|*.wav")]
		public string Sound_File
		{ get; set; }
		
		[XmlIgnore]
		[Display(Name = "Alternative Text Colour", GroupName = "Input", Order = 27)]	
		public System.Windows.Media.Brush TextBrushForSound
		{ get; set; } 
		
		[Browsable(false)]
		public string TextBrushForSoundSerialize 
		{
		  get { return Serialize.BrushToString(TextBrushForSound); }
		  set { TextBrushForSound = Serialize.StringToBrush(value); } 
		}	
		
		[Display(Name="Alter Text Colour", Description="Change Text Colour\nTo Help identify Chart with sound", GroupName="Input", Order=28)]
		public bool AlterFontColour
		{ get; set; }
		
		#endregion
		
		#region Guides
				
		[RefreshProperties(RefreshProperties.All)]
		[NinjaScriptProperty]
		[Display(ResourceType = typeof (Custom.Resource), Name = "SHOW GUIDES & AIDS",Order = 29,
		 GroupName = "Input")]
		public bool ShowGuide
		{ get; set; }	
		
					
		[NinjaScriptProperty]
		[Display(Name="Print available time zones", 
		Description = "Make sure NinjaTrader Output is open", GroupName="Input",Order=30)]
		public bool PrintTimeZones
		{ get; set; }
		
		[NinjaScriptProperty]
		[ReadOnly(true)]
		[Display(Name = "------", GroupName = "Input", Order = 29)]
		public string Author
		{
		 	get{return author;} 
			set{author = (value);} 
		}
		#endregion
			
		#endregion
			
	}
	#region TypeConverter to hide properties in the PropertyGrid
	public class BarTimerHybridv6Converter : IndicatorBaseConverter
    {
        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object component, Attribute[] attrs)
        {
            BarTimerHybridv6 indicator = component as BarTimerHybridv6;

            PropertyDescriptorCollection propertyDescriptorCollection = 
			base.GetPropertiesSupported(context) ? base.GetProperties(context, component, attrs): 
				TypeDescriptor.GetProperties(component, attrs);

            if (indicator == null || propertyDescriptorCollection == null)
                return propertyDescriptorCollection;

			#region Number Atrributes - working

			
			 PropertyDescriptor timebasedlevel 		= propertyDescriptorCollection ["TimeBasedLevel"];
			 PropertyDescriptor UseNumLevels 		= propertyDescriptorCollection ["ShowTriggerLevels"];
			 PropertyDescriptor nontimebasedlevel	= propertyDescriptorCollection ["NonTimeBasedLevel"];
			 PropertyDescriptor showmaxrange 		= propertyDescriptorCollection ["ShowMaxRange"];
			 PropertyDescriptor showpercent 		= propertyDescriptorCollection ["ShowPercent"];
			 PropertyDescriptor showflashoptions 	= propertyDescriptorCollection ["ShowFlashOptions"];
			 PropertyDescriptor myflashtype		  	= propertyDescriptorCollection ["MyFlashType"];
			 PropertyDescriptor gradientbase 		= propertyDescriptorCollection ["Gradient_Mixer_Colour1"];
			 PropertyDescriptor gradientmix 		= propertyDescriptorCollection ["Gradient_Mixer_Colour"];
			 PropertyDescriptor maxbarslookback 	= propertyDescriptorCollection ["MaximumBarsLookBack"];
			 PropertyDescriptor flashplotcolor		= propertyDescriptorCollection ["FlashPlotColor"];
			 PropertyDescriptor baseplotcolor 		= propertyDescriptorCollection ["BasePlotColor"];


  			propertyDescriptorCollection.Remove(showpercent);
		    propertyDescriptorCollection.Remove(showmaxrange);
			propertyDescriptorCollection.Remove(timebasedlevel);
			propertyDescriptorCollection.Remove(nontimebasedlevel);
			propertyDescriptorCollection.Remove(maxbarslookback);
			
			if (indicator.ShowTriggerLevels)
            {
				propertyDescriptorCollection.Add(showpercent);
				propertyDescriptorCollection.Add(timebasedlevel);
				propertyDescriptorCollection.Add(nontimebasedlevel);
				propertyDescriptorCollection.Add(showmaxrange);
			
			}	
			#endregion
			


			#region Flash Atrributes
		
			
			propertyDescriptorCollection.Remove(gradientbase);
			propertyDescriptorCollection.Remove(gradientmix);
			propertyDescriptorCollection.Remove(myflashtype);
			propertyDescriptorCollection.Remove(baseplotcolor);
			propertyDescriptorCollection.Remove(flashplotcolor);

	
					if (indicator.ShowFlashOptions)
	            {
					propertyDescriptorCollection.Add(gradientbase);
					propertyDescriptorCollection.Add(myflashtype);
					propertyDescriptorCollection.Add(gradientmix);
					propertyDescriptorCollection.Add(baseplotcolor);
					propertyDescriptorCollection.Add(flashplotcolor);
	            }
				#endregion
						
			#region Show Counter Setup Attributes	
				
			PropertyDescriptor showcounteroptions 		= propertyDescriptorCollection["ShowCounterOptions"];
			PropertyDescriptor author			 		= propertyDescriptorCollection["Author"];
			//Counter Setup
			PropertyDescriptor countdown 				= propertyDescriptorCollection["CountDown"];
			PropertyDescriptor countup 					= propertyDescriptorCollection["CountUp"];	
			PropertyDescriptor countertextcolor 		= propertyDescriptorCollection["CounterTextColor"];
			PropertyDescriptor displaytype 				= propertyDescriptorCollection["MyDisplayType"];
			PropertyDescriptor alttimezones 			= propertyDescriptorCollection["AltTimeZone"];
			PropertyDescriptor countertextcolour1 		= propertyDescriptorCollection["TextBrush1"];
			PropertyDescriptor countertextcolour2 		= propertyDescriptorCollection["TextBrush2"];
			PropertyDescriptor clockbrushoutline 		= propertyDescriptorCollection["ClockBrushOutline"];
			PropertyDescriptor counterbrushoutline 		= propertyDescriptorCollection["CounterBrushOutline"];
			PropertyDescriptor clockposition			= propertyDescriptorCollection["Clock_Position"];
			PropertyDescriptor textfont 				= propertyDescriptorCollection["TextFont"];
				
			//Sound options
			PropertyDescriptor soundson					= propertyDescriptorCollection["SoundsOn"];// imp Property must be set to RefreshALL
			PropertyDescriptor Sound_File				= propertyDescriptorCollection["Sound_File"];
			PropertyDescriptor alterfontcolour			= propertyDescriptorCollection["AlterFontColour"];
			PropertyDescriptor textbrushforsound		= propertyDescriptorCollection["TextBrushForSound"];		
				
			// Guide
			PropertyDescriptor showguide 				= propertyDescriptorCollection["ShowGuide"];	
			PropertyDescriptor printtimezones 			= propertyDescriptorCollection["PrintTimeZones"];
			propertyDescriptorCollection.Remove(printtimezones);
			
			propertyDescriptorCollection.Remove(countdown);
			propertyDescriptorCollection.Remove(countup);
			
			propertyDescriptorCollection.Remove(alttimezones);
			propertyDescriptorCollection.Remove(displaytype);
			propertyDescriptorCollection.Remove(countertextcolour1);
			propertyDescriptorCollection.Remove(countertextcolour2);
				
			propertyDescriptorCollection.Remove(clockbrushoutline);
			propertyDescriptorCollection.Remove(counterbrushoutline);
	
			propertyDescriptorCollection.Remove(clockposition);
			propertyDescriptorCollection.Remove(textfont);
//remove sound
			propertyDescriptorCollection.Remove(Sound_File);
			propertyDescriptorCollection.Remove(alterfontcolour);
			propertyDescriptorCollection.Remove(textbrushforsound);
				
					if(indicator.ShowCounterOptions)
				{			           
					  propertyDescriptorCollection.Add(countdown);
					  propertyDescriptorCollection.Add(countup);
					  propertyDescriptorCollection.Add(alttimezones);
					  propertyDescriptorCollection.Add(displaytype);
					  propertyDescriptorCollection.Add(countertextcolour1);
					  propertyDescriptorCollection.Add(countertextcolour2);
					  propertyDescriptorCollection.Add(clockbrushoutline);
					  propertyDescriptorCollection.Add(counterbrushoutline);
					  propertyDescriptorCollection.Add(clockposition);
					  propertyDescriptorCollection.Add(textfont);
				}
				#endregion									        
		
		if(indicator.SoundsOn)
			
				{		
					 propertyDescriptorCollection.Add(Sound_File); 
					 propertyDescriptorCollection.Add(textbrushforsound); 
				     propertyDescriptorCollection.Add(alterfontcolour); 
				}
		
		if (indicator.ShowGuide)
            {
               propertyDescriptorCollection.Add(printtimezones);
            }

			#region remove property by groupname
			foreach(PropertyDescriptor pd in propertyDescriptorCollection)
			{
				if(pd.IsBrowsable)
				{
					foreach(Attribute attrib in pd.Attributes)
					{
						if(attrib is DisplayAttribute)
						{
							DisplayAttribute display = (DisplayAttribute)attrib as DisplayAttribute;
							
							string groupName = display.GetValueSafe(NinjaTrader.Gui.DisplayAttributeExtensions.DisplayAttributeValue.GroupName);
							
							if(!groupName.IsNullOrEmpty())
							{
								if( groupName == "Plots")//|| groupName == "Setup"groupName == "Visual" ||
								{
									propertyDescriptorCollection.Remove(pd);
								}
							
							}
						}
					}
				}
			}
			#endregion
		
            return propertyDescriptorCollection;
        }

        public override bool GetPropertiesSupported(ITypeDescriptorContext context)
       { return true; }
    }
	#endregion
	
	
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Mindset.BarTimerHybridv6[] cacheBarTimerHybridv6;
		public Mindset.BarTimerHybridv6 BarTimerHybridv6(bool showCounterOptions, bool countUp, bool countDown, bool showTriggerLevels, bool showPercent, int timeBasedLevel, bool showMaxRange, bool showFlashOptions, bool showGuide, bool printTimeZones, string author)
		{
			return BarTimerHybridv6(Input, showCounterOptions, countUp, countDown, showTriggerLevels, showPercent, timeBasedLevel, showMaxRange, showFlashOptions, showGuide, printTimeZones, author);
		}

		public Mindset.BarTimerHybridv6 BarTimerHybridv6(ISeries<double> input, bool showCounterOptions, bool countUp, bool countDown, bool showTriggerLevels, bool showPercent, int timeBasedLevel, bool showMaxRange, bool showFlashOptions, bool showGuide, bool printTimeZones, string author)
		{
			if (cacheBarTimerHybridv6 != null)
				for (int idx = 0; idx < cacheBarTimerHybridv6.Length; idx++)
					if (cacheBarTimerHybridv6[idx] != null && cacheBarTimerHybridv6[idx].ShowCounterOptions == showCounterOptions && cacheBarTimerHybridv6[idx].CountUp == countUp && cacheBarTimerHybridv6[idx].CountDown == countDown && cacheBarTimerHybridv6[idx].ShowTriggerLevels == showTriggerLevels && cacheBarTimerHybridv6[idx].ShowPercent == showPercent && cacheBarTimerHybridv6[idx].TimeBasedLevel == timeBasedLevel && cacheBarTimerHybridv6[idx].ShowMaxRange == showMaxRange && cacheBarTimerHybridv6[idx].ShowFlashOptions == showFlashOptions && cacheBarTimerHybridv6[idx].ShowGuide == showGuide && cacheBarTimerHybridv6[idx].PrintTimeZones == printTimeZones && cacheBarTimerHybridv6[idx].Author == author && cacheBarTimerHybridv6[idx].EqualsInput(input))
						return cacheBarTimerHybridv6[idx];
			return CacheIndicator<Mindset.BarTimerHybridv6>(new Mindset.BarTimerHybridv6(){ ShowCounterOptions = showCounterOptions, CountUp = countUp, CountDown = countDown, ShowTriggerLevels = showTriggerLevels, ShowPercent = showPercent, TimeBasedLevel = timeBasedLevel, ShowMaxRange = showMaxRange, ShowFlashOptions = showFlashOptions, ShowGuide = showGuide, PrintTimeZones = printTimeZones, Author = author }, input, ref cacheBarTimerHybridv6);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Mindset.BarTimerHybridv6 BarTimerHybridv6(bool showCounterOptions, bool countUp, bool countDown, bool showTriggerLevels, bool showPercent, int timeBasedLevel, bool showMaxRange, bool showFlashOptions, bool showGuide, bool printTimeZones, string author)
		{
			return indicator.BarTimerHybridv6(Input, showCounterOptions, countUp, countDown, showTriggerLevels, showPercent, timeBasedLevel, showMaxRange, showFlashOptions, showGuide, printTimeZones, author);
		}

		public Indicators.Mindset.BarTimerHybridv6 BarTimerHybridv6(ISeries<double> input , bool showCounterOptions, bool countUp, bool countDown, bool showTriggerLevels, bool showPercent, int timeBasedLevel, bool showMaxRange, bool showFlashOptions, bool showGuide, bool printTimeZones, string author)
		{
			return indicator.BarTimerHybridv6(input, showCounterOptions, countUp, countDown, showTriggerLevels, showPercent, timeBasedLevel, showMaxRange, showFlashOptions, showGuide, printTimeZones, author);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Mindset.BarTimerHybridv6 BarTimerHybridv6(bool showCounterOptions, bool countUp, bool countDown, bool showTriggerLevels, bool showPercent, int timeBasedLevel, bool showMaxRange, bool showFlashOptions, bool showGuide, bool printTimeZones, string author)
		{
			return indicator.BarTimerHybridv6(Input, showCounterOptions, countUp, countDown, showTriggerLevels, showPercent, timeBasedLevel, showMaxRange, showFlashOptions, showGuide, printTimeZones, author);
		}

		public Indicators.Mindset.BarTimerHybridv6 BarTimerHybridv6(ISeries<double> input , bool showCounterOptions, bool countUp, bool countDown, bool showTriggerLevels, bool showPercent, int timeBasedLevel, bool showMaxRange, bool showFlashOptions, bool showGuide, bool printTimeZones, string author)
		{
			return indicator.BarTimerHybridv6(input, showCounterOptions, countUp, countDown, showTriggerLevels, showPercent, timeBasedLevel, showMaxRange, showFlashOptions, showGuide, printTimeZones, author);
		}
	}
}

#endregion
