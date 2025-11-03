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

public enum TableCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

namespace NinjaTrader.NinjaScript.Indicators
{
    public class RyFEM : Indicator
    {
        #region Constants
        private const int MIN_LENGTH = 1;
        private const int MAX_LENGTH = 1000;
        private const string DEFAULT_FONT_FAMILY = "Consolas";
        #endregion

        #region Variables
        private double daily_bull_avg;
        private double daily_bear_avg;
        private double top;
        private double bull_success;
        private double bear_success;
        private bool isInitialized;
		private TableConfiguration tableConfig;
		private DateTime lastSuccessCalcDate = DateTime.MinValue;
        #endregion

		#region Data Structures
	    public class TableConfiguration
	    {
	        public int FontSize { get; set; } = 12;
	        public int XOffset { get; set; } = 10;
	        public int YOffset { get; set; } = 10;
	        public TableCorner Corner { get; set; } = TableCorner.TopRight;
	    }
		#endregion

        #region OnStateChange
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                SetDefaultValues();
            }
            else if (State == State.Configure)
            {
                AddDataSeries(Data.BarsPeriodType.Day, 1);
				BarsRequiredToPlot = Length * 2; // Need enough data for historical calculations
            }
            else if (State == State.DataLoaded)
            {
                InitializeComponents();
            }
        }

        private void SetDefaultValues()
        {
            Description = @"Expected Move - Calcula niveles esperados usando rangos de días alcistas/bajistas";
            Name = "RyFEM";
            Calculate = Calculate.OnBarClose;
            IsOverlay = true;
            DisplayInDataBox = true;
            DrawOnPricePanel = true;
            DrawHorizontalGridLines = false;
            DrawVerticalGridLines = false;
            PaintPriceMarkers = false;
            ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
            IsSuspendedWhileInactive = true;

            // Default configuration
            Length = 252;
            tableConfig = new TableConfiguration();
			ShowBacktest = true;
        }

        private void InitializeComponents()
        {
            try
            {
                isInitialized = true;
                Print($"RyFEM: Indicador inicializado correctamente con Length={Length}");
            }
            catch (Exception ex)
            {
                Print($"RyFEM: Error inicializando componente: {ex.Message}");
            }
        }
        #endregion

        #region OnBarUpdate
        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0) return;
            if (!isInitialized || CurrentBar < 1) return;
            if (BarsArray[1] == null || BarsArray[1].Count < Length * 2) return;

            try
            {
                // --- Basic Expected Move Calculation ---
                top = Opens[1][0];

                double bull_sum = 0.0;
                double bear_sum = 0.0;
                for (int i = 1; i <= Length; i++)
                {
                    bull_sum += Highs[1][i] - Opens[1][i];
                    bear_sum += Opens[1][i] - Lows[1][i];
                }

                daily_bull_avg = (Length > 0) ? bull_sum / Length : 0;
                daily_bear_avg = (Length > 0) ? bear_sum / Length : 0;

                // --- Historical Success Rate Calculation (Backtest) ---
                // Only run this expensive calculation once per day
                if (Times[1][0].Date == lastSuccessCalcDate) return;

                lastSuccessCalcDate = Times[1][0].Date;
                int highSuccessCount = 0;
                int lowSuccessCount = 0;

                // Loop through the last 'Length' days to perform the backtest
                for (int i = 1; i <= Length; i++)
                {
                    // For each historical day 'i', calculate its EM based on the 'Length' days prior to it
                    double hist_bull_sum = 0.0;
                    double hist_bear_sum = 0.0;
                    for (int j = 1; j <= Length; j++)
                    {
                        int lookbackIndex = i + j;
                        hist_bull_sum += Highs[1][lookbackIndex] - Opens[1][lookbackIndex];
                        hist_bear_sum += Opens[1][lookbackIndex] - Lows[1][lookbackIndex];
                    }

                    double hist_bull_avg = (Length > 0) ? hist_bull_sum / Length : 0;
                    double hist_bear_avg = (Length > 0) ? hist_bear_sum / Length : 0;

                    // Now, get the data for the historical day 'i'
                    double hist_open = Opens[1][i];
                    double hist_high = Highs[1][i];
                    double hist_low = Lows[1][i];

                    // Calculate the EM for that historical day
                    double hist_em_high = hist_open + hist_bull_avg;
                    double hist_em_low = hist_open - hist_bear_avg;

                    // Check if it was a success
                    if (hist_high >= hist_em_high) highSuccessCount++;
                    if (hist_low <= hist_em_low) lowSuccessCount++;
                }

                double totalSuccess = highSuccessCount + lowSuccessCount;
                if (totalSuccess > 0)
                {
                    bull_success = (highSuccessCount / totalSuccess) * 100;
                    bear_success = (lowSuccessCount / totalSuccess) * 100;
                }
                else
                {
                    bull_success = 0;
                    bear_success = 0;
                }
            }
            catch (Exception ex)
            {
                Print($"RyFEM: Error during calculation in OnBarUpdate: {ex.Message}");
            }
        }
        #endregion

        #region OnRender - Statistics Table
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (!ShowBacktest || Bars == null || Bars.Count == 0 || RenderTarget == null)
                return;

            string bullSuccessText = "Bull Success = " + bull_success.ToString("F2") + "%";
            string bearSuccessText = "Bear Success = " + bear_success.ToString("F2") + "%";
            string highEMText = "High EM = " + (top + daily_bull_avg).ToString("F2");
            string lowEMText = "Low EM = " + (top - daily_bear_avg).ToString("F2");

            string fullText = string.Join("\n", bullSuccessText, bearSuccessText, highEMText, lowEMText);

            using (TextFormat textFormat = new TextFormat(Core.Globals.DirectWriteFactory, DEFAULT_FONT_FAMILY, tableConfig.FontSize))
			{
				using (TextLayout textLayout = new TextLayout(Core.Globals.DirectWriteFactory, fullText, textFormat, 400, 400))
				{
					float x = CalculateTableXPosition(textLayout.Metrics.Width);
					float y = CalculateTableYPosition(textLayout.Metrics.Height);

					using (var bgBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new Color4(0, 0, 0, 0.8f)))
					{
						RenderTarget.FillRectangle(new RectangleF(x - 5, y - 5, textLayout.Metrics.Width + 10, textLayout.Metrics.Height + 10), bgBrush);
					}

					using (var textBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, Color4.White))
					{
						RenderTarget.DrawTextLayout(new Vector2(x, y), textLayout, textBrush);
					}
				}
			}
        }

        private float CalculateTableXPosition(float tableWidth)
        {
            switch (TableCorner)
            {
                case TableCorner.TopLeft:
                case TableCorner.BottomLeft:
                    return TableX;
                case TableCorner.TopRight:
                case TableCorner.BottomRight:
                    return ChartPanel.W - tableWidth - TableX;
                default:
                    return TableX;
            }
        }

        private float CalculateTableYPosition(float tableHeight)
        {
            switch (TableCorner)
            {
                case TableCorner.TopLeft:
                case TableCorner.TopRight:
                    return TableY;
                case TableCorner.BottomLeft:
                case TableCorner.BottomRight:
                    return ChartPanel.H - tableHeight - TableY;
                default:
                    return TableY;
            }
        }
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Length", Description = "Período de lookback para promedios", Order = 1, GroupName = "Parameters")]
        [Range(MIN_LENGTH, MAX_LENGTH)]
        public int Length { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Backtest", Description = "Muestra la tabla de resultados", Order = 2, GroupName = "Parameters")]
        public bool ShowBacktest { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Table Font Size", Description = "Font size for table display", Order = 3, GroupName = "Parameters")]
        [Range(8, 24)]
        public int TableFontSize
        {
            get => tableConfig.FontSize;
            set
            {
                if (value >= 8 && value <= 24)
                    tableConfig.FontSize = value;
                else
                    Print("RyFEM: TableFontSize debe estar entre 8 y 24");
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Table Corner", Description = "Select corner for table positioning", Order = 4, GroupName = "Parameters")]
        public TableCorner TableCorner
        {
            get => tableConfig.Corner;
            set => tableConfig.Corner = value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Table X Position", Description = "X offset from selected corner", Order = 5, GroupName = "Parameters")]
        [Range(0, 500)]
        public int TableX
        {
            get => tableConfig.XOffset;
            set => tableConfig.XOffset = value;
        }

        [NinjaScriptProperty]
        [Display(Name = "Table Y Position", Description = "Y offset from selected corner", Order = 6, GroupName = "Parameters")]
        [Range(0, 500)]
        public int TableY
        {
            get => tableConfig.YOffset;
            set => tableConfig.YOffset = value;
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private RyFEM[] cacheRyFEM;
		public RyFEM RyFEM(int length, bool showBacktest, int tableFontSize, TableCorner tableCorner, int tableX, int tableY)
		{
			return RyFEM(Input, length, showBacktest, tableFontSize, tableCorner, tableX, tableY);
		}

		public RyFEM RyFEM(ISeries<double> input, int length, bool showBacktest, int tableFontSize, TableCorner tableCorner, int tableX, int tableY)
		{
			if (cacheRyFEM != null)
				for (int idx = 0; idx < cacheRyFEM.Length; idx++)
					if (cacheRyFEM[idx] != null && cacheRyFEM[idx].Length == length && cacheRyFEM[idx].ShowBacktest == showBacktest && cacheRyFEM[idx].TableFontSize == tableFontSize && cacheRyFEM[idx].TableCorner == tableCorner && cacheRyFEM[idx].TableX == tableX && cacheRyFEM[idx].TableY == tableY && cacheRyFEM[idx].EqualsInput(input))
						return cacheRyFEM[idx];
			return CacheIndicator<RyFEM>(new RyFEM(){ Length = length, ShowBacktest = showBacktest, TableFontSize = tableFontSize, TableCorner = tableCorner, TableX = tableX, TableY = tableY }, input, ref cacheRyFEM);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RyFEM RyFEM(int length, bool showBacktest, int tableFontSize, TableCorner tableCorner, int tableX, int tableY)
		{
			return indicator.RyFEM(Input, length, showBacktest, tableFontSize, tableCorner, tableX, tableY);
		}

		public Indicators.RyFEM RyFEM(ISeries<double> input , int length, bool showBacktest, int tableFontSize, TableCorner tableCorner, int tableX, int tableY)
		{
			return indicator.RyFEM(input, length, showBacktest, tableFontSize, tableCorner, tableX, tableY);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RyFEM RyFEM(int length, bool showBacktest, int tableFontSize, TableCorner tableCorner, int tableX, int tableY)
		{
			return indicator.RyFEM(Input, length, showBacktest, tableFontSize, tableCorner, tableX, tableY);
		}

		public Indicators.RyFEM RyFEM(ISeries<double> input , int length, bool showBacktest, int tableFontSize, TableCorner tableCorner, int tableX, int tableY)
		{
			return indicator.RyFEM(input, length, showBacktest, tableFontSize, tableCorner, tableX, tableY);
		}
	}
}

#endregion
