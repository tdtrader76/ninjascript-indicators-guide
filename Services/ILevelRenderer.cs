
using System.Collections.Generic;

namespace NinjaTrader.NinjaScript.Indicators.RyF
{
    public interface ILevelRenderer
    {
        void RenderLevels(IEnumerable<PriceLevel> levels);
    }
}
