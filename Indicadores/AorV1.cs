using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;

namespace NinjaTrader.NinjaScript.Indicators
{
    public class AorV1 : Indicator
    {
        #region Variables
        private double openingRangeHigh = 0;
        private double openingRangeLow = double.MaxValue;
        private double openingRangeOpen = 0;

        private bool rangeEstablished = false;
        private DateTime currentSessionDate = DateTime.MinValue;
        private SessionIterator sessionIterator;

        private int rangeStartBar = -1;
        private int rangeEndBar = -1;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Draws the opening range, midpoint, and configurable extension levels.";
                Name = "AorV1";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                IsSuspendedWhileInactive = true;

                // Time Settings
                OpeningRangeStartTime = new TimeSpan(9, 30, 0);
                OpeningRangeEndTime = new TimeSpan(10, 0, 0);

                // Display Options
                ShadeOpeningRange = true;
                ShowMidpoint = true;
                ShowExtensions1 = true;
                ShowExtensions2 = false;
                ShowPriceLabels = true;

                // Extension Multipliers
                Extension1Multiplier = 1.0;
                Extension2Multiplier = 1.5;

                // Visuals - Range
                RangeColor = Brushes.CornflowerBlue;
                RangeStyle = DashStyleHelper.Dot;
                RangeWidth = 2;
                ShadeColor = Brushes.CornflowerBlue;
                ShadeOpacity = 15;

                // Visuals - Midpoint
                MidpointColor = Brushes.Gray;
                MidpointStyle = DashStyleHelper.Dash;
                MidpointWidth = 1;

                // Visuals - Extensions
                ExtensionColor = Brushes.Orange;
                ExtensionStyle = DashStyleHelper.Dash;
                ExtensionWidth = 1;

                // Visuals - Labels
                LabelTextColor = Brushes.White;
                LabelOffset = 5;
            }
            else if (State == State.DataLoaded)
            {
                sessionIterator = new SessionIterator(Bars);
            }
        }

        protected override void OnBarUpdate()
        {
            if (Bars == null || CurrentBar < 1 || sessionIterator == null)
                return;

            DateTime sessionDate = sessionIterator.GetTradingDay(Time[0]);

            // Reset on a new session
            if (sessionDate != currentSessionDate)
            {
                currentSessionDate = sessionDate;
                openingRangeHigh = 0;
                openingRangeLow = double.MaxValue;
                openingRangeOpen = 0;
                rangeEstablished = false;
                rangeStartBar = -1;
                rangeEndBar = -1;
            }

            TimeSpan currentTime = Time[0].TimeOfDay;

            // Step 1: Capture the opening range high and low
            if (!rangeEstablished && currentTime >= OpeningRangeStartTime && currentTime <= OpeningRangeEndTime)
            {
                if (openingRangeOpen == 0) openingRangeOpen = Open[0];
                openingRangeHigh = Math.Max(openingRangeHigh, High[0]);
                openingRangeLow = Math.Min(openingRangeLow, Low[0]);

                if (rangeStartBar == -1) rangeStartBar = CurrentBar;
                rangeEndBar = CurrentBar;
            }

            // Step 2: Range is established, draw the levels on the bar AFTER the range ends
            if (!rangeEstablished && currentTime > OpeningRangeEndTime)
            {
                if (openingRangeHigh <= 0 || openingRangeLow == double.MaxValue)
                    return;

                rangeEstablished = true;

                int barsAgo = CurrentBar - rangeEndBar;
                int futureBars = -250; // Draw lines 250 bars into the future

                // Draw Shaded Region
                if (ShadeOpeningRange)
                {
                    Color semiTransparentColor = Color.FromArgb((byte)(ShadeOpacity * 2.55), ((SolidColorBrush)ShadeColor).Color.R, ((SolidColorBrush)ShadeColor).Color.G, ((SolidColorBrush)ShadeColor).Color.B);
                    Draw.Rectangle(this, "OR_Shade", false, CurrentBar - rangeStartBar, openingRangeLow, barsAgo, openingRangeHigh, Brushes.Transparent, new SolidColorBrush(semiTransparentColor), 0);
                }

                // Draw High and Low Lines
                Draw.Line(this, "OR_High", false, barsAgo, openingRangeHigh, futureBars, openingRangeHigh, RangeColor, RangeStyle, RangeWidth);
                Draw.Line(this, "OR_Low", false, barsAgo, openingRangeLow, futureBars, openingRangeLow, RangeColor, RangeStyle, RangeWidth);

                double rangeSize = openingRangeHigh - openingRangeLow;
                double midpoint = openingRangeLow + rangeSize / 2;

                // Draw Price Labels
                if (ShowPriceLabels)
                {
                    double priceOffset = LabelOffset * TickSize;
                    double rangeInPoints = rangeSize / TickSize;
                    string highLabel = string.Format("IB H {0} (Rango: {1} pts)", Instrument.MasterInstrument.FormatPrice(openingRangeHigh), rangeInPoints);
                    string lowLabel = string.Format("IB L {0} (Rango: {1} pts)", Instrument.MasterInstrument.FormatPrice(openingRangeLow), rangeInPoints);

                    Draw.Text(this, "IB_H_Label", highLabel, -10, openingRangeHigh + priceOffset, LabelTextColor);
                    Draw.Text(this, "IB_L_Label", lowLabel, -10, openingRangeLow - priceOffset, LabelTextColor);
                }

                // Draw Midpoint
                if (ShowMidpoint)
                {
                    Draw.Line(this, "OR_Midpoint", false, barsAgo, midpoint, futureBars, midpoint, MidpointColor, MidpointStyle, MidpointWidth);
                }

                // Draw Extension Set 1
                if (ShowExtensions1 && Extension1Multiplier > 0)
                {
                    double ext1Up = openingRangeHigh + (rangeSize * Extension1Multiplier);
                    double ext1Down = openingRangeLow - (rangeSize * Extension1Multiplier);
                    Draw.Line(this, "OR_Ext1_Up", false, barsAgo, ext1Up, futureBars, ext1Up, ExtensionColor, ExtensionStyle, ExtensionWidth);
                    Draw.Line(this, "OR_Ext1_Down", false, barsAgo, ext1Down, futureBars, ext1Down, ExtensionColor, ExtensionStyle, ExtensionWidth);
                }

                // Draw Extension Set 2
                if (ShowExtensions2 && Extension2Multiplier > 0)
                {
                    double ext2Up = openingRangeHigh + (rangeSize * Extension2Multiplier);
                    double ext2Down = openingRangeLow - (rangeSize * Extension2Multiplier);
                    Draw.Line(this, "OR_Ext2_Up", false, barsAgo, ext2Up, futureBars, ext2Up, ExtensionColor, ExtensionStyle, ExtensionWidth);
                    Draw.Line(this, "OR_Ext2_Down", false, barsAgo, ext2Down, futureBars, ext2Down, ExtensionColor, ExtensionStyle, ExtensionWidth);
                }
            }
        }

        #region Properties
        [NinjaScriptProperty]
        [Display(Name="Opening Range Start Time", Description="The start time of the opening range.", Order=1, GroupName="Time Settings")]
        public TimeSpan OpeningRangeStartTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Opening Range End Time", Description="The end time of the opening range.", Order=2, GroupName="Time Settings")]
        public TimeSpan OpeningRangeEndTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Shade Opening Range", Description="If true, shades the opening range area.", Order=3, GroupName="Display Options")]
        public bool ShadeOpeningRange { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Midpoint", Description="If true, shows the midpoint of the opening range.", Order=4, GroupName="Display Options")]
        public bool ShowMidpoint { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Extensions 1", Description="If true, shows the first set of extension lines.", Order=5, GroupName="Display Options")]
        public bool ShowExtensions1 { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Extensions 2", Description="If true, shows the second set of extension lines.", Order=6, GroupName="Display Options")]
        public bool ShowExtensions2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Price Labels", Description="If true, shows IB H and IB L price labels.", Order=7, GroupName="Display Options")]
        public bool ShowPriceLabels { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Extension 1 Multiplier", Description="The multiplier for the first extension level.", Order=8, GroupName="Extension Multipliers")]
        public double Extension1Multiplier { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Extension 2 Multiplier", Description="The multiplier for the second extension level.", Order=9, GroupName="Extension Multipliers")]
        public double Extension2Multiplier { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="Range Color", Description="Color for the High/Low lines.", Order=10, GroupName="Visuals - Range")]
        public Brush RangeColor { get; set; }

        [Browsable(false)]
        public string RangeColorSerializable
        {
            get { return Serialize.BrushToString(RangeColor); }
            set { RangeColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name="Range Line Style", Description="Dash style for the High/Low lines.", Order=11, GroupName="Visuals - Range")]
        public DashStyleHelper RangeStyle { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Range Line Width", Description="Width for the High/Low lines.", Order=12, GroupName="Visuals - Range")]
        public int RangeWidth { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="Shade Color", Description="Color for the shaded opening range area.", Order=13, GroupName="Visuals - Range")]
        public Brush ShadeColor { get; set; }

        [Browsable(false)]
        public string ShadeColorSerializable
        {
            get { return Serialize.BrushToString(ShadeColor); }
            set { ShadeColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name="Shade Opacity", Description="Opacity for the shaded area (0-100).", Order=14, GroupName="Visuals - Range")]
        public int ShadeOpacity { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="Midpoint Color", Description="Color for the Midpoint line.", Order=15, GroupName="Visuals - Midpoint")]
        public Brush MidpointColor { get; set; }

        [Browsable(false)]
        public string MidpointColorSerializable
        {
            get { return Serialize.BrushToString(MidpointColor); }
            set { MidpointColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name="Midpoint Line Style", Description="Dash style for the Midpoint line.", Order=16, GroupName="Visuals - Midpoint")]
        public DashStyleHelper MidpointStyle { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Midpoint Line Width", Description="Width for the Midpoint line.", Order=17, GroupName="Visuals - Midpoint")]
        public int MidpointWidth { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="Extension Color", Description="Color for the Extension lines.", Order=18, GroupName="Visuals - Extensions")]
        public Brush ExtensionColor { get; set; }

        [Browsable(false)]
        public string ExtensionColorSerializable
        {
            get { return Serialize.BrushToString(ExtensionColor); }
            set { ExtensionColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name="Extension Line Style", Description="Dash style for the Extension lines.", Order=19, GroupName="Visuals - Extensions")]
        public DashStyleHelper ExtensionStyle { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Extension Line Width", Description="Width for the Extension lines.", Order=20, GroupName="Visuals - Extensions")]
        public int ExtensionWidth { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name="Label Text Color", Description="Color for the IB H/L price labels.", Order=21, GroupName="Visuals - Labels")]
        public Brush LabelTextColor { get; set; }

        [Browsable(false)]
        public string LabelTextColorSerializable
        {
            get { return Serialize.BrushToString(LabelTextColor); }
            set { LabelTextColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name="Label Offset (Ticks)", Description="Vertical offset in ticks for the price labels.", Order=22, GroupName="Visuals - Labels")]
        public int LabelOffset { get; set; }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private AorV1[] cacheAorV1;
        public AorV1 AorV1(TimeSpan openingRangeStartTime, TimeSpan openingRangeEndTime, bool shadeOpeningRange, bool showMidpoint, bool showExtensions1, bool showExtensions2, bool showPriceLabels, double extension1Multiplier, double extension2Multiplier, Brush rangeColor, DashStyleHelper rangeStyle, int rangeWidth, Brush shadeColor, int shadeOpacity, Brush midpointColor, DashStyleHelper midpointStyle, int midpointWidth, Brush extensionColor, DashStyleHelper extensionStyle, int extensionWidth, Brush labelTextColor, int labelOffset)
        {
            return AorV1(Input, openingRangeStartTime, openingRangeEndTime, shadeOpeningRange, showMidpoint, showExtensions1, showExtensions2, showPriceLabels, extension1Multiplier, extension2Multiplier, rangeColor, rangeStyle, rangeWidth, shadeColor, shadeOpacity, midpointColor, midpointStyle, midpointWidth, extensionColor, extensionStyle, extensionWidth, labelTextColor, labelOffset);
        }

        public AorV1 AorV1(ISeries<double> input, TimeSpan openingRangeStartTime, TimeSpan openingRangeEndTime, bool shadeOpeningRange, bool showMidpoint, bool showExtensions1, bool showExtensions2, bool showPriceLabels, double extension1Multiplier, double extension2Multiplier, Brush rangeColor, DashStyleHelper rangeStyle, int rangeWidth, Brush shadeColor, int shadeOpacity, Brush midpointColor, DashStyleHelper midpointStyle, int midpointWidth, Brush extensionColor, DashStyleHelper extensionStyle, int extensionWidth, Brush labelTextColor, int labelOffset)
        {
            if (cacheAorV1 != null)
                for (int idx = 0; idx < cacheAorV1.Length; idx++)
                    if (cacheAorV1[idx] != null && cacheAorV1[idx].OpeningRangeStartTime == openingRangeStartTime && cacheAorV1[idx].OpeningRangeEndTime == openingRangeEndTime && cacheAorV1[idx].ShadeOpeningRange == shadeOpeningRange && cacheAorV1[idx].ShowMidpoint == showMidpoint && cacheAorV1[idx].ShowExtensions1 == showExtensions1 && cacheAorV1[idx].ShowExtensions2 == showExtensions2 && cacheAorV1[idx].ShowPriceLabels == showPriceLabels && cacheAorV1[idx].Extension1Multiplier == extension1Multiplier && cacheAorV1[idx].Extension2Multiplier == extension2Multiplier && cacheAorV1[idx].RangeColor == rangeColor && cacheAorV1[idx].RangeStyle == rangeStyle && cacheAorV1[idx].RangeWidth == rangeWidth && cacheAorV1[idx].ShadeColor == shadeColor && cacheAorV1[idx].ShadeOpacity == shadeOpacity && cacheAorV1[idx].MidpointColor == midpointColor && cacheAorV1[idx].MidpointStyle == midpointStyle && cacheAorV1[idx].MidpointWidth == midpointWidth && cacheAorV1[idx].ExtensionColor == extensionColor && cacheAorV1[idx].ExtensionStyle == extensionStyle && cacheAorV1[idx].ExtensionWidth == extensionWidth && cacheAorV1[idx].LabelTextColor == labelTextColor && cacheAorV1[idx].LabelOffset == labelOffset && cacheAorV1[idx].EqualsInput(input))
                        return cacheAorV1[idx];
            return CacheIndicator<AorV1>(new AorV1(){ OpeningRangeStartTime = openingRangeStartTime, OpeningRangeEndTime = openingRangeEndTime, ShadeOpeningRange = shadeOpeningRange, ShowMidpoint = showMidpoint, ShowExtensions1 = showExtensions1, ShowExtensions2 = showExtensions2, ShowPriceLabels = showPriceLabels, Extension1Multiplier = extension1Multiplier, Extension2Multiplier = extension2Multiplier, RangeColor = rangeColor, RangeStyle = rangeStyle, RangeWidth = rangeWidth, ShadeColor = shadeColor, ShadeOpacity = shadeOpacity, MidpointColor = midpointColor, MidpointStyle = midpointStyle, MidpointWidth = midpointWidth, ExtensionColor = extensionColor, ExtensionStyle = extensionStyle, ExtensionWidth = extensionWidth, LabelTextColor = labelTextColor, LabelOffset = labelOffset }, input, ref cacheAorV1);
        }
    }
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.AorV1 AorV1(TimeSpan openingRangeStartTime, TimeSpan openingRangeEndTime, bool shadeOpeningRange, bool showMidpoint, bool showExtensions1, bool showExtensions2, bool showPriceLabels, double extension1Multiplier, double extension2Multiplier, Brush rangeColor, DashStyleHelper rangeStyle, int rangeWidth, Brush shadeColor, int shadeOpacity, Brush midpointColor, DashStyleHelper midpointStyle, int midpointWidth, Brush extensionColor, DashStyleHelper extensionStyle, int extensionWidth, Brush labelTextColor, int labelOffset)
        {
            return indicator.AorV1(Input, openingRangeStartTime, openingRangeEndTime, shadeOpeningRange, showMidpoint, showExtensions1, showExtensions2, showPriceLabels, extension1Multiplier, extension2Multiplier, rangeColor, rangeStyle, rangeWidth, shadeColor, shadeOpacity, midpointColor, midpointStyle, midpointWidth, extensionColor, extensionStyle, extensionWidth, labelTextColor, labelOffset);
        }

        public Indicators.AorV1 AorV1(ISeries<double> input, TimeSpan openingRangeStartTime, TimeSpan openingRangeEndTime, bool shadeOpeningRange, bool showMidpoint, bool showExtensions1, bool showExtensions2, bool showPriceLabels, double extension1Multiplier, double extension2Multiplier, Brush rangeColor, DashStyleHelper rangeStyle, int rangeWidth, Brush shadeColor, int shadeOpacity, Brush midpointColor, DashStyleHelper midpointStyle, int midpointWidth, Brush extensionColor, DashStyleHelper extensionStyle, int extensionWidth, Brush labelTextColor, int labelOffset)
        {
            return indicator.AorV1(input, openingRangeStartTime, openingRangeEndTime, shadeOpeningRange, showMidpoint, showExtensions1, showExtensions2, showPriceLabels, extension1Multiplier, extension2Multiplier, rangeColor, rangeStyle, rangeWidth, shadeColor, shadeOpacity, midpointColor, midpointStyle, midpointWidth, extensionColor, extensionStyle, extensionWidth, labelTextColor, labelOffset);
        }
    }
}

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.AorV1 AorV1(TimeSpan openingRangeStartTime, TimeSpan openingRangeEndTime, bool shadeOpeningRange, bool showMidpoint, bool showExtensions1, bool showExtensions2, bool showPriceLabels, double extension1Multiplier, double extension2Multiplier, Brush rangeColor, DashStyleHelper rangeStyle, int rangeWidth, Brush shadeColor, int shadeOpacity, Brush midpointColor, DashStyleHelper midpointStyle, int midpointWidth, Brush extensionColor, DashStyleHelper extensionStyle, int extensionWidth, Brush labelTextColor, int labelOffset)
        {
            return indicator.AorV1(Input, openingRangeStartTime, openingRangeEndTime, shadeOpeningRange, showMidpoint, showExtensions1, showExtensions2, showPriceLabels, extension1Multiplier, extension2Multiplier, rangeColor, rangeStyle, rangeWidth, shadeColor, shadeOpacity, midpointColor, midpointStyle, midpointWidth, extensionColor, extensionStyle, extensionWidth, labelTextColor, labelOffset);
        }

        public Indicators.AorV1 AorV1(ISeries<double> input, TimeSpan openingRangeStartTime, TimeSpan openingRangeEndTime, bool shadeOpeningRange, bool showMidpoint, bool showExtensions1, bool showExtensions2, bool showPriceLabels, double extension1Multiplier, double extension2Multiplier, Brush rangeColor, DashStyleHelper rangeStyle, int rangeWidth, Brush shadeColor, int shadeOpacity, Brush midpointColor, DashStyleHelper midpointStyle, int midpointWidth, Brush extensionColor, DashStyleHelper extensionStyle, int extensionWidth, Brush labelTextColor, int labelOffset)
        {
            return indicator.AorV1(input, openingRangeStartTime, openingRangeEndTime, shadeOpeningRange, showMidpoint, showExtensions1, showExtensions2, showPriceLabels, extension1Multiplier, extension2Multiplier, rangeColor, rangeStyle, rangeWidth, shadeColor, shadeOpacity, midpointColor, midpointStyle, midpointWidth, extensionColor, extensionStyle, extensionWidth, labelTextColor, labelOffset);
        }
    }
}

#endregion