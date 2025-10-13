
using NinjaTrader.Gui.Chart;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using System;
using System.Windows.Media;

namespace NinjaTrader.NinjaScript.Indicators.RyF
{
    public class PriceLevel : IDisposable
    {
        public string Name { get; }
        public Brush LineBrush { get; }
        public double Value { get; set; }
        public TextLayout LabelLayout { get; set; }
        public string Modifier { get; set; }

        public PriceLevel(string name, Brush brush, string modifier = "")
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            LineBrush = brush ?? throw new ArgumentNullException(nameof(brush));
            Value = double.NaN;
            Modifier = modifier;
        }

        public void Dispose()
        {
            LabelLayout?.Dispose();
            LabelLayout = null;
        }
    }
}
