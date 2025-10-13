
using System;

namespace NinjaTrader.NinjaScript.Indicators.RyF
{
    public interface ISessionManager
    {
        bool IsNewTradingDay(DateTime currentTime);
        TradingDayData GetPriorDayData(DateTime currentTime);
    }
}
