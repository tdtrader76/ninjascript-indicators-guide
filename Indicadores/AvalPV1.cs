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
    public enum NR2LevelType
{
    PreviousDayClose,
    CurrentDayOpen
}

    public enum GapCalculationMode
{
    Automatic,
    Manual
}

    public class AvalPV1 : Indicator
    {
        #region Variables
        // Input Parameters
        private DateTime selectedDate;
        private double manualPrice;
        private NR2LevelType nr2LevelType;

        // Session management variables for automatic daily updates
        private SessionIterator sessionIterator;
        private DateTime currentDate = Core.Globals.MinDate;
        private double currentDayHigh;
        private double currentDayLow;
        private double currentDayOpen;
        private double priorDayHigh;
        private double priorDayLow;
        private double priorDayOpen;
        private int firstBarOfCurrentDay = -1;
		private int manualStartBar = -1;


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
                Name = name ?? throw new ArgumentNullException(nameof(name));
                LineBrush = brush ?? throw new ArgumentNullException(nameof(brush));
                Value = double.NaN;
            }

            public void Dispose()
            {
                // Liberar recursos de manera segura
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
            switch (State)
            {
                case State.SetDefaults:
                    Description = @"Calculates and displays price levels based on the previous day's range and a manual price.";
                    Name = "AvalPV1";
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
                    Nr2LevelType = NR2LevelType.PreviousDayClose;
                    GapMode = GapCalculationMode.Automatic;
                    ManualGap = 0.0;
                    SelectedDate = DateTime.Today;
                    ManualPrice = 0.0;
                    Width = 1; // Default line width
                    LineBufferPixels = 125; // Default buffer for the line drawing
                    break;

                case State.Configure:
                    // Add the daily data series for prior day's close
                    AddDataSeries(Bars.Instrument.FullName, BarsPeriodType.Day, 1);

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
                    firstBarOfCurrentDay = -1;
					manualStartBar = -1;
                    sessionIterator = null;
                    break;

                case State.DataLoaded:
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
                    break;

                case State.Terminated:
                    // Dispose of DX resources
                    foreach (var brush in dxBrushes.Values)
                        brush?.Dispose();
                    dxBrushes.Clear();

                    foreach (var level in priceLevels.Values)
                        level?.Dispose();
                    break;

                case State.Historical:
                    // Configuration for historical data
                    SetZOrder(-1);
                    
                    if (!Bars.BarsType.IsIntraday)
                    {
                        Draw.TextFixed(this, "NinjaScriptInfo", "AvalPV1 only works on intraday charts", TextPosition.BottomRight);
                        return;
                    }
                    
                    // Calculate the levels once if UseAutomaticDate is false
                    if (!UseAutomaticDate)
					{
                        CalculateLevelsForDate();
						for (int i = 0; i < Bars.Count; i++)
						{
							if (Bars.GetTime(i).Date == SelectedDate.Date)
							{
								manualStartBar = i;
								break;
							}
						}
					}
                    break;

                case State.Realtime:
                    // Enable chart interaction in real-time
                    ChartControl?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        // Future interaction logic can be placed here
                    }));
                    break;
            }
        }

        protected override void OnBarUpdate()
        {
            // Verificación rápida de salida
            if (!UseAutomaticDate)
                return;

            try
            {
                // Verificaciones tempranas para evitar procesamiento innecesario
                if (CurrentBar < 1 || sessionIterator == null) return;
                
                DateTime tradingDay = sessionIterator.GetTradingDay(Time[0]);
                if (tradingDay == DateTime.MinValue) return;

                // Verificar si es un nuevo día o si necesitamos inicializar
                bool isNewDay = currentDate != tradingDay || currentDayOpen == 0;
                
                if (isNewDay)
                {
                    // Guardar valores del día anterior
                    priorDayHigh = currentDayHigh;
                    priorDayLow = currentDayLow;

                    // Verificar que tenemos datos válidos del día anterior
                    if (priorDayHigh > 0 && priorDayLow > 0 && priorDayHigh >= priorDayLow)
                    {
                        double previousDayRange = priorDayHigh - priorDayLow;
                        double priorDayClose = GetPriorDayClose(Time[0], true);

                        // Aplicar cálculo de GAP si está habilitado
                        previousDayRange = ApplyGapCalculation(previousDayRange, priorDayClose, Time[0]);
                        
                        // Obtener precio base para cálculo de niveles
                        double basePrice = ManualPrice;
                        if (basePrice.ApproxCompare(0) == 0)
                        {
                            basePrice = GetBasePriceForNR2(Time[0], priorDayClose);
                        }

                        // Calcular todos los niveles si tenemos un precio base válido
                        if (basePrice > 0)
                        {
                            Print($"DIAGNOSTIC: Using {Nr2LevelType} for NR2 level calculation");
                            CalculateAllLevels(previousDayRange, basePrice);
                            needsLayoutUpdate = true;
                        }
                    }

                    // Inicializar valores para el nuevo día
                    if (Open.IsValidDataPoint(0) && High.IsValidDataPoint(0) && Low.IsValidDataPoint(0))
                    {
                        currentDayOpen = Open[0];
                        currentDayHigh = High[0];
                        currentDayLow = Low[0];
                    }
                    currentDate = tradingDay;
                    firstBarOfCurrentDay = CurrentBar;
                }
                else
                {
                    // Actualizar valores del día actual
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
            // Usar inicialización en bloque para mejorar la eficiencia
            var levelDefinitions = new[]
            {
                new { Name = "Q1", Brush = Brushes.Yellow },
                new { Name = "Q4", Brush = Brushes.Yellow },
                new { Name = "Q2", Brush = Brushes.Plum },
                new { Name = "Q3", Brush = Brushes.Plum },
                new { Name = "Q2/3", Brush = Brushes.ForestGreen },
                new { Name = "Q3/4", Brush = Brushes.IndianRed },
                new { Name = "TC", Brush = Brushes.ForestGreen },
                new { Name = "NR1", Brush = Brushes.BlueViolet },
                new { Name = "NR2", Brush = Brushes.Gold },
                new { Name = "NR3", Brush = Brushes.BlueViolet },
                new { Name = "TV", Brush = Brushes.IndianRed },
                new { Name = "Std1+", Brush = Brushes.ForestGreen },
                new { Name = "Std2+", Brush = Brushes.ForestGreen },
                new { Name = "Std3+", Brush = Brushes.ForestGreen },
                new { Name = "1D+", Brush = Brushes.Gold },
                new { Name = "Std1-", Brush = Brushes.IndianRed },
                new { Name = "Std2-", Brush = Brushes.IndianRed },
                new { Name = "Std3-", Brush = Brushes.IndianRed },
                new { Name = "1D-", Brush = Brushes.Gold }
            };

            // Limpiar y reconstruir el diccionario
            priceLevels.Clear();
            foreach (var def in levelDefinitions)
            {
                priceLevels[def.Name] = new PriceLevel(def.Name, def.Brush);
            }
        }
        
        private double RoundToQuarter(double value)
        {
            // Más eficiente: usar Math.Round con un factor de 4
            return Math.Round(value * 4) / 4;
        }
        
        private void ValidateCalculatedValues()
        {
            // This method is no longer needed as the variables it validated have been removed.
        }

        private double GetPriorDayClose(DateTime time, bool printLog = false)
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
                int priorDayIndex = dailyIndex - 1;
                double priorDayClose = BarsArray[1].GetClose(priorDayIndex);

                if (printLog)
                {
                    DateTime priorDayTime = BarsArray[1].GetTime(priorDayIndex);
                    Print($"DIAGNOSTIC: Using prior day close of {priorDayClose} from daily candle at {priorDayTime} (triggered by bar at {time})");
                }

                return priorDayClose;
            }
            
            // Handle edge cases where the given time is before the second bar of the series
            Print($"DIAGNOSTIC: Could not find a prior day's close for the date {time:d}. The provided time might be too early in the data series.");
            return 0;
        }

        private double GetBasePriceForNR2(DateTime time, double priorDayClose)
        {
            if (Nr2LevelType == NR2LevelType.CurrentDayOpen)
            {
                // Ensure the daily data series is loaded and available
                if (BarsArray[1] != null && BarsArray[1].Count > 0)
                {
                    // Get the index of the daily bar that corresponds to the current intraday bar
                    int dailyIndex = BarsArray[1].GetBar(time);

                    if (dailyIndex >= 0)
                    {
                        // Get the "official" open from the current daily bar
                        double currentDayOpen = BarsArray[1].GetOpen(dailyIndex);
                        Print($"DIAGNOSTIC: Using current day open of {currentDayOpen} for NR2 level");
                        return currentDayOpen;
                    }
                    else
                    {
                        Print("DIAGNOSTIC: Could not get current day open, using prior day close for NR2 level");
                    }
                }
                else
                {
                    Print("DIAGNOSTIC: Daily series not ready, using prior day close for NR2 level");
                }
            }
            
            return priorDayClose;
        }

        private double ApplyGapCalculation(double previousDayRange, double priorDayClose, DateTime time)
        {
            Print($"DIAGNOSTIC: Initial previous day range = {previousDayRange}");

            if (GapMode == GapCalculationMode.Automatic)
            {
                // Ensure the daily data series is loaded and available
                if (BarsArray[1] != null && BarsArray[1].Count > 0)
                {
                    // Get the index of the daily bar that corresponds to the current intraday bar
                    int dailyIndex = BarsArray[1].GetBar(time);

                    if (dailyIndex >= 0)
                    {
                        // Get the "official" open from the current daily bar
                        double currentDailyOpen = BarsArray[1].GetOpen(dailyIndex);
                        Print($"GAP Calc: Using daily open of {currentDailyOpen} from daily candle at {BarsArray[1].GetTime(dailyIndex)} for intraday bar at {time}");

                        if (priorDayClose > 0 && currentDailyOpen != priorDayClose)
                        {
                            double gap = Math.Abs(currentDailyOpen - priorDayClose);
                            Print($"DIAGNOSTIC: Gap calculation = |{currentDailyOpen} (Open) - {priorDayClose} (Close)| = {gap}");

                            double originalRange = previousDayRange;
                            previousDayRange += gap;
                            Print($"DIAGNOSTIC: Modified range = {originalRange} (Initial Range) + {gap} (Gap) = {previousDayRange}");
                        }
                        else if (priorDayClose > 0)
                        {
                            Print($"DIAGNOSTIC: No gap detected - Open: {currentDailyOpen}, Close: {priorDayClose}");
                        }
                    }
                }
            }
            else // Manual mode
            {
                if (ManualGap > 0)
                {
                    double originalRange = previousDayRange;
                    previousDayRange += ManualGap;
                    Print($"DIAGNOSTIC: Manual Gap calculation = {ManualGap}");
                    Print($"DIAGNOSTIC: Modified range = {originalRange} (Initial Range) + {ManualGap} (Manual Gap) = {previousDayRange}");
                }
            }

            return previousDayRange;
        }

        private void CalculateLevelsForDate()
        {
            // Verificaciones tempranas para evitar procesamiento innecesario
            if (Bars == null || Bars.Count == 0)
            {
                Print("DIAGNOSTIC: Bars not loaded, cannot calculate levels.");
                return;
            }

            Print($"DIAGNOSTIC: Starting calculation for selected date: {SelectedDate:d}");
            Print($"DIAGNOSTIC: Chart data series ranges from {Bars.GetTime(0):d} to {Bars.GetTime(Bars.Count - 1):d}");

            // Variables para almacenar los valores del día
            double highForDay = double.MinValue;
            double lowForDay = double.MaxValue;
            bool dateFound = false;
            
            // Usar el iterador de sesión existente si está disponible, si no crear uno nuevo
            SessionIterator sessionIter = sessionIterator ?? new SessionIterator(Bars);

            // Buscar datos para la fecha seleccionada
            for (int i = 0; i < Bars.Count; i++)
            {
                DateTime barTradingDay = sessionIter.GetTradingDay(Bars.GetTime(i));
                if (barTradingDay.Date == SelectedDate.Date)
                {
                    dateFound = true;
                    highForDay = Math.Max(highForDay, Bars.GetHigh(i));
                    lowForDay = Math.Min(lowForDay, Bars.GetLow(i));
                }
            }

            // Verificar si se encontraron datos para la fecha
            if (!dateFound)
            {
                Print($"DIAGNOSTIC: No data found for the selected date: {SelectedDate:d}. Levels will not be drawn.");
                return;
            }

            Print($"DIAGNOSTIC: Data found for {SelectedDate:d}. High: {highForDay}, Low: {lowForDay}");

            // Verificar que los valores sean válidos
            if (highForDay > double.MinValue && lowForDay < double.MaxValue)
            {
                double range = highForDay - lowForDay;
                Print($"DIAGNOSTIC: Calculated range: {range}");
                
                if (range > 0)
                {
                    // Aplicar cálculo de GAP si está habilitado
                    double priorDayClose = GetPriorDayClose(SelectedDate);
                    range = ApplyGapCalculation(range, priorDayClose, SelectedDate);

                    // Obtener precio base para cálculo de niveles
                    double basePrice = ManualPrice;
                    if (basePrice.ApproxCompare(0) == 0)
                    {
                        // Usar el mismo priorDayClose para evitar volver a calcularlo
                        double priorClose = GetPriorDayClose(SelectedDate);
                        basePrice = GetBasePriceForNR2(SelectedDate, priorClose);
                    }

                    // Calcular todos los niveles si tenemos un precio base válido
                    if (basePrice > 0)
                    {
                        Print("DIAGNOSTIC: Range is valid. Calculating all levels.");
                        Print($"DIAGNOSTIC: Using {Nr2LevelType} for NR2 level calculation");
                        CalculateAllLevels(range, basePrice);
                        needsLayoutUpdate = true;
                    }
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

                // Precalcular valores comunes para mejorar la eficiencia
                double halfRange = dayRange / 2.0;
                double q1Level = RoundToQuarter(basePrice + halfRange);
                double q4Level = RoundToQuarter(basePrice - halfRange);
                
                // Precalcular múltiplos comunes del rango para evitar cálculos repetidos
                double range0125 = dayRange * 0.125;
                double range0159 = dayRange * 0.159;
                double range025 = dayRange * 0.25;
                double range0375 = dayRange * 0.375;
                double range050 = dayRange * 0.50;
                
                // Asignar valores a los niveles
                priceLevels["Q1"].Value = q1Level;
                priceLevels["Q4"].Value = q4Level;
                priceLevels["Q2"].Value = RoundToQuarter(q1Level - range025);
                priceLevels["Q3"].Value = RoundToQuarter(q4Level + range025);
                priceLevels["Q2/3"].Value = RoundToQuarter(q1Level - range0375);
                priceLevels["Q3/4"].Value = RoundToQuarter(q4Level + range0375);
                priceLevels["NR2"].Value = RoundToQuarter(basePrice);
                priceLevels["TC"].Value = RoundToQuarter(q1Level - range0125);
                priceLevels["NR1"].Value = RoundToQuarter(q1Level - range0159);
                priceLevels["Std1+"].Value = RoundToQuarter(q1Level + range0125);
                priceLevels["Std2+"].Value = RoundToQuarter(q1Level + range025);
                priceLevels["Std3+"].Value = RoundToQuarter(q1Level + range0375);
                priceLevels["1D+"].Value = RoundToQuarter(q1Level + range050);
                priceLevels["NR3"].Value = RoundToQuarter(q4Level + range0159);
                priceLevels["TV"].Value = RoundToQuarter(q4Level + range0125);
                priceLevels["Std1-"].Value = RoundToQuarter(q4Level - range0125);
                priceLevels["Std2-"].Value = RoundToQuarter(q4Level - range025);
                priceLevels["Std3-"].Value = RoundToQuarter(q4Level - range0375);
                priceLevels["1D-"].Value = RoundToQuarter(q4Level - range050);
                
                // Mostrar los niveles calculados en el output
                Print($"--- NIVELES CALCULADOS ---");
                Print($"Rango del día: {dayRange:F5}");
                Print($"Precio base (NR2): {priceLevels["NR2"].Value:F5}");
                Print($"Q1: {priceLevels["Q1"].Value:F5}");
                Print($"Q2: {priceLevels["Q2"].Value:F5}");
                Print($"Q3: {priceLevels["Q3"].Value:F5}");
                Print($"Q4: {priceLevels["Q4"].Value:F5}");
                Print($"Q2/3: {priceLevels["Q2/3"].Value:F5}");
                Print($"Q3/4: {priceLevels["Q3/4"].Value:F5}");
                Print($"TC: {priceLevels["TC"].Value:F5}");
                Print($"NR1: {priceLevels["NR1"].Value:F5}");
                Print($"NR3: {priceLevels["NR3"].Value:F5}");
                Print($"TV: {priceLevels["TV"].Value:F5}");
                Print($"Std1+: {priceLevels["Std1+"].Value:F5}");
                Print($"Std2+: {priceLevels["Std2+"].Value:F5}");
                Print($"Std3+: {priceLevels["Std3+"].Value:F5}");
                Print($"1D+: {priceLevels["1D+"].Value:F5}");
                Print($"Std1-: {priceLevels["Std1-"].Value:F5}");
                Print($"Std2-: {priceLevels["Std2-"].Value:F5}");
                Print($"Std3-: {priceLevels["Std3-"].Value:F5}");
                Print($"1D-: {priceLevels["1D-"].Value:F5}");
                Print($"------------------------");
            }
            catch (Exception ex)
            {
                Print($"Error in CalculateAllLevels: {ex.Message}");
            }
        }

        private void UpdateTextLayouts()
        {
            // Verificación temprana para evitar procesamiento innecesario
            if (ChartControl == null) return;
            
            // Usar 'using' para asegurar la liberación correcta de recursos
            using (TextFormat textFormat = ChartControl.Properties.LabelFont.ToDirectWriteTextFormat())
            {
                foreach (var level in priceLevels.Values)
                {
                    // Liberar el layout anterior si existe
                    level.LabelLayout?.Dispose();
                    
                    // Saltar niveles sin valor válido
                    if (double.IsNaN(level.Value))
                    {
                        level.LabelLayout = null;
                        continue;
                    }
                    
                    // Crear nueva etiqueta con el valor redondeado
                    string labelText = $"{level.Name} {RoundToQuarter(level.Value):F2}";
                    level.LabelLayout = new TextLayout(Core.Globals.DirectWriteFactory, labelText, textFormat, ChartPanel?.W ?? 0, textFormat.FontSize);
                }
            }
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
            // Verificación rápida de salida para mejorar el rendimiento
            if (priceLevels.Count == 0 || !priceLevels.TryGetValue("Q1", out PriceLevel q1) || double.IsNaN(q1.Value))
                return;

            // Actualizar layouts solo si es necesario
            if (needsLayoutUpdate)
            {
                UpdateTextLayouts();
                needsLayoutUpdate = false;
            }

            // Verificaciones de límites tempranas
            int lastBarIndex = ChartBars?.ToIndex ?? -1;
            if (lastBarIndex < 0) return;

			int startBarIndex = -1;
			if(UseAutomaticDate)
			{
				startBarIndex = firstBarOfCurrentDay;
			}
			else
			{
				startBarIndex = manualStartBar;
			}

			if (startBarIndex < 0)
				return;

            // Para líneas que se extienden dinámicamente, usar desde firstBarOfCurrentDay hasta la última barra + 10 píxeles
            double startBarX = chartControl.GetXByBarIndex(ChartBars, startBarIndex);
            float lineStartX = (float)startBarX;

            // Encontrar la última barra visible y extender 10 píxeles después
            double lastBarX = chartControl.GetXByBarIndex(ChartBars, lastBarIndex);
            float lineEndX = (float)(lastBarX + 10);
            
            // Verificar que tenemos coordenadas válidas
            if (lineEndX <= lineStartX) return;

            // Posición de etiqueta optimizada
            float labelX = lineEndX + 5;
            SharpDX.Direct2D1.Brush labelBrush = GetDxBrush(chartControl.Properties.ChartText);

            // Renderizar niveles con manejo eficiente de recursos
            foreach (var level in priceLevels.Values)
            {
                // Verificaciones rápidas de salida
                if (double.IsNaN(level.Value) || level.LabelLayout == null)
                    continue;

                float y = (float)chartScale.GetYByValue(level.Value);
                
                // Dibujar línea y etiqueta
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
        [Display(Name = "NR2 Level Type", Description = "Select whether NR2 should use the previous day's close or current day's open.", Order = 2, GroupName = "Parameters")]
        public NR2LevelType Nr2LevelType 
        { 
            get { return nr2LevelType; } 
            set { nr2LevelType = value; } 
        }

        [NinjaScriptProperty]
        [Display(Name = "Gap Calculation Mode", Description = "Select whether the gap is calculated automatically or manually.", Order = 3, GroupName = "Parameters")]
        public GapCalculationMode GapMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Manual Gap", Description = "The manual gap value.", Order = 4, GroupName = "Parameters")]
        public double ManualGap { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Selected Date", Description = "Date for calculating levels (only used if 'Use Automatic Date' is false).", Order = 5, GroupName = "Parameters")]
        public DateTime SelectedDate
        {
            get { return selectedDate; }
            set { selectedDate = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Manual Price", Description = "Base price for levels. If 0, uses prior day's close.", Order = 4, GroupName = "Parameters")]
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
		private AvalPV1[] cacheAvalPV1;
		public AvalPV1 AvalPV1(bool useAutomaticDate, NR2LevelType nr2LevelType, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return AvalPV1(Input, useAutomaticDate, nr2LevelType, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public AvalPV1 AvalPV1(ISeries<double> input, bool useAutomaticDate, NR2LevelType nr2LevelType, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			if (cacheAvalPV1 != null)
				for (int idx = 0; idx < cacheAvalPV1.Length; idx++)
					if (cacheAvalPV1[idx] != null && cacheAvalPV1[idx].UseAutomaticDate == useAutomaticDate && cacheAvalPV1[idx].Nr2LevelType == nr2LevelType && cacheAvalPV1[idx].SelectedDate == selectedDate && cacheAvalPV1[idx].ManualPrice == manualPrice && cacheAvalPV1[idx].Width == width && cacheAvalPV1[idx].LineBufferPixels == lineBufferPixels && cacheAvalPV1[idx].EqualsInput(input))
						return cacheAvalPV1[idx];
			return CacheIndicator<AvalPV1>(new AvalPV1(){ UseAutomaticDate = useAutomaticDate, Nr2LevelType = nr2LevelType, SelectedDate = selectedDate, ManualPrice = manualPrice, Width = width, LineBufferPixels = lineBufferPixels }, input, ref cacheAvalPV1);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.AvalPV1 AvalPV1(bool useAutomaticDate, NR2LevelType nr2LevelType, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV1(Input, useAutomaticDate, nr2LevelType, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public Indicators.AvalPV1 AvalPV1(ISeries<double> input , bool useAutomaticDate, NR2LevelType nr2LevelType, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV1(input, useAutomaticDate, nr2LevelType, selectedDate, manualPrice, width, lineBufferPixels);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.AvalPV1 AvalPV1(bool useAutomaticDate, NR2LevelType nr2LevelType, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV1(Input, useAutomaticDate, nr2LevelType, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public Indicators.AvalPV1 AvalPV1(ISeries<double> input , bool useAutomaticDate, NR2LevelType nr2LevelType, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV1(input, useAutomaticDate, nr2LevelType, selectedDate, manualPrice, width, lineBufferPixels);
		}
	}
}

#endregion