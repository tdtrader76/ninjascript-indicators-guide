using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.Indicators.AV1s.Domain;

namespace NinjaTrader.NinjaScript.Indicators.AV1s.Services
{
    #region Service Interfaces

    /// <summary>
    /// Service responsible for calculating price levels based on trading day data
    /// </summary>
    public interface IPriceLevelCalculator
    {
        /// <summary>
        /// Calculates all price levels for the given trading day data and parameters
        /// </summary>
        /// <param name="tradingDayData">The trading day data to calculate levels from</param>
        /// <param name="parameters">Calculation parameters</param>
        /// <returns>Calculation result containing the levels or error information</returns>
        CalculationResult CalculateLevels(TradingDayData tradingDayData, CalculationParameters parameters);

        /// <summary>
        /// Calculates a specific level based on definition and trading data
        /// </summary>
        /// <param name="definition">Level definition</param>
        /// <param name="tradingDayData">Trading day data</param>
        /// <param name="parameters">Calculation parameters</param>
        /// <returns>The calculated price level</returns>
        PriceLevel CalculateLevel(LevelDefinition definition, TradingDayData tradingDayData, CalculationParameters parameters);

        /// <summary>
        /// Validates if the provided data and parameters are suitable for calculation
        /// </summary>
        /// <param name="tradingDayData">Trading day data to validate</param>
        /// <param name="parameters">Parameters to validate</param>
        /// <returns>Validation result with error message if invalid</returns>
        (bool IsValid, string ErrorMessage) ValidateInputs(TradingDayData tradingDayData, CalculationParameters parameters);
    }

    /// <summary>
    /// Service responsible for managing trading sessions and date operations
    /// </summary>
    public interface ISessionManager
    {
        /// <summary>
        /// Determines if the given time represents a new trading day
        /// </summary>
        /// <param name="currentTime">Current time to check</param>
        /// <returns>True if it's a new trading day</returns>
        bool IsNewTradingDay(DateTime currentTime);

        /// <summary>
        /// Gets the prior trading day data for the given time
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <returns>Prior day trading data or null if not available</returns>
        TradingDayData GetPriorDayData(DateTime currentTime);

        /// <summary>
        /// Gets the current trading day data
        /// </summary>
        /// <param name="currentTime">Current time</param>
        /// <returns>Current day trading data or null if not available</returns>
        TradingDayData GetCurrentDayData(DateTime currentTime);

        /// <summary>
        /// Gets the trading day for a specific date
        /// </summary>
        /// <param name="date">Date to get trading day for</param>
        /// <returns>Trading day or null if not found</returns>
        DateTime? GetTradingDay(DateTime date);

        /// <summary>
        /// Applies gap calculation to the range if enabled
        /// </summary>
        /// <param name="priorDayData">Prior day data</param>
        /// <param name="currentDayData">Current day data</param>
        /// <param name="useGapCalculation">Whether to apply gap calculation</param>
        /// <returns>Adjusted range</returns>
        double ApplyGapCalculation(TradingDayData priorDayData, TradingDayData currentDayData, bool useGapCalculation);
    }

    /// <summary>
    /// Service responsible for rendering price levels on the chart
    /// </summary>
    public interface ILevelRenderer
    {
        /// <summary>
        /// Renders price levels for a specific trading day
        /// </summary>
        /// <param name="dayLevels">Day levels to render</param>
        /// <param name="context">Rendering context</param>
        void RenderLevels(DayLevels dayLevels, RenderContext context);

        /// <summary>
        /// Renders dynamic labels for the levels
        /// </summary>
        /// <param name="dayLevels">Day levels to render labels for</param>
        /// <param name="context">Rendering context</param>
        void RenderDynamicLabels(DayLevels dayLevels, RenderContext context);

        /// <summary>
        /// Clears rendered levels for a specific day
        /// </summary>
        /// <param name="tradingDay">Trading day to clear</param>
        void ClearLevels(DateTime tradingDay);

        /// <summary>
        /// Clears all rendered levels
        /// </summary>
        void ClearAllLevels();
    }

    /// <summary>
    /// Service for managing application logging with different levels
    /// </summary>
    public interface ILoggingService
    {
        void LogError(string message, Exception exception = null);
        void LogWarning(string message);
        void LogInfo(string message);
        void LogDebug(string message);
    }

    #endregion

    #region Service Implementations

    /// <summary>
    /// Standard implementation of price level calculator
    /// </summary>
    public sealed class StandardPriceLevelCalculator : IPriceLevelCalculator
    {
        private readonly ILoggingService logger;

        public StandardPriceLevelCalculator(ILoggingService logger = null)
        {
            this.logger = logger ?? new ConsoleLoggingService();
        }

        public CalculationResult CalculateLevels(TradingDayData tradingDayData, CalculationParameters parameters)
        {
            try
            {
                // Validate inputs
                var (isValid, errorMessage) = ValidateInputs(tradingDayData, parameters);
                if (!isValid)
                {
                    logger.LogError($"Invalid inputs for calculation: {errorMessage}");
                    return CalculationResult.Failed(errorMessage);
                }

                logger.LogDebug($"Calculating levels for {tradingDayData}");

                // Calculate base price and range
                var basePrice = DetermineBasePrice(tradingDayData, parameters);
                var range = tradingDayData.Range;

                // Calculate all levels using standard definitions
                var calculatedLevels = new Dictionary<string, PriceLevel>();

                foreach (var definition in StandardLevelDefinitions.EnabledOnly())
                {
                    try
                    {
                        var level = CalculateLevelInternal(definition, basePrice, range);
                        calculatedLevels[definition.Name] = level;
                        logger.LogDebug($"Calculated {definition.Name}: {level.Value:F4}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Failed to calculate level {definition.Name}: {ex.Message}");
                    }
                }

                // Create day levels result
                var dayLevels = new DayLevels(
                    tradingDayData.Date,
                    calculatedLevels,
                    -1, // Will be set by the indicator
                    -1, // Will be set by the indicator
                    tradingDayData
                );

                logger.LogInfo($"Successfully calculated {calculatedLevels.Count} levels for {tradingDayData.Date:yyyy-MM-dd}");
                return CalculationResult.Successful(dayLevels);
            }
            catch (Exception ex)
            {
                logger.LogError($"Unexpected error calculating levels for {tradingDayData?.Date:yyyy-MM-dd}", ex);
                return CalculationResult.Failed($"Calculation failed: {ex.Message}");
            }
        }

        public PriceLevel CalculateLevel(LevelDefinition definition, TradingDayData tradingDayData, CalculationParameters parameters)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (tradingDayData == null) throw new ArgumentNullException(nameof(tradingDayData));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            var basePrice = DetermineBasePrice(tradingDayData, parameters);
            return CalculateLevelInternal(definition, basePrice, tradingDayData.Range);
        }

        public (bool IsValid, string ErrorMessage) ValidateInputs(TradingDayData tradingDayData, CalculationParameters parameters)
        {
            if (tradingDayData == null)
                return (false, "Trading day data cannot be null");

            if (!tradingDayData.IsValid)
                return (false, $"Trading day data is invalid: {tradingDayData}");

            if (parameters == null)
                return (false, "Calculation parameters cannot be null");

            if (!parameters.IsValid)
                return (false, $"Calculation parameters are invalid: {parameters}");

            if (tradingDayData.Range <= 0)
                return (false, $"Trading day range must be positive: {tradingDayData.Range}");

            return (true, null);
        }

        private double DetermineBasePrice(TradingDayData tradingDayData, CalculationParameters parameters)
        {
            if (parameters.ManualPriceOverride > 0)
            {
                logger.LogDebug($"Using manual price override: {parameters.ManualPriceOverride:F4}");
                return parameters.ManualPriceOverride;
            }

            return parameters.LevelType switch
            {
                NR2LevelType.PreviousDayClose => tradingDayData.Close,
                NR2LevelType.CurrentDayOpen => tradingDayData.Open,
                _ => tradingDayData.Close
            };
        }

        private PriceLevel CalculateLevelInternal(LevelDefinition definition, double basePrice, double range)
        {
            var offset = range * definition.Multiplier;

            // Determine if this is an upper or lower level based on naming convention
            var isUpperLevel = definition.Name.Contains("+") ||
                              definition.Name.StartsWith("Q1") ||
                              definition.Name.StartsWith("Q2") ||
                              definition.Name.StartsWith("Q3") ||
                              definition.Name == "ZBuy" ||
                              definition.Name == "1D+";

            var value = isUpperLevel ? basePrice + offset : basePrice - offset;

            // Round to quarter for cleaner pricing
            value = RoundToQuarter(value);

            return new PriceLevel(
                definition.Name,
                value,
                definition.Type,
                definition.Color,
                definition.Description,
                definition.Multiplier
            );
        }

        private static double RoundToQuarter(double value)
        {
            return Math.Round(value * 4) / 4;
        }
    }

    /// <summary>
    /// NinjaTrader-specific implementation of session manager
    /// </summary>
    public sealed class NinjaTraderSessionManager : ISessionManager
    {
        private readonly SessionIterator sessionIterator;
        private readonly ILoggingService logger;
        private DateTime? lastTradingDay;

        public NinjaTraderSessionManager(SessionIterator sessionIterator, ILoggingService logger = null)
        {
            this.sessionIterator = sessionIterator ?? throw new ArgumentNullException(nameof(sessionIterator));
            this.logger = logger ?? new ConsoleLoggingService();
        }

        public bool IsNewTradingDay(DateTime currentTime)
        {
            try
            {
                var currentTradingDay = GetTradingDay(currentTime);
                if (!currentTradingDay.HasValue)
                {
                    logger.LogWarning($"Could not determine trading day for {currentTime}");
                    return false;
                }

                if (!lastTradingDay.HasValue || lastTradingDay.Value.Date != currentTradingDay.Value.Date)
                {
                    logger.LogDebug($"New trading day detected: {currentTradingDay.Value:yyyy-MM-dd}");
                    lastTradingDay = currentTradingDay.Value;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error checking for new trading day: {ex.Message}");
                return false;
            }
        }

        public TradingDayData GetPriorDayData(DateTime currentTime)
        {
            try
            {
                // This would need to be implemented with actual NinjaTrader bar data access
                // For now, return a placeholder that shows the pattern
                logger.LogDebug($"Getting prior day data for {currentTime}");

                // TODO: Implement actual prior day data retrieval
                // This would involve accessing BarsArray[1] (daily bars) and getting previous day's OHLC

                throw new NotImplementedException("Prior day data retrieval needs NinjaTrader context");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error getting prior day data: {ex.Message}");
                return null;
            }
        }

        public TradingDayData GetCurrentDayData(DateTime currentTime)
        {
            try
            {
                logger.LogDebug($"Getting current day data for {currentTime}");

                // TODO: Implement actual current day data retrieval
                throw new NotImplementedException("Current day data retrieval needs NinjaTrader context");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error getting current day data: {ex.Message}");
                return null;
            }
        }

        public DateTime? GetTradingDay(DateTime date)
        {
            try
            {
                var tradingDay = sessionIterator.GetTradingDay(date);
                return tradingDay == DateTime.MinValue ? null : tradingDay;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error getting trading day for {date}: {ex.Message}");
                return null;
            }
        }

        public double ApplyGapCalculation(TradingDayData priorDayData, TradingDayData currentDayData, bool useGapCalculation)
        {
            if (!useGapCalculation || priorDayData == null || currentDayData == null)
                return priorDayData?.Range ?? 0;

            try
            {
                var gap = Math.Abs(currentDayData.Open - priorDayData.Close);
                var adjustedRange = priorDayData.Range + (gap * 0.5);

                logger.LogDebug($"Gap calculation: Range {priorDayData.Range:F4} + Gap {gap:F4}/2 = {adjustedRange:F4}");
                return adjustedRange;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in gap calculation: {ex.Message}");
                return priorDayData.Range;
            }
        }
    }

    /// <summary>
    /// Console-based logging service for debugging
    /// </summary>
    public sealed class ConsoleLoggingService : ILoggingService
    {
        public void LogError(string message, Exception exception = null)
        {
            var fullMessage = exception != null ? $"{message} - {exception}" : message;
            Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss.fff} - {fullMessage}");
        }

        public void LogWarning(string message)
        {
            Console.WriteLine($"[WARN]  {DateTime.Now:HH:mm:ss.fff} - {message}");
        }

        public void LogInfo(string message)
        {
            Console.WriteLine($"[INFO]  {DateTime.Now:HH:mm:ss.fff} - {message}");
        }

        public void LogDebug(string message)
        {
#if DEBUG
            Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss.fff} - {message}");
#endif
        }
    }

    /// <summary>
    /// NinjaTrader Print-based logging service
    /// </summary>
    public sealed class NinjaTraderLoggingService : ILoggingService
    {
        private readonly Action<string> printAction;

        public NinjaTraderLoggingService(Action<string> printAction)
        {
            this.printAction = printAction ?? throw new ArgumentNullException(nameof(printAction));
        }

        public void LogError(string message, Exception exception = null)
        {
            var fullMessage = exception != null ? $"{message} - {exception.Message}" : message;
            printAction($"[AV1s ERROR]: {fullMessage}");
        }

        public void LogWarning(string message)
        {
            printAction($"[AV1s WARNING]: {message}");
        }

        public void LogInfo(string message)
        {
            printAction($"[AV1s INFO]: {message}");
        }

        public void LogDebug(string message)
        {
#if DEBUG
            printAction($"[AV1s DEBUG]: {message}");
#endif
        }
    }

    #endregion

    #region Service Factory

    /// <summary>
    /// Factory for creating service instances with proper dependencies
    /// </summary>
    public static class ServiceFactory
    {
        /// <summary>
        /// Creates a standard price level calculator
        /// </summary>
        public static IPriceLevelCalculator CreateCalculator(ILoggingService logger = null)
        {
            return new StandardPriceLevelCalculator(logger ?? new ConsoleLoggingService());
        }

        /// <summary>
        /// Creates a NinjaTrader session manager
        /// </summary>
        public static ISessionManager CreateSessionManager(SessionIterator sessionIterator, ILoggingService logger = null)
        {
            return new NinjaTraderSessionManager(sessionIterator, logger ?? new ConsoleLoggingService());
        }

        /// <summary>
        /// Creates a NinjaTrader logging service
        /// </summary>
        public static ILoggingService CreateNinjaTraderLogger(Action<string> printAction)
        {
            return new NinjaTraderLoggingService(printAction);
        }

        /// <summary>
        /// Creates a console logging service
        /// </summary>
        public static ILoggingService CreateConsoleLogger()
        {
            return new ConsoleLoggingService();
        }
    }

    #endregion
}