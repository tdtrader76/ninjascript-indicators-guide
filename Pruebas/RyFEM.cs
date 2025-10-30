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
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
#endregion

public enum TableCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

namespace NinjaTrader.NinjaScript.Indicators
{
    #region Data Classes

    /// <summary>
    /// Encapsulates expected move calculation results
    /// </summary>
    public class ExpectedMoveResult
    {
        public double TodayOpen { get; set; }
        public double ExpectedHigh { get; set; }
        public double ExpectedLow { get; set; }
        public double BullishAverage { get; set; }
        public double BearishAverage { get; set; }
        public int BullishDaysCount { get; set; }
        public int BearishDaysCount { get; set; }
        public DateTime CalculationDate { get; set; }
        public bool IsValid => TodayOpen > 0 && ExpectedHigh > 0 && ExpectedLow > 0;
    }

    /// <summary>
    /// Backtesting success rate results
    /// </summary>
    public class BacktestResult
    {
        public int TotalDays { get; set; }
        public int HighSuccessCount { get; set; }
        public int LowSuccessCount { get; set; }
        public double HighSuccessRate => TotalDays > 0 ? (HighSuccessCount * 100.0 / TotalDays) : 0;
        public double LowSuccessRate => TotalDays > 0 ? (LowSuccessCount * 100.0 / TotalDays) : 0;
    }

    /// <summary>
    /// Table rendering configuration
    /// </summary>
    public class TableConfiguration
    {
        public bool Show { get; set; } = true;
        public int FontSize { get; set; } = 12;
        public int XOffset { get; set; } = 10;
        public int YOffset { get; set; } = 10;
        public TableCorner Corner { get; set; } = TableCorner.TopRight;
    }
    #endregion

  
    #region Utility Classes

    /// <summary>
    /// Manages historical data and calculations for expected moves
    /// </summary>
    public class ExpectedMoveDataManager
    {
        private readonly List<double> bullishMoves = new List<double>();
        private readonly List<double> bearishMoves = new List<double>();
        private readonly List<bool> bullSuccessHistory = new List<bool>();
        private readonly List<bool> bearSuccessHistory = new List<bool>();

        public void AddBullishMove(double move, bool success)
        {
            if (move > 0)
            {
                bullishMoves.Add(move);
                bullSuccessHistory.Add(success);
            }
        }

        public void AddBearishMove(double move, bool success)
        {
            if (move > 0)
            {
                bearishMoves.Add(move);
                bearSuccessHistory.Add(success);
            }
        }

        public double GetAverageBullishMove(int lookback)
        {
            return CalculateAverageMove(bullishMoves, lookback);
        }

        public double GetAverageBearishMove(int lookback)
        {
            return CalculateAverageMove(bearishMoves, lookback);
        }

        public BacktestResult CalculateBacktest(int lookback)
        {
            var result = new BacktestResult();

            int usableBullDays = Math.Min(bullSuccessHistory.Count, lookback);
            int usableBearDays = Math.Min(bearSuccessHistory.Count, lookback);

            result.HighSuccessCount = bullSuccessHistory.Skip(Math.Max(0, bullSuccessHistory.Count - usableBullDays))
                                                .Take(usableBullDays)
                                                .Count(s => s);

            result.LowSuccessCount = bearSuccessHistory.Skip(Math.Max(0, bearSuccessHistory.Count - usableBearDays))
                                               .Take(usableBearDays)
                                               .Count(s => s);

            result.TotalDays = Math.Max(usableBullDays, usableBearDays);

            return result;
        }

        public void Clear()
        {
            bullishMoves.Clear();
            bearishMoves.Clear();
            bullSuccessHistory.Clear();
            bearSuccessHistory.Clear();
        }

        private double CalculateAverageMove(List<double> moves, int lookback)
        {
            if (moves.Count == 0) return 0;

            int count = Math.Min(moves.Count, lookback);
            return moves.Skip(Math.Max(0, moves.Count - count))
                       .Take(count)
                       .Average();
        }
    }

    /// <summary>
    /// Calculates expected move levels
    /// </summary>
    public class ExpectedMoveCalculator
    {
        private const double RANGE_MULTIPLIER = 0.682; // 68.2% for standard deviation

        public ExpectedMoveResult Calculate(double todayOpen, double avgBullishMove, double avgBearishMove,
                                          int bullDaysCount, int bearDaysCount, DateTime calculationDate)
        {
            return new ExpectedMoveResult
            {
                TodayOpen = todayOpen,
                BullishAverage = avgBullishMove * RANGE_MULTIPLIER,
                BearishAverage = avgBearishMove * RANGE_MULTIPLIER,
                ExpectedHigh = todayOpen + (avgBullishMove * RANGE_MULTIPLIER),
                ExpectedLow = todayOpen - (avgBearishMove * RANGE_MULTIPLIER),
                BullishDaysCount = bullDaysCount,
                BearishDaysCount = bearDaysCount,
                CalculationDate = calculationDate
            };
        }
    }

    /// <summary>
    /// Safe resource manager for SharpDX objects
    /// </summary>
    public class SafeResourceWrapper : IDisposable
    {
        private readonly List<IDisposable> _resources = new List<IDisposable>();
        private bool _disposed = false;

        public T AddResource<T>(T resource) where T : IDisposable
        {
            if (resource != null && !_disposed)
            {
                _resources.Add(resource);
            }
            return resource;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var resource in _resources)
                {
                    try
                    {
                        resource?.Dispose();
                    }
                    catch
                    {
                        // Log error if needed, but continue disposal
                    }
                }
                _resources.Clear();
                _disposed = true;
            }
        }
    }
    #endregion

    public class RyFEM : Indicator
    {
        #region Constants
        private const int MIN_LENGTH = 1;
        private const int MAX_LENGTH = 1000;
        private const string DEFAULT_FONT_FAMILY = "Consolas";
        #endregion

        #region Variables
        // Core components
        private readonly ExpectedMoveDataManager dataManager = new ExpectedMoveDataManager();
        private readonly ExpectedMoveCalculator calculator = new ExpectedMoveCalculator();

        // Current day results
        private ExpectedMoveResult currentDayResult;
        private ExpectedMoveResult specificDateResult;
        private BacktestResult backtestResult;

        // Yesterday's data for display
        private YesterdayData yesterdayData;

        // Session tracking
        private SessionIterator sessionIterator;
        private DateTime lastProcessedDate;
        private bool isInitialized;

        // Configuration
        private TableConfiguration tableConfig;
        #endregion

        #region Data Structures
        private class YesterdayData
        {
            public double High { get; set; }
            public double Low { get; set; }
            public double Open { get; set; }
            public double Close { get; set; }
            public bool IsValid => High > 0 && Low > 0 && Open > 0 && Close > 0;
        }
        #endregion

        #region OnStateChange
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                SetDefaultValues();
            }
            else if (State == State.Configure)
            {
                AddDataSeries(Data.BarsPeriodType.Day, 1);
            }
            else if (State == State.DataLoaded)
            {
                InitializeComponents();
            }
        }

        private void SetDefaultValues()
        {
            Description = @"Expected Move - Calcula niveles esperados usando rangos de días alcistas/bajistas";
            Name = "RyFEM";
            Calculate = Calculate.OnBarClose;
            IsOverlay = true;
            DisplayInDataBox = true;
            DrawOnPricePanel = false;
            DrawHorizontalGridLines = false;
            DrawVerticalGridLines = false;
            PaintPriceMarkers = false;
            ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
            IsSuspendedWhileInactive = true;

            // Default configuration
            Length = 21;
            tableConfig = new TableConfiguration();
            DebugMode = false;

            // Initialize obsolete properties with safe defaults
            UseSpecificDate = false;
            _specificYear = DateTime.Now.Year;
            _specificMonth = DateTime.Now.Month;
            _specificDay = DateTime.Now.Day;
        }

        private void InitializeComponents()
        {
            try
            {
                sessionIterator = new SessionIterator(Bars);
                lastProcessedDate = Core.Globals.MinDate;
                isInitialized = true;

                // Validate and initialize date parameters
                ValidateDateParameters();

                // Initialize data structures
                currentDayResult = new ExpectedMoveResult();
                specificDateResult = new ExpectedMoveResult();
                backtestResult = new BacktestResult();
                yesterdayData = new YesterdayData();

                Print($"RyFEM: Indicador inicializado correctamente con Length={Length}");
            }
            catch (Exception ex)
            {
                Print($"RyFEM: Error inicializando componente: {ex.Message}");
            }
        }

        private void ValidateDateParameters()
        {
            /*
             * FIX: Issue con specificyear, specificmonth, specificday valores 0
             *
             * Problema: Los parámetros de fecha específica estaban inicializados con valor 0 por defecto,
             * lo que causaba errores al validar fechas o intentar construir objetos DateTime.
             *
             * Solución:
             * 1. Cambiar rangos para valores válidos (Year: 2000-2100, Month: 1-12, Day: 1-31)
             * 2. Agregar validación durante inicialización para corregir valores inválidos
             * 3. Usar backing fields con inicialización segura a valores actuales
             * 4. Agregar setters con validación y mensajes de error informativos
             *
             * Esto previene que los parámetros puedan tener valor 0 y asegura que siempre haya
             * una fecha válida disponible, aunque la funcionalidad esté marcada como obsoleta.
             */

            // Ensure we have valid date values - never allow 0
            if (_specificYear < 2000 || _specificYear > 2100)
            {
                int oldYear = _specificYear;
                _specificYear = DateTime.Now.Year;
                Print($"RyFEM: SpecificYear invalido ({oldYear}), usando año actual: {DateTime.Now.Year}");
            }

            if (_specificMonth < 1 || _specificMonth > 12)
            {
                int oldMonth = _specificMonth;
                _specificMonth = DateTime.Now.Month;
                Print($"RyFEM: SpecificMonth invalido ({oldMonth}), usando mes actual: {DateTime.Now.Month}");
            }

            if (_specificDay < 1 || _specificDay > DateTime.DaysInMonth(_specificYear, _specificMonth))
            {
                int oldDay = _specificDay;
                _specificDay = Math.Min(DateTime.Now.Day, DateTime.DaysInMonth(_specificYear, _specificMonth));
                Print($"RyFEM: SpecificDay invalido ({oldDay}), usando día válido: {_specificDay}");
            }

            if (DebugMode)
            {
                Print($"RyFEM: Fecha validada - {_specificDay:00}/{_specificMonth:00}/{_specificYear}");
            }
        }
        #endregion

        #region OnBarUpdate
        protected override void OnBarUpdate()
        {
            if (!ShouldProcessBar())
                return;

            if (BarsInProgress == 0)
            {
                ProcessPrimaryTimeframe();
            }
        }

        private bool ShouldProcessBar()
        {
            if (CurrentBar < 1 || !isInitialized)
            {
                return false;
            }

            if (!HasEnoughDailyData())
            {
                if (DebugMode)
                    Print($"RyFEM: Datos insuficientes. Daily bars: {BarsArray[1]?.Count ?? 0}, Required: {Length + 2}");
                return false;
            }

            return true;
        }

        private bool HasEnoughDailyData()
        {
            return BarsArray[1] != null && BarsArray[1].Count >= Length + 2;
        }

        private void ProcessPrimaryTimeframe()
        {
            try
            {
                int dailyCurrentBar = BarsArray[1].Count - 1;

                if (!IsValidDailyBar(dailyCurrentBar))
                    return;

                // Calculate current day expected move
                currentDayResult = CalculateExpectedMove(dailyCurrentBar);

                // Load yesterday's data
                LoadYesterdayData(dailyCurrentBar);

                // Calculate backtest results
                CalculateBacktestResults(dailyCurrentBar);

                if (DebugMode)
                {
                    LogCalculationResults(currentDayResult);
                }
            }
            catch (Exception ex)
            {
                Print($"RyFEM: Error en ProcessPrimaryTimeframe: {ex.Message}");
            }
        }

        private bool IsValidDailyBar(int dailyCurrentBar)
        {
            if (dailyCurrentBar < Length)
            {
                if (DebugMode)
                    Print($"RyFEM: Datos insuficientes - dailyCurrentBar={dailyCurrentBar} < Length={Length}");
                return false;
            }
            return true;
        }

        private ExpectedMoveResult CalculateExpectedMove(int dailyCurrentBar)
        {
            double todayOpen = BarsArray[1].GetOpen(dailyCurrentBar);
            DateTime todayDate = BarsArray[1].GetTime(dailyCurrentBar).Date;

            // Calculate averages from historical data
            double bullSum = 0, bearSum = 0;
            int bullCount = 0, bearCount = 0;

            for (int i = 1; i <= Length; i++)
            {
                int barIndex = dailyCurrentBar - i;

                if (barIndex >= 0 && barIndex < BarsArray[1].Count)
                {
                    var dayData = ExtractDayData(barIndex);

                    if (dayData.IsBullish)
                    {
                        bullSum += dayData.Range;
                        bullCount++;
                    }
                    else
                    {
                        bearSum += dayData.Range;
                        bearCount++;
                    }

                    if (DebugMode && i <= 3)
                    {
                        LogDayAnalysis(i, dayData);
                    }
                }
            }

            double avgBull = bullCount > 0 ? bullSum / bullCount : 0;
            double avgBear = bearCount > 0 ? bearSum / bearCount : 0;

            return calculator.Calculate(todayOpen, avgBull, avgBear, bullCount, bearCount, todayDate);
        }

        private DayData ExtractDayData(int barIndex)
        {
            return new DayData
            {
                Open = BarsArray[1].GetOpen(barIndex),
                High = BarsArray[1].GetHigh(barIndex),
                Low = BarsArray[1].GetLow(barIndex),
                Close = BarsArray[1].GetClose(barIndex),
                Date = BarsArray[1].GetTime(barIndex).Date,
                IsBullish = BarsArray[1].GetClose(barIndex) > BarsArray[1].GetOpen(barIndex),
                Range = BarsArray[1].GetHigh(barIndex) - BarsArray[1].GetLow(barIndex)
            };
        }

        private void LoadYesterdayData(int dailyCurrentBar)
        {
            yesterdayData = new YesterdayData();

            if (dailyCurrentBar >= 1)
            {
                int yesterdayIndex = dailyCurrentBar - 1;
                yesterdayData.High = BarsArray[1].GetHigh(yesterdayIndex);
                yesterdayData.Low = BarsArray[1].GetLow(yesterdayIndex);
                yesterdayData.Open = BarsArray[1].GetOpen(yesterdayIndex);
                yesterdayData.Close = BarsArray[1].GetClose(yesterdayIndex);
            }
        }

        private void CalculateBacktestResults(int dailyCurrentBar)
        {
            backtestResult = new BacktestResult();

            for (int i = 1; i <= Length; i++)
            {
                int barIndex = dailyCurrentBar - i;

                if (barIndex >= 1 && barIndex < BarsArray[1].Count)
                {
                    ProcessBacktestDay(barIndex);
                }
            }
        }

        private void ProcessBacktestDay(int barIndex)
        {
            double pastOpen = BarsArray[1].GetOpen(barIndex);

            // Calculate historical averages up to this day
            var historicalAverages = CalculateHistoricalAverages(barIndex);

            double expHigh = pastOpen + historicalAverages.BullAvg;
            double expLow = pastOpen - historicalAverages.BearAvg;

            // Check actual performance
            double actualHigh = BarsArray[1].GetHigh(barIndex);
            double actualLow = BarsArray[1].GetLow(barIndex);

            if (actualHigh >= expHigh)
                backtestResult.HighSuccessCount++;

            if (actualLow <= expLow)
                backtestResult.LowSuccessCount++;

            backtestResult.TotalDays++;
        }

        private (double BullAvg, double BearAvg) CalculateHistoricalAverages(int currentBarIndex)
        {
            double bullSum = 0, bearSum = 0;
            int bullCount = 0, bearCount = 0;

            for (int j = 1; j <= Length && (currentBarIndex - j) >= 0; j++)
            {
                int lookbackIndex = currentBarIndex - j;
                var dayData = ExtractDayData(lookbackIndex);

                if (dayData.IsBullish)
                {
                    bullSum += dayData.Range;
                    bullCount++;
                }
                else
                {
                    bearSum += dayData.Range;
                    bearCount++;
                }
            }

            return (
                BullAvg: bullCount > 0 ? bullSum / bullCount : 0,
                BearAvg: bearCount > 0 ? bearSum / bearCount : 0
            );
        }

        private void LogCalculationResults(ExpectedMoveResult result)
        {
            Print($"=== RYFEM RESULTS ===");
            Print($"Date: {result.CalculationDate:yyyy-MM-dd}");
            Print($"Open: {result.TodayOpen:F2}");
            Print($"Expected High: {RoundToNearestQuarter(result.ExpectedHigh):F2}");
            Print($"Expected Low: {RoundToNearestQuarter(result.ExpectedLow):F2}");
            Print($"Bullish Days: {result.BullishDaysCount}, Avg: {result.BullishAverage:F2}");
            Print($"Bearish Days: {result.BearishDaysCount}, Avg: {result.BearishAverage:F2}");
            Print($"Success Rate: Bull={backtestResult.HighSuccessRate:F1}%, Bear={backtestResult.LowSuccessRate:F1}%");
            Print($"==================");
        }

        private void LogDayAnalysis(int dayIndex, DayData dayData)
        {
            string dayType = dayData.IsBullish ? "ALCISTA" : "BAJISTA";
            Print($"  Day-{dayIndex} ({dayData.Date:yyyy-MM-dd}): {dayType}");
            Print($"    O={dayData.Open:F2}, H={dayData.High:F2}, L={dayData.Low:F2}, C={dayData.Close:F2}");
            Print($"    Range: {dayData.Range:F2}");
        }

        private class DayData
        {
            public double Open { get; set; }
            public double High { get; set; }
            public double Low { get; set; }
            public double Close { get; set; }
            public DateTime Date { get; set; }
            public bool IsBullish { get; set; }
            public double Range { get; set; }
        }

                #endregion

        #region OnRender - Statistics Table
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (!ShouldRenderTable())
                return;

            using (var resourceManager = new SafeResourceWrapper())
            {
                try
                {
                    RenderStatisticsTable(chartControl, resourceManager);
                }
                catch (Exception ex)
                {
                    Print($"RyFEM: Error rendering table: {ex.Message}");
                }
            }
        }

        private bool ShouldRenderTable()
        {
            if (!tableConfig.Show)
            {
                return false;
            }

            if (!currentDayResult.IsValid)
            {
                if (DebugMode)
                    Print($"RyFEM: No se puede renderizar - currentDayResult no es válido");
                return false;
            }

            return RenderTarget != null;
        }

        private void RenderStatisticsTable(ChartControl chartControl, SafeResourceWrapper resourceManager)
        {
            string tableText = BuildTableText();

            var textFormat = resourceManager.AddResource(
                new TextFormat(Core.Globals.DirectWriteFactory, DEFAULT_FONT_FAMILY, tableConfig.FontSize));

            var textLayout = resourceManager.AddResource(
                new TextLayout(Core.Globals.DirectWriteFactory, tableText, textFormat, float.MaxValue, float.MaxValue));

            var textBrush = resourceManager.AddResource(Brushes.White.ToDxBrush(RenderTarget));
            var bgBrush = resourceManager.AddResource(CreateTransparentBrush());
            var borderBrush = resourceManager.AddResource(Brushes.DarkGray.ToDxBrush(RenderTarget));

            DrawTableElements(textLayout, textBrush, bgBrush, borderBrush);
        }

        private string BuildTableText()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("═══ EXPECTED MOVE ═══");
            sb.AppendLine();

            // Current day information
            sb.AppendLine("HOY:");
            sb.AppendLine($"Open:     {currentDayResult.TodayOpen:F2}");
            sb.AppendLine($"Exp High: {RoundToNearestQuarter(currentDayResult.ExpectedHigh):F2}");
            sb.AppendLine($"Exp Low:  {RoundToNearestQuarter(currentDayResult.ExpectedLow):F2}");
            sb.AppendLine();

            // Average ranges
            sb.AppendLine("RANGO PROMEDIO:");
            sb.AppendLine($"Alcista: +{currentDayResult.BullishAverage:F2}");
            sb.AppendLine($"  ({currentDayResult.BullishDaysCount} días)");
            sb.AppendLine($"Bajista: -{currentDayResult.BearishAverage:F2}");
            sb.AppendLine($"  ({currentDayResult.BearishDaysCount} días)");
            sb.AppendLine();

            // Yesterday's data
            if (yesterdayData.IsValid)
            {
                sb.AppendLine("AYER:");
                sb.AppendLine($"High:  {yesterdayData.High:F2}");
                sb.AppendLine($"Low:   {yesterdayData.Low:F2}");
                sb.AppendLine($"Open:  {yesterdayData.Open:F2}");
                sb.AppendLine($"Close: {yesterdayData.Close:F2}");
                sb.AppendLine();
            }

            // Success rates
            sb.AppendLine($"ÉXITO ({Length}d):");
            sb.AppendLine($"Bull: {backtestResult.HighSuccessRate:F1}%");
            sb.AppendLine($"Bear: {backtestResult.LowSuccessRate:F1}%");

            return sb.ToString();
        }

        private SharpDX.Direct2D1.Brush CreateTransparentBrush()
        {
            return new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0, 0, 0, 0)
            ).ToDxBrush(RenderTarget);
        }

        private void DrawTableElements(TextLayout textLayout, SharpDX.Direct2D1.Brush textBrush,
                                      SharpDX.Direct2D1.Brush bgBrush, SharpDX.Direct2D1.Brush borderBrush)
        {
            float width = textLayout.Metrics.Width;
            float height = textLayout.Metrics.Height;

            float x = CalculateTableXPosition(width);
            float y = CalculateTableYPosition(height);

            float padding = 8;
            RectangleF bgRect = new RectangleF(x - padding, y - padding, width + (padding * 2), height + (padding * 2));

            // Draw elements
            RenderTarget.FillRectangle(bgRect, bgBrush);
            RenderTarget.DrawRectangle(bgRect, borderBrush, 2);

            RectangleF textRect = new RectangleF(x, y, width, height);
            RenderTarget.DrawTextLayout(new Vector2(x, y), textLayout, textBrush);
        }
        #endregion

        #region Table Positioning Helper Methods
        private float CalculateTableXPosition(float tableWidth)
        {
            switch (TableCorner)
            {
                case TableCorner.TopLeft:
                case TableCorner.BottomLeft:
                    return TableX;

                case TableCorner.TopRight:
                case TableCorner.BottomRight:
                    return ChartPanel.W - tableWidth - TableX - 16;

                default:
                    return TableX;
            }
        }

        private float CalculateTableYPosition(float tableHeight)
        {
            switch (TableCorner)
            {
                case TableCorner.TopLeft:
                case TableCorner.TopRight:
                    return TableY;

                case TableCorner.BottomLeft:
                case TableCorner.BottomRight:
                    return ChartPanel.H - tableHeight - TableY - 16;

                default:
                    return TableY;
            }
        }
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Length", Description = "Período de lookback para promedios", Order = 1, GroupName = "Parameters")]
        [Range(MIN_LENGTH, MAX_LENGTH)]
        public int Length
        {
            get => _length;
            set
            {
                if (value >= MIN_LENGTH && value <= MAX_LENGTH)
                    _length = value;
                else
                    Print($"RyFEM: Length debe estar entre {MIN_LENGTH} y {MAX_LENGTH}");
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Table", Description = "Display data table", Order = 2, GroupName = "Parameters")]
        public bool ShowTable
        {
            get => tableConfig.Show;
            set => tableConfig.Show = value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Table Font Size", Description = "Font size for table display", Order = 3, GroupName = "Parameters")]
        [Range(8, 24)]
        public int TableFontSize
        {
            get => tableConfig.FontSize;
            set
            {
                if (value >= 8 && value <= 24)
                    tableConfig.FontSize = value;
                else
                    Print("RyFEM: TableFontSize debe estar entre 8 y 24");
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Table Corner", Description = "Select corner for table positioning", Order = 4, GroupName = "Parameters")]
        public TableCorner TableCorner
        {
            get => tableConfig.Corner;
            set => tableConfig.Corner = value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Table X Position", Description = "X offset from selected corner", Order = 5, GroupName = "Parameters")]
        [Range(0, 500)]
        public int TableX
        {
            get => tableConfig.XOffset;
            set => tableConfig.XOffset = value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Table Y Position", Description = "Y offset from selected corner", Order = 6, GroupName = "Parameters")]
        [Range(0, 500)]
        public int TableY
        {
            get => tableConfig.YOffset;
            set => tableConfig.YOffset = value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Debug Mode", Description = "Enable detailed debug output for troubleshooting", Order = 7, GroupName = "Parameters")]
        public bool DebugMode { get; set; }

        // Legacy properties for backward compatibility
        [NinjaScriptProperty]
        [Display(Name = "Use Specific Date", Description = "Enable calculation for a specific date (OBSOLETE)", Order = 8, GroupName = "Legacy")]
        [Obsolete("This feature has been deprecated")]
        public bool UseSpecificDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Year", Description = "Year for specific date (OBSOLETE)", Order = 9, GroupName = "Legacy")]
        [Range(2000, 2100)]
        [Obsolete("This feature has been deprecated")]
        public int SpecificYear
        {
            get => _specificYear;
            set
            {
                if (value >= 2000 && value <= 2100)
                    _specificYear = value;
                else
                    Print($"RyFEM: SpecificYear must be between 2000 and 2100. Current year used instead.");
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Month", Description = "Month for specific date (OBSOLETE)", Order = 10, GroupName = "Legacy")]
        [Range(1, 12)]
        [Obsolete("This feature has been deprecated")]
        public int SpecificMonth
        {
            get => _specificMonth;
            set
            {
                if (value >= 1 && value <= 12)
                    _specificMonth = value;
                else
                    Print($"RyFEM: SpecificMonth must be between 1 and 12. Current month used instead.");
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Day", Description = "Day for specific date (OBSOLETE)", Order = 11, GroupName = "Legacy")]
        [Range(1, 31)]
        [Obsolete("This feature has been deprecated")]
        public int SpecificDay
        {
            get => _specificDay;
            set
            {
                if (value >= 1 && value <= 31)
                    _specificDay = value;
                else
                    Print($"RyFEM: SpecificDay must be between 1 and 31. Current day used instead.");
            }
        }

        // Backing fields
        private int _length = 30;
        private int _specificYear = DateTime.Now.Year;
        private int _specificMonth = DateTime.Now.Month;
        private int _specificDay = DateTime.Now.Day;
        #endregion

        #region Helper Methods
        /// <summary>
        /// Rounds price levels to the nearest multiple of 0.25 (following pattern: .00, .25, .50, .75)
        /// Always rounds UP (ceiling) to the next valid multiple of 0.25
        /// </summary>
        /// <param name="price">The price to round</param>
        /// <returns>Rounded price to nearest 0.25 multiple</returns>
        private double RoundToNearestQuarter(double price)
        {
            if (price <= 0)
                return price;

            // Multiply by 4 to work with quarters, get ceiling, then divide by 4
            double quarters = Math.Ceiling(price * 4);
            return quarters / 4;
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private RyFEM[] cacheRyFEM;
		public RyFEM RyFEM(int length, bool showTable, int tableFontSize, TableCorner tableCorner, int tableX, int tableY, bool debugMode, bool useSpecificDate, int specificYear, int specificMonth, int specificDay)
		{
			return RyFEM(Input, length, showTable, tableFontSize, tableCorner, tableX, tableY, debugMode, useSpecificDate, specificYear, specificMonth, specificDay);
		}

		public RyFEM RyFEM(ISeries<double> input, int length, bool showTable, int tableFontSize, TableCorner tableCorner, int tableX, int tableY, bool debugMode, bool useSpecificDate, int specificYear, int specificMonth, int specificDay)
		{
			if (cacheRyFEM != null)
				for (int idx = 0; idx < cacheRyFEM.Length; idx++)
					if (cacheRyFEM[idx] != null && cacheRyFEM[idx].Length == length && cacheRyFEM[idx].ShowTable == showTable && cacheRyFEM[idx].TableFontSize == tableFontSize && cacheRyFEM[idx].TableCorner == tableCorner && cacheRyFEM[idx].TableX == tableX && cacheRyFEM[idx].TableY == tableY && cacheRyFEM[idx].DebugMode == debugMode && cacheRyFEM[idx].UseSpecificDate == useSpecificDate && cacheRyFEM[idx].SpecificYear == specificYear && cacheRyFEM[idx].SpecificMonth == specificMonth && cacheRyFEM[idx].SpecificDay == specificDay && cacheRyFEM[idx].EqualsInput(input))
						return cacheRyFEM[idx];
			return CacheIndicator<RyFEM>(new RyFEM(){ Length = length, ShowTable = showTable, TableFontSize = tableFontSize, TableCorner = tableCorner, TableX = tableX, TableY = tableY, DebugMode = debugMode, UseSpecificDate = useSpecificDate, SpecificYear = specificYear, SpecificMonth = specificMonth, SpecificDay = specificDay }, input, ref cacheRyFEM);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RyFEM RyFEM(int length, bool showTable, int tableFontSize, TableCorner tableCorner, int tableX, int tableY, bool debugMode, bool useSpecificDate, int specificYear, int specificMonth, int specificDay)
		{
			return indicator.RyFEM(Input, length, showTable, tableFontSize, tableCorner, tableX, tableY, debugMode, useSpecificDate, specificYear, specificMonth, specificDay);
		}

		public Indicators.RyFEM RyFEM(ISeries<double> input , int length, bool showTable, int tableFontSize, TableCorner tableCorner, int tableX, int tableY, bool debugMode, bool useSpecificDate, int specificYear, int specificMonth, int specificDay)
		{
			return indicator.RyFEM(input, length, showTable, tableFontSize, tableCorner, tableX, tableY, debugMode, useSpecificDate, specificYear, specificMonth, specificDay);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RyFEM RyFEM(int length, bool showTable, int tableFontSize, TableCorner tableCorner, int tableX, int tableY, bool debugMode, bool useSpecificDate, int specificYear, int specificMonth, int specificDay)
		{
			return indicator.RyFEM(Input, length, showTable, tableFontSize, tableCorner, tableX, tableY, debugMode, useSpecificDate, specificYear, specificMonth, specificDay);
		}

		public Indicators.RyFEM RyFEM(ISeries<double> input , int length, bool showTable, int tableFontSize, TableCorner tableCorner, int tableX, int tableY, bool debugMode, bool useSpecificDate, int specificYear, int specificMonth, int specificDay)
		{
			return indicator.RyFEM(input, length, showTable, tableFontSize, tableCorner, tableX, tableY, debugMode, useSpecificDate, specificYear, specificMonth, specificDay);
		}
	}
}

#endregion
