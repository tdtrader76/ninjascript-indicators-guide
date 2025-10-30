#region Using declarations
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
#endregion

#region Enums

public enum EVCalDisplayMode
{
    [Description("Tabla Detallada")]
    Table,
    
    [Description("Texto Simple")]
    SimpleText,
    
    [Description("Ambos")]
    Both
}

public enum EVCalChartCorner
{
    [Description("Superior Izquierda")]
    TopLeft,
    
    [Description("Superior Derecha")]
    TopRight,
    
    [Description("Inferior Izquierda")]
    BottomLeft,
    
    [Description("Inferior Derecha")]
    BottomRight
}

#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class EVCal : Indicator
    {
        #region Variables
        private double expectedValue;
        private double winProbability;
        private double lossProbability;
        private double riskRewardRatio;
        
        // SharpDX resources for table rendering
        private SharpDX.Direct2D1.SolidColorBrush backgroundBrush;
        private SharpDX.Direct2D1.SolidColorBrush borderBrush;
        private SharpDX.Direct2D1.SolidColorBrush textBrush;
        private SharpDX.Direct2D1.SolidColorBrush positiveBrush;
        private SharpDX.Direct2D1.SolidColorBrush negativeBrush;
        private SharpDX.DirectWrite.TextFormat textFormat;
        private SharpDX.DirectWrite.TextFormat headerFormat;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Calculadora de Expected Value (Valor Esperado) para futuros financieros. Calcula EV = (P_ganancia × TakeProfit) - (P_pérdida × StopLoss)";
                Name = "EVCal";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
                
                // Parámetros por defecto
                WinProbabilityPercent = 65.0;
                StopLossTicks = 60;
                TakeProfitTicks = 120;
                DisplayMode = EVCalDisplayMode.Table;
                TablePosition = EVCalChartCorner.TopRight;
                ShowTextDisplay = TextPosition.TopLeft;
            }
            else if (State == State.Terminated)
            {
                // Dispose SharpDX resources
                if (backgroundBrush != null) backgroundBrush.Dispose();
                if (borderBrush != null) borderBrush.Dispose();
                if (textBrush != null) textBrush.Dispose();
                if (positiveBrush != null) positiveBrush.Dispose();
                if (negativeBrush != null) negativeBrush.Dispose();
                if (textFormat != null) textFormat.Dispose();
                if (headerFormat != null) headerFormat.Dispose();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 1)
                return;

            // Calcular probabilidades
            winProbability = WinProbabilityPercent / 100.0;
            lossProbability = 1.0 - winProbability;
            
            // Calcular Expected Value en ticks
            // EV = (P_ganancia × TakeProfit) - (P_pérdida × StopLoss)
            expectedValue = (winProbability * TakeProfitTicks) - (lossProbability * StopLossTicks);
            
            // Calcular Risk:Reward Ratio
            riskRewardRatio = (double)TakeProfitTicks / StopLossTicks;
            
            // Validar resultados
            if (double.IsNaN(expectedValue) || double.IsInfinity(expectedValue))
                expectedValue = 0;
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (DisplayMode == EVCalDisplayMode.Table)
            {
                RenderTable(chartControl, chartScale);
            }
            else if (DisplayMode == EVCalDisplayMode.SimpleText)
            {
                RenderSimpleText(chartControl, chartScale);
            }
            else if (DisplayMode == EVCalDisplayMode.Both)
            {
                RenderTable(chartControl, chartScale);
                RenderSimpleText(chartControl, chartScale);
            }
        }

        #region Rendering Methods

        private void RenderTable(ChartControl chartControl, ChartScale chartScale)
        {
            // Initialize brushes
            if (backgroundBrush == null)
                backgroundBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, 
                    new SharpDX.Color(0, 0, 0, 180)); // Negro semi-transparente
            
            if (borderBrush == null)
                borderBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, 
                    SharpDX.Color.White);
            
            if (textBrush == null)
                textBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, 
                    SharpDX.Color.White);
            
            if (positiveBrush == null)
                positiveBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, 
                    new SharpDX.Color(0, 255, 0, 255)); // Verde
            
            if (negativeBrush == null)
                negativeBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, 
                    new SharpDX.Color(255, 0, 0, 255)); // Rojo
            
            if (textFormat == null)
                textFormat = new TextFormat(NinjaTrader.Core.Globals.DirectWriteFactory, 
                    "Arial", 11) 
                    { 
                        TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading, 
                        ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center 
                    };
            
            if (headerFormat == null)
                headerFormat = new TextFormat(NinjaTrader.Core.Globals.DirectWriteFactory, 
                    "Arial", SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, 12) 
                    { 
                        TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading, 
                        ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center 
                    };

            // Table dimensions
            float tableWidth = 280f;
            float tableHeight = 200f;
            float columnWidth1 = 150f;
            float columnWidth2 = tableWidth - columnWidth1;
            float rowHeight = tableHeight / 7;

            // Position calculation
            SharpDX.Vector2 panelPosition = CalculateTablePosition(
                chartControl, 
                TablePosition, 
                tableWidth, 
                tableHeight
            );

            // Draw background
            var backgroundRect = new RectangleF(
                panelPosition.X - 50, 
                panelPosition.Y, 
                tableWidth, 
                tableHeight
            );
            
            RenderTarget.DrawRectangle(backgroundRect, borderBrush, 2);

            // Draw grid lines - Horizontal
            for (int i = 0; i <= 7; i++)
            {
                SharpDX.Vector2 start = new SharpDX.Vector2(
                    panelPosition.X - 50, 
                    panelPosition.Y + (i * rowHeight)
                );
                SharpDX.Vector2 end = new SharpDX.Vector2(
                    panelPosition.X + tableWidth, 
                    panelPosition.Y + (i * rowHeight)
                );
                RenderTarget.DrawLine(start, end, borderBrush, 1);
            }

            // Draw grid lines - Vertical
            SharpDX.Vector2 vertStart = new SharpDX.Vector2(
                panelPosition.X + columnWidth1, 
                panelPosition.Y
            );
            SharpDX.Vector2 vertEnd = new SharpDX.Vector2(
                panelPosition.X + columnWidth1, 
                panelPosition.Y + tableHeight
            );
            RenderTarget.DrawLine(vertStart, vertEnd, borderBrush, 1);

            // Draw header
            float textMargin = 8f;
            DrawText(RenderTarget, "EXPECTED VALUE", headerFormat, textBrush, 
                new RectangleF(
                    panelPosition.X + textMargin, 
                    panelPosition.Y, 
                    tableWidth - textMargin, 
                    rowHeight
                )
            );

            // Data rows
            string[] descriptions = { 
                "Prob. Ganancia", 
                "Prob. Pérdida", 
                "Stop Loss (ticks)", 
                "Take Profit (ticks)", 
                "Risk:Reward", 
                "Expected Value" 
            };
            
            string[] values = { 
                string.Format("{0:F2}%", WinProbabilityPercent),
                string.Format("{0:F2}%", lossProbability * 100),
                StopLossTicks.ToString(),
                TakeProfitTicks.ToString(),
                string.Format("{0:F2}", riskRewardRatio),
                string.Format("{0:F2} ticks", expectedValue)
            };

            for (int i = 0; i < descriptions.Length; i++)
            {
                // Description column
                DrawText(RenderTarget, descriptions[i], textFormat, textBrush, 
                    new RectangleF(
                        panelPosition.X + textMargin, 
                        panelPosition.Y + ((i + 1) * rowHeight), 
                        columnWidth1 - textMargin, 
                        rowHeight
                    )
                );
                
                // Value column - usar color según el valor
                var valueBrush = textBrush;
                if (i == descriptions.Length - 1) // Expected Value row
                {
                    valueBrush = expectedValue >= 0 ? positiveBrush : negativeBrush;
                }
                
                DrawText(RenderTarget, values[i], textFormat, valueBrush, 
                    new RectangleF(
                        panelPosition.X + columnWidth1 + textMargin, 
                        panelPosition.Y + ((i + 1) * rowHeight), 
                        columnWidth2 - textMargin, 
                        rowHeight
                    )
                );
            }
        }

        private void RenderSimpleText(ChartControl chartControl, ChartScale chartScale)
        {
            string displayText = string.Format("EV: {0:F2} ticks | Win: {1:F1}% | R:R: {2:F2}", 
                expectedValue, 
                WinProbabilityPercent, 
                riskRewardRatio);
            
            System.Windows.Media.Brush textColor = expectedValue >= 0 ? 
                System.Windows.Media.Brushes.LimeGreen : 
                System.Windows.Media.Brushes.Red;
            
            Draw.TextFixed(this, "EVDisplay", displayText, ShowTextDisplay, 
                textColor, 
                new Gui.Tools.SimpleFont("Arial", 14) { Bold = true }, 
                System.Windows.Media.Brushes.Black, 
                System.Windows.Media.Brushes.Transparent, 
                0);
        }

        private SharpDX.Vector2 CalculateTablePosition(
            ChartControl chartControl, 
            EVCalChartCorner corner, 
            float width, 
            float height)
        {
            float margin = 10f;
            switch (corner)
            {
                case EVCalChartCorner.TopLeft:
                    return new SharpDX.Vector2(margin, margin);
                
                case EVCalChartCorner.TopRight:
                    return new SharpDX.Vector2(
                        (float)chartControl.ActualWidth - width - margin, 
                        margin
                    );
                
                case EVCalChartCorner.BottomLeft:
                    return new SharpDX.Vector2(
                        margin, 
                        (float)chartControl.ActualHeight - height - margin
                    );
                
                case EVCalChartCorner.BottomRight:
                    return new SharpDX.Vector2(
                        (float)chartControl.ActualWidth - width - margin, 
                        (float)chartControl.ActualHeight - height - margin
                    );
                
                default:
                    return new SharpDX.Vector2(margin, margin);
            }
        }

        private void DrawText(
            RenderTarget renderTarget, 
            string text, 
            TextFormat textFormat, 
            SharpDX.Direct2D1.Brush brush, 
            RectangleF layoutRect)
        {
            using (var textLayout = new TextLayout(
                NinjaTrader.Core.Globals.DirectWriteFactory, 
                text, 
                textFormat, 
                layoutRect.Width, 
                layoutRect.Height))
            {
                renderTarget.DrawTextLayout(
                    new SharpDX.Vector2(layoutRect.X, layoutRect.Y), 
                    textLayout, 
                    brush, 
                    DrawTextOptions.NoSnap
                );
            }
        }

        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Range(0.01, 99.99)]
        [Display(Name = "Probabilidad de Ganancia (%)", Description = "Porcentaje de probabilidad de que la operación sea ganadora", Order = 1, GroupName = "1. Parámetros")]
        public double WinProbabilityPercent { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10000)]
        [Display(Name = "Stop Loss (ticks)", Description = "Tamaño del Stop Loss en ticks", Order = 2, GroupName = "1. Parámetros")]
        public int StopLossTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10000)]
        [Display(Name = "Take Profit (ticks)", Description = "Tamaño del Take Profit en ticks", Order = 3, GroupName = "1. Parámetros")]
        public int TakeProfitTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo de Visualización", Description = "Selecciona cómo mostrar el Expected Value", Order = 1, GroupName = "2. Visualización")]
        public EVCalDisplayMode DisplayMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Posición de Tabla", Description = "Esquina del gráfico donde se muestra la tabla", Order = 2, GroupName = "2. Visualización")]
        public EVCalChartCorner TablePosition { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Posición de Texto Simple", Description = "Posición del texto simple en el gráfico", Order = 3, GroupName = "2. Visualización")]
        public TextPosition ShowTextDisplay { get; set; }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private EVCal[] cacheEVCal;
		public EVCal EVCal(double winProbabilityPercent, int stopLossTicks, int takeProfitTicks, EVCalDisplayMode displayMode, EVCalChartCorner tablePosition, TextPosition showTextDisplay)
		{
			return EVCal(Input, winProbabilityPercent, stopLossTicks, takeProfitTicks, displayMode, tablePosition, showTextDisplay);
		}

		public EVCal EVCal(ISeries<double> input, double winProbabilityPercent, int stopLossTicks, int takeProfitTicks, EVCalDisplayMode displayMode, EVCalChartCorner tablePosition, TextPosition showTextDisplay)
		{
			if (cacheEVCal != null)
				for (int idx = 0; idx < cacheEVCal.Length; idx++)
					if (cacheEVCal[idx] != null && cacheEVCal[idx].WinProbabilityPercent == winProbabilityPercent && cacheEVCal[idx].StopLossTicks == stopLossTicks && cacheEVCal[idx].TakeProfitTicks == takeProfitTicks && cacheEVCal[idx].DisplayMode == displayMode && cacheEVCal[idx].TablePosition == tablePosition && cacheEVCal[idx].ShowTextDisplay == showTextDisplay && cacheEVCal[idx].EqualsInput(input))
						return cacheEVCal[idx];
			return CacheIndicator<EVCal>(new EVCal(){ WinProbabilityPercent = winProbabilityPercent, StopLossTicks = stopLossTicks, TakeProfitTicks = takeProfitTicks, DisplayMode = displayMode, TablePosition = tablePosition, ShowTextDisplay = showTextDisplay }, input, ref cacheEVCal);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.EVCal EVCal(double winProbabilityPercent, int stopLossTicks, int takeProfitTicks, EVCalDisplayMode displayMode, EVCalChartCorner tablePosition, TextPosition showTextDisplay)
		{
			return indicator.EVCal(Input, winProbabilityPercent, stopLossTicks, takeProfitTicks, displayMode, tablePosition, showTextDisplay);
		}

		public Indicators.EVCal EVCal(ISeries<double> input , double winProbabilityPercent, int stopLossTicks, int takeProfitTicks, EVCalDisplayMode displayMode, EVCalChartCorner tablePosition, TextPosition showTextDisplay)
		{
			return indicator.EVCal(input, winProbabilityPercent, stopLossTicks, takeProfitTicks, displayMode, tablePosition, showTextDisplay);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.EVCal EVCal(double winProbabilityPercent, int stopLossTicks, int takeProfitTicks, EVCalDisplayMode displayMode, EVCalChartCorner tablePosition, TextPosition showTextDisplay)
		{
			return indicator.EVCal(Input, winProbabilityPercent, stopLossTicks, takeProfitTicks, displayMode, tablePosition, showTextDisplay);
		}

		public Indicators.EVCal EVCal(ISeries<double> input , double winProbabilityPercent, int stopLossTicks, int takeProfitTicks, EVCalDisplayMode displayMode, EVCalChartCorner tablePosition, TextPosition showTextDisplay)
		{
			return indicator.EVCal(input, winProbabilityPercent, stopLossTicks, takeProfitTicks, displayMode, tablePosition, showTextDisplay);
		}
	}
}

#endregion
