// Versión 1.0 - 2025-11-16 18:49 - Líneas modificadas: 1 (comentario versión), agregado atributo Gui.CategoryOrder
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
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
	#region Enums
	public enum CalculationMode
	{
		EmaContinua,
		MediaDeSesion,
		EmaReiniciable
	}
	#endregion

    [Gui.CategoryOrder("RyF", 1)]
    public class RVol2_modificado : Indicator
    {
        // Constants for Dictionary cleanup and time matching
        private const int MAX_HISTORICAL_DAYS = 5;
        private const int MAX_DICTIONARY_SIZE = 5000;
        private const int TIME_MATCH_TOLERANCE_MINUTES = 30;
        private const int CLEANUP_FREQUENCY_BARS = 100;

        private int totalVolumeOpacity;
        private double threshold = 0.3;
        private int labelFontSize = 16;
        private int emaPeriod = 21;
        private double lastAD = 0;
        private double highVolumeThreshold = 3.0;
        private double mediumVolumeThreshold = 2.0;
        private double emaMultiplierWhiteDot = 2.0;
        private double emaMultiplierGreenDot = 3.0;
        private double previousDayVolumeMultiplier = 4.0;
        private double previousEmaMultiplier = 2.5;
        private TimeSpan rthSessionStart = new TimeSpan(15, 30, 0); // 15:30 hora de España

        // CVOL2 integration
        private Dictionary<DateTime, double> historicalVolumes = new Dictionary<DateTime, double>();

		// State variables for calculation modes
		private double sessionAccumulatedVolume;
		private int sessionBarCount;
		private bool inSession;
		private double lastCalculationValue;
		private double emaMultiplier;
		private bool isFirstBarOfEmaSession;
		private int lastEmaPeriod = -1;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Indicador de volumen modificado con 3 modos de cálculo para la media móvil.";
                Name = "RVol2 Modificado";
                Calculate = Calculate.OnBarClose;
                IsOverlay = false;
                DisplayInDataBox = true;
                DrawOnPricePanel = false;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = false;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // Default values
                TotalVolumeOpacity = 50;
                Threshold = 0.3;
                LabelFontSize = 16;
                EmaPeriod = 21;
                HighVolumeThreshold = 3.0;
                MediumVolumeThreshold = 2.0;
                EmaMultiplierWhiteDot = 2.0;
                EmaMultiplierGreenDot = 3.0;
                PreviousDayVolumeMultiplier = 4.0;
                PreviousEmaMultiplier = 2.5;

				// Default values for calculation modes
                CalcMode = CalculationMode.EmaContinua;
                SessionStartTime = new TimeSpan(15, 30, 0);
                SessionEndTime = new TimeSpan(22, 0, 0);
                EmaResetTime = new TimeSpan(15, 30, 0);

                // Add plots
                AddPlot(new Stroke(Brushes.CornflowerBlue, 8), PlotStyle.Bar, "TotalVolume");
                AddPlot(new Stroke(Brushes.ForestGreen, 3), PlotStyle.Bar, "EffectiveVolume");
                AddPlot(new Stroke(Brushes.Orange, 2), PlotStyle.Line, "VolumeEMA");
            }
            else if (State == State.Configure)
            {
                // Apply opacity to total volume plot (Plot 0)
                byte alpha = (byte)((TotalVolumeOpacity / 100.0) * 255);
                Plots[0].Brush = new SolidColorBrush(Color.FromArgb(alpha, Colors.CornflowerBlue.R, Colors.CornflowerBlue.G, Colors.CornflowerBlue.B));
                Plots[0].Brush.Freeze();
            }
            else if (State == State.DataLoaded)
            {
				// Initialize state variables
				sessionAccumulatedVolume = 0;
				sessionBarCount = 0;
				inSession = false;
				lastCalculationValue = 0;
				emaMultiplier = 2.0 / (EmaPeriod + 1);
				isFirstBarOfEmaSession = true;
            }
            else if (State == State.Terminated)
            {
                // Cleanup resources
                if (historicalVolumes != null)
                {
                    historicalVolumes.Clear();
                    historicalVolumes = null;
                }
            }
        }

        protected override void OnBarUpdate()
        {
            try
            {
				// Si el periodo de la EMA cambia, reiniciar las variables de cálculo.
				if (EmaPeriod != lastEmaPeriod)
				{
					emaMultiplier = 2.0 / (EmaPeriod + 1);
					isFirstBarOfEmaSession = true;
					lastCalculationValue = 0;
					lastEmaPeriod = EmaPeriod;
				}

                if (CurrentBar < 1)
                    return;

                double volume = Volume[0];

                // Calculate hi: max between previous close and current high
                double hi = Math.Max(Close[1], High[0]);

                // Calculate lo: min between previous close and current low
                double lo = Math.Min(Close[1], Low[0]);

                // Calculate effective volume (accumulation/distribution)
                double ad = 0;
                double range = hi - lo;
                if (range > double.Epsilon)
                {
                    // Formula: When price rises (Close[0] > Close[1]), AD is positive (accumulation)
                    ad = ((Close[0] - Close[1]) / range) * volume;
                }

                // Store for label display
                lastAD = ad;

            // CVOL2 logic: Compare with previous day volume
            DateTime currentTime = Time[0];

            // Store current volume
            historicalVolumes[currentTime] = volume;

            // Periodic cleanup of historical data (every CLEANUP_FREQUENCY_BARS bars)
            if (CurrentBar % CLEANUP_FREQUENCY_BARS == 0)
            {
                DateTime cutoffDate = currentTime.AddDays(-MAX_HISTORICAL_DAYS);

                // Remove data older than MAX_HISTORICAL_DAYS
                var keysToRemove = historicalVolumes.Keys
                    .Where(k => k < cutoffDate)
                    .ToList();

                foreach (var key in keysToRemove)
                    historicalVolumes.Remove(key);

                // If dictionary is too large, keep only last MAX_DICTIONARY_SIZE items
                if (historicalVolumes.Count > MAX_DICTIONARY_SIZE * 2)
                {
                    var oldKeysToRemove = historicalVolumes.Keys
                        .OrderBy(k => k)
                        .Take(historicalVolumes.Count - MAX_DICTIONARY_SIZE)
                        .ToList();

                    foreach (var key in oldKeysToRemove)
                        historicalVolumes.Remove(key);
                }
            }

            // Find volume at same time on previous day
            DateTime previousDayTime = currentTime.AddDays(-1);
            double previousVolume = 0;

            if (historicalVolumes.ContainsKey(previousDayTime))
            {
                previousVolume = historicalVolumes[previousDayTime];
            }
            else
            {
                // Search for closest bar within TIME_MATCH_TOLERANCE_MINUTES
                DateTime closestTime = FindClosestVolumeTime(previousDayTime, TIME_MATCH_TOLERANCE_MINUTES);

                if (closestTime != DateTime.MinValue)
                {
                    previousVolume = historicalVolumes[closestTime];
                }
            }

            // Calculate volume ratio
            double volumeRatio = 1.0;
            if (previousVolume > 0)
            {
                volumeRatio = volume / previousVolume;
            }

            // Set plot values
            // Plot 0: Total volume with color based on EMA comparison
            Values[0][0] = volume;

			// --- LOGICA DE CALCULO CONDICIONAL PARA LA MEDIA MOVIL ---
			TimeSpan currentTimeOfDay = Time[0].TimeOfDay;
			bool isCurrentlyInSession = currentTimeOfDay >= SessionStartTime && currentTimeOfDay <= SessionEndTime;

			switch (CalcMode)
			{
				case CalculationMode.EmaContinua:
					// Se calcula en cada barra. Usa 'isFirstBarOfEmaSession' solo la primera vez que se carga el indicador.
					if (isFirstBarOfEmaSession)
					{
						lastCalculationValue = volume;
						isFirstBarOfEmaSession = false;
					}
					else
					{
						lastCalculationValue = (volume * emaMultiplier) + (lastCalculationValue * (1 - emaMultiplier));
					}
					break;

				case CalculationMode.EmaReiniciable:
					// Se calcula solo dentro de la sesión y se reinicia a una hora específica.
					if (ShouldResetEma())
					{
						isFirstBarOfEmaSession = true; // Programar un reinicio
					}

					if (isCurrentlyInSession)
					{
						if (isFirstBarOfEmaSession)
						{
							lastCalculationValue = volume;
							isFirstBarOfEmaSession = false;
						}
						else
						{
							lastCalculationValue = (volume * emaMultiplier) + (lastCalculationValue * (1 - emaMultiplier));
						}
					}
					break;

				case CalculationMode.MediaDeSesion:
					// Media simple acumulada que se reinicia al principio de la sesión.
					bool newSessionStarted = isCurrentlyInSession && !inSession; // 'inSession' aquí actúa como 'wasInSession'
					if (newSessionStarted)
					{
						sessionAccumulatedVolume = volume;
						sessionBarCount = 1;
					}
					else if (isCurrentlyInSession)
					{
						sessionAccumulatedVolume += volume;
						sessionBarCount++;
					}

					if (isCurrentlyInSession && sessionBarCount > 0)
					{
						lastCalculationValue = sessionAccumulatedVolume / sessionBarCount;
					}
					break;
			}

			// Actualizar el estado de la sesión para la próxima barra.
			inSession = isCurrentlyInSession;

			// Dibujar el valor calculado
			if (lastCalculationValue > 0)
			{
				Values[2][0] = lastCalculationValue;
			}


            // Color volume bars with NEW PRIORITY LOGIC:
            // 1st PRIORITY: If volume >= 4x previous day same bar -> ORANGE
            // 2nd PRIORITY: If volume >= 2.5x previous bar's MA -> WHITE (EXCEPT first RTH bar at 15:30)
            // 3rd PRIORITY: If volume >= current MA -> DodgerBlue
            // 4th: Otherwise -> DimGray

            // Check if this is the first bar of RTH session (15:30)
            bool isFirstRthBar = IsFirstRthBar();

            if (volumeRatio >= PreviousDayVolumeMultiplier)
            {
                // PRIORITY 1: Volume is 4x or more compared to same bar yesterday: Orange
                // This applies even to first RTH bar
                PlotBrushes[0][0] = Brushes.Orange;
            }
            else if (Values[2][1] > 0 && volume >= (Values[2][1] * PreviousEmaMultiplier) && !isFirstRthBar)
            {
                // PRIORITY 2: Volume is 2.5x or more compared to previous bar's MA: White
                // EXCEPTION: First RTH bar (15:30) is excluded from white color
                PlotBrushes[0][0] = Brushes.White;
            }
            else if (Values[2][0] > 0 && volume >= Values[2][0])
            {
                // PRIORITY 3: Volume is above or equal to current MA: DodgerBlue
                PlotBrushes[0][0] = Brushes.DodgerBlue;
            }
            else
            {
                // Volume is below MA (or not enough bars for calculation): DimGray
                PlotBrushes[0][0] = Brushes.DimGray;
            }

            // Plot 1: Effective Volume with threshold filter
            double volumeThreshold = volume * Threshold;

            if (Math.Abs(ad) > volumeThreshold)
                {
                    // If ad is positive, price went up - accumulation (buying) - ForestGreen
                    // If ad is negative, price went down - distribution (selling) - IndianRed
                    // Both bars go in same direction (positive), so we use absolute value

                    if (ad > 0)
                    {
                        // Accumulation (buying) - ForestGreen
                        Values[1][0] = Math.Abs(ad);
                        PlotBrushes[1][0] = Brushes.ForestGreen;
                    }
                    else
                    {
                        // Distribution (selling) - IndianRed
                        Values[1][0] = Math.Abs(ad);
                        PlotBrushes[1][0] = Brushes.IndianRed;
                    }
                }
                else
                {
                    Values[1][0] = 0;
                }
            }
            catch (Exception ex)
            {
                Log($"RVol2 OnBarUpdate error: {ex.Message}", LogLevel.Error);
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (Bars == null || ChartControl == null || CurrentBar < 1)
                return;

            // Get the chart panel
            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];

            // Calculate position: 25 pixels to the right of last bar
            double lastBarX = chartControl.GetXByBarIndex(ChartBars, ChartBars.ToIndex);
            double labelX = lastBarX + 25;

            // Position label at middle of Y axis range
            double yMax = chartScale.MaxValue;
            double yMin = chartScale.MinValue;
            double yMid = (yMax + yMin) / 2;
            double labelY = chartScale.GetYByValue(yMid);

            // Format the label text (positive = accumulation, negative = distribution)
            string labelText = lastAD.ToString("F0");

            // Choose color based on sign (positive ad = accumulation = green, negative ad = distribution = red)
            SharpDX.Color labelColor = lastAD > 0 ? SharpDX.Color.ForestGreen : SharpDX.Color.IndianRed;

            // Set font properties
            if (Core.Globals.DirectWriteFactory == null || RenderTarget == null)
                return;

            string fontFamily = "Arial";
            float fontSize = (float)LabelFontSize;

            using (SharpDX.DirectWrite.TextFormat textFormat = new SharpDX.DirectWrite.TextFormat(
                Core.Globals.DirectWriteFactory, fontFamily,
                SharpDX.DirectWrite.FontWeight.Bold, SharpDX.DirectWrite.FontStyle.Normal, fontSize))
            {
                textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
                textFormat.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

                using (SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(
                    Core.Globals.DirectWriteFactory, labelText, textFormat, 200, fontSize))
                {
                    using (SharpDX.Direct2D1.SolidColorBrush brush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, labelColor))
                    {
                        RenderTarget.DrawTextLayout(
                            new SharpDX.Vector2((float)labelX, (float)labelY),
                            textLayout,
                            brush,
                            SharpDX.Direct2D1.DrawTextOptions.NoSnap);
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to find the closest volume time within tolerance
        /// </summary>
        private DateTime FindClosestVolumeTime(DateTime targetTime, int toleranceMinutes)
        {
            return historicalVolumes.Keys
                .Where(k => k.Date == targetTime.Date &&
                            Math.Abs((k - targetTime).TotalMinutes) <= toleranceMinutes)
                .OrderBy(k => Math.Abs((k - targetTime).TotalMinutes))
                .FirstOrDefault();
        }

        /// <summary>
        /// Helper method to detect if current bar is the first bar of RTH session
        /// </summary>
        private bool IsFirstRthBar()
        {
            if (CurrentBar < 1)
                return false;

            // Check if current bar is at or after RTH start (15:30)
            // and previous bar was before RTH start
            TimeSpan currentTime = Time[0].TimeOfDay;
            TimeSpan previousTime = Time[1].TimeOfDay;

            // First bar of RTH: previous bar was before 15:30, current bar is at or after 15:30
            return previousTime < rthSessionStart && currentTime >= rthSessionStart;
        }

		/// <summary>
		/// Helper method to detect if the EMA should be reset
		/// </summary>
		private bool ShouldResetEma()
		{
			if (CurrentBar < 1)
				return false;

			TimeSpan currentTime = Time[0].TimeOfDay;
			TimeSpan previousTime = Time[1].TimeOfDay;

			return previousTime < EmaResetTime && currentTime >= EmaResetTime;
		}

        #region Properties
        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Total Volume Opacity", Description = "Opacity of the total volume bar (0-100%)", Order = 1, GroupName = "Visual")]
        public int TotalVolumeOpacity
        {
            get { return totalVolumeOpacity; }
            set { totalVolumeOpacity = Math.Max(0, Math.Min(100, value)); }
        }

        [NinjaScriptProperty]
        [Range(0.0, 1.0)]
        [Display(Name = "Threshold", Description = "Threshold percentage for filtering effective volume (0.3 = 30%)", Order = 2, GroupName = "Parameters")]
        public double Threshold
        {
            get { return threshold; }
            set { threshold = Math.Max(0.0, Math.Min(1.0, value)); }
        }

        [NinjaScriptProperty]
        [Range(8, 72)]
        [Display(Name = "Label Font Size", Description = "Font size for the volume label", Order = 3, GroupName = "Visual")]
        public int LabelFontSize
        {
            get { return labelFontSize; }
            set { labelFontSize = Math.Max(8, Math.Min(72, value)); }
        }

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "EMA Period", Description = "Period for the volume EMA", Order = 3, GroupName = "Parameters")]
        public int EmaPeriod
        {
            get { return emaPeriod; }
            set { emaPeriod = Math.Max(1, Math.Min(200, value)); }
        }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "High Volume Threshold", Description = "Ratio threshold for high volume (orange color)", Order = 4, GroupName = "Parameters")]
        public double HighVolumeThreshold
        {
            get { return highVolumeThreshold; }
            set { highVolumeThreshold = Math.Max(1.0, Math.Min(10.0, value)); }
        }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "Medium Volume Threshold", Description = "Ratio threshold for medium volume (white color)", Order = 5, GroupName = "Parameters")]
        public double MediumVolumeThreshold
        {
            get { return mediumVolumeThreshold; }
            set { mediumVolumeThreshold = Math.Max(1.0, Math.Min(10.0, value)); }
        }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "EMA Multiplier White Dot", Description = "EMA multiplier to display white dot above volume bar", Order = 6, GroupName = "Parameters")]
        public double EmaMultiplierWhiteDot
        {
            get { return emaMultiplierWhiteDot; }
            set { emaMultiplierWhiteDot = Math.Max(1.0, Math.Min(10.0, value)); }
        }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "EMA Multiplier Green Dot", Description = "EMA multiplier to display green dot above volume bar", Order = 7, GroupName = "Parameters")]
        public double EmaMultiplierGreenDot
        {
            get { return emaMultiplierGreenDot; }
            set { emaMultiplierGreenDot = Math.Max(1.0, Math.Min(10.0, value)); }
        }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "Previous Day Volume Multiplier (Orange)", Description = "Multiplier for previous day volume comparison (e.g., 4.0 = 4x previous day volume)", Order = 8, GroupName = "Parameters")]
        public double PreviousDayVolumeMultiplier
        {
            get { return previousDayVolumeMultiplier; }
            set { previousDayVolumeMultiplier = Math.Max(1.0, Math.Min(10.0, value)); }
        }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "Previous EMA Multiplier (White)", Description = "Multiplier for previous bar's EMA (e.g., 2.5 = 2.5x previous EMA)", Order = 9, GroupName = "Parameters")]
        public double PreviousEmaMultiplier
        {
            get { return previousEmaMultiplier; }
            set { previousEmaMultiplier = Math.Max(1.0, Math.Min(10.0, value)); }
        }

		[NinjaScriptProperty]
		[Display(Name = "Modo de Cálculo", Description = "Selecciona el modo de cálculo para la línea de media móvil.", Order = 10, GroupName = "Modo de Cálculo")]
		public CalculationMode CalcMode { get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditor")]
		[Display(Name = "Hora Inicio Sesión", Description = "Hora de inicio para el modo 'Media de Sesión'.", Order = 11, GroupName = "Modo de Cálculo")]
		public TimeSpan SessionStartTime { get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditor")]
		[Display(Name = "Hora Fin Sesión", Description = "Hora de fin para el modo 'Media de Sesión'.", Order = 12, GroupName = "Modo de Cálculo")]
		public TimeSpan SessionEndTime { get; set; }

		[NinjaScriptProperty]
		[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditor")]
		[Display(Name = "Hora Reinicio EMA", Description = "Hora para reiniciar la EMA en el modo 'EMA Reiniciable'.", Order = 13, GroupName = "Modo de Cálculo")]
		public TimeSpan EmaResetTime { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> TotalVolume
        {
            get { return Values[0]; }
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> EffectiveVolume
        {
            get { return Values[1]; }
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> VolumeEMA
        {
            get { return Values[2]; }
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private RVol2_modificado[] cacheRVol2_modificado;
		public RVol2_modificado RVol2_modificado(int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double emaMultiplierWhiteDot, double emaMultiplierGreenDot, double previousDayVolumeMultiplier, double previousEmaMultiplier, CalculationMode calcMode, TimeSpan sessionStartTime, TimeSpan sessionEndTime, TimeSpan emaResetTime)
		{
			return RVol2_modificado(Input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, emaMultiplierWhiteDot, emaMultiplierGreenDot, previousDayVolumeMultiplier, previousEmaMultiplier, calcMode, sessionStartTime, sessionEndTime, emaResetTime);
		}

		public RVol2_modificado RVol2_modificado(ISeries<double> input, int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double emaMultiplierWhiteDot, double emaMultiplierGreenDot, double previousDayVolumeMultiplier, double previousEmaMultiplier, CalculationMode calcMode, TimeSpan sessionStartTime, TimeSpan sessionEndTime, TimeSpan emaResetTime)
		{
			if (cacheRVol2_modificado != null)
				for (int idx = 0; idx < cacheRVol2_modificado.Length; idx++)
					if (cacheRVol2_modificado[idx] != null && cacheRVol2_modificado[idx].TotalVolumeOpacity == totalVolumeOpacity && cacheRVol2_modificado[idx].Threshold == threshold && cacheRVol2_modificado[idx].LabelFontSize == labelFontSize && cacheRVol2_modificado[idx].EmaPeriod == emaPeriod && cacheRVol2_modificado[idx].HighVolumeThreshold == highVolumeThreshold && cacheRVol2_modificado[idx].MediumVolumeThreshold == mediumVolumeThreshold && cacheRVol2_modificado[idx].EmaMultiplierWhiteDot == emaMultiplierWhiteDot && cacheRVol2_modificado[idx].EmaMultiplierGreenDot == emaMultiplierGreenDot && cacheRVol2_modificado[idx].PreviousDayVolumeMultiplier == previousDayVolumeMultiplier && cacheRVol2_modificado[idx].PreviousEmaMultiplier == previousEmaMultiplier && cacheRVol2_modificado[idx].CalcMode == calcMode && cacheRVol2_modificado[idx].SessionStartTime == sessionStartTime && cacheRVol2_modificado[idx].SessionEndTime == sessionEndTime && cacheRVol2_modificado[idx].EmaResetTime == emaResetTime && cacheRVol2_modificado[idx].EqualsInput(input))
						return cacheRVol2_modificado[idx];
			return CacheIndicator<RVol2_modificado>(new RVol2_modificado(){ TotalVolumeOpacity = totalVolumeOpacity, Threshold = threshold, LabelFontSize = labelFontSize, EmaPeriod = emaPeriod, HighVolumeThreshold = highVolumeThreshold, MediumVolumeThreshold = mediumVolumeThreshold, EmaMultiplierWhiteDot = emaMultiplierWhiteDot, EmaMultiplierGreenDot = emaMultiplierGreenDot, PreviousDayVolumeMultiplier = previousDayVolumeMultiplier, PreviousEmaMultiplier = previousEmaMultiplier, CalcMode = calcMode, SessionStartTime = sessionStartTime, SessionEndTime = sessionEndTime, EmaResetTime = emaResetTime }, input, ref cacheRVol2_modificado);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RVol2_modificado RVol2_modificado(int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double emaMultiplierWhiteDot, double emaMultiplierGreenDot, double previousDayVolumeMultiplier, double previousEmaMultiplier, CalculationMode calcMode, TimeSpan sessionStartTime, TimeSpan sessionEndTime, TimeSpan emaResetTime)
		{
			return indicator.RVol2_modificado(Input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, emaMultiplierWhiteDot, emaMultiplierGreenDot, previousDayVolumeMultiplier, previousEmaMultiplier, calcMode, sessionStartTime, sessionEndTime, emaResetTime);
		}

		public Indicators.RVol2_modificado RVol2_modificado(ISeries<double> input , int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double emaMultiplierWhiteDot, double emaMultiplierGreenDot, double previousDayVolumeMultiplier, double previousEmaMultiplier, CalculationMode calcMode, TimeSpan sessionStartTime, TimeSpan sessionEndTime, TimeSpan emaResetTime)
		{
			return indicator.RVol2_modificado(input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, emaMultiplierWhiteDot, emaMultiplierGreenDot, previousDayVolumeMultiplier, previousEmaMultiplier, calcMode, sessionStartTime, sessionEndTime, emaResetTime);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RVol2_modificado RVol2_modificado(int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double emaMultiplierWhiteDot, double emaMultiplierGreenDot, double previousDayVolumeMultiplier, double previousEmaMultiplier, CalculationMode calcMode, TimeSpan sessionStartTime, TimeSpan sessionEndTime, TimeSpan emaResetTime)
		{
			return indicator.RVol2_modificado(Input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, emaMultiplierWhiteDot, emaMultiplierGreenDot, previousDayVolumeMultiplier, previousEmaMultiplier, calcMode, sessionStartTime, sessionEndTime, emaResetTime);
		}

		public Indicators.RVol2_modificado RVol2_modificado(ISeries<double> input , int totalVolumeOpacity, double threshold, int labelFontSize, int emaPeriod, double highVolumeThreshold, double mediumVolumeThreshold, double emaMultiplierWhiteDot, double emaMultiplierGreenDot, double previousDayVolumeMultiplier, double previousEmaMultiplier, CalculationMode calcMode, TimeSpan sessionStartTime, TimeSpan sessionEndTime, TimeSpan emaResetTime)
		{
			return indicator.RVol2_modificado(input, totalVolumeOpacity, threshold, labelFontSize, emaPeriod, highVolumeThreshold, mediumVolumeThreshold, emaMultiplierWhiteDot, emaMultiplierGreenDot, previousDayVolumeMultiplier, previousEmaMultiplier, calcMode, sessionStartTime, sessionEndTime, emaResetTime);
		}
	}
}

#endregion