// Security and safety improvements

public class SecureAV1s : Indicator
{
    // Input validation with proper error messages
    [NinjaScriptProperty]
    public DateTime SelectedDate
    {
        get => selectedDate;
        set
        {
            if (value < new DateTime(1970, 1, 1))
                throw new ArgumentOutOfRangeException(nameof(value), "Selected date cannot be before 1970");
            if (value > DateTime.Today.AddYears(1))
                throw new ArgumentOutOfRangeException(nameof(value), "Selected date cannot be more than 1 year in the future");

            selectedDate = value;
        }
    }

    [NinjaScriptProperty]
    public double ManualPrice
    {
        get => manualPrice;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException("Manual price cannot be NaN or Infinity");
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Manual price cannot be negative");
            if (value > 1000000) // Reasonable upper bound
                throw new ArgumentOutOfRangeException(nameof(value), "Manual price exceeds maximum allowed value");

            manualPrice = value;
        }
    }

    // Safe array access with bounds checking
    private double GetSafeBarData(ISeries<double> series, int index, string dataType)
    {
        if (series == null)
            throw new ArgumentNullException(nameof(series), $"{dataType} series is null");

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

        return series[index];
    }

    // Safe division with zero checks
    private double SafeDivide(double numerator, double denominator, string operation)
    {
        if (Math.Abs(denominator) < double.Epsilon)
        {
            LogError($"Division by zero attempted in operation: {operation}");
            return double.NaN;
        }

        var result = numerator / denominator;
        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            LogError($"Invalid result from division in operation: {operation}");
            return double.NaN;
        }

        return result;
    }

    // Secure resource management with proper disposal patterns
    private sealed class DisposableResourceManager : IDisposable
    {
        private readonly List<IDisposable> resources = new List<IDisposable>();
        private readonly object lockObject = new object();
        private bool disposed = false;

        public T AddResource<T>(T resource) where T : IDisposable
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(DisposableResourceManager));

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
                        // Log but don't throw in dispose
                        System.Diagnostics.Debug.WriteLine($"Error disposing resource: {ex.Message}");
                    }
                }
                resources.Clear();
            }
        }
    }

    private DisposableResourceManager resourceManager = new DisposableResourceManager();

    // Validate calculation inputs before processing
    private bool ValidateCalculationInputs(double dayRange, double basePrice, out string errorMessage)
    {
        errorMessage = null;

        if (double.IsNaN(dayRange) || double.IsInfinity(dayRange))
        {
            errorMessage = "Day range is not a valid number";
            return false;
        }

        if (dayRange <= 0)
        {
            errorMessage = "Day range must be positive";
            return false;
        }

        if (dayRange > 10000) // Reasonable upper bound
        {
            errorMessage = "Day range exceeds maximum allowed value";
            return false;
        }

        if (double.IsNaN(basePrice) || double.IsInfinity(basePrice))
        {
            errorMessage = "Base price is not a valid number";
            return false;
        }

        if (basePrice <= 0)
        {
            errorMessage = "Base price must be positive";
            return false;
        }

        return true;
    }

    // Specific exception handling instead of generic catch-all
    protected override void OnBarUpdate()
    {
        if (!UseAutomaticDate) return;

        try
        {
            if (CurrentBar < 1 || sessionIterator == null) return;

            var tradingDay = GetTradingDaySafely(Time[0]);
            if (!tradingDay.HasValue) return;

            ProcessTradingDay(tradingDay.Value);
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
        catch (Exception ex)
        {
            LogError($"Unexpected error in OnBarUpdate: {ex.Message}");
            // Re-throw critical exceptions
            if (ex is OutOfMemoryException || ex is StackOverflowException)
                throw;
        }
    }

    private DateTime? GetTradingDaySafely(DateTime time)
    {
        try
        {
            var tradingDay = sessionIterator.GetTradingDay(time);
            return tradingDay == DateTime.MinValue ? null : tradingDay;
        }
        catch (Exception ex)
        {
            LogError($"Error getting trading day for {time}: {ex.Message}");
            return null;
        }
    }

    // Proper logging with different levels
    private void LogError(string message) => Print($"ERROR: {message}");
    private void LogWarning(string message) => Print($"WARNING: {message}");
    private void LogInfo(string message) => Print($"INFO: {message}");
}