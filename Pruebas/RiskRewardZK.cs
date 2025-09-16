//////////////////// ZK_Moded by Zacarías Satrústegui. Telegram @FJ222   //////////////////////////////////////////////////////////////////////////////////////////////////////////////
///
///           Recompensas/Reward to THK2aGfnMLS7jx6n6pbeExu2P6hHhrMCSn (Tron Net)  |  0x72AFf83fbB071d2F2C9656De9F1ADE9d6D70d58a (Ethereum Net)     
///           
// 10-02-2024 - v1.0. Añadido clculo de contratos + puntos + riesgo
// 15-03-2024 - v1.1. Ahora establece automáticamente los contratos calculados en el ChartTrader. Tiene en cuenta si está en un Instrumento Micro.
// 19-03-2024 - v1.2. Al eliminar el RiskReward del gráfico se setean los contratos a 1 para evitar problemas.
// 29-03-2024 - v1.3. Añadida opción en propiedades de activar/desactivar el autorelleno de contratos en el ChartTrader + contratos máximos.
// 12-05-2024 - v1.4. Añadidos múltiples TPs intermedios entre la entrada y TP final. 
// 13-05-2024 - v1.5. Ahora al mover el EntryAnchor inmediatamente se actualizan el SL y el TP, cosa que el RR original de Ninja no hace.
// 14-05-2024 - v1.6. Añadida opción de elegir el color del texto de los TPs intermedios.
// 14-05-2024 - v1.7. Añadida opción de ocultar texto de los TPs y SL.
// 25-05-2024 - v1.8. Se cambia de público a privado variable List<ChartAnchor> anchorsTPs porque causaba problemas cuando había más de dos RisKReward en pantalla.(se movían a la vez)
// 20-07-2024 - v1.9. Añadido offset para sacar el texto informativo y posibilidad de ponerlo en vertical.
// 25-07-2024 - v1.10. Arreglado texto exterior si estaba de arriba a abajo el RiskReward.
// 06-11-2024 - v1.11. Se incluye límite si superan contratos en cuenta real no estableciéndolos.
// 06-03-2025 - v1.12. Incluído modo minimalista en texto informativo.
// 08-03-2025 - v1.13. Incluído coloreo de zonas de TPs, fuente seleccionable.
// ?? - . Añadir opción de cálculo real según el riesgo seleccionado y no con 1 contrato.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
#region Using declarations

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using System.Runtime.CompilerServices;
using SharpDX.DirectWrite;

#endregion

namespace NinjaTrader.NinjaScript.DrawingTools
{
    public class RiskRewardPlus : DrawingTool, INotifyPropertyChanged
    {
        
        private const int cursorSensitivity = 15;
        private ChartAnchor editingAnchor;
        private double entryPrice;
        private bool needsRatioUpdate = true;
        private double ratio = 2;
        private double risk;
        private double reward;
        private double stopPrice;
        private double targetPrice;
        private double textleftPoint;
        private double textRightPoint;

        //Modificado inicio
        private System.Windows.Media.Brush colorInfo;
        private Gui.Tools.SimpleFont font;

        private NinjaTrader.Gui.Tools.QuantityUpDown quantitySelector;
        private double contratosCalculados = 1;

        public event PropertyChangedEventHandler PropertyChanged;

        ChartControl chartControlx;

        [Browsable(false)]
        private List<ChartAnchor> anchorsTPs { get; set; }


        //Modificado final
        #region PropiedadesUI


        [Browsable(false)]
        private bool DrawTarget { get { return (RiskAnchor != null && !RiskAnchor.IsEditing) || (RewardAnchor != null && !RewardAnchor.IsEditing); } }

        [Display(Order = 1)]
        public ChartAnchor EntryAnchor { get; set; }
        [Display(Order = 2)]
        public ChartAnchor RiskAnchor { get; set; }
        [Browsable(false)]
        public ChartAnchor RewardAnchor { get; set; }

        public override object Icon { get { return Icons.DrawRiskReward; } }

        [Range(0, double.MaxValue)]
        [NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardRatio", GroupName = "NinjaScriptGeneral", Order = 1)]
        public double Ratio
        {
            get { return ratio; }
            set
            {
                if (ratio.ApproxCompare(value) == 0)
                    return;
                ratio = value;
                needsRatioUpdate = true;
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Versión del indicador:", Order = 5, GroupName = "Versión")]
        public string Version
        {
            get { return "1.13"; }
            set { }
        }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Capital total", Order = 20, GroupName = "Parámetros de riesgo")]
        public int CapitalTotal
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Porcentaje a arriesgar", Order = 25, GroupName = "Parámetros de riesgo")]
        public double PorcentajeRiesgo
        { get; set; }


        [NinjaScriptProperty]
        [Display(Name = "Utilizar cantidad fija de riesgo", Description = "Utilizar cantidad fija de riesgo", GroupName = "Parámetros de riesgo", Order = 30)]
        public bool CantidadFijaRiesgoActivo
        { get; set; }


        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Cantidad a arriesgar ($$)", Order = 35, GroupName = "Parámetros de riesgo")]
        public int CantidadEnRiesgo
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Calcular en Micro contratos", Description = "", GroupName = "Parámetros de riesgo", Order = 40)]
        public bool CalcularEnMicrosActivo
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Establecer contratos en ChartTrader", Description = "", GroupName = "Parámetros de riesgo", Order = 50)]
        public bool EstablecerContratosActivo
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Contratos máximos", Order = 60, GroupName = "Parámetros de riesgo")]
        public int ContratosMax
        { get; set; }



        [NinjaScriptProperty]
        [Display(Name = "Limitar contratos cuenta real NO MICRO", Description = "Alertar si se está en cuenta no simulada", GroupName = "Parámetros de riesgo", Order = 70)]
        public bool AlertaContratosCuentaReal
        { get; set; }


        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Contratos máximos cuenta real NO MICRO", Order = 80, GroupName = "Parámetros de riesgo")]
        public int ContratosMaxCuentaReal
        { get; set; }


        // a implementar en el futuro
        [NinjaScriptProperty]
        [Browsable(false)]
        [Display(Name = "Mostrar cálculo monetario real", Description = "Muestra monetariamente los cifras reales y no en base a 1 contrato", GroupName = "Parámetros de riesgo", Order = 100)]
        public bool MostrarCalculosReales
        { get; set; }

        
        


        // Lines


        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolAnchor", GroupName = "NinjaScriptLines", Order = 3)]
        public Stroke AnchorLineStroke { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeEntry", GroupName = "NinjaScriptLines", Order = 6)]
        public Stroke EntryLineStroke { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeRisk", GroupName = "NinjaScriptLines", Order = 4)]
        public Stroke StopLineStroke { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeReward", GroupName = "NinjaScriptLines", Order = 5)]
        public Stroke TargetLineStroke { get; set; }


        // Parámetros visuales


        [Display(ResourceType = typeof(Custom.Resource), Name = "Fuente", GroupName = "Parámetros visuales", Order = 1)]
        public Gui.Tools.SimpleFont Font
        {
            get { return font; }
            set
            {
                font = value;               
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Color de linea informativa", Description = "Color de linea informativa de contratos / riesgo", GroupName = "Parámetros visuales", Order = 2)]
        public Stroke ColorInfoStroke
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Color TPs intermedios", Description = "Color del texto y línea de los TPs intermedios", GroupName = "Parámetros visuales", Order = 5)]
        public Stroke ColorTextTPsStroke
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Color fondo SL", Description = "Color del area SL", GroupName = "Parámetros visuales", Order = 6)]
        public Stroke ColorFondoSL
        { get; set; }


        [NinjaScriptProperty]
        [Display(Name = "Color TP impar", Description = "Color de los TPs impares", GroupName = "Parámetros visuales", Order = 7)]
        public Stroke ColorTPImpares
        { get; set; }



        [NinjaScriptProperty]
        [Display(Name = "Color TP par", Description = "Color de los TPs pares", GroupName = "Parámetros visuales", Order = 8)]
        public Stroke ColorTPpares
        { get; set; }


        [NinjaScriptProperty]
        [Display(Name = "Mostrar RRs intermedios", Description = "Dibuja los TPs y RR intermedios por si se quieren tomar parciales", GroupName = "Parámetros visuales", Order = 10)]
        public bool MostrarRRsIntermedios
        { get; set; }


        [NinjaScriptProperty]
        [Display(Name = "Ocultar texto de los TPs", Description = "No dibuja el texto de los TPs", GroupName = "Parámetros visuales", Order = 15)]
        public bool OcultarTextoTPs
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Ocultar texto del SL", Description = "No dibuja el texto del SL", GroupName = "Parámetros visuales", Order = 20)]
        public bool OcultarTextoSL
        { get; set; }


        [NinjaScriptProperty]
        [Display(Name = "Texto informativo en el exterior", Description = "Escribir fuera la información", GroupName = "Parámetros visuales", Order = 25)]
        public bool TextoInformativoEnExterior
        { get; set; }



        [NinjaScriptProperty]
        [Display(Name = "Texto informativo minimalista", Description = "Menos letras al informar", GroupName = "Parámetros visuales", Order = 35)]
        public bool TextoInformativoMinimalista
        { get; set; }


        [NinjaScriptProperty]
        [Range(-100, 100)]
        [Display(Name = "Offset Y texto", Description = "Mover texto arriba o abajo, en píxeles", Order = 45, GroupName = "Parámetros visuales")]
        public int OffsetYTexto
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Texto informativo en vertical", Description = "Escribir fuera la información", GroupName = "Parámetros visuales", Order = 55)]
        public bool TextoInformativoEnVertical
        { get; set; }

        public override IEnumerable<ChartAnchor> Anchors { get { return new[] { EntryAnchor, RiskAnchor, RewardAnchor }; } }

        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolFibonacciRetracementsExtendLinesRight", GroupName = "NinjaScriptLines", Order = 2)]
        public bool IsExtendedLinesRight { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolFibonacciRetracementsExtendLinesLeft", GroupName = "NinjaScriptLines", Order = 1)]
        public bool IsExtendedLinesLeft { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolTextAlignment", GroupName = "NinjaScriptGeneral", Order = 2)]
        public TextLocation TextAlignment { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRulerYValueDisplayUnit", GroupName = "NinjaScriptGeneral", Order = 3)]
        public ValueUnit DisplayUnit { get; set; }
        
        public override bool SupportsAlerts { get { return true; } }


        #endregion
        private void DrawPriceText(ChartAnchor anchor, Point point, double price, ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale)
        {
            if (TextAlignment == TextLocation.Off)
                return;

            string priceString;
            ChartBars chartBars = GetAttachedToChartBars();

            // bars can be null while chart is initializing
            if (chartBars == null)
                return;

            // NS can change ChartAnchor price via Draw method or directly meaning we needed to resync price before drawing
            if (!IsUserDrawn)
                price = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(anchor.Price);

            priceString = GetPriceString(price, chartBars);

            
            if (MostrarCalculosReales)
            {
                priceString = GetPriceString(price, chartBars);
            }

            Stroke color;
            textleftPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
            textRightPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale).X;

            if (anchor == RewardAnchor)
            {
                color = TargetLineStroke;
                priceString = string.Format("{0}  (1:{1})", priceString, Ratio);
                //Modificado inicio
                if (OcultarTextoTPs)
                {
                    priceString = "";
                }
                //Modificado fin 

            }
            else if (anchor == RiskAnchor) 
            { 
                color = StopLineStroke;
                //Modificado inicio
                if (OcultarTextoSL)
                {
                    priceString = "";
                }
                //Modificado fin 
            }
            else if (anchor == EntryAnchor)
            {
                color = EntryLineStroke;
                priceString = "";
            }
            else color = AnchorLineStroke;


           // SimpleFont wpfFont = chartControl.Properties.LabelFont ?? new SimpleFont();
            SharpDX.DirectWrite.TextFormat textFormat = Font.ToDirectWriteTextFormat();
            textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
            textFormat.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;
            SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, priceString, textFormat, chartPanel.H, textFormat.FontSize);

            if (RiskAnchor.Time <= EntryAnchor.Time)
            {
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textleftPoint; break;
                        case TextLocation.ExtremeRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                        case TextLocation.ExtremeRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                    }
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textleftPoint; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                    }
            }
            else if (RiskAnchor.Time >= EntryAnchor.Time)
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                {
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textRightPoint; break;
                        case TextLocation.ExtremeRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                    }
                }
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                        case TextLocation.ExtremeRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                    }
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textRightPoint; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                    }

            RenderTarget.DrawTextLayout(new SharpDX.Vector2((float)point.X, (float)point.Y), textLayout, color.BrushDX, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
        }

        private void DrawPriceText(ChartAnchor anchor, Point point, string price, ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale)
        {
            if (TextAlignment == TextLocation.Off)
                return;

            string priceString;
            ChartBars chartBars = GetAttachedToChartBars();

            // bars can be null while chart is initializing
            if (chartBars == null)
                return;

            Stroke color;
            textleftPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
            textRightPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale).X;


            color = ColorInfoStroke;
                       

            //SimpleFont wpfFont = chartControl.Properties.LabelFont ?? new SimpleFont();
            SharpDX.DirectWrite.TextFormat textFormat = Font.ToDirectWriteTextFormat();
            textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
            textFormat.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;
            SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, price, textFormat, chartPanel.H, textFormat.FontSize);

            if (RiskAnchor.Time <= EntryAnchor.Time)
            {
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textleftPoint; break;
                        case TextLocation.ExtremeRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                        case TextLocation.ExtremeRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                    }
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textleftPoint; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                    }
            }
            else if (RiskAnchor.Time >= EntryAnchor.Time)
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                {
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textRightPoint; break;
                        case TextLocation.ExtremeRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                    }
                }
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                        case TextLocation.ExtremeRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                    }
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textRightPoint; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                    }

            if (TextoInformativoEnVertical)
            {
                point.Y = point.Y - textLayout.Metrics.Height / 2;
                RenderTarget.DrawTextLayout(new SharpDX.Vector2((float)point.X, (float)point.Y), textLayout, color.BrushDX, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
            }
            else
            {
                RenderTarget.DrawTextLayout(new SharpDX.Vector2((float)point.X, (float)point.Y), textLayout, color.BrushDX, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
            }
            
        }

        private void DrawPriceText(ChartAnchor anchor, Point point, double price, ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, int x)
        {
            if (TextAlignment == TextLocation.Off)
                return;

            string priceString;
            ChartBars chartBars = GetAttachedToChartBars();

            // bars can be null while chart is initializing
            if (chartBars == null)
                return;

            // NS can change ChartAnchor price via Draw method or directly meaning we needed to resync price before drawing
            if (!IsUserDrawn)
                price = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(anchor.Price);

            priceString = GetPriceString(price, chartBars);

            Stroke color;
            textleftPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
            textRightPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale).X;

            if (anchor == RewardAnchor)
            {
                color = TargetLineStroke;
                priceString = string.Format("{0}  (1:{1})", priceString, Ratio);

            }
            else if (anchor == RiskAnchor) color = StopLineStroke;
            else if (anchor == EntryAnchor) color = EntryLineStroke;               
            else color = ColorTextTPsStroke; //AnchorLineStroke

            //Modificado inicio 

            if (anchor != RiskAnchor && anchor != EntryAnchor)
            {
                if (anchor == RewardAnchor)
                {                   
                    priceString = string.Format("{0}  (1:{1})", priceString, Ratio);
                }
                else
                {
                    priceString = string.Format("{0}  (1:{1})", priceString, x+1);
                }
            }
            if (OcultarTextoTPs)
            {
                priceString = "";
            }
            //Modificado fin 


            //SimpleFont wpfFont = chartControl.Properties.LabelFont ?? new SimpleFont();
            SharpDX.DirectWrite.TextFormat textFormat = Font.ToDirectWriteTextFormat();
            textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
            textFormat.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;
            SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, priceString, textFormat, chartPanel.H, textFormat.FontSize);

            if (RiskAnchor.Time <= EntryAnchor.Time)
            {
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textleftPoint; break;
                        case TextLocation.ExtremeRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                        case TextLocation.ExtremeRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                    }
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textleftPoint; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                    }
            }
            else if (RiskAnchor.Time >= EntryAnchor.Time)
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                {
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textRightPoint; break;
                        case TextLocation.ExtremeRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                    }
                }
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                        case TextLocation.ExtremeRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                    }
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textRightPoint; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                    }

            RenderTarget.DrawTextLayout(new SharpDX.Vector2((float)point.X, (float)point.Y), textLayout, color.BrushDX, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
        }
        public override IEnumerable<AlertConditionItem> GetAlertConditionItems()
        {
            return Anchors.Select(anchor => new AlertConditionItem
            {
                Name = anchor.DisplayName,
                ShouldOnlyDisplayName = true,
                Tag = anchor
            });
        }

        public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, Point point)
        {
            switch (DrawingState)
            {
                case DrawingState.Building: return Cursors.Pen;
                case DrawingState.Moving: return IsLocked ? Cursors.No : Cursors.SizeAll;
                case DrawingState.Editing: return IsLocked ? Cursors.No : (editingAnchor == EntryAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE);
                default:
                    // draw move cursor if cursor is near line path anywhere
                    Point entryAnchorPixelPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);

                    // see if we are near an anchor right away. this is is cheap so no big deal to do often
                    ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);

                    if (closest != null)
                        return IsLocked ? Cursors.Arrow : (closest == EntryAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE);

                    Point stopAnchorPixelPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
                    Vector anchorsVector = stopAnchorPixelPoint - entryAnchorPixelPoint;

                    // see if the mouse is along one of our lines for moving
                    if (MathHelper.IsPointAlongVector(point, entryAnchorPixelPoint, anchorsVector, cursorSensitivity))
                        return IsLocked ? Cursors.Arrow : Cursors.SizeAll;

                    if (!DrawTarget)
                        return null;

                    Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
                    Vector targetToEntryVector = targetPoint - entryAnchorPixelPoint;
                    return MathHelper.IsPointAlongVector(point, entryAnchorPixelPoint, targetToEntryVector, cursorSensitivity) ? (IsLocked ? Cursors.Arrow : Cursors.SizeAll) : null;
            }
        }

        private string GetPriceString(double price, ChartBars chartBars)
        {
            string priceString;
            double yValueEntry = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            double tickSize = AttachedTo.Instrument.MasterInstrument.TickSize;
            double pointValue = AttachedTo.Instrument.MasterInstrument.PointValue;
            switch (DisplayUnit)
            {
                case ValueUnit.Currency:
                    if (AttachedTo.Instrument.MasterInstrument.InstrumentType == InstrumentType.Forex)
                    {
                        priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue * Account.All[0].ForexLotSize)) :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue * Account.All[0].ForexLotSize));
                    }
                    else
                    { //modificado inicio
                        if (MostrarCalculosReales)
                        {
                            priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue)) :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue));
                        }
                        else  //modificado fin
                        {
                            priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue)) :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue));
                        }
                        
                    }
                    break;
                case ValueUnit.Percent:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / yValueEntry).ToString("P", Core.Globals.GeneralOptions.CurrentCulture) :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / yValueEntry).ToString("P", Core.Globals.GeneralOptions.CurrentCulture);
                    break;
                case ValueUnit.Ticks:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize).ToString("F0") :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize).ToString("F0");
                    break;
                case ValueUnit.Pips:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize / 10).ToString("F0") :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize / 10).ToString("F0");
                    break;
                default:
                    priceString = chartBars.Bars.Instrument.MasterInstrument.FormatPrice(price);
                    break;
            }
            return priceString;
        }

        private string GetPriceString(double price, ChartBars chartBars, int contratos)
        {
            string priceString;
            double yValueEntry = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            double tickSize = AttachedTo.Instrument.MasterInstrument.TickSize;
            double pointValue = AttachedTo.Instrument.MasterInstrument.PointValue;
            switch (DisplayUnit)
            {
                case ValueUnit.Currency:
                    if (AttachedTo.Instrument.MasterInstrument.InstrumentType == InstrumentType.Forex)
                    {
                        priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue * Account.All[0].ForexLotSize)) :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue * Account.All[0].ForexLotSize));
                    }
                    else
                    { //modificado inicio
                        if (MostrarCalculosReales)
                        {
                            priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue * contratos)) :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue * contratos));
                        }
                        else  //modificado fin
                        {
                            priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue)) :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue));
                        }

                    }
                    break;
                case ValueUnit.Percent:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / yValueEntry).ToString("P", Core.Globals.GeneralOptions.CurrentCulture) :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / yValueEntry).ToString("P", Core.Globals.GeneralOptions.CurrentCulture);
                    break;
                case ValueUnit.Ticks:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize).ToString("F0") :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize).ToString("F0");
                    break;
                case ValueUnit.Pips:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize / 10).ToString("F0") :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize / 10).ToString("F0");
                    break;
                default:
                    priceString = chartBars.Bars.Instrument.MasterInstrument.FormatPrice(price);
                    break;
            }
            return priceString;
        }

        public override Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
        {
            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
            Point entryPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point stopPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);

            if (!DrawTarget)
                return new[] { entryPoint, stopPoint };

            Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
            return new[] { entryPoint, stopPoint, targetPoint };
        }

        public override bool IsAlertConditionTrue(AlertConditionItem conditionItem, Condition condition, ChartAlertValue[] values, ChartControl chartControl, ChartScale chartScale)
        {
            // dig up which anchor we are running on to determine line
            ChartAnchor chartAnchor = conditionItem.Tag as ChartAnchor;
            if (chartAnchor == null)
                return false;

            ChartPanel chartPanel = chartControl.ChartPanels[PanelIndex];
            double alertY = chartScale.GetYByValue(chartAnchor.Price);
            Point entryPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point stopPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
            double anchorMinX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Min() : new[] { entryPoint.X, stopPoint.X }.Min();
            double anchorMaxX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Max() : new[] { entryPoint.X, stopPoint.X }.Max();
            double lineStartX = IsExtendedLinesLeft ? chartPanel.X : anchorMinX;
            double lineEndX = IsExtendedLinesRight ? chartPanel.X + chartPanel.W : anchorMaxX;

            // first thing, if our smallest x is greater than most recent bar, we have nothing to do yet.
            // do not try to check Y because lines could cross through stuff
            double firstBarX = chartControl.GetXByTime(values[0].Time);
            double firstBarY = chartScale.GetYByValue(values[0].Value);

            if (lineEndX < firstBarX) // bars passed our drawing tool
                return false;

            Point lineStartPoint = new Point(lineStartX, alertY);
            Point lineEndPoint = new Point(lineEndX, alertY);

            Point barPoint = new Point(firstBarX, firstBarY);
            // NOTE: 'left / right' is relative to if line was vertical. it can end up backwards too
            MathHelper.PointLineLocation pointLocation = MathHelper.GetPointLineLocation(lineStartPoint, lineEndPoint, barPoint);
            // for vertical things, think of a vertical line rotated 90 degrees to lay flat, where it's normal vector is 'up'
            switch (condition)
            {
                case Condition.Greater: return pointLocation == MathHelper.PointLineLocation.LeftOrAbove;
                case Condition.GreaterEqual: return pointLocation == MathHelper.PointLineLocation.LeftOrAbove || pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.Less: return pointLocation == MathHelper.PointLineLocation.RightOrBelow;
                case Condition.LessEqual: return pointLocation == MathHelper.PointLineLocation.RightOrBelow || pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.Equals: return pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.NotEqual: return pointLocation != MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.CrossAbove:
                case Condition.CrossBelow:
                    Predicate<ChartAlertValue> predicate = v =>
                    {
                        double barX = chartControl.GetXByTime(v.Time);
                        double barY = chartScale.GetYByValue(v.Value);
                        Point stepBarPoint = new Point(barX, barY);
                        // NOTE: 'left / right' is relative to if line was vertical. it can end up backwards too
                        MathHelper.PointLineLocation ptLocation = MathHelper.GetPointLineLocation(lineStartPoint, lineEndPoint, stepBarPoint);
                        if (condition == Condition.CrossAbove)
                            return ptLocation == MathHelper.PointLineLocation.LeftOrAbove;
                        return ptLocation == MathHelper.PointLineLocation.RightOrBelow;
                    };
                    return MathHelper.DidPredicateCross(values, predicate);
            }
            return false;
        }

        public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale, DateTime firstTimeOnChart, DateTime lastTimeOnChart)
        {
            return DrawingState == DrawingState.Building || Anchors.Any(a => a.Time >= firstTimeOnChart && a.Time <= lastTimeOnChart);
        }

        public override void OnCalculateMinMax()
        {
            // It is important to set MinValue and MaxValue to the min/max Y values your drawing tool uses if you want it to support auto scale
            MinValue = double.MaxValue;
            MaxValue = double.MinValue;

            if (!IsVisible)
                return;

            // return min/max values only if something has been actually drawn
            if (Anchors.Any(a => !a.IsEditing))
                foreach (ChartAnchor anchor in Anchors)
                {
                    if (anchor.DisplayName == RewardAnchor.DisplayName && !DrawTarget)
                        continue;

                    MinValue = Math.Min(anchor.Price, MinValue);
                    MaxValue = Math.Max(anchor.Price, MaxValue);
                }
        }

        public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            switch (DrawingState)
            {
                case DrawingState.Building:
                    //Print("OnMouseDown. DrawingState.Building");
                    if (EntryAnchor.IsEditing)
                    {
                        dataPoint.CopyDataValues(EntryAnchor);
                        dataPoint.CopyDataValues(RiskAnchor);
                        EntryAnchor.IsEditing = false;
                        entryPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
                    }
                    else if (RiskAnchor.IsEditing)
                    {
                        //Print("OnMouseDown. DrawingState.Building. RiskAnchor.IsEditing");
                        dataPoint.CopyDataValues(RiskAnchor);
                        RiskAnchor.IsEditing = false;
                        stopPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
                        SetReward();
                        CalcularContratos();
                        // we set the anchor for the target after stop mouse down event via SetReward()
                        //however we need make sure Time is in view when builiding, but always when SetRreward is used..
                        RewardAnchor.Time = EntryAnchor.Time;
                        RewardAnchor.SlotIndex = EntryAnchor.SlotIndex;
                        RewardAnchor.IsEditing = false;
                    }
                    // if the anchors are no longer being edited, set the drawing state to normal and unselect the object
                    if (!EntryAnchor.IsEditing && !RiskAnchor.IsEditing && !RewardAnchor.IsEditing)
                    {
                        //Print("OnMouseDown. DrawingState.Building. No Anchors editing.");
                        DrawingState = DrawingState.Normal;
                        IsSelected = false;
                    }
                    break;
                case DrawingState.Normal:
                    //Print("OnMouseDown. DrawingState.Normal.");
                    Point point = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
                    //find which anchor has been clicked relative to the mouse point and make whichever anchor now editable
                    editingAnchor = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);
                    if (editingAnchor != null)
                    {
                        editingAnchor.IsEditing = true;
                        DrawingState = DrawingState.Editing;
                    }
                    else if (GetCursor(chartControl, chartPanel, chartScale, point) == null)
                    {
                        //Print("OnMouseDown. DrawingState.Normal. GetCursor.");
                        IsSelected = false; // missed
                    }                        
                    else
                    {
                        // didnt click an anchor but on a line so start moving
                        //Print("OnMouseDown. DrawingState.Normal. Setting DrawingState=Moving");
                        DrawingState = DrawingState.Moving;
                    }
                        
                    break;
            }
            if (EstablecerContratosActivo)
            {
                if (chartControl != null)
                {
                    // chart controls run on the UI thread. Dispatcher Invoke is used to access the thread.
                    // Typically, the InvokeAsync() is used access the UI thread asynchronously when it is ready. However, if this information is needed immediately, use Invoke so that this blocks the NinjaScript thread from continuing until this operation is complete.
                    // Beware that using Invoke improperly can result in deadlocks.
                    // This example uses Invoke so that the UI control values are available as the historical data is processing

                    chartControl.Dispatcher.Invoke((Action)(() =>
                    {
                        // the window of the chart
                        Window chartWindow = Window.GetWindow(chartControl.Parent);
                        NinjaTrader.Gui.Tools.AccountSelector chartTraderAccountSelector = Window.GetWindow(chartControl.Parent).FindFirst("ChartTraderControlAccountSelector") as NinjaTrader.Gui.Tools.AccountSelector;


                        // find the ChartTrader quantity selector by AutomationID
                        quantitySelector = chartWindow.FindFirst("ChartTraderControlQuantitySelector") as QuantityUpDown;
                        if (quantitySelector != null)
                        {
                            if (!EsInstrumentoMicro(AttachedTo.Instrument.MasterInstrument.Name) && AlertaContratosCuentaReal)
                            {
                                if (contratosCalculados >= ContratosMaxCuentaReal + 0.5)
                                {
                                    if (chartTraderAccountSelector != null && chartTraderAccountSelector.SelectedAccount != null &&
                                    !chartTraderAccountSelector.SelectedAccount.DisplayName.StartsWith("Sim"))
                                    {
                                        //MessageBox.Show("Contratos máximos en cuenta real superados! ");r
                                        quantitySelector.Value = 1;
                                        return;
                                    }
                                }
                            }
                            quantitySelector.Value = Math.Min(Convert.ToInt32(contratosCalculados), ContratosMax);
                        }

                    }));
                }
            }
            
        }

        public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (IsLocked && DrawingState != DrawingState.Building || !IsVisible)
                return;

            if (DrawingState == DrawingState.Building)
            {                
                if (EntryAnchor.IsEditing)
                {
                    //Print("OnMouseMove. DrawingState.Building. EntryAnchor.IsEditing");
                    dataPoint.CopyDataValues(EntryAnchor);
                }                   
                else if (RiskAnchor.IsEditing)
                {
                    //Print("OnMouseMove. DrawingState.Building. RiskAnchor.IsEditing");
                    dataPoint.CopyDataValues(RiskAnchor);
                }                    
                else if (RewardAnchor.IsEditing)
                {
                    //Print("OnMouseMove. DrawingState.Building. RewardAnchor.IsEditing");
                    dataPoint.CopyDataValues(RewardAnchor);
                }                    
            }
            else if (DrawingState == DrawingState.Editing && editingAnchor != null)
            {
                //Print("OnMouseMove. DrawingState == DrawingState.Editing");
                dataPoint.CopyDataValues(editingAnchor);

                //if (editingAnchor != EntryAnchor) //eliminado del RR original de Ninja. Ahora moviendo EntryAnchor se actualiza instantáneo el TP y SL
                //{
                if (editingAnchor != RewardAnchor && Ratio.ApproxCompare(0) != 0)
                {
                    SetReward();
                    CalcularContratos();
                }
                else if (Ratio.ApproxCompare(0) != 0) //sólo queda editingAnchor = Reward Anchor
                {
                    SetRisk();
                    CalcularContratos();                        
                }

                //}
                if (EstablecerContratosActivo)
                {
                    if (chartControl != null)
                    {
                        // chart controls run on the UI thread. Dispatcher Invoke is used to access the thread.
                        // Typically, the InvokeAsync() is used access the UI thread asynchronously when it is ready. However, if this information is needed immediately, use Invoke so that this blocks the NinjaScript thread from continuing until this operation is complete.
                        // Beware that using Invoke improperly can result in deadlocks.
                        // This example uses Invoke so that the UI control values are available as the historical data is processing

                        chartControl.Dispatcher.Invoke((Action)(() =>
                        {
                            // the window of the chart
                            Window chartWindow = Window.GetWindow(chartControl.Parent);
                            NinjaTrader.Gui.Tools.AccountSelector chartTraderAccountSelector = Window.GetWindow(chartControl.Parent).FindFirst("ChartTraderControlAccountSelector") as NinjaTrader.Gui.Tools.AccountSelector;


                            // find the ChartTrader quantity selector by AutomationID
                            quantitySelector = chartWindow.FindFirst("ChartTraderControlQuantitySelector") as QuantityUpDown;
                            if (quantitySelector != null)
                            {
                                if (!EsInstrumentoMicro(AttachedTo.Instrument.MasterInstrument.Name) && AlertaContratosCuentaReal)
                                {
                                    if (contratosCalculados >= ContratosMaxCuentaReal + 0.5)
                                    {
                                        if (chartTraderAccountSelector != null && chartTraderAccountSelector.SelectedAccount != null &&
                                        !chartTraderAccountSelector.SelectedAccount.DisplayName.StartsWith("Sim"))
                                        {
                                            //MessageBox.Show("Contratos máximos en cuenta real superados! ");r
                                            quantitySelector.Value = 1;
                                            return;
                                        }
                                    }
                                }
                                quantitySelector.Value = Math.Min(Convert.ToInt32(contratosCalculados), ContratosMax);
                            }

                        }));
                    }
                }
                
            }
            else if (DrawingState == DrawingState.Moving)
            {
                //Print("OnMouseMove. DrawingState.Moving");
                foreach (ChartAnchor anchor in Anchors)
                {
                    anchor.MoveAnchor(InitialMouseDownAnchor, dataPoint, chartControl, chartPanel, chartScale, this);
                }
                //inicio modificado
                for (int i = 0; i < anchorsTPs.Count(); i++)
                {
                    anchorsTPs[i].MoveAnchor(InitialMouseDownAnchor, dataPoint, chartControl, chartPanel, chartScale, this);                    
                }
                //fin modificado


            }

            entryPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            stopPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
            targetPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RewardAnchor.Price);
            //inicio modificado
            for (int i = 0; i < anchorsTPs.Count(); i++)
            {
                anchorsTPs[i].Price = AttachedTo.Instrument.MasterInstrument.RoundToTickSize((entryPrice - stopPrice) * (i + 1) + entryPrice);
            }
            //fin modificado


        }
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            //don't set anchors until we're done drawing
            if (DrawingState == DrawingState.Building)
                return;

            //set the drawing state back to normal when mouse is relased
            if (DrawingState == DrawingState.Editing || DrawingState == DrawingState.Moving)
                DrawingState = DrawingState.Normal;
            if (editingAnchor != null)
            {
                if (editingAnchor == EntryAnchor)
                {
                    //Print("OnMouseUp. editingAnchor == EntryAnchor");
                    SetReward();
                    if (Ratio.ApproxCompare(0) != 0)
                    {
                        SetRisk();
                    }
                       
                    CalcularContratos();
                }
                editingAnchor.IsEditing = false;
            }
            editingAnchor = null;
            if (EstablecerContratosActivo)
            {
                if (chartControl != null)
                {
                    // chart controls run on the UI thread. Dispatcher Invoke is used to access the thread.
                    // Typically, the InvokeAsync() is used access the UI thread asynchronously when it is ready. However, if this information is needed immediately, use Invoke so that this blocks the NinjaScript thread from continuing until this operation is complete.
                    // Beware that using Invoke improperly can result in deadlocks.
                    // This example uses Invoke so that the UI control values are available as the historical data is processing

                    chartControl.Dispatcher.Invoke((Action)(() =>
                    {
                        // the window of the chart
                        Window chartWindow = Window.GetWindow(chartControl.Parent);
                        NinjaTrader.Gui.Tools.AccountSelector chartTraderAccountSelector = Window.GetWindow(chartControl.Parent).FindFirst("ChartTraderControlAccountSelector") as NinjaTrader.Gui.Tools.AccountSelector;


                        // find the ChartTrader quantity selector by AutomationID
                        quantitySelector = chartWindow.FindFirst("ChartTraderControlQuantitySelector") as QuantityUpDown;
                        if (quantitySelector != null)
                        {
                            if (!EsInstrumentoMicro(AttachedTo.Instrument.MasterInstrument.Name) && AlertaContratosCuentaReal)
                            {
                                if (contratosCalculados >= ContratosMaxCuentaReal + 0.5)
                                {
                                    if (chartTraderAccountSelector != null && chartTraderAccountSelector.SelectedAccount != null &&
                                    !chartTraderAccountSelector.SelectedAccount.DisplayName.StartsWith("Sim"))
                                    {
                                        //MessageBox.Show("Contratos máximos en cuenta real superados! ");r
                                        quantitySelector.Value = 1;
                                        return;
                                    }
                                }
                            }
                            quantitySelector.Value = Math.Min(Convert.ToInt32(contratosCalculados), ContratosMax);
                        }

                    }));
                }
            }
                
        }

        public override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (!IsVisible)
                return;
            if (Anchors.All(a => a.IsEditing))
                return;

            chartControlx = chartControl;

            // this will be true right away to fix a restoral issue, so check if we really want to set reward
            if (needsRatioUpdate && DrawTarget)
            {
                InicializarListaTPsIntermedios();
                SetReward();
            }               

            ChartPanel chartPanel = chartControl.ChartPanels[PanelIndex];
            Point entryPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point stopPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);

            AnchorLineStroke.RenderTarget = RenderTarget;
            EntryLineStroke.RenderTarget = RenderTarget;
            StopLineStroke.RenderTarget = RenderTarget;            
            //Modificado inicio
            ColorInfoStroke.RenderTarget = RenderTarget;
            ColorTextTPsStroke.RenderTarget = RenderTarget;
            ColorFondoSL.RenderTarget = RenderTarget;
            ColorTPImpares.RenderTarget = RenderTarget;
            ColorTPpares.RenderTarget = RenderTarget;
            //Modificado final

            // first of all, turn on anti-aliasing to smooth out our line
            RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.PerPrimitive;
            // linea diagonal del SL a Entry
            RenderTarget.DrawLine(entryPoint.ToVector2(), stopPoint.ToVector2(), AnchorLineStroke.BrushDX, AnchorLineStroke.Width, AnchorLineStroke.StrokeStyle);

            double anchorMinX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Min() : new[] { entryPoint.X, stopPoint.X }.Min();
            double anchorMaxX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Max() : new[] { entryPoint.X, stopPoint.X }.Max();
            double lineStartX = IsExtendedLinesLeft ? chartPanel.X : anchorMinX;
            double lineEndX = IsExtendedLinesRight ? chartPanel.X + chartPanel.W : anchorMaxX;

            SharpDX.Vector2 entryStartVector = new SharpDX.Vector2((float)lineStartX, (float)entryPoint.Y);
            SharpDX.Vector2 entryEndVector = new SharpDX.Vector2((float)lineEndX, (float)entryPoint.Y);
            SharpDX.Vector2 stopStartVector = new SharpDX.Vector2((float)lineStartX, (float)stopPoint.Y);
            SharpDX.Vector2 stopEndVector = new SharpDX.Vector2((float)lineEndX, (float)stopPoint.Y);

            // don't try and draw the target stuff until we have calculated the target
            SharpDX.Direct2D1.Brush tmpBrush = IsInHitTest ? chartControl.SelectionBrush : AnchorLineStroke.BrushDX;
            if (DrawTarget)
            {
                AnchorLineStroke.RenderTarget = RenderTarget;
                // linea diagonal de Entry a TP
                RenderTarget.DrawLine(entryPoint.ToVector2(), targetPoint.ToVector2(), tmpBrush, AnchorLineStroke.Width, AnchorLineStroke.StrokeStyle);

                TargetLineStroke.RenderTarget = RenderTarget;
                SharpDX.Vector2 targetStartVector = new SharpDX.Vector2((float)lineStartX, (float)targetPoint.Y);
                SharpDX.Vector2 targetEndVector = new SharpDX.Vector2((float)lineEndX, (float)targetPoint.Y);

                tmpBrush = IsInHitTest ? chartControl.SelectionBrush : TargetLineStroke.BrushDX;
                // linea del TP
                RenderTarget.DrawLine(targetStartVector, targetEndVector, tmpBrush, TargetLineStroke.Width, TargetLineStroke.StrokeStyle);
                DrawPriceText(RewardAnchor, targetPoint, targetPrice, chartControl, chartPanel, chartScale);

                ////////////////////////// Tps intermedios. Líneas y fondos /////////////////

                //Modificado inicio 
                if (MostrarRRsIntermedios && anchorsTPs != null && anchorsTPs.Count() != 0)
                {
                    Point puntoTpAnterior = new Point();
                    for (int i = 0; i < anchorsTPs.Count(); i++)
                    {                        
                        Point tpPoint = anchorsTPs[i].GetPoint(chartControl, chartPanel, chartScale);
                        //Print(string.Format("i: {0}   x: {1}   y: {2}  | {3}", i, tpPoint.X, tpPoint.Y, anchorsTPs.Count()));
                        SharpDX.Vector2 tpStartVector = new SharpDX.Vector2((float)lineStartX, (float)tpPoint.Y);
                        SharpDX.Vector2 tpEndVector = new SharpDX.Vector2((float)lineEndX, (float)tpPoint.Y);

                        tmpBrush = IsInHitTest ? chartControl.SelectionBrush : ColorTextTPsStroke.BrushDX;
                        RenderTarget.DrawLine(tpStartVector, tpEndVector, tmpBrush, ColorTextTPsStroke.Width, ColorTextTPsStroke.StrokeStyle); 
                        DrawPriceText(anchorsTPs[i], tpPoint, anchorsTPs[i].Price, chartControl, chartPanel, chartScale, i);
                        // Fondos de los TPS 
                        if (i == 0)
                        {
                            SharpDX.RectangleF tpRectangle = new SharpDX.RectangleF(tpStartVector.X, entryStartVector.Y, tpEndVector.X - tpStartVector.X, tpStartVector.Y - entryStartVector.Y);
                            RenderTarget.FillRectangle(tpRectangle, ColorTPImpares.BrushDX);                            
                        }
                        else
                        {
                            if (i % 2 == 0)  //nº par pero los TP van al revés
                            {
                                puntoTpAnterior = anchorsTPs[i-1].GetPoint(chartControl, chartPanel, chartScale);
                                SharpDX.RectangleF tpRectangle = new SharpDX.RectangleF(tpStartVector.X, (float)puntoTpAnterior.Y, (float)(tpEndVector.X - tpStartVector.X), (float)(tpStartVector.Y - puntoTpAnterior.Y));
                                RenderTarget.FillRectangle(tpRectangle, ColorTPImpares.BrushDX);
                            }
                            else
                            {
                                puntoTpAnterior = anchorsTPs[i - 1].GetPoint(chartControl, chartPanel, chartScale);
                                SharpDX.RectangleF tpRectangle = new SharpDX.RectangleF(tpStartVector.X, (float)puntoTpAnterior.Y, (float)(tpEndVector.X - tpStartVector.X), (float)(tpStartVector.Y - puntoTpAnterior.Y));
                                RenderTarget.FillRectangle(tpRectangle, ColorTPpares.BrushDX);
                            }                           
                        }                       
                    }
                    //Después de recorrer todos los TPs intermedios queda dibujar el último rectángulo
                    puntoTpAnterior = anchorsTPs[anchorsTPs.Count() - 1].GetPoint(chartControl, chartPanel, chartScale);
                    SharpDX.RectangleF tpRectangleEnd = new SharpDX.RectangleF(targetStartVector.X, (float)puntoTpAnterior.Y, (float)(targetEndVector.X - targetStartVector.X), (float)(targetStartVector.Y - puntoTpAnterior.Y));

                    if (anchorsTPs.Count() % 2 == 0) 
                    {                        
                        RenderTarget.FillRectangle(tpRectangleEnd, ColorTPImpares.BrushDX);
                    }
                    else
                    {                        
                        RenderTarget.FillRectangle(tpRectangleEnd, ColorTPpares.BrushDX);
                    }
                   
                }               

                ///////////////////////////////////////////////////////

            }

            // linea de Entry
            tmpBrush = IsInHitTest ? chartControl.SelectionBrush : EntryLineStroke.BrushDX;
            RenderTarget.DrawLine(entryStartVector, entryEndVector, tmpBrush, EntryLineStroke.Width, EntryLineStroke.StrokeStyle);
            DrawPriceText(EntryAnchor, entryPoint, entryPrice, chartControl, chartPanel, chartScale);

            tmpBrush = IsInHitTest ? chartControl.SelectionBrush : StopLineStroke.BrushDX;
            // linea roja del SL y área
            RenderTarget.DrawLine(stopStartVector, stopEndVector, tmpBrush, StopLineStroke.Width, StopLineStroke.StrokeStyle);

            SharpDX.RectangleF stopRectangle = new SharpDX.RectangleF(stopStartVector.X, entryStartVector.Y, stopEndVector.X - entryStartVector.X, stopEndVector.Y - entryEndVector.Y);
            RenderTarget.FillRectangle(stopRectangle, ColorFondoSL.BrushDX);


            //Modificado inicio
            Point betweenStoplossandEntry = new Point(0, 0);
            betweenStoplossandEntry.X = (stopPoint.X + entryPoint.X) / 2;
            betweenStoplossandEntry.Y = (stopPoint.Y + entryPoint.Y) / 2;

         
            //Modificado final

            DrawPriceText(RiskAnchor, stopPoint, stopPrice, chartControl, chartPanel, chartScale);

            //Modificado inicio

            double riesgo = CantidadFijaRiesgoActivo ? CantidadEnRiesgo : CapitalTotal * PorcentajeRiesgo / 100;
            string cuantosMicros = "";

            if (EsInstrumentoMicro(AttachedTo.Instrument.MasterInstrument.Name))
            {
                cuantosMicros = Math.Round((riesgo / (Math.Abs(entryPrice - stopPrice) * AttachedTo.Instrument.MasterInstrument.PointValue)), 1).ToString();
                if (TextoInformativoEnVertical)
                {
                    if (TextoInformativoMinimalista) 
                    {
                        cuantosMicros = cuantosMicros + string.Format(" micros\n{0} pts.\n{1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                    }
                    else
                    {
                        cuantosMicros = cuantosMicros + string.Format(" micros\nPuntos: {0}\nRiesgo: {1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                    }
                    
                }
                else
                {
                    if (TextoInformativoMinimalista)
                    {
                        cuantosMicros = cuantosMicros + string.Format(" micros | {0} pts. | {1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                    }
                    else
                    {
                        cuantosMicros = cuantosMicros + string.Format(" micros | Puntos: {0} | Riesgo: {1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                    }
                    
                }
                    
            }
            else
            {
                if (CalcularEnMicrosActivo)
                {
                    cuantosMicros = Math.Round((riesgo / (Math.Abs(entryPrice - stopPrice) * AttachedTo.Instrument.MasterInstrument.PointValue) * 10), 1).ToString();                    
                    if (TextoInformativoEnVertical)
                    {
                        if (TextoInformativoMinimalista)
                        {
                            cuantosMicros = cuantosMicros + string.Format(" micros\n{0} pts.\n{1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                        }
                        else
                        {
                            cuantosMicros = cuantosMicros + string.Format(" micros\nPuntos: {0}\nRiesgo: {1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                        }
                        
                    }
                    else
                    {
                        if (TextoInformativoMinimalista)
                        {
                            cuantosMicros = cuantosMicros + string.Format(" micros | {0} pts. | {1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                        }
                        else
                        {
                            cuantosMicros = cuantosMicros + string.Format(" micros | Puntos: {0} | Riesgo: {1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                        }
                        
                    }
                }
                else
                {
                    cuantosMicros = Math.Round((riesgo / (Math.Abs(entryPrice - stopPrice) * AttachedTo.Instrument.MasterInstrument.PointValue)), 1).ToString();                    
                    if (TextoInformativoEnVertical)
                    {
                        if (TextoInformativoMinimalista)
                        {
                            cuantosMicros = cuantosMicros + string.Format(" minis\n{0} pts.\n{1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                        }
                        else
                        {
                            cuantosMicros = cuantosMicros + string.Format(" minis\nPuntos: {0}\nRiesgo: {1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                        }
                        
                    }
                    else
                    {
                        if (TextoInformativoMinimalista)
                        {
                            cuantosMicros = cuantosMicros + string.Format(" minis | {0} pts. | {1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                        }
                        else
                        {
                            cuantosMicros = cuantosMicros + string.Format(" minis | Puntos: {0} | Riesgo: {1} $", Math.Abs(entryPrice - stopPrice), riesgo);
                        }
                        
                    }
                }
            }
            if (TextoInformativoEnExterior)
            {
                int offsetTextVert = 0;
                if (TextoInformativoEnVertical)
                    offsetTextVert = 20;
                                
                if (stopPoint.Y > entryPoint.Y)
                {
                    stopPoint.Y += (20 + OffsetYTexto + offsetTextVert);
                }
                else
                {
                    stopPoint.Y -= (20 + OffsetYTexto + offsetTextVert + 5);
                }    
                DrawPriceText(RiskAnchor, stopPoint, cuantosMicros, chartControl, chartPanel, chartScale);                
            }
            else
            {
                DrawPriceText(RiskAnchor, betweenStoplossandEntry, cuantosMicros, chartControl, chartPanel, chartScale);                
            }
            
            //Modificado final

        }

        private bool EsInstrumentoMicro(string s)
        {
            if (s.StartsWith("M2K") || s.StartsWith("M6A") || s.StartsWith("M6B") || s.StartsWith("M6B") || s.StartsWith("MBT") ||
                s.StartsWith("MCL") || s.StartsWith("MES") || s.StartsWith("MET") || s.StartsWith("MGC") || s.StartsWith("MHG") ||
                s.StartsWith("MICD") || s.StartsWith("MISF") || s.StartsWith("MMC") || s.StartsWith("MNQ") || s.StartsWith("MSC") ||
                s.StartsWith("MYM"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private void InicializarListaTPsIntermedios()
        {
            anchorsTPs.Clear();
            for (int i = 0; i < Ratio - 1; i++)
            {
                anchorsTPs.Add(new ChartAnchor { IsEditing = true, DrawingTool = this });
            }
        }
      
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void SetReward()
        {
            if (Anchors == null || AttachedTo == null)
                return;
                        
            entryPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            stopPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
            risk = entryPrice - stopPrice;
            reward = risk * Ratio;
            targetPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(entryPrice + reward);

            RewardAnchor.Price = targetPrice;
            RewardAnchor.IsEditing = false;

            needsRatioUpdate = false;
            //inicio modificado
            ChartAnchor item;
            for (int i = 0; i < anchorsTPs.Count(); i++)
            {
                item = anchorsTPs[i];
                item.Price = AttachedTo.Instrument.MasterInstrument.RoundToTickSize((entryPrice - stopPrice) * (i+1) + entryPrice);                
                item.IsEditing = false;
            }
            //fin modificado
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public void SetRisk()
        {
            if (Anchors == null || AttachedTo == null)
                return;            
            entryPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            targetPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RewardAnchor.Price);

            reward = targetPrice - entryPrice;
            risk = reward / Ratio;
            stopPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(entryPrice - risk);

            RiskAnchor.Price = stopPrice;
            RiskAnchor.IsEditing = false;

            needsRatioUpdate = false;

            //inicio modificado
            ChartAnchor item;
            for (int i = 0; i < anchorsTPs.Count(); i++)
            {
                item = anchorsTPs[i];
                item.Price = AttachedTo.Instrument.MasterInstrument.RoundToTickSize((entryPrice - stopPrice) * (i + 1) + entryPrice);
                item.IsEditing = false;
            }
            //fin modificado
        }

        public void CalcularContratos()
        {
            if (Anchors == null || AttachedTo == null)
                return;

            double riesgo = CantidadFijaRiesgoActivo ? CantidadEnRiesgo : CapitalTotal * PorcentajeRiesgo / 100;


            if (!EsInstrumentoMicro(AttachedTo.Instrument.MasterInstrument.Name) && CalcularEnMicrosActivo)
            {
                contratosCalculados = Math.Round((riesgo / (Math.Abs(entryPrice - stopPrice) * AttachedTo.Instrument.MasterInstrument.PointValue) * 10), 1);
            }
            else
            {
                contratosCalculados = Math.Round((riesgo / (Math.Abs(entryPrice - stopPrice) * AttachedTo.Instrument.MasterInstrument.PointValue) * 1), 1);
            }
            if ((contratosCalculados < 1) || (contratosCalculados > 500)) contratosCalculados = 1;
        }
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = Custom.Resource.NinjaScriptDrawingToolRiskRewardDescription;
                Name = "RiskRewardZK";
                Ratio = 2;
                AnchorLineStroke = new Stroke(Brushes.DarkGray, DashStyleHelper.Solid, 1f, 50);
                EntryLineStroke = new Stroke(Brushes.Goldenrod, DashStyleHelper.Solid, 2f);
                StopLineStroke = new Stroke(Brushes.Crimson, DashStyleHelper.Solid, 2f);
                TargetLineStroke = new Stroke(Brushes.SeaGreen, DashStyleHelper.Solid, 2f);
                EntryAnchor = new ChartAnchor { IsEditing = true, DrawingTool = this };
                RiskAnchor = new ChartAnchor { IsEditing = true, DrawingTool = this };
                RewardAnchor = new ChartAnchor { IsEditing = true, DrawingTool = this };
                EntryAnchor.DisplayName = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorEntry;
                RiskAnchor.DisplayName = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorRisk;
                RewardAnchor.DisplayName = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorReward;
                CapitalTotal = 100000;
                PorcentajeRiesgo = 0.5;
                CantidadFijaRiesgoActivo = false;
                CantidadEnRiesgo = 100;
                CalcularEnMicrosActivo = true;
                EstablecerContratosActivo = true;
                ContratosMax = 100;
                AlertaContratosCuentaReal = true;
                ContratosMaxCuentaReal = 3;

                ColorInfoStroke = new Stroke(Brushes.White, DashStyleHelper.Solid, 2f);
                ColorTextTPsStroke = new Stroke(Brushes.SeaGreen, DashStyleHelper.Solid, 2f);
                ColorFondoSL = new Stroke(Brushes.Crimson, DashStyleHelper.Solid, 2f, 20);
                ColorTPImpares = new Stroke(Brushes.SeaGreen, DashStyleHelper.Solid, 2f, 20);
                ColorTPpares = new Stroke(Brushes.YellowGreen, DashStyleHelper.Solid, 2f, 20);
                anchorsTPs = new List<ChartAnchor>();
                MostrarRRsIntermedios = false;
                OcultarTextoTPs = false;
                OcultarTextoSL = false;
                MostrarCalculosReales = false;
                TextoInformativoEnExterior = false;
                OffsetYTexto = 0;
                TextoInformativoEnVertical = false;
                TextoInformativoMinimalista = false;
                Font = new Gui.Tools.SimpleFont() { Size = 12 };

            }
            else if (State == State.Configure)
            {
                InicializarListaTPsIntermedios();
            }
            else if (State == State.Terminated)
            {

                if (chartControlx != null)
                {
                    // chart controls run on the UI thread. Dispatcher Invoke is used to access the thread.
                    // Typically, the InvokeAsync() is used access the UI thread asynchronously when it is ready. However, if this information is needed immediately, use Invoke so that this blocks the NinjaScript thread from continuing until this operation is complete.
                    // Beware that using Invoke improperly can result in deadlocks.
                    // This example uses Invoke so that the UI control values are available as the historical data is processing
                    if (EstablecerContratosActivo)
                    {
                        chartControlx.Dispatcher.Invoke((Action)(() =>
                        {
                            // the window of the chart
                            Window chartWindow = Window.GetWindow(chartControlx.Parent);


                            // find the ChartTrader quantity selector by AutomationID
                            quantitySelector = chartWindow.FindFirst("ChartTraderControlQuantitySelector") as QuantityUpDown;
                            if (quantitySelector != null)
                            {
                                quantitySelector.Value = 1;
                            }
                        }));
                    }
                }
                Dispose();
            }


        }
    }
}