#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq; // Needed for FirstOrDefault(), Contains(), Any(), ToList(), etc.
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls; // Needed for Button, ToolBar, ToolBarTray, Separator
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
using SharpDX.DirectWrite; // For Text rendering
using SharpDX;              // For Vector2 etc.
//using SharpDX.Direct2D1;

// Explicit using alias to resolve ambiguity
using SDX_TextAlignment = SharpDX.DirectWrite.TextAlignment;
using SW_TextAlignment = System.Windows.TextAlignment;
using SDX_FontWeight = SharpDX.DirectWrite.FontWeight;
using SW_FontWeight = System.Windows.FontWeight;
using SDX_FontStyle = SharpDX.DirectWrite.FontStyle;
//using SW_FontStyle = System.Windows.FontStyle;
using SDX_FontStretch = SharpDX.DirectWrite.FontStretch;
//using SW_FontStretch = System.Windows.FontStretch;
using NinjaTrader.NinjaScript.Indicators.PropTraderz;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators.PropTraderz
{
    public class GoldenSetupV4 : Indicator
    {
        #region Variables & Properties

        // --- NUEVO: Variable para guardar referencia a la ventana del Chart ---
        private Chart chartWindow;
        // --- FIN NUEVO ---

        // --- Botón y Visibilidad ---
        private Button toggleButton;
        private bool elementsVisible = true; // Flag para líneas Y señales
        private bool isToggleButtonAdded = false;

        // --- Gestión Directa de Líneas ---
        private Dictionary<string, HorizontalLine> drawnLevelLines = new Dictionary<string, HorizontalLine>();

        // --- Parámetros de Niveles Operables ---
        [NinjaScriptProperty] [Display(Name = "Trade xxx00 Levels?", GroupName = "02. Traded Golden Levels", Order = 10)] public bool Tradexxx00Level { get; set; }
        [NinjaScriptProperty] [Display(Name = "Trade xxx12 Levels?", GroupName = "02. Traded Golden Levels", Order = 20)] public bool Tradexxx12Level { get; set; }
        [NinjaScriptProperty] [Display(Name = "Trade xxx26 Levels?", GroupName = "02. Traded Golden Levels", Order = 30)] public bool Tradexxx26Level { get; set; }
        [NinjaScriptProperty] [Display(Name = "Trade xxx33 Levels?", GroupName = "02. Traded Golden Levels", Order = 40)] public bool Tradexxx33Level { get; set; }
        [NinjaScriptProperty] [Display(Name = "Trade xxx50 Levels?", GroupName = "02. Traded Golden Levels", Order = 50)] public bool Tradexxx50Level { get; set; }
        [NinjaScriptProperty] [Display(Name = "Trade xxx62 Levels?", GroupName = "02. Traded Golden Levels", Order = 60)] public bool Tradexxx62Level { get; set; }
        [NinjaScriptProperty] [Display(Name = "Trade xxx77 Levels?", GroupName = "02. Traded Golden Levels", Order = 70)] public bool Tradexxx77Level { get; set; }
        [NinjaScriptProperty] [Display(Name = "Trade xxx88 Levels?", GroupName = "02. Traded Golden Levels", Order = 80)] public bool Tradexxx88Level { get; set; }

        // --- Parámetros de Texto/Etiquetas ---
        [NinjaScriptProperty] [Display(Name = "Text position Side", Order = 0, GroupName = "Parameters")] public SideEnumv4 Side { get; set; }
        [NinjaScriptProperty] [Display(Name = "Text position", Order = 1, GroupName = "Parameters")] public PositionEnumv4 Position { get; set; }
        [NinjaScriptProperty] [Range(1, 120)] [Display(Name = "Text Size", Order = 2, GroupName = "Parameters")] public int Size { get; set; }
        [NinjaScriptProperty] [Display(Name = "Text Bold Style", Order = 3, GroupName = "Parameters")] public bool Bold { get; set; }
        [NinjaScriptProperty] [Range(0, 255)] [Display(Name = "Text Opacity", Order = 4, GroupName = "Parameters")] public byte Opacity { get; set; }

        // --- Parámetros de Definición de Niveles ---
        [NinjaScriptProperty] [Range(0, 100)] [Display(Name = "LevelA", Order = 10, GroupName = "Parameters")] public int LevelA { get; set; } // 0
        [NinjaScriptProperty] [Range(0, 100)] [Display(Name = "LevelB", Order = 11, GroupName = "Parameters")] public int LevelB { get; set; } // 26
        [NinjaScriptProperty] [Range(0, 100)] [Display(Name = "LevelC", Order = 12, GroupName = "Parameters")] public int LevelC { get; set; } // 50
        [NinjaScriptProperty] [Range(0, 100)] [Display(Name = "LevelD", Order = 13, GroupName = "Parameters")] public int LevelD { get; set; } // 77
        [NinjaScriptProperty] [Range(0, 100)] [Display(Name = "LevelE", Order = 14, GroupName = "Parameters")] public int LevelE { get; set; } // 12
        [NinjaScriptProperty] [Range(0, 100)] [Display(Name = "LevelF", Order = 15, GroupName = "Parameters")] public int LevelF { get; set; } // 33
        [NinjaScriptProperty] [Range(0, 100)] [Display(Name = "LevelG", Order = 16, GroupName = "Parameters")] public int LevelG { get; set; } // 62
        [NinjaScriptProperty] [Range(0, 100)] [Display(Name = "LevelH", Order = 17, GroupName = "Parameters")] public int LevelH { get; set; } // 88

        // --- Parámetros de Estilo de Líneas ---
        [NinjaScriptProperty] [Display(Name = "LevelA Stroke", Order = 10, GroupName = "Parameters")] public Stroke LevelALine { get; set; }
        [NinjaScriptProperty] [Display(Name = "LevelB Stroke", Order = 11, GroupName = "Parameters")] public Stroke LevelBLine { get; set; }
        [NinjaScriptProperty] [Display(Name = "LevelC Stroke", Order = 12, GroupName = "Parameters")] public Stroke LevelCLine { get; set; }
        [NinjaScriptProperty] [Display(Name = "LevelD Stroke", Order = 13, GroupName = "Parameters")] public Stroke LevelDLine { get; set; }
        [NinjaScriptProperty] [Display(Name = "LevelE Stroke", Order = 14, GroupName = "Parameters")] public Stroke LevelELine { get; set; }
        [NinjaScriptProperty] [Display(Name = "LevelF Stroke", Order = 15, GroupName = "Parameters")] public Stroke LevelFLine { get; set; }
        [NinjaScriptProperty] [Display(Name = "LevelG Stroke", Order = 16, GroupName = "Parameters")] public Stroke LevelGLine { get; set; }
        [NinjaScriptProperty] [Display(Name = "LevelH Stroke", Order = 17, GroupName = "Parameters")] public Stroke LevelHLine { get; set; }

        // --- Parámetros de Información y Señales Visuales ---
        [NinjaScriptProperty] [Display(Name = "Show Nearest Level", Order = 0, GroupName = "Info")] public bool ShowNearestLevel { get; set; }
        [NinjaScriptProperty] [Display(Name = "Nearest Level Position", Order = 1, GroupName = "Info")] public TextPosition NearestLevelPosition { get; set; } = TextPosition.TopLeft;
        [NinjaScriptProperty] [Display(Name = "Show Entries signals?", Order = 2, GroupName = "Info")] public bool ShowEntrySignal { get; set; }
        [NinjaScriptProperty] [Display(Name = "Show Targeta signals?", Order = 3, GroupName = "Info")] public bool ShowTargetSignal { get; set; }
        [NinjaScriptProperty] [Display(Name = "Text Size", Order = 4, GroupName = "Info")] public int textSignalSize { get; set; }
        [NinjaScriptProperty] [Display(Name = "Text Offset", Order = 5, GroupName = "Info")] public int textSignalOffset { get; set; }
        [NinjaScriptProperty] [Range(0, 100)] [Display(Name = "Text Opacity", Order = 6, GroupName = "Info")] public int textSignalOpacity { get; set; }
        [NinjaScriptProperty] [XmlIgnore] [Display(Name = "Buy Signal Color", Order = 7, GroupName = "Info")] public Brush BuySignalColor { get; set; }
        [Browsable(false)] public string BuySignalColorSerialize { get { return Serialize.BrushToString(BuySignalColor); } set { BuySignalColor = Serialize.StringToBrush(value); } }
        [NinjaScriptProperty] [XmlIgnore] [Display(Name = "Sell Signal Color", Order = 8, GroupName = "Info")] public Brush SellSignalColor { get; set; }
        [Browsable(false)] public string SellSignalColorSerialize { get { return Serialize.BrushToString(SellSignalColor); } set { SellSignalColor = Serialize.StringToBrush(value); } }
        [NinjaScriptProperty] [XmlIgnore] [Display(Name = "Target Signal Color", Order = 9, GroupName = "Info")] public Brush TargetSignalColor { get; set; }
        [Browsable(false)] public string TargetSignalColorSerialize { get { return Serialize.BrushToString(TargetSignalColor); } set { TargetSignalColor = Serialize.StringToBrush(value); } }
        [NinjaScriptProperty] [Display(Name = "Show Watermark", Order = 10, GroupName = "Info")] public bool ShowWatermark { get; set; } = true;
        [Display(Name = "Discord Info", Order = 11, GroupName = "Info")] public string DiscordInfo { get; set; } = "https://discord.gg/NAekcZXbam";

        // --- Internals ---
        private readonly string watermarkText = "PropTraderz";
        private readonly Brush watermarkBrush = Brushes.SeaGreen;
        // private bool isLong, isShort; // Estado de señal no persistente

        #endregion

        #region State Machine & Button Handling

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Golden Levels V4.9 Toolbar Button Pattern"; // Updated Version
                Name = "GoldenSetupV4.3";
                Calculate = Calculate.OnBarClose; IsOverlay = true; IsChartOnly = true; IsAutoScale = false; DisplayInDataBox = true;
                DrawOnPricePanel = true; PaintPriceMarkers = false; BarsRequiredToPlot = 10;

                // --- Valores por Defecto ---
                Side = SideEnumv4.Right; Size = 15; Position = PositionEnumv4.Above; Bold = true; Opacity = 180;
                LevelA = 0; LevelB = 26; LevelC = 50; LevelD = 77; LevelE = 12; LevelF = 33; LevelG = 62; LevelH = 88;
                LevelALine = new Stroke(Brushes.DarkCyan, DashStyleHelper.Solid, 2); LevelBLine = new Stroke(Brushes.Gold, DashStyleHelper.Dash, 4);
                LevelCLine = new Stroke(Brushes.DarkCyan, DashStyleHelper.Solid, 2); LevelDLine = new Stroke(Brushes.Gold, DashStyleHelper.Dash, 4);
                LevelELine = new Stroke(Brushes.Silver, DashStyleHelper.Solid, 2); LevelFLine = new Stroke(Brushes.Silver, DashStyleHelper.Solid, 2);
                LevelGLine = new Stroke(Brushes.Silver, DashStyleHelper.Solid, 2); LevelHLine = new Stroke(Brushes.Silver, DashStyleHelper.Solid, 2);
                ShowWatermark = true; ShowNearestLevel = true; NearestLevelPosition = TextPosition.TopRight;
                ShowEntrySignal = true; ShowTargetSignal = false; textSignalSize = 12; textSignalOffset = 6; textSignalOpacity = 30;
                BuySignalColor = Brushes.LimeGreen; SellSignalColor = Brushes.Red; TargetSignalColor = Brushes.Cyan;
                Tradexxx00Level = false; Tradexxx12Level = false; Tradexxx26Level = true; Tradexxx33Level = false;
                Tradexxx50Level = false; Tradexxx62Level = false; Tradexxx77Level = true; Tradexxx88Level = false;

                // --- Plots ---
                AddPlot(new Stroke(Brushes.Transparent, 0), PlotStyle.Dot, "xxx00"); AddPlot(new Stroke(Brushes.Transparent, 0), PlotStyle.Dot, "xxx12"); AddPlot(new Stroke(Brushes.Transparent, 0), PlotStyle.Dot, "xxx26"); AddPlot(new Stroke(Brushes.Transparent, 0), PlotStyle.Dot, "xxx33"); AddPlot(new Stroke(Brushes.Transparent, 0), PlotStyle.Dot, "xxx50"); AddPlot(new Stroke(Brushes.Transparent, 0), PlotStyle.Dot, "xxx62"); AddPlot(new Stroke(Brushes.Transparent, 0), PlotStyle.Dot, "xxx77"); AddPlot(new Stroke(Brushes.Transparent, 0), PlotStyle.Dot, "xxx88");

                // --- Resetear Estado ---
                toggleButton = null; isToggleButtonAdded = false; elementsVisible = true;
                drawnLevelLines = new Dictionary<string, HorizontalLine>();
            //    chartWindow = null;
            }
            else if (State == State.Configure) 
			{ 
				if (drawnLevelLines == null) drawnLevelLines = new Dictionary<string, HorizontalLine>();
			}
            else if (State == State.DataLoaded)
            { 
				ChartControl.Dispatcher.Invoke(() => AddButtonToChart());
			}
			else if (State == State.Transition)
            {
		
			}	
            else if (State == State.Terminated) { // Cleanup logic
		        if (toggleButton != null)
		        {
		            ChartControl.Dispatcher.Invoke(() =>
		            {
		             // Remove the button and its container when closing the chart
		                chartWindow.MainMenu.Remove(toggleButton);
		            });
		            toggleButton = null;
		        }	
                if (drawnLevelLines != null) {
					List<string>keysToRemove=drawnLevelLines.Keys.ToList(); 
					foreach(string key in keysToRemove)
					{
						RemoveDrawObject(key);
					} 
					drawnLevelLines.Clear();
				}
                toggleButton = null; isToggleButtonAdded = false; chartWindow = null; 
			}
        }

	    private void AddButtonToChart()
	    {
	
	        // Create the button
            if (toggleButton == null)
            {
                 toggleButton = new Button {
                    Content = elementsVisible ? "Hide Lvls" : "Show Lvls", Padding = new Thickness(3,0,3,0),
                    Margin = new Thickness(2,0,2,0), ToolTip = "Toggle Golden Setup Levels Visibility",
                    Background = Brushes.DimGray, Foreground = Brushes.White, FontWeight = System.Windows.FontWeights.Bold };
                 toggleButton.Click += ToggleButton_Click;
            }	
	
	        // Add the button to the chart
			ChartControl.Dispatcher.InvokeAsync((Action)(() =>
			{
				//Obtain the Chart on which the indicator is configured
				chartWindow = Window.GetWindow(this.ChartControl.Parent) as Chart;
				if (chartWindow == null)
				{
				  Print("chartWindow == null");
				  return;
				}
	            // Here use the PaintOverlay to add elements to the visualization
				chartWindow.MainMenu.Add(toggleButton);	
	        }));

	    }			
			
        // Manejador del botón
        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            elementsVisible = !elementsVisible; if (toggleButton != null) toggleButton.Content = elementsVisible ? "Hide Lvls" : "Show Lvls";
            if (ChartControl != null) { foreach (var kvp in drawnLevelLines) { if (kvp.Value != null && ChartControl.ChartObjects.Contains(kvp.Value)) { kvp.Value.IsVisible = elementsVisible; } } }
            ForceRefresh();
        }

        #endregion

        #region Drawing Logic
        private Stroke SelectStrokeForLevel(double levelPrice)
        {
            int levelComponent = (int)Math.Round((levelPrice % 100) + 0.0001);
            if (levelComponent == LevelA) return LevelALine; if (levelComponent == LevelB) return LevelBLine; if (levelComponent == LevelC) return LevelCLine; if (levelComponent == LevelD) return LevelDLine; if (levelComponent == LevelE) return LevelELine; if (levelComponent == LevelF) return LevelFLine; if (levelComponent == LevelG) return LevelGLine; if (levelComponent == LevelH) return LevelHLine;
            return LevelALine ?? new Stroke(Brushes.Gray, DashStyleHelper.Dot, 1); // Fallback
        }
        #endregion

        #region Calculation & Signals (OnBarUpdate)
        protected override void OnBarUpdate()
        {
			
		    // --- GUARDA ---
		    if (Instrument == null || Instrument.MasterInstrument == null)
		    {
		         Print("GoldenSetupV4: Instrument not available in OnBarUpdate."); // Opcional: para debug
		         return; // Salir si no hay instrumento
		    }
		    // --- FIN GUARDA ---			
		
            if (CurrentBar < BarsRequiredToPlot) return;
            if (drawnLevelLines == null) { drawnLevelLines = new Dictionary<string, HorizontalLine>(); return; }

            for (int i = 0; i < 8; i++) Values[i][0] = 0;

            if (IsFirstTickOfBar)
            {
                double price = Close[0];
                double currentPriceInt = Math.Floor(price / 100);
                double lowerPriceInt = currentPriceInt - 1;
                double upperPriceInt = currentPriceInt + 1;

                // --- Calcular Niveles Requeridos (+/-100 range) ---
                HashSet<string> requiredLevelTags = new HashSet<string>();
                Dictionary<string, double> levelPrices = new Dictionary<string, double>();
                Action<string, int, double, string> AddRequiredLevel = (keyBase, levelOffset, blockInt, tagPrefix) => {
                    double levelPrice = blockInt * 100 + levelOffset; string tag = tagPrefix + levelPrice.ToString();
                    requiredLevelTags.Add(tag);
                    if(blockInt == currentPriceInt) levelPrices[keyBase] = levelPrice;
                    if(blockInt == upperPriceInt) levelPrices[keyBase + "100"] = levelPrice;
                    if(blockInt == lowerPriceInt) levelPrices[keyBase + "-100"] = levelPrice; };
                foreach (double blockInt in new[] { lowerPriceInt, currentPriceInt, upperPriceInt }) {
                    AddRequiredLevel("A", LevelA, blockInt, "Level"); AddRequiredLevel("B", LevelB, blockInt, "Level"); AddRequiredLevel("C", LevelC, blockInt, "Level"); AddRequiredLevel("D", LevelD, blockInt, "Level");
                    AddRequiredLevel("E", LevelE, blockInt, "Level"); AddRequiredLevel("F", LevelF, blockInt, "Level"); AddRequiredLevel("G", LevelG, blockInt, "Level"); AddRequiredLevel("H", LevelH, blockInt, "Level"); }

                // --- Sincronizar Líneas Dibujadas ---
                List<string> tagsToRemove = drawnLevelLines.Keys.Where(tag => !requiredLevelTags.Contains(tag)).ToList();
                foreach (string tag in tagsToRemove) { RemoveDrawObject(tag); drawnLevelLines.Remove(tag); }

                foreach (string tag in requiredLevelTags) {
                     double levelPrice;
                     if (!double.TryParse(tag.Substring(5), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out levelPrice)) { continue; }
                     Stroke stroke = SelectStrokeForLevel(levelPrice); if (stroke == null || stroke.Brush == null) continue;
                     if (!drawnLevelLines.ContainsKey(tag)) {
                         HorizontalLine newLine = Draw.HorizontalLine(this, tag, levelPrice, stroke.Brush, stroke.DashStyleHelper, (int)stroke.Width);
                         if (newLine != null) { newLine.IsVisible = elementsVisible; drawnLevelLines.Add(tag, newLine); }
                     } else {
                         HorizontalLine existingLine = drawnLevelLines[tag]; bool existsOnChart = ChartControl != null && ChartControl.ChartObjects.Contains(existingLine);
                         if (existingLine != null && existsOnChart) { if(existingLine.IsVisible != elementsVisible) existingLine.IsVisible = elementsVisible; } else {
                              drawnLevelLines.Remove(tag); HorizontalLine newLine = Draw.HorizontalLine(this, tag, levelPrice, stroke.Brush, stroke.DashStyleHelper, (int)stroke.Width);
                              if (newLine != null) { newLine.IsVisible = elementsVisible; drawnLevelLines.Add(tag, newLine); } } } }

                // --- Gestionar Visibilidad de SEÑALES y Texto Fijo ---
                string cbStr = "_" + CurrentBar; bool isL = false; bool isS = false;
                if (elementsVisible) {
                    if (ShowNearestLevel) {
                        double nearestPrice = double.NaN; double minDiff = double.MaxValue;
                        foreach(var kvp in drawnLevelLines) { bool existsOnChart = ChartControl != null && ChartControl.ChartObjects.Contains(kvp.Value); ChartAnchor anchor = kvp.Value?.Anchors?.FirstOrDefault();
                             if(anchor != null && kvp.Value.IsVisible && existsOnChart) { double diff = Math.Abs(anchor.Price - price); if (diff < minDiff) { minDiff = diff; nearestPrice = anchor.Price; } } }
                        if (!double.IsNaN(nearestPrice)) { string format = Instrument.MasterInstrument.TickSize.ToString(); Draw.TextFixed(this, "nstGL", "\nNearest: " + nearestPrice.ToString(format), NearestLevelPosition, Brushes.White, new SimpleFont("Arial", 12), Brushes.Transparent, Brushes.Transparent, 0); } else { RemoveDrawObject("nstGL"); }
                    } else { RemoveDrawObject("nstGL"); }

                    if (ShowEntrySignal) {
                        // ==================================================
                        // == LÓGICA DE SEÑALES (COMPLETADA)              ==
                        // ==================================================
                        double levelE100 = levelPrices.ContainsKey("E") ? levelPrices["E"] + 100 : double.NaN;
                        double levelD_100 = levelPrices.ContainsKey("D") ? levelPrices["D"] - 100 : double.NaN;

                        // --- Señales LONG ---
                        if (Tradexxx00Level && levelPrices.ContainsKey("A") && levelPrices.ContainsKey("H-100") && CrossAbove(Close, levelPrices["A"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossAbove(Close,levelPrices["H-100"],i)){c=true;break;}} if(c){isL=true;Values[0][0]=1;Draw.ArrowUp(this,"L0"+cbStr,false,0,Low[0]-(4*TickSize),BuySignalColor); Draw.Text(this,"LT0"+cbStr,false,"L",0,Low[0]-((textSignalOffset+14)*TickSize),0,BuySignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("E")&&levelPrices.ContainsKey("B")){Draw.Dot(this,"L1T0"+cbStr,false,0,levelPrices["E"],TargetSignalColor);Draw.Text(this,"T1_0"+cbStr,false,"T1",0,levelPrices["E"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"L2T0"+cbStr,false,0,levelPrices["B"],TargetSignalColor);Draw.Text(this,"T2_0"+cbStr,false,"T2",0,levelPrices["B"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx12Level && levelPrices.ContainsKey("E") && levelPrices.ContainsKey("A") && CrossAbove(Close, levelPrices["E"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossAbove(Close,levelPrices["A"],i)){c=true;break;}} if(c){isL=true;Values[1][0]=1;Draw.ArrowUp(this,"L12"+cbStr,false,0,Low[0]-(4*TickSize),BuySignalColor); Draw.Text(this,"LT12"+cbStr,false,"L",0,Low[0]-((textSignalOffset+14)*TickSize),0,BuySignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("B")&&levelPrices.ContainsKey("F")){Draw.Dot(this,"L1T12"+cbStr,false,0,levelPrices["B"],TargetSignalColor);Draw.Text(this,"T1_12"+cbStr,false,"T1",0,levelPrices["B"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"L2T12"+cbStr,false,0,levelPrices["F"],TargetSignalColor);Draw.Text(this,"T2_12"+cbStr,false,"T2",0,levelPrices["F"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx26Level && levelPrices.ContainsKey("B") && levelPrices.ContainsKey("E") && CrossAbove(Close, levelPrices["B"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossAbove(Close,levelPrices["E"],i)){c=true;break;}} if(c){isL=true;Values[2][0]=1;Draw.ArrowUp(this,"L26"+cbStr,false,0,Low[0]-(4*TickSize),BuySignalColor); Draw.Text(this,"LT26"+cbStr,false,"L",0,Low[0]-((textSignalOffset+14)*TickSize),0,BuySignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("F")&&levelPrices.ContainsKey("C")){Draw.Dot(this,"L1T26"+cbStr,false,0,levelPrices["F"],TargetSignalColor);Draw.Text(this,"T1_26"+cbStr,false,"T1",0,levelPrices["F"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"L2T26"+cbStr,false,0,levelPrices["C"],TargetSignalColor);Draw.Text(this,"T2_26"+cbStr,false,"T2",0,levelPrices["C"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx33Level && levelPrices.ContainsKey("F") && levelPrices.ContainsKey("B") && CrossAbove(Close, levelPrices["F"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossAbove(Close,levelPrices["B"],i)){c=true;break;}} if(c){isL=true;Values[3][0]=1;Draw.ArrowUp(this,"L33"+cbStr,false,0,Low[0]-(4*TickSize),BuySignalColor); Draw.Text(this,"LT33"+cbStr,false,"L",0,Low[0]-((textSignalOffset+14)*TickSize),0,BuySignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("C")&&levelPrices.ContainsKey("G")){Draw.Dot(this,"L1T33"+cbStr,false,0,levelPrices["C"],TargetSignalColor);Draw.Text(this,"T1_33"+cbStr,false,"T1",0,levelPrices["C"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"L2T33"+cbStr,false,0,levelPrices["G"],TargetSignalColor);Draw.Text(this,"T2_33"+cbStr,false,"T2",0,levelPrices["G"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx50Level && levelPrices.ContainsKey("C") && levelPrices.ContainsKey("F") && CrossAbove(Close, levelPrices["C"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossAbove(Close,levelPrices["F"],i)){c=true;break;}} if(c){isL=true;Values[4][0]=1;Draw.ArrowUp(this,"L50"+cbStr,false,0,Low[0]-(4*TickSize),BuySignalColor); Draw.Text(this,"LT50"+cbStr,false,"L",0,Low[0]-((textSignalOffset+14)*TickSize),0,BuySignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("G")&&levelPrices.ContainsKey("D")){Draw.Dot(this,"L1T50"+cbStr,false,0,levelPrices["G"],TargetSignalColor);Draw.Text(this,"T1_50"+cbStr,false,"T1",0,levelPrices["G"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"L2T50"+cbStr,false,0,levelPrices["D"],TargetSignalColor);Draw.Text(this,"T2_50"+cbStr,false,"T2",0,levelPrices["D"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx62Level && levelPrices.ContainsKey("G") && levelPrices.ContainsKey("C") && CrossAbove(Close, levelPrices["G"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossAbove(Close,levelPrices["C"],i)){c=true;break;}} if(c){isL=true;Values[5][0]=1;Draw.ArrowUp(this,"L62"+cbStr,false,0,Low[0]-(4*TickSize),BuySignalColor); Draw.Text(this,"LT62"+cbStr,false,"L",0,Low[0]-((textSignalOffset+14)*TickSize),0,BuySignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("D")&&levelPrices.ContainsKey("H")){Draw.Dot(this,"L1T62"+cbStr,false,0,levelPrices["D"],TargetSignalColor);Draw.Text(this,"T1_62"+cbStr,false,"T1",0,levelPrices["D"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"L2T62"+cbStr,false,0,levelPrices["H"],TargetSignalColor);Draw.Text(this,"T2_62"+cbStr,false,"T2",0,levelPrices["H"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx77Level && levelPrices.ContainsKey("D") && levelPrices.ContainsKey("G") && CrossAbove(Close, levelPrices["D"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossAbove(Close,levelPrices["G"],i)){c=true;break;}} if(c){isL=true;Values[6][0]=1;Draw.ArrowUp(this,"L77"+cbStr,false,0,Low[0]-(4*TickSize),BuySignalColor); Draw.Text(this,"LT77"+cbStr,false,"L",0,Low[0]-((textSignalOffset+14)*TickSize),0,BuySignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("H")&&levelPrices.ContainsKey("A100")){Draw.Dot(this,"L1T77"+cbStr,false,0,levelPrices["H"],TargetSignalColor);Draw.Text(this,"T1_77"+cbStr,false,"T1",0,levelPrices["H"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"L2T77"+cbStr,false,0,levelPrices["A100"],TargetSignalColor);Draw.Text(this,"T2_77"+cbStr,false,"T2",0,levelPrices["A100"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx88Level && levelPrices.ContainsKey("H") && levelPrices.ContainsKey("D") && CrossAbove(Close, levelPrices["H"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossAbove(Close,levelPrices["D"],i)){c=true;break;}} if(c){isL=true;Values[7][0]=1;Draw.ArrowUp(this,"L88"+cbStr,false,0,Low[0]-(4*TickSize),BuySignalColor); Draw.Text(this,"LT88"+cbStr,false,"L",0,Low[0]-((textSignalOffset+14)*TickSize),0,BuySignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("A100")&&!double.IsNaN(levelE100)){Draw.Dot(this,"L1T88"+cbStr,false,0,levelPrices["A100"],TargetSignalColor);Draw.Text(this,"T1_88"+cbStr,false,"T1",0,levelPrices["A100"]+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"L2T88"+cbStr,false,0,levelE100,TargetSignalColor);Draw.Text(this,"T2_88"+cbStr,false,"T2",0,levelE100+(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}

                        // --- Señales SHORT ---
                        if (Tradexxx88Level && levelPrices.ContainsKey("H") && levelPrices.ContainsKey("A100") && CrossBelow(Close, levelPrices["H"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossBelow(Close,levelPrices["A100"],i)){c=true;break;}} if(c){isS=true;Values[7][0]=-1;Draw.ArrowDown(this,"S88"+cbStr,false,0,High[0]+(4*TickSize),SellSignalColor); Draw.Text(this,"ST88"+cbStr,false,"S",0,High[0]+((textSignalOffset+14)*TickSize),0,SellSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("D")&&levelPrices.ContainsKey("G")){Draw.Dot(this,"S1T88"+cbStr,false,0,levelPrices["D"],TargetSignalColor);Draw.Text(this,"S_T1_88"+cbStr,false,"T1",0,levelPrices["D"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"S2T88"+cbStr,false,0,levelPrices["G"],TargetSignalColor);Draw.Text(this,"S_T2_88"+cbStr,false,"T2",0,levelPrices["G"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx77Level && levelPrices.ContainsKey("D") && levelPrices.ContainsKey("H") && CrossBelow(Close, levelPrices["D"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossBelow(Close,levelPrices["H"],i)){c=true;break;}} if(c){isS=true;Values[6][0]=-1;Draw.ArrowDown(this,"S77"+cbStr,false,0,High[0]+(4*TickSize),SellSignalColor); Draw.Text(this,"ST77"+cbStr,false,"S",0,High[0]+((textSignalOffset+14)*TickSize),0,SellSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("G")&&levelPrices.ContainsKey("C")){Draw.Dot(this,"S1T77"+cbStr,false,0,levelPrices["G"],TargetSignalColor);Draw.Text(this,"S_T1_77"+cbStr,false,"T1",0,levelPrices["G"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"S2T77"+cbStr,false,0,levelPrices["C"],TargetSignalColor);Draw.Text(this,"S_T2_77"+cbStr,false,"T2",0,levelPrices["C"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx62Level && levelPrices.ContainsKey("G") && levelPrices.ContainsKey("D") && CrossBelow(Close, levelPrices["G"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossBelow(Close,levelPrices["D"],i)){c=true;break;}} if(c){isS=true;Values[5][0]=-1;Draw.ArrowDown(this,"S62"+cbStr,false,0,High[0]+(4*TickSize),SellSignalColor); Draw.Text(this,"ST62"+cbStr,false,"S",0,High[0]+((textSignalOffset+14)*TickSize),0,SellSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("C")&&levelPrices.ContainsKey("F")){Draw.Dot(this,"S1T62"+cbStr,false,0,levelPrices["C"],TargetSignalColor);Draw.Text(this,"S_T1_62"+cbStr,false,"T1",0,levelPrices["C"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"S2T62"+cbStr,false,0,levelPrices["F"],TargetSignalColor);Draw.Text(this,"S_T2_62"+cbStr,false,"T2",0,levelPrices["F"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx50Level && levelPrices.ContainsKey("C") && levelPrices.ContainsKey("G") && CrossBelow(Close, levelPrices["C"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossBelow(Close,levelPrices["G"],i)){c=true;break;}} if(c){isS=true;Values[4][0]=-1;Draw.ArrowDown(this,"S50"+cbStr,false,0,High[0]+(4*TickSize),SellSignalColor); Draw.Text(this,"ST50"+cbStr,false,"S",0,High[0]+((textSignalOffset+14)*TickSize),0,SellSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("F")&&levelPrices.ContainsKey("B")){Draw.Dot(this,"S1T50"+cbStr,false,0,levelPrices["F"],TargetSignalColor);Draw.Text(this,"S_T1_50"+cbStr,false,"T1",0,levelPrices["F"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"S2T50"+cbStr,false,0,levelPrices["B"],TargetSignalColor);Draw.Text(this,"S_T2_50"+cbStr,false,"T2",0,levelPrices["B"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx33Level && levelPrices.ContainsKey("F") && levelPrices.ContainsKey("C") && CrossBelow(Close, levelPrices["F"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossBelow(Close,levelPrices["C"],i)){c=true;break;}} if(c){isS=true;Values[3][0]=-1;Draw.ArrowDown(this,"S33"+cbStr,false,0,High[0]+(4*TickSize),SellSignalColor); Draw.Text(this,"ST33"+cbStr,false,"S",0,High[0]+((textSignalOffset+14)*TickSize),0,SellSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("B")&&levelPrices.ContainsKey("E")){Draw.Dot(this,"S1T33"+cbStr,false,0,levelPrices["B"],TargetSignalColor);Draw.Text(this,"S_T1_33"+cbStr,false,"T1",0,levelPrices["B"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"S2T33"+cbStr,false,0,levelPrices["E"],TargetSignalColor);Draw.Text(this,"S_T2_33"+cbStr,false,"T2",0,levelPrices["E"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx26Level && levelPrices.ContainsKey("B") && levelPrices.ContainsKey("F") && CrossBelow(Close, levelPrices["B"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossBelow(Close,levelPrices["F"],i)){c=true;break;}} if(c){isS=true;Values[2][0]=-1;Draw.ArrowDown(this,"S26"+cbStr,false,0,High[0]+(4*TickSize),SellSignalColor); Draw.Text(this,"ST26"+cbStr,false,"S",0,High[0]+((textSignalOffset+14)*TickSize),0,SellSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("E")&&levelPrices.ContainsKey("A")){Draw.Dot(this,"S1T26"+cbStr,false,0,levelPrices["E"],TargetSignalColor);Draw.Text(this,"S_T1_26"+cbStr,false,"T1",0,levelPrices["E"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"S2T26"+cbStr,false,0,levelPrices["A"],TargetSignalColor);Draw.Text(this,"S_T2_26"+cbStr,false,"T2",0,levelPrices["A"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx12Level && levelPrices.ContainsKey("E") && levelPrices.ContainsKey("B") && CrossBelow(Close, levelPrices["E"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossBelow(Close,levelPrices["B"],i)){c=true;break;}} if(c){isS=true;Values[1][0]=-1;Draw.ArrowDown(this,"S12"+cbStr,false,0,High[0]+(4*TickSize),SellSignalColor); Draw.Text(this,"ST12"+cbStr,false,"S",0,High[0]+((textSignalOffset+14)*TickSize),0,SellSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("A")&&levelPrices.ContainsKey("H-100")){Draw.Dot(this,"S1T12"+cbStr,false,0,levelPrices["A"],TargetSignalColor);Draw.Text(this,"S_T1_12"+cbStr,false,"T1",0,levelPrices["A"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"S2T12"+cbStr,false,0,levelPrices["H-100"],TargetSignalColor);Draw.Text(this,"S_T2_12"+cbStr,false,"T2",0,levelPrices["H-100"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}
                        if (Tradexxx00Level && levelPrices.ContainsKey("A") && levelPrices.ContainsKey("E") && CrossBelow(Close, levelPrices["A"], 1)) { bool c=false; for(int i=1;i<=10&&CurrentBar-i>=BarsRequiredToPlot;i++){if(CrossBelow(Close,levelPrices["E"],i)){c=true;break;}} if(c){isS=true;Values[0][0]=-1;Draw.ArrowDown(this,"S0"+cbStr,false,0,High[0]+(4*TickSize),SellSignalColor); Draw.Text(this,"ST0"+cbStr,false,"S",0,High[0]+((textSignalOffset+14)*TickSize),0,SellSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity); if(ShowTargetSignal&&levelPrices.ContainsKey("H-100")&&!double.IsNaN(levelD_100)){Draw.Dot(this,"S1T0"+cbStr,false,0,levelPrices["H-100"],TargetSignalColor);Draw.Text(this,"S_T1_0"+cbStr,false,"T1",0,levelPrices["H-100"]-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);Draw.Dot(this,"S2T0"+cbStr,false,0,levelD_100,TargetSignalColor);Draw.Text(this,"S_T2_0"+cbStr,false,"T2",0,levelD_100-(textSignalOffset*TickSize),0,TargetSignalColor,new SimpleFont("Arial",textSignalSize),SW_TextAlignment.Center,Brushes.Transparent,Brushes.Transparent,textSignalOpacity);}}}

                        // ==================================================
                        // == FIN: LÓGICA DE SEÑALES                     ==
                        // ==================================================
                    }

                } else { // Ocultar señales y texto
                    RemoveDrawObject("nstGL");
                    // ==================================================
                    // == INICIO: RemoveDrawObject PARA SEÑALES       ==
                    // ==================================================
                     RemoveDrawObject("L0"+cbStr);RemoveDrawObject("LT0"+cbStr);RemoveDrawObject("L1T0"+cbStr);RemoveDrawObject("T1_0"+cbStr);RemoveDrawObject("L2T0"+cbStr);RemoveDrawObject("T2_0"+cbStr);
                     RemoveDrawObject("L12"+cbStr);RemoveDrawObject("LT12"+cbStr);RemoveDrawObject("L1T12"+cbStr);RemoveDrawObject("T1_12"+cbStr);RemoveDrawObject("L2T12"+cbStr);RemoveDrawObject("T2_12"+cbStr);
                     RemoveDrawObject("L26"+cbStr);RemoveDrawObject("LT26"+cbStr);RemoveDrawObject("L1T26"+cbStr);RemoveDrawObject("T1_26"+cbStr);RemoveDrawObject("L2T26"+cbStr);RemoveDrawObject("T2_26"+cbStr);
                     RemoveDrawObject("L33"+cbStr);RemoveDrawObject("LT33"+cbStr);RemoveDrawObject("L1T33"+cbStr);RemoveDrawObject("T1_33"+cbStr);RemoveDrawObject("L2T33"+cbStr);RemoveDrawObject("T2_33"+cbStr);
                     RemoveDrawObject("L50"+cbStr);RemoveDrawObject("LT50"+cbStr);RemoveDrawObject("L1T50"+cbStr);RemoveDrawObject("T1_50"+cbStr);RemoveDrawObject("L2T50"+cbStr);RemoveDrawObject("T2_50"+cbStr);
                     RemoveDrawObject("L62"+cbStr);RemoveDrawObject("LT62"+cbStr);RemoveDrawObject("L1T62"+cbStr);RemoveDrawObject("T1_62"+cbStr);RemoveDrawObject("L2T62"+cbStr);RemoveDrawObject("T2_62"+cbStr);
                     RemoveDrawObject("L77"+cbStr);RemoveDrawObject("LT77"+cbStr);RemoveDrawObject("L1T77"+cbStr);RemoveDrawObject("T1_77"+cbStr);RemoveDrawObject("L2T77"+cbStr);RemoveDrawObject("T2_77"+cbStr);
                     RemoveDrawObject("L88"+cbStr);RemoveDrawObject("LT88"+cbStr);RemoveDrawObject("L1T88"+cbStr);RemoveDrawObject("T1_88"+cbStr);RemoveDrawObject("L2T88"+cbStr);RemoveDrawObject("T2_88"+cbStr);
                     RemoveDrawObject("S0"+cbStr);RemoveDrawObject("ST0"+cbStr);RemoveDrawObject("S1T0"+cbStr);RemoveDrawObject("S_T1_0"+cbStr);RemoveDrawObject("S2T0"+cbStr);RemoveDrawObject("S_T2_0"+cbStr);
                     RemoveDrawObject("S12"+cbStr);RemoveDrawObject("ST12"+cbStr);RemoveDrawObject("S1T12"+cbStr);RemoveDrawObject("S_T1_12"+cbStr);RemoveDrawObject("S2T12"+cbStr);RemoveDrawObject("S_T2_12"+cbStr);
                     RemoveDrawObject("S26"+cbStr);RemoveDrawObject("ST26"+cbStr);RemoveDrawObject("S1T26"+cbStr);RemoveDrawObject("S_T1_26"+cbStr);RemoveDrawObject("S2T26"+cbStr);RemoveDrawObject("S_T2_26"+cbStr);
                     RemoveDrawObject("S33"+cbStr);RemoveDrawObject("ST33"+cbStr);RemoveDrawObject("S1T33"+cbStr);RemoveDrawObject("S_T1_33"+cbStr);RemoveDrawObject("S2T33"+cbStr);RemoveDrawObject("S_T2_33"+cbStr);
                     RemoveDrawObject("S50"+cbStr);RemoveDrawObject("ST50"+cbStr);RemoveDrawObject("S1T50"+cbStr);RemoveDrawObject("S_T1_50"+cbStr);RemoveDrawObject("S2T50"+cbStr);RemoveDrawObject("S_T2_50"+cbStr);
                     RemoveDrawObject("S62"+cbStr);RemoveDrawObject("ST62"+cbStr);RemoveDrawObject("S1T62"+cbStr);RemoveDrawObject("S_T1_62"+cbStr);RemoveDrawObject("S2T62"+cbStr);RemoveDrawObject("S_T2_62"+cbStr);
                     RemoveDrawObject("S77"+cbStr);RemoveDrawObject("ST77"+cbStr);RemoveDrawObject("S1T77"+cbStr);RemoveDrawObject("S_T1_77"+cbStr);RemoveDrawObject("S2T77"+cbStr);RemoveDrawObject("S_T2_77"+cbStr);
                     RemoveDrawObject("S88"+cbStr);RemoveDrawObject("ST88"+cbStr);RemoveDrawObject("S1T88"+cbStr);RemoveDrawObject("S_T1_88"+cbStr);RemoveDrawObject("S2T88"+cbStr);RemoveDrawObject("S_T2_88"+cbStr);
                    // ==================================================
                    // == FIN: RemoveDrawObject PARA SEÑALES         ==
                    // ==================================================
                }
                 // --- Fin Gestionar Visibilidad de SEÑALES ---

            } // Fin IsFirstTickOfBar
        }

        #endregion

        #region Plots Accessors
        [Browsable(false)][XmlIgnore] public Series<double> xxx00 { get { return Values[0]; } }
        [Browsable(false)][XmlIgnore] public Series<double> xxx12 { get { return Values[1]; } }
        [Browsable(false)][XmlIgnore] public Series<double> xxx26 { get { return Values[2]; } }
        [Browsable(false)][XmlIgnore] public Series<double> xxx33 { get { return Values[3]; } }
        [Browsable(false)][XmlIgnore] public Series<double> xxx50 { get { return Values[4]; } }
        [Browsable(false)][XmlIgnore] public Series<double> xxx62 { get { return Values[5]; } }
        [Browsable(false)][XmlIgnore] public Series<double> xxx77 { get { return Values[6]; } }
        [Browsable(false)][XmlIgnore] public Series<double> xxx88 { get { return Values[7]; } }
        #endregion

        #region Rendering (OnRender)

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
             // Llamar primero al base.OnRender
             base.OnRender(chartControl, chartScale);

             // --- GUARDAS ---
             // Asegurar que tenemos todo lo necesario para renderizar
             if (Instrument == null || Instrument.MasterInstrument == null) return;
             if (IsInHitTest || Bars == null || ChartPanel == null || RenderTarget == null || ChartBars == null || ChartBars.Bars == null || drawnLevelLines == null) return;
             // --- FIN GUARDAS ---

            // Renderizar Watermark (si está habilitado)
            if (ShowWatermark) {
                 try {
                     using (var factory = new SharpDX.DirectWrite.Factory(SharpDX.DirectWrite.FactoryType.Shared))
                     using (var textFormat = new TextFormat(factory, "Arial", SDX_FontWeight.Bold, SDX_FontStyle.Normal, SDX_FontStretch.Normal, 25 * 96f / 72f))
                     using (var brush = watermarkBrush.ToDxBrush(RenderTarget)) {
                         if (brush == null) return; // Safety check for brush
                         var textLayout = new TextLayout(factory, watermarkText, textFormat, ChartPanel.W, ChartPanel.H);
                         float xPos = ChartPanel.X + 5; float yPos = ChartPanel.Y + ChartPanel.H - textLayout.Metrics.Height - 5;
                         RenderTarget.DrawTextLayout(new Vector2(xPos, yPos), textLayout, brush);
                     }
                 } catch (Exception ex) { Print($"Error rendering Watermark: {ex.Message}"); }
            }

            // Renderizar Etiquetas de Precio para líneas VISIBLES
            try {
                 // string originalPriceFormat = Instrument.MasterInstrument.TickSize.ToString();
                 string priceFormat = "F2"; // <-- FORMATO SIMPLE PARA DEBUGGING

                 using (var factory = new SharpDX.DirectWrite.Factory(SharpDX.DirectWrite.FactoryType.Shared))
                 using (var textFormat = new TextFormat(factory, "Arial", Bold ? SDX_FontWeight.Bold : SDX_FontWeight.Normal, SDX_FontStyle.Normal, SDX_FontStretch.Normal, Size * 96f / 72f))
                 {
                     // --- DEBUGGING: Flag para imprimir encabezado una vez ---
                     bool headerPrinted = false;
                     // --- FIN DEBUGGING ---

                     foreach (var kvp in drawnLevelLines) {
                         HorizontalLine hLine = kvp.Value;
                         string tag = kvp.Key; // Tag de la línea (ej: "Level12345")
                         bool existsOnChart = ChartControl != null && ChartControl.ChartObjects.Contains(hLine);
                         ChartAnchor anchor = hLine?.Anchors?.FirstOrDefault();

                          // Procesar solo si la línea es válida, visible, y está en el gráfico
                          if (hLine != null && existsOnChart && hLine.IsVisible && anchor != null && hLine.Stroke != null && hLine.Stroke.Brush != null)
                          {
                              double anchorPrice = anchor.Price; // Precio real del ancla en el gráfico
                              float lineY = chartScale.GetYByValue(anchorPrice); // Posición Y en píxeles

                              // Recorte vertical: Solo dibujar etiquetas para líneas DENTRO del panel
                              if (lineY >= ChartPanel.Y && lineY <= ChartPanel.Y + ChartPanel.H)
                              {
                                  double labelPrice = double.NaN; // Precio que se mostrará en la etiqueta (extraído del tag)
                                  string labelText = "TagErr";   // Texto por defecto en caso de error

                                  // Extraer precio del Tag
                                  if (tag != null && tag.StartsWith("Level") && double.TryParse(tag.Substring(5), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out labelPrice))
                                  {
                                       labelText = labelPrice.ToString(priceFormat); // Formatear precio del tag
                                  }
                                  else
                                  {
                                       Print($"OnRender: Invalid tag format or parse error: {tag}");
                                       // Fallback: usar el precio del ancla si el tag falla
                                       labelPrice = anchorPrice;
                                       labelText = anchorPrice.ToString(priceFormat);
                                  }

                                  // --- DEBUGGING: Imprimir valores para comparar ---
                                  if (!headerPrinted) { Print($"--- OnRender Debug (Bar: {CurrentBar}) ---"); headerPrinted = true; }
                                  //Print($"Tag: {tag}, LabelPrice(Tag): {labelPrice:F5}, AnchorPrice: {anchorPrice:F5}, LabelText: {labelText}");
                                  // --- FIN DEBUGGING ---

                                  // Dibujar la etiqueta
                                  using (var textBrush = hLine.Stroke.Brush.ToDxBrush(RenderTarget)) {
                                      if (textBrush == null) continue; // Salir si el pincel es nulo
                                      if (textBrush is SharpDX.Direct2D1.SolidColorBrush solidBrush) { var color = solidBrush.Color; /* color.A = Opacity / 255f; */ solidBrush.Color = color; }

                                      using (var textLayout = new TextLayout(factory, labelText, textFormat, float.MaxValue, float.MaxValue)) {
                                          var metrics = textLayout.Metrics;
                                          if (metrics.Width <= 0 || metrics.Height <= 0) continue; // Evitar dibujar si el tamaño es inválido

                                          float x = (Side == SideEnumv4.Right) ? ChartPanel.X + ChartPanel.W - metrics.Width - 5 : ChartPanel.X + 5;
                                          float y = lineY; // Usar Y de la línea
                                          y += (Position == PositionEnumv4.Above) ? -(metrics.Height + (float)hLine.Stroke.Width / 2.0f) : (float)hLine.Stroke.Width / 2.0f;
                                          y = Math.Max(ChartPanel.Y, Math.Min(y, ChartPanel.Y + ChartPanel.H - metrics.Height)); // Clamp
                                          RenderTarget.DrawTextLayout(new Vector2(x, y), textLayout, textBrush);
                                      } // Fin using textLayout
                                  } // Fin using textBrush
                              } // Fin if lineY in view
                          } // Fin if line valid and visible
                     } // Fin foreach
                 } // Fin using textFormat, factory
            } catch (Exception ex) { Print($"Error rendering GoldenSetupV4 labels: {ex.Message} {ex.StackTrace}"); }
        }

        #endregion

    } // Fin clase GoldenSetupV4

    #region Enums (fuera de la clase principal)
    public enum SideEnumv4 { Left, Right }
    public enum PositionEnumv4 { Below, Above }
    #endregion

} // Fin namespace NinjaTrader.NinjaScript.Indicators.PropTraderz

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private PropTraderz.GoldenSetupV4[] cacheGoldenSetupV4;
		public PropTraderz.GoldenSetupV4 GoldenSetupV4(bool tradexxx00Level, bool tradexxx12Level, bool tradexxx26Level, bool tradexxx33Level, bool tradexxx50Level, bool tradexxx62Level, bool tradexxx77Level, bool tradexxx88Level, SideEnumv4 side, PositionEnumv4 position, int size, bool bold, byte opacity, int levelA, int levelB, int levelC, int levelD, int levelE, int levelF, int levelG, int levelH, Stroke levelALine, Stroke levelBLine, Stroke levelCLine, Stroke levelDLine, Stroke levelELine, Stroke levelFLine, Stroke levelGLine, Stroke levelHLine, bool showNearestLevel, TextPosition nearestLevelPosition, bool showEntrySignal, bool showTargetSignal, int textSignalSize, int textSignalOffset, int textSignalOpacity, Brush buySignalColor, Brush sellSignalColor, Brush targetSignalColor, bool showWatermark)
		{
			return GoldenSetupV4(Input, tradexxx00Level, tradexxx12Level, tradexxx26Level, tradexxx33Level, tradexxx50Level, tradexxx62Level, tradexxx77Level, tradexxx88Level, side, position, size, bold, opacity, levelA, levelB, levelC, levelD, levelE, levelF, levelG, levelH, levelALine, levelBLine, levelCLine, levelDLine, levelELine, levelFLine, levelGLine, levelHLine, showNearestLevel, nearestLevelPosition, showEntrySignal, showTargetSignal, textSignalSize, textSignalOffset, textSignalOpacity, buySignalColor, sellSignalColor, targetSignalColor, showWatermark);
		}

		public PropTraderz.GoldenSetupV4 GoldenSetupV4(ISeries<double> input, bool tradexxx00Level, bool tradexxx12Level, bool tradexxx26Level, bool tradexxx33Level, bool tradexxx50Level, bool tradexxx62Level, bool tradexxx77Level, bool tradexxx88Level, SideEnumv4 side, PositionEnumv4 position, int size, bool bold, byte opacity, int levelA, int levelB, int levelC, int levelD, int levelE, int levelF, int levelG, int levelH, Stroke levelALine, Stroke levelBLine, Stroke levelCLine, Stroke levelDLine, Stroke levelELine, Stroke levelFLine, Stroke levelGLine, Stroke levelHLine, bool showNearestLevel, TextPosition nearestLevelPosition, bool showEntrySignal, bool showTargetSignal, int textSignalSize, int textSignalOffset, int textSignalOpacity, Brush buySignalColor, Brush sellSignalColor, Brush targetSignalColor, bool showWatermark)
		{
			if (cacheGoldenSetupV4 != null)
				for (int idx = 0; idx < cacheGoldenSetupV4.Length; idx++)
					if (cacheGoldenSetupV4[idx] != null && cacheGoldenSetupV4[idx].Tradexxx00Level == tradexxx00Level && cacheGoldenSetupV4[idx].Tradexxx12Level == tradexxx12Level && cacheGoldenSetupV4[idx].Tradexxx26Level == tradexxx26Level && cacheGoldenSetupV4[idx].Tradexxx33Level == tradexxx33Level && cacheGoldenSetupV4[idx].Tradexxx50Level == tradexxx50Level && cacheGoldenSetupV4[idx].Tradexxx62Level == tradexxx62Level && cacheGoldenSetupV4[idx].Tradexxx77Level == tradexxx77Level && cacheGoldenSetupV4[idx].Tradexxx88Level == tradexxx88Level && cacheGoldenSetupV4[idx].Side == side && cacheGoldenSetupV4[idx].Position == position && cacheGoldenSetupV4[idx].Size == size && cacheGoldenSetupV4[idx].Bold == bold && cacheGoldenSetupV4[idx].Opacity == opacity && cacheGoldenSetupV4[idx].LevelA == levelA && cacheGoldenSetupV4[idx].LevelB == levelB && cacheGoldenSetupV4[idx].LevelC == levelC && cacheGoldenSetupV4[idx].LevelD == levelD && cacheGoldenSetupV4[idx].LevelE == levelE && cacheGoldenSetupV4[idx].LevelF == levelF && cacheGoldenSetupV4[idx].LevelG == levelG && cacheGoldenSetupV4[idx].LevelH == levelH && cacheGoldenSetupV4[idx].LevelALine == levelALine && cacheGoldenSetupV4[idx].LevelBLine == levelBLine && cacheGoldenSetupV4[idx].LevelCLine == levelCLine && cacheGoldenSetupV4[idx].LevelDLine == levelDLine && cacheGoldenSetupV4[idx].LevelELine == levelELine && cacheGoldenSetupV4[idx].LevelFLine == levelFLine && cacheGoldenSetupV4[idx].LevelGLine == levelGLine && cacheGoldenSetupV4[idx].LevelHLine == levelHLine && cacheGoldenSetupV4[idx].ShowNearestLevel == showNearestLevel && cacheGoldenSetupV4[idx].NearestLevelPosition == nearestLevelPosition && cacheGoldenSetupV4[idx].ShowEntrySignal == showEntrySignal && cacheGoldenSetupV4[idx].ShowTargetSignal == showTargetSignal && cacheGoldenSetupV4[idx].textSignalSize == textSignalSize && cacheGoldenSetupV4[idx].textSignalOffset == textSignalOffset && cacheGoldenSetupV4[idx].textSignalOpacity == textSignalOpacity && cacheGoldenSetupV4[idx].BuySignalColor == buySignalColor && cacheGoldenSetupV4[idx].SellSignalColor == sellSignalColor && cacheGoldenSetupV4[idx].TargetSignalColor == targetSignalColor && cacheGoldenSetupV4[idx].ShowWatermark == showWatermark && cacheGoldenSetupV4[idx].EqualsInput(input))
						return cacheGoldenSetupV4[idx];
			return CacheIndicator<PropTraderz.GoldenSetupV4>(new PropTraderz.GoldenSetupV4(){ Tradexxx00Level = tradexxx00Level, Tradexxx12Level = tradexxx12Level, Tradexxx26Level = tradexxx26Level, Tradexxx33Level = tradexxx33Level, Tradexxx50Level = tradexxx50Level, Tradexxx62Level = tradexxx62Level, Tradexxx77Level = tradexxx77Level, Tradexxx88Level = tradexxx88Level, Side = side, Position = position, Size = size, Bold = bold, Opacity = opacity, LevelA = levelA, LevelB = levelB, LevelC = levelC, LevelD = levelD, LevelE = levelE, LevelF = levelF, LevelG = levelG, LevelH = levelH, LevelALine = levelALine, LevelBLine = levelBLine, LevelCLine = levelCLine, LevelDLine = levelDLine, LevelELine = levelELine, LevelFLine = levelFLine, LevelGLine = levelGLine, LevelHLine = levelHLine, ShowNearestLevel = showNearestLevel, NearestLevelPosition = nearestLevelPosition, ShowEntrySignal = showEntrySignal, ShowTargetSignal = showTargetSignal, textSignalSize = textSignalSize, textSignalOffset = textSignalOffset, textSignalOpacity = textSignalOpacity, BuySignalColor = buySignalColor, SellSignalColor = sellSignalColor, TargetSignalColor = targetSignalColor, ShowWatermark = showWatermark }, input, ref cacheGoldenSetupV4);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.PropTraderz.GoldenSetupV4 GoldenSetupV4(bool tradexxx00Level, bool tradexxx12Level, bool tradexxx26Level, bool tradexxx33Level, bool tradexxx50Level, bool tradexxx62Level, bool tradexxx77Level, bool tradexxx88Level, SideEnumv4 side, PositionEnumv4 position, int size, bool bold, byte opacity, int levelA, int levelB, int levelC, int levelD, int levelE, int levelF, int levelG, int levelH, Stroke levelALine, Stroke levelBLine, Stroke levelCLine, Stroke levelDLine, Stroke levelELine, Stroke levelFLine, Stroke levelGLine, Stroke levelHLine, bool showNearestLevel, TextPosition nearestLevelPosition, bool showEntrySignal, bool showTargetSignal, int textSignalSize, int textSignalOffset, int textSignalOpacity, Brush buySignalColor, Brush sellSignalColor, Brush targetSignalColor, bool showWatermark)
		{
			return indicator.GoldenSetupV4(Input, tradexxx00Level, tradexxx12Level, tradexxx26Level, tradexxx33Level, tradexxx50Level, tradexxx62Level, tradexxx77Level, tradexxx88Level, side, position, size, bold, opacity, levelA, levelB, levelC, levelD, levelE, levelF, levelG, levelH, levelALine, levelBLine, levelCLine, levelDLine, levelELine, levelFLine, levelGLine, levelHLine, showNearestLevel, nearestLevelPosition, showEntrySignal, showTargetSignal, textSignalSize, textSignalOffset, textSignalOpacity, buySignalColor, sellSignalColor, targetSignalColor, showWatermark);
		}

		public Indicators.PropTraderz.GoldenSetupV4 GoldenSetupV4(ISeries<double> input , bool tradexxx00Level, bool tradexxx12Level, bool tradexxx26Level, bool tradexxx33Level, bool tradexxx50Level, bool tradexxx62Level, bool tradexxx77Level, bool tradexxx88Level, SideEnumv4 side, PositionEnumv4 position, int size, bool bold, byte opacity, int levelA, int levelB, int levelC, int levelD, int levelE, int levelF, int levelG, int levelH, Stroke levelALine, Stroke levelBLine, Stroke levelCLine, Stroke levelDLine, Stroke levelELine, Stroke levelFLine, Stroke levelGLine, Stroke levelHLine, bool showNearestLevel, TextPosition nearestLevelPosition, bool showEntrySignal, bool showTargetSignal, int textSignalSize, int textSignalOffset, int textSignalOpacity, Brush buySignalColor, Brush sellSignalColor, Brush targetSignalColor, bool showWatermark)
		{
			return indicator.GoldenSetupV4(input, tradexxx00Level, tradexxx12Level, tradexxx26Level, tradexxx33Level, tradexxx50Level, tradexxx62Level, tradexxx77Level, tradexxx88Level, side, position, size, bold, opacity, levelA, levelB, levelC, levelD, levelE, levelF, levelG, levelH, levelALine, levelBLine, levelCLine, levelDLine, levelELine, levelFLine, levelGLine, levelHLine, showNearestLevel, nearestLevelPosition, showEntrySignal, showTargetSignal, textSignalSize, textSignalOffset, textSignalOpacity, buySignalColor, sellSignalColor, targetSignalColor, showWatermark);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.PropTraderz.GoldenSetupV4 GoldenSetupV4(bool tradexxx00Level, bool tradexxx12Level, bool tradexxx26Level, bool tradexxx33Level, bool tradexxx50Level, bool tradexxx62Level, bool tradexxx77Level, bool tradexxx88Level, SideEnumv4 side, PositionEnumv4 position, int size, bool bold, byte opacity, int levelA, int levelB, int levelC, int levelD, int levelE, int levelF, int levelG, int levelH, Stroke levelALine, Stroke levelBLine, Stroke levelCLine, Stroke levelDLine, Stroke levelELine, Stroke levelFLine, Stroke levelGLine, Stroke levelHLine, bool showNearestLevel, TextPosition nearestLevelPosition, bool showEntrySignal, bool showTargetSignal, int textSignalSize, int textSignalOffset, int textSignalOpacity, Brush buySignalColor, Brush sellSignalColor, Brush targetSignalColor, bool showWatermark)
		{
			return indicator.GoldenSetupV4(Input, tradexxx00Level, tradexxx12Level, tradexxx26Level, tradexxx33Level, tradexxx50Level, tradexxx62Level, tradexxx77Level, tradexxx88Level, side, position, size, bold, opacity, levelA, levelB, levelC, levelD, levelE, levelF, levelG, levelH, levelALine, levelBLine, levelCLine, levelDLine, levelELine, levelFLine, levelGLine, levelHLine, showNearestLevel, nearestLevelPosition, showEntrySignal, showTargetSignal, textSignalSize, textSignalOffset, textSignalOpacity, buySignalColor, sellSignalColor, targetSignalColor, showWatermark);
		}

		public Indicators.PropTraderz.GoldenSetupV4 GoldenSetupV4(ISeries<double> input , bool tradexxx00Level, bool tradexxx12Level, bool tradexxx26Level, bool tradexxx33Level, bool tradexxx50Level, bool tradexxx62Level, bool tradexxx77Level, bool tradexxx88Level, SideEnumv4 side, PositionEnumv4 position, int size, bool bold, byte opacity, int levelA, int levelB, int levelC, int levelD, int levelE, int levelF, int levelG, int levelH, Stroke levelALine, Stroke levelBLine, Stroke levelCLine, Stroke levelDLine, Stroke levelELine, Stroke levelFLine, Stroke levelGLine, Stroke levelHLine, bool showNearestLevel, TextPosition nearestLevelPosition, bool showEntrySignal, bool showTargetSignal, int textSignalSize, int textSignalOffset, int textSignalOpacity, Brush buySignalColor, Brush sellSignalColor, Brush targetSignalColor, bool showWatermark)
		{
			return indicator.GoldenSetupV4(input, tradexxx00Level, tradexxx12Level, tradexxx26Level, tradexxx33Level, tradexxx50Level, tradexxx62Level, tradexxx77Level, tradexxx88Level, side, position, size, bold, opacity, levelA, levelB, levelC, levelD, levelE, levelF, levelG, levelH, levelALine, levelBLine, levelCLine, levelDLine, levelELine, levelFLine, levelGLine, levelHLine, showNearestLevel, nearestLevelPosition, showEntrySignal, showTargetSignal, textSignalSize, textSignalOffset, textSignalOpacity, buySignalColor, sellSignalColor, targetSignalColor, showWatermark);
		}
	}
}

#endregion
