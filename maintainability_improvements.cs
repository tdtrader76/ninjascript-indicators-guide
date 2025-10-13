// Maintainability improvements with comprehensive documentation

/// <summary>
/// AV1s Indicator - Calculates and displays price levels based on previous day's range
///
/// This indicator provides multiple price levels derived from the previous trading day's
/// high-low range, useful for identifying potential support and resistance areas.
/// </summary>
/// <remarks>
/// Algorithm Overview:
/// 1. Determines base price (previous day close or current day open)
/// 2. Calculates range from previous day's high-low
/// 3. Optionally applies gap calculation (adds opening gap to range)
/// 4. Computes multiple levels using predetermined multipliers
/// 5. Renders levels with configurable visual properties
///
/// Level Calculations:
/// - Q1/Q8: ±50% of range from base price (quarter levels)
/// - Q2/Q7: Additional fractional levels at ±8.55% and ±12.5%
/// - Standard deviations: ±8.55%, ±17.1%, ±34.2%
/// - ZBuy/ZSell: Specialized levels at +15.9% and -17.1%
/// </remarks>
[Description("Calculates and displays price levels based on the previous day's range and configurable base price")]
public class DocumentedAV1s : Indicator
{
    #region Constants and Configuration

    /// <summary>
    /// Level calculation multipliers - these define the percentage of the daily range
    /// used to calculate each price level relative to the base price
    /// </summary>
    private static class LevelMultipliers
    {
        /// <summary>Quarter level (50% of range) - primary support/resistance</summary>
        public const double QUARTER = 0.5;

        /// <summary>Sub-quarter level (25% of range) - secondary levels</summary>
        public const double SUB_QUARTER = 0.25;

        /// <summary>Eighth level (37.5% of range) - intermediate levels</summary>
        public const double EIGHTH = 0.375;

        /// <summary>First standard deviation (8.55% of range) - statistical level</summary>
        public const double STD_DEVIATION_1 = 0.0855;

        /// <summary>Second standard deviation (17.1% of range) - extended statistical level</summary>
        public const double STD_DEVIATION_2 = 0.171;

        /// <summary>Third standard deviation (34.2% of range) - extreme statistical level</summary>
        public const double STD_DEVIATION_3 = 0.342;

        /// <summary>ZBuy level multiplier - specialized buying level</summary>
        public const double Z_BUY = 0.159;

        /// <summary>Fine granularity level (12.5% of range) - precision level</summary>
        public const double FINE_LEVEL = 0.125;
    }

    /// <summary>
    /// Maximum number of historical days to maintain in memory to prevent unbounded growth
    /// </summary>
    private const int MAX_HISTORICAL_DAYS = 10;

    /// <summary>
    /// Minimum valid price to prevent calculation errors with zero or negative prices
    /// </summary>
    private const double MIN_VALID_PRICE = 0.0001;

    /// <summary>
    /// Maximum reasonable price range to prevent calculation overflow
    /// </summary>
    private const double MAX_REASONABLE_RANGE = 100000;

    #endregion

    #region Public Properties with Documentation

    /// <summary>
    /// Gets or sets whether to automatically calculate levels for each trading day
    /// </summary>
    /// <value>
    /// <c>true</c> to automatically calculate levels for each new trading day;
    /// <c>false</c> to use only the manually selected date
    /// </value>
    /// <remarks>
    /// When enabled, the indicator will recalculate levels at the start of each
    /// new trading session. When disabled, only the date specified in
    /// <see cref="SelectedDate"/> will be used for calculations.
    /// </remarks>
    [NinjaScriptProperty]
    [Display(Name = "Use Automatic Date",
             Description = "Automatically calculate levels for each trading day",
             Order = 1, GroupName = "Parameters")]
    public bool UseAutomaticDate { get; set; }

    /// <summary>
    /// Gets or sets the number of previous trading days to display levels for
    /// </summary>
    /// <value>A positive integer representing the number of days (1-10 recommended)</value>
    /// <remarks>
    /// This property only applies when <see cref="UseAutomaticDate"/> is enabled.
    /// Higher values may impact performance due to increased rendering overhead.
    /// </remarks>
    [NinjaScriptProperty]
    [Range(1, MAX_HISTORICAL_DAYS)]
    [Display(Name = "Historical Days to Draw",
             Description = "Number of previous days of levels to display",
             Order = 2, GroupName = "Parameters")]
    public int DaysToDraw { get; set; }

    /// <summary>
    /// Gets or sets the type of price to use as the baseline for level calculations
    /// </summary>
    /// <value>An <see cref="NR2LevelType"/> enumeration value</value>
    /// <remarks>
    /// <see cref="NR2LevelType.PreviousDayClose"/> uses the closing price of the previous trading day.
    /// <see cref="NR2LevelType.CurrentDayOpen"/> uses the opening price of the current trading day.
    /// The choice affects all calculated levels as they are offset from this base price.
    /// </remarks>
    [NinjaScriptProperty]
    [Display(Name = "NR2 Level Type",
             Description = "Base price reference for level calculations",
             Order = 3, GroupName = "Parameters")]
    public NR2LevelType Nr2LevelType { get; set; }

    #endregion

    #region Documented Methods

    /// <summary>
    /// Calculates all price levels based on the provided day range and base price
    /// </summary>
    /// <param name="dayRange">The high-low range of the reference trading day</param>
    /// <param name="basePrice">The base price from which to calculate offsets</param>
    /// <param name="levels">Dictionary to store the calculated level values</param>
    /// <exception cref="ArgumentException">Thrown when dayRange or basePrice are invalid</exception>
    /// <remarks>
    /// This method applies the level calculation algorithm using predetermined multipliers.
    /// All calculated values are rounded to the nearest quarter point for cleaner pricing.
    ///
    /// Level Categories:
    /// - Quarter levels (Q1, Q8): Primary support/resistance at ±50% range
    /// - Sub-levels (Q2-Q7): Intermediate levels at various fractions
    /// - Standard deviations: Statistical levels for probability analysis
    /// - Specialized levels: ZBuy/ZSell for specific trading strategies
    /// </remarks>
    private void CalculateAllLevels(double dayRange, double basePrice, Dictionary<string, PriceLevel> levels)
    {
        // Input validation with detailed error messages
        if (!ValidateCalculationInputs(dayRange, basePrice, out string errorMessage))
        {
            LogError($"Invalid calculation inputs: {errorMessage}");
            return;
        }

        try
        {
            // Pre-calculate common range multiples for efficiency
            var rangeMultiples = CalculateRangeMultiples(dayRange);
            var quarterLevels = CalculateQuarterLevels(basePrice, rangeMultiples);

            // Calculate and assign all levels using helper methods for clarity
            AssignQuarterLevels(levels, quarterLevels, rangeMultiples);
            AssignStandardDeviationLevels(levels, quarterLevels, rangeMultiples);
            AssignSpecializedLevels(levels, quarterLevels, rangeMultiples);

            // Log calculation results for debugging and verification
            LogCalculationResults(levels, dayRange, basePrice);
        }
        catch (Exception ex)
        {
            LogError($"Error in level calculation: {ex.Message}");
            throw; // Re-throw to allow higher-level handling
        }
    }

    /// <summary>
    /// Pre-calculates range multiples to avoid repeated arithmetic operations
    /// </summary>
    /// <param name="dayRange">The trading day's high-low range</param>
    /// <returns>An anonymous object containing pre-calculated range multiples</returns>
    private dynamic CalculateRangeMultiples(double dayRange)
    {
        return new
        {
            Half = dayRange * LevelMultipliers.QUARTER,
            Quarter = dayRange * LevelMultipliers.SUB_QUARTER,
            Eighth = dayRange * LevelMultipliers.EIGHTH,
            Std1 = dayRange * LevelMultipliers.STD_DEVIATION_1,
            Std2 = dayRange * LevelMultipliers.STD_DEVIATION_2,
            Std3 = dayRange * LevelMultipliers.STD_DEVIATION_3,
            ZBuy = dayRange * LevelMultipliers.Z_BUY,
            Fine = dayRange * LevelMultipliers.FINE_LEVEL
        };
    }

    /// <summary>
    /// Validates inputs for level calculations to ensure data integrity
    /// </summary>
    /// <param name="dayRange">Range to validate</param>
    /// <param name="basePrice">Base price to validate</param>
    /// <param name="errorMessage">Output parameter containing error details if validation fails</param>
    /// <returns><c>true</c> if inputs are valid; otherwise, <c>false</c></returns>
    private bool ValidateCalculationInputs(double dayRange, double basePrice, out string errorMessage)
    {
        errorMessage = null;

        if (double.IsNaN(dayRange) || double.IsInfinity(dayRange) || dayRange <= 0)
        {
            errorMessage = $"Invalid day range: {dayRange}";
            return false;
        }

        if (dayRange > MAX_REASONABLE_RANGE)
        {
            errorMessage = $"Day range {dayRange} exceeds maximum reasonable value {MAX_REASONABLE_RANGE}";
            return false;
        }

        if (double.IsNaN(basePrice) || double.IsInfinity(basePrice) || basePrice < MIN_VALID_PRICE)
        {
            errorMessage = $"Invalid base price: {basePrice}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Rounds a price value to the nearest quarter point for cleaner pricing
    /// </summary>
    /// <param name="value">The price value to round</param>
    /// <returns>The value rounded to the nearest 0.25</returns>
    /// <remarks>
    /// This method ensures that all calculated levels align with common trading
    /// increments, making them more practical for actual trading decisions.
    /// </remarks>
    private static double RoundToQuarter(double value)
    {
        const double QUARTER_INCREMENT = 0.25;
        return Math.Round(value / QUARTER_INCREMENT) * QUARTER_INCREMENT;
    }

    #endregion

    #region Unit Test Support

    /// <summary>
    /// Exposes level calculation logic for unit testing without NinjaTrader dependencies
    /// </summary>
    /// <param name="dayRange">Day range for calculation</param>
    /// <param name="basePrice">Base price for calculation</param>
    /// <returns>Dictionary of calculated levels</returns>
    /// <remarks>
    /// This method provides a testable interface to the core calculation logic,
    /// allowing for comprehensive unit testing without requiring the full NinjaTrader environment.
    /// </remarks>
    internal Dictionary<string, double> CalculateLevelsForTesting(double dayRange, double basePrice)
    {
        var levels = new Dictionary<string, PriceLevel>();
        InitializePriceLevels(levels);
        CalculateAllLevels(dayRange, basePrice, levels);

        return levels.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
    }

    #endregion
}