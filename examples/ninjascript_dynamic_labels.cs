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
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Indicators
{
    public class DynamicLabels : Indicator
    {
        private SharpDX.DirectWrite.TextFormat textFormat;
        private SharpDX.Direct2D1.SolidColorBrush textBrush;
        private Dictionary<DateTime, string> dailyLabels;
        
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Etiquetas que se posicionan dinámicamente";
                Name = "DynamicLabels";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
                
                // Inicializar diccionario para etiquetas diarias
                dailyLabels = new Dictionary<DateTime, string>();
            }
            else if (State == State.DataLoaded)
            {
                // Generar algunas etiquetas de ejemplo por día
                GenerateDailyLabels();
            }
            else if (State == State.Terminated)
            {
                // Limpiar recursos
                if (textFormat != null)
                {
                    textFormat.Dispose();
                    textFormat = null;
                }
                if (textBrush != null)
                {
                    textBrush.Dispose();
                    textBrush = null;
                }
            }
        }
        
        private void GenerateDailyLabels()
        {
            // Ejemplo: crear etiquetas para cada día único en los datos
            for (int i = 0; i < Bars.Count; i++)
            {
                DateTime barDate = Times[0][i].Date;
                if (!dailyLabels.ContainsKey(barDate))
                {
                    // Crear etiqueta personalizada para cada día
                    dailyLabels[barDate] = $"Día: {barDate:dd/MM}";
                }
            }
        }

        protected override void OnBarUpdate()
        {
            // Aquí puedes agregar lógica para actualizar las etiquetas
            // basado en condiciones específicas
        }

        public override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);
            
            // Inicializar recursos de renderizado si no existen
            if (textFormat == null)
            {
                textFormat = new SharpDX.DirectWrite.TextFormat(
                    NinjaTrader.Core.Globals.DirectWriteFactory,
                    "Arial", 12);
            }
            
            if (textBrush == null)
            {
                textBrush = new SharpDX.Direct2D1.SolidColorBrush(
                    RenderTarget, 
                    SharpDX.Color.White);
            }
            
            // Paso 2: Obtener la última barra visible
            int lastVisibleBarIndex = ChartBars.ToIndex;
            
            if (lastVisibleBarIndex < 0 || lastVisibleBarIndex >= Bars.Count)
                return;
            
            // Paso 3: Calcular la posición X base (15 píxeles desde la última barra visible)
            double lastBarPrice = Bars.GetClose(lastVisibleBarIndex);
            float baseXPosition = chartControl.GetXByBarIndex(ChartBars, lastVisibleBarIndex) + 15;
            
            // Paso 4: Dibujar etiquetas para cada día
            DrawDailyLabels(chartControl, chartScale, baseXPosition, lastVisibleBarIndex);
        }
        
        private void DrawDailyLabels(ChartControl chartControl, ChartScale chartScale, 
                                   float baseXPosition, int lastVisibleBarIndex)
        {
            int labelIndex = 0;
            
            // Iterar a través de las etiquetas diarias
            foreach (var kvp in dailyLabels)
            {
                DateTime labelDate = kvp.Key;
                string labelText = kvp.Value;
                
                // Paso 5: Encontrar la primera barra del día para esta etiqueta
                int firstBarOfDay = FindFirstBarOfDay(labelDate);
                
                if (firstBarOfDay == -1) continue;
                
                // Paso 6: Calcular posición Y basada en el precio de la primera barra del día
                double firstBarPrice = Bars.GetClose(firstBarOfDay);
                float yPosition = chartScale.GetYByValue(firstBarPrice);
                
                // Paso 7: Ajustar posición Y para evitar superposición
                yPosition += (labelIndex * 25); // Separar etiquetas verticalmente
                
                // Paso 8: Verificar que la etiqueta esté dentro del área visible
                if (IsLabelVisible(chartControl, baseXPosition, yPosition))
                {
                    // Paso 9: Dibujar la etiqueta
                    DrawLabel(baseXPosition, yPosition, labelText);
                }
                
                labelIndex++;
            }
        }
        
        private int FindFirstBarOfDay(DateTime targetDate)
        {
            // Buscar la primera barra del día especificado
            for (int i = 0; i < Bars.Count; i++)
            {
                if (Times[0][i].Date == targetDate.Date)
                {
                    return i;
                }
            }
            return -1;
        }
        
        private bool IsLabelVisible(ChartControl chartControl, float x, float y)
        {
            // Verificar si la etiqueta está dentro del área visible del gráfico
            return x >= 0 && x <= chartControl.ActualWidth &&
                   y >= 0 && y <= chartControl.ActualHeight;
        }
        
        private void DrawLabel(float x, float y, string text)
        {
            // Paso 10: Crear rectángulo para el texto
            var textRectangle = new SharpDX.RectangleF(x, y, 200, 20);
            
            // Dibujar el texto
            RenderTarget.DrawText(
                text,
                textFormat,
                textRectangle,
                textBrush);
        }
        
        #region Propiedades
        
        [NinjaScriptProperty]
        [Display(Name = "Offset X", Description = "Distancia en píxeles desde la última barra", 
                 Order = 1, GroupName = "Configuración")]
        public int OffsetX { get; set; } = 15;
        
        [NinjaScriptProperty]
        [Display(Name = "Separación Vertical", Description = "Píxeles entre etiquetas", 
                 Order = 2, GroupName = "Configuración")]
        public int VerticalSpacing { get; set; } = 25;
        
        #endregion
    }
}