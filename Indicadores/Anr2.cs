using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;

// Enum to define the chart corners for table positioning
public enum ChartCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

namespace NinjaTrader.NinjaScript.Indicators
{
    public class Anr2 : Indicator
    {
        #region Variables
        private double previousDayRange, halfRange, nr2Level, maxCorrected, minCorrected;
        private double priorDayHigh, priorDayLow;
        private DateTime calculationDate = Core.Globals.MinDate;
        private SessionIterator sessionIterator;
        private DateTime currentDate = Core.Globals.MinDate;
        private double currentDayOpen = 0;
        private double gapValue = 0;
        private double gapRangeValue = 0;
        private double originalPDR = 0;
        private double originalHalfRange = 0;

        private Bars dailyBars;
        private SharpDX.Direct2D1.SolidColorBrush backgroundBrush;
        private SharpDX.Direct2D1.SolidColorBrush borderBrush;
        private SharpDX.Direct2D1.SolidColorBrush textBrush;
        private SharpDX.DirectWrite.TextFormat textFormat;
        #endregion

        #region OnStateChange
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Displays a table with NR2 levels and price range information";
                Name = "Anr2";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                IsSuspendedWhileInactive = true;
                BarsRequiredToPlot = 2;

                UseManualDate = false;
                ManualDate = DateTime.Now;
                ManualNR2Level = 0;
                TablePosition = ChartCorner.TopRight;
                UseGapCalculation = false;

                AddPlot(Brushes.Transparent, "DummyPlot");
            }
            else if (State == State.Configure)
            {
                AddDataSeries(Bars.Instrument.FullName, BarsPeriodType.Day, 1);
            }
            else if (State == State.DataLoaded)
            {
                dailyBars = BarsArray[1];
                sessionIterator = new SessionIterator(Bars);
            }
            else if (State == State.Historical)
            {
                if (!Bars.BarsType.IsIntraday)
                {
                    Draw.TextFixed(this, "NinjaScriptInfo", "Anr2 only works on intraday charts", TextPosition.BottomRight);
                    Log("Anr2 only works on intraday charts", LogLevel.Error);
                }
            }
            else if (State == State.Terminated)
            {
                // Dispose of brushes
                if (backgroundBrush != null) backgroundBrush.Dispose();
                if (borderBrush != null) borderBrush.Dispose();
                if (textBrush != null) textBrush.Dispose();
                if (textFormat != null) textFormat.Dispose();
            }
        }
        #endregion

        #region OnBarUpdate
        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToPlot || BarsArray[1] == null || BarsArray[1].Count < 1)
                return;

            // Session detection for getting the open of the day
            if (sessionIterator.GetTradingDay(Time[0]) != currentDate)
            {
                currentDate = sessionIterator.GetTradingDay(Time[0]);
                currentDayOpen = Open[0];
            }

            // Determine the date for calculation
            calculationDate = GetCalculationDate(currentDate);
            
            // Find the index of the calculation date in the daily bars series
            int dailyBarIndex = dailyBars.GetBar(calculationDate);
            if (dailyBarIndex < 0)
                return;

            // Get high and low of that day
            priorDayHigh = dailyBars.GetHigh(dailyBarIndex);
            priorDayLow = dailyBars.GetLow(dailyBarIndex);

            originalPDR = 0;
            if (priorDayHigh > 0 && priorDayLow > 0 && priorDayHigh >= priorDayLow)
            {
                originalPDR = priorDayHigh - priorDayLow;
            }
            originalHalfRange = originalPDR / 2;

            double gap = 0;
            if (UseGapCalculation)
            {
                double previousDayClose = GetPriorDayClose(Time[0]);
                if (previousDayClose > 0 && currentDayOpen > 0)
                {
                    gap = Math.Abs(currentDayOpen - previousDayClose);
                }
            }

            previousDayRange = originalPDR + gap; // previousDayRange is the modified range for calculations
            halfRange = previousDayRange / 2;

            // Store values for table
            gapValue = gap / 2;
            gapRangeValue = previousDayRange / 2;

            if (!UseGapCalculation)
            {
                gapValue = 0;
                gapRangeValue = 0;
            }

            nr2Level = ManualNR2Level;
            if (nr2Level <= 0)
            {
                nr2Level = GetPriorDayClose(Time[0]);
            }

            maxCorrected = nr2Level + halfRange;
            minCorrected = nr2Level - halfRange;
        }
        #endregion

        #region OnRender
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (ChartPanel == null || RenderTarget == null || previousDayRange <= 0)
                return;

            // Lazy initialization of SharpDX resources
            if (backgroundBrush == null || backgroundBrush.IsDisposed)
                backgroundBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(0, 0, 0, 0)); // Transparent
            if (borderBrush == null || borderBrush.IsDisposed)
                borderBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255, 255, 255, 255)); // White
            if (textBrush == null || textBrush.IsDisposed)
                textBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255, 255, 255, 255)); // White
            if (textFormat == null || textFormat.IsDisposed)
            {
                NinjaTrader.Gui.Tools.SimpleFont simpleFont = chartControl.Properties.LabelFont ?? new NinjaTrader.Gui.Tools.SimpleFont("Arial", 10);
                textFormat = simpleFont.ToDirectWriteTextFormat();
            }

            float rowHeight = 20f;
            float columnWidth = 150f;
            float tableWidth = columnWidth * 2;
            float tableHeight = rowHeight * 10; // Header + 9 rows
            float margin = 20f;

            SharpDX.Vector2 panelPosition;
            switch (TablePosition)
            {
                case ChartCorner.TopRight:
                    panelPosition = new SharpDX.Vector2(ChartPanel.X + ChartPanel.W - tableWidth - margin, ChartPanel.Y + margin);
                    break;
                case ChartCorner.BottomLeft:
                    panelPosition = new SharpDX.Vector2(ChartPanel.X + margin, ChartPanel.Y + ChartPanel.H - tableHeight - margin);
                    break;
                case ChartCorner.BottomRight:
                    panelPosition = new SharpDX.Vector2(ChartPanel.X + ChartPanel.W - tableWidth - margin, ChartPanel.Y + ChartPanel.H - tableHeight - margin);
                    break;
                case ChartCorner.TopLeft:
                default:
                    panelPosition = new SharpDX.Vector2(ChartPanel.X + margin, ChartPanel.Y + margin);
                    break;
            }

            SharpDX.RectangleF backgroundRect = new SharpDX.RectangleF(panelPosition.X, panelPosition.Y, tableWidth, tableHeight);
            
            RenderTarget.FillRectangle(backgroundRect, backgroundBrush);
            RenderTarget.DrawRectangle(backgroundRect, borderBrush, 1);

            for (int i = 0; i <= 9; i++)
            {
                SharpDX.Vector2 startPoint = new SharpDX.Vector2(panelPosition.X, panelPosition.Y + (i * rowHeight));
                SharpDX.Vector2 endPoint = new SharpDX.Vector2(panelPosition.X + tableWidth, panelPosition.Y + (i * rowHeight));
                RenderTarget.DrawLine(startPoint, endPoint, borderBrush, 1);
            }

            for (int i = 0; i <= 2; i++)
            {
                SharpDX.Vector2 startPoint = new SharpDX.Vector2(panelPosition.X + (i * columnWidth), panelPosition.Y);
                SharpDX.Vector2 endPoint = new SharpDX.Vector2(panelPosition.X + (i * columnWidth), panelPosition.Y + tableHeight);
                RenderTarget.DrawLine(startPoint, endPoint, borderBrush, 1);
            }

            float textMargin = 5f;
            DrawText(RenderTarget, "Description", textFormat, textBrush, new SharpDX.RectangleF(panelPosition.X + textMargin, panelPosition.Y, columnWidth - textMargin, rowHeight));
            DrawText(RenderTarget, "Value", textFormat, textBrush, new SharpDX.RectangleF(panelPosition.X + columnWidth + textMargin, panelPosition.Y, columnWidth - textMargin, rowHeight));

            string[] descriptions = { "Máximo", "Mínimo", "PDR", "Rango", "GAP", "Rango GAP", "NR2 Level", "Max Corrected", "Min Corrected" };
            double[] values = { priorDayHigh, priorDayLow, originalPDR, originalHalfRange, gapValue, gapRangeValue, nr2Level, maxCorrected, minCorrected };

            for (int i = 0; i < descriptions.Length; i++)
            {
                DrawText(RenderTarget, descriptions[i], textFormat, textBrush, new SharpDX.RectangleF(panelPosition.X + textMargin, panelPosition.Y + ((i + 1) * rowHeight), columnWidth - textMargin, rowHeight));
                string formattedValue = FormatPrice(values[i]);
                DrawText(RenderTarget, formattedValue, textFormat, textBrush, new SharpDX.RectangleF(panelPosition.X + columnWidth + textMargin, panelPosition.Y + ((i + 1) * rowHeight), columnWidth - textMargin, rowHeight));
            }
        }
        #endregion

        #region Helper Methods
        private double GetPriorDayClose(DateTime time)
        {
            if (BarsArray[1] == null || BarsArray[1].Count < 2)
            {
                Log("DIAGNOSTIC: Daily series not ready or not enough data to find prior day's close.", LogLevel.Information);
                return 0;
            }

            int dailyIndex = BarsArray[1].GetBar(time);

            if (dailyIndex > 0)
            {
                return BarsArray[1].GetClose(dailyIndex - 1);
            }

            Log($"DIAGNOSTIC: Could not find a prior day's close for the date {time:d}. The provided time might be too early in the data series.", LogLevel.Information);
            return 0;
        }

        private DateTime GetCalculationDate(DateTime baseDate)
        {
            if (UseManualDate)
                return ManualDate.Date;

            DateTime calcDate = baseDate.AddDays(-1); // Default to yesterday

            switch (baseDate.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    calcDate = baseDate.AddDays(-3);
                    break;
                case DayOfWeek.Sunday:
                    calcDate = baseDate.AddDays(-2);
                    break;
                case DayOfWeek.Saturday:
                    calcDate = baseDate.AddDays(-1); // For Saturday, previous day is Friday.
                    break;
            }
            return calcDate;
        }

        private void DrawText(RenderTarget renderTarget, string text, TextFormat textFormat, SharpDX.Direct2D1.Brush brush, RectangleF layoutRect)
        {
            using (var textLayout = new TextLayout(NinjaTrader.Core.Globals.DirectWriteFactory, text, textFormat, layoutRect.Width, layoutRect.Height))
            {
                renderTarget.DrawTextLayout(new SharpDX.Vector2(layoutRect.X, layoutRect.Y), textLayout, brush, DrawTextOptions.NoSnap);
            }
        }

        private string FormatPrice(double price)
        {
            if (double.IsNaN(price) || double.IsInfinity(price)) return "N/A";
            if (Instrument != null && Instrument.MasterInstrument != null) return Instrument.MasterInstrument.FormatPrice(price);
            return price.ToString("F2");
        }
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Use Manual Date", Description = "Enable to select a manual date for range calculation", Order = 1, GroupName = "Parameters")]
        public bool UseManualDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Manual Date", Description = "Date for range calculation", Order = 2, GroupName = "Parameters")]
        public DateTime ManualDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Manual NR2 Level", Description = "Manual input for NR2 level", Order = 3, GroupName = "Parameters")]
        public double ManualNR2Level { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Table Position", Description = "Position of the data table on the chart.", Order = 4, GroupName = "Parameters")]
        public ChartCorner TablePosition { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "GAP", Description = "Enable to include the opening gap in the range calculation.", Order = 5, GroupName = "Parameters")]
        public bool UseGapCalculation { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> DummyPlot { get { return Values[0]; } }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Anr2[] cacheAnr2;
		public Anr2 Anr2(bool useManualDate, DateTime manualDate, double manualNR2Level, ChartCorner tablePosition, bool useGapCalculation)
		{
			return Anr2(Input, useManualDate, manualDate, manualNR2Level, tablePosition, useGapCalculation);
		}

		public Anr2 Anr2(ISeries<double> input, bool useManualDate, DateTime manualDate, double manualNR2Level, ChartCorner tablePosition, bool useGapCalculation)
		{
			if (cacheAnr2 != null)
				for (int idx = 0; idx < cacheAnr2.Length; idx++)
					if (cacheAnr2[idx] != null && cacheAnr2[idx].UseManualDate == useManualDate && cacheAnr2[idx].ManualDate == manualDate && cacheAnr2[idx].ManualNR2Level == manualNR2Level && cacheAnr2[idx].TablePosition == tablePosition && cacheAnr2[idx].UseGapCalculation == useGapCalculation && cacheAnr2[idx].EqualsInput(input))
						return cacheAnr2[idx];
			return CacheIndicator<Anr2>(new Anr2(){ UseManualDate = useManualDate, ManualDate = manualDate, ManualNR2Level = manualNR2Level, TablePosition = tablePosition, UseGapCalculation = useGapCalculation }, input, ref cacheAnr2);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Anr2 Anr2(bool useManualDate, DateTime manualDate, double manualNR2Level, ChartCorner tablePosition, bool useGapCalculation)
		{
			return indicator.Anr2(Input, useManualDate, manualDate, manualNR2Level, tablePosition, useGapCalculation);
		}

		public Indicators.Anr2 Anr2(ISeries<double> input , bool useManualDate, DateTime manualDate, double manualNR2Level, ChartCorner tablePosition, bool useGapCalculation)
		{
			return indicator.Anr2(input, useManualDate, manualDate, manualNR2Level, tablePosition, useGapCalculation);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Anr2 Anr2(bool useManualDate, DateTime manualDate, double manualNR2Level, ChartCorner tablePosition, bool useGapCalculation)
		{
			return indicator.Anr2(Input, useManualDate, manualDate, manualNR2Level, tablePosition, useGapCalculation);
		}

		public Indicators.Anr2 Anr2(ISeries<double> input , bool useManualDate, DateTime manualDate, double manualNR2Level, ChartCorner tablePosition, bool useGapCalculation)
		{
			return indicator.Anr2(input, useManualDate, manualDate, manualNR2Level, tablePosition, useGapCalculation);
		}
	}
}

#endregion
