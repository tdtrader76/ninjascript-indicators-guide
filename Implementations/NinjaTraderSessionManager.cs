using NinjaTrader.Data;
using System;

namespace NinjaTrader.NinjaScript.Indicators.RyF
{
    public class NinjaTraderSessionManager : ISessionManager
    {
        private readonly Bars bars;
        private readonly Bars dailyBars;
        private readonly SessionIterator sessionIterator;
        private DateTime currentTradingDay = DateTime.MinValue;
        public Action<string> Logger { get; set; }

        public NinjaTraderSessionManager(Bars bars, Bars dailyBars)
        {
            this.bars = bars ?? throw new ArgumentNullException(nameof(bars));
            this.dailyBars = dailyBars ?? throw new ArgumentNullException(nameof(dailyBars));
            if (this.bars.Count > 0)
            {
                this.sessionIterator = new SessionIterator(this.bars);
            }
        }

        public bool IsNewTradingDay(DateTime currentTime)
        {
            if (sessionIterator == null) return false;

            DateTime tradingDay = sessionIterator.GetTradingDay(currentTime);
            if (tradingDay != currentTradingDay)
            {
                currentTradingDay = tradingDay;
                return true;
            }
            return false;
        }

        public TradingDayData GetPriorDayData(DateTime currentTime)
        {
            if (dailyBars == null || dailyBars.Count < 2)
            {
                Logger?.Invoke("Warning: Daily series not ready or not enough data.");
                return null;
            }

            int currentDailyIndex = dailyBars.GetBar(currentTime);
            if (currentDailyIndex < 1)
            {
                Logger?.Invoke("Warning: Not enough history from the current point to get prior day data.");
                return null;
            }

            int priorDayIndex = currentDailyIndex - 1;

            // Safe data access
            DateTime date = dailyBars.GetTime(priorDayIndex);
            double high = GetSafeBarData(dailyBars.High, priorDayIndex, "High");
            double low = GetSafeBarData(dailyBars.Low, priorDayIndex, "Low");
            double open = GetSafeBarData(dailyBars.Open, priorDayIndex, "Open");
            double close = GetSafeBarData(dailyBars.Close, priorDayIndex, "Close");

            if (double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(open) || double.IsNaN(close))
            {
                return null; // One of the data points was invalid
            }

            return new TradingDayData(date, high, low, open, close);
        }

        private double GetSafeBarData(ISeries<double> series, int index, string dataType)
        {
            if (index < 0 || index >= series.Count)
            {
                Logger?.Invoke($"Warning: Index {index} out of bounds for {dataType} series (Count: {series.Count})");
                return double.NaN;
            }

            if (!series.IsValidDataPoint(index))
            {
                Logger?.Invoke($"Warning: Invalid data point at index {index} for {dataType} series");
                return double.NaN;
            }

            return series[index];
        }
    }
}