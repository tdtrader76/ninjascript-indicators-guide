// Performance optimization examples

public class OptimizedAV1s : Indicator
{
    // Use object pooling for frequently allocated objects
    private readonly ObjectPool<StringBuilder> stringBuilderPool;
    private readonly ConcurrentDictionary<string, PriceLevel> levelCache;

    // Pre-calculate multipliers to avoid repeated arithmetic
    private static readonly Dictionary<string, double> LEVEL_MULTIPLIERS = new Dictionary<string, double>
    {
        ["Q1"] = 0.5,
        ["Q3"] = 0.25,
        ["Q5"] = 0.25,
        ["Std1"] = 0.0855,
        ["Std2"] = 0.171,
        ["Std3"] = 0.342,
        // ... etc
    };

    // Batch calculations to reduce redundant operations
    private void CalculateAllLevelsOptimized(double dayRange, double basePrice, Dictionary<string, PriceLevel> levels)
    {
        if (basePrice <= 0 || dayRange <= 0) return;

        // Pre-calculate common values once
        var precalculatedValues = new
        {
            HalfRange = dayRange * 0.5,
            QuarterRange = dayRange * 0.25,
            EighthRange = dayRange * 0.125,
            Std1Range = dayRange * 0.0855,
            Std2Range = dayRange * 0.171,
            Std3Range = dayRange * 0.342
        };

        var upperQuarter = RoundToQuarter(basePrice + precalculatedValues.HalfRange);
        var lowerQuarter = RoundToQuarter(basePrice - precalculatedValues.HalfRange);

        // Batch level updates to reduce property access overhead
        var levelUpdates = new Dictionary<string, double>
        {
            ["Q1"] = upperQuarter,
            ["Q8"] = lowerQuarter,
            ["Q3"] = RoundToQuarter(upperQuarter - precalculatedValues.QuarterRange),
            ["Q5"] = RoundToQuarter(lowerQuarter + precalculatedValues.QuarterRange),
            // ... continue with batch approach
        };

        // Single iteration to update all levels and plots
        foreach (var kvp in levelUpdates)
        {
            if (levels.TryGetValue(kvp.Key, out var level))
            {
                level.Value = kvp.Value;
                UpdatePlotValue(kvp.Key, kvp.Value);
            }
        }
    }

    // Optimized resource disposal with proper exception handling
    protected override void OnStateChange()
    {
        if (State == State.Terminated)
        {
            DisposeResourcesSafely();
        }
    }

    private void DisposeResourcesSafely()
    {
        var disposables = new List<IDisposable>();

        // Collect all disposable resources
        disposables.AddRange(dxBrushes.Values);
        if (dynamicTextFormat != null) disposables.Add(dynamicTextFormat);
        if (dynamicTextBrush != null) disposables.Add(dynamicTextBrush);

        // Add level disposables
        if (currentDayLevels?.Levels != null)
            disposables.AddRange(currentDayLevels.Levels.Values);

        foreach (var day in historicalLevels ?? Enumerable.Empty<DayLevels>())
            if (day?.Levels != null)
                disposables.AddRange(day.Levels.Values);

        // Dispose all resources with exception safety
        foreach (var disposable in disposables)
        {
            try
            {
                disposable?.Dispose();
            }
            catch (Exception ex)
            {
                LogError($"Error disposing resource: {ex.Message}");
            }
        }

        // Clear collections
        dxBrushes.Clear();
        historicalLevels.Clear();
    }

    // Use StringBuilder for string concatenations in logging
    private void LogLevelCalculations(Dictionary<string, PriceLevel> levels, double dayRange)
    {
        var sb = stringBuilderPool.Get();
        try
        {
            sb.AppendLine("--- CALCULATED LEVELS ---");
            sb.AppendLine($"Day Range: {dayRange:F5}");

            foreach (var level in levels.Values.Where(l => !double.IsNaN(l.Value)).OrderByDescending(l => l.Value))
            {
                sb.AppendLine($"{level.Name}: {level.Value:F5}");
            }

            sb.AppendLine("------------------------");
            Print(sb.ToString());
        }
        finally
        {
            stringBuilderPool.Return(sb);
        }
    }

    // Cache expensive calculations
    private readonly ConcurrentDictionary<(double range, double basePrice), Dictionary<string, double>> calculationCache
        = new ConcurrentDictionary<(double, double), Dictionary<string, double>>();

    private Dictionary<string, double> GetCachedLevels(double range, double basePrice)
    {
        return calculationCache.GetOrAdd((range, basePrice), key => CalculateLevelsInternal(key.range, key.basePrice));
    }
}