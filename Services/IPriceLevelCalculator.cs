
using System.Collections.Generic;

namespace NinjaTrader.NinjaScript.Indicators.RyF
{
    public interface IPriceLevelCalculator
    {
        IReadOnlyCollection<PriceLevel> CalculateLevels(TradingDayData priorDay, CalculationParameters parameters);
    }
}
