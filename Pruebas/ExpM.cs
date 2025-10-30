using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;

public enum ChartCorner2
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public enum DataDisplayMode
{
    TodayOnly,
    AllDays
}

namespace NinjaTrader.NinjaScript.Indicators
{
    public class ExpM : Indicator
    {
        #region Variables
        private List<ExpectedMoveData> historicalData;
        private double bullSum, bearSum;
        private int bullCount, bearCount;
        private double dailyBullAvg, dailyBearAvg;
        private double expHigh, expLow;
        private double currentOpen, priorDayHigh, priorDayLow;
        private double highSuccessRate, lowSuccessRate;
        private DateTime currentDate;
        private SessionIterator sessionIterator;
        private Bars dailyBars;

        // Variables for CSV and historical data
        private string csvFilePath;
        private bool dataLoaded;

        // Render objects
        private SharpDX.Direct2D1.SolidColorBrush backgroundBrush;
        private SharpDX.Direct2D1.SolidColorBrush borderBrush;
        private SharpDX.Direct2D1.SolidColorBrush textBrush;
        private SharpDX.Direct2D1.SolidColorBrush headerBrush;
        private SharpDX.DirectWrite.TextFormat textFormat;
        private SharpDX.DirectWrite.TextFormat headerFormat;
        #endregion

        #region Data Structure
        public class ExpectedMoveData
        {
            public DateTime Date { get; set; }
            public double Open { get; set; }
            public double ExpectedHigh { get; set; }
            public double ExpectedLow { get; set; }
            public double PriorDayHigh { get; set; }
            public double PriorDayLow { get; set; }
            public double HighSuccessRate { get; set; }
            public double LowSuccessRate { get; set; }
            public bool IsBullishDay { get; set; }
            public bool HighTargetHit { get; set; }
            public bool LowTargetHit { get; set; }
        }
        #endregion

        #region OnStateChange
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Expected Move calculator with historical data tracking and table display";
                Name = "ExpM";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                IsSuspendedWhileInactive = true;
                BarsRequiredToPlot = 10;

                Length = 50;
                DisplayMode = DataDisplayMode.TodayOnly;
                UseManualDate = false;
                ManualDate = DateTime.Now;
                TablePosition = ChartCorner2.TopRight;
                SaveToCSV = true;
                CSVFileName = "ExpectedMoveData.csv";

                historicalData = new List<ExpectedMoveData>();
                dataLoaded = false;

                AddPlot(Brushes.Transparent, "ExpectedHigh");
                AddPlot(Brushes.Transparent, "ExpectedLow");
                AddPlot(Brushes.Transparent, "PriorDayHigh");
                AddPlot(Brushes.Transparent, "PriorDayLow");
                AddPlot(Brushes.Transparent, "CurrentOpen");
            }
            else if (State == State.Configure)
            {
                AddDataSeries(Bars.Instrument.FullName, BarsPeriodType.Day, 1);
            }
            else if (State == State.DataLoaded)
            {
                dailyBars = BarsArray[1];
                sessionIterator = new SessionIterator(Bars);
                csvFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8", CSVFileName);
                LoadHistoricalData();
            }
            else if (State == State.Historical)
            {
                if (!Bars.BarsType.IsIntraday)
                {
                    Draw.TextFixed(this, "NinjaScriptInfo", "ExpM only works on intraday charts", TextPosition.BottomRight);
                }
            }
            else if (State == State.Terminated)
            {
                if (SaveToCSV && historicalData.Count > 0)
                    SaveHistoricalData();

                DisposeRenderObjects();
            }
        }
        #endregion

        #region OnBarUpdate
        protected override void OnBarUpdate()
        {
            // Depuración: Imprimir información básica
            if (CurrentBar == 0)
            {
                Print($"=== EXPM INICIADO ===");
                Print($"BarsRequiredToPlot: {BarsRequiredToPlot}");
                Print($"Length: {Length}");
                Print($"DailyBars Count: {dailyBars?.Count ?? 0}");
            }

            if (CurrentBar < BarsRequiredToPlot || dailyBars == null || dailyBars.Count < Length)
            {
                if (CurrentBar % 10 == 0) // Imprimir cada 10 barras para no saturar
                {
                    Print($"CurrentBar: {CurrentBar}, Required: {BarsRequiredToPlot}, DailyBars Count: {dailyBars?.Count ?? 0}, Length: {Length}");
                }
                return;
            }

            // Update current date and calculate for new day
            DateTime tradingDay = sessionIterator.GetTradingDay(Time[0]);
            if (tradingDay != currentDate)
            {
                currentDate = tradingDay;
                Print($"=== NUEVO DÍA: {currentDate:d} ===");
                CalculateExpectedMove();
                CalculateStatistics();
                SaveCurrentDayData();

                // Imprimir resultados del cálculo
                Print($"CÁLCULOS PARA HOY ({currentDate:d}):");
                Print($"  Open: {FormatPrice(currentOpen)}");
                Print($"  Expected High: {FormatPrice(expHigh)}");
                Print($"  Expected Low: {FormatPrice(expLow)}");
                Print($"  Prior Day High: {FormatPrice(priorDayHigh)}");
                Print($"  Prior Day Low: {FormatPrice(priorDayLow)}");
                Print($"  High Success Rate: {highSuccessRate:F1}%");
                Print($"  Low Success Rate: {lowSuccessRate:F1}%");

                // Forzar redibujado
                ForceRefresh();
            }

            // Update plot values
            if (expHigh > 0) ExpectedHigh[0] = expHigh;
            if (expLow > 0) ExpectedLow[0] = expLow;
            if (priorDayHigh > 0) PriorDayHigh[0] = priorDayHigh;
            if (priorDayLow > 0) PriorDayLow[0] = priorDayLow;
            if (currentOpen > 0) CurrentOpen[0] = currentOpen;
        }
        #endregion

        #region Calculation Methods
        private void CalculateExpectedMove()
        {
            Print($"Calculando Expected Move para {currentDate:d}");

            // Get current day's open and prior day's data
            int currentDayIndex = dailyBars.GetBar(currentDate);
            Print($"  CurrentDayIndex: {currentDayIndex}");
            if (currentDayIndex < 1)
            {
                Print("  ERROR: No hay suficiente datos históricos");
                return;
            }

            currentOpen = dailyBars.GetOpen(currentDayIndex);
            priorDayHigh = dailyBars.GetHigh(currentDayIndex - 1);
            priorDayLow = dailyBars.GetLow(currentDayIndex - 1);
            double priorDayOpen = dailyBars.GetOpen(currentDayIndex - 1);

            Print($"  Datos del día anterior ({dailyBars.GetTime(currentDayIndex - 1):d}):");
            Print($"    Open: {FormatPrice(priorDayOpen)}");
            Print($"    High: {FormatPrice(priorDayHigh)}");
            Print($"    Low: {FormatPrice(priorDayLow)}");

            Print($"  Datos de hoy ({currentDate:d}):");
            Print($"    Open: {FormatPrice(currentOpen)}");

            // Calculate historical averages
            bullSum = 0;
            bearSum = 0;
            bullCount = 0;
            bearCount = 0;

            for (int i = 1; i <= Length && currentDayIndex - i >= 0; i++)
            {
                double dayHigh = dailyBars.GetHigh(currentDayIndex - i);
                double dayLow = dailyBars.GetLow(currentDayIndex - i);
                double dayOpen = dailyBars.GetOpen(currentDayIndex - i);
                DateTime dayDate = dailyBars.GetTime(currentDayIndex - i);

                bool isBullishDay = dayHigh - dayOpen > dayOpen - dayLow;

                if (isBullishDay)
                {
                    bullSum += dayHigh - dayOpen;
                    bullCount++;
                    if (i <= 2) // Imprimir los primeros días para depuración
                        Print($"    Día {dayDate:d}: BULLISH (High-Open: {FormatPrice(dayHigh - dayOpen)})");
                }
                else
                {
                    bearSum += dayOpen - dayLow;
                    bearCount++;
                    if (i <= 2) // Imprimir los primeros días para depuración
                        Print($"    Día {dayDate:d}: BEARISH (Open-Low: {FormatPrice(dayOpen - dayLow)})");
                }
            }

            dailyBullAvg = bullCount > 0 ? bullSum / bullCount : 0;
            dailyBearAvg = bearCount > 0 ? bearSum / bearCount : 0;

            Print($"  Promedios históricos de {Length} días:");
            Print($"    Bull Count: {bullCount}, Bull Sum: {FormatPrice(bullSum)}, Bull Avg: {FormatPrice(dailyBullAvg)}");
            Print($"    Bear Count: {bearCount}, Bear Sum: {FormatPrice(bearSum)}, Bear Avg: {FormatPrice(dailyBearAvg)}");

            // Calculate expected move levels
            expHigh = currentOpen + dailyBullAvg;
            expLow = currentOpen - dailyBearAvg;

            Print($"  Expected Move calculado:");
            Print($"    Expected High: {FormatPrice(expHigh)} (Open {FormatPrice(currentOpen)} + {FormatPrice(dailyBullAvg)})");
            Print($"    Expected Low: {FormatPrice(expLow)} (Open {FormatPrice(currentOpen)} - {FormatPrice(dailyBearAvg)})");
        }

        private void CalculateStatistics()
        {
            int totalDays = 0;
            int highSuccessCount = 0;
            int lowSuccessCount = 0;

            for (int i = 0; i < historicalData.Count; i++)
            {
                var data = historicalData[i];

                // Calculate if targets were hit (using actual daily high/low)
                int dailyIndex = dailyBars.GetBar(data.Date);
                if (dailyIndex >= 0)
                {
                    double actualHigh = dailyBars.GetHigh(dailyIndex);
                    double actualLow = dailyBars.GetLow(dailyIndex);

                    data.HighTargetHit = actualHigh >= data.ExpectedHigh;
                    data.LowTargetHit = actualLow <= data.ExpectedLow;
                }

                if (data.HighTargetHit) highSuccessCount++;
                if (data.LowTargetHit) lowSuccessCount++;
                totalDays++;
            }

            // Include current day in statistics
            if (expHigh > 0 && expLow > 0)
            {
                double currentHigh = High[0];
                double currentLow = Low[0];

                if (currentHigh >= expHigh) highSuccessCount++;
                if (currentLow <= expLow) lowSuccessCount++;
                totalDays++;
            }

            highSuccessRate = totalDays > 0 ? (double)highSuccessCount / totalDays * 100 : 0;
            lowSuccessRate = totalDays > 0 ? (double)lowSuccessCount / totalDays * 100 : 0;
        }

        private void SaveCurrentDayData()
        {
            var newData = new ExpectedMoveData
            {
                Date = currentDate,
                Open = currentOpen,
                ExpectedHigh = expHigh,
                ExpectedLow = expLow,
                PriorDayHigh = priorDayHigh,
                PriorDayLow = priorDayLow,
                HighSuccessRate = highSuccessRate,
                LowSuccessRate = lowSuccessRate,
                IsBullishDay = priorDayHigh - priorDayLow > priorDayLow - priorDayLow,
                HighTargetHit = High[0] >= expHigh,
                LowTargetHit = Low[0] <= expLow
            };

            // Check if data for this date already exists and update/replace
            var existingData = historicalData.FirstOrDefault(d => d.Date.Date == currentDate.Date);
            if (existingData != null)
            {
                historicalData.Remove(existingData);
            }

            historicalData.Add(newData);

            // Auto-save to CSV every day
            if (SaveToCSV)
                SaveHistoricalData();
        }
        #endregion

        #region OnRender
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            // Depuración del renderizado
            if (CurrentBar == 0)
            {
                Print("=== ONRENDER INICIADO ===");
                Print($"ChartPanel: {ChartPanel != null}");
                Print($"RenderTarget: {RenderTarget != null}");
                Print($"HistoricalData Count: {historicalData.Count}");
                Print($"CurrentDate: {currentDate:d}");
                Print($"ExpHigh: {FormatPrice(expHigh)}");
                Print($"DisplayMode: {DisplayMode}");
            }

            if (ChartPanel == null || RenderTarget == null)
            {
                if (CurrentBar % 50 == 0) // Imprimir cada 50 barras para no saturar
                    Print($"OnRender: ChartPanel={ChartPanel != null}, RenderTarget={RenderTarget != null}");
                return;
            }

            // Permitir renderizado incluso si no hay historicalData, usar datos actuales
            if (historicalData.Count == 0 && expHigh <= 0)
            {
                if (CurrentBar % 50 == 0)
                    Print("OnRender: No hay datos para mostrar (ni históricos ni actuales)");
                return;
            }

            InitializeRenderObjects(chartControl);

            Print($"OnRender: Dibujando tabla con datos actuales...");

            // Determine what data to show
            List<ExpectedMoveData> dataToShow = new List<ExpectedMoveData>();

            if (DisplayMode == DataDisplayMode.TodayOnly)
            {
                var todayData = historicalData.FirstOrDefault(d => d.Date.Date == currentDate.Date);
                if (todayData != null)
                    dataToShow.Add(todayData);
                else if (expHigh > 0) // Show current day calculation even if not saved yet
                {
                    dataToShow.Add(new ExpectedMoveData
                    {
                        Date = currentDate,
                        Open = currentOpen,
                        ExpectedHigh = expHigh,
                        ExpectedLow = expLow,
                        PriorDayHigh = priorDayHigh,
                        PriorDayLow = priorDayLow,
                        HighSuccessRate = highSuccessRate,
                        LowSuccessRate = lowSuccessRate
                    });
                }
            }
            else if (UseManualDate)
            {
                var manualData = historicalData.FirstOrDefault(d => d.Date.Date == ManualDate.Date);
                if (manualData != null)
                    dataToShow.Add(manualData);
            }
            else
            {
                // Show last 5 days
                int startIndex = Math.Max(0, historicalData.Count - 5);
                for (int i = startIndex; i < historicalData.Count; i++)
                    dataToShow.Add(historicalData[i]);
            }

            if (dataToShow.Count == 0) return;

            DrawDataTable(chartControl, dataToShow);
        }
        #endregion

        #region Rendering Methods
        private void InitializeRenderObjects(ChartControl chartControl)
        {
            if (backgroundBrush == null || backgroundBrush.IsDisposed)
                backgroundBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(0, 0, 0, 200));
            if (borderBrush == null || borderBrush.IsDisposed)
                borderBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255, 255, 255, 255));
            if (textBrush == null || textBrush.IsDisposed)
                textBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255, 255, 255, 255));
            if (headerBrush == null || headerBrush.IsDisposed)
                headerBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color(255, 255, 0, 255));
            if (textFormat == null || textFormat.IsDisposed)
            {
                NinjaTrader.Gui.Tools.SimpleFont simpleFont = chartControl.Properties.LabelFont ?? new NinjaTrader.Gui.Tools.SimpleFont("Arial", 9);
                textFormat = simpleFont.ToDirectWriteTextFormat();
            }
            if (headerFormat == null || headerFormat.IsDisposed)
            {
                NinjaTrader.Gui.Tools.SimpleFont headerFont = new NinjaTrader.Gui.Tools.SimpleFont("Arial", 10);
                headerFormat = headerFont.ToDirectWriteTextFormat();
            }
        }

        private void DrawDataTable(ChartControl chartControl, List<ExpectedMoveData> dataToShow)
        {
            float rowHeight = 18f;
            float columnWidth = 120f;
            float tableWidth = columnWidth * 3;
            int rowCount = 3 + dataToShow.Count * 6; // Headers + 6 rows per day
            float tableHeight = rowHeight * rowCount;
            float margin = 10f;

            SharpDX.Vector2 panelPosition = GetTablePosition(tableWidth, tableHeight, margin);
            SharpDX.RectangleF backgroundRect = new SharpDX.RectangleF(panelPosition.X, panelPosition.Y, tableWidth, tableHeight);

            RenderTarget.DrawRectangle(backgroundRect, borderBrush, 1);

            // Draw grid
            DrawGrid(panelPosition, tableWidth, tableHeight, rowHeight, columnWidth);

            // Draw headers
            string[] headers = { "Description", "Today", "Statistics" };
            for (int i = 0; i < headers.Length; i++)
            {
                DrawText(RenderTarget, headers[i], headerFormat, headerBrush,
                    new SharpDX.RectangleF(panelPosition.X + (i * columnWidth) + 3, panelPosition.Y, columnWidth - 3, rowHeight));
            }

            // Draw data
            int currentRow = 1;
            foreach (var data in dataToShow)
            {
                DrawDayData(panelPosition, columnWidth, rowHeight, currentRow, data);
                currentRow += 6;
            }
        }

        private void DrawDayData(SharpDX.Vector2 panelPosition, float columnWidth, float rowHeight, int startRow, ExpectedMoveData data)
        {
            float textMargin = 3f;
            string[] descriptions = {
                "Date", "Open", "Exp High", "Exp Low",
                "Prior High", "Prior Low"
            };

            string[] todayValues = {
                data.Date.ToString("MM/dd"),
                FormatPrice(data.Open),
                FormatPrice(data.ExpectedHigh),
                FormatPrice(data.ExpectedLow),
                FormatPrice(data.PriorDayHigh),
                FormatPrice(data.PriorDayLow)
            };

            string[] statisticsValues = {
                data.IsBullishDay ? "Bull" : "Bear",
                "",
                data.HighTargetHit ? "✓ Hit" : "✗ Miss",
                data.LowTargetHit ? "✓ Hit" : "✗ Miss",
                $"{highSuccessRate:F1}%",
                $"{lowSuccessRate:F1}%"
            };

            for (int i = 0; i < descriptions.Length; i++)
            {
                float yPos = panelPosition.Y + ((startRow + i) * rowHeight);

                // Description
                DrawText(RenderTarget, descriptions[i], textFormat, textBrush,
                    new SharpDX.RectangleF(panelPosition.X + textMargin, yPos, columnWidth - textMargin, rowHeight));

                // Today values
                DrawText(RenderTarget, todayValues[i], textFormat, textBrush,
                    new SharpDX.RectangleF(panelPosition.X + columnWidth + textMargin, yPos, columnWidth - textMargin, rowHeight));

                // Statistics
                DrawText(RenderTarget, statisticsValues[i], textFormat, textBrush,
                    new SharpDX.RectangleF(panelPosition.X + (2 * columnWidth) + textMargin, yPos, columnWidth - textMargin, rowHeight));
            }
        }

        private SharpDX.Vector2 GetTablePosition(float tableWidth, float tableHeight, float margin)
        {
            switch (TablePosition)
            {
                case ChartCorner2.TopRight:
                    return new SharpDX.Vector2(ChartPanel.X + ChartPanel.W - tableWidth - margin, ChartPanel.Y + margin);
                case ChartCorner2.BottomLeft:
                    return new SharpDX.Vector2(ChartPanel.X + margin, ChartPanel.Y + ChartPanel.H - tableHeight - margin);
                case ChartCorner2.BottomRight:
                    return new SharpDX.Vector2(ChartPanel.X + ChartPanel.W - tableWidth - margin, ChartPanel.Y + ChartPanel.H - tableHeight - margin);
                case ChartCorner2.TopLeft:
                default:
                    return new SharpDX.Vector2(ChartPanel.X + margin, ChartPanel.Y + margin);
            }
        }

        private void DrawGrid(SharpDX.Vector2 panelPosition, float tableWidth, float tableHeight, float rowHeight, float columnWidth)
        {
            // Horizontal lines
            for (int i = 0; i <= tableHeight / rowHeight; i++)
            {
                SharpDX.Vector2 startPoint = new SharpDX.Vector2(panelPosition.X, panelPosition.Y + (i * rowHeight));
                SharpDX.Vector2 endPoint = new SharpDX.Vector2(panelPosition.X + tableWidth, panelPosition.Y + (i * rowHeight));
                RenderTarget.DrawLine(startPoint, endPoint, borderBrush, 1);
            }

            // Vertical lines
            for (int i = 0; i <= 3; i++)
            {
                SharpDX.Vector2 startPoint = new SharpDX.Vector2(panelPosition.X + (i * columnWidth), panelPosition.Y);
                SharpDX.Vector2 endPoint = new SharpDX.Vector2(panelPosition.X + (i * columnWidth), panelPosition.Y + tableHeight);
                RenderTarget.DrawLine(startPoint, endPoint, borderBrush, 1);
            }
        }

        private void DrawText(RenderTarget renderTarget, string text, TextFormat textFormat, SharpDX.Direct2D1.Brush brush, RectangleF layoutRect)
        {
            using (var textLayout = new TextLayout(NinjaTrader.Core.Globals.DirectWriteFactory, text, textFormat, layoutRect.Width, layoutRect.Height))
            {
                renderTarget.DrawTextLayout(new SharpDX.Vector2(layoutRect.X, layoutRect.Y), textLayout, brush, DrawTextOptions.NoSnap);
            }
        }
        #endregion

        #region CSV Methods
        private void LoadHistoricalData()
        {
            if (!File.Exists(csvFilePath) || dataLoaded) return;

            try
            {
                var lines = File.ReadAllLines(csvFilePath);
                foreach (var line in lines.Skip(1)) // Skip header
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 9)
                    {
                        historicalData.Add(new ExpectedMoveData
                        {
                            Date = DateTime.Parse(parts[0]),
                            Open = double.Parse(parts[1]),
                            ExpectedHigh = double.Parse(parts[2]),
                            ExpectedLow = double.Parse(parts[3]),
                            PriorDayHigh = double.Parse(parts[4]),
                            PriorDayLow = double.Parse(parts[5]),
                            HighSuccessRate = double.Parse(parts[6]),
                            LowSuccessRate = double.Parse(parts[7]),
                            IsBullishDay = bool.Parse(parts[8])
                        });
                    }
                }
                dataLoaded = true;
                Print($"Loaded {historicalData.Count} historical records from CSV");
            }
            catch (Exception ex)
            {
                Print($"Error loading CSV: {ex.Message}");
            }
        }

        private void SaveHistoricalData()
        {
            try
            {
                var directory = Path.GetDirectoryName(csvFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (var writer = new StreamWriter(csvFilePath))
                {
                    writer.WriteLine("Date,Open,ExpectedHigh,ExpectedLow,PriorDayHigh,PriorDayLow,HighSuccessRate,LowSuccessRate,IsBullishDay");

                    foreach (var data in historicalData.OrderByDescending(d => d.Date))
                    {
                        writer.WriteLine($"{data.Date:yyyy-MM-dd},{data.Open},{data.ExpectedHigh},{data.ExpectedLow}," +
                                      $"{data.PriorDayHigh},{data.PriorDayLow},{data.HighSuccessRate},{data.LowSuccessRate},{data.IsBullishDay}");
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"Error saving CSV: {ex.Message}");
            }
        }

        private void DisposeRenderObjects()
        {
            if (backgroundBrush != null && !backgroundBrush.IsDisposed) backgroundBrush.Dispose();
            if (borderBrush != null && !borderBrush.IsDisposed) borderBrush.Dispose();
            if (textBrush != null && !textBrush.IsDisposed) textBrush.Dispose();
            if (headerBrush != null && !headerBrush.IsDisposed) headerBrush.Dispose();
            if (textFormat != null && !textFormat.IsDisposed) textFormat.Dispose();
            if (headerFormat != null && !headerFormat.IsDisposed) headerFormat.Dispose();
        }
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Length", Description = "Number of days for historical average calculation", Order = 1, GroupName = "Parameters")]
        [Range(10, int.MaxValue)]
        public int Length { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Display Mode", Description = "Show data for today only or all historical days", Order = 2, GroupName = "Parameters")]
        public DataDisplayMode DisplayMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Manual Date", Description = "Enable to select a specific date to display", Order = 3, GroupName = "Parameters")]
        public bool UseManualDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Manual Date", Description = "Specific date to display when Use Manual Date is enabled", Order = 4, GroupName = "Parameters")]
        public DateTime ManualDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Table Position", Description = "Position of the data table on the chart", Order = 5, GroupName = "Parameters")]
        public ChartCorner2 TablePosition { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Save to CSV", Description = "Save calculated data to CSV file", Order = 6, GroupName = "Parameters")]
        public bool SaveToCSV { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "CSV File Name", Description = "Name of the CSV file to save data", Order = 7, GroupName = "Parameters")]
        public string CSVFileName { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> ExpectedHigh { get { return Values[0]; } }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> ExpectedLow { get { return Values[1]; } }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> PriorDayHigh { get { return Values[2]; } }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> PriorDayLow { get { return Values[3]; } }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> CurrentOpen { get { return Values[4]; } }
        #endregion

        #region Helper Methods
        private string FormatPrice(double price)
        {
            if (double.IsNaN(price) || double.IsInfinity(price)) return "N/A";
            if (Instrument != null && Instrument.MasterInstrument != null)
                return Instrument.MasterInstrument.FormatPrice(price);
            return price.ToString("F2");
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ExpM[] cacheExpM;
		public ExpM ExpM(int length, DataDisplayMode displayMode, bool useManualDate, DateTime manualDate, ChartCorner2 tablePosition, bool saveToCSV, string cSVFileName)
		{
			return ExpM(Input, length, displayMode, useManualDate, manualDate, tablePosition, saveToCSV, cSVFileName);
		}

		public ExpM ExpM(ISeries<double> input, int length, DataDisplayMode displayMode, bool useManualDate, DateTime manualDate, ChartCorner2 tablePosition, bool saveToCSV, string cSVFileName)
		{
			if (cacheExpM != null)
				for (int idx = 0; idx < cacheExpM.Length; idx++)
					if (cacheExpM[idx] != null && cacheExpM[idx].Length == length && cacheExpM[idx].DisplayMode == displayMode && cacheExpM[idx].UseManualDate == useManualDate && cacheExpM[idx].ManualDate == manualDate && cacheExpM[idx].TablePosition == tablePosition && cacheExpM[idx].SaveToCSV == saveToCSV && cacheExpM[idx].CSVFileName == cSVFileName && cacheExpM[idx].EqualsInput(input))
						return cacheExpM[idx];
			return CacheIndicator<ExpM>(new ExpM(){ Length = length, DisplayMode = displayMode, UseManualDate = useManualDate, ManualDate = manualDate, TablePosition = tablePosition, SaveToCSV = saveToCSV, CSVFileName = cSVFileName }, input, ref cacheExpM);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ExpM ExpM(int length, DataDisplayMode displayMode, bool useManualDate, DateTime manualDate, ChartCorner2 tablePosition, bool saveToCSV, string cSVFileName)
		{
			return indicator.ExpM(Input, length, displayMode, useManualDate, manualDate, tablePosition, saveToCSV, cSVFileName);
		}

		public Indicators.ExpM ExpM(ISeries<double> input , int length, DataDisplayMode displayMode, bool useManualDate, DateTime manualDate, ChartCorner2 tablePosition, bool saveToCSV, string cSVFileName)
		{
			return indicator.ExpM(input, length, displayMode, useManualDate, manualDate, tablePosition, saveToCSV, cSVFileName);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ExpM ExpM(int length, DataDisplayMode displayMode, bool useManualDate, DateTime manualDate, ChartCorner2 tablePosition, bool saveToCSV, string cSVFileName)
		{
			return indicator.ExpM(Input, length, displayMode, useManualDate, manualDate, tablePosition, saveToCSV, cSVFileName);
		}

		public Indicators.ExpM ExpM(ISeries<double> input , int length, DataDisplayMode displayMode, bool useManualDate, DateTime manualDate, ChartCorner2 tablePosition, bool saveToCSV, string cSVFileName)
		{
			return indicator.ExpM(input, length, displayMode, useManualDate, manualDate, tablePosition, saveToCSV, cSVFileName);
		}
	}
}

#endregion
