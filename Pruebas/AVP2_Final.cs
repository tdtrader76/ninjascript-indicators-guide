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
    public enum AVP2LabelAlignment
    {
        Left,
        Center,
        Right
    }

    public enum AVP2RangeType
    {
        OneDay,
        ThreeDays,
        TenDays,
        Manual,
        ExpMove
    }

    public class AVP2 : Indicator
    {
        #region Constants
        // Range percentages for trading levels
        private const double TCH_PERCENTAGE = 0.125;  // 12.5% from Q1
        private const double TCL_PERCENTAGE = 0.159;  // 15.9% from Q1
        private const double TVH_PERCENTAGE = 0.159;  // 15.9% from Q4
        private const double TVL_PERCENTAGE = 0.125;  // 12.5% from Q4
        private const double Z2H_PERCENTAGE = 0.159;  // 15.9% from NR2 (upper)
        private const double Z2L_PERCENTAGE = 0.125;  // 12.5% from NR2 (upper)
        private const double Z3H_PERCENTAGE = 0.125;  // 12.5% from NR2 (lower)
        private const double Z3L_PERCENTAGE = 0.159;  // 15.9% from NR2 (lower)
        private const double Q2_Q3_PERCENTAGE = 0.25; // 25% from Q1/Q4
        private const double STD1_PERCENTAGE = 0.125; // 12.5% extensions
        private const double STD2_PERCENTAGE = 0.159; // 15.9% extensions
        private const double STD3_PERCENTAGE = 0.25;  // 25% extensions
        private const double STD4_PERCENTAGE = 0.375; // 37.5% extensions
        private const double D1_PERCENTAGE = 0.5;     // 50% projections

        // Expected Move constants
        private const double EXP_MOVE_MULTIPLIER = 0.682; // 68.2% for standard deviation
        private const int MIN_EXP_MOVE_LENGTH = 5;
        private const int MAX_EXP_MOVE_LENGTH = 200;
        #endregion

        #region Variables
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

        private SessionIterator sessionIterator;
        private DateTime currentDate = Core.Globals.MinDate;
        private double currentDayHigh;
        private double currentDayLow;
        private double currentDayOpen;
        private double priorDayHigh;
        private double priorDayLow;
        private double priorDayOpen;

        private readonly Queue<DayData> history = new Queue<DayData>();

        private class DayData
        {
            public DateTime Date { get; set; }
            public double High { get; set; }
            public double Low { get; set; }
            public double Open { get; set; }
        }

        private DayLevels currentDayLevels;
        private readonly Queue<DayLevels> historicalLevels = new Queue<DayLevels>();

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

        private readonly Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush> dxBrushes = new Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush>();
        private SharpDX.DirectWrite.TextFormat textFormat;
        private bool needsLayoutUpdate = false;

        private const float LABEL_MARGIN = 5f;
        private const float LABEL_MIN_WIDTH = 100f;
        private const float TOUCH_BUFFER = 10f;
        private const float CURRENT_DAY_EXTENSION = 30f;
        private const float LABEL_OFFSET_X = 5f;
        private const float LABEL_OFFSET_Y = 2f;

        private readonly object lockObject = new object();

        // NR2 específico por fecha
        private Dictionary<DateTime, double> dailyOpenCache = new Dictionary<DateTime, double>();

        // Expected Move data structure
        private class ExpectedMoveResult
        {
            public double ExpectedHigh { get; set; }
            public double ExpectedLow { get; set; }
            public double TodayOpen { get; set; }
            public bool IsValid => ExpectedHigh > 0 && ExpectedLow > 0 && TodayOpen > 0;
        }
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "AVP2 - Advanced Volume/Price Levels with configurable range, session and extra levels";
                Name = "AVP2";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                DaysToDraw = 5;
                RangeType = AVP2RangeType.OneDay;
                Width = 1;
                DebugMode = false;
				UseManualNR2 = false;
				ManualNR2Value = 0;
                ManualQ1 = 0;
                ManualQ4 = 0;
                UseSpecificDateNR2 = false;
                SpecificDateNR2 = DateTime.Now.Date;
                ExpMoveLength = 21;
            }
            else if (State == State.Configure)
            {
                AddDataSeries(Data.BarsPeriodType.Day, 1);
            }
            else if (State == State.DataLoaded)
            {
                sessionIterator = new SessionIterator(Bars);
                currentDayLevels = new DayLevels();
                textFormat = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, "Arial", 11);
            }
            else if (State == State.Terminated)
            {
                if (currentDayLevels != null)
                    foreach (var level in currentDayLevels.Levels.Values) level?.Dispose();

                foreach (var day in historicalLevels)
                    foreach (var level in day.Levels.Values) level?.Dispose();

                ClearAllLevels();

                foreach (var brush in dxBrushes.Values)
                    brush?.Dispose();
                dxBrushes.Clear();

                textFormat?.Dispose();
                textFormat = null;
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0) return;
            if (CurrentBar < 1) return;

            DateTime barTime = Times[0][0];
            DateTime tradingDay = sessionIterator.GetTradingDay(barTime);

            if (tradingDay != currentDate)
            {
                if (currentDate != Core.Globals.MinDate)
                {
                    FinalizeCurrentDay();
                }
                StartNewTradingDay(tradingDay);
            }
            UpdateDayHighLowOpen();
        }

        private void StartNewTradingDay(DateTime tradingDay)
        {
            currentDate = tradingDay;

            if (currentDayHigh > 0 && currentDayLow > 0)
            {
                lock (lockObject)
                {
                    history.Enqueue(new DayData
                    {
                        Date = currentDate.AddDays(-1),
                        High = currentDayHigh,
                        Low = currentDayLow,
                        Open = currentDayOpen
                    });

                    while (history.Count > 10)
                    {
                        history.Dequeue();
                    }
                }
            }

            priorDayHigh = currentDayHigh;
            priorDayLow = currentDayLow;
            priorDayOpen = currentDayOpen;

            currentDayHigh = High[0];
            currentDayLow = Low[0];
            currentDayOpen = Open[0];

            currentDayLevels = new DayLevels { StartBarIndex = CurrentBar };

            if (CanCalculateLevels())
            {
                CalculateAndCreateLevels(currentDayLevels);
            }
        }

        private bool CanCalculateLevels()
        {
            if (RangeType == AVP2RangeType.Manual)
            {
                // Can calculate if we have either Q1/Q4 OR NR2 with prior day data
                return (ManualQ1 > 0 && ManualQ4 > 0) || (ManualNR2Value > 0 && priorDayHigh > 0 && priorDayLow > 0);
            }
            if (RangeType == AVP2RangeType.ExpMove)
            {
                // Check if we have enough data for Expected Move calculation
                return HasEnoughDataForExpectedMove();
            }
            if (RangeType == AVP2RangeType.OneDay)
            {
                return priorDayHigh > 0 && priorDayLow > 0;
            }
            else if (RangeType == AVP2RangeType.ThreeDays)
            {
                return history.Count >= 3;
            }
            else // TenDays
            {
                return history.Count >= 10;
            }
        }

        private bool HasEnoughDataForExpectedMove()
        {
            // Check if we have enough historical data for Expected Move calculation
            if (BarsArray == null || BarsArray.Length <= 1 || BarsArray[1] == null)
            {
                if(DebugMode) Print("AVP2: No daily data available for Expected Move calculation");
                return false;
            }

            int requiredBars = ExpMoveLength + 1; // Need Length historical bars + current day
            bool hasEnoughData = BarsArray[1].Count >= requiredBars;

            if (!hasEnoughData && DebugMode)
            {
                Print($"AVP2: Insufficient data for Expected Move. Need {requiredBars} daily bars, have {BarsArray[1].Count}");
            }

            return hasEnoughData;
        }

        private void UpdateDayHighLowOpen()
        {
            if (currentDayHigh == 0 || High[0] > currentDayHigh) currentDayHigh = High[0];
            if (currentDayLow == 0 || Low[0] < currentDayLow) currentDayLow = Low[0];
        }

        private void FinalizeCurrentDay()
        {
            if (currentDayLevels != null)
            {
                lock (lockObject)
                {
                    currentDayLevels.EndBarIndex = CurrentBar - 1;

                    // Only save to historical levels if NOT using manual Q1/Q4
                    bool isUsingManualQ1Q4 = (RangeType == AVP2RangeType.Manual && ManualQ1 > 0 && ManualQ4 > 0);

                    if (!isUsingManualQ1Q4)
                    {
                        historicalLevels.Enqueue(currentDayLevels);

                        while (historicalLevels.Count > DaysToDraw)
                        {
                            var oldDay = historicalLevels.Dequeue();
                            oldDay.Levels.Clear();
                        }
                    }
                    else
                    {
                        // If using manual Q1/Q4, don't save to historical levels
                        // Just clear the levels to free memory
                        currentDayLevels.Levels.Clear();
                    }
                }
            }
        }

          private class RangeData
        {
            public double Q1 { get; set; }
            public double Q4 { get; set; }
            public double NR2 { get; set; }
            public double Range { get; set; }
            public bool IsValid => Q1 > 0 && Q4 > 0 && Q1 > Q4 && Range > 0;
        }

        private void CalculateAndCreateLevels(DayLevels dayLevels)
        {
            dayLevels.Levels.Clear();

            var rangeData = CalculateRangeData();
            if (!rangeData.IsValid)
            {
                if(DebugMode) Print("Invalid range data - cannot calculate levels");
                return;
            }

            CalculateAllLevels(rangeData.Range, rangeData.Q1, rangeData.Q4, rangeData.NR2, dayLevels.Levels);

            if (ShouldDrawYesterdayLevels())
            {
                AddYesterdayLevels(dayLevels.Levels);
            }

            needsLayoutUpdate = true;
        }

        private RangeData CalculateRangeData()
        {
            double q1 = 0, q4 = 0, nr2 = 0;

            if (RangeType == AVP2RangeType.Manual)
            {
                return CalculateManualRangeData(ref q1, ref q4, ref nr2);
            }
            else
            {
                return CalculateAutomaticRangeData(ref q1, ref q4, ref nr2);
            }
        }

        private RangeData CalculateManualRangeData(ref double q1, ref double q4, ref double nr2)
        {
            // Check if user provided Q1 and Q4
            if (ManualQ1 > 0 && ManualQ4 > 0)
            {
                q1 = ManualQ1;
                q4 = ManualQ4;

                double dayRange = q1 - q4;

                // NEW: Calculate NR2 based on specific date if enabled
                if (UseSpecificDateNR2)
                {
                    nr2 = GetNR2FromSpecificDate();
                    if (DebugMode)
                    {
                        Print($"AVP2: Using specific date {SpecificDateNR2:yyyy-MM-dd} for NR2: {nr2:F2}");
                    }
                }
                else
                {
                    nr2 = (dayRange / 2) + q4;
                }

                return new RangeData { Q1 = q1, Q4 = q4, NR2 = nr2, Range = dayRange };
            }
            // Check if user provided NR2 and UseManualNR2 is enabled
            else if (UseManualNR2 && ManualNR2Value > 0 && priorDayHigh > 0 && priorDayLow > 0)
            {
                return CalculateQ1Q4FromNR2(ManualNR2Value, priorDayHigh, priorDayLow);
            }

            // Fallback: not enough data
            return new RangeData();
        }

        private double GetNR2FromSpecificDate()
        {
            try
            {
                DateTime targetDate = SpecificDateNR2.Date;

                // Check cache first
                if (dailyOpenCache.TryGetValue(targetDate, out double cachedOpen))
                {
                    if (DebugMode)
                        Print($"AVP2: Found cached opening for {targetDate:yyyy-MM-dd}: {cachedOpen:F2}");
                    return cachedOpen;
                }

                // Search in daily bars (if available)
                if (BarsArray != null && BarsArray.Length > 1 && BarsArray[1] != null)
                {
                    for (int i = BarsArray[1].Count - 1; i >= 0; i--)
                    {
                        DateTime barDate = BarsArray[1].GetTime(i).Date;
                        double barOpen = BarsArray[1].GetOpen(i);

                        // Cache the opening price
                        if (!dailyOpenCache.ContainsKey(barDate))
                        {
                            dailyOpenCache[barDate] = barOpen;
                        }

                        if (barDate == targetDate)
                        {
                            if (DebugMode)
                                Print($"AVP2: Found opening for {targetDate:yyyy-MM-dd}: {barOpen:F2}");
                            return barOpen;
                        }

                        // Stop if we've gone too far past the target date
                        if (barDate < targetDate.AddDays(-30))
                        {
                            break;
                        }
                    }
                }

                // Fallback: try to use current day's open if target date is today
                if (targetDate == DateTime.Now.Date && currentDayOpen > 0)
                {
                    if (DebugMode)
                        Print($"AVP2: Using current day open for {targetDate:yyyy-MM-dd}: {currentDayOpen:F2}");
                    return currentDayOpen;
                }

                // Final fallback: use midpoint
                double fallbackNR2 = (ManualQ1 + ManualQ4) / 2;
                if (DebugMode)
                    Print($"AVP2: Could not find opening for {targetDate:yyyy-MM-dd}, using midpoint: {fallbackNR2:F2}");

                return fallbackNR2;
            }
            catch (Exception ex)
            {
                Print($"AVP2: Error getting NR2 from specific date: {ex.Message}");
                return (ManualQ1 + ManualQ4) / 2;
            }
        }

        private RangeData CalculateAutomaticRangeData(ref double q1, ref double q4, ref double nr2)
        {
            // If UseManualNR2 is true, calculate Q1 and Q4 based on manual NR2 and prior day range
            if (UseManualNR2 && ManualNR2Value > 0 && priorDayHigh > 0 && priorDayLow > 0)
            {
                return CalculateQ1Q4FromNR2(ManualNR2Value, priorDayHigh, priorDayLow);
            }
            else
            {
                // Normal calculation based on range type
                if (RangeType == AVP2RangeType.ExpMove)
                {
                    return CalculateExpectedMoveRangeData(ref q1, ref q4, ref nr2);
                }
                else if (RangeType == AVP2RangeType.OneDay)
                {
                    q1 = priorDayHigh;
                    q4 = priorDayLow;
                }
                else if (RangeType == AVP2RangeType.ThreeDays)
                {
                    if (history.Count >= 3)
                    {
                        var lastThreeDays = history.Skip(history.Count - 3).ToList();
                        q1 = lastThreeDays.Max(d => d.High);
                        q4 = lastThreeDays.Min(d => d.Low);
                    }
                }
                else // TenDays
                {
                    if (history.Count >= 10)
                    {
                        q1 = history.Max(d => d.High);
                        q4 = history.Min(d => d.Low);
                    }
                }

                double dayRange = q1 - q4;
                if (dayRange > 0)
                {
                    nr2 = (dayRange / 2) + q4;
                    return new RangeData { Q1 = q1, Q4 = q4, NR2 = nr2, Range = dayRange };
                }
            }

            return new RangeData();
        }

        private RangeData CalculateExpectedMoveRangeData(ref double q1, ref double q4, ref double nr2)
        {
            try
            {
                var expMoveResult = CalculateExpectedMove();
                if (expMoveResult.IsValid)
                {
                    q1 = expMoveResult.ExpectedHigh; // Use Expected High as Q1
                    q4 = expMoveResult.ExpectedLow;  // Use Expected Low as Q4
                    nr2 = expMoveResult.TodayOpen;   // Use Today Open as NR2 (center)

                    double dayRange = q1 - q4;

                    if (DebugMode)
                    {
                        Print($"AVP2: Expected Move calculation - Open: {expMoveResult.TodayOpen:F2}, ExpHigh: {q1:F2}, ExpLow: {q4:F2}, Range: {dayRange:F2}");
                    }

                    return new RangeData { Q1 = q1, Q4 = q4, NR2 = nr2, Range = dayRange };
                }
                else
                {
                    if (DebugMode)
                        Print("AVP2: Expected Move calculation failed - invalid result");
                }
            }
            catch (Exception ex)
            {
                if (DebugMode)
                    Print($"AVP2: Error in Expected Move calculation: {ex.Message}");
            }

            return new RangeData();
        }

        private ExpectedMoveResult CalculateExpectedMove()
        {
            if (!HasEnoughDataForExpectedMove())
                return new ExpectedMoveResult();

            int dailyCurrentBar = BarsArray[1].Count - 1;
            double todayOpen = BarsArray[1].GetOpen(dailyCurrentBar);

            // Calculate averages from historical data
            double bullSum = 0, bearSum = 0;
            int bullCount = 0, bearCount = 0;

            for (int i = 1; i <= ExpMoveLength; i++)
            {
                int barIndex = dailyCurrentBar - i;

                if (barIndex >= 0 && barIndex < BarsArray[1].Count)
                {
                    double dayOpen = BarsArray[1].GetOpen(barIndex);
                    double dayHigh = BarsArray[1].GetHigh(barIndex);
                    double dayLow = BarsArray[1].GetLow(barIndex);
                    double dayClose = BarsArray[1].GetClose(barIndex);
                    double dayRange = dayHigh - dayLow;

                    // Classify day as bullish or bearish
                    bool isBullish = dayClose > dayOpen;

                    if (isBullish)
                    {
                        bullSum += dayRange;
                        bullCount++;
                    }
                    else
                    {
                        bearSum += dayRange;
                        bearCount++;
                    }

                    if (DebugMode && i <= 3)
                    {
                        string dayType = isBullish ? "BULLISH" : "BEARISH";
                        Print($"AVP2: Day-{i} ({BarsArray[1].GetTime(barIndex):yyyy-MM-dd}): {dayType}, Range: {dayRange:F2}");
                    }
                }
            }

            double avgBull = bullCount > 0 ? bullSum / bullCount : 0;
            double avgBear = bearCount > 0 ? bearSum / bearCount : 0;

            // Apply the Expected Move multiplier (68.2% for standard deviation)
            double expectedHigh = todayOpen + (avgBull * EXP_MOVE_MULTIPLIER);
            double expectedLow = todayOpen - (avgBear * EXP_MOVE_MULTIPLIER);

            if (DebugMode)
            {
                Print($"AVP2: Expected Move Summary:");
                Print($"  Today Open: {todayOpen:F2}");
                Print($"  Bullish Days: {bullCount}, Avg Range: {avgBull:F2}, Applied: {avgBull * EXP_MOVE_MULTIPLIER:F2}");
                Print($"  Bearish Days: {bearCount}, Avg Range: {avgBear:F2}, Applied: {avgBear * EXP_MOVE_MULTIPLIER:F2}");
                Print($"  Expected High: {expectedHigh:F2}");
                Print($"  Expected Low: {expectedLow:F2}");
            }

            return new ExpectedMoveResult
            {
                TodayOpen = todayOpen,
                ExpectedHigh = expectedHigh,
                ExpectedLow = expectedLow
            };
        }

        private RangeData CalculateQ1Q4FromNR2(double nr2Value, double high, double low)
        {
            double range = high - low;
            double nr2 = nr2Value;
            double q1 = nr2 + (range / 2);
            double q4 = nr2 - (range / 2);

            return new RangeData { Q1 = q1, Q4 = q4, NR2 = nr2, Range = range };
        }

        private bool ShouldDrawYesterdayLevels()
        {
            return (RangeType == AVP2RangeType.ThreeDays ||
                   RangeType == AVP2RangeType.TenDays ||
                   RangeType == AVP2RangeType.Manual ||
                   RangeType == AVP2RangeType.ExpMove ||
                   UseManualNR2) &&
                   (priorDayHigh > 0 && priorDayLow > 0);
        }

        private void AddYesterdayLevels(Dictionary<string, PriceLevel> levels)
        {
            AddLevel(levels, "YH", priorDayHigh, Brushes.Coral);
            AddLevel(levels, "YL", priorDayLow, Brushes.Coral);
            AddLevel(levels, "YM", priorDayLow + (priorDayHigh - priorDayLow) / 2, Brushes.Gold);
        }

        private void CalculateAllLevels(double dayRange, double q1, double q4, double nr2, Dictionary<string, PriceLevel> levels)
        {
            // Main levels - Q1 and Q4
            AddLevel(levels, "Q1", q1, Brushes.Yellow);
            AddLevel(levels, "Q4", q4, Brushes.Yellow);

            // NR2 level (midpoint)
            AddLevel(levels, "NR2", nr2, Brushes.Orange);

            // TC levels (from Q1) - Trading Channel levels
            AddLevel(levels, "TCH", q1 - (dayRange * TCH_PERCENTAGE), Brushes.ForestGreen);
            AddLevel(levels, "TCL", q1 - (dayRange * TCL_PERCENTAGE), Brushes.ForestGreen);

            // TV levels (from Q4) - Trading Value levels
            AddLevel(levels, "TVH", q4 + (dayRange * TVH_PERCENTAGE), Brushes.IndianRed);
            AddLevel(levels, "TVL", q4 + (dayRange * TVL_PERCENTAGE), Brushes.IndianRed);

            // Z2 levels (from NR2) - Zone 2 levels (upper projections)
            // Skip Z2H when using ExpMove mode
            if (RangeType != AVP2RangeType.ExpMove)
            {
                AddLevel(levels, "Z2H", nr2 + (dayRange * Z2H_PERCENTAGE), Brushes.DarkOrchid);
            }
            AddLevel(levels, "Z2L", nr2 + (dayRange * Z2L_PERCENTAGE), Brushes.DarkOrchid);

            // Z3 levels (from NR2) - Zone 3 levels (lower projections)
            AddLevel(levels, "Z3H", nr2 - (dayRange * Z3H_PERCENTAGE), Brushes.Plum);
            // Skip Z3L when using ExpMove mode
            if (RangeType != AVP2RangeType.ExpMove)
            {
                AddLevel(levels, "Z3L", nr2 - (dayRange * Z3L_PERCENTAGE), Brushes.Plum);
            }

            // Upper extensions (Std+) - Standard deviation extensions above Q1
            AddLevel(levels, "Std1+", q1 + (dayRange * STD1_PERCENTAGE), Brushes.ForestGreen);
            AddLevel(levels, "Std2+", q1 + (dayRange * STD2_PERCENTAGE), Brushes.ForestGreen);
            AddLevel(levels, "Std3+", q1 + (dayRange * STD3_PERCENTAGE), Brushes.ForestGreen);
            AddLevel(levels, "Std4+", q1 + (dayRange * STD4_PERCENTAGE), Brushes.ForestGreen);

            // Lower extensions (Std-) - Standard deviation extensions below Q4
            AddLevel(levels, "Std1-", q4 - (dayRange * STD1_PERCENTAGE), Brushes.IndianRed);
            AddLevel(levels, "Std2-", q4 - (dayRange * STD2_PERCENTAGE), Brushes.IndianRed);
            AddLevel(levels, "Std3-", q4 - (dayRange * STD3_PERCENTAGE), Brushes.IndianRed);
            AddLevel(levels, "Std4-", q4 - (dayRange * STD4_PERCENTAGE), Brushes.IndianRed);

            // Q2 and Q3 levels - Quarterly levels at 25% from extremes
            AddLevel(levels, "Q2", q1 - (dayRange * Q2_Q3_PERCENTAGE), Brushes.Plum);
            AddLevel(levels, "Q3", q4 + (dayRange * Q2_Q3_PERCENTAGE), Brushes.Plum);

            // D1 levels (projections) - Daily projection levels at 50% range
            AddLevel(levels, "D1+", q1 + (dayRange * D1_PERCENTAGE), Brushes.Gold);
            AddLevel(levels, "D1-", q4 - (dayRange * D1_PERCENTAGE), Brushes.Gold);
        }

        private void AddLevel(Dictionary<string, PriceLevel> levels, string name, double value, System.Windows.Media.Brush brush, string modifier = "")
        {
            // Validate input parameters
            if (levels == null)
            {
                if(DebugMode) Print("Error: levels dictionary is null");
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                if(DebugMode) Print("Error: level name cannot be null or empty");
                return;
            }

            if (brush == null)
            {
                if(DebugMode) Print($"Error: brush cannot be null for level {name}");
                return;
            }

            // Validate value
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                if(DebugMode) Print($"Error: Invalid value {value} for level {name}");
                return;
            }

            // Create and add the level
            try
            {
                levels[name] = new PriceLevel(name, brush, modifier) { Value = value };
                if(DebugMode) Print($"{name}: {value:F2} {modifier}");
            }
            catch (Exception ex)
            {
                if(DebugMode) Print($"Error creating level {name}: {ex.Message}");
            }
        }

        private double RoundToQuarterPoint(double value)
        {
            return Math.Ceiling(value * 4) / 4;
        }

        private void UpdateAllTextLayouts()
        {
            if (currentDayLevels != null)
                UpdateTextLayouts(currentDayLevels.Levels);
            foreach (var day in historicalLevels)
                UpdateTextLayouts(day.Levels);
        }

        private void UpdateTextLayouts(Dictionary<string, PriceLevel> levels)
        {
            if (ChartControl == null) return;

            using (TextFormat textFormat = ChartControl.Properties.LabelFont.ToDirectWriteTextFormat())
            {
                foreach (var level in levels.Values)
                {
                    level.LabelLayout?.Dispose();
                    if (double.IsNaN(level.Value))
                    {
                        level.LabelLayout = null;
                        continue;
                    }
                    double roundedValue = RoundToQuarterPoint(level.Value);
                    string labelText = !string.IsNullOrEmpty(level.Modifier) ? $"{level.Name} ({level.Modifier}) {roundedValue:F2}" : $"{level.Name} {roundedValue:F2}";
                    level.LabelLayout = new TextLayout(Core.Globals.DirectWriteFactory, labelText, textFormat, ChartPanel?.W ?? 0, textFormat.FontSize);
                }
            }
        }

        private float GetSlidingLabelX(float lineStartX, float lineEndX, ChartControl chartControl, int startBarIndex, int endBarIndex)
        {
            float chartLeft = 0;
            float chartRight = (float)chartControl.CanvasRight;
            float visibleStartX = Math.Max(lineStartX, chartLeft);
            float visibleEndX = Math.Min(lineEndX, chartRight);
            float labelX = visibleEndX - LABEL_MARGIN;
            labelX = Math.Max(chartLeft + LABEL_MARGIN, Math.Min(labelX, chartRight - LABEL_MIN_WIDTH));
            if (visibleStartX <= visibleEndX && visibleStartX <= chartRight)
            {
                return labelX;
            }
            else
            {
                return -1000;
            }
        }

        private bool ShouldDrawLabel(float labelX, ChartControl chartControl, int startBarIndex)
        {
            if (labelX < 0 || labelX > chartControl.CanvasRight)
                return false;
            float firstBarOfDayX = (float)chartControl.GetXByBarIndex(ChartBars, startBarIndex);
            if (labelX <= firstBarOfDayX + TOUCH_BUFFER)
                return false;
            return true;
        }

        private void ClearAllLevels()
        {
            // Properly dispose current day levels
            if (currentDayLevels?.Levels != null)
            {
                foreach (var level in currentDayLevels.Levels.Values)
                {
                    level?.Dispose();
                }
                currentDayLevels.Levels.Clear();
            }

            // Properly dispose historical levels
            while (historicalLevels.Count > 0)
            {
                var day = historicalLevels.Dequeue();
                if (day?.Levels != null)
                {
                    foreach (var level in day.Levels.Values)
                    {
                        level?.Dispose();
                    }
                    day.Levels.Clear();
                }
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (Bars == null || ChartControl == null) return;

            if (needsLayoutUpdate)
            {
                UpdateAllTextLayouts();
                needsLayoutUpdate = false;
            }

            lock (lockObject)
            {
                foreach (var day in historicalLevels)
                {
                    DrawLevelsForRange(chartControl, chartScale, day.Levels, day.StartBarIndex, day.EndBarIndex, false);
                }

                if (currentDayLevels?.Levels?.Count > 0)
                {
                    DrawLevelsForRange(chartControl, chartScale, currentDayLevels.Levels, currentDayLevels.StartBarIndex, -1, true);
                }
            }
        }

        private void DrawLevelsForRange(ChartControl chartControl, ChartScale chartScale, Dictionary<string, PriceLevel> levels, int startBarIndex, int endBarIndex, bool isCurrentDay)
        {
            if (levels.Count == 0 || startBarIndex < 0)
                return;

            var whiteBrush = GetDxBrush(Brushes.White);
            float lineStartX = (float)chartControl.GetXByBarIndex(ChartBars, startBarIndex);
            float lineEndX;

            if (isCurrentDay)
            {
                int lastBarIndex = ChartBars.ToIndex;
                float lastBarX = (float)chartControl.GetXByBarIndex(ChartBars, lastBarIndex);
                lineEndX = lastBarX + CURRENT_DAY_EXTENSION;
            }
            else
            {
                if (endBarIndex < startBarIndex) return;
                lineEndX = (float)chartControl.GetXByBarIndex(ChartBars, endBarIndex);
            }

            foreach (var level in levels.Values)
            {
                if (double.IsNaN(level.Value) || level.Value <= 0) continue;

                float y = (float)chartScale.GetYByValue(level.Value);
                if (float.IsNaN(lineStartX) || float.IsNaN(lineEndX)) continue;

                SharpDX.Direct2D1.Brush dxBrush = GetDxBrush(level.LineBrush);
                if (dxBrush == null) continue;

                RenderTarget.DrawLine(new SharpDX.Vector2(lineStartX, y), new SharpDX.Vector2(lineEndX, y), dxBrush, Width);

                if (level.LabelLayout != null)
                {
                    float labelX = GetSlidingLabelX(lineStartX, lineEndX + LABEL_MARGIN, chartControl, startBarIndex, endBarIndex);
                    if (ShouldDrawLabel(labelX, chartControl, startBarIndex))
                    {
                        RenderTarget.DrawTextLayout(new SharpDX.Vector2(labelX + LABEL_OFFSET_X, y + LABEL_OFFSET_Y), level.LabelLayout, whiteBrush);
                    }
                }
            }
        }

        private SharpDX.Direct2D1.Brush GetDxBrush(System.Windows.Media.Brush wpfBrush)
        {
            if (dxBrushes.TryGetValue(wpfBrush, out SharpDX.Direct2D1.Brush dxBrush))
                return dxBrush;

            dxBrush = wpfBrush.ToDxBrush(RenderTarget);
            dxBrushes.Add(wpfBrush, dxBrush);
            return dxBrush;
        }

        #region Properties
        [NinjaScriptProperty]
        [Display(Name="Range Type", Order=1, GroupName="Parameters")]
        public AVP2RangeType RangeType { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Use Manual NR2", Order=2, GroupName="Parameters")]
        public bool UseManualNR2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Manual NR2 Value", Order=3, GroupName="Parameters")]
        public double ManualNR2Value { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Manual Q1", Order=4, GroupName="Parameters")]
        public double ManualQ1 { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Manual Q4", Order=5, GroupName="Parameters")]
        public double ManualQ4 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Debug Mode", Order = 10, GroupName = "Parameters")]
        public bool DebugMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Width", Order = 11, GroupName = "Parameters")]
        public int Width { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Days to Draw", Order = 12, GroupName = "Parameters")]
        public int DaysToDraw { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Specific Date NR2", Description = "Use specific date opening for NR2 calculation", Order = 13, GroupName = "NR2 Date")]
        public bool UseSpecificDateNR2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Specific Date for NR2", Description = "Date to use for NR2 opening value", Order = 14, GroupName = "NR2 Date")]
        public DateTime SpecificDateNR2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Exp Move Length", Description = "Lookback period for Expected Move calculation", Order = 15, GroupName = "Expected Move")]
        [Range(MIN_EXP_MOVE_LENGTH, MAX_EXP_MOVE_LENGTH)]
        public int ExpMoveLength { get; set; }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private AVP2[] cacheAVP2;
		public AVP2 AVP2(AVP2RangeType rangeType, bool useManualNR2, double manualNR2Value, double manualQ1, double manualQ4, bool debugMode, int width, int daysToDraw, bool useSpecificDateNR2, DateTime specificDateNR2, int expMoveLength)
		{
			return AVP2(Input, rangeType, useManualNR2, manualNR2Value, manualQ1, manualQ4, debugMode, width, daysToDraw, useSpecificDateNR2, specificDateNR2, expMoveLength);
		}

		public AVP2 AVP2(ISeries<double> input, AVP2RangeType rangeType, bool useManualNR2, double manualNR2Value, double manualQ1, double manualQ4, bool debugMode, int width, int daysToDraw, bool useSpecificDateNR2, DateTime specificDateNR2, int expMoveLength)
		{
			if (cacheAVP2 != null)
				for (int idx = 0; idx < cacheAVP2.Length; idx++)
					if (cacheAVP2[idx] != null && cacheAVP2[idx].RangeType == rangeType && cacheAVP2[idx].UseManualNR2 == useManualNR2 && cacheAVP2[idx].ManualNR2Value == manualNR2Value && cacheAVP2[idx].ManualQ1 == manualQ1 && cacheAVP2[idx].ManualQ4 == manualQ4 && cacheAVP2[idx].DebugMode == debugMode && cacheAVP2[idx].Width == width && cacheAVP2[idx].DaysToDraw == daysToDraw && cacheAVP2[idx].UseSpecificDateNR2 == useSpecificDateNR2 && cacheAVP2[idx].SpecificDateNR2 == specificDateNR2 && cacheAVP2[idx].ExpMoveLength == expMoveLength && cacheAVP2[idx].EqualsInput(input))
						return cacheAVP2[idx];
			return CacheIndicator<AVP2>(new AVP2(){ RangeType = rangeType, UseManualNR2 = useManualNR2, ManualNR2Value = manualNR2Value, ManualQ1 = manualQ1, ManualQ4 = manualQ4, DebugMode = debugMode, Width = width, DaysToDraw = daysToDraw, UseSpecificDateNR2 = useSpecificDateNR2, SpecificDateNR2 = specificDateNR2, ExpMoveLength = expMoveLength }, input, ref cacheAVP2);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.AVP2 AVP2(AVP2RangeType rangeType, bool useManualNR2, double manualNR2Value, double manualQ1, double manualQ4, bool debugMode, int width, int daysToDraw, bool useSpecificDateNR2, DateTime specificDateNR2, int expMoveLength)
		{
			return indicator.AVP2(Input, rangeType, useManualNR2, manualNR2Value, manualQ1, manualQ4, debugMode, width, daysToDraw, useSpecificDateNR2, specificDateNR2, expMoveLength);
		}

		public Indicators.AVP2 AVP2(ISeries<double> input , AVP2RangeType rangeType, bool useManualNR2, double manualNR2Value, double manualQ1, double manualQ4, bool debugMode, int width, int daysToDraw, bool useSpecificDateNR2, DateTime specificDateNR2, int expMoveLength)
		{
			return indicator.AVP2(input, rangeType, useManualNR2, manualNR2Value, manualQ1, manualQ4, debugMode, width, daysToDraw, useSpecificDateNR2, specificDateNR2, expMoveLength);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.AVP2 AVP2(AVP2RangeType rangeType, bool useManualNR2, double manualNR2Value, double manualQ1, double manualQ4, bool debugMode, int width, int daysToDraw, bool useSpecificDateNR2, DateTime specificDateNR2, int expMoveLength)
		{
			return indicator.AVP2(Input, rangeType, useManualNR2, manualNR2Value, manualQ1, manualQ4, debugMode, width, daysToDraw, useSpecificDateNR2, specificDateNR2, expMoveLength);
		}

		public Indicators.AVP2 AVP2(ISeries<double> input , AVP2RangeType rangeType, bool useManualNR2, double manualNR2Value, double manualQ1, double manualQ4, bool debugMode, int width, int daysToDraw, bool useSpecificDateNR2, DateTime specificDateNR2, int expMoveLength)
		{
			return indicator.AVP2(input, rangeType, useManualNR2, manualNR2Value, manualQ1, manualQ4, debugMode, width, daysToDraw, useSpecificDateNR2, specificDateNR2, expMoveLength);
		}
	}
}

#endregion