#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class AvalP : Indicator
    {
        #region Variables
        // Input Parameters
        private DateTime selectedDate;
        private double manualPrice;

        // Session management variables for automatic daily updates
        private SessionIterator sessionIterator;
        private DateTime currentDate = Core.Globals.MinDate;
        private double currentDayHigh;
        private double currentDayLow;
        private double currentDayOpen;
        private double priorDayHigh;
        private double priorDayLow;
        private double priorDayOpen;


        // A dictionary to hold all price levels for easier management
        private readonly Dictionary<string, PriceLevel> priceLevels = new Dictionary<string, PriceLevel>();

        // Represents a calculated price level
        private class PriceLevel : IDisposable
        {
            public string Name { get; }
            public System.Windows.Media.Brush LineBrush { get; }
            public double Value { get; set; }
            public TextLayout LabelLayout { get; set; }

            public PriceLevel(string name, System.Windows.Media.Brush brush)
            {
                Name = name;
                LineBrush = brush;
                Value = double.NaN;
            }

            public void Dispose()
            {
                LabelLayout?.Dispose();
                LabelLayout = null;
            }
        }

        // Caching and performance optimization
        private readonly Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush> dxBrushes = new Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush>();
        private bool needsLayoutUpdate = false;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Calculates and displays price levels based on the previous day's range and a manual price.";
                Name = "AvalP";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // Default Parameters
                UseAutomaticDate = true;
                SelectedDate = DateTime.Today;
                ManualPrice = 23000.0;
                Width = 2; // Default line width
                LineBufferPixels = 125; // Default buffer for the line drawing
            }
            else if (State == State.Configure)
            {
                // Initialize the price levels
                InitializePriceLevels();

                // Reset session variables
                currentDate = Core.Globals.MinDate;
                currentDayHigh = 0;
                currentDayLow = 0;
                currentDayOpen = 0;
                priorDayHigh = 0;
                priorDayLow = 0;
                priorDayOpen = 0;
                sessionIterator = null;
            }
            else if (State == State.DataLoaded)
            {
                // Data loaded, now we can calculate levels or initialize session iterator
                ClearOutputWindow();
                if (UseAutomaticDate)
                {
                    try
                    {
                        sessionIterator = new SessionIterator(Bars);
                    }
                    catch (Exception ex)
                    {
                        Print($"Error initializing SessionIterator: {ex.Message}");
                    }
                }
            }
            else if (State == State.Terminated)
            {
                // Dispose of DX resources
                foreach (var brush in dxBrushes.Values)
                    brush.Dispose();
                dxBrushes.Clear();

                foreach (var level in priceLevels.Values)
                    level.Dispose();
            }
            else if (State == State.Historical)
            {
                // Configuration for historical data
                SetZOrder(-1);
                
                if (!Bars.BarsType.IsIntraday)
                {
                    Draw.TextFixed(this, "NinjaScriptInfo", "AvalP only works on intraday charts", TextPosition.BottomRight);
                    return;
                }
                
                // Calculate the levels once if UseAutomaticDate is false
                if (!UseAutomaticDate)
                    CalculateLevelsForDate();
            }
            else if (State == State.Realtime)
            {
                // Enable chart interaction in real-time
                ChartControl?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    if (ChartControl != null)
                    {
                        // Future interaction logic can be placed here
                    }
                }));
            }
        }

        protected override void OnBarUpdate()
        {
            if (!UseAutomaticDate)
                return;

            try
            {
                if (CurrentBar < 1 || sessionIterator == null) return;
                
                DateTime tradingDay = sessionIterator.GetTradingDay(Time[0]);
                if (tradingDay == DateTime.MinValue) return;

                if (currentDate != tradingDay || currentDayOpen == 0)
                {
                    priorDayHigh = currentDayHigh;
                    priorDayLow = currentDayLow;

                    if (priorDayHigh > 0 && priorDayLow > 0 && priorDayHigh >= priorDayLow)
                    {
                        double previousDayRange = priorDayHigh - priorDayLow;
                        CalculateAllLevels(previousDayRange, ManualPrice);
                        needsLayoutUpdate = true;
                    }

                    if (Open.IsValidDataPoint(0) && High.IsValidDataPoint(0) && Low.IsValidDataPoint(0))
                    {
                        currentDayOpen = Open[0];
                        currentDayHigh = High[0];
                        currentDayLow = Low[0];
                    }
                    currentDate = tradingDay;
                }
                else
                {
                    if (High.IsValidDataPoint(0) && Low.IsValidDataPoint(0))
                    {
                        currentDayHigh = Math.Max(currentDayHigh, High[0]);
                        currentDayLow = Math.Min(currentDayLow, Low[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"Error in OnBarUpdate: {ex.Message}");
            }
        }

        #region Private Methods

        private void InitializePriceLevels()
        {
            priceLevels.Clear();
            priceLevels.Add("Q1", new PriceLevel("Q1", Brushes.Yellow));
            priceLevels.Add("Q4", new PriceLevel("Q4", Brushes.Yellow));
            priceLevels.Add("Q2", new PriceLevel("Q2", Brushes.Plum));
            priceLevels.Add("Q3", new PriceLevel("Q3", Brushes.Plum));
            priceLevels.Add("Q2/3", new PriceLevel("Q2/3", Brushes.ForestGreen));
            priceLevels.Add("Q3/4", new PriceLevel("Q3/4", Brushes.IndianRed));
            priceLevels.Add("TC", new PriceLevel("TC", Brushes.ForestGreen));
            priceLevels.Add("NR1", new PriceLevel("NR1", Brushes.BlueViolet));
            priceLevels.Add("NR2", new PriceLevel("NR2", Brushes.Gold));
            priceLevels.Add("NR3", new PriceLevel("NR3", Brushes.BlueViolet));
            priceLevels.Add("TV", new PriceLevel("TV", Brushes.IndianRed));
            priceLevels.Add("Std1+", new PriceLevel("Std1+", Brushes.ForestGreen));
            priceLevels.Add("Std2+", new PriceLevel("Std2+", Brushes.ForestGreen));
            priceLevels.Add("Std3+", new PriceLevel("Std3+", Brushes.ForestGreen));
            priceLevels.Add("1D+", new PriceLevel("1D+", Brushes.Gold));
            priceLevels.Add("Std1-", new PriceLevel("Std1-", Brushes.IndianRed));
            priceLevels.Add("Std2-", new PriceLevel("Std2-", Brushes.IndianRed));
            priceLevels.Add("Std3-", new PriceLevel("Std3-", Brushes.IndianRed));
            priceLevels.Add("1D-", new PriceLevel("1D-", Brushes.Gold));
        }
        
        private double RoundToQuarter(double value)
        {
            double integerPart = Math.Floor(value);
            double decimalPart = value - integerPart;

            if (decimalPart >= 0.01 && decimalPart <= 0.24) return integerPart + 0.25;
            if (decimalPart >= 0.26 && decimalPart <= 0.49) return integerPart + 0.50;
            if (decimalPart >= 0.51 && decimalPart <= 0.74) return integerPart + 0.75;
            if (decimalPart >= 0.76 && decimalPart <= 0.99) return integerPart + 1.00;
            return value;
        }
        
        private void ValidateCalculatedValues()
        {
            // This method is no longer needed as the variables it validated have been removed.
        }

        private void CalculateLevelsForDate()
        {
            if (Bars == null || Bars.Count == 0)
            {
                Print("DIAGNOSTIC: Bars not loaded, cannot calculate levels.");
                return;
            }

            Print($"DIAGNOSTIC: Starting calculation for selected date: {SelectedDate:d}");
            Print($"DIAGNOSTIC: Chart data series ranges from {Bars.GetTime(0):d} to {Bars.GetTime(Bars.Count - 1):d}");

            double highForDay = double.MinValue;
            double lowForDay = double.MaxValue;
            bool dateFound = false;
            SessionIterator sessionIterator = new SessionIterator(Bars);

            for (int i = 0; i < Bars.Count; i++)
            {
                DateTime barTradingDay = sessionIterator.GetTradingDay(Bars.GetTime(i));
                if (barTradingDay.Date == SelectedDate.Date)
                {
                    dateFound = true;
                    highForDay = Math.Max(highForDay, Bars.GetHigh(i));
                    lowForDay = Math.Min(lowForDay, Bars.GetLow(i));
                }
            }

            if (!dateFound)
            {
                Print($"DIAGNOSTIC: No data found for the selected date: {SelectedDate:d}. Levels will not be drawn.");
                return;
            }

            Print($"DIAGNOSTIC: Data found for {SelectedDate:d}. High: {highForDay}, Low: {lowForDay}");

            if (highForDay > double.MinValue && lowForDay < double.MaxValue)
            {
                double range = highForDay - lowForDay;
                Print($"DIAGNOSTIC: Calculated range: {range}");
                if (range > 0)
                {
                    Print("DIAGNOSTIC: Range is valid. Calculating all levels.");
                    CalculateAllLevels(range, ManualPrice);
                    needsLayoutUpdate = true;
                }
                else
                {
                    Print("DIAGNOSTIC: Calculated range is zero or negative. Levels will not be drawn.");
                }
            }
        }

        private void CalculateAllLevels(double dayRange, double basePrice)
        {
            try
            {
                if (basePrice <= 0 || dayRange <= 0) return;

                double halfRange = dayRange / 2.0;
                double q1Level = RoundToQuarter(basePrice + halfRange);
                double q4Level = RoundToQuarter(basePrice - halfRange);
                
                priceLevels["Q1"].Value = q1Level;
                priceLevels["Q4"].Value = q4Level;
                priceLevels["Q2"].Value = RoundToQuarter(q1Level - (dayRange * 0.25));
                priceLevels["Q3"].Value = RoundToQuarter(q4Level + (dayRange * 0.25));
                priceLevels["Q2/3"].Value = RoundToQuarter(q1Level - (dayRange * 0.375));
                priceLevels["Q3/4"].Value = RoundToQuarter(q4Level + (dayRange * 0.375));
                priceLevels["NR2"].Value = RoundToQuarter(basePrice);
                priceLevels["TC"].Value = RoundToQuarter(q1Level - (dayRange * 0.125));
                priceLevels["NR1"].Value = RoundToQuarter(q1Level - (dayRange * 0.159));
                priceLevels["Std1+"].Value = RoundToQuarter(q1Level + (dayRange * 0.125));
                priceLevels["Std2+"].Value = RoundToQuarter(q1Level + (dayRange * 0.25));
                priceLevels["Std3+"].Value = RoundToQuarter(q1Level + (dayRange * 0.375));
                priceLevels["1D+"].Value = RoundToQuarter(q1Level + (dayRange * 0.50));
                priceLevels["NR3"].Value = RoundToQuarter(q4Level + (dayRange * 0.159));
                priceLevels["TV"].Value = RoundToQuarter(q4Level + (dayRange * 0.125));
                priceLevels["Std1-"].Value = RoundToQuarter(q4Level - (dayRange * 0.125));
                priceLevels["Std2-"].Value = RoundToQuarter(q4Level - (dayRange * 0.25));
                priceLevels["Std3-"].Value = RoundToQuarter(q4Level - (dayRange * 0.375));
                priceLevels["1D-"].Value = RoundToQuarter(q4Level - (dayRange * 0.50));
            }
            catch (Exception ex)
            {
                Print($"Error in CalculateAllLevels: {ex.Message}");
            }
        }

        private void UpdateTextLayouts()
        {
            if (ChartControl == null) return;
            TextFormat textFormat = ChartControl.Properties.LabelFont.ToDirectWriteTextFormat();

            foreach (var level in priceLevels.Values)
            {
                level.LabelLayout?.Dispose();
                if (double.IsNaN(level.Value))
                {
                    level.LabelLayout = null;
                    continue;
                }
                string labelText = $"{level.Name} {RoundToQuarter(level.Value):F2}";
                level.LabelLayout = new TextLayout(Core.Globals.DirectWriteFactory, labelText, textFormat, ChartPanel.W, textFormat.FontSize);
            }
            textFormat.Dispose();
        }

        private SharpDX.Direct2D1.Brush GetDxBrush(System.Windows.Media.Brush wpfBrush)
        {
            if (dxBrushes.TryGetValue(wpfBrush, out SharpDX.Direct2D1.Brush dxBrush))
                return dxBrush;

            dxBrush = wpfBrush.ToDxBrush(RenderTarget);
            dxBrushes.Add(wpfBrush, dxBrush);
            return dxBrush;
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (priceLevels.Count == 0 || !priceLevels.TryGetValue("Q1", out PriceLevel q1) || double.IsNaN(q1.Value))
                return;

            if (needsLayoutUpdate)
            {
                UpdateTextLayouts();
                needsLayoutUpdate = false;
            }

            int lastBarIndex = ChartBars.ToIndex;
            if (lastBarIndex < 0) return;

            double lastBarX = chartControl.GetXByBarIndex(ChartBars, lastBarIndex);
            float lineEndX = (float)(lastBarX + LineBufferPixels); // Aquí sumar en vez de restar
            float lineStartX = ChartPanel.X;
            lineEndX = Math.Min(lineEndX, lineStartX + ChartPanel.W);

            if (lineEndX < lineStartX)
                return;

            // Position label 5 pixels after the line ends for readability
            float labelX = lineEndX + 5;
            SharpDX.Direct2D1.Brush labelBrush = GetDxBrush(chartControl.Properties.ChartText);

            foreach (var level in priceLevels.Values)
            {
                if (double.IsNaN(level.Value) || level.LabelLayout == null)
                    continue;

                float y = (float)chartScale.GetYByValue(level.Value);
                
                RenderTarget.DrawLine(
                    new SharpDX.Vector2(lineStartX, y),
                    new SharpDX.Vector2(lineEndX, y),
                    GetDxBrush(level.LineBrush),
                    Width
                );
                
                Point textPoint = new Point(labelX, y - level.LabelLayout.Metrics.Height / 2);
                RenderTarget.DrawTextLayout(textPoint.ToVector2(), level.LabelLayout, labelBrush);
            }
        }
        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Use Automatic Date", Description = "If true, calculates range from the prior day automatically. If false, uses the 'Selected Date' below.", Order = 1, GroupName = "Parameters")]
        public bool UseAutomaticDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Selected Date", Description = "Date for calculating levels (only used if 'Use Automatic Date' is false).", Order = 2, GroupName = "Parameters")]
        public DateTime SelectedDate
        {
            get { return selectedDate; }
            set { selectedDate = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Manual Price", Description = "Base price for calculating levels", Order = 3, GroupName = "Parameters")]
        public double ManualPrice
        {
            get { return manualPrice; }
            set { manualPrice = Math.Max(0, value); }
        }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Line Width", Description = "Width of the level lines", Order = 4, GroupName = "Visuals")]
        public int Width { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Line Buffer (Pixels)", Description = "Pixel buffer from the last bar for line drawing.", Order = 5, GroupName = "Visuals")]
        public int LineBufferPixels { get; set; }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private AvalP[] cacheAvalP;
		public AvalP AvalP(bool useAutomaticDate, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return AvalP(Input, useAutomaticDate, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public AvalP AvalP(ISeries<double> input, bool useAutomaticDate, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			if (cacheAvalP != null)
				for (int idx = 0; idx < cacheAvalP.Length; idx++)
					if (cacheAvalP[idx] != null && cacheAvalP[idx].UseAutomaticDate == useAutomaticDate && cacheAvalP[idx].SelectedDate == selectedDate && cacheAvalP[idx].ManualPrice == manualPrice && cacheAvalP[idx].Width == width && cacheAvalP[idx].LineBufferPixels == lineBufferPixels && cacheAvalP[idx].EqualsInput(input))
						return cacheAvalP[idx];
			return CacheIndicator<AvalP>(new AvalP(){ UseAutomaticDate = useAutomaticDate, SelectedDate = selectedDate, ManualPrice = manualPrice, Width = width, LineBufferPixels = lineBufferPixels }, input, ref cacheAvalP);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.AvalP AvalP(bool useAutomaticDate, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalP(Input, useAutomaticDate, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public Indicators.AvalP AvalP(ISeries<double> input , bool useAutomaticDate, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalP(input, useAutomaticDate, selectedDate, manualPrice, width, lineBufferPixels);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.AvalP AvalP(bool useAutomaticDate, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalP(Input, useAutomaticDate, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public Indicators.AvalP AvalP(ISeries<double> input , bool useAutomaticDate, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalP(input, useAutomaticDate, selectedDate, manualPrice, width, lineBufferPixels);
		}
	}
}

#endregion
