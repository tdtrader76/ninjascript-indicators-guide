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
using SharpDX.DirectWrite;
using NinjaTrader.Core;
using System.IO;  // for streamwriter
using System.Reflection;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
	// Version 1.0 - AES (Advanced Export Single)
	// Based on ChartToCSV by NinjaTrader_PaulH
	// Modified to export data from a single specific indicator
	//
	// Notes: This indicator exports data from a specific indicator selected by name to a text file.
	// The user must specify the indicator name in the "IndicatorName" property.
	// If the indicator is not found or not specified, an error message will be displayed.
	//
	// Operational use: Apply to chart, set the IndicatorName property, wait until all indicators have finished calculating,
	// click the WriteData button. The data will be exported and the button will show "Done".
	// Click "Done" to close the file or remove the indicator from the chart.
	//

	public class AES : Indicator
	{
		private List <string> Labels;
		private List <double> Data;
		private bool init = true, doitonce = true, alsodoitonce = true;
		private StreamWriter sw;
		private string path;
		private bool longButtonClicked;
		private System.Windows.Controls.Button longButton;
		private System.Windows.Controls.Grid myGrid;
		private DateTime start_Date, end_Date;
		private bool doneWriting = false;

		// Safe method to close StreamWriter
		private void SafeCloseStreamWriter()
		{
			if (sw != null)
			{
				try
				{
					if (sw.BaseStream != null && sw.BaseStream.CanWrite)
					{
						sw.Flush();
						sw.Close();
					}
				}
				catch (Exception ex)
				{
					Print(this.Name + ": Error closing StreamWriter - " + ex.Message);
				}
				finally
				{
					sw = null;
				}
			}
		}

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Exports data from a specific indicator to a text file";
				Name										= "AES";
				Calculate									= Calculate.OnEachTick;
				IsOverlay									= true;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;

				// Default property
				IndicatorName								= string.Empty;
			}
			else if (State == State.Historical)
			{
				if (UserControlCollection.Contains(myGrid))
					return;

				Dispatcher.InvokeAsync((() =>
				{
					myGrid = new System.Windows.Controls.Grid
					{
						Name = "MyCustomGrid", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top
					};

					System.Windows.Controls.ColumnDefinition column1 = new System.Windows.Controls.ColumnDefinition();

					myGrid.ColumnDefinitions.Add(column1);

					longButton = new System.Windows.Controls.Button
					{
						Name = "WriteData", Content = "WriteData", Foreground = Brushes.White, Background = Brushes.Green
					};

					longButton.Click += OnButtonClick;

					System.Windows.Controls.Grid.SetColumn(longButton, 0);

					myGrid.Children.Add(longButton);

					UserControlCollection.Add(myGrid);
				}));


				Data = new List<double>();  	// initialize for Data list
				Labels = new List<string>();	// initialize the labels list
				Labels.Add("Date;");			// assign the basic labels
				Labels.Add("Time;");
				Labels.Add("Open;");
				Labels.Add("High;");
				Labels.Add("Low;");
				Labels.Add("Close;");
				Labels.Add("Volume;");

				path 	= NinjaTrader.Core.Globals.UserDataDir+Instrument.MasterInstrument.Name+" "+DateTime.Now.DayOfWeek+" "+DateTime.Now.Hour+DateTime.Now.Minute+ ".txt"; // Define the Path to our test file

				sw = File.AppendText(path);  // Open the path for writing

			}
			else if (State == State.Terminated)
			{
				Dispatcher.InvokeAsync((() =>
				{
					if (myGrid != null)
					{
						if (longButton != null)
						{
							myGrid.Children.Remove(longButton);
							longButton.Click -= OnButtonClick;
							longButton = null;
							Print ("state.terminated, removed button?");
						}
					}
				}));

				// Safe close of StreamWriter
				SafeCloseStreamWriter();
			}
		}

		protected override void OnBarUpdate()
		{
			if (doneWriting)  // when the done button is pressed, no need for OBU checks
				return;

			if (CurrentBar < 1)  // Validate sufficient bars available
				return;

			if (State == State.Realtime && longButtonClicked && alsodoitonce)  // on the button click, on the first tick run the process.
			{
				Draw.VerticalLine(this, "writedata", 1, Brushes.SkyBlue);		// Draw vertical reference line on chart, previous bar is last historial
				ReadWrite();													// go get the data
				alsodoitonce = false;											// only once
				SafeCloseStreamWriter();  // close the file safely
				// provide feedback on chart to remove the indicator
				Draw.TextFixed(this, "datawritten1", "Historical chart and indicator data written from: "+start_Date+ " through "+end_Date
					+"\n In file: "+path+"\nRemove indicator "+this.Name+" from chart to remove this message", TextPosition.BottomLeft);
			}
		}

		private void ReadWrite()
		{
			try
			{
				// Validate ChartControl is available
				if (ChartControl == null)
				{
					Draw.TextFixed(this, "error1", "ERROR: ChartControl is not available.",
						TextPosition.BottomLeft, Brushes.Red, new Gui.Tools.SimpleFont("Arial", 12),
						Brushes.Transparent, Brushes.Transparent, 0);
					Print(this.Name + ": ERROR - ChartControl is null.");
					SafeCloseStreamWriter();
					return;
				}

				// Validate IndicatorName property
				if (string.IsNullOrWhiteSpace(IndicatorName))
				{
					Draw.TextFixed(this, "error1", "ERROR: IndicatorName property is empty. Please specify an indicator name in the properties.",
						TextPosition.BottomLeft, Brushes.Red, new Gui.Tools.SimpleFont("Arial", 12), Brushes.Transparent, Brushes.Transparent, 0);
					Print(this.Name + ": ERROR - IndicatorName property is empty. Please specify an indicator name.");
					SafeCloseStreamWriter();
					return;
				}

				// Search for the specified indicator
				IndicatorBase targetIndicator = null;
				lock (ChartControl.Indicators)
				{
					foreach (IndicatorBase indicator in ChartControl.Indicators)
					{
						if (indicator.Name == IndicatorName)
						{
							targetIndicator = indicator;
							break;
						}
					}
				}

				// Validate that the indicator was found
				if (targetIndicator == null)
				{
					Draw.TextFixed(this, "error1", "ERROR: Indicator '" + IndicatorName + "' not found on chart. Please check the indicator name.",
						TextPosition.BottomLeft, Brushes.Red, new Gui.Tools.SimpleFont("Arial", 12), Brushes.Transparent, Brushes.Transparent, 0);
					Print(this.Name + ": ERROR - Indicator '" + IndicatorName + "' not found on chart.");
					SafeCloseStreamWriter();
					return;
				}

				// Validate indicator state
				if (targetIndicator.State < State.Realtime)
				{
					Draw.TextFixed(this, "error1", "ERROR: Indicator '" + IndicatorName + "' is not in Realtime state. State: " + targetIndicator.State,
						TextPosition.BottomLeft, Brushes.Red, new Gui.Tools.SimpleFont("Arial", 12), Brushes.Transparent, Brushes.Transparent, 0);
					Print(this.Name + ": ERROR - Indicator '" + IndicatorName + "' is not in Realtime state. State: " + targetIndicator.State);
					SafeCloseStreamWriter();
					return;
				}

				// Validate and diagnose indicator data availability
				if (targetIndicator.Values.Length == 0)
				{
					Print(this.Name + ": Indicator '" + IndicatorName + "' has no traditional plots. Checking for special indicators...");

					// Check if it's a TPO indicator with special export methods
					var indicatorType = targetIndicator.GetType();
					var getTPOProfileMethod = indicatorType.GetMethod("GetTPOProfile");
					var getTPOCountMethod = indicatorType.GetMethod("GetTPOCountAtPrice");

					if (getTPOProfileMethod != null && getTPOCountMethod != null)
					{
						Print(this.Name + ": Detected TPO indicator. Using TPO export mode.");
						ExportTPOData(targetIndicator);
						return;
					}

					// If not TPO, show reflection data
					var properties = indicatorType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
					Print(this.Name + ": Found " + properties.Length + " public properties:");
					foreach (var prop in properties)
					{
						// Show ALL properties to diagnose the indicator
						Print(this.Name + ": - " + prop.Name + " (" + prop.PropertyType.FullName + ")");
					}

					Draw.TextFixed(this, "error1", "ERROR: Indicator '" + IndicatorName + "' has no plots/values to export.\nCheck Output window for available properties.",
						TextPosition.BottomLeft, Brushes.Red, new Gui.Tools.SimpleFont("Arial", 12), Brushes.Transparent, Brushes.Transparent, 0);
					Print(this.Name + ": ERROR - Indicator '" + IndicatorName + "' has no plots (Values.Length = 0). See above for available properties.");
					SafeCloseStreamWriter();
					return;
				}

				// Debug information
				Print(this.Name + ": Found indicator '" + IndicatorName + "' with " + targetIndicator.Values.Length + " plots/values.");
				for (int i = 0; i < targetIndicator.Values.Length; i++)
				{
					Print(this.Name + ": Plot " + i + ": " + targetIndicator.Plots[i].Name);
				}

				for (int n = 0; n < Bars.Count-1; n++) // process the historical data only
				{
					Data.Clear(); // clear list first
					// add basics
					Data.Add(Open.GetValueAt(n));
					Data.Add(High.GetValueAt(n));
					Data.Add(Low.GetValueAt(n));
					Data.Add(Close.GetValueAt(n));
					Data.Add(Volume.GetValueAt(n));

					// Process only the specified indicator
					for (int seriesCount = 0; seriesCount <  targetIndicator.Values.Length ; seriesCount++)  // process each plot of the indicator
					{
						Plot	plot				= targetIndicator.Plots[seriesCount];						// get a plot from the indictor
						double val					= targetIndicator.Values[seriesCount].GetValueAt(n);		// now get a specific bar value
						Data.Add(val);				// add indicators current plot value to list;

						if (init)
						{
							Labels.Add(targetIndicator.Name+":"+plot.Name+";");	 // add indicator : plotname to labels list
						}
					}

					// Debug: Print data count on first bar
					if (n == 0)
					{
						Print(this.Name + ": First bar - Data.Count = " + Data.Count + " (Expected: 5 OHLCV + " + targetIndicator.Values.Length + " indicator values = " + (5 + targetIndicator.Values.Length) + ")");
					}

					init = false;  //grab labels just once

					// now write labels to file first (header row)
					if (!init && doitonce)
					{
						int LC = Labels.Count-1;

						for (int h = 0; h < Labels.Count; h++)
						{
							sw.Write(Labels[h]);
						}
						sw.WriteLine();  // kick it to the next line
						doitonce = false;
						Dispatcher.InvokeAsync((() =>
						{
							longButton.Background = Brushes.Gold;
							longButton.Foreground = Brushes.Black;
							longButton.Content = "Done";
						}));

					}

					// write bar date and time first
					sw.Write(Time.GetValueAt(n).Date.ToShortDateString()+";"+Time.GetValueAt(n).TimeOfDay+";");

					if (n == 0)
						start_Date = Time.GetValueAt(n);		// save the start date for feedback
					if (n == Bars.Count-2)
						end_Date = Time.GetValueAt(n);			// save the end date for feedback
					// write data after on same line
					for (int j = 0; j < Data.Count; j++)
					{
						sw.Write(Data[j]+";");  				// write the data with a comma for comma delimitation
					}
					sw.WriteLine();								// kick it to the next line to write
				}
			}
			catch (Exception ex)
			{
				Draw.TextFixed(this, "error1", "ERROR: Exception during data export - " + ex.Message,
					TextPosition.BottomLeft, Brushes.Red, new Gui.Tools.SimpleFont("Arial", 12),
					Brushes.Transparent, Brushes.Transparent, 0);
				Print(this.Name + ": ERROR - " + ex.ToString());
			}
			finally
			{
				SafeCloseStreamWriter();
			}
		}

		private void ExportTPOData(IndicatorBase tpoIndicator)
		{
			try
			{
				Print(this.Name + ": Starting TPO data export...");

				// Get TPO profile using reflection
				var getTPOProfileMethod = tpoIndicator.GetType().GetMethod("GetTPOProfile");
				var tpoProfile = getTPOProfileMethod.Invoke(tpoIndicator, null) as System.Collections.IList;

				if (tpoProfile == null || tpoProfile.Count == 0)
				{
					Draw.TextFixed(this, "error1", "ERROR: TPO indicator has no data to export.",
						TextPosition.BottomLeft, Brushes.Red, new Gui.Tools.SimpleFont("Arial", 12),
						Brushes.Transparent, Brushes.Transparent, 0);
					Print(this.Name + ": ERROR - TPO profile is empty.");
					return;
				}

				Print(this.Name + ": Found " + tpoProfile.Count + " TPO price levels.");

				// Write headers
				sw.WriteLine("Price;TPO_Count;TPO_Letters;");

				// Export each TPO level
				foreach (var level in tpoProfile)
				{
					var levelType = level.GetType();
					var priceProperty = levelType.GetProperty("Price");
					var countProperty = levelType.GetProperty("Count");
					var lettersProperty = levelType.GetProperty("Letters");

					if (priceProperty != null && countProperty != null && lettersProperty != null)
					{
						double price = (double)priceProperty.GetValue(level);
						int count = (int)countProperty.GetValue(level);
						string letters = (string)lettersProperty.GetValue(level);

						sw.WriteLine(price + ";" + count + ";" + letters + ";");
					}
				}

				Print(this.Name + ": TPO data export completed successfully.");

				// Update button
				Dispatcher.InvokeAsync((() =>
				{
					if (longButton != null)
					{
						longButton.Background = Brushes.Gold;
						longButton.Foreground = Brushes.Black;
						longButton.Content = "Done";
					}
				}));

				// Show completion message
				Draw.TextFixed(this, "datawritten1", "TPO data exported successfully to:\n" + path +
					"\nTotal price levels: " + tpoProfile.Count +
					"\nRemove indicator " + this.Name + " from chart to remove this message",
					TextPosition.BottomLeft);
			}
			catch (Exception ex)
			{
				Draw.TextFixed(this, "error1", "ERROR: Failed to export TPO data - " + ex.Message,
					TextPosition.BottomLeft, Brushes.Red, new Gui.Tools.SimpleFont("Arial", 12),
					Brushes.Transparent, Brushes.Transparent, 0);
				Print(this.Name + ": ERROR - TPO export failed: " + ex.ToString());
			}
		}

		private void OnButtonClick(object sender, RoutedEventArgs rea)
		{
			System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
			if (button == longButton && button.Name == "WriteData" && button.Content == "WriteData")
			{
				button.Content = "Writing data";
				button.Name = "DataWritten";
				longButtonClicked = true;
				return;
			}

			if (button == longButton && button.Name == "DataWritten" && button.Content == "Done")
			{
				Dispatcher.InvokeAsync((() =>
				{
					if (myGrid != null)
					{
						if (longButton != null)
						{
							myGrid.Children.Remove(longButton);
							longButton.Click -= OnButtonClick;
							longButton = null;
						}
					}
				}));
				doneWriting = true;
				return;
			}
		}

		#region Properties

		[NinjaScriptProperty]
		[Display(Name = "Indicator Name", Description = "Name of the indicator to export data from", Order = 1, GroupName = "Parameters")]
		public string IndicatorName
		{ get; set; }

		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private AES[] cacheAES;
		public AES AES(string indicatorName)
		{
			return AES(Input, indicatorName);
		}

		public AES AES(ISeries<double> input, string indicatorName)
		{
			if (cacheAES != null)
				for (int idx = 0; idx < cacheAES.Length; idx++)
					if (cacheAES[idx] != null && cacheAES[idx].IndicatorName == indicatorName && cacheAES[idx].EqualsInput(input))
						return cacheAES[idx];
			return CacheIndicator<AES>(new AES(){ IndicatorName = indicatorName }, input, ref cacheAES);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.AES AES(string indicatorName)
		{
			return indicator.AES(Input, indicatorName);
		}

		public Indicators.AES AES(ISeries<double> input , string indicatorName)
		{
			return indicator.AES(input, indicatorName);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.AES AES(string indicatorName)
		{
			return indicator.AES(Input, indicatorName);
		}

		public Indicators.AES AES(ISeries<double> input , string indicatorName)
		{
			return indicator.AES(input, indicatorName);
		}
	}
}

#endregion
