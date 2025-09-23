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
    public class AvalPV4 : Indicator
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
        		private int manualEndBar = -1;

        // Dictionaries to hold price levels for current and previous day
        private Dictionary<string, PriceLevel> currentPriceLevels = new Dictionary<string, PriceLevel>();
        private Dictionary<string, PriceLevel> previousPriceLevels = new Dictionary<string, PriceLevel>();

		// Bar indices for drawing previous day's lines
		private int previousDayStartBar = -1;
		private int previousDayEndBar = -1;

        // Represents a calculated price level
        private class PriceLevel : IDisposable
        {
            public string Name { get; }
            public System.Windows.Media.Brush LineBrush { get; }
            public double Value { get; set; }
            public TextLayout LabelLayout { get; set; }
            public string Modifier { get; set; }

            public PriceLevel(string name, System.Windows.Media.Brush brush, string modifier = "")
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
                LineBrush = brush ?? throw new ArgumentNullException(nameof(brush));
                Value = double.NaN;
                Modifier = modifier;
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
                    Name = "AvalPV4";
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
                    UseGapCalculation = false;
                    SelectedDate = DateTime.Today;
                    ManualPrice = 0.0;
                    Width = 1; // Default line width
                    LineBufferPixels = 125; // Default buffer for the line drawing

                    AddPlot(Brushes.Transparent, "Q1");
                    AddPlot(Brushes.Transparent, "Q4");
                    AddPlot(Brushes.Transparent, "Q2");
                    AddPlot(Brushes.Transparent, "Q3");
                    AddPlot(Brushes.Transparent, "Q2_3");
                    AddPlot(Brushes.Transparent, "Q3_4");
                    AddPlot(Brushes.Transparent, "TC");
                    AddPlot(Brushes.Transparent, "NR1");
                    AddPlot(Brushes.Transparent, "NR2");
                    AddPlot(Brushes.Transparent, "NR3");
                    AddPlot(Brushes.Transparent, "TV");
                    AddPlot(Brushes.Transparent, "Std1Plus");
                    AddPlot(Brushes.Transparent, "Std2Plus");
                    AddPlot(Brushes.Transparent, "Std3Plus");
                    AddPlot(Brushes.Transparent, "OneDPlus");
                    AddPlot(Brushes.Transparent, "Std1Minus");
                    AddPlot(Brushes.Transparent, "Std2Minus");
                    AddPlot(Brushes.Transparent, "Std3Minus");
                    AddPlot(Brushes.Transparent, "OneDMinus");
                    break;

                case State.Configure:
                    // Add the daily data series for prior day's close
                    AddDataSeries(Bars.Instrument.FullName, BarsPeriodType.Day, 1);

                    // Initialize the price levels
                    InitializePriceLevels(currentPriceLevels);
                	InitializePriceLevels(previousPriceLevels);

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
                                        manualEndBar = -1;                    sessionIterator = null;
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

                    foreach (var level in currentPriceLevels.Values)
                        level?.Dispose();
					foreach (var level in previousPriceLevels.Values)
                        level?.Dispose();
                    break;

                case State.Historical:
                    // Configuration for historical data
                    SetZOrder(-1);
                    
                    if (!Bars.BarsType.IsIntraday)
                    {
                        Draw.TextFixed(this, "NinjaScriptInfo", "AvalPV4 only works on intraday charts", TextPosition.BottomRight);
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
								if (manualStartBar == -1)
									manualStartBar = i;

								if (Bars.GetTime(i).Hour <= 22)
								{
									manualEndBar = i;
								}
							}
							else if (manualStartBar != -1)
							{
								// Una vez que hemos pasado el día seleccionado, podemos detenernos.
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
                    // Archive drawing range for the day that just ended.
					previousDayStartBar = firstBarOfCurrentDay;
					previousDayEndBar = CurrentBar - 1;

					// Swap dictionaries to archive the old levels
					var tempLevels = previousPriceLevels;
					previousPriceLevels = currentPriceLevels;
					currentPriceLevels = tempLevels;
					InitializePriceLevels(currentPriceLevels); // Re-initialize the now-current dictionary

                    // Guardar valores del día anterior
                    priorDayHigh = currentDayHigh;
                    priorDayLow = currentDayLow;

                    // Verificar que tenemos datos válidos del día anterior
                    if (priorDayHigh > 0 && priorDayLow > 0 && priorDayHigh >= priorDayLow)
                    {
                        double previousDayRange = priorDayHigh - priorDayLow;
                        double priorDayClose = GetPriorDayClose(Time[0], true);

                        // Aplicar cálculo de GAP si está habilitado
                        if (UseGapCalculation)
                        {
                            previousDayRange = ApplyGapCalculation(previousDayRange, priorDayClose, Time[0]);
                        }
                        
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
                            CalculateAllLevels(previousDayRange, basePrice, currentPriceLevels);
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

        private void InitializePriceLevels(Dictionary<string, PriceLevel> levels)
        {
            // Usar inicialización en bloque para mejorar la eficiencia
            var levelDefinitions = new[]
            {
                new { Name = "Q1", Brush = Brushes.Yellow, Modifier = "0.5" },
                new { Name = "Q4", Brush = Brushes.Yellow, Modifier = "0.5" },
                new { Name = "Q2", Brush = Brushes.Plum, Modifier = "0.25" },
                new { Name = "Q3", Brush = Brushes.Plum, Modifier = "0.25" },
                new { Name = "Q2/3", Brush = Brushes.ForestGreen, Modifier = "0.375" },
                new { Name = "Q3/4", Brush = Brushes.IndianRed, Modifier = "0.375" },
                new { Name = "TC", Brush = Brushes.ForestGreen, Modifier = "0.0855" },
                new { Name = "NR1", Brush = Brushes.BlueViolet, Modifier = "0.171" },
                new { Name = "NR2", Brush = Brushes.Gold, Modifier = "Base" },
                new { Name = "NR3", Brush = Brushes.BlueViolet, Modifier = "0.159" },
                new { Name = "TV", Brush = Brushes.IndianRed, Modifier = "0.125" },
                new { Name = "Std1+", Brush = Brushes.ForestGreen, Modifier = "0.0855" },
                new { Name = "Std2+", Brush = Brushes.ForestGreen, Modifier = "0.171" },
                new { Name = "Std3+", Brush = Brushes.ForestGreen, Modifier = "0.342" },
                new { Name = "1D+", Brush = Brushes.Gold, Modifier = "0.50" },
                new { Name = "Std1-", Brush = Brushes.IndianRed, Modifier = "0.0855" },
                new { Name = "Std2-", Brush = Brushes.IndianRed, Modifier = "0.171" },
                new { Name = "Std3-", Brush = Brushes.IndianRed, Modifier = "0.342" },
                new { Name = "1D-", Brush = Brushes.Gold, Modifier = "0.50" }
            };

            // Limpiar y reconstruir el diccionario
            levels.Clear();
            foreach (var def in levelDefinitions)
            {
                levels[def.Name] = new PriceLevel(def.Name, def.Brush, def.Modifier);
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
                        previousDayRange += (gap);
                        Print($"DIAGNOSTIC: Modified range = {originalRange} (Initial Range) + {gap} (Half Gap) = {previousDayRange}");
                    }
                    else if (priorDayClose > 0)
                    {
                        Print($"DIAGNOSTIC: No gap detected - Open: {currentDailyOpen}, Close: {priorDayClose}");
                    }
                }
            }

            return previousDayRange;
        }

        private void CalculateLevelsForDate()
        {
            if (BarsArray[1] == null || BarsArray[1].Count < 2)
            {
                Print("DIAGNOSTIC: Daily data series not available for manual calculation.");
                return;
            }

            // In manual mode, the user selects the day they want to see the levels on.
            // The calculations should be based on the trading day PRIOR to the selected date.
            DateTime dateForLevels = SelectedDate.Date;

            // Find the index for the date the user selected.
            int dateForLevels_DailyIndex = BarsArray[1].GetBar(dateForLevels);

            // If we couldn't find the selected date, we can't do anything.
            if (dateForLevels_DailyIndex < 0)
            {
                Print($"DIAGNOSTIC: No daily data found for the selected date: {dateForLevels:d}.");
                return;
            }

            // The calculation day is the day before the day the levels are for.
            // So we need the index for the bar before the selected date's bar.
            int dateForRangeCalc_DailyIndex = dateForLevels_DailyIndex - 1;

            // If there is no prior day, we can't calculate.
            if (dateForRangeCalc_DailyIndex < 0)
            {
                Print($"DIAGNOSTIC: No prior day data found to calculate levels for {dateForLevels:d}.");
                return;
            }

            // Now get the H, L, C of the prior day.
            double priorDayHigh = BarsArray[1].GetHigh(dateForRangeCalc_DailyIndex);
            double priorDayLow = BarsArray[1].GetLow(dateForRangeCalc_DailyIndex);
            double priorDayClose = BarsArray[1].GetClose(dateForRangeCalc_DailyIndex);
            DateTime priorDayDate = BarsArray[1].GetTime(dateForRangeCalc_DailyIndex).Date;

            Print($"DIAGNOSTIC: For levels on {dateForLevels:d}, using data from {priorDayDate:d}. High={priorDayHigh}, Low={priorDayLow}, Close={priorDayClose}");

            double range = priorDayHigh - priorDayLow;

            if (range <= 0)
            {
                Print("DIAGNOSTIC: Calculated range is zero or negative. Levels will not be drawn.");
                return;
            }

            // Apply gap calculation if enabled.
            if (UseGapCalculation)
            {
                // Gap is between prior day's close and current (selected) day's open.
                double selectedDayOpen = BarsArray[1].GetOpen(dateForLevels_DailyIndex);
                Print($"GAP Calc: Using daily open of {selectedDayOpen} from {dateForLevels:d} and prior close of {priorDayClose} from {priorDayDate:d}");
                
                if (priorDayClose > 0 && selectedDayOpen > 0)
                {
                    double gap = Math.Abs(selectedDayOpen - priorDayClose);
                    Print($"DIAGNOSTIC: Gap calculation = |{selectedDayOpen} (Open) - {priorDayClose} (Close)| = {gap}");
                    range += (gap);
                    Print($"DIAGNOSTIC: Modified range (adding half gap) = {range}");
                }
            }

            // Get base price for levels.
            double basePrice = ManualPrice;
            if (basePrice.ApproxCompare(0) == 0)
            {
                if (Nr2LevelType == NR2LevelType.CurrentDayOpen)
                {
                    basePrice = BarsArray[1].GetOpen(dateForLevels_DailyIndex);
                }
                else // PreviousDayClose
                {
                    basePrice = priorDayClose;
                }
            }

            if (basePrice > 0)
            {
                Print($"DIAGNOSTIC: Using Base Price={basePrice} and Range={range} for calculations.");
                CalculateAllLevels(range, basePrice, currentPriceLevels);
                needsLayoutUpdate = true;
            }
        }

        private void CalculateAllLevels(double dayRange, double basePrice, Dictionary<string, PriceLevel> levels)
        {
            try
            {
                if (basePrice <= 0 || dayRange <= 0) return;

                // Precalcular valores comunes para mejorar la eficiencia
                double halfRange = dayRange / 2.0;
                double q1Level = RoundToQuarter(basePrice + halfRange);
                double q4Level = RoundToQuarter(basePrice - halfRange);
                
                // Precalcular múltiplos comunes del rango para evitar cálculos repetidos
                double range_tc_std1 = dayRange * 0.0855;
                double range_nr1_std2 = dayRange * 0.171;
                double range_std3 = dayRange * 0.342;

                // Original values for levels not being changed
                double range0159 = dayRange * 0.159;
                double range025 = dayRange * 0.25;
                double range0375 = dayRange * 0.375;
                double range050 = dayRange * 0.50;
                double range0125 = dayRange * 0.125;
                
                // Asignar valores a los niveles y a los plots
		        levels["Q1"].Value = q1Level;
		        if (CurrentBar >= 0 && q1Level > 0) Q1[0] = q1Level;

		        levels["Q4"].Value = q4Level;
		        if (CurrentBar >= 0 && q4Level > 0) Q4[0] = q4Level;

		        levels["Q2"].Value = RoundToQuarter(q1Level - range025);
		        if (CurrentBar >= 0 && levels["Q2"].Value > 0) Q2[0] = levels["Q2"].Value;

		        levels["Q3"].Value = RoundToQuarter(q4Level + range025);
		        if (CurrentBar >= 0 && levels["Q3"].Value > 0) Q3[0] = levels["Q3"].Value;

		        levels["Q2/3"].Value = RoundToQuarter(q1Level - range0375);
		        if (CurrentBar >= 0 && levels["Q2/3"].Value > 0) Q2_3[0] = levels["Q2/3"].Value;

		        levels["Q3/4"].Value = RoundToQuarter(q4Level + range0375);
		        if (CurrentBar >= 0 && levels["Q3/4"].Value > 0) Q3_4[0] = levels["Q3/4"].Value;

		        levels["NR2"].Value = RoundToQuarter(basePrice);
		        if (CurrentBar >= 0 && levels["NR2"].Value > 0) NR2[0] = levels["NR2"].Value;

		        levels["TC"].Value = RoundToQuarter(q1Level - range_tc_std1);
		        if (CurrentBar >= 0 && levels["TC"].Value > 0) TC[0] = levels["TC"].Value;

		        levels["NR1"].Value = RoundToQuarter(q1Level - range_nr1_std2);
		        if (CurrentBar >= 0 && levels["NR1"].Value > 0) NR1[0] = levels["NR1"].Value;

		        levels["Std1+"].Value = RoundToQuarter(q1Level + range_tc_std1);
		        if (CurrentBar >= 0 && levels["Std1+"].Value > 0) Std1Plus[0] = levels["Std1+"].Value;

		        levels["Std2+"].Value = RoundToQuarter(q1Level + range_nr1_std2);
		        if (CurrentBar >= 0 && levels["Std2+"].Value > 0) Std2Plus[0] = levels["Std2+"].Value;

		        levels["Std3+"].Value = RoundToQuarter(q1Level + range_std3);
		        if (CurrentBar >= 0 && levels["Std3+"].Value > 0) Std3Plus[0] = levels["Std3+"].Value;

		        levels["1D+"].Value = RoundToQuarter(q1Level + range050);
		        if (CurrentBar >= 0 && levels["1D+"].Value > 0) OneDPlus[0] = levels["1D+"].Value;

		        levels["NR3"].Value = RoundToQuarter(q4Level + range0159);
		        if (CurrentBar >= 0 && levels["NR3"].Value > 0) NR3[0] = levels["NR3"].Value;

		        levels["TV"].Value = RoundToQuarter(q4Level + range0125);
		        if (CurrentBar >= 0 && levels["TV"].Value > 0) TV[0] = levels["TV"].Value;

		        levels["Std1-"].Value = RoundToQuarter(q4Level - range_tc_std1);
		        if (CurrentBar >= 0 && levels["Std1-"].Value > 0) Std1Minus[0] = levels["Std1-"].Value;

		        levels["Std2-"].Value = RoundToQuarter(q4Level - range_nr1_std2);
		        if (CurrentBar >= 0 && levels["Std2-"].Value > 0) Std2Minus[0] = levels["Std2-"].Value;

		        levels["Std3-"].Value = RoundToQuarter(q4Level - range_std3);
		        if (CurrentBar >= 0 && levels["Std3-"].Value > 0) Std3Minus[0] = levels["Std3-"].Value;

		        levels["1D-"].Value = RoundToQuarter(q4Level - range050);
		        if (CurrentBar >= 0 && levels["1D-"].Value > 0) OneDMinus[0] = levels["1D-"].Value;
                
                // Mostrar los niveles calculados en el output
                Print($"--- NIVELES CALCULADOS ---");
                Print($"Rango del día: {dayRange:F5}");
                Print($"Precio base (NR2): {levels["NR2"].Value:F5}");
                Print($"Q1: {levels["Q1"].Value:F5}");
                Print($"Q2: {levels["Q2"].Value:F5}");
                Print($"Q3: {levels["Q3"].Value:F5}");
                Print($"Q4: {levels["Q4"].Value:F5}");
                Print($"Q2/3: {levels["Q2/3"].Value:F5}");
                Print($"Q3/4: {levels["Q3/4"].Value:F5}");
                Print($"TC: {levels["TC"].Value:F5}");
                Print($"NR1: {levels["NR1"].Value:F5}");
                Print($"NR3: {levels["NR3"].Value:F5}");
                Print($"TV: {levels["TV"].Value:F5}");
                Print($"Std1+: {levels["Std1+"].Value:F5}");
                Print($"Std2+: {levels["Std2+"].Value:F5}");
                Print($"Std3+: {levels["Std3+"].Value:F5}");
                Print($"1D+: {levels["1D+"].Value:F5}");
                Print($"Std1-: {levels["Std1-"].Value:F5}");
                Print($"Std2-: {levels["Std2-"].Value:F5}");
                Print($"Std3-: {levels["Std3-"].Value:F5}");
                Print($"1D-: {levels["1D-"].Value:F5}");
                Print($"------------------------");
            }
            catch (Exception ex)
            {
                Print($"Error in CalculateAllLevels: {ex.Message}");
            }
        }

        private void UpdateTextLayouts(Dictionary<string, PriceLevel> levels)
        {
            // Verificación temprana para evitar procesamiento innecesario
            if (ChartControl == null) return;
            
            // Usar 'using' para asegurar la liberación correcta de recursos
            using (TextFormat textFormat = ChartControl.Properties.LabelFont.ToDirectWriteTextFormat())
            {
                foreach (var level in levels.Values)
                {
                    // Liberar el layout anterior si existe
                    level.LabelLayout?.Dispose();
                    
                    // Saltar niveles sin valor válido
                    if (double.IsNaN(level.Value))
                    {
                        level.LabelLayout = null;
                        continue;
                    }
                    
                    // Crear nueva etiqueta con el valor y el modificador
                    string labelText;
                    if (!string.IsNullOrEmpty(level.Modifier))
                        labelText = $"{level.Name} ({level.Modifier}) {level.Value:F2}";
                    else
                        labelText = $"{level.Name} {level.Value:F2}";
                    
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
			if (needsLayoutUpdate)
			{
				UpdateTextLayouts(currentPriceLevels);
				UpdateTextLayouts(previousPriceLevels);
				needsLayoutUpdate = false;
			}

			if (UseAutomaticDate)
			{
				// Draw previous day's lines (not extended)
				DrawLevelsForRange(chartControl, chartScale, previousPriceLevels, previousDayStartBar, previousDayEndBar, false);

				// Draw current day's lines (extended)
				DrawLevelsForRange(chartControl, chartScale, currentPriceLevels, firstBarOfCurrentDay, ChartBars.ToIndex, true);
			}
			else // Manual Mode
			{
				// In manual mode, we only draw one set of lines for the selected day
				DrawLevelsForRange(chartControl, chartScale, currentPriceLevels, manualStartBar, manualEndBar, false);
			}
		}

		private void DrawLevelsForRange(ChartControl chartControl, ChartScale chartScale, Dictionary<string, PriceLevel> levels, int startBarIndex, int endBarIndex, bool extendLine)
		{
			if (levels.Count == 0 || !levels.TryGetValue("Q1", out PriceLevel q1) || double.IsNaN(q1.Value) || startBarIndex < 0 || endBarIndex < startBarIndex)
				return;

			int lastBarIndex = ChartBars?.ToIndex ?? -1;
			if (lastBarIndex < 0) return;

			double startBarX = chartControl.GetXByBarIndex(ChartBars, startBarIndex);
			float lineStartX = (float)startBarX;

			double endBarX = chartControl.GetXByBarIndex(ChartBars, endBarIndex);
			float lineEndX = (float)endBarX;

			if (extendLine && UseAutomaticDate) // Only extend in automatic mode
			{
				lineEndX += 10;
			}
			
			if (lineEndX <= lineStartX) return;

			float labelX = lineEndX + 5;
			SharpDX.Direct2D1.Brush labelBrush = GetDxBrush(chartControl.Properties.ChartText);

			foreach (var level in levels.Values)
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
        [Display(Name = "NR2 Level Type", Description = "Select whether NR2 should use the previous day's close or current day's open.", Order = 2, GroupName = "Parameters")]
        public NR2LevelType Nr2LevelType 
        { 
            get { return nr2LevelType; } 
            set { nr2LevelType = value; } 
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


        #region Exported Plots
        [Browsable(false)] [XmlIgnore] public Series<double> Q1 { get { return Values[0]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> Q4 { get { return Values[1]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> Q2 { get { return Values[2]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> Q3 { get { return Values[3]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> Q2_3 { get { return Values[4]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> Q3_4 { get { return Values[5]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> TC { get { return Values[6]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> NR1 { get { return Values[7]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> NR2 { get { return Values[8]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> NR3 { get { return Values[9]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> TV { get { return Values[10]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> Std1Plus { get { return Values[11]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> Std2Plus { get { return Values[12]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> Std3Plus { get { return Values[13]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> OneDPlus { get { return Values[14]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> Std1Minus { get { return Values[15]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> Std2Minus { get { return Values[16]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> Std3Minus { get { return Values[17]; } }
        [Browsable(false)] [XmlIgnore] public Series<double> OneDMinus { get { return Values[18]; } }
        #endregion
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private AvalPV4[] cacheAvalPV4;
		public AvalPV4 AvalPV4(bool useAutomaticDate, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return AvalPV4(Input, useAutomaticDate, nr2LevelType, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public AvalPV4 AvalPV4(ISeries<double> input, bool useAutomaticDate, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			if (cacheAvalPV4 != null)
				for (int idx = 0; idx < cacheAvalPV4.Length; idx++)
					if (cacheAvalPV4[idx] != null && cacheAvalPV4[idx].UseAutomaticDate == useAutomaticDate && cacheAvalPV4[idx].Nr2LevelType == nr2LevelType && cacheAvalPV4[idx].UseGapCalculation == useGapCalculation && cacheAvalPV4[idx].SelectedDate == selectedDate && cacheAvalPV4[idx].ManualPrice == manualPrice && cacheAvalPV4[idx].Width == width && cacheAvalPV4[idx].LineBufferPixels == lineBufferPixels && cacheAvalPV4[idx].EqualsInput(input))
						return cacheAvalPV4[idx];
			return CacheIndicator<AvalPV4>(new AvalPV4(){ UseAutomaticDate = useAutomaticDate, Nr2LevelType = nr2LevelType, UseGapCalculation = useGapCalculation, SelectedDate = selectedDate, ManualPrice = manualPrice, Width = width, LineBufferPixels = lineBufferPixels }, input, ref cacheAvalPV4);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.AvalPV4 AvalPV4(bool useAutomaticDate, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV4(Input, useAutomaticDate, nr2LevelType, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public Indicators.AvalPV4 AvalPV4(ISeries<double> input , bool useAutomaticDate, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV4(input, useAutomaticDate, nr2LevelType, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.AvalPV4 AvalPV4(bool useAutomaticDate, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV4(Input, useAutomaticDate, nr2LevelType, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}

		public Indicators.AvalPV4 AvalPV4(ISeries<double> input , bool useAutomaticDate, NR2LevelType nr2LevelType, bool useGapCalculation, DateTime selectedDate, double manualPrice, int width, int lineBufferPixels)
		{
			return indicator.AvalPV4(input, useAutomaticDate, nr2LevelType, useGapCalculation, selectedDate, manualPrice, width, lineBufferPixels);
		}
	}
}

#endregion
