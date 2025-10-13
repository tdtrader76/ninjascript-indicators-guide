using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Indicators.AV1s.Domain
{
    #region Core Domain Models

    /// <summary>
    /// Represents trading day data with immutable properties and validation
    /// </summary>
    public sealed class TradingDayData
    {
        public DateTime Date { get; }
        public double High { get; }
        public double Low { get; }
        public double Open { get; }
        public double Close { get; }

        /// <summary>
        /// Calculated range for the trading day (High - Low)
        /// </summary>
        public double Range => High - Low;

        /// <summary>
        /// Indicates if this trading day has valid data
        /// </summary>
        public bool IsValid => !double.IsNaN(High) && !double.IsNaN(Low) &&
                              !double.IsNaN(Open) && !double.IsNaN(Close) &&
                              High >= Low && Range > 0;

        public TradingDayData(DateTime date, double high, double low, double open, double close)
        {
            if (high < low)
                throw new ArgumentException($"High ({high}) cannot be less than low ({low})", nameof(high));

            if (double.IsNaN(high) || double.IsInfinity(high))
                throw new ArgumentException($"High must be a valid number, got: {high}", nameof(high));

            if (double.IsNaN(low) || double.IsInfinity(low))
                throw new ArgumentException($"Low must be a valid number, got: {low}", nameof(low));

            if (double.IsNaN(open) || double.IsInfinity(open))
                throw new ArgumentException($"Open must be a valid number, got: {open}", nameof(open));

            if (double.IsNaN(close) || double.IsInfinity(close))
                throw new ArgumentException($"Close must be a valid number, got: {close}", nameof(close));

            Date = date;
            High = high;
            Low = low;
            Open = open;
            Close = close;
        }

        public override string ToString()
        {
            return $"TradingDay[{Date:yyyy-MM-dd}] H:{High:F2} L:{Low:F2} O:{Open:F2} C:{Close:F2} R:{Range:F2}";
        }

        public override bool Equals(object obj)
        {
            return obj is TradingDayData other &&
                   Date.Date == other.Date.Date &&
                   Math.Abs(High - other.High) < 0.0001 &&
                   Math.Abs(Low - other.Low) < 0.0001 &&
                   Math.Abs(Open - other.Open) < 0.0001 &&
                   Math.Abs(Close - other.Close) < 0.0001;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Date.Date, High, Low, Open, Close);
        }
    }

    /// <summary>
    /// Represents a calculated price level with its properties and metadata
    /// </summary>
    public sealed class PriceLevel
    {
        public string Name { get; }
        public double Value { get; }
        public LevelType Type { get; }
        public Brush Color { get; }
        public string Description { get; }
        public double Multiplier { get; }

        /// <summary>
        /// Indicates if this price level has a valid value
        /// </summary>
        public bool IsValid => !double.IsNaN(Value) && !double.IsInfinity(Value) && Value > 0;

        public PriceLevel(string name, double value, LevelType type, Brush color,
                         string description = "", double multiplier = 0.0)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Color = color ?? throw new ArgumentNullException(nameof(color));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty or whitespace", nameof(name));

            Value = value;
            Type = type;
            Description = description ?? string.Empty;
            Multiplier = multiplier;
        }

        public override string ToString()
        {
            return $"Level[{Name}] {Value:F4} ({Type}) x{Multiplier:F3}";
        }

        public override bool Equals(object obj)
        {
            return obj is PriceLevel other &&
                   Name == other.Name &&
                   Math.Abs(Value - other.Value) < 0.0001 &&
                   Type == other.Type;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Value, Type);
        }
    }

    /// <summary>
    /// Configuration parameters for level calculations
    /// </summary>
    public sealed class CalculationParameters
    {
        public double BasePrice { get; }
        public bool UseGapCalculation { get; }
        public NR2LevelType LevelType { get; }
        public double ManualPriceOverride { get; }

        /// <summary>
        /// Indicates if these parameters are valid for calculations
        /// </summary>
        public bool IsValid => !double.IsNaN(BasePrice) && !double.IsInfinity(BasePrice) && BasePrice > 0;

        public CalculationParameters(double basePrice, bool useGapCalculation,
                                   NR2LevelType levelType, double manualPriceOverride = 0.0)
        {
            if (basePrice <= 0)
                throw new ArgumentException("Base price must be positive", nameof(basePrice));

            if (double.IsNaN(basePrice) || double.IsInfinity(basePrice))
                throw new ArgumentException("Base price must be a valid number", nameof(basePrice));

            if (!Enum.IsDefined(typeof(NR2LevelType), levelType))
                throw new ArgumentException("Invalid level type", nameof(levelType));

            BasePrice = basePrice;
            UseGapCalculation = useGapCalculation;
            LevelType = levelType;
            ManualPriceOverride = manualPriceOverride;
        }

        public override string ToString()
        {
            return $"Params[Base:{BasePrice:F2} Gap:{UseGapCalculation} Type:{LevelType} Manual:{ManualPriceOverride:F2}]";
        }
    }

    /// <summary>
    /// Collection of price levels for a specific trading day
    /// </summary>
    public sealed class DayLevels
    {
        public DateTime TradingDay { get; }
        public IReadOnlyDictionary<string, PriceLevel> Levels { get; }
        public int StartBarIndex { get; }
        public int EndBarIndex { get; }
        public TradingDayData SourceData { get; }

        /// <summary>
        /// Indicates if this day has valid levels
        /// </summary>
        public bool HasValidLevels => Levels.Count > 0 &&
                                     Levels.Values.Any(l => l.IsValid);

        public DayLevels(DateTime tradingDay, Dictionary<string, PriceLevel> levels,
                        int startBarIndex, int endBarIndex, TradingDayData sourceData)
        {
            TradingDay = tradingDay;
            Levels = new Dictionary<string, PriceLevel>(levels ?? new Dictionary<string, PriceLevel>());
            StartBarIndex = startBarIndex;
            EndBarIndex = endBarIndex;
            SourceData = sourceData ?? throw new ArgumentNullException(nameof(sourceData));
        }

        /// <summary>
        /// Gets a level by name, returns null if not found
        /// </summary>
        public PriceLevel GetLevel(string name)
        {
            return Levels.TryGetValue(name ?? string.Empty, out var level) ? level : null;
        }

        /// <summary>
        /// Gets all levels of a specific type
        /// </summary>
        public IEnumerable<PriceLevel> GetLevelsByType(LevelType type)
        {
            return Levels.Values.Where(l => l.Type == type);
        }

        /// <summary>
        /// Gets all valid levels (excluding NaN/invalid values)
        /// </summary>
        public IEnumerable<PriceLevel> GetValidLevels()
        {
            return Levels.Values.Where(l => l.IsValid);
        }

        public override string ToString()
        {
            return $"DayLevels[{TradingDay:yyyy-MM-dd}] {Levels.Count} levels, Bars:{StartBarIndex}-{EndBarIndex}";
        }
    }

    #endregion

    #region Enums and Supporting Types

    /// <summary>
    /// Type of price level calculation method
    /// </summary>
    public enum NR2LevelType
    {
        [Description("Previous Day Close")]
        PreviousDayClose,

        [Description("Current Day Open")]
        CurrentDayOpen
    }

    /// <summary>
    /// Category of price level
    /// </summary>
    public enum LevelType
    {
        [Description("Quarter Level")]
        Quarter,

        [Description("Sub-Quarter Level")]
        SubQuarter,

        [Description("Eighth Level")]
        Eighth,

        [Description("Standard Deviation")]
        StandardDeviation,

        [Description("Specialized Level")]
        Specialized,

        [Description("Gap Level")]
        Gap,

        [Description("Manual Level")]
        Manual
    }

    /// <summary>
    /// Result of a level calculation operation
    /// </summary>
    public sealed class CalculationResult
    {
        public bool Success { get; }
        public DayLevels DayLevels { get; }
        public string ErrorMessage { get; }
        public DateTime CalculatedAt { get; }

        private CalculationResult(bool success, DayLevels dayLevels, string errorMessage)
        {
            Success = success;
            DayLevels = dayLevels;
            ErrorMessage = errorMessage ?? string.Empty;
            CalculatedAt = DateTime.UtcNow;
        }

        public static CalculationResult Successful(DayLevels dayLevels)
        {
            return new CalculationResult(true, dayLevels ?? throw new ArgumentNullException(nameof(dayLevels)), null);
        }

        public static CalculationResult Failed(string errorMessage)
        {
            return new CalculationResult(false, null, errorMessage ?? "Unknown error");
        }

        public override string ToString()
        {
            return Success ? $"Success: {DayLevels}" : $"Failed: {ErrorMessage}";
        }
    }

    /// <summary>
    /// Context for rendering operations
    /// </summary>
    public sealed class RenderContext
    {
        public DateTime TradingDay { get; }
        public int StartBarIndex { get; }
        public int EndBarIndex { get; }
        public int CurrentBarIndex { get; }
        public double ChartScale { get; }
        public RenderParameters Parameters { get; }

        public RenderContext(DateTime tradingDay, int startBarIndex, int endBarIndex,
                           int currentBarIndex, double chartScale, RenderParameters parameters)
        {
            TradingDay = tradingDay;
            StartBarIndex = startBarIndex;
            EndBarIndex = endBarIndex;
            CurrentBarIndex = currentBarIndex;
            ChartScale = chartScale;
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }
    }

    /// <summary>
    /// Parameters for rendering price levels
    /// </summary>
    public sealed class RenderParameters
    {
        public int LineWidth { get; }
        public int LineBufferPixels { get; }
        public bool ShowDynamicLabels { get; }
        public int LabelOffsetX { get; }
        public int LabelVerticalSpacing { get; }
        public bool ShowHistoricalLevels { get; }

        public RenderParameters(int lineWidth, int lineBufferPixels, bool showDynamicLabels,
                              int labelOffsetX, int labelVerticalSpacing, bool showHistoricalLevels = true)
        {
            if (lineWidth < 1) throw new ArgumentException("Line width must be positive", nameof(lineWidth));
            if (lineBufferPixels < 0) throw new ArgumentException("Line buffer cannot be negative", nameof(lineBufferPixels));
            if (labelOffsetX < 0) throw new ArgumentException("Label offset cannot be negative", nameof(labelOffsetX));
            if (labelVerticalSpacing < 1) throw new ArgumentException("Label spacing must be positive", nameof(labelVerticalSpacing));

            LineWidth = lineWidth;
            LineBufferPixels = lineBufferPixels;
            ShowDynamicLabels = showDynamicLabels;
            LabelOffsetX = labelOffsetX;
            LabelVerticalSpacing = labelVerticalSpacing;
            ShowHistoricalLevels = showHistoricalLevels;
        }
    }

    #endregion

    #region Level Definitions

    /// <summary>
    /// Immutable definition of a price level type with its properties
    /// </summary>
    public sealed class LevelDefinition
    {
        public string Name { get; }
        public Brush Color { get; }
        public double Multiplier { get; }
        public LevelType Type { get; }
        public string Description { get; }
        public bool IsEnabled { get; }

        public LevelDefinition(string name, Brush color, double multiplier, LevelType type,
                              string description = "", bool isEnabled = true)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Color = color ?? throw new ArgumentNullException(nameof(color));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty", nameof(name));

            if (multiplier < 0)
                throw new ArgumentException("Multiplier cannot be negative", nameof(multiplier));

            Multiplier = multiplier;
            Type = type;
            Description = description ?? string.Empty;
            IsEnabled = isEnabled;
        }

        public PriceLevel CreateLevel(double basePrice, double range)
        {
            if (basePrice <= 0) throw new ArgumentException("Base price must be positive");
            if (range <= 0) throw new ArgumentException("Range must be positive");

            var offset = range * Multiplier;
            var value = Type == LevelType.Quarter || Type == LevelType.SubQuarter
                       ? basePrice + offset  // Positive levels
                       : basePrice - offset; // Negative levels (for some specialized levels)

            return new PriceLevel(Name, value, Type, Color, Description, Multiplier);
        }

        public override string ToString()
        {
            return $"LevelDef[{Name}] x{Multiplier:F3} ({Type}) {(IsEnabled ? "Enabled" : "Disabled")}";
        }
    }

    /// <summary>
    /// Standard level definitions used in AV1s calculations
    /// </summary>
    public static class StandardLevelDefinitions
    {
        private static readonly Lazy<IReadOnlyList<LevelDefinition>> _allDefinitions =
            new Lazy<IReadOnlyList<LevelDefinition>>(CreateStandardDefinitions);

        public static IReadOnlyList<LevelDefinition> All => _allDefinitions.Value;

        public static LevelDefinition Q1 => All.First(l => l.Name == "Q1");
        public static LevelDefinition Q8 => All.First(l => l.Name == "Q8");
        public static LevelDefinition Std1Plus => All.First(l => l.Name == "Std1+");
        public static LevelDefinition Std1Minus => All.First(l => l.Name == "Std1-");

        private static IReadOnlyList<LevelDefinition> CreateStandardDefinitions()
        {
            return new List<LevelDefinition>
            {
                // Quarter levels (primary support/resistance)
                new LevelDefinition("Q1", Brushes.Yellow, 0.5, LevelType.Quarter, "Upper Quarter Level"),
                new LevelDefinition("Q8", Brushes.Yellow, 0.5, LevelType.Quarter, "Lower Quarter Level"),

                // Sub-quarter levels
                new LevelDefinition("Q3", Brushes.Orange, 0.25, LevelType.SubQuarter, "Upper Sub-Quarter"),
                new LevelDefinition("Q5", Brushes.Orange, 0.25, LevelType.SubQuarter, "Lower Sub-Quarter"),

                // Eighth levels
                new LevelDefinition("Q2", Brushes.LightGreen, 0.375, LevelType.Eighth, "Upper Eighth"),
                new LevelDefinition("Q4", Brushes.LightGreen, 0.125, LevelType.Eighth, "Lower Upper Eighth"),
                new LevelDefinition("Q6", Brushes.LightGreen, 0.125, LevelType.Eighth, "Upper Lower Eighth"),
                new LevelDefinition("Q7", Brushes.LightGreen, 0.375, LevelType.Eighth, "Lower Eighth"),

                // Standard deviation levels
                new LevelDefinition("Std1+", Brushes.Cyan, 0.0855, LevelType.StandardDeviation, "First Standard Deviation Above"),
                new LevelDefinition("Std2+", Brushes.Cyan, 0.171, LevelType.StandardDeviation, "Second Standard Deviation Above"),
                new LevelDefinition("Std3+", Brushes.Cyan, 0.342, LevelType.StandardDeviation, "Third Standard Deviation Above"),
                new LevelDefinition("Std1-", Brushes.IndianRed, 0.0855, LevelType.StandardDeviation, "First Standard Deviation Below"),
                new LevelDefinition("Std2-", Brushes.IndianRed, 0.171, LevelType.StandardDeviation, "Second Standard Deviation Below"),
                new LevelDefinition("Std3-", Brushes.IndianRed, 0.342, LevelType.StandardDeviation, "Third Standard Deviation Below"),

                // Specialized levels
                new LevelDefinition("ZBuy", Brushes.LimeGreen, 0.159, LevelType.Specialized, "Specialized Buy Level"),
                new LevelDefinition("ZSell", Brushes.Red, 0.171, LevelType.Specialized, "Specialized Sell Level"),
                new LevelDefinition("1D+", Brushes.Gold, 0.50, LevelType.Specialized, "Daily Level Above"),
                new LevelDefinition("1D-", Brushes.Gold, 0.50, LevelType.Specialized, "Daily Level Below")
            }.AsReadOnly();
        }

        /// <summary>
        /// Gets level definitions filtered by type
        /// </summary>
        public static IEnumerable<LevelDefinition> ByType(LevelType type)
        {
            return All.Where(l => l.Type == type);
        }

        /// <summary>
        /// Gets enabled level definitions only
        /// </summary>
        public static IEnumerable<LevelDefinition> EnabledOnly()
        {
            return All.Where(l => l.IsEnabled);
        }
    }

    #endregion
}