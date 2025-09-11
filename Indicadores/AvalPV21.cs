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

    public class AvalPV21 : Indicator
    {
        #region Variables
        // Input Parameters
        private DateTime selectedDate;
        private double manualPrice;
        private bool useCurrentDayOpen; // Reemplaza a la enumeración

        // Session management variables for automatic daily updates
        private SessionIterator sessionIterator;
        private DateTime currentDate = Core.Globals.MinDate;
        private double currentDayHigh;
        private double currentDayLow;
        private double currentDayOpen;
        private double priorDayHigh;
        private double priorDayLow;
        private double priorDayOpen;

        // Variables para el control de dibujo por día
        private DateTime drawingDay = Core.Globals.MinDate;
        private int firstBarOfDay = -1;
        private int lastBarOfDay = -1;


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
                    Name = "AvalPV21";
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
                    useCurrentDayOpen = false; // false = PreviousDayClose, true = CurrentDayOpen
                    UseGapCalculation = false;
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
                    sessionIterator = null;
                    
                    // Reset drawing day variables
                    drawingDay = Core.Globals.MinDate;
                    firstBarOfDay = -1;
                    lastBarOfDay = -1;
                    
                    // Reset render flags
                    needsLayoutUpdate = false;
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
                        Draw.TextFixed(this, "NinjaScriptInfo", "AvalPV21 only works on intraday charts", TextPosition.BottomRight);
                        return;
                    }
                    
                    // Calculate the levels once if UseAutomaticDate is false
                    if (!UseAutomaticDate)
                        CalculateLevelsForDate();
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
                    Print($"DIAGNOSTIC: New day detected: {tradingDay:d}");
                    
                    // Calcular el rango del día anterior usando la serie diaria
                    double previousDayRange = 0;
                    double priorDayClose = 0;
                    double priorDayHigh = 0;
                    double priorDayLow = 0;
                    
                    // Asegurarse de que la serie diaria está disponible
                    if (BarsArray[1] != null && BarsArray[1].Count > 1)
                    {
                        // Buscar el índice correcto del día anterior
                        int priorDayIndex = -1;
                        for (int i = BarsArray[1].Count - 1; i >= 0; i--)
                        {
                            DateTime dailyBarTime = BarsArray[1].GetTime(i);
                            DateTime dailyTradingDay = dailyBarTime.Date;
                            
                            if (dailyTradingDay < tradingDay)
                            {
                                priorDayIndex = i;
                                Print($"DIAGNOSTIC: Found previous day at index {priorDayIndex} with date {dailyTradingDay:d}");
                                break;
                            }
                        }
                        
                        if (priorDayIndex >= 0)
                        {
                            // Obtener los datos del día anterior de la serie diaria
                            priorDayHigh = BarsArray[1].GetHigh(priorDayIndex);
                            priorDayLow = BarsArray[1].GetLow(priorDayIndex);
                            priorDayClose = BarsArray[1].GetClose(priorDayIndex);
                            
                            previousDayRange = priorDayHigh - priorDayLow;
                            
                            Print($"DIAGNOSTIC: Previous day data - Date: {BarsArray[1].GetTime(priorDayIndex):d}, High: {priorDayHigh}, Low: {priorDayLow}, Close: {priorDayClose}, Range: {previousDayRange}");
                        }
                        else
                        {
                            Print($"DIAGNOSTIC: Could not find previous day data");
                        }
                    }
                    else
                    {
                        Print("DIAGNOSTIC: Daily series not ready or not enough data");
                    }

                    // Variables para el cálculo
                    double gap = 0;
                    double halfGap = 0;
                    double finalRange = previousDayRange;
                    
                    // Aplicar cálculo de GAP si está habilitado y tenemos datos válidos
                    if (UseGapCalculation && previousDayRange > 0 && priorDayClose > 0)
                    {
                        // Ensure the daily data series is loaded and available
                        if (BarsArray[1] != null && BarsArray[1].Count > 0)
                        {
                            int dailyIndex = BarsArray[1].GetBar(Time[0]);
                            Print($"DIAGNOSTIC: Current day index = {dailyIndex}");
                            
                            if (dailyIndex >= 0)
                            {
                                // Get the "official" open from the current daily bar
                                double currentDailyOpen = BarsArray[1].GetOpen(dailyIndex);
                                Print($"GAP Calc: Using daily open of {currentDailyOpen} from daily candle at {BarsArray[1].GetTime(dailyIndex)} for calculation at {Time[0]}");
                                
                                // Mostrar valores antes del cálculo del gap
                                Print($"DIAGNOSTIC: Previous close = {priorDayClose}, Current open = {currentDailyOpen}");
                                
                                // Calcular el gap como valor absoluto para que siempre sea positivo
                                gap = Math.Abs(currentDailyOpen - priorDayClose);
                                halfGap = gap / 2.0;
                                Print($"DIAGNOSTIC: Gap calculation = |{currentDailyOpen} (Open) - {priorDayClose} (Close)| = {gap}");
                                Print($"DIAGNOSTIC: Half gap = {gap} / 2 = {halfGap}");
                                
                                // Sumar la mitad del gap al rango del día anterior
                                finalRange = previousDayRange + halfGap;
                                Print($"DIAGNOSTIC: Final range with gap = {previousDayRange} (Previous Day Range) + {halfGap} (Half Gap) = {finalRange}");
                            }
                            else
                            {
                                Print($"DIAGNOSTIC: Could not get current day index");
                            }
                        }
                        else
                        {
                            Print("DIAGNOSTIC: Daily series not ready for gap calculation");
                        }
                    }
                    
                    // Dividir el rango final entre 2 para calcular los niveles
                    double halfRange = finalRange / 2.0;
                    Print($"DIAGNOSTIC: Half range for level calculations = {finalRange} / 2 = {halfRange}");
                    
                    // Obtener precio base para cálculo de niveles
                    double basePrice = ManualPrice;
                    if (basePrice.ApproxCompare(0) == 0)
                    {
                        basePrice = GetBasePriceForNR2(Time[0], priorDayClose);
                    }

                    // Calcular todos los niveles si tenemos un precio base válido y halfRange válido
                    if (basePrice > 0 && halfRange > 0)
                    {
                        Print($"DIAGNOSTIC: Using CurrentDayOpen = {useCurrentDayOpen} for NR2 level calculation");
                        Print($"DIAGNOSTIC: Final calculation parameters - Previous Day Range: {previousDayRange}, Gap: {gap}, Half Gap: {halfGap}, Final Range: {finalRange}, Half Range: {halfRange}, Base Price: {basePrice}");
                        CalculateAllLevels(halfRange, basePrice);
                        needsLayoutUpdate = true;
                        
                        // Calcular los límites del día para dibujo
                        CalculateDayBoundaries(tradingDay);
                    }
                    else
                    {
                        Print($"DIAGNOSTIC: Cannot calculate levels - BasePrice: {basePrice}, Half Range: {halfRange}");
                    }

                    // Inicializar valores para el nuevo día (para el seguimiento intradía)
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
                    // Actualizar valores del día actual (para el seguimiento intradía)
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
                Print($"Error stack trace: {ex.StackTrace}");
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
            if (useCurrentDayOpen) // Reemplaza a la enumeración
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
            
            // Ensure the daily data series is loaded and available
            if (BarsArray[1] != null && BarsArray[1].Count > 0)
            {
                // Get the index of the daily bar that corresponds to the current intraday bar
                int dailyIndex = BarsArray[1].GetBar(time);

                if (dailyIndex >= 0)
                {
                    // Get the "official" open from the current daily bar
                    double currentDailyOpen = BarsArray[1].GetOpen(dailyIndex);
                    Print($"GAP Calc: Using daily open of {currentDailyOpen} from daily candle at {BarsArray[1].GetTime(dailyIndex)} for calculation at {time}");
                    
                    if (priorDayClose > 0 && currentDailyOpen > priorDayClose)
                    {
                        double gap = currentDailyOpen - priorDayClose;
                        Print($"DIAGNOSTIC: Gap calculation = {currentDailyOpen} (Open) - {priorDayClose} (Close) = {gap}");
                        
                        double originalRange = previousDayRange;
                        previousDayRange += (gap / 2.0); // Agregar la mitad del gap
                        Print($"DIAGNOSTIC: Modified range = {originalRange} (Initial Range) + {gap / 2.0} (Half Gap) = {previousDayRange}");
                    }
                }
            }
            
            return previousDayRange;
        }

        private void CalculateDayBoundaries(DateTime targetDate)
        {
            // Si ya hemos calculado los límites para este día, no recalcular
            if (drawingDay == targetDate && firstBarOfDay >= 0 && lastBarOfDay >= 0)
            {
                //Print($"DIAGNOSTIC: Using cached day boundaries for {targetDate:d}");
                return;
            }

            // Actualizar el día de dibujo
            drawingDay = targetDate;
            firstBarOfDay = -1;
            lastBarOfDay = -1;

            Print($"DIAGNOSTIC: Calculating day boundaries for {targetDate:d}");

            // Verificar que tenemos datos
            if (Bars == null || Bars.Count == 0)
            {
                Print("DIAGNOSTIC: No bars data available");
                return;
            }

            // Crear un iterador de sesión si no tenemos uno
            SessionIterator sessionIter = sessionIterator ?? new SessionIterator(Bars);

            // Buscar el primer y último bar del día objetivo
            for (int i = 0; i < Bars.Count; i++)
            {
                DateTime barTradingDay = sessionIter.GetTradingDay(Bars.GetTime(i));
                
                // Verificar si este bar pertenece al día objetivo
                if (barTradingDay.Date == targetDate.Date)
                {
                    // Si es el primer bar que encontramos, registrar su índice
                    if (firstBarOfDay == -1)
                    {
                        firstBarOfDay = i;
                        Print($"DIAGNOSTIC: First bar of day found at index {i}");
                    }
                    
                    // Actualizar el último bar encontrado
                    lastBarOfDay = i;
                }
            }

            // Si no encontramos bares para el día objetivo
            if (firstBarOfDay == -1)
            {
                Print($"DIAGNOSTIC: No bars found for day {targetDate:d}, using full range");
                firstBarOfDay = 0;
                lastBarOfDay = Bars.Count - 1;
            }
            else
            {
                Print($"DIAGNOSTIC: Day boundaries calculated - First: {firstBarOfDay}, Last: {lastBarOfDay}");
            }
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
            
            // Verificar que la serie diaria está disponible
            if (BarsArray[1] == null || BarsArray[1].Count < 2)
            {
                Print("DIAGNOSTIC: Daily series not ready, cannot calculate levels.");
                return;
            }

            // Buscar el día anterior al seleccionado en la serie diaria
            Print($"DIAGNOSTIC: Looking for previous day to {SelectedDate:d}");
            int priorDayIndex = -1;
            for (int i = BarsArray[1].Count - 1; i >= 0; i--)
            {
                DateTime dailyBarTime = BarsArray[1].GetTime(i);
                DateTime dailyTradingDay = dailyBarTime.Date;
                
                if (dailyTradingDay < SelectedDate.Date)
                {
                    priorDayIndex = i;
                    Print($"DIAGNOSTIC: Found previous day at index {priorDayIndex} with date {dailyTradingDay:d}");
                    break;
                }
            }

            if (priorDayIndex >= 0)
            {
                // Obtener datos del día anterior de la serie diaria
                double priorDayHigh = BarsArray[1].GetHigh(priorDayIndex);
                double priorDayLow = BarsArray[1].GetLow(priorDayIndex);
                double priorDayClose = BarsArray[1].GetClose(priorDayIndex);
                double previousDayRange = priorDayHigh - priorDayLow;
                
                Print($"DIAGNOSTIC: Using daily data for previous day - High: {priorDayHigh}, Low: {priorDayLow}, Close: {priorDayClose}, Range: {previousDayRange}");

                // Variables para el cálculo
                double gap = 0;
                double halfGap = 0;
                double finalRange = previousDayRange;
                
                // Aplicar cálculo de GAP si está habilitado
                if (UseGapCalculation && previousDayRange > 0 && priorDayClose > 0)
                {
                    // Buscar el índice del día seleccionado
                    int selectedIndex = BarsArray[1].GetBar(SelectedDate);
                    if (selectedIndex >= 0)
                    {
                        // Get the "official" open from the selected daily bar
                        double currentDailyOpen = BarsArray[1].GetOpen(selectedIndex);
                        Print($"GAP Calc: Using daily open of {currentDailyOpen} from daily candle at {BarsArray[1].GetTime(selectedIndex)} for calculation at {SelectedDate}");
                        
                        // Calcular el gap como valor absoluto para que siempre sea positivo
                        gap = Math.Abs(currentDailyOpen - priorDayClose);
                        halfGap = gap / 2.0;
                        Print($"DIAGNOSTIC: Gap calculation = |{currentDailyOpen} (Open) - {priorDayClose} (Close)| = {gap}");
                        Print($"DIAGNOSTIC: Half gap = {gap} / 2 = {halfGap}");
                        
                        // Sumar la mitad del gap al rango del día anterior
                        finalRange = previousDayRange + halfGap;
                        Print($"DIAGNOSTIC: Final range with gap = {previousDayRange} (Previous Day Range) + {halfGap} (Half Gap) = {finalRange}");
                    }
                }
                
                // Dividir el rango final entre 2 para calcular los niveles
                double halfRange = finalRange / 2.0;
                Print($"DIAGNOSTIC: Half range for level calculations = {finalRange} / 2 = {halfRange}");

                // Verificar que el halfRange sea válido
                if (halfRange > 0)
                {
                    // Obtener precio base para cálculo de niveles
                    double basePrice = ManualPrice;
                    if (basePrice.ApproxCompare(0) == 0)
                    {
                        basePrice = GetBasePriceForNR2(SelectedDate, priorDayClose);
                    }

                    // Calcular todos los niveles si tenemos un precio base válido
                    if (basePrice > 0)
                    {
                        Print("DIAGNOSTIC: Range is valid. Calculating all levels.");
                        Print($"DIAGNOSTIC: Using CurrentDayOpen = {useCurrentDayOpen} for NR2 level calculation");
                        Print($"DIAGNOSTIC: Final calculation parameters - Previous Day Range: {previousDayRange}, Gap: {gap}, Half Gap: {halfGap}, Final Range: {finalRange}, Half Range: {halfRange}, Base Price: {basePrice}");
                        CalculateAllLevels(halfRange, basePrice);
                        needsLayoutUpdate = true;
                        
                        // Calcular los límites del día para dibujo
                        CalculateDayBoundaries(SelectedDate);
                    }
                }
                else
                {
                    Print("DIAGNOSTIC: Calculated half range is zero or negative. Levels will not be drawn.");
                }
            }
            else
            {
                Print($"DIAGNOSTIC: Could not find data for previous day to {SelectedDate:d}");
            }
        }

        private void CalculateAllLevels(double halfRange, double basePrice)
        {
            try
            {
                if (basePrice <= 0 || halfRange <= 0) return;

                // Mostrar los parámetros de cálculo
                Print($"--- NIVELES CALCULADOS ---");
                Print($"Half Range (parámetro): {halfRange:F5}");
                Print($"Base Price (NR2): {basePrice:F5}");
                
                // Calcular niveles usando halfRange
                double q1Level = RoundToQuarter(basePrice + halfRange);
                double q4Level = RoundToQuarter(basePrice - halfRange);
                
                priceLevels["NR2"].Value = RoundToQuarter(basePrice);
                priceLevels["Q1"].Value = q1Level;
                priceLevels["Q4"].Value = q4Level;
                priceLevels["Q2"].Value = RoundToQuarter(q1Level - (halfRange * 0.5));
                priceLevels["Q3"].Value = RoundToQuarter(q4Level + (halfRange * 0.5));
                priceLevels["Q2/3"].Value = RoundToQuarter(q1Level - (halfRange * 0.75));
                priceLevels["Q3/4"].Value = RoundToQuarter(q4Level + (halfRange * 0.75));
                priceLevels["TC"].Value = RoundToQuarter(q1Level - (halfRange * 0.25));
                priceLevels["NR1"].Value = RoundToQuarter(q1Level - (halfRange * 0.318));
                priceLevels["Std1+"].Value = RoundToQuarter(q1Level + (halfRange * 0.25));
                priceLevels["Std2+"].Value = RoundToQuarter(q1Level + (halfRange * 0.5));
                priceLevels["Std3+"].Value = RoundToQuarter(q1Level + (halfRange * 0.75));
                priceLevels["1D+"].Value = RoundToQuarter(q1Level + halfRange);
                priceLevels["NR3"].Value = RoundToQuarter(q4Level + (halfRange * 0.318));
                priceLevels["TV"].Value = RoundToQuarter(q4Level + (halfRange * 0.25));
                priceLevels["Std1-"].Value = RoundToQuarter(q4Level - (halfRange * 0.25));
                priceLevels["Std2-"].Value = RoundToQuarter(q4Level - (halfRange * 0.5));
                priceLevels["Std3-"].Value = RoundToQuarter(q4Level - (halfRange * 0.75));
                priceLevels["1D-"].Value = RoundToQuarter(q4Level - halfRange);
                
                // Mostrar todos los niveles calculados
                Print($"NR2 (Base Price): {priceLevels["NR2"].Value:F5}");
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
            if (ChartControl == null) 
            {
                //Print("DIAGNOSTIC: ChartControl is null, skipping text layout update");
                return;
            }
            
            //Print("DIAGNOSTIC: Updating text layouts");
            
            // Usar 'using' para asegurar la liberación correcta de recursos
            using (TextFormat textFormat = ChartControl.Properties.LabelFont.ToDirectWriteTextFormat())
            {
                int updatedLayouts = 0;
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
                    updatedLayouts++;
                }
                //Print($"DIAGNOSTIC: Updated {updatedLayouts} text layouts");
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
            {
                return;
            }

            // Actualizar layouts solo si es necesario
            if (needsLayoutUpdate)
            {
                UpdateTextLayouts();
                needsLayoutUpdate = false;
            }

            // Verificaciones de límites tempranas
            int lastBarIndex = ChartBars?.ToIndex ?? -1;
            if (lastBarIndex < 0) 
            {
                return;
            }

            // Siempre renderizar los niveles calculados, independientemente del día
            // Esto debería resolver el problema de las líneas que desaparecen
            
            // Calcular las posiciones X para las líneas basadas en el área visible completa
            int visibleFirstBar = ChartBars.FromIndex;
            int visibleLastBar = ChartBars.ToIndex;
            
            // Asegurarse de que los índices están dentro de los límites válidos
            visibleFirstBar = Math.Max(visibleFirstBar, 0);
            visibleLastBar = Math.Min(visibleLastBar, Bars?.Count - 1 ?? visibleLastBar);

            // Calcular las posiciones X para las líneas
            float lineStartX = (float)chartControl.GetXByBarIndex(ChartBars, visibleFirstBar);
            float lineEndX = (float)chartControl.GetXByBarIndex(ChartBars, visibleLastBar);
            
            // Calcular la posición X para las etiquetas (ligeramente a la izquierda del área visible)
            float labelX = lineEndX + 10; // Cambiado de lineStartX a lineEndX como solicitaste
            
            // Asegurar que las etiquetas no se salgan del área visible
            float panelWidth = ChartPanel?.W ?? 0;
            labelX = Math.Min(labelX, panelWidth - 100); // Dejar espacio para el texto
            
            // Renderizar niveles con manejo eficiente de recursos
            SharpDX.Direct2D1.Brush labelBrush = GetDxBrush(chartControl.Properties.ChartText);

            // Renderizar niveles con manejo eficiente de recursos
            foreach (var level in priceLevels.Values)
            {
                // Verificaciones rápidas de salida
                if (double.IsNaN(level.Value) || level.LabelLayout == null)
                    continue;

                float y = (float)chartScale.GetYByValue(level.Value);
                
                // Verificar que la coordenada Y esté dentro de los límites razonables
                if (y < -10000 || y > (ChartPanel?.H + 10000 ?? float.MaxValue))
                    continue;
                
                // Dibujar línea horizontal que se extiende a través del área visible
                RenderTarget.DrawLine(
                    new SharpDX.Vector2(lineStartX, y),
                    new SharpDX.Vector2(lineEndX, y),
                    GetDxBrush(level.LineBrush),
                    Width
                );
                
                // Dibujar etiqueta en la posición visible
                Point textPoint = new Point(labelX, y - (level.LabelLayout.Metrics.Height / 15));
                RenderTarget.DrawTextLayout(textPoint.ToVector2(), level.LabelLayout, labelBrush);
            }
        }
        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Use Automatic Date", Description = "If true, calculates range from the prior day automatically. If false, uses the 'Selected Date' below.", Order = 1, GroupName = "Parameters")]
        public bool UseAutomaticDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "NR2 Level Type", Description = "False = Previous Day Close, True = Current Day Open", Order = 2, GroupName = "Parameters")]
        public bool UseCurrentDayOpen 
        { 
            get { return useCurrentDayOpen; } 
            set { useCurrentDayOpen = value; } 
        }

        [NinjaScriptProperty]
        [Display(Name = "GAP", Description = "If true, adds half of the opening gap-up to the previous day's range.", Order = 3, GroupName = "Parameters")]
        public bool UseGapCalculation { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Selected Date", Description = "Date for calculating levels (only used if 'Use Automatic Date' is false).", Order = 3, GroupName = "Parameters")]
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
		private AvalPV21[] cacheAvalPV21;
		public AvalPV21 AvalPV21(bool useAutomaticDate, bool useCurrentDayOpen, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return AvalPV21(Input, useAutomaticDate, useCurrentDayOpen, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public AvalPV21 AvalPV21(ISeries<double> input, bool useAutomaticDate, bool useCurrentDayOpen, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			if (cacheAvalPV21 != null)
				for (int idx = 0; idx < cacheAvalPV21.Length; idx++)
					if (cacheAvalPV21[idx] != null && cacheAvalPV21[idx].UseAutomaticDate == useAutomaticDate && cacheAvalPV21[idx].UseCurrentDayOpen == useCurrentDayOpen && cacheAvalPV21[idx].UseGapCalculation == useGapCalculation && cacheAvalPV21[idx].SelectedDate == selectedDate && cacheAvalPV21[idx].ManualPrice == manualPrice && cacheAvalPV21[idx].Width == width && cacheAvalPV21[idx].LineBufferPixels == lineBufferPixels && cacheAvalPV21[idx].EqualsInput(input))
						return cacheAvalPV21[idx];
			return CacheIndicator<AvalPV21>(new AvalPV21(){ UseAutomaticDate = useAutomaticDate, UseCurrentDayOpen = useCurrentDayOpen, UseGapCalculation = useGapCalculation, SelectedDate = selectedDate, ManualPrice = manualPrice, Width = width, LineBufferPixels = lineBufferPixels }, input, ref cacheAvalPV21);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.AvalPV21 AvalPV21(bool useAutomaticDate, bool useCurrentDayOpen, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV21(Input, useAutomaticDate, useCurrentDayOpen, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public Indicators.AvalPV21 AvalPV21(ISeries<double> input , bool useAutomaticDate, bool useCurrentDayOpen, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV21(input, useAutomaticDate, useCurrentDayOpen, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.AvalPV21 AvalPV21(bool useAutomaticDate, bool useCurrentDayOpen, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV21(Input, useAutomaticDate, useCurrentDayOpen, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public Indicators.AvalPV21 AvalPV21(ISeries<double> input , bool useAutomaticDate, bool useCurrentDayOpen, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV21(input, useAutomaticDate, useCurrentDayOpen, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}
	}
}

#endregion
