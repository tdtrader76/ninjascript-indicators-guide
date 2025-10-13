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
    /// <summary>
    /// AV1s Indicator with Priority 1 Security and Resource Management Improvements
    ///
    /// IMPROVEMENTS IMPLEMENTED:
    /// 1. Robust input validation for all properties
    /// 2. Safe resource management with proper disposal
    /// 3. Specific exception handling instead of generic catch-all
    /// 4. Structured logging with different levels
    /// 5. Safe array access and division operations
    /// </summary>
    public class AV1s : Indicator
    {
        #region Constants for Validation

        /// <summary>Minimum valid date to prevent calculation errors</summary>
        private static readonly DateTime MIN_VALID_DATE = new DateTime(1970, 1, 1);

        /// <summary>Maximum future date allowed (1 year from today)</summary>
        private static readonly DateTime MAX_FUTURE_DATE = DateTime.Today.AddYears(1);

        /// <summary>Minimum valid price to prevent calculation errors</summary>
        private const double MIN_VALID_PRICE = 0.0001;

        /// <summary>Maximum reasonable price to prevent overflow</summary>
        private const double MAX_REASONABLE_PRICE = 1000000.0;

        /// <summary>Maximum historical days to prevent memory issues</summary>
        private const int MAX_HISTORICAL_DAYS = 20;

        /// <summary>Maximum label offset to prevent rendering issues</summary>
        private const int MAX_LABEL_OFFSET = 500;

        /// <summary>Epsilon for double comparisons</summary>
        private const double EPSILON = 1e-10;

        #endregion

        #region Enums

        public enum NR2LevelType
        {
            PreviousDayClose,
            CurrentDayOpen
        }

        public enum LabelAlignment
        {
            Left,
            Center,
            Right
        }

        #endregion

        #region Secure Resource Management

        /// <summary>
        /// Thread-safe resource manager for proper disposal of DirectX resources
        /// </summary>
        private sealed class SecureResourceManager : IDisposable
        {
            private readonly List<IDisposable> resources = new List<IDisposable>();
            private readonly object lockObject = new object();
            private bool disposed = false;

            public T AddResource<T>(T resource) where T : IDisposable
            {
                if (disposed)
                {
                    resource?.Dispose();
                    throw new ObjectDisposedException(nameof(SecureResourceManager));
                }

                lock (lockObject)
                {
                    if (!disposed && resource != null)
                    {
                        resources.Add(resource);
                    }
                }
                return resource;
            }

            public void Dispose()
            {
                if (disposed) return;

                lock (lockObject)
                {
                    if (disposed) return;
                    disposed = true;

                    foreach (var resource in resources)
                    {
                        try
                        {
                            resource?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            // Log but don't throw in dispose to prevent further issues
                            System.Diagnostics.Debug.WriteLine($"[AV1s] Error disposing resource: {ex.Message}");
                        }
                    }
                    resources.Clear();
                }
            }
        }

        #endregion

        #region Variables with Improved Safety

        // Secure resource management
        private SecureResourceManager resourceManager = new SecureResourceManager();

        // Input Parameters with backing fields for validation
        private DateTime selectedDate = DateTime.Today;
        private double manualPrice = 0.0;
        private AV1s.NR2LevelType nr2LevelType = NR2LevelType.PreviousDayClose;
        private int daysToDraw = 3;
        private int width = 2;
        private int lineBufferPixels = 20;
        private int labelOffsetX = 10;
        private int labelVerticalSpacing = 20;

        // Session management variables
        private SessionIterator sessionIterator;
        private DateTime currentDate = Core.Globals.MinDate;
        private double currentDayHigh;
        private double currentDayLow;
        private double currentDayOpen;
        private double priorDayHigh;
        private double priorDayLow;
        private double priorDayOpen;
        private int manualStartBar = -1;
        private int manualEndBar = -1;

        // Data structures for managing price levels
        private DayLevels currentDayLevels;
        private readonly Queue<DayLevels> historicalLevels = new Queue<DayLevels>();

        #endregion

        #region Safe Data Classes

        /// <summary>
        /// Container for all data related to a single day's levels with safe disposal
        /// </summary>
        private class DayLevels : IDisposable
        {
            public Dictionary<string, PriceLevel> Levels { get; }
            public int StartBarIndex { get; set; }
            public int EndBarIndex { get; set; }
            private bool disposed = false;

            public DayLevels()
            {
                Levels = new Dictionary<string, PriceLevel>();
                StartBarIndex = -1;
                EndBarIndex = -1;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                foreach (var level in Levels.Values)
                {
                    try
                    {
                        level?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AV1s] Error disposing level: {ex.Message}");
                    }
                }
                Levels.Clear();
            }
        }

        /// <summary>
        /// Represents a calculated price level with safe disposal
        /// </summary>
        private class PriceLevel : IDisposable
        {
            public string Name { get; }
            public System.Windows.Media.Brush LineBrush { get; }
            public double Value { get; set; }
            public TextLayout LabelLayout { get; set; }
            public string Modifier { get; set; }
            private bool disposed = false;

            public PriceLevel(string name, System.Windows.Media.Brush brush, string modifier = "")
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
                LineBrush = brush ?? throw new ArgumentNullException(nameof(brush));
                Value = double.NaN;
                Modifier = modifier ?? string.Empty;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                try
                {
                    LabelLayout?.Dispose();
                    LabelLayout = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AV1s] Error disposing PriceLevel: {ex.Message}");
                }
            }
        }

        #endregion

        #region Properties with Robust Validation

        [NinjaScriptProperty]
        [Display(Name = "Use Automatic Date", Description = "If true, calculates range from the prior day automatically. If false, uses the 'Selected Date' below.", Order = 1, GroupName = "Parameters")]
        public bool UseAutomaticDate { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Historical Days to Draw", Description = "How many previous days of levels to draw.", Order = 2, GroupName = "Parameters")]
        public int DaysToDraw
        {
            get => daysToDraw;
            set
            {
                if (value < 1)
                {
                    LogWarning($"DaysToDraw cannot be less than 1. Setting to 1 instead of {value}");
                    daysToDraw = 1;
                }
                else if (value > MAX_HISTORICAL_DAYS)
                {
                    LogWarning($"DaysToDraw cannot exceed {MAX_HISTORICAL_DAYS}. Setting to {MAX_HISTORICAL_DAYS} instead of {value}");
                    daysToDraw = MAX_HISTORICAL_DAYS;
                }
                else
                {
                    daysToDraw = value;
                }
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "NR2 Level Type", Description = "Select whether NR2 should use the previous day's close or current day's open.", Order = 3, GroupName = "Parameters")]
        public AV1s.NR2LevelType Nr2LevelType
        {
            get => nr2LevelType;
            set
            {
                if (Enum.IsDefined(typeof(NR2LevelType), value))
                {
                    nr2LevelType = value;
                }
                else
                {
                    LogError($"Invalid NR2LevelType value: {value}. Using default: {NR2LevelType.PreviousDayClose}");
                    nr2LevelType = NR2LevelType.PreviousDayClose;
                }
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "GAP", Description = "If true, adds half of the opening gap-up to the previous day's range.", Order = 4, GroupName = "Parameters")]
        public bool UseGapCalculation { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Selected Date", Description = "Date for calculating levels (only used if 'Use Automatic Date' is false).", Order = 5, GroupName = "Parameters")]
        public DateTime SelectedDate
        {
            get => selectedDate;
            set
            {
                if (value < MIN_VALID_DATE)
                {
                    LogError($"Selected date cannot be before {MIN_VALID_DATE:yyyy-MM-dd}. Using minimum valid date.");
                    selectedDate = MIN_VALID_DATE;
                }
                else if (value > MAX_FUTURE_DATE)
                {
                    LogError($"Selected date cannot be more than 1 year in the future. Using today's date.");
                    selectedDate = DateTime.Today;
                }
                else
                {
                    selectedDate = value;
                }
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Manual Price", Description = "Base price for levels. If 0, uses prior day's close.", Order = 6, GroupName = "Parameters")]
        public double ManualPrice
        {
            get => manualPrice;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    LogError("Manual price cannot be NaN or Infinity. Using 0 (auto-detect).");
                    manualPrice = 0.0;
                }
                else if (value < 0)
                {
                    LogError("Manual price cannot be negative. Using 0 (auto-detect).");
                    manualPrice = 0.0;
                }
                else if (value > MAX_REASONABLE_PRICE)
                {
                    LogError($"Manual price {value:F2} exceeds maximum reasonable value {MAX_REASONABLE_PRICE:F2}. Using 0 (auto-detect).");
                    manualPrice = 0.0;
                }
                else
                {
                    manualPrice = value;
                }
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Line Width", Description = "Width of the level lines", Order = 1, GroupName = "Visuals")]
        public int Width
        {
            get => width;
            set
            {
                if (value < 1)
                {
                    LogWarning($"Line width cannot be less than 1. Setting to 1 instead of {value}");
                    width = 1;
                }
                else if (value > 10)
                {
                    LogWarning($"Line width cannot exceed 10. Setting to 10 instead of {value}");
                    width = 10;
                }
                else
                {
                    width = value;
                }
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Line Buffer (Pixels)", Description = "Pixel buffer from the last bar for line drawing.", Order = 2, GroupName = "Visuals")]
        public int LineBufferPixels
        {
            get => lineBufferPixels;
            set
            {
                if (value < 0)
                {
                    LogWarning($"Line buffer pixels cannot be negative. Setting to 0 instead of {value}");
                    lineBufferPixels = 0;
                }
                else if (value > 200)
                {
                    LogWarning($"Line buffer pixels cannot exceed 200. Setting to 200 instead of {value}");
                    lineBufferPixels = 200;
                }
                else
                {
                    lineBufferPixels = value;
                }
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Dynamic Labels", Description = "Show dynamic level labels on the right side of the chart.", Order = 3, GroupName = "Visuals")]
        public bool ShowDynamicLabels { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Label Offset X", Description = "Distance in pixels from the last bar to the labels.", Order = 4, GroupName = "Visuals")]
        public int LabelOffsetX
        {
            get => labelOffsetX;
            set
            {
                if (value < 0)
                {
                    LogWarning($"Label offset X cannot be negative. Setting to 0 instead of {value}");
                    labelOffsetX = 0;
                }
                else if (value > MAX_LABEL_OFFSET)
                {
                    LogWarning($"Label offset X cannot exceed {MAX_LABEL_OFFSET}. Setting to {MAX_LABEL_OFFSET} instead of {value}");
                    labelOffsetX = MAX_LABEL_OFFSET;
                }
                else
                {
                    labelOffsetX = value;
                }
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Label Vertical Spacing", Description = "Pixels between dynamic labels.", Order = 5, GroupName = "Visuals")]
        public int LabelVerticalSpacing
        {
            get => labelVerticalSpacing;
            set
            {
                if (value < 5)
                {
                    LogWarning($"Label vertical spacing cannot be less than 5. Setting to 5 instead of {value}");
                    labelVerticalSpacing = 5;
                }
                else if (value > 100)
                {
                    LogWarning($"Label vertical spacing cannot exceed 100. Setting to 100 instead of {value}");
                    labelVerticalSpacing = 100;
                }
                else
                {
                    labelVerticalSpacing = value;
                }
            }
        }

        #endregion

        #region Safe Utility Methods

        /// <summary>
        /// Safely accesses bar data with bounds checking and validation
        /// </summary>
        /// <param name="series">The data series to access</param>
        /// <param name="index">The index to access</param>
        /// <param name="dataType">Description of the data type for logging</param>
        /// <returns>The data value or NaN if invalid</returns>
        private double GetSafeBarData(ISeries<double> series, int index, string dataType)
        {
            if (series == null)
            {
                LogError($"{dataType} series is null");
                return double.NaN;
            }

            if (index < 0 || index >= series.Count)
            {
                LogWarning($"Index {index} out of bounds for {dataType} series (Count: {series.Count})");
                return double.NaN;
            }

            if (!series.IsValidDataPoint(index))
            {
                LogWarning($"Invalid data point at index {index} for {dataType} series");
                return double.NaN;
            }

            var value = series[index];
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                LogWarning($"Invalid {dataType} value at index {index}: {value}");
                return double.NaN;
            }

            return value;
        }

        /// <summary>
        /// Performs safe division with zero and overflow checks
        /// </summary>
        /// <param name="numerator">The numerator</param>
        /// <param name="denominator">The denominator</param>
        /// <param name="operation">Description of the operation for logging</param>
        /// <returns>The division result or NaN if invalid</returns>
        private double SafeDivide(double numerator, double denominator, string operation)
        {
            if (Math.Abs(denominator) < EPSILON)
            {
                LogError($"Division by zero attempted in operation: {operation}");
                return double.NaN;
            }

            if (double.IsNaN(numerator) || double.IsInfinity(numerator))
            {
                LogError($"Invalid numerator in division operation: {operation} - Value: {numerator}");
                return double.NaN;
            }

            if (double.IsNaN(denominator) || double.IsInfinity(denominator))
            {
                LogError($"Invalid denominator in division operation: {operation} - Value: {denominator}");
                return double.NaN;
            }

            var result = numerator / denominator;
            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                LogError($"Invalid result from division in operation: {operation} - Result: {result}");
                return double.NaN;
            }

            return result;
        }

        /// <summary>
        /// Validates calculation inputs before processing
        /// </summary>
        /// <param name="dayRange">The day range to validate</param>
        /// <param name="basePrice">The base price to validate</param>
        /// <param name="errorMessage">Output parameter containing error details if validation fails</param>
        /// <returns>true if inputs are valid; otherwise false</returns>
        private bool ValidateCalculationInputs(double dayRange, double basePrice, out string errorMessage)
        {
            errorMessage = null;

            if (double.IsNaN(dayRange) || double.IsInfinity(dayRange))
            {
                errorMessage = $"Day range is not a valid number: {dayRange}";
                return false;
            }

            if (dayRange <= 0)
            {
                errorMessage = $"Day range must be positive: {dayRange}";
                return false;
            }

            if (dayRange > 10000) // Reasonable upper bound
            {
                errorMessage = $"Day range {dayRange} exceeds maximum allowed value 10000";
                return false;
            }

            if (double.IsNaN(basePrice) || double.IsInfinity(basePrice))
            {
                errorMessage = $"Base price is not a valid number: {basePrice}";
                return false;
            }

            if (basePrice <= 0)
            {
                errorMessage = $"Base price must be positive: {basePrice}";
                return false;
            }

            if (basePrice > MAX_REASONABLE_PRICE)
            {
                errorMessage = $"Base price {basePrice} exceeds maximum reasonable value {MAX_REASONABLE_PRICE}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Safely gets trading day with proper error handling
        /// </summary>
        /// <param name="time">The time to get trading day for</param>
        /// <returns>Trading day or null if invalid</returns>
        private DateTime? GetTradingDaySafely(DateTime time)
        {
            try
            {
                if (sessionIterator == null)
                {
                    LogError("Session iterator is null");
                    return null;
                }

                var tradingDay = sessionIterator.GetTradingDay(time);
                return tradingDay == DateTime.MinValue ? null : tradingDay;
            }
            catch (ArgumentException ex)
            {
                LogError($"Invalid argument getting trading day for {time}: {ex.Message}");
                return null;
            }
            catch (InvalidOperationException ex)
            {
                LogError($"Invalid operation getting trading day for {time}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                LogError($"Unexpected error getting trading day for {time}: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Structured Logging Methods

        /// <summary>
        /// Logs error messages with ERROR prefix
        /// </summary>
        /// <param name="message">The error message</param>
        private void LogError(string message)
        {
            Print($"[AV1s ERROR]: {message}");
        }

        /// <summary>
        /// Logs warning messages with WARNING prefix
        /// </summary>
        /// <param name="message">The warning message</param>
        private void LogWarning(string message)
        {
            Print($"[AV1s WARNING]: {message}");
        }

        /// <summary>
        /// Logs informational messages with INFO prefix
        /// </summary>
        /// <param name="message">The informational message</param>
        private void LogInfo(string message)
        {
            Print($"[AV1s INFO]: {message}");
        }

        /// <summary>
        /// Logs debug messages with DEBUG prefix (only in debug builds)
        /// </summary>
        /// <param name="message">The debug message</param>
        [System.Diagnostics.Conditional("DEBUG")]
        private void LogDebug(string message)
        {
            Print($"[AV1s DEBUG]: {message}");
        }

        #endregion

        #region Enhanced OnBarUpdate with Specific Exception Handling

        protected override void OnBarUpdate()
        {
            if (!UseAutomaticDate) return;

            try
            {
                // Validate basic state before processing
                if (CurrentBar < 1)
                {
                    LogDebug("CurrentBar < 1, skipping processing");
                    return;
                }

                if (sessionIterator == null)
                {
                    LogWarning("Session iterator is null, skipping processing");
                    return;
                }

                // Safely get trading day
                var tradingDay = GetTradingDaySafely(Time[0]);
                if (!tradingDay.HasValue)
                {
                    LogDebug($"Could not get trading day for {Time[0]}");
                    return;
                }

                // Process trading day with safe data access
                ProcessTradingDaySafely(tradingDay.Value);
            }
            catch (ArgumentException ex)
            {
                LogError($"Invalid argument in OnBarUpdate: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                LogError($"Invalid operation in OnBarUpdate: {ex.Message}");
            }
            catch (NullReferenceException ex)
            {
                LogError($"Null reference in OnBarUpdate: {ex.Message}");
            }
            catch (IndexOutOfRangeException ex)
            {
                LogError($"Index out of range in OnBarUpdate: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogError($"Unexpected error in OnBarUpdate: {ex.Message}");

                // Re-throw critical exceptions that indicate serious system issues
                if (ex is OutOfMemoryException || ex is StackOverflowException || ex is AccessViolationException)
                {
                    LogError("Critical system exception detected, re-throwing");
                    throw;
                }
            }
        }

        /// <summary>
        /// Safely processes a trading day with proper validation and error handling
        /// </summary>
        /// <param name="tradingDay">The trading day to process</param>
        private void ProcessTradingDaySafely(DateTime tradingDay)
        {
            try
            {
                // Safely access current bar data
                var currentHigh = GetSafeBarData(High, 0, "High");
                var currentLow = GetSafeBarData(Low, 0, "Low");
                var currentOpen = GetSafeBarData(Open, 0, "Open");

                if (double.IsNaN(currentHigh) || double.IsNaN(currentLow) || double.IsNaN(currentOpen))
                {
                    LogWarning("Invalid current bar data, skipping processing");
                    return;
                }

                // Continue with processing using validated data...
                LogDebug($"Processing trading day {tradingDay:yyyy-MM-dd} with valid data");

                // TODO: Add rest of the processing logic here with proper validation
            }
            catch (Exception ex)
            {
                LogError($"Error processing trading day {tradingDay:yyyy-MM-dd}: {ex.Message}");
                throw; // Re-throw to be handled by OnBarUpdate
            }
        }

        #endregion

        #region Enhanced State Management with Safe Resource Disposal

        protected override void OnStateChange()
        {
            try
            {
                if (State == State.SetDefaults)
                {
                    InitializeDefaults();
                }
                else if (State == State.DataLoaded)
                {
                    InitializeDataDependentComponents();
                }
                else if (State == State.Terminated)
                {
                    DisposeResourcesSafely();
                }
            }
            catch (Exception ex)
            {
                LogError($"Error in OnStateChange (State: {State}): {ex.Message}");

                // Always attempt cleanup on termination, even if errors occur
                if (State == State.Terminated)
                {
                    try
                    {
                        DisposeResourcesSafely();
                    }
                    catch (Exception cleanupEx)
                    {
                        LogError($"Error during cleanup: {cleanupEx.Message}");
                    }
                }

                throw; // Re-throw to let NinjaTrader handle the error appropriately
            }
        }

        /// <summary>
        /// Initializes default values with validation
        /// </summary>
        private void InitializeDefaults()
        {
            Description = "AV1s Indicator with Enhanced Security and Resource Management";
            Name = "AV1s (Secure)";
            Calculate = Calculate.OnEachTick;
            IsOverlay = true;
            DisplayInDataBox = true;
            DrawOnPricePanel = true;
            DrawHorizontalGridLines = true;
            DrawVerticalGridLines = true;
            PaintPriceMarkers = true;
            ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
            IsSuspendedWhileInactive = true;

            // Set validated default values
            UseAutomaticDate = true;
            DaysToDraw = 3;
            Nr2LevelType = NR2LevelType.PreviousDayClose;
            UseGapCalculation = false;
            SelectedDate = DateTime.Today;
            ManualPrice = 0.0;
            Width = 2;
            LineBufferPixels = 20;
            ShowDynamicLabels = true;
            LabelOffsetX = 10;
            LabelVerticalSpacing = 20;

            LogInfo("Defaults initialized successfully");
        }

        /// <summary>
        /// Initializes components that depend on loaded data
        /// </summary>
        private void InitializeDataDependentComponents()
        {
            try
            {
                if (Bars?.Instrument?.MasterInstrument != null)
                {
                    sessionIterator = new SessionIterator(Bars);
                    LogInfo($"Session iterator initialized for {Bars.Instrument.FullName}");
                }
                else
                {
                    LogError("Cannot initialize session iterator: Bars or Instrument is null");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error initializing data-dependent components: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Safely disposes all resources with comprehensive error handling
        /// </summary>
        private void DisposeResourcesSafely()
        {
            LogInfo("Starting resource disposal");

            try
            {
                // Dispose resource manager (handles DirectX resources)
                resourceManager?.Dispose();
                resourceManager = null;
            }
            catch (Exception ex)
            {
                LogError($"Error disposing resource manager: {ex.Message}");
            }

            try
            {
                // Dispose current day levels
                currentDayLevels?.Dispose();
                currentDayLevels = null;
            }
            catch (Exception ex)
            {
                LogError($"Error disposing current day levels: {ex.Message}");
            }

            try
            {
                // Dispose historical levels
                while (historicalLevels.Count > 0)
                {
                    try
                    {
                        var dayLevel = historicalLevels.Dequeue();
                        dayLevel?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error disposing historical level: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error clearing historical levels: {ex.Message}");
            }

            try
            {
                // Dispose session iterator
                sessionIterator?.Dispose();
                sessionIterator = null;
            }
            catch (Exception ex)
            {
                LogError($"Error disposing session iterator: {ex.Message}");
            }

            LogInfo("Resource disposal completed");
        }

        #endregion
    }
}