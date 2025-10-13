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
using NinjaTrader.NinjaScript.Indicators.RyF; // Using our new namespaces
using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Media;
using System.Xml.Serialization;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
        /// <summary>
    /// Calculates and displays price levels based on the previous day's range.
    /// This refactored version uses a clean architecture approach with decoupled services
    /// for calculation, session management, and rendering.
    /// </summary>
    [Description("Calculates and displays price levels based on the previous day's range. (Refactored)")]
    public partial class AV1s : Indicator
    {
        // Services
        private IPriceLevelCalculator priceLevelCalculator;
        private ISessionManager sessionManager;
        private DirectXLevelRenderer levelRenderer;

        // Data structures
        private List<PriceLevel> currentDayLevels;
        private readonly Queue<DayLevels> historicalLevels = new Queue<DayLevels>();

        // A class to hold all data related to a single day's levels
        private class DayLevels
        {
            public List<PriceLevel> Levels { get; set; }
            public int StartBarIndex { get; set; }
            public int EndBarIndex { get; set; }
        }

        protected override void OnStateChange()
        {
            switch (State)
            {
                case State.SetDefaults:
                    Description = @"Calculates and displays price levels based on the previous day's range.";
                    Name = "AV1s (Refactored)";
                    Calculate = Calculate.OnBarClose;
                    IsOverlay = true;

                    UseAutomaticDate = true;
                    DaysToDraw = 5;
                    Nr2LevelType = NR2LevelType.PreviousDayClose;
                    UseGapCalculation = false;
                    SelectedDate = DateTime.Today;
                    ManualPrice = 0.0;
                    Width = 1;
                    break;

                case State.Configure:
                    AddDataSeries(Bars.Instrument.FullName, BarsPeriodType.Day, 1);
                    break;

                case State.DataLoaded:
                                        priceLevelCalculator = new PriceLevelCalculator { Logger = Print };
                                        sessionManager = new NinjaTraderSessionManager(Bars, BarsArray[1]) { Logger = Print };
                    break;

                case State.Terminated:
                    levelRenderer?.Dispose();
                    break;
            }
        }

        protected override void OnBarUpdate()
        {
            if (!UseAutomaticDate || priceLevelCalculator == null || sessionManager == null)
                return;

            try
            {
                if (sessionManager.IsNewTradingDay(Time[0]))
                {
                    var priorDayData = sessionManager.GetPriorDayData(Time[0]);
                    if (priorDayData == null) return;

                    var parameters = new CalculationParameters
                    {
                        ManualPrice = this.ManualPrice,
                        LevelType = this.Nr2LevelType,
                        UseGapCalculation = this.UseGapCalculation
                    };

                    // This part needs more logic from the original file to adjust range for GAP
                    // and select the correct base price. For simplicity, we pass the raw data for now.

                    var levels = priceLevelCalculator.CalculateLevels(priorDayData, parameters);
                    UpdateCurrentDayLevels(levels.ToList(), CurrentBar);
                }
            }
                        catch (ArgumentException ex)
            {
                Print($"Invalid argument in OnBarUpdate: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Print($"Invalid operation in OnBarUpdate: {ex.Message}");
            }
            catch (Exception ex)
            {
                Print($"An unexpected error occurred in OnBarUpdate: {ex.Message}");
                // Optionally re-throw critical exceptions
                if (ex is OutOfMemoryException || ex is StackOverflowException)
                    throw;
            }
        }

        private void UpdateCurrentDayLevels(List<PriceLevel> newLevels, int startBar)
        {
            // Finalize the previous day
            if (currentDayLevels != null && historicalLevels.Count > 0)
            {
                historicalLevels.Last().EndBarIndex = CurrentBar - 1;
            }

            // Add the new day's levels
            var newDay = new DayLevels { Levels = newLevels, StartBarIndex = startBar };
            historicalLevels.Enqueue(newDay);

            // Maintain queue size
            while (historicalLevels.Count > DaysToDraw)
            {
                historicalLevels.Dequeue();
            }

            currentDayLevels = newLevels;
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (levelRenderer == null)
            {
                levelRenderer = new DirectXLevelRenderer(RenderTarget, chartControl, chartScale, Width);
            }

            foreach (var day in historicalLevels)
            {
                levelRenderer.RenderLevels(day.Levels, day.StartBarIndex, day.EndBarIndex);
            }
        }

        #region Properties

        private DateTime selectedDate;
        private double manualPrice;

        [NinjaScriptProperty]
        [Display(Name = "Use Automatic Date", Order = 1, GroupName = "Parameters")]
        public bool UseAutomaticDate { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Historical Days to Draw", Order = 2, GroupName = "Parameters")]
        public int DaysToDraw { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "NR2 Level Type", Order = 3, GroupName = "Parameters")]
        public NR2LevelType Nr2LevelType { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "GAP", Order = 4, GroupName = "Parameters")]
        public bool UseGapCalculation { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Selected Date", Order = 5, GroupName = "Parameters")]
        public DateTime SelectedDate
        {
            get => selectedDate;
            set
            {
                if (value < new DateTime(1970, 1, 1))
                    throw new ArgumentOutOfRangeException(nameof(value), "Selected date cannot be before 1970");
                if (value > DateTime.Today.AddYears(1))
                    throw new ArgumentOutOfRangeException(nameof(value), "Selected date cannot be more than 1 year in the future");
                selectedDate = value;
            }
        }

        [NinjaScriptProperty]
        [Display(Name = "Manual Price", Order = 6, GroupName = "Parameters")]
        public double ManualPrice
        {
            get => manualPrice;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentException("Manual price cannot be NaN or Infinity");
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Manual price cannot be negative");
                manualPrice = value;
            }
        }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Line Width", Order = 1, GroupName = "Visuals")]
        public int Width { get; set; }

        #endregion

        #region Enums
        public enum NR2LevelType
        {
            PreviousDayClose,
            CurrentDayOpen
        }
        #endregion
    }
}