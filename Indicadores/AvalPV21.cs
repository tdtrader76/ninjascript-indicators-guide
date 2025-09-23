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
	public enum TCVMultiplierType
	{
		[Description("0.125")]
		Default,
		[Description("0.0855")]
		Std1
	}

    public class AvalPV21 : Indicator
    {
        #region Variables
        private DateTime selectedDate;
        private double manualPrice;
        private NR2LevelType nr2LevelType;

        private int lastDayProcessed = -1;
		private int manualStartBar = -1;

        private readonly Dictionary<string, PriceLevel> priceLevels = new Dictionary<string, PriceLevel>();

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
                LabelLayout?.Dispose();
                LabelLayout = null;
            }
        }

        private readonly Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush> dxBrushes = new Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush>();
        private bool needsLayoutUpdate = false;
        #endregion

        protected override void OnStateChange()
        {
            switch (State)
            {
                case State.SetDefaults:
                    Description = @"Calculates and displays price levels based on the previous day's range with custom multipliers.";
                    Name = "AvalPV21";
                    Calculate = Calculate.OnEachTick;
                    IsOverlay = true;
                    DisplayInDataBox = true;
                    DrawOnPricePanel = true;
                    DrawHorizontalGridLines = true;
                    DrawVerticalGridLines = true;
                    PaintPriceMarkers = true;
                    ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                    IsSuspendedWhileInactive = true;

                    UseAutomaticDate = true;
                    Nr2LevelType = NR2LevelType.PreviousDayClose;
                    TCVMultiplier = TCVMultiplierType.Default;
                    UseGapCalculation = false;
                    SelectedDate = DateTime.Today;
                    ManualPrice = 0.0;
                    Width = 1;
                    LineBufferPixels = 125;
                    break;

                case State.Configure:
                    AddDataSeries(Bars.Instrument.FullName, BarsPeriodType.Day, 1);
                    InitializePriceLevels();
                    break;

                case State.DataLoaded:
                    ClearOutputWindow();
                    break;

                case State.Terminated:
                    foreach (var brush in dxBrushes.Values)
                        brush?.Dispose();
                    dxBrushes.Clear();

                    foreach (var level in priceLevels.Values)
                        level?.Dispose();
                    break;

                case State.Historical:
                    SetZOrder(-1);
                    
                    if (!Bars.BarsType.IsIntraday)
                    {
                        Draw.TextFixed(this, "NinjaScriptInfo", "AvalPV21 only works on intraday charts", TextPosition.BottomRight);
                        return;
                    }
                    
                    if (!UseAutomaticDate)
					{
                        CalculateLevelsForDate(SelectedDate);
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
            }
        }

        protected override void OnBarUpdate()
        {
            if (!UseAutomaticDate || BarsArray[1] == null || BarsArray[1].Count < 2 || CurrentBars[1] < 1)
                return;

            try
            {
                int currentDayNumber = Bars.GetTime(0).DayOfYear;
                if (lastDayProcessed != currentDayNumber)
                {
                    lastDayProcessed = currentDayNumber;

                    double priorDayHigh = Highs[1][1];
                    double priorDayLow = Lows[1][1];
                    double priorDayClose = Closes[1][1];

                    if (priorDayHigh > 0 && priorDayLow > 0 && priorDayHigh >= priorDayLow)
                    {
						Print($"--- NUEVO DÍA DETECTADO: {Bars.GetTime(0):d} ---");
						Print($"Máximo del día anterior para el rango: {priorDayHigh:F5}");
						Print($"Mínimo del día anterior para el rango: {priorDayLow:F5}");
                        double previousDayRange = priorDayHigh - priorDayLow;

                        if (UseGapCalculation)
                        {
                            double currentDayOpen = Opens[1][0];
                            previousDayRange = ApplyGapCalculation(previousDayRange, priorDayClose, currentDayOpen);
                        }
                        
                        double basePrice = ManualPrice;
                        if (basePrice.ApproxCompare(0) == 0)
                        {
                            basePrice = (Nr2LevelType == NR2LevelType.CurrentDayOpen) ? Opens[1][0] : priorDayClose;
                        }

                        if (basePrice > 0)
                        {
                            CalculateAllLevels(previousDayRange, basePrice);
                            needsLayoutUpdate = true;
							ForceRefresh();
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

        private void InitializePriceLevels()
        {
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
                new { Name = "Std1+ (0.0855)", Brush = Brushes.ForestGreen },
                new { Name = "Std2+ (0.171)", Brush = Brushes.ForestGreen },
                new { Name = "Std3+ (0.342)", Brush = Brushes.ForestGreen },
                new { Name = "1D+", Brush = Brushes.Gold },
                new { Name = "Std1- (0.0855)", Brush = Brushes.IndianRed },
                new { Name = "Std2- (0.171)", Brush = Brushes.IndianRed },
                new { Name = "Std3- (0.342)", Brush = Brushes.IndianRed },
                new { Name = "1D-", Brush = Brushes.Gold }
            };

            priceLevels.Clear();
            foreach (var def in levelDefinitions)
            {
                priceLevels[def.Name] = new PriceLevel(def.Name, def.Brush);
            }
        }
        
        private double RoundToQuarter(double value)
        {
            return Math.Round(value * 4) / 4;
        }

        private double ApplyGapCalculation(double previousDayRange, double priorDayClose, double currentDayOpen)
        {
            if (priorDayClose > 0 && currentDayOpen.ApproxCompare(priorDayClose) != 0)
            {
                double gap = Math.Abs(currentDayOpen - priorDayClose);
                Print($"Cálculo de Gap: |Open ({currentDayOpen}) - Close Anterior ({priorDayClose})| = {gap:F5}");
                double modifiedRange = previousDayRange + gap;
                Print($"Rango con Gap: {previousDayRange:F5} (Rango Inicial) + {gap:F5} (Gap) = {modifiedRange:F5}");
                return modifiedRange;
            }
            
            Print("No se detectó Gap o no fue posible calcularlo.");
            return previousDayRange;
        }

        private void CalculateLevelsForDate(DateTime date)
        {
            if (BarsArray[1] == null || BarsArray[1].Count < 2) return;

            int dailyIndex = BarsArray[1].GetBar(date);
            if (dailyIndex < 1)
            {
                Print($"No se encontraron datos diarios para la fecha {date:d} o es la primera barra.");
                return;
            }

            double priorDayHigh = BarsArray[1].GetHigh(dailyIndex - 1);
            double priorDayLow = BarsArray[1].GetLow(dailyIndex - 1);
            double priorDayClose = BarsArray[1].GetClose(dailyIndex - 1);

            if (priorDayHigh > 0 && priorDayLow > 0 && priorDayHigh >= priorDayLow)
            {
				Print($"--- CÁLCULO MANUAL PARA FECHA: {date:d} ---");
				Print($"Máximo del día anterior ({BarsArray[1].GetTime(dailyIndex-1):d}) para el rango: {priorDayHigh:F5}");
				Print($"Mínimo del día anterior ({BarsArray[1].GetTime(dailyIndex-1):d}) para el rango: {priorDayLow:F5}");
                double range = priorDayHigh - priorDayLow;

                if (UseGapCalculation)
                {
                    double currentDayOpen = BarsArray[1].GetOpen(dailyIndex);
                    range = ApplyGapCalculation(range, priorDayClose, currentDayOpen);
                }

                double basePrice = ManualPrice;
                if (basePrice.ApproxCompare(0) == 0)
                {
                    basePrice = (Nr2LevelType == NR2LevelType.CurrentDayOpen) ? BarsArray[1].GetOpen(dailyIndex) : priorDayClose;
                }

                if (basePrice > 0)
                {
                    CalculateAllLevels(range, basePrice);
                    needsLayoutUpdate = true;
                }
            }
        }

        private void CalculateAllLevels(double dayRange, double basePrice)
        {
            if (basePrice <= 0 || dayRange <= 0) return;

            Print("--- INICIO DE CÁLCULOS DETALLADOS ---");
            Print($"Rango del día (entrada): {dayRange:F5}");
            Print($"Precio base (entrada): {basePrice:F5}");

            double halfRange = dayRange / 2.0;
            Print($"Rango modificado / 2: {dayRange:F5} / 2 = {halfRange:F5}");

            double q1Level = RoundToQuarter(basePrice + halfRange);
            Print($"Q1 (bruto): {basePrice:F5} + {halfRange:F5} = {basePrice + halfRange:F5} -> Redondeado: {q1Level:F5}");
            
            double q4Level = RoundToQuarter(basePrice - halfRange);
            Print($"Q4 (bruto): {basePrice:F5} - {halfRange:F5} = {basePrice - halfRange:F5} -> Redondeado: {q4Level:F5}");
            
            Print("\n--- Multiplicadores Estándar ---");
            double range0125 = dayRange * 0.125;
            Print($"Rango * 0.125 = {range0125:F5}");
            double range0159 = dayRange * 0.159;
            Print($"Rango * 0.159 = {range0159:F5}");
            double range025 = dayRange * 0.25;
            Print($"Rango * 0.25 = {range025:F5}");
            double range0375 = dayRange * 0.375;
            Print($"Rango * 0.375 = {range0375:F5}");
            double range050 = dayRange * 0.50;
            Print($"Rango * 0.50 = {range050:F5}");

            Print("\n--- Multiplicadores STD Personalizados ---");
            double std1_mult = 0.0855;
            double range_std1 = dayRange * std1_mult;
            Print($"Rango * {std1_mult} = {range_std1:F5}");

			double tctv_mult = (TCVMultiplier == TCVMultiplierType.Default) ? 0.125 : 0.0855;
			double range_tctv = dayRange * tctv_mult;
			Print($"Multiplicador TC/TV seleccionado: {tctv_mult}");
			Print($"Rango * {tctv_mult} (TC/TV) = {range_tctv:F5}");

            Print("\n--- ASIGNACIÓN DE NIVELES ---");
            priceLevels["Q1"].Value = q1Level;
            priceLevels["Q4"].Value = q4Level;
            priceLevels["Q2"].Value = RoundToQuarter(q1Level - range025);
            priceLevels["Q3"].Value = RoundToQuarter(q4Level + range025);
            priceLevels["Q2/3"].Value = RoundToQuarter(q1Level - range0375);
            priceLevels["Q3/4"].Value = RoundToQuarter(q4Level + range0375);
            priceLevels["NR2"].Value = RoundToQuarter(basePrice);
            priceLevels["TC"].Value = RoundToQuarter(q1Level - range_tctv);
            priceLevels["NR1"].Value = RoundToQuarter(q1Level - range0159);
            priceLevels["Std1+ (0.0855)"].Value = RoundToQuarter(q1Level + range_std1);
            priceLevels["Std2+ (0.171)"].Value = RoundToQuarter(q1Level + (dayRange * 0.171));
            priceLevels["Std3+ (0.342)"].Value = RoundToQuarter(q1Level + (dayRange * 0.342));
            priceLevels["1D+"].Value = RoundToQuarter(q1Level + range050);
            priceLevels["NR3"].Value = RoundToQuarter(q4Level + range0159);
            priceLevels["TV"].Value = RoundToQuarter(q4Level + range_tctv);
            priceLevels["Std1- (0.0855)"].Value = RoundToQuarter(q4Level - range_std1);
            priceLevels["Std2- (0.171)"].Value = RoundToQuarter(q4Level - (dayRange * 0.171));
            priceLevels["Std3- (0.342)"].Value = RoundToQuarter(q4Level - (dayRange * 0.342));
            priceLevels["1D-"].Value = RoundToQuarter(q4Level - range050);
        }

        private void UpdateTextLayouts()
        {
            if (ChartControl == null) return; 
            
            using (TextFormat textFormat = ChartControl.Properties.LabelFont.ToDirectWriteTextFormat())
            {
                foreach (var level in priceLevels.Values)
                {
                    level.LabelLayout?.Dispose();
                    
                    if (double.IsNaN(level.Value))
                    {
                        level.LabelLayout = null;
                        continue;
                    }
                    
					string labelText;
					if (level.Name == "TC" || level.Name == "TV")
					{
						double multiplier = (TCVMultiplier == TCVMultiplierType.Default) ? 0.125 : 0.0855;
						labelText = $"{level.Name} ({multiplier:F4}) {RoundToQuarter(level.Value):F2}";
					}
					else
					{
						labelText = $"{level.Name} {RoundToQuarter(level.Value):F2}";
					}

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
            if (priceLevels.Count == 0 || !priceLevels.TryGetValue("Q1", out PriceLevel q1) || double.IsNaN(q1.Value))
                return;

            if (needsLayoutUpdate)
            {
                UpdateTextLayouts();
                needsLayoutUpdate = false;
            }

            int lastBarIndex = ChartBars?.ToIndex ?? -1;
            if (lastBarIndex < 0) return;

			int startBarIndex = -1;
			if(UseAutomaticDate)
			{
				for (int i = lastBarIndex; i >= 0; i--)
				{
					if (Bars.GetTime(i).DayOfYear != Bars.GetTime(lastBarIndex).DayOfYear)
						break;
					startBarIndex = i;
				}
			}
			else
			{
				startBarIndex = manualStartBar;
			}

			if (startBarIndex < 0) return;

            double startBarX = chartControl.GetXByBarIndex(ChartBars, startBarIndex);
            float lineStartX = (float)startBarX;

            double lastBarX = chartControl.GetXByBarIndex(ChartBars, lastBarIndex);
            float lineEndX = (float)(lastBarX + 10);
            
            if (lineEndX <= lineStartX) return;

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
        [Display(Name = "NR2 Level Type", Description = "Select whether NR2 should use the previous day's close or current day's open.", Order = 2, GroupName = "Parameters")]
        public NR2LevelType Nr2LevelType { get; set; }

		[NinjaScriptProperty]
        [Display(Name = "TC/TV Multiplier", Description = "Selects the multiplier for TC and TV levels.", Order = 3, GroupName = "Parameters")]
        public TCVMultiplierType TCVMultiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "GAP", Description = "If true, adds the opening gap to the previous day's range.", Order = 4, GroupName = "Parameters")]
        public bool UseGapCalculation { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Selected Date", Description = "Date for calculating levels (only used if 'Use Automatic Date' is false).", Order = 5, GroupName = "Parameters")]
        public DateTime SelectedDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Manual Price", Description = "Base price for levels. If 0, uses prior day's close.", Order = 6, GroupName = "Parameters")]
        public double ManualPrice { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Line Width", Description = "Width of the level lines", Order = 1, GroupName = "Visuals")]
        public int Width { get; set; } 
        
        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Line Buffer (Pixels)", Description = "Pixel buffer from the last bar for line drawing.", Order = 2, GroupName = "Visuals")]
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
		public AvalPV21 AvalPV21(TCVMultiplierType tCVMultiplier)
		{
			return AvalPV21(Input, tCVMultiplier);
		}

		public AvalPV21 AvalPV21(ISeries<double> input, TCVMultiplierType tCVMultiplier)
		{
			if (cacheAvalPV21 != null)
				for (int idx = 0; idx < cacheAvalPV21.Length; idx++)
					if (cacheAvalPV21[idx] != null && cacheAvalPV21[idx].TCVMultiplier == tCVMultiplier && cacheAvalPV21[idx].EqualsInput(input))
						return cacheAvalPV21[idx];
			return CacheIndicator<AvalPV21>(new AvalPV21(){ TCVMultiplier = tCVMultiplier }, input, ref cacheAvalPV21);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.AvalPV21 AvalPV21(TCVMultiplierType tCVMultiplier)
		{
			return indicator.AvalPV21(Input, tCVMultiplier);
		}

		public Indicators.AvalPV21 AvalPV21(ISeries<double> input , TCVMultiplierType tCVMultiplier)
		{
			return indicator.AvalPV21(input, tCVMultiplier);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.AvalPV21 AvalPV21(TCVMultiplierType tCVMultiplier)
		{
			return indicator.AvalPV21(Input, tCVMultiplier);
		}

		public Indicators.AvalPV21 AvalPV21(ISeries<double> input , TCVMultiplierType tCVMultiplier)
		{
			return indicator.AvalPV21(input, tCVMultiplier);
		}
	}
}

#endregion