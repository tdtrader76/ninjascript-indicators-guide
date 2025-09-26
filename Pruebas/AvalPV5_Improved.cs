#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
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
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public enum LabelAlignment
    {
        Start,
        Middle,
        End
    }

    public enum NR2LevelType
    {
        PreviousDayClose,
        CurrentDayOpen
    }

    public class AvalPV5Improved : Indicator
    {
        #region Variables

        // A class to hold all data related to a single day's levels
        private class DayLevels
        {
            public Dictionary<string, PriceLevel> Levels { get; }
            public int StartBarIndex { get; set; }
            public int EndBarIndex { get; set; }

            public DayLevels()
            {
                Levels = new Dictionary<string, PriceLevel>();
                StartBarIndex = -1;
                EndBarIndex = -1;
            }
        }

        // Input Parameters
        private DateTime selectedDate;
        private double manualPrice;
        private NR2LevelType nr2LevelType;

        // Session management variables for automatic daily updates
        private SessionIterator sessionIterator;
        private DateTime currentDate = Core.Globals.MinDate;
        private double currentDayHigh;
        private double currentDayLow;
        private double currentDayOpen;
        private double priorDayHigh;
        private double priorDayLow;
        private double priorDayOpen;
        private int manualStartBar = -1; private int manualEndBar = -1;

        // Data structures for managing current and historical price levels
        private DayLevels currentDayLevels;
        private readonly Queue<DayLevels> historicalLevels = new Queue<DayLevels>();

        // Represents a calculated price level
        private class PriceLevel : IDisposable
        {
            public string Name { get; }
            public System.Windows.Media.Brush LineBrush { get; }
            public double Value { get; set; }
            public TextLayout LabelLayout { get; set; }
            public string Modifier { get; set; }

            public PriceLevel(string name, System.Windows.Media.Brush brush, string modifier = "")
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
                LineBrush = brush ?? throw new ArgumentNullException(nameof(brush));
                Value = double.NaN;
                Modifier = modifier;
            }

            public void Dispose()
            {
                LabelLayout?.Dispose();
                LabelLayout = null;
            }
        }

        // Caching and performance optimization
        private readonly Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush> dxBrushes = new Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush>();
        private bool needsLayoutUpdate = false;

        // Performance optimization - cached calculations
        private readonly Dictionary<double, double> quarterRoundingCache = new Dictionary<double, double>();
        private bool isInitialized = false;
        #endregion

        protected override void OnStateChange()
        {
            switch (State)
            {
                case State.SetDefaults:
                    Description = @"Calculates and displays price levels based on the previous day's range and a manual price. (Improved Version)";
                    Name = "AvalPV5Improved";
                    Calculate = Calculate.OnBarClose;
                    IsOverlay = true;
                    DisplayInDataBox = true;
                    DrawOnPricePanel = true;
                    DrawHorizontalGridLines = true;
                    DrawVerticalGridLines = true;
                    PaintPriceMarkers = true;
                    ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                    IsSuspendedWhileInactive = true;

                    // Default Parameters
                    UseAutomaticDate = true;
                    DaysToDraw = 5;
                    Nr2LevelType = NR2LevelType.PreviousDayClose;
                    UseGapCalculation = false;
                    SelectedDate = DateTime.Today;
                    ManualPrice = 0.0;
                    Width = 1;
                    LineBufferPixels = 125;
                    LabelAlignment = LabelAlignment.End;

                    AddPlot(Brushes.Transparent, "Q1");
                    AddPlot(Brushes.Transparent, "Q8");
                    AddPlot(Brushes.Transparent, "Q3");
                    AddPlot(Brushes.Transparent, "Q5");
                    AddPlot(Brushes.Transparent, "Q4");
                    AddPlot(Brushes.Transparent, "Q6");
                    AddPlot(Brushes.Transparent, "Q2");
                    AddPlot(Brushes.Transparent, "ZSell");
                    AddPlot(Brushes.Transparent, "NR2");
                    AddPlot(Brushes.Transparent, "ZBuy");
                    AddPlot(Brushes.Transparent, "Q7");
                    AddPlot(Brushes.Transparent, "Std1Plus");
                    AddPlot(Brushes.Transparent, "Std2Plus");
                    AddPlot(Brushes.Transparent, "Std3Plus");
                    AddPlot(Brushes.Transparent, "OneDPlus");
                    AddPlot(Brushes.Transparent, "Std1Minus");
                    AddPlot(Brushes.Transparent, "Std2Minus");
                    AddPlot(Brushes.Transparent, "Std3Minus");
                    AddPlot(Brushes.Transparent, "OneDMinus");
                    break;

                case State.Configure:
                    try
                    {
                        // Add the daily data series for prior day's close
                        AddDataSeries(Bars.Instrument.FullName, BarsPeriodType.Day, 1);

                        // Initialize the price levels structure for the current day
                        currentDayLevels = new DayLevels();
                        InitializePriceLevels(currentDayLevels.Levels);

                        // Reset session variables
                        ResetSessionVariables();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in Configure state: {ex.Message}", ex);
                    }
                    break;

                case State.DataLoaded:
                    try
                    {
                        if (UseAutomaticDate && sessionIterator == null)
                        {
                            sessionIterator = new SessionIterator(Bars);
                        }
                        Print("AvalPV5Improved: Data loaded and initialized successfully");
                        isInitialized = true;
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error initializing SessionIterator: {ex.Message}", ex);
                    }
                    break;

                case State.Terminated:
                    CleanupResources();
                    break;

                case State.Historical:
                    try
                    {
                        SetZOrder(-1);

                        if (!ValidateChartType()) return;

                        if (!UseAutomaticDate)
                        {
                            CalculateLevelsForDate();
                            CalculateManualBarRange();
                            if (currentDayLevels != null)
                            {
                                currentDayLevels.StartBarIndex = manualStartBar;
                                currentDayLevels.EndBarIndex = manualEndBar;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in Historical state: {ex.Message}", ex);
                    }
                    break;

                case State.Realtime:
                    try
                    {
                        ChartControl?.Dispatcher?.BeginInvoke(new Action(() =>
                        {
                            // Future interaction logic can be placed here
                        }));
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error in Realtime state: {ex.Message}", ex);
                    }
                    break;
            }
        }

        protected override void OnBarUpdate()
        {
            if (!UseAutomaticDate || !isInitialized) return;

            try
            {
                if (!ValidateBarData()) return;

                DateTime tradingDay = GetTradingDay();
                if (tradingDay == DateTime.MinValue) return;

                bool isNewDay = IsNewTradingDay(tradingDay);

                if (isNewDay)
                {
                    ProcessNewTradingDay(tradingDay);
                }
                else
                {
                    UpdateCurrentDayData();
                }
            }
            catch (Exception ex)
            {
                LogError($"Error in OnBarUpdate: {ex.Message}", ex);
            }
        }

        #region Private Methods - Validation and Initialization

        private bool ValidateChartType()
        {
            if (!Bars.BarsType.IsIntraday)
            {
                Draw.TextFixed(this, "NinjaScriptInfo", "AvalPV5Improved only works on intraday charts", TextPosition.BottomRight);
                return false;
            }
            return true;
        }

        private bool ValidateBarData()
        {
            return CurrentBar >= 1 && sessionIterator != null;
        }

        private void ResetSessionVariables()
        {
            currentDate = Core.Globals.MinDate;
            currentDayHigh = 0;
            currentDayLow = 0;
            currentDayOpen = 0;
            priorDayHigh = 0;
            priorDayLow = 0;
            priorDayOpen = 0;
            manualStartBar = -1;
            manualEndBar = -1;
            sessionIterator = null;
            historicalLevels.Clear();
        }

        private void CleanupResources()
        {
            try
            {
                // Dispose of DX resources safely
                if (dxBrushes != null)
                {
                    foreach (var brush in dxBrushes.Values)
                    {
                        try
                        {
                            brush?.Dispose();
                        }
                        catch (Exception brushEx)
                        {
                            // Log individual brush disposal errors but continue
                            Print($"Warning: Error disposing DX brush: {brushEx.Message}");
                        }
                    }
                    dxBrushes.Clear();
                }

                // Dispose of all level objects safely
                try
                {
                    if (currentDayLevels?.Levels != null)
                    {
                        foreach (var level in currentDayLevels.Levels.Values)
                        {
                            level?.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Print($"Warning: Error disposing current day levels: {ex.Message}");
                }

                try
                {
                    if (historicalLevels != null)
                    {
                        foreach (var day in historicalLevels)
                        {
                            if (day?.Levels != null)
                            {
                                foreach (var level in day.Levels.Values)
                                {
                                    level?.Dispose();
                                }
                            }
                        }
                        historicalLevels.Clear();
                    }
                }
                catch (Exception ex)
                {
                    Print($"Warning: Error disposing historical levels: {ex.Message}");
                }

                quarterRoundingCache?.Clear();
            }
            catch (Exception ex)
            {
                LogError($"Error during cleanup: {ex.Message}", ex);
            }
        }

        #endregion

        #region Private Methods - Trading Day Processing

        private DateTime GetTradingDay()
        {
            try
            {
                return sessionIterator.GetTradingDay(Time[0]);
            }
            catch (Exception ex)
            {
                LogError($"Error getting trading day: {ex.Message}", ex);
                return DateTime.MinValue;
            }
        }

        private bool IsNewTradingDay(DateTime tradingDay)
        {
            return currentDate != tradingDay || currentDayOpen == 0;
        }

        private void ProcessNewTradingDay(DateTime tradingDay)
        {
            try
            {
                FinalizePreviousDay();
                InitializeNewDay(tradingDay);
                CalculateNewDayLevels();
            }
            catch (Exception ex)
            {
                LogError($"Error processing new trading day: {ex.Message}", ex);
            }
        }

        private void FinalizePreviousDay()
        {
            if (currentDayLevels?.StartBarIndex != -1)
            {
                currentDayLevels.EndBarIndex = CurrentBar - 1;
                historicalLevels.Enqueue(currentDayLevels);
                needsLayoutUpdate = true; // Force text layout update for historical levels

                // Maintain the queue size
                while (historicalLevels.Count > DaysToDraw)
                {
                    var oldDay = historicalLevels.Dequeue();
                    oldDay?.Levels?.Values?.ToList().ForEach(level => level?.Dispose());
                }
            }
        }

        private void InitializeNewDay(DateTime tradingDay)
        {
            currentDayLevels = new DayLevels();
            currentDayLevels.StartBarIndex = CurrentBar;
            InitializePriceLevels(currentDayLevels.Levels);

            // Store previous day values
            priorDayHigh = currentDayHigh;
            priorDayLow = currentDayLow;

            // Initialize current day values
            if (IsValidBarData(0))
            {
                currentDayOpen = Open[0];
                currentDayHigh = High[0];
                currentDayLow = Low[0];
            }

            currentDate = tradingDay;
        }

        private void CalculateNewDayLevels()
        {
            if (!IsValidPriorDayData()) return;

            double previousDayRange = CalculatePreviousDayRange();
            if (previousDayRange <= 0) return;

            double priorDayClose = GetPriorDayClose(Time[0], true);

            if (UseGapCalculation)
            {
                previousDayRange = ApplyGapCalculation(previousDayRange, priorDayClose, Time[0]);
            }

            double basePrice = GetCalculationBasePrice(priorDayClose);

            if (basePrice > 0)
            {
                Print($"DIAGNOSTIC: Using {Nr2LevelType} for NR2 level calculation");
                CalculateAllLevels(previousDayRange, basePrice, currentDayLevels.Levels);
                needsLayoutUpdate = true;
            }
        }

        private void UpdateCurrentDayData()
        {
            if (IsValidBarData(0))
            {
                currentDayHigh = Math.Max(currentDayHigh, High[0]);
                currentDayLow = Math.Min(currentDayLow, Low[0]);
            }
        }

        #endregion

        #region Private Methods - Data Validation

        private bool IsValidBarData(int barsAgo)
        {
            return Open.IsValidDataPoint(barsAgo) &&
                   High.IsValidDataPoint(barsAgo) &&
                   Low.IsValidDataPoint(barsAgo) &&
                   !double.IsNaN(Open[barsAgo]) &&
                   !double.IsNaN(High[barsAgo]) &&
                   !double.IsNaN(Low[barsAgo]) &&
                   !double.IsInfinity(Open[barsAgo]) &&
                   !double.IsInfinity(High[barsAgo]) &&
                   !double.IsInfinity(Low[barsAgo]);
        }

        private bool IsValidPriorDayData()
        {
            return priorDayHigh > 0 &&
                   priorDayLow > 0 &&
                   priorDayHigh >= priorDayLow &&
                   !double.IsNaN(priorDayHigh) &&
                   !double.IsNaN(priorDayLow) &&
                   !double.IsInfinity(priorDayHigh) &&
                   !double.IsInfinity(priorDayLow);
        }

        private bool IsValidDailySeriesData()
        {
            return BarsArray != null &&
                   BarsArray.Length > 1 &&
                   BarsArray[1] != null &&
                   BarsArray[1].Count >= 2;
        }

        #endregion

        #region Private Methods - Level Calculations

        private void InitializePriceLevels(Dictionary<string, PriceLevel> levels)
        {
            try
            {
                var levelDefinitions = new[]
                {
                    new { Name = "Q1", Brush = Brushes.Yellow, Modifier = "0.5" },
                    new { Name = "Q8", Brush = Brushes.Yellow, Modifier = "0.5" },
                    new { Name = "Q3", Brush = Brushes.Plum, Modifier = "0.25" },
                    new { Name = "Q5", Brush = Brushes.Plum, Modifier = "0.25" },
                    new { Name = "Q4", Brush = Brushes.ForestGreen, Modifier = "0.375" },
                    new { Name = "Q6", Brush = Brushes.IndianRed, Modifier = "0.375" },
                    new { Name = "Q2", Brush = Brushes.ForestGreen, Modifier = "0.0855" },
                    new { Name = "ZSell", Brush = Brushes.BlueViolet, Modifier = "0.171" },
                    new { Name = "NR2", Brush = Brushes.Gold, Modifier = "Base" },
                    new { Name = "ZBuy", Brush = Brushes.BlueViolet, Modifier = "0.159" },
                    new { Name = "Q7", Brush = Brushes.IndianRed, Modifier = "0.125" },
                    new { Name = "Std1+", Brush = Brushes.ForestGreen, Modifier = "0.0855" },
                    new { Name = "Std2+", Brush = Brushes.ForestGreen, Modifier = "0.171" },
                    new { Name = "Std3+", Brush = Brushes.ForestGreen, Modifier = "0.342" },
                    new { Name = "1D+", Brush = Brushes.Gold, Modifier = "0.50" },
                    new { Name = "Std1-", Brush = Brushes.IndianRed, Modifier = "0.0855" },
                    new { Name = "Std2-", Brush = Brushes.IndianRed, Modifier = "0.171" },
                    new { Name = "Std3-", Brush = Brushes.IndianRed, Modifier = "0.342" },
                    new { Name = "1D-", Brush = Brushes.Gold, Modifier = "0.50" }
                };

                levels.Clear();
                foreach (var def in levelDefinitions)
                {
                    if (def.Brush != null && !string.IsNullOrEmpty(def.Name))
                    {
                        levels[def.Name] = new PriceLevel(def.Name, def.Brush, def.Modifier);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error initializing price levels: {ex.Message}", ex);
            }
        }

        private double RoundToQuarter(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;

            // Use cache to improve performance for repeated calculations
            if (quarterRoundingCache.TryGetValue(value, out double cached))
                return cached;

            double rounded = Math.Round(value * 4.0, MidpointRounding.AwayFromZero) / 4.0;

            // Cache the result (limit cache size to prevent memory issues)
            if (quarterRoundingCache.Count < 1000)
                quarterRoundingCache[value] = rounded;

            return rounded;
        }

        private double CalculatePreviousDayRange()
        {
            return priorDayHigh - priorDayLow;
        }

        private double GetCalculationBasePrice(double priorDayClose)
        {
            double basePrice = ManualPrice;
            if (basePrice.ApproxCompare(0) == 0)
            {
                basePrice = GetBasePriceForNR2(Time[0], priorDayClose);
            }
            return basePrice;
        }

        #endregion

        #region Private Methods - Data Access

        private double GetPriorDayClose(DateTime time, bool printLog = false)
        {
            try
            {
                if (!IsValidDailySeriesData())
                {
                    if (printLog) Print("DIAGNOSTIC: Daily series not ready or not enough data to find prior day's close.");
                    return 0;
                }

                int dailyIndex = BarsArray[1].GetBar(time);

                if (dailyIndex > 0)
                {
                    int priorDayIndex = dailyIndex - 1;
                    double priorDayClose = BarsArray[1].GetClose(priorDayIndex);

                    if (printLog && !double.IsNaN(priorDayClose) && !double.IsInfinity(priorDayClose))
                    {
                        DateTime priorDayTime = BarsArray[1].GetTime(priorDayIndex);
                        Print($"DIAGNOSTIC: Using prior day close of {priorDayClose} from daily candle at {priorDayTime} (triggered by bar at {time})");
                    }

                    return double.IsNaN(priorDayClose) || double.IsInfinity(priorDayClose) ? 0 : priorDayClose;
                }

                if (printLog) Print($"DIAGNOSTIC: Could not find a prior day's close for the date {time:d}. The provided time might be too early in the data series.");
                return 0;
            }
            catch (Exception ex)
            {
                LogError($"Error getting prior day close: {ex.Message}", ex);
                return 0;
            }
        }

        private double GetBasePriceForNR2(DateTime time, double priorDayClose)
        {
            try
            {
                if (Nr2LevelType == NR2LevelType.CurrentDayOpen && IsValidDailySeriesData())
                {
                    int dailyIndex = BarsArray[1].GetBar(time);

                    if (dailyIndex >= 0)
                    {
                        double currentDayOpen = BarsArray[1].GetOpen(dailyIndex);

                        if (!double.IsNaN(currentDayOpen) && !double.IsInfinity(currentDayOpen))
                        {
                            Print($"DIAGNOSTIC: Using current day open of {currentDayOpen} for NR2 level");
                            return currentDayOpen;
                        }
                    }

                    Print("DIAGNOSTIC: Could not get current day open, using prior day close for NR2 level");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error getting base price for NR2: {ex.Message}", ex);
            }

            return priorDayClose;
        }

        private double ApplyGapCalculation(double previousDayRange, double priorDayClose, DateTime time)
        {
            try
            {
                Print($"DIAGNOSTIC: Initial previous day range = {previousDayRange}");

                if (!IsValidDailySeriesData()) return previousDayRange;

                int dailyIndex = BarsArray[1].GetBar(time);

                if (dailyIndex >= 0)
                {
                    double currentDailyOpen = BarsArray[1].GetOpen(dailyIndex);

                    if (!double.IsNaN(currentDailyOpen) && !double.IsInfinity(currentDailyOpen))
                    {
                        Print($"GAP Calc: Using daily open of {currentDailyOpen} from daily candle at {BarsArray[1].GetTime(dailyIndex)} for intraday bar at {time}");

                        if (priorDayClose > 0 && !currentDailyOpen.ApproxCompare(priorDayClose).Equals(0))
                        {
                            double gap = Math.Abs(currentDailyOpen - priorDayClose);
                            Print($"DIAGNOSTIC: Gap calculation = |{currentDailyOpen} (Open) - {priorDayClose} (Close)| = {gap}");

                            double originalRange = previousDayRange;
                            previousDayRange += gap;
                            Print($"DIAGNOSTIC: Modified range = {originalRange} (Initial Range) + {gap} (Gap) = {previousDayRange}");
                        }
                        else if (priorDayClose > 0)
                        {
                            Print($"DIAGNOSTIC: No gap detected - Open: {currentDailyOpen}, Close: {priorDayClose}");
                        }
                    }
                }

                return previousDayRange;
            }
            catch (Exception ex)
            {
                LogError($"Error applying gap calculation: {ex.Message}", ex);
                return previousDayRange;
            }
        }

        #endregion

        #region Private Methods - Manual Date Processing

        private void CalculateLevelsForDate()
        {
            try
            {
                if (!IsValidDailySeriesData())
                {
                    Print("DIAGNOSTIC: Daily data series not available for manual calculation.");
                    return;
                }

                DateTime dateForLevels = SelectedDate.Date;
                int dateForLevels_DailyIndex = BarsArray[1].GetBar(dateForLevels);

                if (dateForLevels_DailyIndex < 0)
                {
                    Print($"DIAGNOSTIC: No daily data found for the selected date: {dateForLevels:d}.");
                    return;
                }

                int dateForRangeCalc_DailyIndex = dateForLevels_DailyIndex - 1;

                if (dateForRangeCalc_DailyIndex < 0)
                {
                    Print($"DIAGNOSTIC: No prior day data found to calculate levels for {dateForLevels:d}.");
                    return;
                }

                var priorDayData = GetPriorDayData(dateForRangeCalc_DailyIndex, dateForLevels_DailyIndex);
                if (!priorDayData.IsValid) return;

                double range = CalculateRangeWithGap(priorDayData);
                if (range <= 0) return;

                double basePrice = GetManualModeBasePrice(priorDayData, dateForLevels_DailyIndex);

                if (basePrice > 0)
                {
                    Print($"DIAGNOSTIC: Using Base Price={basePrice} and Range={range} for calculations.");
                    CalculateAllLevels(range, basePrice, currentDayLevels.Levels);
                    needsLayoutUpdate = true;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error calculating levels for date: {ex.Message}", ex);
            }
        }

        private (double High, double Low, double Close, DateTime Date, bool IsValid) GetPriorDayData(int priorDayIndex, int currentDayIndex)
        {
            try
            {
                double priorDayHigh = BarsArray[1].GetHigh(priorDayIndex);
                double priorDayLow = BarsArray[1].GetLow(priorDayIndex);
                double priorDayClose = BarsArray[1].GetClose(priorDayIndex);
                DateTime priorDayDate = BarsArray[1].GetTime(priorDayIndex).Date;

                bool isValid = !double.IsNaN(priorDayHigh) && !double.IsNaN(priorDayLow) && !double.IsNaN(priorDayClose) &&
                              !double.IsInfinity(priorDayHigh) && !double.IsInfinity(priorDayLow) && !double.IsInfinity(priorDayClose) &&
                              priorDayHigh > 0 && priorDayLow > 0;

                if (isValid)
                {
                    Print($"DIAGNOSTIC: For levels on {BarsArray[1].GetTime(currentDayIndex).Date:d}, using data from {priorDayDate:d}. High={priorDayHigh}, Low={priorDayLow}, Close={priorDayClose}");
                }

                return (priorDayHigh, priorDayLow, priorDayClose, priorDayDate, isValid);
            }
            catch (Exception ex)
            {
                LogError($"Error getting prior day data: {ex.Message}", ex);
                return (0, 0, 0, DateTime.MinValue, false);
            }
        }

        private double CalculateRangeWithGap((double High, double Low, double Close, DateTime Date, bool IsValid) priorDayData)
        {
            double range = priorDayData.High - priorDayData.Low;

            if (range <= 0)
            {
                Print("DIAGNOSTIC: Calculated range is zero or negative. Levels will not be drawn.");
                return 0;
            }

            if (UseGapCalculation)
            {
                try
                {
                    int selectedDayIndex = BarsArray[1].GetBar(SelectedDate.Date);
                    if (selectedDayIndex >= 0)
                    {
                        double selectedDayOpen = BarsArray[1].GetOpen(selectedDayIndex);

                        if (!double.IsNaN(selectedDayOpen) && !double.IsInfinity(selectedDayOpen))
                        {
                            Print($"GAP Calc: Using daily open of {selectedDayOpen} from {SelectedDate.Date:d} and prior close of {priorDayData.Close} from {priorDayData.Date:d}");

                            if (priorDayData.Close > 0 && selectedDayOpen > 0)
                            {
                                double gap = Math.Abs(selectedDayOpen - priorDayData.Close);
                                Print($"DIAGNOSTIC: Gap calculation = |{selectedDayOpen} (Open) - {priorDayData.Close} (Close)| = {gap}");
                                range += gap;
                                Print($"DIAGNOSTIC: Modified range (adding gap) = {range}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error calculating gap in manual mode: {ex.Message}", ex);
                }
            }

            return range;
        }

        private double GetManualModeBasePrice((double High, double Low, double Close, DateTime Date, bool IsValid) priorDayData, int selectedDayIndex)
        {
            double basePrice = ManualPrice;

            if (basePrice.ApproxCompare(0) == 0)
            {
                if (Nr2LevelType == NR2LevelType.CurrentDayOpen)
                {
                    basePrice = BarsArray[1].GetOpen(selectedDayIndex);
                }
                else
                {
                    basePrice = priorDayData.Close;
                }
            }

            return basePrice;
        }

        private void CalculateManualBarRange()
        {
            try
            {
                for (int i = 0; i < Bars.Count; i++)
                {
                    if (Bars.GetTime(i).Date == SelectedDate.Date)
                    {
                        if (manualStartBar == -1)
                            manualStartBar = i;

                        if (Bars.GetTime(i).Hour <= 22)
                        {
                            manualEndBar = i;
                        }
                    }
                    else if (manualStartBar != -1)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error calculating manual bar range: {ex.Message}", ex);
            }
        }

        #endregion

        #region Private Methods - Level Calculations Implementation

        private void CalculateAllLevels(double dayRange, double basePrice, Dictionary<string, PriceLevel> levels)
        {
            try
            {
                if (basePrice <= 0 || dayRange <= 0 || double.IsNaN(basePrice) || double.IsNaN(dayRange) ||
                    double.IsInfinity(basePrice) || double.IsInfinity(dayRange))
                {
                    Print("DIAGNOSTIC: Invalid input parameters for level calculation");
                    return;
                }

                // Pre-calculate common values for efficiency
                var calculations = PreCalculateRangeMultipliers(dayRange);
                double halfRange = dayRange / 2.0;
                double q1Level = RoundToQuarter(basePrice + halfRange);
                double q4Level = RoundToQuarter(basePrice - halfRange);

                // Calculate and assign all levels with error checking
                var levelCalculations = new Dictionary<string, Func<double>>
                {
                    ["Q1"] = () => q1Level,
                    ["Q8"] = () => q4Level,
                    ["Q3"] = () => RoundToQuarter(q1Level - calculations.range025),
                    ["Q5"] = () => RoundToQuarter(q4Level + calculations.range025),
                    ["Q4"] = () => RoundToQuarter(q1Level - calculations.range0375),
                    ["Q6"] = () => RoundToQuarter(q4Level + calculations.range0375),
                    ["NR2"] = () => RoundToQuarter(basePrice),
                    ["Q2"] = () => RoundToQuarter(q1Level - calculations.range_tc_std1),
                    ["ZSell"] = () => RoundToQuarter(q1Level - calculations.range_nr1_std2),
                    ["Std1+"] = () => RoundToQuarter(q1Level + calculations.range_tc_std1),
                    ["Std2+"] = () => RoundToQuarter(q1Level + calculations.range_nr1_std2),
                    ["Std3+"] = () => RoundToQuarter(q1Level + calculations.range_std3),
                    ["1D+"] = () => RoundToQuarter(q1Level + calculations.range050),
                    ["ZBuy"] = () => RoundToQuarter(q4Level + calculations.range0159),
                    ["Q7"] = () => RoundToQuarter(q4Level + calculations.range0125),
                    ["Std1-"] = () => RoundToQuarter(q4Level - calculations.range_tc_std1),
                    ["Std2-"] = () => RoundToQuarter(q4Level - calculations.range_nr1_std2),
                    ["Std3-"] = () => RoundToQuarter(q4Level - calculations.range_std3),
                    ["1D-"] = () => RoundToQuarter(q4Level - calculations.range050)
                };

                // Calculate and assign values to levels and plots
                AssignLevelValues(levels, levelCalculations);

                // Log calculated levels
                LogCalculatedLevels(dayRange, levels);
            }
            catch (Exception ex)
            {
                LogError($"Error in CalculateAllLevels: {ex.Message}", ex);
            }
        }

        private (double range_tc_std1, double range_nr1_std2, double range_std3, double range0159,
                double range025, double range0375, double range050, double range0125) PreCalculateRangeMultipliers(double dayRange)
        {
            return (
                range_tc_std1: dayRange * 0.0855,
                range_nr1_std2: dayRange * 0.171,
                range_std3: dayRange * 0.342,
                range0159: dayRange * 0.159,
                range025: dayRange * 0.25,
                range0375: dayRange * 0.375,
                range050: dayRange * 0.50,
                range0125: dayRange * 0.125
            );
        }

        private void AssignLevelValues(Dictionary<string, PriceLevel> levels, Dictionary<string, Func<double>> calculations)
        {
            foreach (var calc in calculations)
            {
                if (levels.TryGetValue(calc.Key, out PriceLevel level))
                {
                    try
                    {
                        double value = calc.Value();
                        if (!double.IsNaN(value) && !double.IsInfinity(value))
                        {
                            level.Value = value;
                            AssignToPlot(calc.Key, value);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error calculating level {calc.Key}: {ex.Message}", ex);
                    }
                }
            }
        }

        private void AssignToPlot(string levelName, double value)
        {
            if (CurrentBar < 0 || value <= 0) return;

            try
            {
                switch (levelName)
                {
                    case "Q1": Q1[0] = value; break;
                    case "Q8": Q8[0] = value; break;
                    case "Q3": Q3[0] = value; break;
                    case "Q5": Q5[0] = value; break;
                    case "Q4": Q4[0] = value; break;
                    case "Q6": Q6[0] = value; break;
                    case "Q2": Q2[0] = value; break;
                    case "ZSell": ZSell[0] = value; break;
                    case "NR2": NR2[0] = value; break;
                    case "ZBuy": ZBuy[0] = value; break;
                    case "Q7": Q7[0] = value; break;
                    case "Std1+": Std1Plus[0] = value; break;
                    case "Std2+": Std2Plus[0] = value; break;
                    case "Std3+": Std3Plus[0] = value; break;
                    case "1D+": OneDPlus[0] = value; break;
                    case "Std1-": Std1Minus[0] = value; break;
                    case "Std2-": Std2Minus[0] = value; break;
                    case "Std3-": Std3Minus[0] = value; break;
                    case "1D-": OneDMinus[0] = value; break;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error assigning plot value for {levelName}: {ex.Message}", ex);
            }
        }

        #endregion

        #region OnRender

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            try
            {
                // Only update text layouts when necessary
                if (needsLayoutUpdate)
                {
                    CreateTextLayouts(chartControl);
                    needsLayoutUpdate = false;
                }

                // Draw all historical levels - let NinjaTrader handle clipping
                foreach (var day in historicalLevels)
                {
                    DrawDayLevels(day, chartControl, chartScale);
                }

                // Draw current day levels
                if (currentDayLevels != null)
                {
                    DrawDayLevels(currentDayLevels, chartControl, chartScale);
                }
            }
            catch (Exception ex)
            {
                LogError($"Error in OnRender: {ex.Message}", ex);
            }
        }


        private void DrawDayLevels(DayLevels dayLevels, ChartControl chartControl, ChartScale chartScale)
        {
            if (dayLevels == null || dayLevels.StartBarIndex == -1) return;

            // Get white brush for labels
            if (!dxBrushes.TryGetValue(Brushes.White, out var whiteBrush))
            {
                whiteBrush = Brushes.White.ToDxBrush(RenderTarget);
                dxBrushes[Brushes.White] = whiteBrush;
            }

            foreach (var level in dayLevels.Levels.Values)
            {
                if (double.IsNaN(level.Value) || level.Value <= 0) continue;

                float y = chartScale.GetYByValue(level.Value);

                // Draw the line across the entire level range
                float lineStartX = chartControl.GetXByBarIndex(ChartBars, dayLevels.StartBarIndex);
                float lineEndX;

                if (dayLevels.EndBarIndex != -1)
                {
                    // Historical day: line ends at the last bar of the day.
                    lineEndX = chartControl.GetXByBarIndex(ChartBars, dayLevels.EndBarIndex);
                }
                else
                {
                    // Current day: line extends to the right edge of the chart.
                    lineEndX = (float)chartControl.CanvasRight;
                }

                // Ensure we have valid coordinates before drawing.
                if (float.IsNaN(lineStartX) || float.IsNaN(lineEndX)) continue;

                SharpDX.Direct2D1.Brush dxBrush = GetDxBrush(level.LineBrush);
                if (dxBrush == null) continue;

                // Draw the line across the full range
                RenderTarget.DrawLine(new SharpDX.Vector2(lineStartX, y), new SharpDX.Vector2(lineEndX, y), dxBrush, Width);

                // Draw labels - position them within the chart view area
                if (level.LabelLayout != null)
                {
                    var labelX = GetLabelX(lineStartX, lineEndX, chartControl);
                    RenderTarget.DrawTextLayout(
                        new SharpDX.Vector2(labelX, y - 15),
                        level.LabelLayout,
                        whiteBrush
                    );
                }
            }
        }

        private SharpDX.Direct2D1.Brush GetDxBrush(System.Windows.Media.Brush brush)
        {
            if (dxBrushes.TryGetValue(brush, out var dxBrush))
                return dxBrush;

            var solidColorBrush = brush as System.Windows.Media.SolidColorBrush;
            if (solidColorBrush == null) return null;

            dxBrush = solidColorBrush.ToDxBrush(RenderTarget);
            dxBrushes[brush] = dxBrush;
            return dxBrush;
        }

        private float GetLabelX(float startX, float endX, ChartControl chartControl)
        {
            // Get the visible area of the chart
            float chartLeft = 0;
            float chartRight = (float)chartControl.CanvasRight;

            // Calculate the preferred position based on alignment
            float preferredX = LabelAlignment switch
            {
                LabelAlignment.Start => startX + 5,
                LabelAlignment.Middle => (startX + endX) / 2,
                LabelAlignment.End => endX - 60,
                _ => endX - 60
            };

            // Ensure the label is visible within the chart area
            // If the preferred position is outside the visible area, adjust it
            if (preferredX < chartLeft)
            {
                // If start position is before visible area, place label at the left edge
                preferredX = chartLeft + 5;
            }
            else if (preferredX > chartRight - 100) // Reserve space for label width
            {
                // If end position is after visible area, place label at the right edge
                preferredX = chartRight - 100;
            }

            // Additional check: if the level line intersects the visible area,
            // ensure the label is positioned within the visible portion
            float visibleStartX = Math.Max(startX, chartLeft);
            float visibleEndX = Math.Min(endX, chartRight);

            // If the line is visible in the current viewport
            if (visibleStartX <= visibleEndX)
            {
                switch (LabelAlignment)
                {
                    case LabelAlignment.Start:
                        preferredX = visibleStartX + 5;
                        break;
                    case LabelAlignment.Middle:
                        preferredX = (visibleStartX + visibleEndX) / 2 - 50; // Center with offset for readability
                        break;
                    case LabelAlignment.End:
                        preferredX = visibleEndX - 100;
                        break;
                }

                // Final bounds check
                preferredX = Math.Max(chartLeft + 5, Math.Min(preferredX, chartRight - 100));
            }

            return preferredX;
        }

        private void CreateTextLayouts(ChartControl chartControl)
        {
            try
            {
                // Create text layouts for current day levels
                var levels = currentDayLevels?.Levels;
                if (levels != null)
                {
                    foreach (var level in levels.Values)
                    {
                        CreateSingleTextLayout(chartControl, level);
                    }
                }

                // Create text layouts for historical day levels
                foreach (var day in historicalLevels)
                {
                    if (day?.Levels != null)
                    {
                        foreach (var level in day.Levels.Values)
                        {
                            CreateSingleTextLayout(chartControl, level);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error creating text layouts: {ex.Message}", ex);
            }
        }

        private void CreateSingleTextLayout(ChartControl chartControl, PriceLevel level)
        {
            if (double.IsNaN(level.Value) || level.Value <= 0) return;

            try
            {
                level.LabelLayout?.Dispose();

                string text = $"{level.Name} {level.Value:F2} ({level.Modifier})";
                level.LabelLayout = new TextLayout(
                    Core.Globals.DirectWriteFactory,
                    text,
                    chartControl.Properties.LabelFont.ToDirectWriteTextFormat(),
                    200,
                    (float)(chartControl.Properties.LabelFont.Size + 2)
                );
            }
            catch (Exception ex)
            {
                LogError($"Error creating text layout for {level.Name}: {ex.Message}", ex);
            }
        }

        #endregion

        #region Private Methods - Logging and Error Handling

        private void LogError(string message, Exception ex = null)
        {
            string fullMessage = ex != null ? $"{message} - Exception: {ex}" : message;
            Print($"ERROR in AvalPV5Improved: {fullMessage}");

            // Log to NinjaTrader's log system if available
            try
            {
                Log(fullMessage, LogLevel.Error);
            }
            catch
            {
                // Ignore logging errors to prevent infinite loops
            }
        }

        private void LogCalculatedLevels(double dayRange, Dictionary<string, PriceLevel> levels)
        {
            try
            {
                Print($"--- NIVELES CALCULADOS (IMPROVED) ---");
                Print($"Rango del día: {dayRange:F5}");

                if (levels.TryGetValue("NR2", out var nr2Level))
                    Print($"Precio base (NR2): {nr2Level.Value:F5}");

                foreach (var level in levels.Values.OrderBy(l => l.Value))
                {
                    if (!double.IsNaN(level.Value) && level.Value > 0)
                        Print($"{level.Name}: {level.Value:F5}");
                }
                Print($"-----------------------------------");
            }
            catch (Exception ex)
            {
                LogError($"Error logging calculated levels: {ex.Message}", ex);
            }
        }
        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Use Automatic Date", Order = 1, GroupName = "Parameters")]
        public bool UseAutomaticDate { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Days to Draw", Order = 2, GroupName = "Parameters")]
        public int DaysToDraw { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "NR2 Level Type", Order = 3, GroupName = "Parameters")]
        public NR2LevelType Nr2LevelType { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Gap Calculation", Order = 4, GroupName = "Parameters")]
        public bool UseGapCalculation { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Selected Date", Order = 5, GroupName = "Parameters")]
        public DateTime SelectedDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Manual Price", Order = 6, GroupName = "Parameters")]
        public double ManualPrice { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Width", Order = 7, GroupName = "Visual")]
        public int Width { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Line Buffer Pixels", Order = 8, GroupName = "Visual")]
        public int LineBufferPixels { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Label Alignment", Order = 9, GroupName = "Visual")]
        public LabelAlignment LabelAlignment { get; set; }

        #endregion

        #region Plot Properties

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Q1 => Values[0];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Q8 => Values[1];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Q3 => Values[2];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Q5 => Values[3];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Q4 => Values[4];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Q6 => Values[5];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Q2 => Values[6];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> ZSell => Values[7];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> NR2 => Values[8];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> ZBuy => Values[9];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Q7 => Values[10];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Std1Plus => Values[11];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Std2Plus => Values[12];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Std3Plus => Values[13];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> OneDPlus => Values[14];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Std1Minus => Values[15];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Std2Minus => Values[16];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Std3Minus => Values[17];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> OneDMinus => Values[18];

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private AvalPV5Improved[] cacheAvalPV5Improved;
        public AvalPV5Improved AvalPV5Improved(bool useAutomaticDate, int daysToDraw, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels, LabelAlignment labelAlignment)
        {
            return AvalPV5Improved(Input, useAutomaticDate, daysToDraw, nr2LevelType, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels, labelAlignment);
        }

        public AvalPV5Improved AvalPV5Improved(ISeries<double> input, bool useAutomaticDate, int daysToDraw, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels, LabelAlignment labelAlignment)
        {
            if (cacheAvalPV5Improved != null)
                for (int idx = 0; idx < cacheAvalPV5Improved.Length; idx++)
                    if (cacheAvalPV5Improved[idx] != null && cacheAvalPV5Improved[idx].UseAutomaticDate == useAutomaticDate && cacheAvalPV5Improved[idx].DaysToDraw == daysToDraw && cacheAvalPV5Improved[idx].Nr2LevelType == nr2LevelType && cacheAvalPV5Improved[idx].UseGapCalculation == useGapCalculation && cacheAvalPV5Improved[idx].SelectedDate == selectedDate && cacheAvalPV5Improved[idx].ManualPrice == manualPrice && cacheAvalPV5Improved[idx].Width == width && cacheAvalPV5Improved[idx].LineBufferPixels == lineBufferPixels && cacheAvalPV5Improved[idx].LabelAlignment == labelAlignment && cacheAvalPV5Improved[idx].EqualsInput(input))
                        return cacheAvalPV5Improved[idx];
            return CacheIndicator<AvalPV5Improved>(new AvalPV5Improved(){ UseAutomaticDate = useAutomaticDate, DaysToDraw = daysToDraw, Nr2LevelType = nr2LevelType, UseGapCalculation = useGapCalculation, SelectedDate = selectedDate, ManualPrice = manualPrice, Width = width, LineBufferPixels = lineBufferPixels, LabelAlignment = labelAlignment }, input, ref cacheAvalPV5Improved);
        }
    }
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.AvalPV5Improved AvalPV5Improved(bool useAutomaticDate, int daysToDraw, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels, LabelAlignment labelAlignment)
        {
            return indicator.AvalPV5Improved(Input, useAutomaticDate, daysToDraw, nr2LevelType, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels, labelAlignment);
        }

        public Indicators.AvalPV5Improved AvalPV5Improved(ISeries<double> input , bool useAutomaticDate, int daysToDraw, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels, LabelAlignment labelAlignment)
        {
            return indicator.AvalPV5Improved(input, useAutomaticDate, daysToDraw, nr2LevelType, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels, labelAlignment);
        }
    }
}

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.AvalPV5Improved AvalPV5Improved(bool useAutomaticDate, int daysToDraw, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels, LabelAlignment labelAlignment)
        {
            return indicator.AvalPV5Improved(Input, useAutomaticDate, daysToDraw, nr2LevelType, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels, labelAlignment);
        }

        public Indicators.AvalPV5Improved AvalPV5Improved(ISeries<double> input , bool useAutomaticDate, int daysToDraw, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels, LabelAlignment labelAlignment)
        {
            return indicator.AvalPV5Improved(input, useAutomaticDate, daysToDraw, nr2LevelType, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels, labelAlignment);
        }
    }
}

#endregion
