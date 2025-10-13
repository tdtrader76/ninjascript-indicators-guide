
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NinjaTrader.NinjaScript.Indicators.RyF
{
    /// <summary>
    /// Calculates price levels based on prior day data and a set of calculation parameters.
    /// This class includes performance optimizations such as caching and efficient calculations.
    /// </summary>
    public class PriceLevelCalculator : IPriceLevelCalculator
    {
        /// <summary>
        /// Defines the percentage of the daily range used to calculate each price level.
        /// </summary>
        private static class LevelMultipliers
        {
            public const double Half = 0.5;
            public const double Quarter = 0.25;
            public const double Frac0375 = 0.375;
            public const double Frac0125 = 0.125;
            public const double Std1 = 0.0855;
            public const double Std2 = 0.171;
            public const double Std3 = 0.342;
            public const double ZBuy = 0.159;
        }

        private readonly ConcurrentDictionary<(double range, double basePrice), IReadOnlyCollection<PriceLevel>> calculationCache
            = new ConcurrentDictionary<(double, double), IReadOnlyCollection<PriceLevel>>();

        public Action<string> Logger { get; set; }

        /// <summary>
        /// Calculates a collection of price levels for a given trading day.
        /// </summary>
        /// <param name="priorDay">The data from the prior trading day.</param>
        /// <param name="parameters">The parameters for the calculation.</param>
        /// <returns>A read-only collection of calculated price levels.</returns>
        public IReadOnlyCollection<PriceLevel> CalculateLevels(TradingDayData priorDay, CalculationParameters parameters)
        {
            if (priorDay == null || parameters == null || priorDay.Range <= 0)
                return new List<PriceLevel>();

            double basePrice = parameters.ManualPrice > 0 ? parameters.ManualPrice : priorDay.Close;
            double range = priorDay.Range;

            return calculationCache.GetOrAdd((range, basePrice), key =>
            {
                var levels = new Dictionary<string, PriceLevel>();
                InitializePriceLevels(levels);
                CalculateAllLevelsOptimized(key.range, key.basePrice, levels);
                LogCalculations(levels, key.range, key.basePrice);
                return levels.Values.ToList().AsReadOnly();
            });
        }

        private void InitializePriceLevels(Dictionary<string, PriceLevel> levels)
        {
            // Definitions remain the same
            var levelDefinitions = new[]
            {
                new { Name = "Q1", Brush = System.Windows.Media.Brushes.Yellow, Modifier = "0.5" },
                new { Name = "Q8", Brush = System.Windows.Media.Brushes.Yellow, Modifier = "0.5" },
                new { Name = "Q3", Brush = System.Windows.Media.Brushes.Plum, Modifier = "0.25" },
                new { Name = "Q5", Brush = System.Windows.Media.Brushes.Plum, Modifier = "0.25" },
                new { Name = "Q4", Brush = System.Windows.Media.Brushes.ForestGreen, Modifier = "0.375" },
                new { Name = "Q6", Brush = System.Windows.Media.Brushes.IndianRed, Modifier = "0.375" },
                new { Name = "Q2", Brush = System.Windows.Media.Brushes.ForestGreen, Modifier = "0.0855" },
                new { Name = "ZSell", Brush = System.Windows.Media.Brushes.BlueViolet, Modifier = "0.171" },
                new { Name = "NR2", Brush = System.Windows.Media.Brushes.Gold, Modifier = "Base" },
                new { Name = "ZBuy", Brush = System.Windows.Media.Brushes.BlueViolet, Modifier = "0.159" },
                new { Name = "Q7", Brush = System.Windows.Media.Brushes.IndianRed, Modifier = "0.125" },
                new { Name = "Std1+", Brush = System.Windows.Media.Brushes.ForestGreen, Modifier = "0.0855" },
                new { Name = "Std2+", Brush = System.Windows.Media.Brushes.ForestGreen, Modifier = "0.171" },
                new { Name = "Std3+", Brush = System.Windows.Media.Brushes.ForestGreen, Modifier = "0.342" },
                new { Name = "1D+", Brush = System.Windows.Media.Brushes.Gold, Modifier = "0.50" },
                new { Name = "Std1-", Brush = System.Windows.Media.Brushes.IndianRed, Modifier = "0.0855" },
                new { Name = "Std2-", Brush = System.Windows.Media.Brushes.IndianRed, Modifier = "0.171" },
                new { Name = "Std3-", Brush = System.Windows.Media.Brushes.IndianRed, Modifier = "0.342" },
                new { Name = "1D-", Brush = System.Windows.Media.Brushes.Gold, Modifier = "0.50" }
            };
            levels.Clear();
            foreach (var def in levelDefinitions) { levels[def.Name] = new PriceLevel(def.Name, def.Brush, def.Modifier); }
        }

        private void CalculateAllLevelsOptimized(double dayRange, double basePrice, Dictionary<string, PriceLevel> levels)
        {
            double upperQuarter = RoundToQuarter(basePrice + (dayRange * LevelMultipliers.Half));
            double lowerQuarter = RoundToQuarter(basePrice - (dayRange * LevelMultipliers.Half));

            levels["Q1"].Value = upperQuarter;
            levels["Q8"].Value = lowerQuarter;
            levels["Q3"].Value = RoundToQuarter(upperQuarter - (dayRange * LevelMultipliers.Quarter));
            levels["Q5"].Value = RoundToQuarter(lowerQuarter + (dayRange * LevelMultipliers.Quarter));
            levels["Q4"].Value = RoundToQuarter(upperQuarter - (dayRange * LevelMultipliers.Frac0375));
            levels["Q6"].Value = RoundToQuarter(lowerQuarter + (dayRange * LevelMultipliers.Frac0375));
            levels["NR2"].Value = RoundToQuarter(basePrice);
            levels["Q2"].Value = RoundToQuarter(upperQuarter - (dayRange * LevelMultipliers.Std1));
            levels["ZSell"].Value = RoundToQuarter(upperQuarter - (dayRange * LevelMultipliers.Std2));
            levels["Std1+"].Value = RoundToQuarter(upperQuarter + (dayRange * LevelMultipliers.Std1));
            levels["Std2+"].Value = RoundToQuarter(upperQuarter + (dayRange * LevelMultipliers.Std2));
            levels["Std3+"].Value = RoundToQuarter(upperQuarter + (dayRange * LevelMultipliers.Std3));
            levels["1D+"].Value = RoundToQuarter(upperQuarter + (dayRange * LevelMultipliers.Half));
            levels["ZBuy"].Value = RoundToQuarter(lowerQuarter + (dayRange * LevelMultipliers.ZBuy));
            levels["Q7"].Value = RoundToQuarter(lowerQuarter + (dayRange * LevelMultipliers.Frac0125));
            levels["Std1-"].Value = RoundToQuarter(lowerQuarter - (dayRange * LevelMultipliers.Std1));
            levels["Std2-"].Value = RoundToQuarter(lowerQuarter - (dayRange * LevelMultipliers.Std2));
            levels["Std3-"].Value = RoundToQuarter(lowerQuarter - (dayRange * LevelMultipliers.Std3));
            levels["1D-"].Value = RoundToQuarter(lowerQuarter - (dayRange * LevelMultipliers.Half));
        }

        private void LogCalculations(Dictionary<string, PriceLevel> levels, double dayRange, double basePrice)
        {
            if (Logger == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("--- CALCULATED LEVELS (Optimized) ---");
            sb.AppendLine($"Day Range: {dayRange:F5}, Base Price: {basePrice:F5}");
            foreach (var level in levels.Values.Where(l => !double.IsNaN(l.Value)).OrderByDescending(l => l.Value))
            {
                sb.AppendLine($"{level.Name}: {level.Value:F5}");
            }
            sb.AppendLine("-------------------------------------");
            Logger(sb.ToString());
        }

        private double RoundToQuarter(double value)
        {
            return Math.Round(value * 4) / 4;
        }
    }
}
