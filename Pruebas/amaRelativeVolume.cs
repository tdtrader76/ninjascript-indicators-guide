//+----------------------------------------------------------------------------------------------+
//| Copyright © <2017>  <LizardIndicators.com - powered by AlderLab UG>
//
//| This program is free software: you can redistribute it and/or modify
//| it under the terms of the GNU General Public License as published by
//| the Free Software Foundation, either version 3 of the License, or
//| any later version.
//|
//| This program is distributed in the hope that it will be useful,
//| but WITHOUT ANY WARRANTY; without even the implied warranty of
//| MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//| GNU General Public License for more details.
//|
//| By installing this software you confirm acceptance of the GNU
//| General Public License terms. You may find a copy of the license
//| here; http://www.gnu.org/licenses/
//+----------------------------------------------------------------------------------------------+

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
using NinjaTrader.Core;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators.LizardIndicators;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
#endregion

// This namespace holds indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators.LizardIndicators
{
	/// <summary>
	/// The Relative Volume indicator compares the volume of the current trading day to the averaged volume of all trading days of the selected reference period.
	///	The indicator can be used on daily, weekly or minute charts. On minute charts the volume of each bar will be compared to the average volume of the corresponding bars
	/// that were printing at the same time of the day. The comparison can be made for all days or just for the same day of the week.
	/// </summary>
	[Gui.CategoryOrder("Algorithmic Options", 0)]
	[Gui.CategoryOrder("Input Parameters", 10)]
	[Gui.CategoryOrder("Display Options", 20)]
	[Gui.CategoryOrder("Data Series", 30)]
	[Gui.CategoryOrder("Set up", 35)]
	[Gui.CategoryOrder("Visual", 40)]
	[Gui.CategoryOrder("Plot Colors", 50)]
	[Gui.CategoryOrder("Plot Parameters", 55)]
	[Gui.CategoryOrder("Text Boxes", 60)]
	[Gui.CategoryOrder("Estimated Values", 70)]
	[Gui.CategoryOrder("Holidays", 80)]
	[Gui.CategoryOrder("Version", 90)]
	[TypeConverter("NinjaTrader.NinjaScript.Indicators.amaRelativeVolumeTypeConverter")]
	public class amaRelativeVolume : Indicator
	{
		private int								days						= 40;
		private int 							weeks						= 8;
		private int 							upperThreshold				= 120;
		private int 							lowerThreshold				= 80;
		private int								displacement				= 0;
		private int								index1						= 0;
		private int								index2						= 0;
		private int								maxCount					= 0;
		private int								minCount					= 0;
		private int								priorMaxCount				= 0;
		private int								priorMinCount				= 0;
		private int 							sessionBars					= 0;
		private int 							barsAgo						= 0;
		private double							volSum						= 0.0;
		private bool 							showRelativeVolume			= true;
		private bool 							showRatioPlot				= true;
		private bool							showNumericalValue			= true;
		private bool							markEstimatedValues			= false;
		private bool							showDayOfWeek				= true;
		private bool							excludeHolidays				= true;
		private bool							showHolidayDates			= false;
		private bool							dayOfWeek					= true;
		private bool							showPercentage				= true;
		private bool							breakAtEOD					= true;
		private bool							calculateFromPriceData		= true;
		private bool							isIntraday					= true;
		private bool							skipTradingDay				= false;
		private bool							errorMessage				= false;
		private bool							basicError					= false;
		private bool							sundaySessionError			= false;
		private DateTime						sessionDateTmp				= Globals.MinDate;
		private DateTime						cacheSessionEndTmp			= Globals.MinDate;
		private DateTime						lastBarTimeStamp			= Globals.MinDate;
		private DateTime						currentDate					= Globals.MinDate;
		private	DateTime 						exchangeTime 				= Globals.MinDate;
		private DateTime 						priorExchangeTime 			= Globals.MinDate;
		private DateTime 						priorLocalTime 				= Globals.MinDate;
		private DateTime						priorTradingDay				= Globals.MinDate;
		private amaRVCalcMode					calcMode					= amaRVCalcMode.All_Days;
		private double[,,] 						historicalVolume			= null;
		private int[,]							count						= null;
		private SessionIterator					sessionIterator				= null;
		private System.Windows.Media.Brush		highVolumeBrush				= Brushes.Navy;
		private System.Windows.Media.Brush  	mediumVolumeBrush			= Brushes.Lavender;
		private System.Windows.Media.Brush		lowVolumeBrush				= Brushes.Red;
		private System.Windows.Media.Brush  	highRatioBrush				= Brushes.PaleGreen;
		private System.Windows.Media.Brush		lowRatioBrush				= Brushes.Yellow;
		private System.Windows.Media.Brush  	highRatioBrushOpaque		= null;
		private System.Windows.Media.Brush		lowRatioBrushOpaque			= null;
		private System.Windows.Media.Brush  	zerolineBrush				= Brushes.Black;
		private System.Windows.Media.Brush  	textBrush					= Brushes.Black;
		private System.Windows.Media.Brush  	textBoxBrush				= Brushes.Lavender;
		private System.Windows.Media.Brush  	backColorBrush				= Brushes.Silver;
		private System.Windows.Media.Brush		errorBrush					= null;
		private System.Windows.TextAlignment	textAlignmentLeft			= System.Windows.TextAlignment.Left;
		private SimpleFont						textFont					= null;
		private SimpleFont						errorFont					= null;
		private int								textFontSize				= 15;	
		private int								textBoxOpacity				= 70; 
		private string							errorText1					= "The amaRelativeVolume only works on price data.";
		private string							errorText2					= "The amaRelativeVolume indicator can only be used with minute, daily or weekly bars.";
		private string							errorText3					= "Please set the amaRelativeVolume indicator to calculation mode 'All_Days' for use with weekly data.";
		private string							errorText4					= "The amaRelativeVolume cannot be used when the Break EOD data series property is unselected.";
		private string							errorText5					= "The amaRelativeVolume cannot be used with a displacement.";
		private string							errorText6					= "When set to 'All_Days', the amaRelativeVolume indicator can only be used with session templates adapted to the trading hours of the instrument.";
		private string							errorText7					= "amaRelativeVolume: Insufficient historical data. Please increase chart lookback period to show relative volume.";
		private int								plot0Width					= 4;
		private int								plot1Width					= 3;
		private int								lineWidth					= 1;
		private PlotStyle						plot0Style					= PlotStyle.Bar;
		private DashStyleHelper					dash0Style					= DashStyleHelper.Solid;
		private PlotStyle						plot1Style					= PlotStyle.Line;
		private DashStyleHelper					dash1Style					= DashStyleHelper.Solid;
		private DashStyleHelper					lineStyle					= DashStyleHelper.Solid;
		private DateTime						publicHoliday0				= new DateTime (2009,01,19);
		private DateTime						publicHoliday1				= new DateTime (2009,02,16);
		private DateTime						publicHoliday2				= new DateTime (2009,04,10);
		private DateTime						publicHoliday3				= new DateTime (2009,05,25);
		private DateTime						publicHoliday4				= new DateTime (2009,07,03);
		private DateTime						publicHoliday5				= new DateTime (2009,09,07);
		private DateTime						publicHoliday6				= new DateTime (2009,11,26);
		private DateTime						publicHoliday7				= new DateTime (2009,12,24);
		private DateTime						publicHoliday8				= new DateTime (2010,01,18);
		private DateTime						publicHoliday9				= new DateTime (2010,02,15);
		private DateTime						publicHoliday10				= new DateTime (2010,04,02);
		private DateTime						publicHoliday11				= new DateTime (2010,05,31);
		private DateTime						publicHoliday12				= new DateTime (2010,07,05);
		private DateTime						publicHoliday13				= new DateTime (2010,09,06);
		private DateTime						publicHoliday14				= new DateTime (2010,11,25);
		private DateTime						publicHoliday15				= new DateTime (2010,12,24);
		private DateTime						publicHoliday16				= new DateTime (2011,01,17);
		private DateTime						publicHoliday17				= new DateTime (2011,02,21);
		private DateTime						publicHoliday18				= new DateTime (2011,04,22);
		private DateTime						publicHoliday19				= new DateTime (2011,05,30);
		private DateTime						publicHoliday20				= new DateTime (2011,07,04);
		private DateTime						publicHoliday21				= new DateTime (2011,09,05);
		private DateTime						publicHoliday22				= new DateTime (2011,11,24);
		private DateTime						publicHoliday23				= new DateTime (2011,12,27);
		private DateTime						publicHoliday24				= new DateTime (2012,01,16);
		private DateTime						publicHoliday25				= new DateTime (2012,02,20);
		private DateTime						publicHoliday26				= new DateTime (2012,04,06);
		private DateTime						publicHoliday27				= new DateTime (2012,05,28);
		private DateTime						publicHoliday28				= new DateTime (2012,07,04);
		private DateTime						publicHoliday29				= new DateTime (2012,09,03);
		private DateTime						publicHoliday30				= new DateTime (2012,11,22);
		private DateTime						publicHoliday31				= new DateTime (2012,12,24);
		private DateTime						publicHoliday32				= new DateTime (2013,01,21);
		private DateTime						publicHoliday33				= new DateTime (2013,02,18);
		private DateTime						publicHoliday34				= new DateTime (2013,03,29);
		private DateTime						publicHoliday35				= new DateTime (2013,05,27);
		private DateTime						publicHoliday36				= new DateTime (2013,07,04);
		private DateTime						publicHoliday37				= new DateTime (2013,09,02);
		private DateTime						publicHoliday38				= new DateTime (2013,11,28);
		private DateTime						publicHoliday39				= new DateTime (2013,12,24);
		private DateTime						publicHoliday40				= new DateTime (2014,01,20);
		private DateTime						publicHoliday41				= new DateTime (2014,02,17);
		private DateTime						publicHoliday42				= new DateTime (2014,04,18);
		private DateTime						publicHoliday43				= new DateTime (2014,05,26);
		private DateTime						publicHoliday44				= new DateTime (2014,07,04);
		private DateTime						publicHoliday45				= new DateTime (2014,09,01);
		private DateTime						publicHoliday46				= new DateTime (2014,11,27);
		private DateTime						publicHoliday47				= new DateTime (2014,12,24);
		private DateTime						publicHoliday48				= new DateTime (2015,01,19);
		private DateTime						publicHoliday49				= new DateTime (2015,02,16);
		private DateTime						publicHoliday50				= new DateTime (2015,04,03);
		private DateTime						publicHoliday51				= new DateTime (2015,05,25);
		private DateTime						publicHoliday52				= new DateTime (2015,07,03);
		private DateTime						publicHoliday53				= new DateTime (2015,09,07);
		private DateTime						publicHoliday54				= new DateTime (2015,11,26);
		private DateTime						publicHoliday55				= new DateTime (2015,12,24);
		private DateTime						publicHoliday56				= new DateTime (2016,01,18);
		private DateTime						publicHoliday57				= new DateTime (2016,02,15);
		private DateTime						publicHoliday58				= new DateTime (2016,05,30);
		private DateTime						publicHoliday59				= new DateTime (2016,07,04);
		private DateTime						publicHoliday60				= new DateTime (2016,09,05);
		private DateTime						publicHoliday61				= new DateTime (2016,11,24);
		private DateTime						publicHoliday62				= new DateTime (2017,01,16);
		private DateTime						publicHoliday63				= new DateTime (2017,02,20);
		private DateTime						publicHoliday64				= new DateTime (2017,05,29);
		private DateTime						publicHoliday65				= new DateTime (2017,07,04);
		private DateTime						publicHoliday66				= new DateTime (2017,09,04);
		private DateTime						publicHoliday67				= new DateTime (2017,11,23);
		private DateTime						publicHoliday68				= new DateTime (2018,01,15);
		private DateTime						publicHoliday69				= new DateTime (2018,02,19);
		private DateTime						publicHoliday70				= new DateTime (2018,05,28);
		private DateTime						publicHoliday71				= new DateTime (2018,07,04);
		private DateTime						publicHoliday72				= new DateTime (2018,09,03);
		private DateTime						publicHoliday73				= new DateTime (2018,11,22);
		private DateTime						publicHoliday74				= new DateTime (2018,12,24);
		private DateTime						publicHoliday75				= new DateTime (2019,01,21);
		private DateTime						publicHoliday76				= new DateTime (2019,02,18);
		private DateTime						publicHoliday77				= new DateTime (2019,05,27);
		private DateTime						publicHoliday78				= new DateTime (2019,07,04);
		private DateTime						publicHoliday79				= new DateTime (2019,09,02);
		private DateTime						publicHoliday80				= new DateTime (2019,11,28);
		private DateTime						publicHoliday81				= new DateTime (2019,12,24);
		private DateTime[]						publicHoliday				= new DateTime [82];		
		private TimeZoneInfo					globalTimeZone				= Core.Globals.GeneralOptions.TimeZoneInfo;
		private TimeZoneInfo					instrumentTimeZone;
		private string							versionString				= "v 1.3  -  November 15, 2018";
		private Series<double>					averageVolume;			
		private Series<double>					referenceVolume;
		private Series<double>					cumulVolume;
		private Series<double>					cumulReferenceVolume;
			
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= "\r\nThe Relative Volume indicator compares the volume of the current trading day to the averaged volume of all trading days of the selected reference period."
											   + " The indicator can be used on daily, weekly or minute charts. On minute charts the volume of each bar will be compared to the average volume of the corresponding bars"
											   + " that were printing at the same time of the day. The comparison can be made for all days or just for the same day of the week.";
				Name						= "amaRelativeVolume";
				IsSuspendedWhileInactive 	= true;
				ArePlotsConfigurable		= false;
				AreLinesConfigurable		= false;
				DrawOnPricePanel			= false;
				MaximumBarsLookBack			= MaximumBarsLookBack.Infinite;
				AddPlot(new Stroke(Brushes.Lime, 5), PlotStyle.Bar, "Relative Volume");
				AddPlot(new Stroke(Brushes.Red, 2), PlotStyle.Line, "Cumulated Ratio");
				AddLine(new Stroke(Brushes.Purple, 1), 0, "Zero Line");
			}
			else if (State == State.Configure)
			{
				displacement = Displacement;
				Plots[0].Width = plot0Width;
				Plots[1].Width = plot1Width;
				Plots[1].DashStyleHelper = dash1Style;
				Lines[0].Width = lineWidth;
				Lines[0].DashStyleHelper = lineStyle;
				Lines[0].Brush = zerolineBrush;
				highRatioBrushOpaque = highRatioBrush.Clone();
				highRatioBrushOpaque.Opacity = (float) textBoxOpacity/100.0;
				highRatioBrushOpaque.Freeze();
				lowRatioBrushOpaque = lowRatioBrush.Clone();
				lowRatioBrushOpaque.Opacity = (float) textBoxOpacity/100.0;
				lowRatioBrushOpaque.Freeze();
			}	
			else if (State == State.DataLoaded)
			{
				averageVolume = new Series<double>(this, MaximumBarsLookBack.Infinite);
				referenceVolume = new Series<double> (this, MaximumBarsLookBack.Infinite);
				cumulVolume = new Series<double> (this, MaximumBarsLookBack.Infinite);
				cumulReferenceVolume = new Series<double>(this, MaximumBarsLookBack.Infinite);
				if(Input is PriceSeries)
					calculateFromPriceData = true;
				else
					calculateFromPriceData = false;
		    	sessionIterator = new SessionIterator(Bars);
			}	
			else if (State == State.Historical)
			{
				if(calcMode == amaRVCalcMode.Day_Of_Week)
				{
					historicalVolume = new double [7, 1440, weeks];
					count = new int[7, 1440];
					for(int i = 0; i < 7; i++)
						for (int j = 0; j < 1440; j++)
						{	
							for (int k = 0 ; k < weeks; k++)
								historicalVolume[i,j,k] = 0.0;
							count[i,j] = 0;
						}
				}
				else
				{		
					historicalVolume = new double [1, 1440, days];
					count = new int[1, 1440];
					for (int i = 0; i < 1440; i++)
					{	
						for (int k = 0 ; k < days; k++)
							historicalVolume[0, i, k] = 0.0;
						count[0, i] = 0;
					}
				}				
				if(Bars.BarsPeriod.BarsPeriodType == BarsPeriodType.Minute)
					isIntraday = true;
				else
					isIntraday = false;
				if(showRatioPlot && showNumericalValue)
					showPercentage = true;
				else
					showPercentage = false;
				if(Bars.BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && showDayOfWeek)
					dayOfWeek = true;
				else
					dayOfWeek = false;
				instrumentTimeZone = Instrument.MasterInstrument.TradingHours.TimeZoneInfo;
				if (excludeHolidays)
				{
					publicHoliday[0] = publicHoliday0;
					publicHoliday[1] = publicHoliday1;
					publicHoliday[2] = publicHoliday2;
					publicHoliday[3] = publicHoliday3;
					publicHoliday[4] = publicHoliday4;
					publicHoliday[5] = publicHoliday5;
					publicHoliday[6] = publicHoliday6;
					publicHoliday[7] = publicHoliday7;
					publicHoliday[8] = publicHoliday8;
					publicHoliday[9] = publicHoliday9;
					publicHoliday[10] = publicHoliday10;
					publicHoliday[11] = publicHoliday11;
					publicHoliday[12] = publicHoliday12;
					publicHoliday[13] = publicHoliday13;
					publicHoliday[14] = publicHoliday14;
					publicHoliday[15] = publicHoliday15;
					publicHoliday[16] = publicHoliday16;
					publicHoliday[17] = publicHoliday17;
					publicHoliday[18] = publicHoliday18;
					publicHoliday[19] = publicHoliday19;
					publicHoliday[20] = publicHoliday20;
					publicHoliday[21] = publicHoliday21;
					publicHoliday[22] = publicHoliday22;
					publicHoliday[23] = publicHoliday23;
					publicHoliday[24] = publicHoliday24;
					publicHoliday[25] = publicHoliday25;
					publicHoliday[26] = publicHoliday26;
					publicHoliday[27] = publicHoliday27;
					publicHoliday[28] = publicHoliday28;
					publicHoliday[29] = publicHoliday29;
					publicHoliday[30] = publicHoliday30;
					publicHoliday[31] = publicHoliday31;
					publicHoliday[32] = publicHoliday32;
					publicHoliday[33] = publicHoliday33;
					publicHoliday[34] = publicHoliday34;
					publicHoliday[35] = publicHoliday35;
					publicHoliday[36] = publicHoliday36;
					publicHoliday[37] = publicHoliday37;
					publicHoliday[38] = publicHoliday38;
					publicHoliday[39] = publicHoliday39;
					publicHoliday[40] = publicHoliday40;
					publicHoliday[41] = publicHoliday41;
					publicHoliday[42] = publicHoliday42;
					publicHoliday[43] = publicHoliday43;
					publicHoliday[44] = publicHoliday44;
					publicHoliday[45] = publicHoliday45;
					publicHoliday[46] = publicHoliday46;
					publicHoliday[47] = publicHoliday47;
					publicHoliday[48] = publicHoliday48;
					publicHoliday[49] = publicHoliday49;
					publicHoliday[50] = publicHoliday50;
					publicHoliday[51] = publicHoliday51;
					publicHoliday[52] = publicHoliday52;
					publicHoliday[53] = publicHoliday53;
					publicHoliday[54] = publicHoliday54;
					publicHoliday[55] = publicHoliday55;
					publicHoliday[56] = publicHoliday56;
					publicHoliday[57] = publicHoliday57;
					publicHoliday[58] = publicHoliday58;
					publicHoliday[59] = publicHoliday59;
					publicHoliday[60] = publicHoliday60;
					publicHoliday[61] = publicHoliday61;
					publicHoliday[62] = publicHoliday62;
					publicHoliday[63] = publicHoliday63;
					publicHoliday[64] = publicHoliday64;
					publicHoliday[65] = publicHoliday65;
					publicHoliday[66] = publicHoliday66;
					publicHoliday[67] = publicHoliday67;
					publicHoliday[68] = publicHoliday68;
					publicHoliday[69] = publicHoliday69;
					publicHoliday[70] = publicHoliday70;
					publicHoliday[71] = publicHoliday71;
					publicHoliday[72] = publicHoliday72;
					publicHoliday[73] = publicHoliday73;
					publicHoliday[74] = publicHoliday74;
					publicHoliday[75] = publicHoliday75;
					publicHoliday[76] = publicHoliday76;
					publicHoliday[77] = publicHoliday77;
					publicHoliday[78] = publicHoliday78;
					publicHoliday[79] = publicHoliday79;
					publicHoliday[80] = publicHoliday80;
					publicHoliday[81] = publicHoliday81;
				}			
				else for(int i=0; i<82; i++)
					publicHoliday[i] = Globals.MinDate;			
				if(ChartBars != null)
				{	
					breakAtEOD = ChartBars.Bars.IsResetOnNewTradingDay;
					errorBrush = ChartControl.Properties.AxisPen.Brush;
					errorBrush.Freeze();
					errorFont = new SimpleFont("Arial", 24);
					textFont = new SimpleFont("Arial", textFontSize);
				}
				basicError = false;
				errorMessage = false;
				if(!calculateFromPriceData)
				{
					Draw.TextFixed(this, "error text 1", errorText1, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);  
					errorMessage = true;
					basicError = true;
				}	
				else if(Bars.BarsPeriod.BarsPeriodType != BarsPeriodType.Minute && Bars.BarsPeriod.BarsPeriodType != BarsPeriodType.Day && Bars.BarsPeriod.BarsPeriodType != BarsPeriodType.Week)
				{
					Draw.TextFixed(this, "error text 2", errorText2, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);  
					errorMessage = true;
					basicError = true;
				}
				else if (Bars.BarsPeriod.BarsPeriodType == BarsPeriodType.Week && calcMode == amaRVCalcMode.Day_Of_Week)
				{
					Draw.TextFixed(this, "error text 3", errorText3, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);  
					errorMessage = true;
					basicError = true;
				}
				else if(!breakAtEOD)
				{
					Draw.TextFixed(this, "error text 4", errorText4, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);  
					errorMessage = true;
					basicError = true;
				}
				else if(displacement != 0)
				{
					Draw.TextFixed(this, "error text 5", errorText5, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);  
					errorMessage = true;
					basicError = true;
				}
			}
		}
		
		protected override void OnBarUpdate()
		{
			if(IsFirstTickOfBar)
			{	
				if(errorMessage)
				{	
					if(basicError)
						return;
					else if(sundaySessionError)
					{	
						Draw.TextFixed(this, "error text 6", errorText6, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);
						return;
					}	
				}	
			}
			else if(errorMessage)
				return;
			
			if(CurrentBar == 0)
			{	
				exchangeTime = TimeZoneInfo.ConvertTime(Time[0], globalTimeZone, instrumentTimeZone);
				index1 = (int)exchangeTime.DayOfWeek;
				if(isIntraday)
					index2 = 60 * exchangeTime.Hour + exchangeTime.Minute;
				else 
					index2 = 0;
				sessionIterator.CalculateTradingDay(Time[0], true);
				currentDate = sessionIterator.ActualTradingDayExchange;
				averageVolume.Reset();
				referenceVolume.Reset();
				return;
			}
			
			if(IsFirstTickOfBar)
			{	
				if(calcMode == amaRVCalcMode.All_Days)
				{	
					if(!skipTradingDay)
					{
						volSum = 0.0;
						for (int j = days - 1; j > 0; j--)
						{	
							historicalVolume[0, index2, j] = historicalVolume[0, index2, j-1];
							volSum += historicalVolume[0, index2, j];	
						}	
						historicalVolume[0, index2, 0] = Volume[1];
						volSum += Volume[1];
						count[0, index2] += 1;
						minCount = Math.Min(minCount, count[0, index2]);
						maxCount = Math.Max(maxCount, count[0, index2]);
						averageVolume[1] = volSum / Math.Min(days, count[0, index2]);
					}
					else
					{
						volSum = 0.0;
						minCount = Math.Min(minCount, count[0, index2]);
						maxCount = Math.Max(maxCount, count[0, index2]);
						for (int j = days - 1; j >= 0; j--)
							volSum += historicalVolume[0, index2, j];	
						if(count[0, index2] > 0)
							averageVolume[1] = volSum / Math.Min(days, count[0, index2]);
						else 
							averageVolume.Reset(1);
					}
					
					exchangeTime = TimeZoneInfo.ConvertTime(Time[0], globalTimeZone, instrumentTimeZone);
					if(isIntraday)
						index2 = 60 * exchangeTime.Hour + exchangeTime.Minute;
					
					lastBarTimeStamp = GetLastBarSessionDate(Time[0]);
					if(lastBarTimeStamp > currentDate)
					{	
						if(lastBarTimeStamp.DayOfWeek == DayOfWeek.Sunday)
						{
							sundaySessionError = true;
							errorMessage = true;
							return;
						}	
						priorMinCount = minCount;
						priorMaxCount = maxCount;
						minCount = days + 2; 
						maxCount = 0;
						sessionBars = 1;
						if(dayOfWeek && priorMaxCount > days)
							Draw.Text(this, "dayOfWeek" + CurrentBar, true, Convert.ToString(lastBarTimeStamp.DayOfWeek)+ "  ", 0, 0, -textFontSize-3, textBrush, textFont, textAlignmentLeft, textBoxBrush, textBoxBrush, textBoxOpacity); 
						skipTradingDay = false;
						for (int i = 0; i < 56; i++)
						{
							if (publicHoliday[i].Date == lastBarTimeStamp)
							{	
								skipTradingDay = true;
								break;
							}	
						}
						currentDate = lastBarTimeStamp;
					}
					else
						sessionBars = sessionBars + 1;
					
					if(priorMaxCount > days)
					{	
						priorTradingDay = lastBarTimeStamp;
						priorExchangeTime = exchangeTime;
						for (int i = 1; i <= priorMaxCount; i++)
						{
							if(priorTradingDay.DayOfWeek == DayOfWeek.Monday)
							{
								priorTradingDay = priorTradingDay.AddDays(-3);
								priorExchangeTime = priorExchangeTime.AddDays(-3);
							}
							else
							{	
								priorTradingDay = priorTradingDay.AddDays(-1);
								priorExchangeTime = priorExchangeTime.AddDays(-1);
							}	
							priorLocalTime = TimeZoneInfo.ConvertTime(priorExchangeTime, instrumentTimeZone, globalTimeZone);
							barsAgo = CurrentBar - Bars.GetBar(priorLocalTime);
							if(Time[barsAgo] == priorLocalTime)
							{	
								if(averageVolume.IsValidDataPoint(barsAgo))
								{	
									referenceVolume[0] = averageVolume[barsAgo];
									break;
								}
							}
							if(i == priorMaxCount)
							{	
								if(lastBarTimeStamp.DayOfWeek == DayOfWeek.Monday)
									priorExchangeTime = priorExchangeTime.AddDays(-3);
								else
									priorExchangeTime = priorExchangeTime.AddDays(-1);
								priorLocalTime = TimeZoneInfo.ConvertTime(priorExchangeTime, instrumentTimeZone, globalTimeZone);
								barsAgo = CurrentBar - Bars.GetBar(priorLocalTime);
								if(averageVolume.IsValidDataPoint(barsAgo))
									referenceVolume[0] = averageVolume[barsAgo];
								else
									referenceVolume.Reset();
							}	
						}
						if(markEstimatedValues && count[0, index2] < days)
							BackBrush = backColorBrush;
					}	
					else
						referenceVolume.Reset();
				}	
				else if(calcMode == amaRVCalcMode.Day_Of_Week)
				{	
					if(!skipTradingDay)
					{
						volSum = 0.0;
						for (int j = weeks - 1; j > 0; j--)
						{	
							historicalVolume[index1, index2, j] = historicalVolume[index1, index2, j-1];
							volSum += historicalVolume[index1, index2, j];	
						}	
						historicalVolume[index1, index2, 0] = Volume[1];
						volSum += Volume[1];
						count[index1, index2] += 1;
						minCount = Math.Min(minCount, count[index1, index2]);
						maxCount = Math.Max(maxCount, count[index1, index2]);
						averageVolume[1] = volSum / Math.Min(weeks, count[index1, index2]);
					}
					else
					{
						volSum = 0.0;
						minCount = Math.Min(minCount, count[index1, index2]);
						maxCount = Math.Max(maxCount, count[index1, index2]);
						for (int j = weeks - 1; j >= 0; j--)
							volSum += historicalVolume[index1, index2, j];	
						if(count[index1, index2] > 0)
							averageVolume[1] = volSum / Math.Min(weeks, count[index1, index2]);
						else 
							averageVolume.Reset(1);
					}
					
					exchangeTime = TimeZoneInfo.ConvertTime(Time[0], globalTimeZone, instrumentTimeZone);
					index1 = (int) exchangeTime.DayOfWeek;
					if(isIntraday)
						index2 = 60 * exchangeTime.Hour + exchangeTime.Minute;
					
					lastBarTimeStamp = GetLastBarSessionDate(Time[0]);
					if(lastBarTimeStamp > currentDate)
					{	
						if(lastBarTimeStamp.DayOfWeek < currentDate.DayOfWeek)
						{
							priorMinCount = minCount;
							priorMaxCount = maxCount;
							minCount = weeks + 2; 
							maxCount = 0;
						}	
						sessionBars = 1;
						if(dayOfWeek && priorMaxCount > weeks)
							Draw.Text(this, "dayOfWeek" + CurrentBar, true, Convert.ToString(lastBarTimeStamp.DayOfWeek)+ "  ", 0, 0 , -textFontSize-3, textBrush, textFont, textAlignmentLeft, textBoxBrush, textBoxBrush, textBoxOpacity); 
						skipTradingDay = false;
						for (int i = 0; i < 56; i++)
						{
							if (publicHoliday[i].Date == lastBarTimeStamp)
								skipTradingDay = true;
						}
						currentDate = lastBarTimeStamp;
					}
					else
						sessionBars = sessionBars + 1;
					
					if(priorMaxCount > weeks)
					{	
						for (int i = 1; i <= priorMaxCount; i++)
						{
							priorExchangeTime = exchangeTime.AddDays(-7*i);
							priorLocalTime = TimeZoneInfo.ConvertTime(priorExchangeTime, instrumentTimeZone, globalTimeZone);
							barsAgo = CurrentBar - Bars.GetBar(priorLocalTime);
							if(Time[barsAgo] == priorLocalTime)
							{	
								if(averageVolume.IsValidDataPoint(barsAgo))
								{	
									referenceVolume[0] = averageVolume[barsAgo];
									break;
								}	
							}
							if(i == priorMaxCount)
							{	
								priorExchangeTime = exchangeTime.AddDays(-7);
								priorLocalTime = TimeZoneInfo.ConvertTime(priorExchangeTime, instrumentTimeZone, globalTimeZone);
								barsAgo = CurrentBar - Bars.GetBar(priorLocalTime);
								if(averageVolume.IsValidDataPoint(barsAgo))
									referenceVolume[0] = averageVolume[barsAgo];
								else
									referenceVolume.Reset();
							}	
						}
						if(markEstimatedValues && count[index1, index2] < weeks)
							BackBrush = backColorBrush;
					}	
					else
						referenceVolume.Reset();
				}
				if (referenceVolume.IsValidDataPoint(0))
				{	
					if(sessionBars == 1)
						cumulReferenceVolume[0] = referenceVolume[0];
					else if (cumulReferenceVolume.IsValidDataPoint(1))
						cumulReferenceVolume[0] = referenceVolume[0] + cumulReferenceVolume[1];
					else	
						cumulReferenceVolume.Reset();
				}	
				else
					cumulReferenceVolume.Reset();
			}
			
			if(referenceVolume.IsValidDataPoint(0))
			{	
				RelativeVolume[0] = 100 * Volume[0] / referenceVolume[0];
				if(RelativeVolume[0] > upperThreshold)
					PlotBrushes[0][0] = highVolumeBrush;
				else if(RelativeVolume[0] < lowerThreshold)
					PlotBrushes[0][0] = lowVolumeBrush;
				else
					PlotBrushes[0][0] = mediumVolumeBrush;
				
				if(showRatioPlot)
				{	
					if(sessionBars == 1)
					{	
						cumulVolume[0] = Volume[0];
						CumulatedRatio[0] = RelativeVolume[0];
						PlotBrushes[1][0] = Brushes.Transparent;
					}	
					else if (cumulVolume.IsValidDataPoint(1))
					{	
						cumulVolume[0] = Volume[0] + cumulVolume[1];
						CumulatedRatio[0] = 100 * cumulVolume[0] / cumulReferenceVolume[0];
						if(CumulatedRatio[0] > 100)
							PlotBrushes[1][0] = highRatioBrush;
						else
							PlotBrushes[1][0] = lowRatioBrush;
					}	
					else
					{	
						cumulVolume.Reset();
						CumulatedRatio.Reset();
					}
				}	
			}	
			else
			{	
				RelativeVolume.Reset();
				if(showRatioPlot)
				{	
					cumulVolume.Reset();
					CumulatedRatio.Reset();
				}	
			}	
		}

		#region Properties
        [Browsable(false)]	
        [XmlIgnore()]		
        public Series<double> RelativeVolume
        {
            get { return Values[0]; }
        }
		
       	[Browsable(false)]	
        [XmlIgnore()]		
        public Series<double> CumulatedRatio
        {
            get { return Values[1]; }
        }
		
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Calculation mode", Description = "Select between comparing current volume to average volume of all days or same day of week only", GroupName = "Algorithmic Options", Order = 0)]
 		[RefreshProperties(RefreshProperties.All)] 
		public amaRVCalcMode CalcMode
		{	
            get { return calcMode; }
            set { calcMode = value; }
		}
		
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Reference period in days",  Description = "Sets the reference period which is used for calculating relative volume", GroupName = "Algorithmic Options", Order = 1)]
		public int Days
		{
			get { return days; }
			set { days = Math.Max(1, value); }
		}

		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Reference period in weeks", Description = "Sets the reference period which is used for calculating relative volume", GroupName = "Algorithmic Options", Order = 2)]
		public int Weeks
		{
			get { return weeks; }
			set { weeks = Math.Max(1, value); }
		}
			
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "High volume threshold", Description = "Sets the minimum percentage for a high relative volume bar", GroupName = "Input Parameters", Order = 0)]
		public int UpperThreshold
		{
			get { return upperThreshold; }
			set { upperThreshold = Math.Min(Math.Max(1, value),1000); }
		}
		
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Low volume threshold", Description = "Sets the maximum percentage for a low relative volume bar", GroupName = "Input Parameters", Order = 1)]
		public int LowerThreshold
		{
			get { return lowerThreshold; }
			set { lowerThreshold = Math.Min(Math.Max(1, value),1000); }
		}
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Show relative volume", Description = "Shows relative volume plot", GroupName = "Display Options", Order = 0)]
     	[RefreshProperties(RefreshProperties.All)] 
		public bool ShowRelativeVolume
		{
			get { return showRelativeVolume; }
			set { showRelativeVolume = value; }
		}
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Show cumulated ratio", Description = "Shows cumulated ratio plot", GroupName = "Display Options", Order = 1)]
     	[RefreshProperties(RefreshProperties.All)] 
		public bool ShowRatioPlot
		{
			get { return showRatioPlot; }
			set { showRatioPlot = value; }
		}
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Show numerical value", Description = "Shows numerical value for the cumulated ratio plot", GroupName = "Display Options", Order = 2)]
    	[RefreshProperties(RefreshProperties.All)] 
		public bool ShowNumericalValue
		{
			get { return showNumericalValue; }
			set { showNumericalValue = value; }
		}
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Mark estimated values", Description = "Marks values which were calculated from fewer data points or interpolated", GroupName = "Display Options", Order = 3)]
      	[RefreshProperties(RefreshProperties.All)] 
		public bool MarkEstimatedValues
		{
			get { return markEstimatedValues; }
			set { markEstimatedValues = value; }
		}
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Show  day of week", Description = "Shows the day of week below the relative volume bars", GroupName = "Display Options", Order = 4)]
     	[RefreshProperties(RefreshProperties.All)] 
       	public bool ShowDayOfWeek
        {
            get { return showDayOfWeek; }
            set { showDayOfWeek = value; }
        }
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "High volume", Description = "Sets the color for relative volume when above the upper threshold", GroupName = "Plot Colors", Order = 0)]
		public System.Windows.Media.Brush HighVolumeBrush
		{ 
			get {return highVolumeBrush;}
			set {highVolumeBrush = value;}
		}

		[Browsable(false)]
		public string HighVolumeBrushSerializable
		{
			get { return Serialize.BrushToString(highVolumeBrush); }
			set { highVolumeBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Medium volume", Description = "Sets the color for relative volume when between upper and lower threshold", GroupName = "Plot Colors", Order = 1)]
		public System.Windows.Media.Brush MediumVolumeBrush
		{ 
			get {return mediumVolumeBrush;}
			set {mediumVolumeBrush = value;}
		}

		[Browsable(false)]
		public string MediumVolumeBrushSerializable
		{
			get { return Serialize.BrushToString(mediumVolumeBrush); }
			set { mediumVolumeBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Low volume", Description = "Sets the color for relative volume when below the lower threshold", GroupName = "Plot Colors", Order = 2)]
		public System.Windows.Media.Brush LowVolumeBrush
		{ 
			get {return lowVolumeBrush;}
			set {lowVolumeBrush = value;}
		}

		[Browsable(false)]
		public string LowVolumeBrushSerializable
		{
			get { return Serialize.BrushToString(lowVolumeBrush); }
			set { lowVolumeBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Higher cumulated ratio", Description = "Sets the color for a cumulated ratio which is above average", GroupName = "Plot Colors", Order = 3)]
		public System.Windows.Media.Brush HighRatioBrush
		{ 
			get {return highRatioBrush;}
			set {highRatioBrush = value;}
		}

		[Browsable(false)]
		public string HighRatioBrushSerializable
		{
			get { return Serialize.BrushToString(highRatioBrush); }
			set { highRatioBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Lower cumulated ratio", Description = "Sets the color for a cumulated ratio which is below average", GroupName = "Plot Colors", Order = 4)]
		public System.Windows.Media.Brush LowRatioBrush
		{ 
			get {return lowRatioBrush;}
			set {lowRatioBrush = value;}
		}

		[Browsable(false)]
		public string LowRatioBrushSerializable
		{
			get { return Serialize.BrushToString(lowRatioBrush); }
			set { lowRatioBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Zeroline", Description = "Sets the color for the zeroline", GroupName = "Plot Colors", Order = 5)]
		public System.Windows.Media.Brush ZerolineBrush
		{
			get { return zerolineBrush; }
			set { zerolineBrush = value; }
		}

		[Browsable(false)]
		public string ZerolineBrushSerialize
		{
			get { return Serialize.BrushToString(zerolineBrush); }
			set { zerolineBrush = Serialize.StringToBrush(value); }
		}
		
		[Range(1, int.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Bar width relative volume", Description = "Sets the bar width for the relative volume bars", GroupName = "Plot Parameters", Order = 0)]
		public int Plot0Width
		{	
            get { return plot0Width; }
            set { plot0Width = value; }
		}
			
		[Display(ResourceType = typeof(Custom.Resource), Name = "Dash style cumulated ratio", Description = "Sets the dash style for the cumulated ratio plot", GroupName = "Plot Parameters", Order = 1)]
		public DashStyleHelper Dash1Style
		{
			get { return dash1Style; }
			set { dash1Style = value; }
		}
		
		[Range(1, int.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Plot width cumulated ratio", Description = "Sets the plot width for cumulated ratio plot", GroupName = "Plot Parameters", Order = 2)]
		public int Plot1Width
		{	
            get { return plot1Width; }
            set { plot1Width = value; }
		}
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Dash style zeroline", Description = "Sets the dash style for the zeroline", GroupName = "Plot Parameters", Order = 3)]
		public DashStyleHelper LineStyle
		{
			get { return lineStyle; }
			set { lineStyle = value; }
		}
		
		[Range(1, int.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Line width zeroline", Description = "Sets the line width for the zeroline", GroupName = "Plot Parameters", Order = 4)]
		public int LineWidth
		{	
            get { return lineWidth; }
            set { lineWidth = value; }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Text color", Description = "Sets the color for the text inside the boxes", GroupName = "Text Boxes", Order = 0)]
		public System.Windows.Media.Brush TextBrush
		{
			get { return textBrush; }
			set { textBrush = value; }
		}

		[Browsable(false)]
		public string TextBrushSerialize
		{
			get { return Serialize.BrushToString(textBrush); }
			set { textBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Box color", Description = "Sets the color for the text boxes", GroupName = "Text Boxes", Order = 1)]
		public System.Windows.Media.Brush TextBoxBrush
		{
			get { return textBoxBrush; }
			set { textBoxBrush = value; }
		}

		[Browsable(false)]
		public string TextBoxBrushSerialize
		{
			get { return Serialize.BrushToString(textBoxBrush); }
			set { textBoxBrush = Serialize.StringToBrush(value); }
		}
		
		[Range(1, int.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Box Opacity", Description = "Sets the opacity for the text boxes", GroupName = "Text Boxes", Order = 2)]
		public int TextBoxOpacity
		{	
            get { return textBoxOpacity; }
            set { textBoxOpacity = value; }
		}
		
		[Range(1, int.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Font Size", Description = "Sets the font size for the text boxes", GroupName = "Text Boxes", Order = 3)]
		public int TextFontSize
		{	
            get { return textFontSize; }
            set { textFontSize = value; }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "BackColor", Description = "Sets the background color for estimated values", GroupName = "Estimated Values", Order = 0)]
		public System.Windows.Media.Brush BackColorBrush
		{
			get { return backColorBrush; }
			set { backColorBrush = value; }
		}

		[Display(ResourceType = typeof(Custom.Resource), Name = "Exclude Holidays", Description = "Exclude low volume days from calculating the reference volume", GroupName = "Holidays", Order = 0)]
       	public bool ExcludeHolidays
        {
            get { return excludeHolidays; }
            set { excludeHolidays = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Show holiday dates", Description = "Shows all selected holiday dates below", GroupName = "Holidays", Order = 1)]
     	[RefreshProperties(RefreshProperties.All)] 
       	public bool ShowHolidayDates
        {
            get { return showHolidayDates; }
            set { showHolidayDates = value; }
        }

		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 01", Description = "Enter holiday date", GroupName = "Holidays", Order = 2)]
        public DateTime PublicHoliday0
        {
            get { return publicHoliday0; }
            set { publicHoliday0 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 02", Description = "Enter holiday date", GroupName = "Holidays", Order = 3)]
        public DateTime PublicHoliday1
        {
            get { return publicHoliday1; }
            set { publicHoliday1 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 03", Description = "Enter holiday date", GroupName = "Holidays", Order = 4)]
        public DateTime PublicHoliday2
        {
            get { return publicHoliday2; }
            set { publicHoliday2 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 04", Description = "Enter holiday date", GroupName = "Holidays", Order = 5)]
        public DateTime PublicHoliday3
        {
            get { return publicHoliday3; }
            set { publicHoliday3 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 05", Description = "Enter holiday date", GroupName = "Holidays", Order = 6)]
        public DateTime PublicHoliday4
        {
            get { return publicHoliday4; }
            set { publicHoliday4 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 06", Description = "Enter holiday date", GroupName = "Holidays", Order = 7)]
        public DateTime PublicHoliday5
        {
            get { return publicHoliday5; }
            set { publicHoliday5 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 07", Description = "Enter holiday date", GroupName = "Holidays", Order = 8)]
        public DateTime PublicHoliday6
        {
            get { return publicHoliday6; }
            set { publicHoliday6 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 08", Description = "Enter holiday date", GroupName = "Holidays", Order = 9)]
        public DateTime PublicHoliday7
        {
            get { return publicHoliday7; }
            set { publicHoliday7 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 09", Description = "Enter holiday date", GroupName = "Holidays", Order = 10)]
        public DateTime PublicHoliday8
        {
            get { return publicHoliday8; }
            set { publicHoliday8 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 10", Description = "Enter holiday date", GroupName = "Holidays", Order = 11)]
        public DateTime PublicHoliday9
        {
            get { return publicHoliday9; }
            set { publicHoliday9 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 11", Description = "Enter holiday date", GroupName = "Holidays", Order = 12)]
        public DateTime PublicHoliday10
        {
            get { return publicHoliday10; }
            set { publicHoliday10 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 12", Description = "Enter holiday date", GroupName = "Holidays", Order = 13)]
        public DateTime PublicHoliday11
        {
            get { return publicHoliday11; }
            set { publicHoliday11 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 13", Description = "Enter holiday date", GroupName = "Holidays", Order = 14)]
        public DateTime PublicHoliday12
        {
            get { return publicHoliday12; }
            set { publicHoliday12 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 14", Description = "Enter holiday date", GroupName = "Holidays", Order = 15)]
        public DateTime PublicHoliday13
        {
            get { return publicHoliday13; }
            set { publicHoliday13 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 15", Description = "Enter holiday date", GroupName = "Holidays", Order = 16)]
        public DateTime PublicHoliday14
        {
            get { return publicHoliday14; }
            set { publicHoliday14 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 16", Description = "Enter holiday date", GroupName = "Holidays", Order = 17)]
        public DateTime PublicHoliday15
        {
            get { return publicHoliday15; }
            set { publicHoliday15 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 17", Description = "Enter holiday date", GroupName = "Holidays", Order = 18)]
        public DateTime PublicHoliday16
        {
            get { return publicHoliday16; }
            set { publicHoliday16 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 18", Description = "Enter holiday date", GroupName = "Holidays", Order = 19)]
        public DateTime PublicHoliday17
        {
            get { return publicHoliday17; }
            set { publicHoliday17 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 19", Description = "Enter holiday date", GroupName = "Holidays", Order = 20)]
        public DateTime PublicHoliday18
        {
            get { return publicHoliday18; }
            set { publicHoliday18 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 20", Description = "Enter holiday date", GroupName = "Holidays", Order = 21)]
        public DateTime PublicHoliday19
        {
            get { return publicHoliday19; }
            set { publicHoliday19 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 21", Description = "Enter holiday date", GroupName = "Holidays", Order = 22)]
        public DateTime PublicHoliday20
        {
            get { return publicHoliday20; }
            set { publicHoliday20 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 22", Description = "Enter holiday date", GroupName = "Holidays", Order = 23)]
        public DateTime PublicHoliday21
        {
            get { return publicHoliday21; }
            set { publicHoliday21 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 23", Description = "Enter holiday date", GroupName = "Holidays", Order = 24)]
        public DateTime PublicHoliday22
        {
            get { return publicHoliday22; }
            set { publicHoliday22 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 24", Description = "Enter holiday date", GroupName = "Holidays", Order = 25)]
        public DateTime PublicHoliday23
        {
            get { return publicHoliday23; }
            set { publicHoliday23 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 25", Description = "Enter holiday date", GroupName = "Holidays", Order = 26)]
        public DateTime PublicHoliday24
        {
            get { return publicHoliday24; }
            set { publicHoliday24 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 26", Description = "Enter holiday date", GroupName = "Holidays", Order = 27)]
        public DateTime PublicHoliday25
        {
            get { return publicHoliday25; }
            set { publicHoliday25 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 27", Description = "Enter holiday date", GroupName = "Holidays", Order = 28)]
        public DateTime PublicHoliday26
        {
            get { return publicHoliday26; }
            set { publicHoliday26 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 28", Description = "Enter holiday date", GroupName = "Holidays", Order = 29)]
        public DateTime PublicHoliday27
        {
            get { return publicHoliday27; }
            set { publicHoliday27 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 29", Description = "Enter holiday date", GroupName = "Holidays", Order = 30)]
        public DateTime PublicHoliday28
        {
            get { return publicHoliday28; }
            set { publicHoliday28 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 30", Description = "Enter holiday date", GroupName = "Holidays", Order = 31)]
        public DateTime PublicHoliday29
        {
            get { return publicHoliday29; }
            set { publicHoliday29 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 31", Description = "Enter holiday date", GroupName = "Holidays", Order = 32)]
        public DateTime PublicHoliday30
        {
            get { return publicHoliday30; }
            set { publicHoliday30 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 32", Description = "Enter holiday date", GroupName = "Holidays", Order = 33)]
        public DateTime PublicHoliday31
        {
            get { return publicHoliday31; }
            set { publicHoliday31 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 33", Description = "Enter holiday date", GroupName = "Holidays", Order = 34)]
        public DateTime PublicHoliday32
        {
            get { return publicHoliday32; }
            set { publicHoliday32 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 34", Description = "Enter holiday date", GroupName = "Holidays", Order = 35)]
        public DateTime PublicHoliday33
        {
            get { return publicHoliday33; }
            set { publicHoliday33 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 35", Description = "Enter holiday date", GroupName = "Holidays", Order = 36)]
        public DateTime PublicHoliday34
        {
            get { return publicHoliday34; }
            set { publicHoliday34 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 36", Description = "Enter holiday date", GroupName = "Holidays", Order = 37)]
        public DateTime PublicHoliday35
        {
            get { return publicHoliday35; }
            set { publicHoliday35 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 37", Description = "Enter holiday date", GroupName = "Holidays", Order = 38)]
        public DateTime PublicHoliday36
        {
            get { return publicHoliday36; }
            set { publicHoliday36 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 38", Description = "Enter holiday date", GroupName = "Holidays", Order = 39)]
        public DateTime PublicHoliday37
        {
            get { return publicHoliday37; }
            set { publicHoliday37 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 39", Description = "Enter holiday date", GroupName = "Holidays", Order = 40)]
        public DateTime PublicHoliday38
        {
            get { return publicHoliday38; }
            set { publicHoliday38 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 40", Description = "Enter holiday date", GroupName = "Holidays", Order = 41)]
        public DateTime PublicHoliday39
        {
            get { return publicHoliday39; }
            set { publicHoliday39 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 41", Description = "Enter holiday date", GroupName = "Holidays", Order = 42)]
        public DateTime PublicHoliday40
        {
            get { return publicHoliday40; }
            set { publicHoliday40 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 42", Description = "Enter holiday date", GroupName = "Holidays", Order = 43)]
        public DateTime PublicHoliday41
        {
            get { return publicHoliday41; }
            set { publicHoliday41 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 43", Description = "Enter holiday date", GroupName = "Holidays", Order = 44)]
        public DateTime PublicHoliday42
        {
            get { return publicHoliday42; }
            set { publicHoliday42 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 44", Description = "Enter holiday date", GroupName = "Holidays", Order = 45)]
        public DateTime PublicHoliday43
        {
            get { return publicHoliday43; }
            set { publicHoliday43 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 45", Description = "Enter holiday date", GroupName = "Holidays", Order = 46)]
        public DateTime PublicHoliday44
        {
            get { return publicHoliday44; }
            set { publicHoliday44 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 46", Description = "Enter holiday date", GroupName = "Holidays", Order = 47)]
        public DateTime PublicHoliday45
        {
            get { return publicHoliday45; }
            set { publicHoliday45 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 47", Description = "Enter holiday date", GroupName = "Holidays", Order = 48)]
        public DateTime PublicHoliday46
        {
            get { return publicHoliday46; }
            set { publicHoliday46 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 48", Description = "Enter holiday date", GroupName = "Holidays", Order = 49)]
        public DateTime PublicHoliday47
        {
            get { return publicHoliday47; }
            set { publicHoliday47 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 49", Description = "Enter holiday date", GroupName = "Holidays", Order = 50)]
        public DateTime PublicHoliday48
        {
            get { return publicHoliday48; }
            set { publicHoliday48 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 50", Description = "Enter holiday date", GroupName = "Holidays", Order = 51)]
        public DateTime PublicHoliday49
        {
            get { return publicHoliday49; }
            set { publicHoliday49 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 51", Description = "Enter holiday date", GroupName = "Holidays", Order = 52)]
        public DateTime PublicHoliday50
        {
            get { return publicHoliday50; }
            set { publicHoliday50 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 52", Description = "Enter holiday date", GroupName = "Holidays", Order = 53)]
        public DateTime PublicHoliday51
        {
            get { return publicHoliday51; }
            set { publicHoliday51 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 53", Description = "Enter holiday date", GroupName = "Holidays", Order = 54)]
        public DateTime PublicHoliday52
        {
            get { return publicHoliday52; }
            set { publicHoliday52 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 54", Description = "Enter holiday date", GroupName = "Holidays", Order = 55)]
        public DateTime PublicHoliday53
        {
            get { return publicHoliday53; }
            set { publicHoliday53 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 55", Description = "Enter holiday date", GroupName = "Holidays", Order = 56)]
        public DateTime PublicHoliday54
        {
            get { return publicHoliday54; }
            set { publicHoliday54 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 56", Description = "Enter holiday date", GroupName = "Holidays", Order = 57)]
        public DateTime PublicHoliday55
        {
            get { return publicHoliday55; }
            set { publicHoliday55 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 57", Description = "Enter holiday date", GroupName = "Holidays", Order = 58)]
        public DateTime PublicHoliday56
        {
            get { return publicHoliday56; }
            set { publicHoliday56 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 58", Description = "Enter holiday date", GroupName = "Holidays", Order = 59)]
        public DateTime PublicHoliday57
        {
            get { return publicHoliday57; }
            set { publicHoliday57 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 59", Description = "Enter holiday date", GroupName = "Holidays", Order = 60)]
        public DateTime PublicHoliday58
        {
            get { return publicHoliday58; }
            set { publicHoliday58 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 60", Description = "Enter holiday date", GroupName = "Holidays", Order = 61)]
        public DateTime PublicHoliday59
        {
            get { return publicHoliday59; }
            set { publicHoliday59 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 61", Description = "Enter holiday date", GroupName = "Holidays", Order = 62)]
        public DateTime PublicHoliday60
        {
            get { return publicHoliday60; }
            set { publicHoliday60 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 62", Description = "Enter holiday date", GroupName = "Holidays", Order = 63)]
        public DateTime PublicHoliday61
        {
            get { return publicHoliday61; }
            set { publicHoliday61 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 63", Description = "Enter holiday date", GroupName = "Holidays", Order = 64)]
        public DateTime PublicHoliday62
        {
            get { return publicHoliday62; }
            set { publicHoliday62 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 64", Description = "Enter holiday date", GroupName = "Holidays", Order = 65)]
        public DateTime PublicHoliday63
        {
            get { return publicHoliday63; }
            set { publicHoliday63 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 65", Description = "Enter holiday date", GroupName = "Holidays", Order = 66)]
        public DateTime PublicHoliday64
        {
            get { return publicHoliday64; }
            set { publicHoliday64 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 66", Description = "Enter holiday date", GroupName = "Holidays", Order = 67)]
        public DateTime PublicHoliday65
        {
            get { return publicHoliday65; }
            set { publicHoliday65 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 67", Description = "Enter holiday date", GroupName = "Holidays", Order = 68)]
        public DateTime PublicHoliday66
        {
            get { return publicHoliday66; }
            set { publicHoliday66 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 68", Description = "Enter holiday date", GroupName = "Holidays", Order = 69)]
        public DateTime PublicHoliday67
        {
            get { return publicHoliday67; }
            set { publicHoliday67 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 69", Description = "Enter holiday date", GroupName = "Holidays", Order = 70)]
        public DateTime PublicHoliday68
        {
            get { return publicHoliday68; }
            set { publicHoliday68 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 70", Description = "Enter holiday date", GroupName = "Holidays", Order = 71)]
        public DateTime PublicHoliday69
        {
            get { return publicHoliday69; }
            set { publicHoliday69 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 71", Description = "Enter holiday date", GroupName = "Holidays", Order = 72)]
        public DateTime PublicHoliday70
        {
            get { return publicHoliday70; }
            set { publicHoliday70 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 72", Description = "Enter holiday date", GroupName = "Holidays", Order = 73)]
        public DateTime PublicHoliday71
        {
            get { return publicHoliday71; }
            set { publicHoliday71 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 73", Description = "Enter holiday date", GroupName = "Holidays", Order = 74)]
        public DateTime PublicHoliday72
        {
            get { return publicHoliday72; }
            set { publicHoliday72 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 74", Description = "Enter holiday date", GroupName = "Holidays", Order = 75)]
        public DateTime PublicHoliday73
        {
            get { return publicHoliday73; }
            set { publicHoliday73 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 75", Description = "Enter holiday date", GroupName = "Holidays", Order = 76)]
        public DateTime PublicHoliday74
        {
            get { return publicHoliday74; }
            set { publicHoliday74 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 76", Description = "Enter holiday date", GroupName = "Holidays", Order = 77)]
        public DateTime PublicHoliday75
        {
            get { return publicHoliday75; }
            set { publicHoliday75 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 77", Description = "Enter holiday date", GroupName = "Holidays", Order = 78)]
        public DateTime PublicHoliday76
        {
            get { return publicHoliday76; }
            set { publicHoliday76 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 78", Description = "Enter holiday date", GroupName = "Holidays", Order = 79)]
        public DateTime PublicHoliday77
        {
            get { return publicHoliday77; }
            set { publicHoliday77 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 79", Description = "Enter holiday date", GroupName = "Holidays", Order = 80)]
        public DateTime PublicHoliday78
        {
            get { return publicHoliday78; }
            set { publicHoliday78 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 80", Description = "Enter holiday date", GroupName = "Holidays", Order = 81)]
        public DateTime PublicHoliday79
        {
            get { return publicHoliday79; }
            set { publicHoliday79 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 81", Description = "Enter holiday date", GroupName = "Holidays", Order = 82)]
        public DateTime PublicHoliday80
        {
            get { return publicHoliday80; }
            set { publicHoliday80 = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Holiday 82", Description = "Enter holiday date", GroupName = "Holidays", Order = 83)]
        public DateTime PublicHoliday81
        {
            get { return publicHoliday81; }
            set { publicHoliday81 = value; }
        }
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Release and date", Description = "Release and date", GroupName = "Version", Order = 0)]
		public string VersionString
		{	
            get { return versionString; }
            set { ; }
		}
		#endregion
	
		#region Miscellaneous
		
		private DateTime GetLastBarSessionDate(DateTime time)
		{
			if (time > cacheSessionEndTmp) 
			{
				if (Bars.BarsType.IsIntraday)
				{	
					sessionIterator.CalculateTradingDay(time, true);
					sessionDateTmp = sessionIterator.ActualTradingDayExchange;
					cacheSessionEndTmp = sessionIterator.ActualSessionEnd;
				}	
				else
				{	
					sessionDateTmp = time.Date;
					cacheSessionEndTmp = time.Date;
				}	
			}
			return sessionDateTmp;			
		}
		
		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (Bars == null || ChartControl == null || !IsVisible) return;
			
			int	lastBarPainted 	 						= ChartBars.ToIndex;
			int lastBarCounted							= Inputs[0].Count - 1;
			int	lastBarOnUpdate							= lastBarCounted - (Calculate == Calculate.OnBarClose ? 1 : 0);
			int	lastBarIndex							= Math.Min(lastBarPainted, lastBarOnUpdate);
			
			if(!Values[0].IsValidDataPointAt(lastBarIndex))
			{
				if(!errorMessage)
				{	
					SharpDX.Direct2D1.Brush errorBrushDX = errorBrush.ToDxBrush(RenderTarget);
					TextFormat textFormat2 = new TextFormat(Globals.DirectWriteFactory, "Arial", 20.0f);		
					SharpDX.DirectWrite.TextLayout textLayout2 = new SharpDX.DirectWrite.TextLayout(NinjaTrader.Core.Globals.DirectWriteFactory, errorText7, textFormat2, 1000, 20.0f);
					SharpDX.Vector2 lowerTextPoint = new SharpDX.Vector2(ChartPanel.W/2 - textLayout2.Metrics.Width/2, ChartPanel.Y + (ChartPanel.H/2)); 
					RenderTarget.DrawTextLayout(lowerTextPoint, textLayout2, errorBrushDX, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
					errorBrushDX.Dispose();
					textFormat2.Dispose();
					textLayout2.Dispose();
				}	
				return;
			}
			else if(showNumericalValue)
			{
				SharpDX.Direct2D1.Brush highRatioBrushDX 		= highRatioBrush.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush lowRatioBrushDX 		= lowRatioBrush.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush highRatioFillBrushDX 	= highRatioBrushOpaque.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush lowRatioFillBrushDX 	= lowRatioBrushOpaque.ToDxBrush(RenderTarget);
				SharpDX.Direct2D1.Brush textBrushDX				= textBrush.ToDxBrush(RenderTarget);;
				double barWidth									= chartControl.GetBarPaintWidth(chartControl.BarsArray[0]);
				double lastValue								= CumulatedRatio.GetValueAt(lastBarIndex);
				double lastX									= ChartControl.GetXByBarIndex(ChartBars, lastBarIndex);
				double lastY									= chartScale.GetYByValue(lastValue);
				double labelOffset								= (this.TextFontSize + barWidth)/2.0 + 20;
				TextFormat textFormat 							= new TextFormat(Globals.DirectWriteFactory, "Arial", (float)textFontSize);		
				TextLayout textLayout 							= new TextLayout(Globals.DirectWriteFactory, Convert.ToString(Math.Round(lastValue)) + " %", textFormat, Math.Max(100, ChartControl.Properties.BarMarginRight - (float)labelOffset - 20), textFontSize);
				SharpDX.Vector2 textPointDX 					= new SharpDX.Vector2((float)(lastX + labelOffset), ((float)(lastY - textFontSize/2.0)));
				SharpDX.RectangleF rect							= new SharpDX.RectangleF(textPointDX.X, textPointDX.Y, textLayout.Metrics.Width, textLayout.Metrics.Height);;
				if(lastValue > 100)
				{	
					RenderTarget.FillRectangle(rect, highRatioFillBrushDX);
					RenderTarget.DrawRectangle(rect, highRatioBrushDX);
				}	
				else
				{	
					RenderTarget.FillRectangle(rect, lowRatioFillBrushDX);
					RenderTarget.DrawRectangle(rect, lowRatioBrushDX);
				}	
				RenderTarget.DrawTextLayout(textPointDX, textLayout, textBrushDX);
				highRatioBrushDX.Dispose();
				lowRatioBrushDX.Dispose();
				highRatioFillBrushDX.Dispose();
				lowRatioFillBrushDX.Dispose();
				textBrushDX.Dispose();
				textLayout.Dispose();
				textFormat.Dispose();
			}
			base.OnRender(chartControl, chartScale);
		}
		#endregion
	}
}

namespace NinjaTrader.NinjaScript.Indicators
{		
	public class amaRelativeVolumeTypeConverter : NinjaTrader.NinjaScript.IndicatorBaseConverter
	{
		public override bool GetPropertiesSupported(ITypeDescriptorContext context) { return true; }

		public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
		{
			PropertyDescriptorCollection propertyDescriptorCollection = base.GetPropertiesSupported(context) ? base.GetProperties(context, value, attributes) : TypeDescriptor.GetProperties(value, attributes);

			amaRelativeVolume			thisRelativeVolumeInstance			= (amaRelativeVolume) value;
			amaRVCalcMode				calcModeFromInstance				= thisRelativeVolumeInstance.CalcMode;
			bool						showRelativeVolumeFromInstance		= thisRelativeVolumeInstance.ShowRelativeVolume;
			bool						showRatioPlotFromInstance			= thisRelativeVolumeInstance.ShowRatioPlot;
			bool						showNumericalValueFromInstance		= thisRelativeVolumeInstance.ShowNumericalValue;
			bool						markEstimatedValuesFromInstance		= thisRelativeVolumeInstance.MarkEstimatedValues;
			bool						showDayOfWeekFromInstance			= thisRelativeVolumeInstance.ShowDayOfWeek;
			bool						showHolidaysFromInstance			= thisRelativeVolumeInstance.ShowHolidayDates;
			
			PropertyDescriptorCollection adjusted = new PropertyDescriptorCollection(null);
			
			foreach (PropertyDescriptor thisDescriptor in propertyDescriptorCollection)
			{
				if (calcModeFromInstance == amaRVCalcMode.All_Days && thisDescriptor.Name == "Weeks") 
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if (calcModeFromInstance == amaRVCalcMode.Day_Of_Week && thisDescriptor.Name == "Days") 
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if (!showRelativeVolumeFromInstance && (thisDescriptor.Name == "HighVolumeBrush" || thisDescriptor.Name == "MediumVolumeBrush" 
					|| thisDescriptor.Name == "LowVolumeBrush" || thisDescriptor.Name == "Plot0Width"))
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if (!showRatioPlotFromInstance && (thisDescriptor.Name == "HighRatioBrush" || thisDescriptor.Name == "LowRatioBrush" 
					|| thisDescriptor.Name == "Dash1Style" || thisDescriptor.Name == "Plot1Width"))
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if (!showNumericalValueFromInstance && !showDayOfWeekFromInstance && (thisDescriptor.Name == "TextBoxBrush" || thisDescriptor.Name == "TextBoxOpacity" || thisDescriptor.Name == "TextFontSize"))
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if (!markEstimatedValuesFromInstance && thisDescriptor.Name == "BackColorBrush")
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if(!showHolidaysFromInstance && (thisDescriptor.Name == "PublicHoliday0" || thisDescriptor.Name == "PublicHoliday1" || thisDescriptor.Name == "PublicHoliday2" || thisDescriptor.Name == "PublicHoliday3"
					|| thisDescriptor.Name == "PublicHoliday4" || thisDescriptor.Name == "PublicHoliday5" || thisDescriptor.Name == "PublicHoliday6" || thisDescriptor.Name == "PublicHoliday7" 
					|| thisDescriptor.Name == "PublicHoliday8" || thisDescriptor.Name == "PublicHoliday9" || thisDescriptor.Name == "PublicHoliday10" || thisDescriptor.Name == "PublicHoliday11" 
					|| thisDescriptor.Name == "PublicHoliday12" || thisDescriptor.Name == "PublicHoliday13" || thisDescriptor.Name == "PublicHoliday14" || thisDescriptor.Name == "PublicHoliday15" 
					|| thisDescriptor.Name == "PublicHoliday16" || thisDescriptor.Name == "PublicHoliday17" || thisDescriptor.Name == "PublicHoliday18" || thisDescriptor.Name == "PublicHoliday19" 
					|| thisDescriptor.Name == "PublicHoliday20" || thisDescriptor.Name == "PublicHoliday21" || thisDescriptor.Name == "PublicHoliday22" || thisDescriptor.Name == "PublicHoliday23"
					|| thisDescriptor.Name == "PublicHoliday24" || thisDescriptor.Name == "PublicHoliday25" || thisDescriptor.Name == "PublicHoliday26" || thisDescriptor.Name == "PublicHoliday27" 
					|| thisDescriptor.Name == "PublicHoliday28" || thisDescriptor.Name == "PublicHoliday29" || thisDescriptor.Name == "PublicHoliday30" || thisDescriptor.Name == "PublicHoliday31" 
					|| thisDescriptor.Name == "PublicHoliday32" || thisDescriptor.Name == "PublicHoliday33" || thisDescriptor.Name == "PublicHoliday34" || thisDescriptor.Name == "PublicHoliday35" 
					|| thisDescriptor.Name == "PublicHoliday36" || thisDescriptor.Name == "PublicHoliday37" || thisDescriptor.Name == "PublicHoliday38" || thisDescriptor.Name == "PublicHoliday39" 
					|| thisDescriptor.Name == "PublicHoliday40" || thisDescriptor.Name == "PublicHoliday41" || thisDescriptor.Name == "PublicHoliday42" || thisDescriptor.Name == "PublicHoliday43"
					|| thisDescriptor.Name == "PublicHoliday44" || thisDescriptor.Name == "PublicHoliday45" || thisDescriptor.Name == "PublicHoliday46" || thisDescriptor.Name == "PublicHoliday47" 
					|| thisDescriptor.Name == "PublicHoliday48" || thisDescriptor.Name == "PublicHoliday49" || thisDescriptor.Name == "PublicHoliday50" || thisDescriptor.Name == "PublicHoliday51" 
					|| thisDescriptor.Name == "PublicHoliday52" || thisDescriptor.Name == "PublicHoliday53" || thisDescriptor.Name == "PublicHoliday54" || thisDescriptor.Name == "PublicHoliday55"
					|| thisDescriptor.Name == "PublicHoliday56" || thisDescriptor.Name == "PublicHoliday57" || thisDescriptor.Name == "PublicHoliday58" || thisDescriptor.Name == "PublicHoliday59" 
					|| thisDescriptor.Name == "PublicHoliday60" || thisDescriptor.Name == "PublicHoliday61" || thisDescriptor.Name == "PublicHoliday62" || thisDescriptor.Name == "PublicHoliday63" 
					|| thisDescriptor.Name == "PublicHoliday64" || thisDescriptor.Name == "PublicHoliday65" || thisDescriptor.Name == "PublicHoliday66" || thisDescriptor.Name == "PublicHoliday67" 
					|| thisDescriptor.Name == "PublicHoliday68" || thisDescriptor.Name == "PublicHoliday69" || thisDescriptor.Name == "PublicHoliday70" || thisDescriptor.Name == "PublicHoliday71" 
					|| thisDescriptor.Name == "PublicHoliday72" || thisDescriptor.Name == "PublicHoliday73" || thisDescriptor.Name == "PublicHoliday74" || thisDescriptor.Name == "PublicHoliday75" 
					|| thisDescriptor.Name == "PublicHoliday76" || thisDescriptor.Name == "PublicHoliday77" || thisDescriptor.Name == "PublicHoliday78" || thisDescriptor.Name == "PublicHoliday79" 
					|| thisDescriptor.Name == "PublicHoliday80" || thisDescriptor.Name == "PublicHoliday81"))
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else	
					adjusted.Add(thisDescriptor);
			}
			return adjusted;
		}
	}
}

#region Public Enums
public enum amaRVCalcMode 
{
	All_Days, 
	Day_Of_Week
}
#endregion

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private LizardIndicators.amaRelativeVolume[] cacheamaRelativeVolume;
		public LizardIndicators.amaRelativeVolume amaRelativeVolume(amaRVCalcMode calcMode, int days, int weeks, int upperThreshold, int lowerThreshold)
		{
			return amaRelativeVolume(Input, calcMode, days, weeks, upperThreshold, lowerThreshold);
		}

		public LizardIndicators.amaRelativeVolume amaRelativeVolume(ISeries<double> input, amaRVCalcMode calcMode, int days, int weeks, int upperThreshold, int lowerThreshold)
		{
			if (cacheamaRelativeVolume != null)
				for (int idx = 0; idx < cacheamaRelativeVolume.Length; idx++)
					if (cacheamaRelativeVolume[idx] != null && cacheamaRelativeVolume[idx].CalcMode == calcMode && cacheamaRelativeVolume[idx].Days == days && cacheamaRelativeVolume[idx].Weeks == weeks && cacheamaRelativeVolume[idx].UpperThreshold == upperThreshold && cacheamaRelativeVolume[idx].LowerThreshold == lowerThreshold && cacheamaRelativeVolume[idx].EqualsInput(input))
						return cacheamaRelativeVolume[idx];
			return CacheIndicator<LizardIndicators.amaRelativeVolume>(new LizardIndicators.amaRelativeVolume(){ CalcMode = calcMode, Days = days, Weeks = weeks, UpperThreshold = upperThreshold, LowerThreshold = lowerThreshold }, input, ref cacheamaRelativeVolume);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.LizardIndicators.amaRelativeVolume amaRelativeVolume(amaRVCalcMode calcMode, int days, int weeks, int upperThreshold, int lowerThreshold)
		{
			return indicator.amaRelativeVolume(Input, calcMode, days, weeks, upperThreshold, lowerThreshold);
		}

		public Indicators.LizardIndicators.amaRelativeVolume amaRelativeVolume(ISeries<double> input , amaRVCalcMode calcMode, int days, int weeks, int upperThreshold, int lowerThreshold)
		{
			return indicator.amaRelativeVolume(input, calcMode, days, weeks, upperThreshold, lowerThreshold);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.LizardIndicators.amaRelativeVolume amaRelativeVolume(amaRVCalcMode calcMode, int days, int weeks, int upperThreshold, int lowerThreshold)
		{
			return indicator.amaRelativeVolume(Input, calcMode, days, weeks, upperThreshold, lowerThreshold);
		}

		public Indicators.LizardIndicators.amaRelativeVolume amaRelativeVolume(ISeries<double> input , amaRVCalcMode calcMode, int days, int weeks, int upperThreshold, int lowerThreshold)
		{
			return indicator.amaRelativeVolume(input, calcMode, days, weeks, upperThreshold, lowerThreshold);
		}
	}
}

#endregion
