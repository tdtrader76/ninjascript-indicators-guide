#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

/// <summary>
/// BasicTPO - Time Price Opportunity Calculator
///
/// This indicator calculates TPO (Time Price Opportunity) counts for each price level.
/// Market Profile divides trading into time periods (e.g., 30 minutes), and each period
/// receives a letter (A, B, C, etc.). For each price level where trading occurred,
/// the letter is marked, and the count of letters at each price = TPO count.
///
/// Features:
/// - Configurable TPO period (default 30 minutes)
/// - Tick size multiplier for price level grouping
/// - Session-based reset (RTH support)
/// - Public API for data access (compatible with AES export)
/// - Visual rendering of TPO distribution
///
/// Version: 1.0
/// Created: 2025-10-03
/// </summary>

namespace NinjaTrader.NinjaScript.Indicators
{
	public class BasicTPO : Indicator
	{
		#region Variables

		// TPO Data Structures
		private Dictionary<double, int> tpoCountsByPrice;          // precio -> conteo de TPOs
		private Dictionary<double, string> tpoLettersByPrice;      // precio -> letras (ej: "ABC")
		private Dictionary<double, List<char>> tpoLettersListByPrice; // precio -> lista de letras individuales

		// Period Tracking
		private DateTime currentPeriodStart;
		private char currentLetter = 'A';
		private int letterIndex = 0;
		private bool isNewPeriod = true;

		// Session Tracking
		private DateTime lastSessionDate;
		private bool isFirstBarOfNewSession = false;

		// Price Level Calculation
		private double effectiveTickSize;

		// TPO Period Tracking
		private List<DateTime> periodStartTimes;
		private HashSet<double> processedPricesInPeriod; // Evita duplicados en el mismo período
		private char lastPrintedPeriodLetter = '\0'; // Track last printed period to avoid duplicates

		#endregion

		#region OnStateChange

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Calculates TPO (Time Price Opportunity) counts per price level for Market Profile analysis. " +
				              @"Each time period is assigned a letter, and TPO count represents how many periods traded at each price.";
				Name = "BasicTPO";
				Calculate = Calculate.OnBarClose;
				IsOverlay = false;
				DisplayInDataBox = true;
				DrawOnPricePanel = false;
				DrawHorizontalGridLines = true;
				DrawVerticalGridLines = true;
				PaintPriceMarkers = false;
				ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive = true;

				// Default Properties
				TPOPeriodMinutes = 30;
				TickSizeMultiplier = 1;
				UseRTHOnly = true;
				ShowDebugInfo = false;

				// Add a single plot for visualization (max TPO count)
				AddPlot(new Stroke(Brushes.CornflowerBlue, 2), PlotStyle.Bar, "TPOCount");
			}
			else if (State == State.Configure)
			{
				// Validate parameters
				if (TPOPeriodMinutes < 1)
					TPOPeriodMinutes = 1;
				if (TickSizeMultiplier < 1)
					TickSizeMultiplier = 1;
			}
			else if (State == State.DataLoaded)
			{
				// Initialize data structures
				tpoCountsByPrice = new Dictionary<double, int>();
				tpoLettersByPrice = new Dictionary<double, string>();
				tpoLettersListByPrice = new Dictionary<double, List<char>>();
				periodStartTimes = new List<DateTime>();
				processedPricesInPeriod = new HashSet<double>();

				// Calculate effective tick size
				effectiveTickSize = TickSize * TickSizeMultiplier;

				// Initialize period tracking
				currentPeriodStart = DateTime.MinValue;
				lastSessionDate = DateTime.MinValue;

				if (ShowDebugInfo)
				{
					Print(string.Format("{0}: Initialized with TPOPeriod={1}min, TickSizeMultiplier={2}, EffectiveTickSize={3}",
						Name, TPOPeriodMinutes, TickSizeMultiplier, effectiveTickSize));
				}
			}
		}

		#endregion

		#region OnBarUpdate

		protected override void OnBarUpdate()
		{
			// Need at least one bar
			if (CurrentBar < 1)
				return;

			// Check for new session
			CheckForNewSession();

			// Check for new TPO period
			CheckForNewPeriod();

			// Process the current bar's price range
			ProcessBarPriceRange();

			// Update plot with current max TPO count (for visualization)
			UpdatePlotValue();

			// Print TPO data by price level if enabled and we're at the start of a new period
			if (ShowTPOByPriceLevel && isNewPeriod && CurrentBar > 1)
			{
				PrintTPOByPriceLevel();
			}
		}

		#endregion

		#region Core TPO Logic

		/// <summary>
		/// Checks if we're starting a new trading session and resets TPO data if needed
		/// </summary>
		private void CheckForNewSession()
		{
			if (Bars == null || Bars.BarsType == null)
				return;

			// Check if this is the first bar of a new session
			if (Bars.IsFirstBarOfSession)
			{
				DateTime currentDate = Time[0].Date;

				// Only reset if it's truly a new session date
				if (lastSessionDate != DateTime.MinValue && currentDate > lastSessionDate)
				{
					if (ShowDebugInfo)
					{
						Print(string.Format("{0}: New session detected at {1}. Resetting TPO data. Previous session: {2}",
							Name, Time[0], lastSessionDate));
						Print(string.Format("{0}: Previous session had {1} price levels with TPO data",
							Name, tpoCountsByPrice.Count));
					}

					ResetTPOData();
				}

				lastSessionDate = currentDate;
				isFirstBarOfNewSession = true;
			}
			else
			{
				isFirstBarOfNewSession = false;
			}
		}

		/// <summary>
		/// Checks if we're starting a new TPO period (e.g., every 30 minutes)
		/// </summary>
		private void CheckForNewPeriod()
		{
			// If RTH only, skip bars outside regular trading hours
			if (UseRTHOnly && !Bars.BarsType.IsIntraday)
			{
				// For daily or higher timeframes, treat each bar as one period
				isNewPeriod = true;
			}

			// Initialize on first bar
			if (currentPeriodStart == DateTime.MinValue)
			{
				currentPeriodStart = Time[0];
				isNewPeriod = true;
				periodStartTimes.Add(currentPeriodStart);

				if (ShowDebugInfo)
					Print(string.Format("{0}: First period started at {1}, Letter: {2}", Name, currentPeriodStart, currentLetter));
			}
			else
			{
				// Check if enough time has passed for a new period
				TimeSpan elapsed = Time[0] - currentPeriodStart;

				if (elapsed.TotalMinutes >= TPOPeriodMinutes)
				{
					// Start new period
					currentPeriodStart = Time[0];
					isNewPeriod = true;
					letterIndex++;
					currentLetter = GetLetterForIndex(letterIndex);
					periodStartTimes.Add(currentPeriodStart);
					processedPricesInPeriod.Clear(); // Reset processed prices for new period

					if (ShowDebugInfo)
						Print(string.Format("{0}: New period #{1} started at {2}, Letter: {3}",
							Name, letterIndex + 1, currentPeriodStart, currentLetter));
				}
				else
				{
					isNewPeriod = false;
				}
			}
		}

		/// <summary>
		/// Processes the price range of the current bar and updates TPO counts
		/// </summary>
		private void ProcessBarPriceRange()
		{
			// Get the high and low of the current bar
			double barHigh = High[0];
			double barLow = Low[0];

			// Round to effective tick size levels
			double startPrice = RoundToTickLevel(barLow);
			double endPrice = RoundToTickLevel(barHigh);

			// Iterate through all price levels touched by this bar
			for (double price = startPrice; price <= endPrice; price += effectiveTickSize)
			{
				// Round to avoid floating point precision issues
				double roundedPrice = Math.Round(price, 8);

				// Skip if we've already processed this price in the current period
				// This prevents double-counting within the same TPO period
				if (processedPricesInPeriod.Contains(roundedPrice))
					continue;

				// Mark this price as processed for this period
				processedPricesInPeriod.Add(roundedPrice);

				// Update TPO count
				if (!tpoCountsByPrice.ContainsKey(roundedPrice))
				{
					tpoCountsByPrice[roundedPrice] = 0;
					tpoLettersByPrice[roundedPrice] = "";
					tpoLettersListByPrice[roundedPrice] = new List<char>();
				}

				tpoCountsByPrice[roundedPrice]++;
				tpoLettersListByPrice[roundedPrice].Add(currentLetter);

				// Update letters string (for display)
				tpoLettersByPrice[roundedPrice] = new string(tpoLettersListByPrice[roundedPrice].ToArray());
			}
		}

		/// <summary>
		/// Updates the plot value for visualization
		/// </summary>
		private void UpdatePlotValue()
		{
			// Find the TPO count for the current bar's close price
			double closePrice = RoundToTickLevel(Close[0]);

			if (tpoCountsByPrice.ContainsKey(closePrice))
			{
				Values[0][0] = tpoCountsByPrice[closePrice];
			}
			else
			{
				Values[0][0] = 0;
			}
		}

		/// <summary>
		/// Prints TPO data organized by price level to the Output window
		/// Called at the end of each TPO period
		/// </summary>
		private void PrintTPOByPriceLevel()
		{
			// Only print if we have data and it's a new period we haven't printed yet
			if (tpoCountsByPrice == null || tpoCountsByPrice.Count == 0)
				return;

			// Avoid printing the same period multiple times
			if (lastPrintedPeriodLetter == currentLetter)
				return;

			lastPrintedPeriodLetter = currentLetter;

			// Create header with separator
			StringBuilder output = new StringBuilder();
			output.AppendLine("");
			output.AppendLine("================================================================================");
			output.AppendLine(string.Format("TPO PROFILE - Period: {0} | Time: {1} | Total Periods: {2}",
				currentLetter, Time[0].ToString("yyyy-MM-dd HH:mm:ss"), letterIndex + 1));
			output.AppendLine("================================================================================");
			output.AppendLine("");

			// Get all prices sorted descending (highest to lowest)
			var sortedPrices = tpoCountsByPrice.Keys.OrderByDescending(p => p).ToList();

			// Find the Point of Control (price with highest TPO count)
			double poc = GetPointOfControl();
			int maxTPOCount = GetMaxTPOCount();

			// Print each price level with its TPO data
			foreach (double price in sortedPrices)
			{
				int count = tpoCountsByPrice[price];
				string letters = tpoLettersByPrice.ContainsKey(price) ? tpoLettersByPrice[price] : "";

				// Mark the Point of Control with an asterisk
				string pocMarker = (Math.Abs(price - poc) < 0.0001) ? " <-- POC" : "";

				// Create visual bar representation (one '#' per TPO)
				string visualBar = new string('#', count);

				output.AppendLine(string.Format("Price: {0,10:F2} | TPOs: {1,3} | Letters: {2,-30} | {3}{4}",
					price, count, letters, visualBar, pocMarker));
			}

			// Print summary statistics
			output.AppendLine("");
			output.AppendLine("--------------------------------------------------------------------------------");
			output.AppendLine(string.Format("Point of Control (POC): {0:F2} | Max TPOs: {1} | Total Price Levels: {2}",
				poc, maxTPOCount, sortedPrices.Count));
			output.AppendLine("================================================================================");
			output.AppendLine("");

			// Print to NinjaTrader Output window
			Print(output.ToString());
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Rounds a price to the effective tick level
		/// </summary>
		private double RoundToTickLevel(double price)
		{
			return Math.Round(price / effectiveTickSize) * effectiveTickSize;
		}

		/// <summary>
		/// Gets the letter for a given period index (A, B, C, ... Z, AA, AB, etc.)
		/// </summary>
		private char GetLetterForIndex(int index)
		{
			// Simple implementation: A-Z, then wrap around
			// For more sophisticated implementation, could use AA, AB, etc.
			const int alphabetSize = 26;

			if (index < alphabetSize)
			{
				return (char)('A' + index);
			}
			else
			{
				// After Z, cycle back to A (or could implement AA, AB, etc.)
				return (char)('A' + (index % alphabetSize));
			}
		}

		/// <summary>
		/// Resets all TPO data (called at start of new session)
		/// </summary>
		private void ResetTPOData()
		{
			if (tpoCountsByPrice != null)
				tpoCountsByPrice.Clear();

			if (tpoLettersByPrice != null)
				tpoLettersByPrice.Clear();

			if (tpoLettersListByPrice != null)
				tpoLettersListByPrice.Clear();

			if (periodStartTimes != null)
				periodStartTimes.Clear();

			if (processedPricesInPeriod != null)
				processedPricesInPeriod.Clear();

			currentPeriodStart = DateTime.MinValue;
			currentLetter = 'A';
			letterIndex = 0;
			isNewPeriod = true;
			lastPrintedPeriodLetter = '\0';
		}

		#endregion

		#region Public API for Data Access (AES Compatible)

		/// <summary>
		/// Gets the complete dictionary of TPO counts by price level
		/// This is the primary data structure for export via AES
		/// </summary>
		public Dictionary<double, int> TPOCounts
		{
			get { return tpoCountsByPrice; }
		}

		/// <summary>
		/// Gets the complete dictionary of TPO letters by price level
		/// </summary>
		public Dictionary<double, string> TPOLetters
		{
			get { return tpoLettersByPrice; }
		}

		/// <summary>
		/// Gets the TPO count at a specific price level
		/// </summary>
		/// <param name="price">The price level to query</param>
		/// <returns>TPO count at that price, or 0 if no TPO data exists</returns>
		public int GetTPOCountAtPrice(double price)
		{
			double roundedPrice = RoundToTickLevel(price);

			if (tpoCountsByPrice != null && tpoCountsByPrice.ContainsKey(roundedPrice))
				return tpoCountsByPrice[roundedPrice];

			return 0;
		}

		/// <summary>
		/// Gets the TPO letters at a specific price level
		/// </summary>
		/// <param name="price">The price level to query</param>
		/// <returns>String of TPO letters at that price (e.g., "ABC"), or empty string if no TPO data</returns>
		public string GetTPOLettersAtPrice(double price)
		{
			double roundedPrice = RoundToTickLevel(price);

			if (tpoLettersByPrice != null && tpoLettersByPrice.ContainsKey(roundedPrice))
				return tpoLettersByPrice[roundedPrice];

			return "";
		}

		/// <summary>
		/// Gets all price levels that have TPO data
		/// </summary>
		/// <returns>List of all price levels with TPO counts</returns>
		public List<double> GetAllTPOPrices()
		{
			if (tpoCountsByPrice != null)
				return new List<double>(tpoCountsByPrice.Keys);

			return new List<double>();
		}

		/// <summary>
		/// Gets the price level with the highest TPO count (Point of Control)
		/// </summary>
		/// <returns>Price level with max TPO count, or 0 if no data</returns>
		public double GetPointOfControl()
		{
			if (tpoCountsByPrice == null || tpoCountsByPrice.Count == 0)
				return 0;

			return tpoCountsByPrice.OrderByDescending(kvp => kvp.Value).First().Key;
		}

		/// <summary>
		/// Gets the maximum TPO count across all price levels
		/// </summary>
		/// <returns>Maximum TPO count, or 0 if no data</returns>
		public int GetMaxTPOCount()
		{
			if (tpoCountsByPrice == null || tpoCountsByPrice.Count == 0)
				return 0;

			return tpoCountsByPrice.Values.Max();
		}

		/// <summary>
		/// Gets the current TPO period letter
		/// </summary>
		public char GetCurrentPeriodLetter()
		{
			return currentLetter;
		}

		/// <summary>
		/// Gets the total number of TPO periods processed
		/// </summary>
		public int GetTotalPeriods()
		{
			return letterIndex + 1;
		}

		/// <summary>
		/// Gets all TPO data as a sorted list (by price descending)
		/// Useful for Market Profile visualization
		/// </summary>
		public List<TPOLevel> GetTPOProfile()
		{
			if (tpoCountsByPrice == null)
				return new List<TPOLevel>();

			var profile = new List<TPOLevel>();

			foreach (var kvp in tpoCountsByPrice.OrderByDescending(x => x.Key))
			{
				profile.Add(new TPOLevel
				{
					Price = kvp.Key,
					Count = kvp.Value,
					Letters = tpoLettersByPrice.ContainsKey(kvp.Key) ? tpoLettersByPrice[kvp.Key] : ""
				});
			}

			return profile;
		}

		#endregion

		#region Properties

		[Range(1, 120)]
		[NinjaScriptProperty]
		[Display(Name = "TPO Period (minutes)", Description = "Duration of each TPO period in minutes", Order = 1, GroupName = "TPO Parameters")]
		public int TPOPeriodMinutes { get; set; }

		[Range(1, 10)]
		[NinjaScriptProperty]
		[Display(Name = "Tick Size Multiplier", Description = "Groups price levels by multiples of tick size (1 = every tick, 2 = every 2 ticks, etc.)", Order = 2, GroupName = "TPO Parameters")]
		public int TickSizeMultiplier { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Use RTH Only", Description = "Only calculate TPO during Regular Trading Hours", Order = 3, GroupName = "TPO Parameters")]
		public bool UseRTHOnly { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Debug Info", Description = "Print debug information to Output window", Order = 4, GroupName = "TPO Parameters")]
		public bool ShowDebugInfo { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show TPO By Price Level", Description = "Print TPO data organized by price level to Output window at end of each period", Order = 5, GroupName = "TPO Parameters")]
		public bool ShowTPOByPriceLevel { get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> TPOCount
		{
			get { return Values[0]; }
		}

		#endregion
	}

	/// <summary>
	/// Represents a single TPO level with price, count, and letters
	/// </summary>
	public class TPOLevel
	{
		public double Price { get; set; }
		public int Count { get; set; }
		public string Letters { get; set; }

		public override string ToString()
		{
			return string.Format("Price: {0:F2}, Count: {1}, Letters: {2}", Price, Count, Letters);
		}
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private BasicTPO[] cacheBasicTPO;
		public BasicTPO BasicTPO(int tPOPeriodMinutes, int tickSizeMultiplier, bool useRTHOnly, bool showDebugInfo, bool showTPOByPriceLevel)
		{
			return BasicTPO(Input, tPOPeriodMinutes, tickSizeMultiplier, useRTHOnly, showDebugInfo, showTPOByPriceLevel);
		}

		public BasicTPO BasicTPO(ISeries<double> input, int tPOPeriodMinutes, int tickSizeMultiplier, bool useRTHOnly, bool showDebugInfo, bool showTPOByPriceLevel)
		{
			if (cacheBasicTPO != null)
				for (int idx = 0; idx < cacheBasicTPO.Length; idx++)
					if (cacheBasicTPO[idx] != null && cacheBasicTPO[idx].TPOPeriodMinutes == tPOPeriodMinutes && cacheBasicTPO[idx].TickSizeMultiplier == tickSizeMultiplier && cacheBasicTPO[idx].UseRTHOnly == useRTHOnly && cacheBasicTPO[idx].ShowDebugInfo == showDebugInfo && cacheBasicTPO[idx].ShowTPOByPriceLevel == showTPOByPriceLevel && cacheBasicTPO[idx].EqualsInput(input))
						return cacheBasicTPO[idx];
			return CacheIndicator<BasicTPO>(new BasicTPO(){ TPOPeriodMinutes = tPOPeriodMinutes, TickSizeMultiplier = tickSizeMultiplier, UseRTHOnly = useRTHOnly, ShowDebugInfo = showDebugInfo, ShowTPOByPriceLevel = showTPOByPriceLevel }, input, ref cacheBasicTPO);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.BasicTPO BasicTPO(int tPOPeriodMinutes, int tickSizeMultiplier, bool useRTHOnly, bool showDebugInfo, bool showTPOByPriceLevel)
		{
			return indicator.BasicTPO(Input, tPOPeriodMinutes, tickSizeMultiplier, useRTHOnly, showDebugInfo, showTPOByPriceLevel);
		}

		public Indicators.BasicTPO BasicTPO(ISeries<double> input , int tPOPeriodMinutes, int tickSizeMultiplier, bool useRTHOnly, bool showDebugInfo, bool showTPOByPriceLevel)
		{
			return indicator.BasicTPO(input, tPOPeriodMinutes, tickSizeMultiplier, useRTHOnly, showDebugInfo, showTPOByPriceLevel);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.BasicTPO BasicTPO(int tPOPeriodMinutes, int tickSizeMultiplier, bool useRTHOnly, bool showDebugInfo, bool showTPOByPriceLevel)
		{
			return indicator.BasicTPO(Input, tPOPeriodMinutes, tickSizeMultiplier, useRTHOnly, showDebugInfo, showTPOByPriceLevel);
		}

		public Indicators.BasicTPO BasicTPO(ISeries<double> input , int tPOPeriodMinutes, int tickSizeMultiplier, bool useRTHOnly, bool showDebugInfo, bool showTPOByPriceLevel)
		{
			return indicator.BasicTPO(input, tPOPeriodMinutes, tickSizeMultiplier, useRTHOnly, showDebugInfo, showTPOByPriceLevel);
		}
	}
}

#endregion
