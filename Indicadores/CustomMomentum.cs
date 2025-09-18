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

// Añadir un alias para la herramienta de dibujo para resolver la ambigüedad
using NTDrawingTools = NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class CustomMomentum : Indicator
    {
        #region Variables
        private int period = 14;
        private double currentMomentumValue = 0;
        private TextFormat textFormat;
        private SharpDX.Direct2D1.Brush textBrush;
        private SharpDX.Direct2D1.Brush backgroundBrush;

        // Variables para gestionar el estado de los pinceles y el texto
        private System.Windows.Media.Brush lastValueColor;
        private System.Windows.Media.Brush lastBackgroundColor;
        private string lastMomentumText;
        private TextLayout textLayout;

        // Variables para la comparación de momentum
        private int firstBarIndex = -1;
        private double firstMomentumValue;
        private List<Signal> signals = new List<Signal>();
        #endregion

        // Estructura para almacenar la información de la señal
        private struct Signal
        {
            public int BarIndex;
            public double Y;
            public bool IsUp;
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Indicador de Momentum personalizable con períodos configurables, estilo de línea y visualización de valor";
                Name = "CustomMomentum";
                Calculate = Calculate.OnBarClose;
                IsOverlay = false;
                DisplayInDataBox = true;
                DrawOnPricePanel = false;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // Configuración de la línea del momentum
                Period = 14;
                LineColor = Brushes.Blue;
                LineStyle = DashStyleHelper.Solid;
                LineWidth = 2;

                // Configuración del texto de valor
                ShowValue = true;
                ValueColor = Brushes.White;
                BackgroundColor = Brushes.Black;

                // Nueva propiedad para la comparación
                EnableComparison = true;

                AddPlot(Brushes.Blue, "Momentum");
            }
            else if (State == State.Configure)
            {
                Plots[0].Brush = LineColor;
                Plots[0].DashStyleHelper = LineStyle;
                Plots[0].Width = LineWidth;
            }
            else if (State == State.DataLoaded)
            {
                // Limpiar señales al cargar datos
                signals.Clear();
            }
            else if (State == State.Terminated)
            {
                DisposeResources();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Period)
                return;

            double currentPrice = Close[0];
            double pastPrice = Close[Period];

            if (pastPrice.ApproxCompare(0) != 0)
            {
                currentMomentumValue = (currentPrice / pastPrice) * 100;
                Values[0][0] = currentMomentumValue;
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            // Dibujar las señales de divergencia
            foreach (var signal in signals)
            {
                if (signal.IsUp)
                    Draw.ArrowUp(this, $"signalUp_{signal.BarIndex}", true, signal.BarIndex, signal.Y - 5, Brushes.Green);
                else
                    Draw.ArrowDown(this, $"signalDown_{signal.BarIndex}", true, signal.BarIndex, signal.Y + 5, Brushes.Red);
            }

            if (!ShowValue || Bars == null || ChartPanel == null || IsInHitTest)
                return;

            UpdateResources();

            if (currentMomentumValue.ApproxCompare(0) != 0)
            {
                string momentumText = $"Momentum: {currentMomentumValue:F2}";

                if (textLayout == null || momentumText != lastMomentumText)
                {
                    textLayout?.Dispose();
                    textLayout = new TextLayout(Core.Globals.DirectWriteFactory, momentumText, textFormat, ChartPanel.W, ChartPanel.H);
                    lastMomentumText = momentumText;
                }

                float x = (float)(ChartPanel.W - textLayout.Metrics.Width - 10);
                float y = 10;

                var backgroundRect = new RectangleF(x - 5, y - 2, textLayout.Metrics.Width + 10, textLayout.Metrics.Height + 4);
                RenderTarget.FillRectangle(backgroundRect, backgroundBrush);
                RenderTarget.DrawTextLayout(new Vector2(x, y), textLayout, textBrush);
            }
        }

        public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (!EnableComparison || ChartPanel != chartPanel)
                return;

            int barIndex = chartControl.GetBarIdxByTime(ChartControl, dataPoint.Time);

            if (barIndex < 0 || barIndex >= Bars.Count)
                return;

            double clickedMomentum = Values[0][CurrentBar - barIndex];

            if (firstBarIndex == -1)
            {
                firstBarIndex = barIndex;
                firstMomentumValue = clickedMomentum;
                Draw.Ellipse(this, "firstClickMarker", false, barIndex, firstMomentumValue, barIndex, firstMomentumValue, Brushes.Yellow, Brushes.Transparent, 2);
                ChartControl.InvalidateVisual();
            }
            else
            {
                CompareMomentumAndDrawSignal(barIndex, clickedMomentum);
                RemoveDrawObject("firstClickMarker");
                firstBarIndex = -1;
                ChartControl.InvalidateVisual();
            }
        }

        private void CompareMomentumAndDrawSignal(int secondBarIndex, double secondMomentumValue)
        {
            bool isUp = secondMomentumValue > firstMomentumValue;
            signals.Add(new Signal { BarIndex = secondBarIndex, Y = secondMomentumValue, IsUp = isUp });
        }

        private void UpdateResources()
        {
            if (textFormat == null)
                textFormat = new TextFormat(Core.Globals.DirectWriteFactory, "Arial", SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, 14);

            if (textBrush == null || lastValueColor != ValueColor)
            {
                textBrush?.Dispose();
                textBrush = ValueColor.ToDxBrush(RenderTarget);
                lastValueColor = ValueColor;
            }

            if (backgroundBrush == null || lastBackgroundColor != BackgroundColor)
            {
                backgroundBrush?.Dispose();
                backgroundBrush = BackgroundColor.ToDxBrush(RenderTarget);
                lastBackgroundColor = BackgroundColor;
            }
        }

        private void DisposeResources()
        {
            textFormat?.Dispose();
            textBrush?.Dispose();
            backgroundBrush?.Dispose();
            textLayout?.Dispose();
        }

        #region Properties
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Período", Description = "Número de períodos para el cálculo del momentum", Order = 1, GroupName = "Parámetros")]
        public int Period
        {
            get { return period; }
            set { period = Math.Max(1, value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Color de Línea", Description = "Color de la línea del momentum", Order = 2, GroupName = "Estilo")]
        public System.Windows.Media.Brush LineColor { get; set; }

        [Browsable(false)]
        public string LineColorSerializable
        {
            get { return Serialize.BrushToString(LineColor); }
            set { LineColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Estilo de Línea", Description = "Estilo de la línea del momentum", Order = 3, GroupName = "Estilo")]
        public DashStyleHelper LineStyle { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Grosor de Línea", Description = "Grosor de la línea del momentum", Order = 4, GroupName = "Estilo")]
        public int LineWidth { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar Valor", Description = "Mostrar el valor actual del momentum en el gráfico", Order = 5, GroupName = "Visualización")]
        public bool ShowValue { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Color del Valor", Description = "Color del texto del valor", Order = 6, GroupName = "Visualización")]
        public System.Windows.Media.Brush ValueColor { get; set; }

        [Browsable(false)]
        public string ValueColorSerializable
        {
            get { return Serialize.BrushToString(ValueColor); }
            set { ValueColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Color de Fondo", Description = "Color de fondo del valor", Order = 7, GroupName = "Visualización")]
        public System.Windows.Media.Brush BackgroundColor { get; set; }

        [Browsable(false)]
        public string BackgroundColorSerializable
        {
            get { return Serialize.BrushToString(BackgroundColor); }
            set { BackgroundColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Activar Comparación", Description = "Habilita la comparación de momentum entre dos velas con clics del ratón.", Order = 8, GroupName = "Parámetros")]
        public bool EnableComparison { get; set; }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private CustomMomentum[] cacheCustomMomentum;
		public CustomMomentum CustomMomentum(int period, bool enableComparison)
		{
			return CustomMomentum(Input, period, enableComparison);
		}

		public CustomMomentum CustomMomentum(ISeries<double> input, int period, bool enableComparison)
		{
			if (cacheCustomMomentum != null)
				for (int idx = 0; idx < cacheCustomMomentum.Length; idx++)
					if (cacheCustomMomentum[idx] != null && cacheCustomMomentum[idx].Period == period && cacheCustomMomentum[idx].EnableComparison == enableComparison && cacheCustomMomentum[idx].EqualsInput(input))
						return cacheCustomMomentum[idx];
			return CacheIndicator<CustomMomentum>(new CustomMomentum(){ Period = period, EnableComparison = enableComparison }, input, ref cacheCustomMomentum);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.CustomMomentum CustomMomentum(int period, bool enableComparison)
		{
			return indicator.CustomMomentum(Input, period, enableComparison);
		}

		public Indicators.CustomMomentum CustomMomentum(ISeries<double> input, int period, bool enableComparison)
		{
			return indicator.CustomMomentum(input, period, enableComparison);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.CustomMomentum CustomMomentum(int period, bool enableComparison)
		{
			return indicator.CustomMomentum(Input, period, enableComparison);
		}

		public Indicators.CustomMomentum CustomMomentum(ISeries<double> input, int period, bool enableComparison)
		{
			return indicator.CustomMomentum(input, period, enableComparison);
		}
	}
}

#endregion