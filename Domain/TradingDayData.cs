
using System;

namespace NinjaTrader.NinjaScript.Indicators.RyF
{
    // Domain models
    public class TradingDayData
    {
        public DateTime Date { get; }
        public double High { get; }
        public double Low { get; }
        public double Open { get; }
        public double Close { get; }
        public double Range => High - Low;

        public TradingDayData(DateTime date, double high, double low, double open, double close)
        {
            if (high < low) throw new ArgumentException("High cannot be less than low");
            Date = date;
            High = high;
            Low = low;
            Open = open;
            Close = close;
        }
    }
}
