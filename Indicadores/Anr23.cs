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
    /// <summary>
    /// Indicator that displays a table with NR2 levels and price range information
    /// </summary>
    public class Anr23 : Indicator
    {
        #region Variables
        private double previousDayRange = 0;
        private double halfRange = 0;
        private double nr2Level = 0;
        private double maxCorrected = 0;
        private double minCorrected = 0;
        private SessionIterator sessionIterator;
        private DateTime currentDate = Core.Globals.MinDate;
        private double currentDayHigh = 0;
        private double currentDayLow = 0;
        private double currentDayOpen = 0;
        private double priorDayHigh = 0;
        private double priorDayLow = 0;
        private double priorDayOpen = 0;
        
        // User input for NR2 level
        private double userNR2Level = 0;
        #endregion

        #region OnStateChange
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Displays a table with NR2 levels and price range information";
                Name = "Anr23";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                IsSuspendedWhileInactive = true;
                BarsRequiredToPlot = 2; // Need at least 2 bars to calculate previous day range
                UserNR2Level = 0.0;
                TablePosition = ChartCorner.TopRight;

                AddPlot(Brushes.Transparent, "DummyPlot");
            }
            else if (State == State.Configure)
            {
                // Add the daily data series
                AddDataSeries(Bars.Instrument.FullName, BarsPeriodType.Day, 1);

                // Reset values
                previousDayRange = 0;
                halfRange = 0;
                nr2Level = 0;
                maxCorrected = 0;
                minCorrected = 0;
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
        }
        #endregion

        #region OnBarUpdate
        protected override void OnBarUpdate()
        {
            try
            {
                // VALIDATION CRITICAL 1: Check minimum bars
                if (CurrentBar < BarsRequiredToPlot)
                {
                    return;
                }

                // VALIDATION CRITICAL 2: Check valid data
                if (double.IsNaN(Close[0]) || Close[0] <= 0)
                    return;

                // VALIDATION CRITICAL 3: Only work with intraday data
                if (!Bars.BarsType.IsIntraday)
                    return;

                // If the current data is not the same date as the current bar then its a new session
                if (currentDate != sessionIterator.GetTradingDay(Time[0]) || currentDayOpen == 0)
                {
                    // The current day OHLC values are now the prior days value so set
                    // them for our calculations
                    priorDayOpen = currentDayOpen;
                    priorDayHigh = currentDayHigh;
                    priorDayLow = currentDayLow;

                    // Calculate previous day range
                    if (priorDayHigh > 0 && priorDayLow > 0 && priorDayHigh >= priorDayLow)
                    {
                        previousDayRange = priorDayHigh - priorDayLow;
                        halfRange = previousDayRange / 2;
                    }

                    // Initialize the current day settings to the new days data
                    currentDayOpen = Open[0];
                    currentDayHigh = High[0];
                    currentDayLow = Low[0];

                    currentDate = sessionIterator.GetTradingDay(Time[0]);
                }
                else // The current day is the same day
                {
                    // Set the current day OHLC values
                    currentDayHigh = Math.Max(currentDayHigh, High[0]);
                    currentDayLow = Math.Min(currentDayLow, Low[0]);
                }

                // Calculate levels based on user input NR2 level
                nr2Level = userNR2Level;
                
                // If UserNR2Level is not set, use the prior day's close
                if (nr2Level.ApproxCompare(0) == 0)
                {
                    nr2Level = GetPriorDayClose(Time[0]);
                }

                // Calculate corrected levels
                maxCorrected = nr2Level + halfRange;
                minCorrected = nr2Level - halfRange;

                // Validate results
                if (double.IsNaN(previousDayRange) || double.IsInfinity(previousDayRange))
                    previousDayRange = 0;
                    
                if (double.IsNaN(halfRange) || double.IsInfinity(halfRange))
                    halfRange = 0;
                    
                if (double.IsNaN(maxCorrected) || double.IsInfinity(maxCorrected))
                    maxCorrected = 0;
                    
                if (double.IsNaN(minCorrected) || double.IsInfinity(minCorrected))
                    minCorrected = 0;
            }
            catch (Exception ex)
            {
                // Log error for debugging
                Print($"Error in OnBarUpdate: {ex.Message}");
            }
        }
        #endregion

        #region Private Methods
        private double GetPriorDayClose(DateTime time)
        {
            // Ensure the daily series has data
            if (BarsArray[1] == null || BarsArray[1].Count < 2)
            {
                Print("DIAGNOSTIC: Daily series not ready or not enough data to find prior day's close.");
                return 0;
            }

            // Get the index of the daily bar corresponding to the given time
            int dailyIndex = BarsArray[1].GetBar(time);

            // If the index is valid and not the very first bar, we can get the previous bar's close
            if (dailyIndex > 0)
            {
                return BarsArray[1].GetClose(dailyIndex - 1);
            }
            
            // Handle edge cases where the given time is before the second bar of the series
            Print($"DIAGNOSTIC: Could not find a prior day's close for the date {time:d}. The provided time might be too early in the data series.");
            return 0;
        }
        #endregion

        #region OnRender
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            try
            {
                // Call base rendering
                base.OnRender(chartControl, chartScale);

                // Only render if we have valid data
                if (ChartPanel == null || RenderTarget == null)
                    return;
                    
                // Only render if we have valid range data
                if (previousDayRange <= 0 || double.IsNaN(previousDayRange) || double.IsInfinity(previousDayRange))
                    return;
                    
                // Validate other required values
                if (double.IsNaN(halfRange) || double.IsInfinity(halfRange))
                    return;
                    
                if (double.IsNaN(nr2Level) || double.IsInfinity(nr2Level) || nr2Level <= 0)
                    return;
                    
                if (double.IsNaN(maxCorrected) || double.IsInfinity(maxCorrected))
                    return;
                    
                if (double.IsNaN(minCorrected) || double.IsInfinity(minCorrected))
                    return;

                // Set up rendering properties
                // Table dimensions
                float rowHeight = 20f;
                float columnWidth = 150f;
                float tableWidth = columnWidth * 2;
                float tableHeight = rowHeight * 7; // Header + 6 rows of data
                float margin = 20f;

                // Calculate panel position based on user selection
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

                // Create background rectangle
                SharpDX.RectangleF backgroundRect = new SharpDX.RectangleF(
                    panelPosition.X, 
                    panelPosition.Y, 
                    tableWidth, 
                    tableHeight);

                // Create brushes with fully qualified names to avoid ambiguity
                SharpDX.Direct2D1.SolidColorBrush backgroundBrush = new SharpDX.Direct2D1.SolidColorBrush(
                    RenderTarget, 
                    new SharpDX.Color(0, 0, 0, 0)); // Transparent background

                SharpDX.Direct2D1.SolidColorBrush borderBrush = new SharpDX.Direct2D1.SolidColorBrush(
                    RenderTarget, 
                    new SharpDX.Color(255, 255, 255, 255)); // White border

                SharpDX.Direct2D1.SolidColorBrush textBrush = new SharpDX.Direct2D1.SolidColorBrush(
                    RenderTarget, 
                    new SharpDX.Color(255, 255, 255, 255)); // White text

                // Get font
                NinjaTrader.Gui.Tools.SimpleFont simpleFont = chartControl.Properties.LabelFont ?? 
                    new NinjaTrader.Gui.Tools.SimpleFont("Arial", 10);
                SharpDX.DirectWrite.TextFormat textFormat = simpleFont.ToDirectWriteTextFormat();

                // Draw background
                RenderTarget.FillRectangle(backgroundRect, backgroundBrush);

                // Draw border
                RenderTarget.DrawRectangle(backgroundRect, borderBrush, 1);

                // Draw table rows
                for (int i = 0; i <= 6; i++)
                {
                    SharpDX.Vector2 startPoint = new SharpDX.Vector2(panelPosition.X, panelPosition.Y + (i * rowHeight));
                    SharpDX.Vector2 endPoint = new SharpDX.Vector2(panelPosition.X + tableWidth, panelPosition.Y + (i * rowHeight));
                    RenderTarget.DrawLine(startPoint, endPoint, borderBrush, 1);
                }

                // Draw table columns
                for (int i = 0; i <= 2; i++)
                {
                    SharpDX.Vector2 startPoint = new SharpDX.Vector2(panelPosition.X + (i * columnWidth), panelPosition.Y);
                    SharpDX.Vector2 endPoint = new SharpDX.Vector2(panelPosition.X + (i * columnWidth), panelPosition.Y + tableHeight);
                    RenderTarget.DrawLine(startPoint, endPoint, borderBrush, 1);
                }

                // Draw headers
                DrawText(RenderTarget, "Description", textFormat, textBrush, 
                    new SharpDX.RectangleF(panelPosition.X, panelPosition.Y, columnWidth, rowHeight));
                
                DrawText(RenderTarget, "Value", textFormat, textBrush, 
                    new SharpDX.RectangleF(panelPosition.X + columnWidth, panelPosition.Y, columnWidth, rowHeight));

                // Draw data rows
                string[] descriptions = {
                    "Fecha",
                    "Máximo",
                    "Mínimo",
                    "NR2",
                    "Max ValP",
                    "min ValP"
                };

                string[] values = {
                    currentDate.ToString("d"),
                    FormatPrice(priorDayHigh),
                    FormatPrice(priorDayLow),
                    FormatPrice(nr2Level),
                    FormatPrice(maxCorrected),
                    FormatPrice(minCorrected)
                };

                for (int i = 0; i < descriptions.Length; i++)
                {
                    // Draw description
                    DrawText(RenderTarget, descriptions[i], textFormat, textBrush, 
                        new SharpDX.RectangleF(panelPosition.X, panelPosition.Y + ((i + 1) * rowHeight), columnWidth, rowHeight));

                    // Draw value
                    DrawText(RenderTarget, values[i], textFormat, textBrush, 
                        new SharpDX.RectangleF(panelPosition.X + columnWidth, panelPosition.Y + ((i + 1) * rowHeight), columnWidth, rowHeight));
                }

                // Clean up resources
                backgroundBrush.Dispose();
                borderBrush.Dispose();
                textBrush.Dispose();
                textFormat.Dispose();
            }
            catch (Exception ex)
            {
                // Log error for debugging
                Print($"Error in OnRender: {ex.Message}");
            }
        }

        private void DrawText(RenderTarget renderTarget, string text, TextFormat textFormat, SharpDX.Direct2D1.Brush brush, RectangleF layoutRect)
        {
            try
            {
                using (var textLayout = new TextLayout(NinjaTrader.Core.Globals.DirectWriteFactory, text, textFormat, layoutRect.Width, layoutRect.Height))
                {
                    renderTarget.DrawTextLayout(new SharpDX.Vector2(layoutRect.X, layoutRect.Y), textLayout, brush, DrawTextOptions.NoSnap);
                }
            }
            catch (Exception ex)
            {
                Print($"Error in DrawText: {ex.Message}");
            }
        }

        private string FormatPrice(double price)
        {
            try
            {
                if (double.IsNaN(price) || double.IsInfinity(price))
                    return "N/A";

                // Use instrument formatting if available
                if (Instrument != null && Instrument.MasterInstrument != null)
                    return Instrument.MasterInstrument.FormatPrice(price);
                else
                    return price.ToString("F2");
            }
            catch
            {
                return price.ToString("F2");
            }
        }
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "NR2 Level", Description = "Manual NR2 level. If 0, uses prior day's close.", Order = 1, GroupName = "Parameters")]
        public double UserNR2Level
        {
            get { return userNR2Level; }
            set { userNR2Level = value; }
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> DummyPlot
        {
            get { return Values[0]; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Table Position", Description = "Position of the data table on the chart.", Order = 2, GroupName = "Parameters")]
        public ChartCorner TablePosition { get; set; }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private Anr23[] cacheAnr23;
		public Anr23 Anr23(double userNR2Level, ChartCorner tablePosition)
		{
			return Anr23(Input, userNR2Level, tablePosition);
		}

		public Anr23 Anr23(ISeries<double> input, double userNR2Level, ChartCorner tablePosition)
		{
			if (cacheAnr23 != null)
				for (int idx = 0; idx < cacheAnr23.Length; idx++)
					if (cacheAnr23[idx] != null && cacheAnr23[idx].UserNR2Level == userNR2Level && cacheAnr23[idx].TablePosition == tablePosition && cacheAnr23[idx].EqualsInput(input))
						return cacheAnr23[idx];
			return CacheIndicator<Anr23>(new Anr23(){ UserNR2Level = userNR2Level, TablePosition = tablePosition }, input, ref cacheAnr23);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.Anr23 Anr23(double userNR2Level, ChartCorner tablePosition)
		{
			return indicator.Anr23(Input, userNR2Level, tablePosition);
		}

		public Indicators.Anr23 Anr23(ISeries<double> input , double userNR2Level, ChartCorner tablePosition)
		{
			return indicator.Anr23(input, userNR2Level, tablePosition);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.Anr23 Anr23(double userNR2Level, ChartCorner tablePosition)
		{
			return indicator.Anr23(Input, userNR2Level, tablePosition);
		}

		public Indicators.Anr23 Anr23(ISeries<double> input , double userNR2Level, ChartCorner tablePosition)
		{
			return indicator.Anr23(input, userNR2Level, tablePosition);
		}
	}
}

#endregion
