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
    public class APval : Indicator
    {
        #region Variables
        private DateTime selectedDate;
        private double manualPrice;
        private SessionIterator sessionIterator;
        private DateTime currentDate = Core.Globals.MinDate;
        private double currentDayOpen;
        private Bars dailyBars;
        private readonly Dictionary<string, PriceLevel> priceLevels = new Dictionary<string, PriceLevel>();

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

        private readonly Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush> dxBrushes = new Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush>();
        private bool needsLayoutUpdate = false;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Calculates and displays price levels based on a modified previous day's range (with optional gap) and a manual price. All levels are exposed for Strategy Analyzer.";
                Name = "APval";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                IsSuspendedWhileInactive = true;
                BarsRequiredToPlot = 2;

                UseAutomaticDate = true;
                UseGapCalculation = false;
                SelectedDate = DateTime.Today;
                ManualPrice = 0.0;
                Width = 1;
                LineBufferPixels = 125;

                // Add plots for all levels to make them available to the Strategy Analyzer
                AddPlot(Brushes.Transparent, "Q1_Plot");
                AddPlot(Brushes.Transparent, "Q4_Plot");
                AddPlot(Brushes.Transparent, "Q2_Plot");
                AddPlot(Brushes.Transparent, "Q3_Plot");
                AddPlot(Brushes.Transparent, "Q2_3_Plot");
                AddPlot(Brushes.Transparent, "Q3_4_Plot");
                AddPlot(Brushes.Transparent, "TC_Plot");
                AddPlot(Brushes.Transparent, "NR1_Plot");
                AddPlot(Brushes.Transparent, "NR2_Plot");
                AddPlot(Brushes.Transparent, "NR3_Plot");
                AddPlot(Brushes.Transparent, "TV_Plot");
                AddPlot(Brushes.Transparent, "Std1_Plus_Plot");
                AddPlot(Brushes.Transparent, "Std2_Plus_Plot");
                AddPlot(Brushes.Transparent, "Std3_Plus_Plot");
                AddPlot(Brushes.Transparent, "OneD_Plus_Plot");
                AddPlot(Brushes.Transparent, "Std1_Minus_Plot");
                AddPlot(Brushes.Transparent, "Std2_Minus_Plot");
                AddPlot(Brushes.Transparent, "Std3_Minus_Plot");
                AddPlot(Brushes.Transparent, "OneD_Minus_Plot");
            }
            else if (State == State.Configure)
            {
                AddDataSeries(Bars.Instrument.FullName, BarsPeriodType.Day, 1);
                InitializePriceLevels();
            }
            else if (State == State.DataLoaded)
            {
                dailyBars = BarsArray[1];
                sessionIterator = new SessionIterator(Bars);
            }
            else if (State == State.Terminated)
            {
                foreach (var brush in dxBrushes.Values) brush.Dispose();
                dxBrushes.Clear();
                foreach (var level in priceLevels.Values) level.Dispose();
            }
            else if (State == State.Historical)
            {
                SetZOrder(-1);
                if (!Bars.BarsType.IsIntraday)
                {
                    Draw.TextFixed(this, "NinjaScriptInfo", "APval only works on intraday charts", TextPosition.BottomRight);
                    return;
                }
                if (!UseAutomaticDate)
                    CalculateLevelsForDate();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToPlot || dailyBars == null || dailyBars.Count < 1) return;
            if (!UseAutomaticDate) return;

            try
            {
                if (sessionIterator == null) return;
                DateTime tradingDay = sessionIterator.GetTradingDay(Time[0]);
                if (tradingDay == DateTime.MinValue) return;

                if (currentDate != tradingDay)
                {
                    currentDate = tradingDay;
                    currentDayOpen = Open[0];
                    DateTime calculationDate = GetCalculationDate();
                    int dailyBarIndex = dailyBars.GetBar(calculationDate);
                    if (dailyBarIndex < 0) return;

                    double priorDayHigh = dailyBars.GetHigh(dailyBarIndex);
                    double priorDayLow = dailyBars.GetLow(dailyBarIndex);

                    if (priorDayHigh > 0 && priorDayLow > 0 && priorDayHigh >= priorDayLow)
                    {
                        double previousDayRange = priorDayHigh - priorDayLow;
                        if (UseGapCalculation)
                        {
                            double priorDayClose = GetPriorDayClose(Time[0], false);
                            if (priorDayClose > 0 && currentDayOpen > 0)
                            {
                                double gap = Math.Abs(currentDayOpen - priorDayClose);
                                previousDayRange += gap;
                            }
                        }
                        
                        double basePrice = ManualPrice;
                        if (basePrice.ApproxCompare(0) == 0)
                        {
                            basePrice = GetPriorDayClose(Time[0], false);
                        }

                        if (basePrice > 0 && previousDayRange > 0)
                        {
                            CalculateAllLevels(previousDayRange, basePrice);
                            needsLayoutUpdate = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"Error in OnBarUpdate: {ex.Message}");
            }
        }

        #region Private Methods
        private DateTime GetCalculationDate()
        {
            if (!UseAutomaticDate) return SelectedDate.Date;
            DateTime today = Time[0].Date;
            DateTime calcDate = today.AddDays(-1);
            switch (today.DayOfWeek)
            {
                case DayOfWeek.Monday: calcDate = today.AddDays(-3); break;
                case DayOfWeek.Sunday: calcDate = today.AddDays(-2); break;
            }
            return calcDate;
        }

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

        private double GetPriorDayClose(DateTime time, bool printLog = false)
        {
            if (dailyBars == null || dailyBars.Count < 2) return 0;
            int dailyIndex = dailyBars.GetBar(time);
            if (dailyIndex > 0) return dailyBars.GetClose(dailyIndex - 1);
            return 0;
        }

        private void CalculateLevelsForDate()
        {
            DateTime calculationDate = GetCalculationDate();
            int dailyBarIndex = dailyBars.GetBar(calculationDate);
            if (dailyBarIndex < 0) return;

            double highForDay = dailyBars.GetHigh(dailyBarIndex);
            double lowForDay = dailyBars.GetLow(dailyBarIndex);

            if (highForDay > double.MinValue && lowForDay < double.MaxValue)
            {
                double range = highForDay - lowForDay;
                if (range > 0)
                {
                    double basePrice = ManualPrice;
                    if (basePrice.ApproxCompare(0) == 0)
                    {
                        basePrice = GetPriorDayClose(calculationDate.AddDays(1));
                    }
                    if (basePrice > 0)
                    {
                        CalculateAllLevels(range, basePrice);
                        needsLayoutUpdate = true;
                    }
                }
            }
        }

        private void CalculateAllLevels(double dayRange, double basePrice)
        {
            if (basePrice <= 0 || dayRange <= 0) return;
            double halfRange = dayRange / 2.0;
            
            priceLevels["Q1"].Value = RoundToQuarter(basePrice + halfRange);
            priceLevels["Q4"].Value = RoundToQuarter(basePrice - halfRange);
            priceLevels["Q2"].Value = RoundToQuarter(priceLevels["Q1"].Value - (dayRange * 0.25));
            priceLevels["Q3"].Value = RoundToQuarter(priceLevels["Q4"].Value + (dayRange * 0.25));
            priceLevels["Q2/3"].Value = RoundToQuarter(priceLevels["Q1"].Value - (dayRange * 0.375));
            priceLevels["Q3/4"].Value = RoundToQuarter(priceLevels["Q4"].Value + (dayRange * 0.375));
            priceLevels["NR2"].Value = RoundToQuarter(basePrice);
            priceLevels["TC"].Value = RoundToQuarter(priceLevels["Q1"].Value - (dayRange * 0.125));
            priceLevels["NR1"].Value = RoundToQuarter(priceLevels["Q1"].Value - (dayRange * 0.159));
            priceLevels["Std1+"].Value = RoundToQuarter(priceLevels["Q1"].Value + (dayRange * 0.125));
            priceLevels["Std2+"].Value = RoundToQuarter(priceLevels["Q1"].Value + (dayRange * 0.25));
            priceLevels["Std3+"].Value = RoundToQuarter(priceLevels["Q1"].Value + (dayRange * 0.375));
            priceLevels["1D+"].Value = RoundToQuarter(priceLevels["Q1"].Value + (dayRange * 0.50));
            priceLevels["NR3"].Value = RoundToQuarter(priceLevels["Q4"].Value + (dayRange * 0.159));
            priceLevels["TV"].Value = RoundToQuarter(priceLevels["Q4"].Value + (dayRange * 0.125));
            priceLevels["Std1-"].Value = RoundToQuarter(priceLevels["Q4"].Value - (dayRange * 0.125));
            priceLevels["Std2-"].Value = RoundToQuarter(priceLevels["Q4"].Value - (dayRange * 0.25));
            priceLevels["Std3-"].Value = RoundToQuarter(priceLevels["Q4"].Value - (dayRange * 0.375));
            priceLevels["1D-"].Value = RoundToQuarter(priceLevels["Q4"].Value - (dayRange * 0.50));

            // Set plot values for strategy analyzer
            Q1_Plot[0] = priceLevels["Q1"].Value;
            Q4_Plot[0] = priceLevels["Q4"].Value;
            Q2_Plot[0] = priceLevels["Q2"].Value;
            Q3_Plot[0] = priceLevels["Q3"].Value;
            Q2_3_Plot[0] = priceLevels["Q2/3"].Value;
            Q3_4_Plot[0] = priceLevels["Q3/4"].Value;
            TC_Plot[0] = priceLevels["TC"].Value;
            NR1_Plot[0] = priceLevels["NR1"].Value;
            NR2_Plot[0] = priceLevels["NR2"].Value;
            NR3_Plot[0] = priceLevels["NR3"].Value;
            TV_Plot[0] = priceLevels["TV"].Value;
            Std1_Plus_Plot[0] = priceLevels["Std1+"].Value;
            Std2_Plus_Plot[0] = priceLevels["Std2+"].Value;
            Std3_Plus_Plot[0] = priceLevels["Std3+"].Value;
            OneD_Plus_Plot[0] = priceLevels["1D+"].Value;
            Std1_Minus_Plot[0] = priceLevels["Std1-"].Value;
            Std2_Minus_Plot[0] = priceLevels["Std2-"].Value;
            Std3_Minus_Plot[0] = priceLevels["Std3-"].Value;
            OneD_Minus_Plot[0] = priceLevels["1D-"].Value;
        }

        private void UpdateTextLayouts()
        {
            if (ChartControl == null) return;
            TextFormat textFormat = ChartControl.Properties.LabelFont.ToDirectWriteTextFormat();
            foreach (var level in priceLevels.Values)
            {
                level.LabelLayout?.Dispose();
                if (double.IsNaN(level.Value)) { level.LabelLayout = null; continue; }
                string labelText = $"{level.Name} {RoundToQuarter(level.Value):F2}";
                level.LabelLayout = new TextLayout(Core.Globals.DirectWriteFactory, labelText, textFormat, ChartPanel.W, textFormat.FontSize);
            }
            textFormat.Dispose();
        }

        private SharpDX.Direct2D1.Brush GetDxBrush(System.Windows.Media.Brush wpfBrush)
        {
            if (dxBrushes.TryGetValue(wpfBrush, out SharpDX.Direct2D1.Brush dxBrush)) return dxBrush;
            dxBrush = wpfBrush.ToDxBrush(RenderTarget);
            dxBrushes.Add(wpfBrush, dxBrush);
            return dxBrush;
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (priceLevels.Count == 0 || !priceLevels.TryGetValue("Q1", out PriceLevel q1) || double.IsNaN(q1.Value)) return;
            if (needsLayoutUpdate) { UpdateTextLayouts(); needsLayoutUpdate = false; }
            int lastBarIndex = ChartBars.ToIndex;
            if (lastBarIndex < 0) return;
            double lastBarX = chartControl.GetXByBarIndex(ChartBars, lastBarIndex);
            float lineEndX = (float)(lastBarX + LineBufferPixels);
            float lineStartX = ChartPanel.X;
            lineEndX = Math.Min(lineEndX, lineStartX + ChartPanel.W);
            if (lineEndX < lineStartX) return;
            float labelX = lineEndX + 5;
            SharpDX.Direct2D1.Brush labelBrush = GetDxBrush(chartControl.Properties.ChartText);
            foreach (var level in priceLevels.Values)
            {
                if (double.IsNaN(level.Value) || level.LabelLayout == null) continue;
                float y = (float)chartScale.GetYByValue(level.Value);
                RenderTarget.DrawLine(new SharpDX.Vector2(lineStartX, y), new SharpDX.Vector2(lineEndX, y), GetDxBrush(level.LineBrush), Width);
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
        [Display(Name = "GAP", Description = "If true, adds the opening gap to the previous day's range.", Order = 2, GroupName = "Parameters")]
        public bool UseGapCalculation { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Selected Date", Description = "Date for calculating levels (only used if 'Use Automatic Date' is false).", Order = 3, GroupName = "Parameters")]
        public DateTime SelectedDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Manual Price", Description = "Base price for levels. If 0, uses prior day's close.", Order = 4, GroupName = "Parameters")]
        public double ManualPrice { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Line Width", Description = "Width of the level lines", Order = 5, GroupName = "Visuals")]
        public int Width { get; set; }
        
        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Line Buffer (Pixels)", Description = "Pixel buffer from the last bar for line drawing.", Order = 6, GroupName = "Visuals")]
        public int LineBufferPixels { get; set; }

        // Plots for Strategy Analyzer access
        [Browsable(false)][XmlIgnore] public Series<double> Q1_Plot { get { return Values[0]; } }
        [Browsable(false)][XmlIgnore] public Series<double> Q4_Plot { get { return Values[1]; } }
        [Browsable(false)][XmlIgnore] public Series<double> Q2_Plot { get { return Values[2]; } }
        [Browsable(false)][XmlIgnore] public Series<double> Q3_Plot { get { return Values[3]; } }
        [Browsable(false)][XmlIgnore] public Series<double> Q2_3_Plot { get { return Values[4]; } }
        [Browsable(false)][XmlIgnore] public Series<double> Q3_4_Plot { get { return Values[5]; } }
        [Browsable(false)][XmlIgnore] public Series<double> TC_Plot { get { return Values[6]; } }
        [Browsable(false)][XmlIgnore] public Series<double> NR1_Plot { get { return Values[7]; } }
        [Browsable(false)][XmlIgnore] public Series<double> NR2_Plot { get { return Values[8]; } }
        [Browsable(false)][XmlIgnore] public Series<double> NR3_Plot { get { return Values[9]; } }
        [Browsable(false)][XmlIgnore] public Series<double> TV_Plot { get { return Values[10]; } }
        [Browsable(false)][XmlIgnore] public Series<double> Std1_Plus_Plot { get { return Values[11]; } }
        [Browsable(false)][XmlIgnore] public Series<double> Std2_Plus_Plot { get { return Values[12]; } }
        [Browsable(false)][XmlIgnore] public Series<double> Std3_Plus_Plot { get { return Values[13]; } }
        [Browsable(false)][XmlIgnore] public Series<double> OneD_Plus_Plot { get { return Values[14]; } }
        [Browsable(false)][XmlIgnore] public Series<double> Std1_Minus_Plot { get { return Values[15]; } }
        [Browsable(false)][XmlIgnore] public Series<double> Std2_Minus_Plot { get { return Values[16]; } }
        [Browsable(false)][XmlIgnore] public Series<double> Std3_Minus_Plot { get { return Values[17]; } }
        [Browsable(false)][XmlIgnore] public Series<double> OneD_Minus_Plot { get { return Values[18]; } }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private APval[] cacheAPval;
		public APval APval(bool useAutomaticDate, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return APval(Input, useAutomaticDate, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public APval APval(ISeries<double> input, bool useAutomaticDate, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			if (cacheAPval != null)
				for (int idx = 0; idx < cacheAPval.Length; idx++)
					if (cacheAPval[idx] != null && cacheAPval[idx].UseAutomaticDate == useAutomaticDate && cacheAPval[idx].UseGapCalculation == useGapCalculation && cacheAPval[idx].SelectedDate == selectedDate && cacheAPval[idx].ManualPrice == manualPrice && cacheAPval[idx].Width == width && cacheAPval[idx].LineBufferPixels == lineBufferPixels && cacheAPval[idx].EqualsInput(input))
						return cacheAPval[idx];
			return CacheIndicator<APval>(new APval(){ UseAutomaticDate = useAutomaticDate, UseGapCalculation = useGapCalculation, SelectedDate = selectedDate, ManualPrice = manualPrice, Width = width, LineBufferPixels = lineBufferPixels }, input, ref cacheAPval);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.APval APval(bool useAutomaticDate, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.APval(Input, useAutomaticDate, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public Indicators.APval APval(ISeries<double> input , bool useAutomaticDate, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.APval(input, useAutomaticDate, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.APval APval(bool useAutomaticDate, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.APval(Input, useAutomaticDate, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public Indicators.APval APval(ISeries<double> input , bool useAutomaticDate, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.APval(input, useAutomaticDate, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}
	}
}

#endregion
