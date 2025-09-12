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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using System.Xml;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class VaL : Indicator
    {
        #region Variables
        private DateTime selectedDate;
        private double manualPrice;

        private SessionIterator sessionIterator;
        private DateTime currentDate = Core.Globals.MinDate;
        private double currentDayHigh;
        private double currentDayLow;
        private double currentDayOpen;
        private double priorDayHigh;
        private double priorDayLow;
        private bool levelsCalculated = false;

        private string persistenceFilePath = "";
        private Dictionary<DateTime, Dictionary<string, double>> savedLevels = new Dictionary<DateTime, Dictionary<string, double>>();

        private readonly Dictionary<string, PriceLevel> priceLevels = new Dictionary<string, PriceLevel>();

        public enum LevelType
        {
            QuarterMain,
            QuarterMid,
            MidQuarter,
            TrendBullish,
            TrendBearish,
            Neutral,
            StandardBullish,
            StandardBearish,
            Extended
        }

        private readonly Dictionary<string, LevelConfig> levelConfigs = new Dictionary<string, LevelConfig>();

        private class LevelConfig
        {
            public LevelType Type { get; set; }
            public string DefaultColorName { get; set; }
            public int DefaultWidth { get; set; }
        }

        private class PriceLevel : IDisposable
        {
            public string Name { get; }
            public LevelType Type { get; }
            public System.Windows.Media.Brush LineBrush { get; set; }
            public int LineWidth { get; set; }
            public double Value { get; set; }
            public TextLayout LabelLayout { get; set; }

            public PriceLevel(string name, LevelType type, System.Windows.Media.Brush brush, int width)
            {
                Name = name;
                Type = type;
                LineBrush = brush;
                LineWidth = width;
                Value = double.NaN;
            }

            public void Dispose()
            {
                LabelLayout?.Dispose();
                LabelLayout = null;
            }
        }

        private readonly Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush> dxBrushes = 
            new Dictionary<System.Windows.Media.Brush, SharpDX.Direct2D1.Brush>();
        private bool needsLayoutUpdate = false;
        private bool isConfigured = false;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Advanced price level calculator with persistence and customization.";
                Name = "VaL";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                UseAutomaticDate = true;
                UseGapCalculation = false;
                UsePersistence = true;
                SelectedDate = DateTime.Today;
                ManualPrice = 0.0;
                LineBufferPixels = 125;
                ShowLabels = true;
                MinimumTick = 0.25;

                QuarterMainColor = Brushes.Yellow;
                QuarterMidColor = Brushes.Plum;
                MidQuarterColor = Brushes.Orange;
                TrendBullishColor = Brushes.ForestGreen;
                TrendBearishColor = Brushes.Crimson;
                NeutralColor = Brushes.Cyan;
                StandardBullishColor = Brushes.ForestGreen;
                StandardBearishColor = Brushes.IndianRed;
                ExtendedColor = Brushes.Gold;

                QuarterMainWidth = 1;
                QuarterMidWidth = 1;
                MidQuarterWidth = 1;
                TrendWidth = 1;
                NeutralWidth = 1;
                StandardWidth = 1;
                ExtendedWidth = 1;
            }
            else if (State == State.Configure)
            {
                if (!ValidateConfiguration()) return;
                
                AddDataSeries(Bars.Instrument.FullName, BarsPeriodType.Day, 1);
                InitializeLevelConfigurations();
                InitializePriceLevels();
                ResetSessionVariables();
                SetupPersistence();
                isConfigured = true;
            }
            else if (State == State.DataLoaded)
            {
                if (!isConfigured) return;
                
                ClearOutputWindow();
                if (UseAutomaticDate)
                {
                    try
                    {
                        sessionIterator = new SessionIterator(Bars);
                        Print($"VaL: SessionIterator initialized successfully for {Bars.Instrument.FullName}");
                    }
                    catch (Exception ex)
                    {
                        Print($"VaL Error: Failed to initialize SessionIterator - {ex.Message}");
                        return;
                    }
                }

                if (UsePersistence)
                {
                    LoadSavedLevels();
                }
            }
            else if (State == State.Historical)
            {
                if (!Bars.BarsType.IsIntraday)
                {
                    Draw.TextFixed(this, "VaLInfo", "VaL only works on intraday charts", TextPosition.BottomRight);
                    Print("VaL Warning: Indicator only works on intraday charts");
                    return;
                }
                
                SetZOrder(-1);
                
                if (!UseAutomaticDate)
                {
                    CalculateLevelsForSelectedDate();
                }
            }
            else if (State == State.Realtime)
            {
                Print("VaL: Switched to real-time mode");
            }
            else if (State == State.Terminated)
            {
                if (UsePersistence && levelsCalculated)
                {
                    SaveCurrentLevels();
                }
                CleanupResources();
            }
        }

        protected override void OnBarUpdate()
        {
            if (!UseAutomaticDate || !isConfigured) return;

            try
            {
                if (CurrentBar < 1 || sessionIterator == null) return;
                
                DateTime tradingDay = sessionIterator.GetTradingDay(Time[0]);
                if (tradingDay == DateTime.MinValue) return;

                if (currentDate != tradingDay)
                {
                    HandleNewTradingDay(tradingDay);
                }
                else
                {
                    UpdateCurrentDayHighLow();
                }
            }
            catch (Exception ex)
            {
                Print($"VaL Error in OnBarUpdate: {ex.Message}");
            }
        }

        #region Private Methods

        private bool ValidateConfiguration()
        {
            if (Bars?.Instrument == null)
            {
                Print("VaL Error: Invalid instrument configuration");
                return false;
            }
            
            if (MinimumTick <= 0)
            {
                Print("VaL Warning: Invalid MinimumTick value, using default 0.25");
                MinimumTick = 0.25;
            }
            
            return true;
        }

        private void SetupPersistence()
        {
            if (!UsePersistence) return;
            
            try
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string ninjaTraderPath = Path.Combine(documentsPath, "NinjaTrader 8", "bin", "Custom", "Indicators", "VaL_Data");
                
                if (!Directory.Exists(ninjaTraderPath))
                {
                    Directory.CreateDirectory(ninjaTraderPath);
                }
                
                string fileName = $"VaL_Levels_{Bars.Instrument.FullName.Replace(" ", "_").Replace("/", "_")}.xml";
                persistenceFilePath = Path.Combine(ninjaTraderPath, fileName);
                
                Print($"VaL: Persistence file path set to: {persistenceFilePath}");
            }
            catch (Exception ex)
            {
                Print($"VaL Error setting up persistence: {ex.Message}");
                UsePersistence = false;
            }
        }

        private void InitializeLevelConfigurations()
        {
            levelConfigs.Clear();
            
            levelConfigs.Add("Q1", new LevelConfig { Type = LevelType.QuarterMain, DefaultColorName = "Yellow", DefaultWidth = 1 });
            levelConfigs.Add("Q4", new LevelConfig { Type = LevelType.QuarterMain, DefaultColorName = "Yellow", DefaultWidth = 1 });
            levelConfigs.Add("Q2", new LevelConfig { Type = LevelType.QuarterMid, DefaultColorName = "Plum", DefaultWidth = 1 });
            levelConfigs.Add("Q3", new LevelConfig { Type = LevelType.QuarterMid, DefaultColorName = "Plum", DefaultWidth = 1 });
            
            levelConfigs.Add("Q2/3", new LevelConfig { Type = LevelType.MidQuarter, DefaultColorName = "Orange", DefaultWidth = 1 });
            levelConfigs.Add("Q3/4", new LevelConfig { Type = LevelType.MidQuarter, DefaultColorName = "Orange", DefaultWidth = 1 });
            
            levelConfigs.Add("TC", new LevelConfig { Type = LevelType.TrendBullish, DefaultColorName = "ForestGreen", DefaultWidth = 1 });
            levelConfigs.Add("TV", new LevelConfig { Type = LevelType.TrendBearish, DefaultColorName = "Crimson", DefaultWidth = 1 });
            
            levelConfigs.Add("NR1", new LevelConfig { Type = LevelType.Neutral, DefaultColorName = "Cyan", DefaultWidth = 1 });
            levelConfigs.Add("NR2", new LevelConfig { Type = LevelType.Neutral, DefaultColorName = "Cyan", DefaultWidth = 1 });
            levelConfigs.Add("NR3", new LevelConfig { Type = LevelType.Neutral, DefaultColorName = "Cyan", DefaultWidth = 1 });
            
            levelConfigs.Add("Std1+", new LevelConfig { Type = LevelType.StandardBullish, DefaultColorName = "ForestGreen", DefaultWidth = 1 });
            levelConfigs.Add("Std2+", new LevelConfig { Type = LevelType.StandardBullish, DefaultColorName = "ForestGreen", DefaultWidth = 1 });
            levelConfigs.Add("Std3+", new LevelConfig { Type = LevelType.StandardBullish, DefaultColorName = "ForestGreen", DefaultWidth = 1 });
            levelConfigs.Add("Std1-", new LevelConfig { Type = LevelType.StandardBearish, DefaultColorName = "IndianRed", DefaultWidth = 1 });
            levelConfigs.Add("Std2-", new LevelConfig { Type = LevelType.StandardBearish, DefaultColorName = "IndianRed", DefaultWidth = 1 });
            levelConfigs.Add("Std3-", new LevelConfig { Type = LevelType.StandardBearish, DefaultColorName = "IndianRed", DefaultWidth = 1 });
            
            levelConfigs.Add("1D+", new LevelConfig { Type = LevelType.Extended, DefaultColorName = "Gold", DefaultWidth = 1 });
            levelConfigs.Add("1D-", new LevelConfig { Type = LevelType.Extended, DefaultColorName = "Gold", DefaultWidth = 1 });
        }

        private void ResetSessionVariables()
        {
            currentDate = Core.Globals.MinDate;
            currentDayHigh = 0;
            currentDayLow = 0;
            currentDayOpen = 0;
            priorDayHigh = 0;
            priorDayLow = 0;
            sessionIterator = null;
            levelsCalculated = false;
        }

        private void InitializePriceLevels()
        {
            priceLevels.Clear();
            
            foreach (var config in levelConfigs)
            {
                System.Windows.Media.Brush brush = GetBrushForLevelType(config.Value.Type);
                int width = GetWidthForLevelType(config.Value.Type);
                priceLevels.Add(config.Key, new PriceLevel(config.Key, config.Value.Type, brush, width));
            }
        }

        private System.Windows.Media.Brush GetBrushForLevelType(LevelType type)
        {
            switch (type)
            {
                case LevelType.QuarterMain: return QuarterMainColor;
                case LevelType.QuarterMid: return QuarterMidColor;
                case LevelType.MidQuarter: return MidQuarterColor;
                case LevelType.TrendBullish: return TrendBullishColor;
                case LevelType.TrendBearish: return TrendBearishColor;
                case LevelType.Neutral: return NeutralColor;
                case LevelType.StandardBullish: return StandardBullishColor;
                case LevelType.StandardBearish: return StandardBearishColor;
                case LevelType.Extended: return ExtendedColor;
                default: return Brushes.White;
            }
        }

        private int GetWidthForLevelType(LevelType type)
        {
            switch (type)
            {
                case LevelType.QuarterMain: return QuarterMainWidth;
                case LevelType.QuarterMid: return QuarterMidWidth;
                case LevelType.MidQuarter: return MidQuarterWidth;
                case LevelType.TrendBullish: return TrendWidth;
                case LevelType.TrendBearish: return TrendWidth;
                case LevelType.Neutral: return NeutralWidth;
                case LevelType.StandardBullish: return StandardWidth;
                case LevelType.StandardBearish: return StandardWidth;
                case LevelType.Extended: return ExtendedWidth;
                default: return 1;
            }
        }

        private void HandleNewTradingDay(DateTime tradingDay)
        {
            priorDayHigh = currentDayHigh;
            priorDayLow = currentDayLow;

            if (IsValidPriorDayData())
            {
                if (UsePersistence && savedLevels.ContainsKey(tradingDay.Date))
                {
                    LoadLevelsForDate(tradingDay.Date);
                    Print($"VaL: Loaded saved levels for {tradingDay.Date:d}");
                }
                else
                {
                    double priorDayClose = GetPriorDayClose(Time[0]);
                    if (priorDayClose > 0)
                    {
                        double calculatedRange = CalculateRange(priorDayClose);
                        double basePrice = GetBasePrice(priorDayClose);
                        
                        if (calculatedRange > 0 && basePrice > 0)
                        {
                            LogCalculationSummary(calculatedRange, basePrice);
                            CalculateAllLevels(calculatedRange, basePrice);
                            needsLayoutUpdate = true;
                            levelsCalculated = true;
                            
                            PrintAllCalculatedLevels();
                            
                            if (UsePersistence)
                            {
                                SaveLevelsForDate(tradingDay.Date);
                            }
                        }
                    }
                }
            }

            InitializeCurrentDayValues(tradingDay);
        }

        private bool IsValidPriorDayData()
        {
            return priorDayHigh > 0 && priorDayLow > 0 && priorDayHigh >= priorDayLow;
        }

        private void InitializeCurrentDayValues(DateTime tradingDay)
        {
            if (Open.IsValidDataPoint(0) && High.IsValidDataPoint(0) && Low.IsValidDataPoint(0))
            {
                currentDayOpen = Open[0];
                currentDayHigh = High[0];
                currentDayLow = Low[0];
            }
            currentDate = tradingDay;
        }

        private void UpdateCurrentDayHighLow()
        {
            if (High.IsValidDataPoint(0) && Low.IsValidDataPoint(0))
            {
                currentDayHigh = Math.Max(currentDayHigh, High[0]);
                currentDayLow = Math.Min(currentDayLow, Low[0]);
            }
        }

        private void LoadLevelsForDate(DateTime date)
        {
            if (savedLevels.ContainsKey(date))
            {
                var dayLevels = savedLevels[date];
                foreach (var level in priceLevels.Values)
                {
                    if (dayLevels.ContainsKey(level.Name))
                    {
                        level.Value = dayLevels[level.Name];
                    }
                }
                levelsCalculated = true;
                needsLayoutUpdate = true;
            }
        }

        private void SaveLevelsForDate(DateTime date)
        {
            var dayLevels = new Dictionary<string, double>();
            foreach (var level in priceLevels.Values)
            {
                if (!double.IsNaN(level.Value))
                {
                    dayLevels[level.Name] = level.Value;
                }
            }
            
            if (dayLevels.Count > 0)
            {
                savedLevels[date] = dayLevels;
            }
        }

        private void SaveCurrentLevels()
        {
            if (string.IsNullOrEmpty(persistenceFilePath) || savedLevels.Count == 0) return;
            
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                XmlElement root = xmlDoc.CreateElement("VaLLevels");
                xmlDoc.AppendChild(root);
                
                foreach (var dateEntry in savedLevels)
                {
                    XmlElement dateElement = xmlDoc.CreateElement("Date");
                    dateElement.SetAttribute("value", dateEntry.Key.ToString("yyyy-MM-dd"));
                    
                    foreach (var levelEntry in dateEntry.Value)
                    {
                        XmlElement levelElement = xmlDoc.CreateElement("Level");
                        levelElement.SetAttribute("name", levelEntry.Key);
                        levelElement.SetAttribute("value", levelEntry.Value.ToString());
                        dateElement.AppendChild(levelElement);
                    }
                    
                    root.AppendChild(dateElement);
                }
                
                xmlDoc.Save(persistenceFilePath);
                Print($"VaL: Saved {savedLevels.Count} days of level data");
            }
            catch (Exception ex)
            {
                Print($"VaL Error saving levels: {ex.Message}");
            }
        }

        private void LoadSavedLevels()
        {
            if (string.IsNullOrEmpty(persistenceFilePath) || !File.Exists(persistenceFilePath)) return;
            
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(persistenceFilePath);
                
                savedLevels.Clear();
                
                XmlNodeList dateNodes = xmlDoc.SelectNodes("//Date");
                foreach (XmlNode dateNode in dateNodes)
                {
                    if (DateTime.TryParse(dateNode.Attributes["value"].Value, out DateTime date))
                    {
                        var dayLevels = new Dictionary<string, double>();
                        
                        XmlNodeList levelNodes = dateNode.SelectNodes("Level");
                        foreach (XmlNode levelNode in levelNodes)
                        {
                            string levelName = levelNode.Attributes["name"].Value;
                            if (double.TryParse(levelNode.Attributes["value"].Value, out double levelValue))
                            {
                                dayLevels[levelName] = levelValue;
                            }
                        }
                        
                        if (dayLevels.Count > 0)
                        {
                            savedLevels[date] = dayLevels;
                        }
                    }
                }
                
                Print($"VaL: Loaded {savedLevels.Count} days of saved level data");
            }
            catch (Exception ex)
            {
                Print($"VaL Error loading saved levels: {ex.Message}");
            }
        }

        private double CalculateRange(double priorDayClose)
        {
            double baseRange = priorDayHigh - priorDayLow;
            
            if (!UseGapCalculation) return baseRange;
            
            double gap = GetGapValue(priorDayClose);
            return baseRange + Math.Abs(gap);
        }

        private double GetGapValue(double priorDayClose)
        {
            if (BarsArray[1] == null || BarsArray[1].Count == 0) return 0;
            
            int dailyIndex = BarsArray[1].GetBar(Time[0]);
            if (dailyIndex < 0) return 0;
            
            double currentDailyOpen = BarsArray[1].GetOpen(dailyIndex);
            return currentDailyOpen.ApproxCompare(priorDayClose) != 0 ? currentDailyOpen - priorDayClose : 0;
        }

        private double GetBasePrice(double priorDayClose)
        {
            return ManualPrice.ApproxCompare(0) == 0 ? priorDayClose : ManualPrice;
        }

        private void LogCalculationSummary(double range, double basePrice)
        {
            Print("=== VaL Detailed Calculation Summary ===");
            Print($"Trading Date: {currentDate:d}");
            Print($"Prior Day High: {priorDayHigh:F2}");
            Print($"Prior Day Low: {priorDayLow:F2}");
            Print($"Prior Day Range: {(priorDayHigh - priorDayLow):F2}");
            
            double priorDayClose = GetPriorDayClose(Time[0]);
            Print($"Prior Day Close: {priorDayClose:F2}");
            
            if (UseGapCalculation)
            {
                double gap = GetGapValue(priorDayClose);
                Print($"Gap: {gap:F2}");
                Print($"Calculated Range (with Gap): {range:F2}");
            }
            else
            {
                Print($"Gap Calculation: Disabled");
                Print($"Calculated Range: {range:F2}");
            }
            
            Print($"Base Price: {basePrice:F2}");
            Print("=======================================");
        }
        
        private double RoundToTickSize(double value)
        {
            if (MinimumTick <= 0) return value;
            return Math.Round(value / MinimumTick) * MinimumTick;
        }

        private double GetPriorDayClose(DateTime time)
        {
            if (BarsArray[1] == null || BarsArray[1].Count < 2) return 0;

            int dailyIndex = BarsArray[1].GetBar(time);
            if (dailyIndex <= 0) return 0;

            return BarsArray[1].GetClose(dailyIndex - 1);
        }

        private void CalculateLevelsForSelectedDate()
        {
            if (Bars == null || Bars.Count == 0) return;

            Print($"VaL: Calculating levels for selected date: {SelectedDate:d}");

            double highForDay = double.MinValue;
            double lowForDay = double.MaxValue;
            bool dateFound = false;

            SessionIterator sessionIter = new SessionIterator(Bars);
            
            for (int i = 0; i < Bars.Count; i++)
            {
                DateTime barTradingDay = sessionIter.GetTradingDay(Bars.GetTime(i));
                if (barTradingDay.Date == SelectedDate.Date)
                {
                    dateFound = true;
                    highForDay = Math.Max(highForDay, Bars.GetHigh(i));
                    lowForDay = Math.Min(lowForDay, Bars.GetLow(i));
                }
            }

            if (!dateFound)
            {
                Print($"VaL Warning: No data found for selected date: {SelectedDate:d}");
                return;
            }

            double baseRange = highForDay - lowForDay;
            double priorDayClose = GetPriorDayCloseForDate(SelectedDate);
            double gap = 0;
            double calculatedRange = baseRange;
            
            if (UseGapCalculation && priorDayClose > 0)
            {
                gap = GetGapForSelectedDate(SelectedDate, priorDayClose);
                calculatedRange = baseRange + Math.Abs(gap);
            }

            if (calculatedRange > 0)
            {
                double basePrice = GetBasePrice(priorDayClose);
                if (basePrice > 0)
                {
                    Print("=== VaL Detailed Calculation Summary ===");
                    Print($"Selected Date: {SelectedDate:d}");
                    Print($"Prior Day Close: {priorDayClose:F2}");
                    Print($"Day High: {highForDay:F2}");
                    Print($"Day Low: {lowForDay:F2}");
                    Print($"Base Range (High - Low): {baseRange:F2}");
                    if (UseGapCalculation)
                    {
                        Print($"Gap: {gap:F2}");
                        Print($"Calculated Range (with Gap): {calculatedRange:F2}");
                    }
                    else
                    {
                        Print($"Gap Calculation: Disabled");
                        Print($"Calculated Range: {calculatedRange:F2}");
                    }
                    Print($"Base Price: {basePrice:F2}");
                    Print("=======================================");
                    
                    CalculateAllLevels(calculatedRange, basePrice);
                    needsLayoutUpdate = true;
                    levelsCalculated = true;
                    
                    PrintAllCalculatedLevels();
                }
            }
        }

        private double GetPriorDayCloseForDate(DateTime selectedDate)
        {
            if (BarsArray[1] == null || BarsArray[1].Count < 2) return 0;

            int selectedDayIndex = -1;
            for (int i = 0; i < BarsArray[1].Count; i++)
            {
                DateTime dailyBarDate = BarsArray[1].GetTime(i).Date;
                if (dailyBarDate == selectedDate.Date)
                {
                    selectedDayIndex = i;
                    break;
                }
            }

            if (selectedDayIndex > 0)
            {
                return BarsArray[1].GetClose(selectedDayIndex - 1);
            }

            return 0;
        }

        private double GetGapForSelectedDate(DateTime selectedDate, double priorDayClose)
        {
            if (BarsArray[1] == null || BarsArray[1].Count == 0) return 0;

            for (int i = 0; i < BarsArray[1].Count; i++)
            {
                DateTime dailyBarDate = BarsArray[1].GetTime(i).Date;
                if (dailyBarDate == selectedDate.Date)
                {
                    double selectedDayOpen = BarsArray[1].GetOpen(i);
                    double gap = selectedDayOpen - priorDayClose;
                    Print($"VaL Gap Calculation: Selected Day Open {selectedDayOpen:F2} - Prior Close {priorDayClose:F2} = {gap:F2}");
                    return gap;
                }
            }

            return 0;
        }

        private void PrintAllCalculatedLevels()
        {
            Print("=== All Calculated Levels ===");
            
            var sortedLevels = priceLevels.Values
                .Where(level => !double.IsNaN(level.Value))
                .OrderByDescending(level => level.Value)
                .ToList();

            foreach (var level in sortedLevels)
            {
                Print($"{level.Name}: {level.Value:F2}");
            }
            
            Print("=============================");
        }

        private void CalculateAllLevels(double dayRange, double basePrice)
        {
            try
            {
                if (basePrice <= 0 || dayRange <= 0) return;

                double halfRange = dayRange * 0.5;
                double q1Level = RoundToTickSize(basePrice + halfRange);
                double q4Level = RoundToTickSize(basePrice - halfRange);
                
                priceLevels["Q1"].Value = q1Level;
                priceLevels["Q4"].Value = q4Level;
                priceLevels["Q2"].Value = RoundToTickSize(q1Level - (dayRange * 0.25));
                priceLevels["Q3"].Value = RoundToTickSize(q4Level + (dayRange * 0.25));
                
                priceLevels["Q2/3"].Value = RoundToTickSize(q1Level - (dayRange * 0.375));
                priceLevels["Q3/4"].Value = RoundToTickSize(q4Level + (dayRange * 0.375));
                
                priceLevels["NR2"].Value = RoundToTickSize(basePrice);
                
                priceLevels["TC"].Value = RoundToTickSize(q1Level - (dayRange * 0.125));
                priceLevels["TV"].Value = RoundToTickSize(q4Level + (dayRange * 0.125));
                priceLevels["NR1"].Value = RoundToTickSize(q1Level - (dayRange * 0.159));
                priceLevels["NR3"].Value = RoundToTickSize(q4Level + (dayRange * 0.159));
                
                priceLevels["Std1+"].Value = RoundToTickSize(q1Level + (dayRange * 0.125));
                priceLevels["Std2+"].Value = RoundToTickSize(q1Level + (dayRange * 0.25));
                priceLevels["Std3+"].Value = RoundToTickSize(q1Level + (dayRange * 0.375));
                priceLevels["1D+"].Value = RoundToTickSize(q1Level + (dayRange * 0.50));
                
                priceLevels["Std1-"].Value = RoundToTickSize(q4Level - (dayRange * 0.125));
                priceLevels["Std2-"].Value = RoundToTickSize(q4Level - (dayRange * 0.25));
                priceLevels["Std3-"].Value = RoundToTickSize(q4Level - (dayRange * 0.375));
                priceLevels["1D-"].Value = RoundToTickSize(q4Level - (dayRange * 0.50));
                
                UpdateLevelAppearance();
                
                Print($"VaL: All levels calculated successfully. Q1: {q1Level:F2}, Q4: {q4Level:F2}");
            }
            catch (Exception ex)
            {
                Print($"VaL Error in CalculateAllLevels: {ex.Message}");
            }
        }

        private void UpdateLevelAppearance()
        {
            foreach (var level in priceLevels.Values)
            {
                level.LineBrush = GetBrushForLevelType(level.Type);
                level.LineWidth = GetWidthForLevelType(level.Type);
            }
        }

        private void UpdateTextLayouts()
        {
            if (ChartControl == null || !ShowLabels) return;
            
            TextFormat textFormat = null;
            try
            {
                textFormat = ChartControl.Properties.LabelFont.ToDirectWriteTextFormat();
                
                foreach (var level in priceLevels.Values)
                {
                    level.LabelLayout?.Dispose();
                    
                    if (double.IsNaN(level.Value))
                    {
                        level.LabelLayout = null;
                        continue;
                    }
                    
                    string labelText = $"{level.Name} {level.Value:F2}";
                    level.LabelLayout = new TextLayout(Core.Globals.DirectWriteFactory, labelText, 
                        textFormat, ChartPanel.W, textFormat.FontSize);
                }
            }
            finally
            {
                textFormat?.Dispose();
            }
        }

        private SharpDX.Direct2D1.Brush GetDxBrush(System.Windows.Media.Brush wpfBrush)
        {
            if (dxBrushes.TryGetValue(wpfBrush, out SharpDX.Direct2D1.Brush dxBrush))
                return dxBrush;

            dxBrush = wpfBrush.ToDxBrush(RenderTarget);
            dxBrushes[wpfBrush] = dxBrush;
            return dxBrush;
        }

        private void CleanupResources()
        {
            try
            {
                foreach (var brush in dxBrushes.Values)
                    brush?.Dispose();
                dxBrushes.Clear();

                foreach (var level in priceLevels.Values)
                    level?.Dispose();
                priceLevels.Clear();
                
                sessionIterator = null;
                
                Print("VaL: Resources cleaned up successfully");
            }
            catch (Exception ex)
            {
                Print($"VaL Error during cleanup: {ex.Message}");
            }
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (!levelsCalculated || priceLevels.Count == 0) return;
            
            if (!priceLevels.TryGetValue("Q1", out PriceLevel q1) || double.IsNaN(q1.Value)) return;

            if (needsLayoutUpdate)
            {
                UpdateTextLayouts();
                needsLayoutUpdate = false;
            }

            int lastBarIndex = ChartBars.ToIndex;
            if (lastBarIndex < 0) return;

            double lastBarX = chartControl.GetXByBarIndex(ChartBars, lastBarIndex);
            
            double firstVisibleBarX = chartControl.GetXByBarIndex(ChartBars, ChartBars.FromIndex);
            double lastVisibleBarX = chartControl.GetXByBarIndex(ChartBars, ChartBars.ToIndex);
            
            float lineStartX = (float)Math.Max(firstVisibleBarX - 50, ChartPanel.X); 
            float lineEndX = (float)Math.Min(lastVisibleBarX + LineBufferPixels, ChartPanel.X + ChartPanel.W);
            
            if (lineEndX <= lineStartX) return;

            float labelX = lineEndX + 5;
            SharpDX.Direct2D1.Brush labelBrush = ShowLabels ? GetDxBrush(chartControl.Properties.ChartText) : null;

            foreach (var level in priceLevels.Values)
            {
                if (double.IsNaN(level.Value)) continue;

                float y = (float)chartScale.GetYByValue(level.Value);
                
                RenderTarget.DrawLine(
                    new SharpDX.Vector2(lineStartX, y),
                    new SharpDX.Vector2(lineEndX, y),
                    GetDxBrush(level.LineBrush),
                    level.LineWidth
                );
                
                if (ShowLabels && level.LabelLayout != null && labelBrush != null)
                {
                    Point textPoint = new Point(labelX, y - level.LabelLayout.Metrics.Height * 0.5);
                    RenderTarget.DrawTextLayout(textPoint.ToVector2(), level.LabelLayout, labelBrush);
                }
            }
        }
        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Use Automatic Date", Description = "If true, calculates range from the prior day automatically. If false, uses the 'Selected Date' below.", Order = 1, GroupName = "Parameters")]
        public bool UseAutomaticDate { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Gap Calculation", Description = "If true, adds the opening gap (up or down) to the previous day's range.", Order = 2, GroupName = "Parameters")]
        public bool UseGapCalculation { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Persistence", Description = "If true, saves and loads calculated levels for future sessions.", Order = 3, GroupName = "Parameters")]
        public bool UsePersistence { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Selected Date", Description = "Date for calculating levels (only used if 'Use Automatic Date' is false).", Order = 4, GroupName = "Parameters")]
        public DateTime SelectedDate
        {
            get { return selectedDate; }
            set { selectedDate = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "Manual Price", Description = "Base price for levels. If 0, uses prior day's close.", Order = 5, GroupName = "Parameters")]
        public double ManualPrice
        {
            get { return manualPrice; }
            set { manualPrice = Math.Max(0, value); }
        }

        [NinjaScriptProperty]
        [Range(0.01, double.MaxValue)]
        [Display(Name = "Minimum Tick Size", Description = "Minimum tick size for rounding levels (e.g., 0.25 for ES).", Order = 6, GroupName = "Parameters")]
        public double MinimumTick { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Line Buffer (Pixels)", Description = "Pixel buffer from the last bar for line drawing.", Order = 1, GroupName = "Visuals")]
        public int LineBufferPixels { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Description = "Show/hide price level labels.", Order = 2, GroupName = "Visuals")]
        public bool ShowLabels { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Quarter Main Color", Description = "Color for Q1, Q4 levels", Order = 1, GroupName = "Colors")]
        public System.Windows.Media.Brush QuarterMainColor { get; set; }
        [Browsable(false)]
        public string QuarterMainColorSerializable
        {
            get { return Serialize.BrushToString(QuarterMainColor); }
            set { QuarterMainColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Quarter Mid Color", Description = "Color for Q2, Q3 levels", Order = 2, GroupName = "Colors")]
        public System.Windows.Media.Brush QuarterMidColor { get; set; }
        [Browsable(false)]
        public string QuarterMidColorSerializable
        {
            get { return Serialize.BrushToString(QuarterMidColor); }
            set { QuarterMidColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Mid-Quarter Color", Description = "Color for Q2/3, Q3/4 levels", Order = 3, GroupName = "Colors")]
        public System.Windows.Media.Brush MidQuarterColor { get; set; }
        [Browsable(false)]
        public string MidQuarterColorSerializable
        {
            get { return Serialize.BrushToString(MidQuarterColor); }
            set { MidQuarterColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Trend Bullish Color", Description = "Color for TC level", Order = 4, GroupName = "Colors")]
        public System.Windows.Media.Brush TrendBullishColor { get; set; }
        [Browsable(false)]
        public string TrendBullishColorSerializable
        {
            get { return Serialize.BrushToString(TrendBullishColor); }
            set { TrendBullishColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Trend Bearish Color", Description = "Color for TV level", Order = 5, GroupName = "Colors")]
        public System.Windows.Media.Brush TrendBearishColor { get; set; }
        [Browsable(false)]
        public string TrendBearishColorSerializable
        {
            get { return Serialize.BrushToString(TrendBearishColor); }
            set { TrendBearishColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Neutral Color", Description = "Color for NR1, NR2, NR3 levels", Order = 6, GroupName = "Colors")]
        public System.Windows.Media.Brush NeutralColor { get; set; }
        [Browsable(false)]
        public string NeutralColorSerializable
        {
            get { return Serialize.BrushToString(NeutralColor); }
            set { NeutralColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Standard Bullish Color", Description = "Color for Std levels above Q1", Order = 7, GroupName = "Colors")]
        public System.Windows.Media.Brush StandardBullishColor { get; set; }
        [Browsable(false)]
        public string StandardBullishColorSerializable
        {
            get { return Serialize.BrushToString(StandardBullishColor); }
            set { StandardBullishColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Standard Bearish Color", Description = "Color for Std levels below Q4", Order = 8, GroupName = "Colors")]
        public System.Windows.Media.Brush StandardBearishColor { get; set; }
        [Browsable(false)]
        public string StandardBearishColorSerializable
        {
            get { return Serialize.BrushToString(StandardBearishColor); }
            set { StandardBearishColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Extended Color", Description = "Color for 1D+, 1D- levels", Order = 9, GroupName = "Colors")]
        public System.Windows.Media.Brush ExtendedColor { get; set; }
        [Browsable(false)]
        public string ExtendedColorSerializable
        {
            get { return Serialize.BrushToString(ExtendedColor); }
            set { ExtendedColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Quarter Main Width", Description = "Line width for Q1, Q4 levels", Order = 1, GroupName = "Line Widths")]
        public int QuarterMainWidth { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Quarter Mid Width", Description = "Line width for Q2, Q3 levels", Order = 2, GroupName = "Line Widths")]
        public int QuarterMidWidth { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Mid-Quarter Width", Description = "Line width for Q2/3, Q3/4 levels", Order = 3, GroupName = "Line Widths")]
        public int MidQuarterWidth { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Trend Width", Description = "Line width for TC, TV levels", Order = 4, GroupName = "Line Widths")]
        public int TrendWidth { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Neutral Width", Description = "Line width for NR1, NR2, NR3 levels", Order = 5, GroupName = "Line Widths")]
        public int NeutralWidth { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Standard Width", Description = "Line width for Std levels", Order = 6, GroupName = "Line Widths")]
        public int StandardWidth { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Extended Width", Description = "Line width for 1D+, 1D- levels", Order = 7, GroupName = "Line Widths")]
        public int ExtendedWidth { get; set; }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private VaL[] cacheVaL;
        public VaL VaL(bool useAutomaticDate, bool useGapCalculation, bool usePersistence, DateTime selectedDate, double manualPrice, double minimumTick, int lineBufferPixels, bool showLabels, System.Windows.Media.Brush quarterMainColor, System.Windows.Media.Brush quarterMidColor, System.Windows.Media.Brush midQuarterColor, System.Windows.Media.Brush trendBullishColor, System.Windows.Media.Brush trendBearishColor, System.Windows.Media.Brush neutralColor, System.Windows.Media.Brush standardBullishColor, System.Windows.Media.Brush standardBearishColor, System.Windows.Media.Brush extendedColor, int quarterMainWidth, int quarterMidWidth, int midQuarterWidth, int trendWidth, int neutralWidth, int standardWidth, int extendedWidth)
        {
            return VaL(Input, useAutomaticDate, useGapCalculation, usePersistence, selectedDate, manualPrice, minimumTick, lineBufferPixels, showLabels, quarterMainColor, quarterMidColor, midQuarterColor, trendBullishColor, trendBearishColor, neutralColor, standardBullishColor, standardBearishColor, extendedColor, quarterMainWidth, quarterMidWidth, midQuarterWidth, trendWidth, neutralWidth, standardWidth, extendedWidth);
        }

        public VaL VaL(ISeries<double> input, bool useAutomaticDate, bool useGapCalculation, bool usePersistence, DateTime selectedDate, double manualPrice, double minimumTick, int lineBufferPixels, bool showLabels, System.Windows.Media.Brush quarterMainColor, System.Windows.Media.Brush quarterMidColor, System.Windows.Media.Brush midQuarterColor, System.Windows.Media.Brush trendBullishColor, System.Windows.Media.Brush trendBearishColor, System.Windows.Media.Brush neutralColor, System.Windows.Media.Brush standardBullishColor, System.Windows.Media.Brush standardBearishColor, System.Windows.Media.Brush extendedColor, int quarterMainWidth, int quarterMidWidth, int midQuarterWidth, int trendWidth, int neutralWidth, int standardWidth, int extendedWidth)
        {
            if (cacheVaL != null)
                for (int idx = 0; idx < cacheVaL.Length; idx++)
                    if (cacheVaL[idx] != null && 
                        cacheVaL[idx].UseAutomaticDate == useAutomaticDate && 
                        cacheVaL[idx].UseGapCalculation == useGapCalculation && 
                        cacheVaL[idx].UsePersistence == usePersistence && 
                        cacheVaL[idx].SelectedDate == selectedDate && 
                        cacheVaL[idx].ManualPrice == manualPrice && 
                        cacheVaL[idx].MinimumTick == minimumTick && 
                        cacheVaL[idx].LineBufferPixels == lineBufferPixels && 
                        cacheVaL[idx].ShowLabels == showLabels && 
                        cacheVaL[idx].QuarterMainColor == quarterMainColor && 
                        cacheVaL[idx].QuarterMidColor == quarterMidColor && 
                        cacheVaL[idx].MidQuarterColor == midQuarterColor && 
                        cacheVaL[idx].TrendBullishColor == trendBullishColor && 
                        cacheVaL[idx].TrendBearishColor == trendBearishColor && 
                        cacheVaL[idx].NeutralColor == neutralColor && 
                        cacheVaL[idx].StandardBullishColor == standardBullishColor && 
                        cacheVaL[idx].StandardBearishColor == standardBearishColor && 
                        cacheVaL[idx].ExtendedColor == extendedColor && 
                        cacheVaL[idx].QuarterMainWidth == quarterMainWidth && 
                        cacheVaL[idx].QuarterMidWidth == quarterMidWidth && 
                        cacheVaL[idx].MidQuarterWidth == midQuarterWidth && 
                        cacheVaL[idx].TrendWidth == trendWidth && 
                        cacheVaL[idx].NeutralWidth == neutralWidth && 
                        cacheVaL[idx].StandardWidth == standardWidth && 
                        cacheVaL[idx].ExtendedWidth == extendedWidth && 
                        cacheVaL[idx].EqualsInput(input))
                        return cacheVaL[idx];
            return CacheIndicator<VaL>(new VaL()
            { 
                UseAutomaticDate = useAutomaticDate, 
                UseGapCalculation = useGapCalculation, 
                UsePersistence = usePersistence, 
                SelectedDate = selectedDate, 
                ManualPrice = manualPrice, 
                MinimumTick = minimumTick, 
                LineBufferPixels = lineBufferPixels, 
                ShowLabels = showLabels, 
                QuarterMainColor = quarterMainColor, 
                QuarterMidColor = quarterMidColor, 
                MidQuarterColor = midQuarterColor, 
                TrendBullishColor = trendBullishColor, 
                TrendBearishColor = trendBearishColor, 
                NeutralColor = neutralColor, 
                StandardBullishColor = standardBullishColor, 
                StandardBearishColor = standardBearishColor, 
                ExtendedColor = extendedColor, 
                QuarterMainWidth = quarterMainWidth, 
                QuarterMidWidth = quarterMidWidth, 
                MidQuarterWidth = midQuarterWidth, 
                TrendWidth = trendWidth, 
                NeutralWidth = neutralWidth, 
                StandardWidth = standardWidth, 
                ExtendedWidth = extendedWidth 
            }, input, ref cacheVaL);
        }
    }
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.VaL VaL(bool useAutomaticDate, bool useGapCalculation, bool usePersistence, DateTime selectedDate, double manualPrice, double minimumTick, int lineBufferPixels, bool showLabels, System.Windows.Media.Brush quarterMainColor, System.Windows.Media.Brush quarterMidColor, System.Windows.Media.Brush midQuarterColor, System.Windows.Media.Brush trendBullishColor, System.Windows.Media.Brush trendBearishColor, System.Windows.Media.Brush neutralColor, System.Windows.Media.Brush standardBullishColor, System.Windows.Media.Brush standardBearishColor, System.Windows.Media.Brush extendedColor, int quarterMainWidth, int quarterMidWidth, int midQuarterWidth, int trendWidth, int neutralWidth, int standardWidth, int extendedWidth)
        {
            return indicator.VaL(Input, useAutomaticDate, useGapCalculation, usePersistence, selectedDate, manualPrice, minimumTick, lineBufferPixels, showLabels, quarterMainColor, quarterMidColor, midQuarterColor, trendBullishColor, trendBearishColor, neutralColor, standardBullishColor, standardBearishColor, extendedColor, quarterMainWidth, quarterMidWidth, midQuarterWidth, trendWidth, neutralWidth, standardWidth, extendedWidth);
        }

        public Indicators.VaL VaL(ISeries<double> input, bool useAutomaticDate, bool useGapCalculation, bool usePersistence, DateTime selectedDate, double manualPrice, double minimumTick, int lineBufferPixels, bool showLabels, System.Windows.Media.Brush quarterMainColor, System.Windows.Media.Brush quarterMidColor, System.Windows.Media.Brush midQuarterColor, System.Windows.Media.Brush trendBullishColor, System.Windows.Media.Brush trendBearishColor, System.Windows.Media.Brush neutralColor, System.Windows.Media.Brush standardBullishColor, System.Windows.Media.Brush standardBearishColor, System.Windows.Media.Brush extendedColor, int quarterMainWidth, int quarterMidWidth, int midQuarterWidth, int trendWidth, int neutralWidth, int standardWidth, int extendedWidth)
        {
            return indicator.VaL(input, useAutomaticDate, useGapCalculation, usePersistence, selectedDate, manualPrice, minimumTick, lineBufferPixels, showLabels, quarterMainColor, quarterMidColor, midQuarterColor, trendBullishColor, trendBearishColor, neutralColor, standardBullishColor, standardBearishColor, extendedColor, quarterMainWidth, quarterMidWidth, midQuarterWidth, trendWidth, neutralWidth, standardWidth, extendedWidth);
        }
    }
}

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.VaL VaL(bool useAutomaticDate, bool useGapCalculation, bool usePersistence, DateTime selectedDate, double manualPrice, double minimumTick, int lineBufferPixels, bool showLabels, System.Windows.Media.Brush quarterMainColor, System.Windows.Media.Brush quarterMidColor, System.Windows.Media.Brush midQuarterColor, System.Windows.Media.Brush trendBullishColor, System.Windows.Media.Brush trendBearishColor, System.Windows.Media.Brush neutralColor, System.Windows.Media.Brush standardBullishColor, System.Windows.Media.Brush standardBearishColor, System.Windows.Media.Brush extendedColor, int quarterMainWidth, int quarterMidWidth, int midQuarterWidth, int trendWidth, int neutralWidth, int standardWidth, int extendedWidth)
        {
            return indicator.VaL(Input, useAutomaticDate, useGapCalculation, usePersistence, selectedDate, manualPrice, minimumTick, lineBufferPixels, showLabels, quarterMainColor, quarterMidColor, midQuarterColor, trendBullishColor, trendBearishColor, neutralColor, standardBullishColor, standardBearishColor, extendedColor, quarterMainWidth, quarterMidWidth, midQuarterWidth, trendWidth, neutralWidth, standardWidth, extendedWidth);
        }

        public Indicators.VaL VaL(ISeries<double> input, bool useAutomaticDate, bool useGapCalculation, bool usePersistence, DateTime selectedDate, double manualPrice, double minimumTick, int lineBufferPixels, bool showLabels, System.Windows.Media.Brush quarterMainColor, System.Windows.Media.Brush quarterMidColor, System.Windows.Media.Brush midQuarterColor, System.Windows.Media.Brush trendBullishColor, System.Windows.Media.Brush trendBearishColor, System.Windows.Media.Brush neutralColor, System.Windows.Media.Brush standardBullishColor, System.Windows.Media.Brush standardBearishColor, System.Windows.Media.Brush extendedColor, int quarterMainWidth, int quarterMidWidth, int midQuarterWidth, int trendWidth, int neutralWidth, int standardWidth, int extendedWidth)
        {
            return indicator.VaL(input, useAutomaticDate, useGapCalculation, usePersistence, selectedDate, manualPrice, minimumTick, lineBufferPixels, showLabels, quarterMainColor, quarterMidColor, midQuarterColor, trendBullishColor, trendBearishColor, neutralColor, standardBullishColor, standardBearishColor, extendedColor, quarterMainWidth, quarterMidWidth, midQuarterWidth, trendWidth, neutralWidth, standardWidth, extendedWidth);
        }
    }
}

#endregion