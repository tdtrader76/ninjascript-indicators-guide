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

namespace NinjaTrader.NinjaScript.Indicators
{
    /// <summary>
    /// Indicator that displays a table with NR2 levels and price range information
    /// </summary>
    public class NR2LevelsTable : Indicator
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
                Name = "NR2LevelsTable";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                IsSuspendedWhileInactive = true;
                BarsRequiredToPlot = 2; // Need at least 2 bars to calculate previous day range

                AddPlot(Brushes.Transparent, "DummyPlot");
            }
            else if (State == State.Configure)
            {
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
                    Draw.TextFixed(this, "NinjaScriptInfo", "NR2LevelsTable only works on intraday charts", TextPosition.BottomRight);
                    Log("NR2LevelsTable only works on intraday charts", LogLevel.Error);
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
                
                // Validate NR2 level is set
                if (nr2Level <= 0 || double.IsNaN(nr2Level) || double.IsInfinity(nr2Level))
                {
                    // Use current price as default if not set
                    if (Close.IsValidDataPoint(0))
                        nr2Level = Close[0];
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
                SharpDX.Vector2 panelPosition = new SharpDX.Vector2(ChartPanel.X + 20, ChartPanel.Y + 20);
                
                // Table dimensions
                float rowHeight = 20f;
                float columnWidth = 150f;
                float tableWidth = columnWidth * 2;
                float tableHeight = rowHeight * 6; // Header + 5 rows of data

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
                for (int i = 0; i <= 5; i++)
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
                    "Prev Day Range", 
                    "Half Range", 
                    "NR2 Level", 
                    "Max Corrected", 
                    "Min Corrected" 
                };

                double[] values = { 
                    previousDayRange, 
                    halfRange, 
                    nr2Level, 
                    maxCorrected, 
                    minCorrected 
                };

                for (int i = 0; i < descriptions.Length; i++)
                {
                    // Draw description
                    DrawText(RenderTarget, descriptions[i], textFormat, textBrush, 
                        new SharpDX.RectangleF(panelPosition.X, panelPosition.Y + ((i + 1) * rowHeight), columnWidth, rowHeight));

                    // Draw value
                    string formattedValue = FormatPrice(values[i]);
                    DrawText(RenderTarget, formattedValue, textFormat, textBrush, 
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
        [Display(Name = "NR2 Level", Description = "Manual input for NR2 level", Order = 1, GroupName = "Parameters")]
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
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private NR2LevelsTable[] cacheNR2LevelsTable;
        
        public NR2LevelsTable NR2LevelsTable(double userNR2Level)
        {
            return NR2LevelsTable(Input, userNR2Level);
        }
        
        public NR2LevelsTable NR2LevelsTable(ISeries<double> input, double userNR2Level)
        {
            if (cacheNR2LevelsTable != null)
                for (int idx = 0; idx < cacheNR2LevelsTable.Length; idx++)
                    if (cacheNR2LevelsTable[idx] != null && cacheNR2LevelsTable[idx].UserNR2Level == userNR2Level && cacheNR2LevelsTable[idx].EqualsInput(input))
                        return cacheNR2LevelsTable[idx];
                        
            return CacheIndicator<NR2LevelsTable>(new NR2LevelsTable(){ UserNR2Level = userNR2Level }, input, ref cacheNR2LevelsTable);
        }
    }
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.NR2LevelsTable NR2LevelsTable(double userNR2Level)
        {
            return indicator.NR2LevelsTable(Input, userNR2Level);
        }
        
        public Indicators.NR2LevelsTable NR2LevelsTable(ISeries<double> input , double userNR2Level)
        {
            return indicator.NR2LevelsTable(input, userNR2Level);
        }
    }
}

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.NR2LevelsTable NR2LevelsTable(double userNR2Level)
        {
            return indicator.NR2LevelsTable(Input, userNR2Level);
        }
        
        public Indicators.NR2LevelsTable NR2LevelsTable(ISeries<double> input , double userNR2Level)
        {
            return indicator.NR2LevelsTable(input, userNR2Level);
        }
    }
}

#endregion