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
	/// The Opening Range indicator is designed to display the opening price and the opening range of the full or regular trading session. 
	/// It further has an option to show the pre-session range.
	/// </summary>
	/// 
	[Gui.CategoryOrder("Opening Range", 5)]
	[Gui.CategoryOrder("Pre-Session Range", 10)]
	[Gui.CategoryOrder("Algorithmic Options", 15)]
	[Gui.CategoryOrder("Display Options", 20)]
	[Gui.CategoryOrder("Data Series", 30)]
	[Gui.CategoryOrder("Set up", 40)]
	[Gui.CategoryOrder("Visual", 50)]
	[Gui.CategoryOrder("Plot Colors", 60)]
	[Gui.CategoryOrder("Plot Parameters", 70)]
	[Gui.CategoryOrder("Area Opacity", 80)]
	[TypeConverter("NinjaTrader.NinjaScript.Indicators.amaOpeningRangeTypeConverter")]
	public class amaOpeningRange : Indicator
	{
		private DateTime						sessionDateTmp					= Globals.MinDate;
		private DateTime						cacheSessionDate				= Globals.MinDate;
		private TimeSpan						openingRangeStart				= new TimeSpan(8,30,0);
		private TimeSpan						openingRangeEnd					= new TimeSpan(9,0,0);
		private TimeSpan						preSessionStart					= new TimeSpan(17,0,0);
		private TimeSpan						preSessionEnd					= new TimeSpan(8,30,0);
		private double							openingRangeMidline				= 0.0;
		private double							preSessionMidline				= 0.0;
		private int								displacement					= 0;
		private int								count							= 0;
		private bool							showRegOpen						= true;
		private bool							showOpeningRange				= true;
		private bool							showOpeningRangeMidline			= true;
		private bool							showPreSession					= true;
		private bool							showPreSessionMidline			= true;
		private bool							showLabels						= true;
		private bool							plotOR							= false;
		private bool							plotPS							= false;
		private bool							timeBased						= true;
		private bool							breakAtEOD						= true;
		private bool							calculateFromPriceData			= true;
		private bool							anchorBarOR						= false;
		private bool							anchorBarPS						= false;
		private bool							basicError						= false;
		private bool							errorMessage					= false;
		private bool							sundaySessionError				= false;
		private bool							startEndTimeError				= false;
		private amaPreSessionTypeOR				preSessionType					= amaPreSessionTypeOR.Full_Period;
		private amaTimeZonesOR					openingRangeTZSelector			= amaTimeZonesOR.Exchange_Time;
		private amaTimeZonesOR					preSessionTZSelector			= amaTimeZonesOR.Exchange_Time;
		private readonly List<int>				newSessionBarIdxArr				= new List<int>();
		private SessionIterator					sessionIterator					= null;
		private System.Windows.Media.Brush		regOpenBrush					= Brushes.Gold;
		private System.Windows.Media.Brush  	openingRangeHighBrush			= Brushes.Navy;
		private System.Windows.Media.Brush		openingRangeLowBrush			= Brushes.Navy;
		private System.Windows.Media.Brush  	openingRangeMidBrush			= Brushes.Navy;
		private System.Windows.Media.Brush		preSessionHighBrush				= Brushes.DarkSlateGray;
		private System.Windows.Media.Brush		preSessionLowBrush				= Brushes.DarkSlateGray;
		private System.Windows.Media.Brush  	preSessionMidBrush				= Brushes.DarkSlateGray;
		private System.Windows.Media.Brush		openingRangeBrushS				= Brushes.RoyalBlue;
		private System.Windows.Media.Brush		preSessionBrushS				= Brushes.DarkSlateGray;
		private System.Windows.Media.Brush		openingRangeBrush 				= null;
		private System.Windows.Media.Brush		preSessionBrush					= null;
		private System.Windows.Media.Brush		errorBrush						= null;
		private SimpleFont						errorFont						= null;
		private string							errorText1						= "The amaOpeningRange only works on price data.";
		private string							errorText2						= "The amaOpeningRange can only be displayed on intraday charts.";
		private string							errorText3						= "The amaOpeningRange cannot be used with a negative displacement.";
		private string							errorText4						= "The amaOpeningRange cannot be used with a displacement on non-equidistant chart bars.";
		private string							errorText5						= "The amaOpeningRange cannot be used when the Break EOD data series property is unselected.";
		private string							errorText6						= "amaOpeningRange: The opening range may not be calculated for fractional Sunday sessions. Please change your trading hours template.";
		private string							errorText7						= "amaOpeningRange: Mismatch between trading hours selected for the opening range / pre-session and the session template selected for the chart bars!";
		private int								plot0Width						= 4;
		private int								plot1Width						= 2;
		private int								plot3Width						= 2;
		private int								plot4Width						= 2;
		private int								plot6Width						= 2;
		private int								textFontSize					= 14;
		private int								shiftLabelOffset				= 10;
		private int								openingRangeOpacity				= 60;
		private int								preSessionOpacity				= 40;
		private PlotStyle						plot0Style						= PlotStyle.Line;
		private DashStyleHelper					dash0Style						= DashStyleHelper.Dot;
		private PlotStyle						plot1Style						= PlotStyle.Line;
		private DashStyleHelper					dash1Style						= DashStyleHelper.Solid;
		private PlotStyle						plot3Style						= PlotStyle.Line;
		private DashStyleHelper					dash3Style						= DashStyleHelper.Dot;
		private PlotStyle						plot4Style						= PlotStyle.Line;
		private DashStyleHelper					dash4Style						= DashStyleHelper.Solid;
		private PlotStyle						plot6Style						= PlotStyle.Line;
		private DashStyleHelper					dash6Style						= DashStyleHelper.Dot;
		private TimeZoneInfo					globalTimeZone					= Core.Globals.GeneralOptions.TimeZoneInfo;
		private TimeZoneInfo					timeZoneOR;
		private TimeZoneInfo					timeZonePS;
		private string							versionString					= "v 2.2  -  January 31, 2018";
		private Series<DateTime>				tradingDate;
		private Series<DateTime>				sessionBegin;
		private Series<DateTime>				anchorTimeOR;
		private Series<DateTime>				cutoffTimeOR;
		private Series<DateTime>				anchorTimePS;
		private Series<DateTime>				cutoffTimePS;
		private Series<bool>					calcOpenOR;
		private Series<bool>					initPlotOR;
		private Series<bool>					calcOpenPS;
		private Series<bool>					initPlotPS;
		private Series<double>					regOpen;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= "\r\n	/// The Opening Range indicator is designed to display the opening price and the opening range of the full or regular trading session." 
												+ " It further has an option to show the pre-session range.";
				Name						= "amaOpeningRange";
				IsSuspendedWhileInactive	= false;
				IsOverlay					= true;
				Calculate					= Calculate.OnPriceChange;
				IsAutoScale					= false;
				ArePlotsConfigurable		= false;
				AddPlot(new Stroke(Brushes.Gray, 2), PlotStyle.Line, "R-Open");	
				AddPlot(new Stroke(Brushes.Gray, 2), PlotStyle.Line, "OR-High");
				AddPlot(new Stroke(Brushes.Gray, 2), PlotStyle.Line, "OR-Low");
				AddPlot(new Stroke(Brushes.Gray, 2), PlotStyle.Line, "OR-Mid");	
				AddPlot(new Stroke(Brushes.Gray, 2), PlotStyle.Line, "Pre-High");
				AddPlot(new Stroke(Brushes.Gray, 2), PlotStyle.Line, "Pre-Low");
				AddPlot(new Stroke(Brushes.Gray, 2), PlotStyle.Line, "Pre-Mid");
			}
			else if (State == State.Configure)
			{
				displacement = Displacement;
				Plots[0].Brush = regOpenBrush.Clone();
				Plots[1].Brush = openingRangeHighBrush.Clone();
				Plots[2].Brush = openingRangeLowBrush.Clone();
				Plots[3].Brush = openingRangeMidBrush.Clone();
				Plots[4].Brush = preSessionHighBrush.Clone();
				Plots[5].Brush = preSessionLowBrush.Clone();
				Plots[6].Brush = preSessionMidBrush.Clone();
				Plots[0].Width = plot0Width;
				Plots[0].PlotStyle = plot0Style;
				Plots[0].DashStyleHelper = dash0Style;			
				Plots[1].Width = plot1Width;
				Plots[1].PlotStyle = plot1Style;
				Plots[1].DashStyleHelper = dash1Style;
				Plots[2].Width = plot1Width;
				Plots[2].PlotStyle = plot1Style;
				Plots[2].DashStyleHelper = dash1Style;
				Plots[3].Width = plot3Width;
				Plots[3].PlotStyle = plot3Style;
				Plots[3].DashStyleHelper = dash3Style;
				Plots[4].Width = plot4Width;
				Plots[4].PlotStyle = plot4Style;
				Plots[4].DashStyleHelper = dash4Style;
				Plots[5].Width = plot4Width;
				Plots[5].PlotStyle = plot4Style;
				Plots[5].DashStyleHelper = dash4Style;
				Plots[6].Width = plot6Width;
				Plots[6].PlotStyle = plot6Style;
				Plots[6].DashStyleHelper = dash6Style;
				openingRangeBrush	= openingRangeBrushS.Clone();
				openingRangeBrush.Opacity = (float) openingRangeOpacity/100.0;
				openingRangeBrush.Freeze();
				preSessionBrush	= preSessionBrushS.Clone();
				preSessionBrush.Opacity = (float) preSessionOpacity/100.0;
				preSessionBrush.Freeze();
			}
		  	else if (State == State.DataLoaded)
		 	{
				tradingDate = new Series<DateTime>(this, MaximumBarsLookBack.TwoHundredFiftySix);
				sessionBegin = new Series<DateTime>(this, MaximumBarsLookBack.TwoHundredFiftySix);
				anchorTimeOR = new Series<DateTime>(this, MaximumBarsLookBack.TwoHundredFiftySix);
				cutoffTimeOR = new Series<DateTime>(this, MaximumBarsLookBack.TwoHundredFiftySix);
				anchorTimePS = new Series<DateTime>(this, MaximumBarsLookBack.TwoHundredFiftySix);
				cutoffTimePS = new Series<DateTime>(this, MaximumBarsLookBack.TwoHundredFiftySix);
				calcOpenOR = new Series<bool>(this, MaximumBarsLookBack.TwoHundredFiftySix);
				initPlotOR = new Series<bool>(this, MaximumBarsLookBack.TwoHundredFiftySix);
				calcOpenPS = new Series<bool>(this, MaximumBarsLookBack.TwoHundredFiftySix);
				initPlotPS = new Series<bool>(this, MaximumBarsLookBack.TwoHundredFiftySix);
				regOpen = new Series<double>(this, MaximumBarsLookBack.TwoHundredFiftySix);
				if (Bars.BarsType.IsTimeBased) 
					timeBased = true;
				else
					timeBased = false;
				if(Input is PriceSeries)
					calculateFromPriceData = true;
				else
					calculateFromPriceData = false;
		    	sessionIterator = new SessionIterator(Bars);
		  	}
			else if (State == State.Historical)
			{
				switch (openingRangeTZSelector)
				{
					case amaTimeZonesOR.Exchange_Time:	
						timeZoneOR = Instrument.MasterInstrument.TradingHours.TimeZoneInfo;
						break;
					case amaTimeZonesOR.Chart_Time:	
						timeZoneOR = Core.Globals.GeneralOptions.TimeZoneInfo;
						break;
					case amaTimeZonesOR.US_Eastern_Standard_Time:	
						timeZoneOR = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");	
						break;
					case amaTimeZonesOR.US_Central_Standard_Time:	
						timeZoneOR = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");	
						break;
					case amaTimeZonesOR.US_Mountain_Standard_Time:	
						timeZoneOR = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");	
						break;
					case amaTimeZonesOR.US_Pacific_Standard_Time:	
						timeZoneOR = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");	
						break;
					case amaTimeZonesOR.AUS_Eastern_Standard_Time:	
						timeZoneOR = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");	
						break;
					case amaTimeZonesOR.Japan_Standard_Time:	
						timeZoneOR = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");	
						break;
					case amaTimeZonesOR.China_Standard_Time:	
						timeZoneOR = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");	
						break;
					case amaTimeZonesOR.India_Standard_Time:	
						timeZoneOR = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");	
						break;
					case amaTimeZonesOR.Central_European_Time:	
						timeZoneOR = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");	
						break;
					case amaTimeZonesOR.GMT_Standard_Time:	
						timeZoneOR = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");	
						break;
				}					
				switch (preSessionTZSelector)
				{
					case amaTimeZonesOR.Exchange_Time:	
						timeZonePS = Instrument.MasterInstrument.TradingHours.TimeZoneInfo;
						break;
					case amaTimeZonesOR.Chart_Time:	
						timeZonePS = Core.Globals.GeneralOptions.TimeZoneInfo;
						break;
					case amaTimeZonesOR.US_Eastern_Standard_Time:	
						timeZonePS = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");	
						break;
					case amaTimeZonesOR.US_Central_Standard_Time:	
						timeZonePS = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");	
						break;
					case amaTimeZonesOR.US_Mountain_Standard_Time:	
						timeZonePS = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");	
						break;
					case amaTimeZonesOR.US_Pacific_Standard_Time:	
						timeZonePS = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");	
						break;
					case amaTimeZonesOR.AUS_Eastern_Standard_Time:	
						timeZonePS = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");	
						break;
					case amaTimeZonesOR.Japan_Standard_Time:	
						timeZonePS = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");	
						break;
					case amaTimeZonesOR.China_Standard_Time:	
						timeZonePS = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");	
						break;
					case amaTimeZonesOR.India_Standard_Time:	
						timeZonePS = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");	
						break;
					case amaTimeZonesOR.Central_European_Time:	
						timeZonePS = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");	
						break;
					case amaTimeZonesOR.GMT_Standard_Time:	
						timeZonePS = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");	
						break;
				}					
				plotOR = false;
				plotPS = false;
				if(ChartBars != null)
				{	
					breakAtEOD = ChartBars.Bars.IsResetOnNewTradingDay;
					errorBrush = ChartControl.Properties.AxisPen.Brush;
					errorBrush.Freeze();
					errorFont = new SimpleFont("Arial", 24);
				}
				this.ZOrder = -5;
				basicError = false;
				errorMessage = false;
				if(!calculateFromPriceData)
				{
					Draw.TextFixed(this, "error text 1", errorText1, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);  
					errorMessage = true;
					basicError = true;
				}	
				else if (!Bars.BarsType.IsIntraday)
				{
					Draw.TextFixed(this, "error text 2", errorText2, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);  
					errorMessage = true;
					basicError = true;
				}
				else if(displacement < 0)
				{
					Draw.TextFixed(this, "error text 3", errorText3, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);  
					errorMessage = true;
					basicError = true;
				}
				else if (ChartBars != null && (ChartControl.BarSpacingType == BarSpacingType.TimeBased || ChartControl.BarSpacingType == BarSpacingType.EquidistantMulti) && displacement != 0)
				{
					Draw.TextFixed(this, "error text 4", errorText4, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);  
					errorMessage = true;
					basicError = true;
				}	
				else if(!breakAtEOD)
				{
					Draw.TextFixed(this, "error text 5", errorText5, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);  
					errorMessage = true;
					basicError = true;
				}
				sundaySessionError = false;
				startEndTimeError = false;
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
						RemoveDrawObject("error text 7");
						return;
					}	
					else if(startEndTimeError)
					{	
						Draw.TextFixed(this, "error text 7", errorText7, TextPosition.Center, errorBrush, errorFont, Brushes.Transparent, Brushes.Transparent, 0);
						return;
					}
				}	
			}
			
			if (CurrentBar == 0)
			{	
				if(IsFirstTickOfBar)
				{	
					tradingDate[0] = GetLastBarSessionDate(Time[0]);
					sessionBegin[0] = sessionIterator.ActualSessionBegin;
					anchorTimeOR[0] = Globals.MinDate;
					cutoffTimeOR[0] = Globals.MinDate;
					anchorTimePS[0] = Globals.MinDate;
					cutoffTimePS[0] = Globals.MinDate;
					calcOpenOR[0] = false;
					initPlotOR[0] = false;
					calcOpenPS[0] = false;
					initPlotPS[0] = false;
					anchorBarOR = false;
					anchorBarPS = false;
					RegOpen.Reset();
					OpeningRangeHigh.Reset();
					OpeningRangeLow.Reset();
					OpeningRangeMid.Reset();
					PreSessionHigh.Reset();
					PreSessionLow.Reset();
					PreSessionMid.Reset();
				}	
				return;
			}
			if(IsFirstTickOfBar)
			{	
				if(Bars.IsFirstBarOfSession)
				{	
					tradingDate[0] = GetLastBarSessionDate(Time[0]);
					if(tradingDate[0].DayOfWeek == DayOfWeek.Sunday)
					{
						sundaySessionError = true; 
						errorMessage = true;
						return;
					}
					sessionBegin[0] = sessionIterator.ActualSessionBegin;
					if(tradingDate[0] != tradingDate[1])
					{	
						plotOR = true;
						calcOpenOR[0] = false;
						initPlotOR[0] = false;
						anchorTimeOR[0] = TimeZoneInfo.ConvertTime(tradingDate[0].Add(openingRangeStart), timeZoneOR, globalTimeZone);
						if(anchorTimeOR[0] >= sessionBegin[0].AddHours(24))
							anchorTimeOR[0] = anchorTimeOR[0].AddHours(-24);
						else if(anchorTimeOR[0] < sessionBegin[0])
							anchorTimeOR[0] = anchorTimeOR[0].AddHours(24);
						cutoffTimeOR[0] = TimeZoneInfo.ConvertTime(tradingDate[0].Add(openingRangeEnd), timeZoneOR, globalTimeZone);
						if(cutoffTimeOR[0] > sessionBegin[0].AddHours(24))
							cutoffTimeOR[0] = cutoffTimeOR[0].AddHours(-24);
						else if(cutoffTimeOR[0] <= sessionBegin[0])
							cutoffTimeOR[0] = cutoffTimeOR[0].AddHours(24);
						if(cutoffTimeOR[0] <= anchorTimeOR[0])
						{
							startEndTimeError = true;
							errorMessage = true;
							return;
						}
						plotPS = true;
						calcOpenPS[0] = false;
						initPlotPS[0] = false;
						if(preSessionType == amaPreSessionTypeOR.Full_Period)
							anchorTimePS[0] = sessionBegin[0];
						else
						{	
							anchorTimePS[0] = TimeZoneInfo.ConvertTime(tradingDate[0].Add(preSessionStart), timeZonePS, globalTimeZone);
							if(anchorTimePS[0] >= sessionBegin[0].AddHours(24))
								anchorTimePS[0] = anchorTimePS[0].AddHours(-24);
							else if(anchorTimePS[0] < sessionBegin[0])
								anchorTimePS[0] = anchorTimePS[0].AddHours(24);
						}	
						if(preSessionType == amaPreSessionTypeOR.Full_Period)
							cutoffTimePS[0] = anchorTimeOR[0];
						else
						{	
							cutoffTimePS[0] = TimeZoneInfo.ConvertTime(tradingDate[0].Add(preSessionEnd), timeZonePS, globalTimeZone);
							if(cutoffTimePS[0] > sessionBegin[0].AddHours(24))
								cutoffTimePS[0] = cutoffTimePS[0].AddHours(-24);
							else if(cutoffTimePS[0] <= sessionBegin[0])
								cutoffTimePS[0] = cutoffTimePS[0].AddHours(24);
							if(cutoffTimePS[0] < anchorTimePS[0])
							{
								startEndTimeError = true;
								errorMessage = true;
								return;
							}
						}	
						if(cutoffTimePS[0] <= anchorTimePS[0])
							plotPS = false;
						regOpen.Reset();
					}	
					else
					{	
						calcOpenOR[0] = calcOpenOR[1];
						initPlotOR[0] = initPlotOR[1];
						calcOpenPS[0] = calcOpenPS[1];
						initPlotPS[0] = initPlotPS[1];
						anchorTimeOR[0] = anchorTimeOR[1];
						cutoffTimeOR[0] = cutoffTimeOR[1];
						anchorTimePS[0] = anchorTimePS[1];
						cutoffTimePS[0] = cutoffTimePS[1];
						regOpen[0] = regOpen[1];
					}	
				}	
				else
				{	
					tradingDate[0] = tradingDate[1];
					sessionBegin[0] = sessionBegin[1];
					calcOpenOR[0] = calcOpenOR[1];
					initPlotOR[0] = initPlotOR[1];
					calcOpenPS[0] = calcOpenPS[1];
					initPlotPS[0] = initPlotPS[1];
					anchorTimeOR[0] = anchorTimeOR[1];
					cutoffTimeOR[0] = cutoffTimeOR[1];
					anchorTimePS[0] = anchorTimePS[1];
					cutoffTimePS[0] = cutoffTimePS[1];
					regOpen[0] = regOpen[1];
				}	
			}	

			if(plotOR)
			{	
				if(timeBased && Time[0] > anchorTimeOR[0] && Time[1] <= anchorTimeOR[0])
					anchorBarOR = true;
				else if(!timeBased && Time[0] >= anchorTimeOR[0] && Time[1] < anchorTimeOR[0])
					anchorBarOR = true;
				else
					anchorBarOR = false;
				if(timeBased && Time[0] > cutoffTimeOR[0] && calcOpenOR[1])
					calcOpenOR[0] = false;
				else if(!timeBased && Time[0] >= cutoffTimeOR[0] && calcOpenOR[1])
					calcOpenOR[0] = false;
				if(anchorBarOR)
				{	
					RegOpen[0]			= Open[0];
					OpeningRangeHigh[0]	= High[0];
					OpeningRangeLow[0]	= Low[0];
					OpeningRangeMid[0] 	= (OpeningRangeHigh[0] + OpeningRangeLow[0])/2.0;
					calcOpenOR[0] 		= true;
					initPlotOR[0]		= true;
				}
				else if(calcOpenOR[0])
				{
					if(IsFirstTickOfBar)
						RegOpen[0]		= RegOpen[1];
					OpeningRangeHigh[0]	= Math.Max(OpeningRangeHigh[1], High[0]);
					OpeningRangeLow[0]	= Math.Min(OpeningRangeLow[1], Low[0]);
					OpeningRangeMid[0] 	= (OpeningRangeHigh[0] + OpeningRangeLow[0])/2.0;
				}
				else if(IsFirstTickOfBar && initPlotOR[0])
				{
					RegOpen[0] = RegOpen[1];
					OpeningRangeHigh[0] = OpeningRangeHigh[1];
					OpeningRangeLow[0] 	= OpeningRangeLow[1];
					OpeningRangeMid[0] 	= OpeningRangeMid[1];
				}
				else if (IsFirstTickOfBar)
				{		
					RegOpen.Reset();
					OpeningRangeHigh.Reset();
					OpeningRangeLow.Reset();
					OpeningRangeMid.Reset();
				}
				
				if(initPlotOR[0])
				{
					if(!showRegOpen)
						PlotBrushes[0][0] = Brushes.Transparent;
					if(!showOpeningRange) 
					{	
						PlotBrushes[1][0] = Brushes.Transparent;
						PlotBrushes[2][0] = Brushes.Transparent;
					}	
					if(!showOpeningRange || !showOpeningRangeMidline) 
						PlotBrushes[3][0] = Brushes.Transparent;
				}
			}	
			else if (IsFirstTickOfBar)
			{		
				RegOpen.Reset();
				OpeningRangeHigh.Reset();
				OpeningRangeLow.Reset();
				OpeningRangeMid.Reset();
			}
			
			if(plotPS)
			{	
				if(timeBased && Time[0] > anchorTimePS[0] && Time[1] <= anchorTimePS[0])
					anchorBarPS = true;
				else if(!timeBased && Time[0] >= anchorTimePS[0] && Time[1] < anchorTimePS[0])
					anchorBarPS = true;
				else
					anchorBarPS = false;
				if(timeBased && Time[0] > cutoffTimePS[0] && calcOpenPS[1])
					calcOpenPS[0] = false;
				else if(!timeBased && Time[0] >= cutoffTimePS[0] && calcOpenPS[1])
					calcOpenPS[0] = false;			
				if(anchorBarPS)
				{	
					PreSessionHigh[0]	= High[0];
					PreSessionLow[0]	= Low[0];
					PreSessionMid[0] 	= (PreSessionHigh[0] + PreSessionLow[0])/2.0;
					calcOpenPS[0]		= true;
					initPlotPS[0]		= true;
				}
				else if(calcOpenPS[0])
				{
					PreSessionHigh[0]	= Math.Max(PreSessionHigh[1], High[0]);
					PreSessionLow[0]	= Math.Min(PreSessionLow[1], Low[0]);
					PreSessionMid[0] 	= (PreSessionHigh[0] + PreSessionLow[0])/2.0;
				}
				else if(IsFirstTickOfBar && initPlotPS[0])
				{	
					PreSessionHigh[0] 	= PreSessionHigh[1];
					PreSessionLow[0] 	= PreSessionLow[1];
					PreSessionMid[0] 	= PreSessionMid[1];
				}	
				else if (IsFirstTickOfBar)
				{		
					PreSessionHigh.Reset();
					PreSessionLow.Reset();
				}
				
				if(initPlotPS[0])
				{
					if(!showPreSession) 
					{	
						PlotBrushes[4][0] = Brushes.Transparent;
						PlotBrushes[5][0] = Brushes.Transparent;
					}	
					if(!showPreSession || !showPreSessionMidline) 
						PlotBrushes[6][0] = Brushes.Transparent;
				}
			}
			else if (IsFirstTickOfBar)
			{		
				PreSessionHigh.Reset();
				PreSessionLow.Reset();
			}	
		}
		
		#region Properties
		[Browsable(false)]
		[XmlIgnore()]
		public Series<double> RegOpen
		{
			get { return Values[0]; }
		}
		
		[Browsable(false)]
		[XmlIgnore()]
		public Series<double> OpeningRangeHigh
		{
			get { return Values[1]; }
		}
		
		[Browsable(false)]
		[XmlIgnore()]
		public Series<double> OpeningRangeLow
		{
			get { return Values[2]; }
		}
		
		[Browsable(false)]
		[XmlIgnore()]
		public Series<double> OpeningRangeMid
		{
			get { return Values[3]; }
		}
		
		[Browsable(false)]
		[XmlIgnore()]
		public Series<double> PreSessionHigh
		{
			get { return Values[4]; }
		}
		
		[Browsable(false)]
		[XmlIgnore()]
		public Series<double> PreSessionLow
		{
			get { return Values[5]; }
		}
		
		[Browsable(false)]
		[XmlIgnore()]
		public Series<double> PreSessionMid
		{
			get { return Values[6]; }
		}
		
		[NinjaScriptProperty] 
		[Display(ResourceType = typeof(Custom.Resource), Name="Select time zone", Description="Enter time zone for opening range start and end time", GroupName="Opening Range", Order = 0)]
		public amaTimeZonesOR OpeningRangeTZSelector
		{
			get
			{
				return openingRangeTZSelector;
			}
			set
			{
				openingRangeTZSelector = value;
			}
		}
			
		[Browsable(false)]
		[XmlIgnore]
		public TimeSpan OpeningRangeStart
		{
			get { return openingRangeStart;}
			set { openingRangeStart = value;}
		}	
	
		[NinjaScriptProperty] 
		[Display(ResourceType = typeof(Custom.Resource), Name="Opening start time (+ h:min)", Description="Enter start time for opening range calculation in selected time zone", GroupName="Opening Range", Order = 1)]
		public string S_OpeningRangeStart	
		{
			get 
			{ 
				return string.Format("{0:D2}:{1:D2}", openingRangeStart.Hours, openingRangeStart.Minutes);
			}
			set 
			{ 
				char[] delimiters = new char[] {':'};
				string[]values =((string)value).Split(delimiters, StringSplitOptions.None);
				openingRangeStart = new TimeSpan(Convert.ToInt16(values[0]),Convert.ToInt16(values[1]),0);
			}
		}
	
		[Browsable(false)]
		[XmlIgnore]
		public TimeSpan OpeningRangeEnd
		{
			get { return openingRangeEnd;}
			set { openingRangeEnd = value;}
		}	
	
		[NinjaScriptProperty] 
		[Display(ResourceType = typeof(Custom.Resource), Name="Opening end time (+ h:min)", Description="Enter end time for opening range calculation in selected time zone", GroupName="Opening Range", Order = 2)]
		public string S_OpeningRangeEnd	
		{
			get 
			{ 
				return string.Format("{0:D2}:{1:D2}", openingRangeEnd.Hours, openingRangeEnd.Minutes);
			}
			set 
			{ 
				char[] delimiters = new char[] {':'};
				string[]values =((string)value).Split(delimiters, StringSplitOptions.None);
				openingRangeEnd = new TimeSpan(Convert.ToInt16(values[0]),Convert.ToInt16(values[1]),0);
			}
		}
		
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Select pre-session", Description = "Select pre-session mode", GroupName = "Pre-Session Range", Order = 0)]
		[RefreshProperties(RefreshProperties.All)] 
		public amaPreSessionTypeOR PreSessionType
		{	
            get { return preSessionType; }
            set { preSessionType = value; }
		}
			
		[NinjaScriptProperty] 
		[Display(ResourceType = typeof(Custom.Resource), Name="Select time zone", Description="Enter time zone for pre-session start and end time", GroupName="Pre-Session Range", Order = 1)]
		public amaTimeZonesOR PreSessionTZSelector
		{
			get
			{
				return preSessionTZSelector;
			}
			set
			{
				preSessionTZSelector = value;
			}
		}
			
		[Browsable(false)]
		[XmlIgnore]
		public TimeSpan PreSessionStart
		{
			get { return preSessionStart;}
			set { preSessionStart = value;}
		}	
	
		[NinjaScriptProperty] 
		[Display(ResourceType = typeof(Custom.Resource), Name="Pre-session start time (+ h:min)", Description="Enter start time for pre-session range calculation in selected time zone", GroupName="Pre-Session Range", Order = 2)]
		public string S_PreSessionStart	
		{
			get 
			{ 
				return string.Format("{0:D2}:{1:D2}", preSessionStart.Hours, preSessionStart.Minutes);
			}
			set 
			{ 
				char[] delimiters = new char[] {':'};
				string[]values =((string)value).Split(delimiters, StringSplitOptions.None);
				preSessionStart = new TimeSpan(Convert.ToInt16(values[0]),Convert.ToInt16(values[1]),0);
			}
		}
	
		[Browsable(false)]
		[XmlIgnore]
		public TimeSpan PreSessionEnd
		{
			get { return preSessionEnd;}
			set { preSessionEnd = value;}
		}	
	
		[NinjaScriptProperty] 
		[Display(ResourceType = typeof(Custom.Resource), Name="Pre-session end time (+ h:min)", Description="Enter end time for pre-session range calculation in selected time zone", GroupName="Pre-Session Range", Order = 3)]
		public string S_PreSessionEnd	
		{
			get 
			{ 
				return string.Format("{0:D2}:{1:D2}", preSessionEnd.Hours, preSessionEnd.Minutes);
			}
			set 
			{ 
				char[] delimiters = new char[] {':'};
				string[]values =((string)value).Split(delimiters, StringSplitOptions.None);
				preSessionEnd = new TimeSpan(Convert.ToInt16(values[0]),Convert.ToInt16(values[1]),0);
			}
		}
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Show regular open", Description = "Shows the regular open price", GroupName = "Display Options", Order = 0)]
    	[RefreshProperties(RefreshProperties.All)] 
        public bool ShowRegOpen
        {
            get { return showRegOpen; }
            set { showRegOpen = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Show opening range", Description = "Shows the opening range", GroupName = "Display Options", Order = 1)]
    	[RefreshProperties(RefreshProperties.All)] 
        public bool ShowOpeningRange
        {
            get { return showOpeningRange; }
            set { showOpeningRange = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Show opening range midline", Description = "Shows the midline of the opening range", GroupName = "Display Options", Order = 2)]
    	[RefreshProperties(RefreshProperties.All)] 
        public bool ShowOpeningRangeMidline
        {
            get { return showOpeningRangeMidline; }
            set { showOpeningRangeMidline = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Show pre-session", Description = "Shows the pre-session", GroupName = "Display Options", Order = 3)]
    	[RefreshProperties(RefreshProperties.All)] 
        public bool ShowPreSession
        {
            get { return showPreSession; }
            set { showPreSession = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Show pre-session midline", Description = "Shows the pre-session", GroupName = "Display Options", Order = 4)]
    	[RefreshProperties(RefreshProperties.All)] 
        public bool ShowPreSessionMidline
        {
            get { return showPreSessionMidline; }
            set { showPreSessionMidline = value; }
        }
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Show labels", Description = "Shows labels for all plots", GroupName = "Display Options", Order = 5)]
  		[RefreshProperties(RefreshProperties.All)] 
       	public bool ShowLabels
        {
            get { return showLabels; }
            set { showLabels = value; }
        }
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Reg open", Description = "Sets the color for the regular open", GroupName = "Plot Colors", Order = 0)]
		public System.Windows.Media.Brush RegOpenBrush
		{ 
			get {return regOpenBrush;}
			set {regOpenBrush = value;}
		}

		[Browsable(false)]
		public string RegOpenBrushSerializable
		{
			get { return Serialize.BrushToString(regOpenBrush); }
			set { regOpenBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Opening range high", Description = "Sets the color for the opening range high", GroupName = "Plot Colors", Order = 1)]
		public System.Windows.Media.Brush OpeningRangeHighBrush
		{ 
			get {return openingRangeHighBrush;}
			set {openingRangeHighBrush = value;}
		}

		[Browsable(false)]
		public string OpeningRangeHighBrushSerializable
		{
			get { return Serialize.BrushToString(openingRangeHighBrush); }
			set { openingRangeHighBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Opening range low", Description = "Sets the color for the opening range low", GroupName = "Plot Colors", Order = 2)]
		public System.Windows.Media.Brush OpeningRangeLowBrush
		{ 
			get {return openingRangeLowBrush;}
			set {openingRangeLowBrush = value;}
		}

		[Browsable(false)]
		public string OpeningRangeLowBrushSerializable
		{
			get { return Serialize.BrushToString(openingRangeLowBrush); }
			set { openingRangeLowBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Opening range midline", Description = "Sets the color for the opening range midline", GroupName = "Plot Colors", Order = 3)]
		public System.Windows.Media.Brush OpeningRangeMidBrush
		{ 
			get {return openingRangeMidBrush;}
			set {openingRangeMidBrush = value;}
		}

		[Browsable(false)]
		public string OpeningRangeMidBrushSerializable
		{
			get { return Serialize.BrushToString(openingRangeMidBrush); }
			set { openingRangeMidBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Pre-session high", Description = "Sets the color for the pre-session high", GroupName = "Plot Colors", Order = 4)]
		public System.Windows.Media.Brush PreSessionHighBrush
		{ 
			get {return preSessionHighBrush;}
			set {preSessionHighBrush = value;}
		}

		[Browsable(false)]
		public string PreSessionHighBrushSerializable
		{
			get { return Serialize.BrushToString(preSessionHighBrush); }
			set { preSessionHighBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Pre-session low", Description = "Sets the color for the pre-session low", GroupName = "Plot Colors", Order = 5)]
		public System.Windows.Media.Brush PreSessionLowBrush
		{ 
			get {return preSessionLowBrush;}
			set {preSessionLowBrush = value;}
		}

		[Browsable(false)]
		public string PreSessionLowBrushSerializable
		{
			get { return Serialize.BrushToString(preSessionLowBrush); }
			set { preSessionLowBrush = Serialize.StringToBrush(value); }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Pre-session midline", Description = "Sets the color for the pre-session midline", GroupName = "Plot Colors", Order = 6)]
		public System.Windows.Media.Brush PreSessionMidBrush
		{ 
			get {return preSessionMidBrush;}
			set {preSessionMidBrush = value;}
		}

		[Browsable(false)]
		public string PreSessionMidBrushSerializable
		{
			get { return Serialize.BrushToString(preSessionMidBrush); }
			set { preSessionMidBrush = Serialize.StringToBrush(value); }
		}

		[Display(ResourceType = typeof(Custom.Resource), Name = "Dash style reg open", Description = "Sets the dash style for the regular open plot", GroupName = "Plot Parameters", Order = 1)]
		public DashStyleHelper Dash0Style
		{
			get { return dash0Style; }
			set { dash0Style = value; }
		}
		
		[Range(1, int.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Plot width reg open", Description = "Sets the plot width for the regular open plot", GroupName = "Plot Parameters", Order = 2)]
		public int Plot0Width
		{	
            get { return plot0Width; }
            set { plot0Width = value; }
		}
			
		[Display(ResourceType = typeof(Custom.Resource), Name = "Dash style OR range", Description = "Sets the dash style for the opening range plots", GroupName = "Plot Parameters", Order = 4)]
		public DashStyleHelper Dash1Style
		{
			get { return dash1Style; }
			set { dash1Style = value; }
		}
		
		[Range(1, int.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Plot width OR range", Description = "Sets the plot width for the opening range plots", GroupName = "Plot Parameters", Order = 5)]
		public int Plot1Width
		{	
            get { return plot1Width; }
            set { plot1Width = value; }
		}
		
		[Display(ResourceType = typeof(Custom.Resource), Name = "Dash style OR midline", Description = "Sets the dash style for the opening range midline", GroupName = "Plot Parameters", Order = 7)]
		public DashStyleHelper Dash3Style
		{
			get { return dash3Style; }
			set { dash3Style = value; }
		}
		
		[Range(1, int.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Plot width OR midline", Description = "Sets the plot width for the opening range midline", GroupName = "Plot Parameters", Order = 8)]
		public int Plot3Width
		{	
            get { return plot3Width; }
            set { plot3Width = value; }
		}
			
		[Display(ResourceType = typeof(Custom.Resource), Name = "Dash style pre-session range", Description = "Sets the dash style for the pre-session range", GroupName = "Plot Parameters", Order = 10)]
		public DashStyleHelper Dash4Style
		{
			get { return dash4Style; }
			set { dash4Style = value; }
		}
		
		[Range(1, int.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Plot width pre-session range", Description = "Sets the plot width for the pre-session range", GroupName = "Plot Parameters", Order = 11)]
		public int Plot4Width
		{	
            get { return plot4Width; }
            set { plot4Width = value; }
		}
			
		[Display(ResourceType = typeof(Custom.Resource), Name = "Dash style pre-session midline", Description = "Sets the dash style for the pre-session midline", GroupName = "Plot Parameters", Order = 13)]
		public DashStyleHelper Dash6Style
		{
			get { return dash6Style; }
			set { dash6Style = value; }
		}
		
		[Range(1, int.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Plot width pre-session midline", Description = "Sets the plot width for the pre-session midline", GroupName = "Plot Parameters", Order = 14)]
		public int Plot6Width
		{	
            get { return plot6Width; }
            set { plot6Width = value; }
		}
		
		[Range(10, 40)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Label font size", Description = "Sets the size for all plot labels", GroupName = "Plot Parameters", Order = 15)]
		public int TextFontSize
		{	
            get { return textFontSize; }
            set { textFontSize = value; }
		}
		
		[Range(0, int.MaxValue)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Label offset", Description = "Allows for shifting the labels further to the right", GroupName = "Plot Parameters", Order = 16)]
		public int ShiftLabelOffset
		{	
            get { return shiftLabelOffset; }
            set { shiftLabelOffset = value; }
		}
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Opening range shading", Description = "Sets the color for the opening range shading", GroupName = "Area Colors", Order = 0)]
		public System.Windows.Media.Brush OpeningRangeBrushS
		{ 
			get {return openingRangeBrushS;}
			set {openingRangeBrushS = value;}
		}

		[Browsable(false)]
		public string OpeningRangeBrushSSerializable
		{
			get { return Serialize.BrushToString(openingRangeBrushS); }
			set { openingRangeBrushS = Serialize.StringToBrush(value); }
		}
		
		[Range(0, 100)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Opening range opacity", Description = "Select channel opacity between 0 (transparent) and 100 (no opacity)", GroupName = "Area Colors", Order = 1)]
        public int OpeningRangeOpacity
        {
            get { return openingRangeOpacity; }
            set { openingRangeOpacity = value; }
        }
		
		[XmlIgnore]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Pre-session shading", Description = "Sets the color for the pre-session shading", GroupName = "Area Colors", Order = 2)]
		public System.Windows.Media.Brush PreSessionBrushS
		{ 
			get {return preSessionBrushS;}
			set {preSessionBrushS = value;}
		}

		[Browsable(false)]
		public string PreSessionBrushSSerializable
		{
			get { return Serialize.BrushToString(preSessionBrushS); }
			set { preSessionBrushS = Serialize.StringToBrush(value); }
		}
		
		[Range(0, 100)]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Pre-session opacity", Description = "Select channel opacity between 0 (transparent) and 100 (no opacity)", GroupName = "Area Colors", Order = 3)]
        public int PreSessionOpacity
        {
            get { return preSessionOpacity; }
            set { preSessionOpacity = value; }
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
		
		public override string FormatPriceMarker(double price)
		{
			return Instrument.MasterInstrument.FormatPrice(Instrument.MasterInstrument.RoundToTickSize(price));
		}
		
		private DateTime GetLastBarSessionDate(DateTime time)
		{
			sessionIterator.CalculateTradingDay(time, timeBased);
			sessionDateTmp = sessionIterator.ActualTradingDayExchange;
			if(cacheSessionDate != sessionDateTmp) 
			{
				cacheSessionDate = sessionDateTmp;
				if (newSessionBarIdxArr.Count == 0 || (newSessionBarIdxArr.Count > 0 && CurrentBar > (int) newSessionBarIdxArr[newSessionBarIdxArr.Count - 1]))
						newSessionBarIdxArr.Add(CurrentBar);
			}
			return sessionDateTmp;			
		}
		
		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (ChartBars == null || BarsArray[0].Count < ChartBars.ToIndex || errorMessage || !IsVisible) return;

			SharpDX.Direct2D1.Brush openingRangeBrushDX = openingRangeBrush.ToDxBrush(RenderTarget);
			SharpDX.Direct2D1.Brush preSessionBrushDX = preSessionBrush.ToDxBrush(RenderTarget);
			SharpDX.Direct2D1.SolidColorBrush transparentBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.Transparent);
			SharpDX.Direct2D1.Brush[] brushesDX = new SharpDX.Direct2D1.Brush[Values.Length];
			SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;
			TextFormat textFormat = new TextFormat(Globals.DirectWriteFactory, "Arial", (float)this.TextFontSize);		

			bool nonEquidistant 			= (chartControl.BarSpacingType == BarSpacingType.TimeBased || chartControl.BarSpacingType == BarSpacingType.EquidistantMulti);
			bool firstLoop					= true;
			bool breakFound					= false;
			int	lastBarPainted 	 			= ChartBars.ToIndex;
			int lastBarCounted				= Inputs[0].Count - 1;
			int	lastBarOnUpdate				= lastBarCounted - (Calculate == Calculate.OnBarClose ? 1 : 0);
			int	lastBarIndex				= Math.Min(lastBarPainted, lastBarOnUpdate);
			int lastPlotCalcIndex			= lastBarIndex - displacement;
			int firstBarPainted	 			= ChartBars.FromIndex;
			int firstBarIndex  	 			= Math.Max(BarsRequiredToPlot, firstBarPainted);
			int firstBarIdxToPaint  		= 0;
			int lastPlotIndex				= 0;
			int firstPlotIndex				= 0;
			int firstIndex					= 0;
			int	resolution					= 1 + this.TextFontSize/2;
			double barWidth					= chartControl.GetBarPaintWidth(chartControl.BarsArray[0]);
			double smallGap					= 2.0;
			double labelOffset				= (this.TextFontSize + barWidth)/2.0 + shiftLabelOffset;
			double x						= 0.0;
			double firstX					= 0.0;
			double lastX					= 0.0;
			double y						= 0.0;
			double yH						= 0.0;
			double yL						= 0.0;
			double[] yArr					= new double[Values.Length];
			float width						= 0.0f;
			float height					= 0.0f;
			float textFontSize				= textFormat.FontSize;
			string[] plotLabels				= new string[Values.Length];
			SharpDX.Vector2 startPointDX;
			SharpDX.Vector2 endPointDX;
			SharpDX.Vector2 textPointDX;
			SharpDX.RectangleF rect;
			
			if(displacement > 0 && nonEquidistant)
				return;
			if(lastBarIndex + displacement >= firstBarIndex)
			{	
				if (displacement > 0 && lastBarIndex < lastBarOnUpdate)
					lastPlotIndex = lastBarIndex + 1;
				else if (displacement > 0 && lastBarIndex == lastBarOnUpdate)
				{	
					lastPlotIndex = lastBarIndex + displacement;
					for(int i = 0; i < displacement; i++)
					{
						x = ChartControl.GetXByBarIndex(ChartBars, lastPlotIndex);
						if(x > ChartPanel.X + ChartPanel.W + 1.5*barWidth - ChartControl.Properties.BarMarginRight)
							lastPlotIndex = lastPlotIndex - 1;
						else
							break;
					}	
				}	
				else
					lastPlotIndex = lastBarIndex;
			
				do
				{
					breakFound = false;
					for (int i = newSessionBarIdxArr.Count - 1; i >= 0; i--)
					{
						int prevSessionBreakIdx = newSessionBarIdxArr[i];
						if (prevSessionBreakIdx + displacement <= lastPlotIndex)
						{
							firstBarIdxToPaint = prevSessionBreakIdx + displacement;
							if(i > 0)
								breakFound = true;
							break;
						}
					}
					if(!breakFound)
						return;
					firstPlotIndex = Math.Max(firstBarIndex, firstBarIdxToPaint);
					if(!Values[0].IsValidDataPointAt(lastPlotCalcIndex) && !Values[1].IsValidDataPointAt(lastPlotCalcIndex) && !Values[4].IsValidDataPointAt(lastPlotCalcIndex))
					{	
						lastPlotIndex = lastPlotIndex - 1;
						lastPlotCalcIndex = lastPlotCalcIndex - 1;
						continue;
					}	
					lastX = ChartControl.GetXByBarIndex(ChartBars, lastPlotIndex);
					
					
					for (int seriesCount = 0; seriesCount < Values.Length; seriesCount++)
					{
						y						= Values[seriesCount].GetValueAt(lastPlotCalcIndex);
						yArr[seriesCount] 		= chartScale.GetYByValue(y);
						plotLabels[seriesCount] = Plots[seriesCount].Name;
						brushesDX[seriesCount] 	= Plots[seriesCount].BrushDX;
					}
					
					if(showOpeningRange && showPreSession)
					{
						if (Math.Abs(yArr[6] - yArr[1]) < resolution && showPreSessionMidline)
						{
							plotLabels[1] = plotLabels[1] + "/" + plotLabels[6]; 
							plotLabels[6] = "";
							brushesDX[6] = transparentBrushDX;
						}
						else if (Math.Abs(yArr[5] - yArr[1]) < resolution)
						{
							plotLabels[1] = plotLabels[1] + "/" + plotLabels[5]; 
							plotLabels[5] = "";
							brushesDX[5] = transparentBrushDX;
						}
						else if (Math.Abs(yArr[4] - yArr[1]) < resolution)
						{
							plotLabels[1] = plotLabels[1] + "/" + plotLabels[4]; 
							plotLabels[4] = "";
							brushesDX[4] = transparentBrushDX;
						}
						if (Math.Abs(yArr[6] - yArr[2]) < resolution && showPreSessionMidline)
						{
							plotLabels[2] = plotLabels[2] + "/" + plotLabels[6]; 
							plotLabels[6] = "";
							brushesDX[6] = transparentBrushDX;
						}
						else if (Math.Abs(yArr[5] - yArr[2]) < resolution)
						{
							plotLabels[2] = plotLabels[2] + "/" + plotLabels[5]; 
							plotLabels[5] = "";
							brushesDX[5] = transparentBrushDX;
						}
						else if (Math.Abs(yArr[4] - yArr[2]) < resolution)
						{
							plotLabels[2] = plotLabels[2] + "/" + plotLabels[4]; 
							plotLabels[4] = "";
							brushesDX[4] = transparentBrushDX;
						}
						if(showOpeningRangeMidline)
						{	
							if (Math.Abs(yArr[6] - yArr[3]) < resolution && showPreSessionMidline)
							{
								plotLabels[3] = plotLabels[3] + "/" + plotLabels[6]; 
								plotLabels[6] = "";
								brushesDX[6] = transparentBrushDX;
							}
							else if (Math.Abs(yArr[5] - yArr[3]) < resolution)
							{
								plotLabels[3] = plotLabels[3] + "/" + plotLabels[5]; 
								plotLabels[5] = "";
								brushesDX[5] = transparentBrushDX;
							}
							else if (Math.Abs(yArr[4] - yArr[3]) < resolution)
							{
								plotLabels[3] = plotLabels[3] + "/" + plotLabels[4]; 
								plotLabels[4] = "";
								brushesDX[4] = transparentBrushDX;
							}
						}
					}
					if(showRegOpen)
					{
							if (Math.Abs(yArr[1] - yArr[0]) < resolution && showOpeningRange)
							{
								plotLabels[0] = plotLabels[0] + "/" + plotLabels[1]; 
								plotLabels[1] = "";
								brushesDX[1] = transparentBrushDX;
							}
							else if (Math.Abs(yArr[2] - yArr[0]) < resolution && showOpeningRange)
							{
								plotLabels[0] = plotLabels[0] + "/" + plotLabels[2]; 
								plotLabels[2] = "";
								brushesDX[2] = transparentBrushDX;
							}
							else if (Math.Abs(yArr[3] - yArr[0]) < resolution && showOpeningRange && showOpeningRangeMidline)
							{
								plotLabels[0] = plotLabels[0] + "/" + plotLabels[3]; 
								plotLabels[3] = "";
								brushesDX[3] = transparentBrushDX;
							}
							else if (Math.Abs(yArr[4] - yArr[0]) < resolution && showPreSession)
							{
								plotLabels[0] = plotLabels[0] + "/" + plotLabels[4]; 
								plotLabels[4] = "";
								brushesDX[4] = transparentBrushDX;
							}
							else if (Math.Abs(yArr[5] - yArr[0]) < resolution && showPreSession)
							{
								plotLabels[0] = plotLabels[0] + "/" + plotLabels[5]; 
								plotLabels[5] = "";
								brushesDX[5] = transparentBrushDX;
							}
							else if (Math.Abs(yArr[6] - yArr[0]) < resolution && showPreSession && showPreSessionMidline)
							{
								plotLabels[0] = plotLabels[0] + "/" + plotLabels[6]; 
								plotLabels[6] = "";
								brushesDX[6] = transparentBrushDX;
							}
					}	
					
					if(showPreSession && preSessionOpacity > 0 && Values[4].IsValidDataPointAt(lastPlotIndex-displacement))
					{	
						for (int idx = lastPlotIndex; idx >= firstPlotIndex; idx --)	
						{
							if(Values[4].IsValidDataPointAt(idx-displacement))
								firstIndex = idx;
							else
								break;
						}	
						if(firstIndex > firstBarPainted)
							firstX = ChartControl.GetXByBarIndex(ChartBars, firstIndex - 1) + smallGap;
						else	
							firstX = ChartControl.GetXByBarIndex(ChartBars, firstIndex);
						yH = chartScale.GetYByValue(PreSessionHigh.GetValueAt(lastPlotCalcIndex));
						yL = chartScale.GetYByValue(PreSessionLow.GetValueAt(lastPlotCalcIndex));
						width = (float)(lastX - firstX);
						height = (float)(yL - yH);
						startPointDX = new SharpDX.Vector2((float)firstX, (float)yH);
						rect = new SharpDX.RectangleF(startPointDX.X, startPointDX.Y, width, height);
						RenderTarget.FillRectangle(rect, preSessionBrushDX);
					}
					
					if(showOpeningRange && openingRangeOpacity > 0 && Values[1].IsValidDataPointAt(lastPlotIndex-displacement))
					{	
						for (int idx = lastPlotIndex; idx >= firstPlotIndex; idx --)	
						{
							if(Values[1].IsValidDataPointAt(idx-displacement))
								firstIndex = idx;
							else
								break;
						}	
						if(firstIndex > firstBarPainted)
							firstX = ChartControl.GetXByBarIndex(ChartBars, firstIndex - 1) + smallGap;
						else	
							firstX = ChartControl.GetXByBarIndex(ChartBars, firstIndex);
						yH = chartScale.GetYByValue(OpeningRangeHigh.GetValueAt(lastPlotCalcIndex));
						yL = chartScale.GetYByValue(OpeningRangeLow.GetValueAt(lastPlotCalcIndex));
						width = (float)(lastX - firstX);
						height = (float)(yL - yH);
						startPointDX = new SharpDX.Vector2((float)firstX, (float)yH);
						rect = new SharpDX.RectangleF(startPointDX.X, startPointDX.Y, width, height);
						RenderTarget.FillRectangle(rect, openingRangeBrushDX);
					}
					
					if(showPreSession && Values[4].IsValidDataPointAt(lastPlotIndex-displacement))
					{	
						lastX = ChartControl.GetXByBarIndex(ChartBars, lastPlotIndex);
						for (int idx = lastPlotIndex; idx >= firstPlotIndex; idx --)	
						{
							if(Values[4].IsValidDataPointAt(idx-displacement))
								firstIndex = idx;
							else
								break;
						}	
						if(firstIndex > firstBarPainted)
							firstX = ChartControl.GetXByBarIndex(ChartBars, firstIndex - 1) + smallGap;
						else	
							firstX = ChartControl.GetXByBarIndex(ChartBars, firstIndex);
						yH = chartScale.GetYByValue(PreSessionHigh.GetValueAt(lastPlotCalcIndex));
						startPointDX = new SharpDX.Vector2((float)firstX, (float)yH);
						endPointDX = new SharpDX.Vector2((float)lastX, (float)yH);
						RenderTarget.DrawLine(startPointDX, endPointDX, Plots[4].BrushDX, Plots[4].Width, Plots[4].StrokeStyle);
						yL = chartScale.GetYByValue(PreSessionLow.GetValueAt(lastPlotCalcIndex));
						startPointDX = new SharpDX.Vector2((float)firstX, (float)yL);
						endPointDX = new SharpDX.Vector2((float)lastX, (float)yL);
						RenderTarget.DrawLine(startPointDX, endPointDX, Plots[5].BrushDX, Plots[5].Width, Plots[5].StrokeStyle);
						if(showLabels && firstLoop)
						{	
							textPointDX = new SharpDX.Vector2((float)(lastX + labelOffset), (float)(yH - textFontSize/2));
							TextLayout textLayout4 = new TextLayout(Globals.DirectWriteFactory, plotLabels[4], textFormat, Math.Max(100, ChartControl.Properties.BarMarginRight - (float)labelOffset - 20), textFontSize);
							RenderTarget.DrawTextLayout(textPointDX, textLayout4, brushesDX[4]);
							textLayout4.Dispose();
							textPointDX = new SharpDX.Vector2((float)(lastX + labelOffset), (float)(yL - textFontSize/2));
							TextLayout textLayout5 = new TextLayout(Globals.DirectWriteFactory, plotLabels[5], textFormat, Math.Max(100, ChartControl.Properties.BarMarginRight - (float)labelOffset - 20), textFontSize);
							RenderTarget.DrawTextLayout(textPointDX, textLayout5, brushesDX[5]);
							textLayout5.Dispose();
						}	
						if(showPreSessionMidline)
						{	
							y = chartScale.GetYByValue(PreSessionMid.GetValueAt(lastPlotCalcIndex));
							startPointDX = new SharpDX.Vector2((float)firstX, (float)y);
							endPointDX = new SharpDX.Vector2((float)lastX, (float)y);
							RenderTarget.DrawLine(startPointDX, endPointDX, Plots[6].BrushDX, Plots[6].Width, Plots[6].StrokeStyle);
							if(showLabels && firstLoop)
							{	
								textPointDX = new SharpDX.Vector2((float)(lastX + labelOffset), (float)(y - textFontSize/2));
								TextLayout textLayout6 = new TextLayout(Globals.DirectWriteFactory, plotLabels[6], textFormat, Math.Max(100, ChartControl.Properties.BarMarginRight - (float)labelOffset - 20), textFontSize);
								RenderTarget.DrawTextLayout(textPointDX, textLayout6, brushesDX[6]);
								textLayout6.Dispose();
							}	
						}					
					}
					
					if(showOpeningRange && Values[1].IsValidDataPointAt(lastPlotIndex-displacement))
					{	
						lastX = ChartControl.GetXByBarIndex(ChartBars, lastPlotIndex);
						for (int idx = lastPlotIndex; idx >= firstPlotIndex; idx --)	
						{
							if(Values[1].IsValidDataPointAt(idx-displacement))
								firstIndex = idx;
							else
								break;
						}	
						if(firstIndex > firstBarPainted)
							firstX = ChartControl.GetXByBarIndex(ChartBars, firstIndex - 1) + smallGap;
						else	
							firstX = ChartControl.GetXByBarIndex(ChartBars, firstIndex);
						yH = chartScale.GetYByValue(OpeningRangeHigh.GetValueAt(lastPlotCalcIndex));
						startPointDX = new SharpDX.Vector2((float)firstX, (float)yH);
						endPointDX = new SharpDX.Vector2((float)lastX, (float)yH);
						RenderTarget.DrawLine(startPointDX, endPointDX, Plots[1].BrushDX, Plots[1].Width, Plots[1].StrokeStyle);
						yL = chartScale.GetYByValue(OpeningRangeLow.GetValueAt(lastPlotCalcIndex));
						startPointDX = new SharpDX.Vector2((float)firstX, (float)yL);
						endPointDX = new SharpDX.Vector2((float)lastX, (float)yL);
						RenderTarget.DrawLine(startPointDX, endPointDX, Plots[2].BrushDX, Plots[2].Width, Plots[2].StrokeStyle);
						if(showLabels && firstLoop)
						{	
							textPointDX = new SharpDX.Vector2((float)(lastX + labelOffset), (float)(yH - textFontSize/2));
							TextLayout textLayout1 = new TextLayout(Globals.DirectWriteFactory, plotLabels[1], textFormat, Math.Max(100, ChartControl.Properties.BarMarginRight - (float)labelOffset - 20), textFontSize);
							RenderTarget.DrawTextLayout(textPointDX, textLayout1, brushesDX[1]);
							textLayout1.Dispose();
							textPointDX = new SharpDX.Vector2((float)(lastX + labelOffset), (float)(yL - textFontSize/2));
							TextLayout textLayout2 = new TextLayout(Globals.DirectWriteFactory, plotLabels[2], textFormat, Math.Max(100, ChartControl.Properties.BarMarginRight - (float)labelOffset - 20), textFontSize);
							RenderTarget.DrawTextLayout(textPointDX, textLayout2, brushesDX[2]);
							textLayout2.Dispose();
						}	
						if(showOpeningRangeMidline)
						{	
							y = chartScale.GetYByValue(OpeningRangeMid.GetValueAt(lastPlotCalcIndex));
							startPointDX = new SharpDX.Vector2((float)firstX, (float)y);
							endPointDX = new SharpDX.Vector2((float)lastX, (float)y);
							RenderTarget.DrawLine(startPointDX, endPointDX, Plots[3].BrushDX, Plots[3].Width, Plots[3].StrokeStyle);
							if(showLabels && firstLoop)
							{	
								textPointDX = new SharpDX.Vector2((float)(lastX + labelOffset), (float)(y - textFontSize/2));
								TextLayout textLayout3 = new TextLayout(Globals.DirectWriteFactory, plotLabels[3], textFormat, Math.Max(100, ChartControl.Properties.BarMarginRight - (float)labelOffset - 20), textFontSize);
								RenderTarget.DrawTextLayout(textPointDX, textLayout3, brushesDX[3]);
								textLayout3.Dispose();
							}	
						}
					}
					
					if(showRegOpen && Values[0].IsValidDataPointAt(lastPlotIndex-displacement))
					{
						lastX = ChartControl.GetXByBarIndex(ChartBars, lastPlotIndex);
						for (int idx = lastPlotIndex; idx >= firstPlotIndex; idx --)	
						{
							if(Values[0].IsValidDataPointAt(idx-displacement))
								firstIndex = idx;
							else
								break;
						}	
						if(firstIndex > firstBarPainted)
							firstX = ChartControl.GetXByBarIndex(ChartBars, firstIndex - 1) + smallGap;
						else	
							firstX = ChartControl.GetXByBarIndex(ChartBars, firstIndex);
						y = chartScale.GetYByValue(RegOpen.GetValueAt(lastPlotCalcIndex));
						startPointDX = new SharpDX.Vector2((float)firstX, (float)y);
						endPointDX = new SharpDX.Vector2((float)lastX, (float)y);
						RenderTarget.DrawLine(startPointDX, endPointDX, Plots[0].BrushDX, Plots[0].Width, Plots[0].StrokeStyle);
						if(showLabels && firstLoop)
						{	
							textPointDX = new SharpDX.Vector2((float)(lastX + labelOffset), (float)(y - textFontSize/2));
							TextLayout textLayout = new TextLayout(Globals.DirectWriteFactory, plotLabels[0], textFormat, Math.Max(100, ChartControl.Properties.BarMarginRight - (float)labelOffset - 20), textFontSize);
							RenderTarget.DrawTextLayout(textPointDX, textLayout, brushesDX[0]);
							textLayout.Dispose();
						}	
					}	
					
					if(lastPlotIndex < firstPlotIndex)
						lastPlotIndex = 0;
					else
						lastPlotIndex = firstPlotIndex - 1;
					lastPlotCalcIndex = lastPlotIndex - displacement;
					firstLoop = false;
				}	
				while (lastPlotIndex > firstBarIndex);
			}	
			openingRangeBrushDX.Dispose();
			preSessionBrushDX.Dispose();
			transparentBrushDX.Dispose();
			for(int seriesCount = 0; seriesCount < Values.Length; seriesCount++)
				brushesDX[seriesCount].Dispose();
			RenderTarget.AntialiasMode = oldAntialiasMode;
			textFormat.Dispose();
		}
		#endregion
	}
}

namespace NinjaTrader.NinjaScript.Indicators
{		
	public class amaOpeningRangeTypeConverter : NinjaTrader.NinjaScript.IndicatorBaseConverter
	{
		public override bool GetPropertiesSupported(ITypeDescriptorContext context) { return true; }

		public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
		{
			PropertyDescriptorCollection propertyDescriptorCollection = base.GetPropertiesSupported(context) ? base.GetProperties(context, value, attributes) : TypeDescriptor.GetProperties(value, attributes);

			amaOpeningRange				thisOpeningRangeInstance				= (amaOpeningRange) value;
			amaPreSessionTypeOR			preSessionTypeFromInstance				= thisOpeningRangeInstance.PreSessionType;
			bool						showRegOpenFromInstance					= thisOpeningRangeInstance.ShowRegOpen;
			bool						showOpeningRangeFromInstance			= thisOpeningRangeInstance.ShowOpeningRange;
			bool						showOpeningRangeMidlineFromInstance		= thisOpeningRangeInstance.ShowOpeningRangeMidline;
			bool						showPreSessionFromInstance				= thisOpeningRangeInstance.ShowPreSession;
			bool						showPreSessionMidlineFromInstance		= thisOpeningRangeInstance.ShowPreSessionMidline;
			bool						showLabelsFromInstance					= thisOpeningRangeInstance.ShowLabels;
			
			PropertyDescriptorCollection adjusted = new PropertyDescriptorCollection(null);
			
			foreach (PropertyDescriptor thisDescriptor in propertyDescriptorCollection)
			{
				if(!showRegOpenFromInstance && (thisDescriptor.Name == "RegOpenBrush"|| thisDescriptor.Name == "Plot0Style" || thisDescriptor.Name == "Dash0Style" || thisDescriptor.Name == "Plot0Width" ))
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if (preSessionTypeFromInstance == amaPreSessionTypeOR.Full_Period && (thisDescriptor.Name == "PreSessionTZSelector" 
					|| thisDescriptor.Name == "S_PreSessionStart" || thisDescriptor.Name == "S_PreSessionEnd"))
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if (!showOpeningRangeFromInstance && (thisDescriptor.Name == "OpeningRangeHighBrush" || thisDescriptor.Name == "OpeningRangeLowBrush" || thisDescriptor.Name == "OpeningRangeMidBrush" 
					|| thisDescriptor.Name == "OpeningRangeBrushS" || thisDescriptor.Name == "Plot1Style" || thisDescriptor.Name == "Dash1Style" || thisDescriptor.Name == "Plot1Width"
					|| thisDescriptor.Name == "ShowOpeningRangeMidline" || thisDescriptor.Name == "Plot3Style" || thisDescriptor.Name == "Dash3Style" || thisDescriptor.Name == "Plot3Width" 
					|| thisDescriptor.Name == "OpeningRangeOpacity"))
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if (!showOpeningRangeMidlineFromInstance && (thisDescriptor.Name == "OpeningRangeMidBrush" || thisDescriptor.Name == "Plot3Style" 
					|| thisDescriptor.Name == "Dash3Style" || thisDescriptor.Name == "Plot3Width" ))
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if (!showPreSessionFromInstance && (thisDescriptor.Name == "PreSessionHighBrush" || thisDescriptor.Name == "PreSessionLowBrush" || thisDescriptor.Name == "PreSessionMidBrush" 
					|| thisDescriptor.Name == "PreSessionBrushS" || thisDescriptor.Name == "Plot4Style" || thisDescriptor.Name == "Dash4Style" || thisDescriptor.Name == "Plot4Width"
					|| thisDescriptor.Name == "ShowPreSessionMidline" || thisDescriptor.Name == "Plot6Style" || thisDescriptor.Name == "Dash6Style" || thisDescriptor.Name == "Plot6Width" 
					|| thisDescriptor.Name == "PreSessionOpacity"))
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if (!showPreSessionMidlineFromInstance && (thisDescriptor.Name == "PreSessionMidBrush" || thisDescriptor.Name == "Plot6Style" 
					|| thisDescriptor.Name == "Dash6Style" || thisDescriptor.Name == "Plot6Width" ))
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else if (!showLabelsFromInstance && (thisDescriptor.Name == "TextFontSize" || thisDescriptor.Name == "ShiftLabelOffset"))
					adjusted.Add(new PropertyDescriptorExtended(thisDescriptor, o => value, null, new Attribute[] {new BrowsableAttribute(false), }));
				else	
					adjusted.Add(thisDescriptor);
			}
			return adjusted;
		}
	}
}

#region Global Enums

public enum amaPreSessionTypeOR 
{
	Full_Period, 
	Custom_Period
}

public enum amaTimeZonesOR
{
	Exchange_Time, 
	Chart_Time, 
	US_Eastern_Standard_Time, 
	US_Central_Standard_Time, 
	US_Mountain_Standard_Time, 
	US_Pacific_Standard_Time, 
	AUS_Eastern_Standard_Time, 
	Japan_Standard_Time, 
	China_Standard_Time, 
	India_Standard_Time, 
	Central_European_Time, 
	GMT_Standard_Time
} 
#endregion

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private LizardIndicators.amaOpeningRange[] cacheamaOpeningRange;
		public LizardIndicators.amaOpeningRange amaOpeningRange(amaTimeZonesOR openingRangeTZSelector, string s_OpeningRangeStart, string s_OpeningRangeEnd, amaPreSessionTypeOR preSessionType, amaTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd)
		{
			return amaOpeningRange(Input, openingRangeTZSelector, s_OpeningRangeStart, s_OpeningRangeEnd, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd);
		}

		public LizardIndicators.amaOpeningRange amaOpeningRange(ISeries<double> input, amaTimeZonesOR openingRangeTZSelector, string s_OpeningRangeStart, string s_OpeningRangeEnd, amaPreSessionTypeOR preSessionType, amaTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd)
		{
			if (cacheamaOpeningRange != null)
				for (int idx = 0; idx < cacheamaOpeningRange.Length; idx++)
					if (cacheamaOpeningRange[idx] != null && cacheamaOpeningRange[idx].OpeningRangeTZSelector == openingRangeTZSelector && cacheamaOpeningRange[idx].S_OpeningRangeStart == s_OpeningRangeStart && cacheamaOpeningRange[idx].S_OpeningRangeEnd == s_OpeningRangeEnd && cacheamaOpeningRange[idx].PreSessionType == preSessionType && cacheamaOpeningRange[idx].PreSessionTZSelector == preSessionTZSelector && cacheamaOpeningRange[idx].S_PreSessionStart == s_PreSessionStart && cacheamaOpeningRange[idx].S_PreSessionEnd == s_PreSessionEnd && cacheamaOpeningRange[idx].EqualsInput(input))
						return cacheamaOpeningRange[idx];
			return CacheIndicator<LizardIndicators.amaOpeningRange>(new LizardIndicators.amaOpeningRange(){ OpeningRangeTZSelector = openingRangeTZSelector, S_OpeningRangeStart = s_OpeningRangeStart, S_OpeningRangeEnd = s_OpeningRangeEnd, PreSessionType = preSessionType, PreSessionTZSelector = preSessionTZSelector, S_PreSessionStart = s_PreSessionStart, S_PreSessionEnd = s_PreSessionEnd }, input, ref cacheamaOpeningRange);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.LizardIndicators.amaOpeningRange amaOpeningRange(amaTimeZonesOR openingRangeTZSelector, string s_OpeningRangeStart, string s_OpeningRangeEnd, amaPreSessionTypeOR preSessionType, amaTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd)
		{
			return indicator.amaOpeningRange(Input, openingRangeTZSelector, s_OpeningRangeStart, s_OpeningRangeEnd, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd);
		}

		public Indicators.LizardIndicators.amaOpeningRange amaOpeningRange(ISeries<double> input , amaTimeZonesOR openingRangeTZSelector, string s_OpeningRangeStart, string s_OpeningRangeEnd, amaPreSessionTypeOR preSessionType, amaTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd)
		{
			return indicator.amaOpeningRange(input, openingRangeTZSelector, s_OpeningRangeStart, s_OpeningRangeEnd, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.LizardIndicators.amaOpeningRange amaOpeningRange(amaTimeZonesOR openingRangeTZSelector, string s_OpeningRangeStart, string s_OpeningRangeEnd, amaPreSessionTypeOR preSessionType, amaTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd)
		{
			return indicator.amaOpeningRange(Input, openingRangeTZSelector, s_OpeningRangeStart, s_OpeningRangeEnd, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd);
		}

		public Indicators.LizardIndicators.amaOpeningRange amaOpeningRange(ISeries<double> input , amaTimeZonesOR openingRangeTZSelector, string s_OpeningRangeStart, string s_OpeningRangeEnd, amaPreSessionTypeOR preSessionType, amaTimeZonesOR preSessionTZSelector, string s_PreSessionStart, string s_PreSessionEnd)
		{
			return indicator.amaOpeningRange(input, openingRangeTZSelector, s_OpeningRangeStart, s_OpeningRangeEnd, preSessionType, preSessionTZSelector, s_PreSessionStart, s_PreSessionEnd);
		}
	}
}

#endregion
