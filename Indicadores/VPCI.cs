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
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Indicators
{
    public class VPCI : Indicator
    {
        #region Variables
        private VWMA vwma_c_l;
        private SMA sma_c_l;
        private VWMA vwma_c_s;
        private SMA sma_c_s;
        private SMA sma_v_s;
        private SMA sma_v_l;
        private VWMA vpci_smooth;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Volume Price Confirmation Indicator (VPCI)";
                Name = "VPCI";
                Calculate = Calculate.OnBarClose;
                IsOverlay = false;
                DisplayInDataBox = true;
                IsSuspendedWhileInactive = true;

                ShortPeriod = 5;
                LongPeriod = 55;

                AddPlot(new Stroke(Brushes.DodgerBlue, 2), PlotStyle.Line, "VPCI");
                AddPlot(new Stroke(Brushes.Gray, 1) { DashStyleHelper = DashStyleHelper.Dash }, PlotStyle.Line, "Zero Line");
                AddPlot(new Stroke(Brushes.Goldenrod, 2) { DashStyleHelper = DashStyleHelper.Dash }, PlotStyle.Line, "VPCI_Smooth");
            }
            else if (State == State.DataLoaded)
            {
                vwma_c_l = VWMA(Close, LongPeriod);
                sma_c_l = SMA(Close, LongPeriod);
                vwma_c_s = VWMA(Close, ShortPeriod);
                sma_c_s = SMA(Close, ShortPeriod);
                sma_v_s = SMA(Volume, ShortPeriod);
                sma_v_l = SMA(Volume, LongPeriod);
                vpci_smooth = VWMA(Values[0], ShortPeriod);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < LongPeriod)
                return;

            Print($"--- Bar {CurrentBar} ---");

            // Calculate the components of the VPCI formula
            double vpc = vwma_c_l[0] - sma_c_l[0];
            Print($"VPC = (VWMA(c,l) {vwma_c_l[0]:F4}) - (SMA(c,l) {sma_c_l[0]:F4}) = {vpc:F4}");

            double vpr = 0;
            if (sma_c_s[0] != 0)
            {
                vpr = vwma_c_s[0] / sma_c_s[0];
            }
            Print($"VPR = (VWMA(c,s) {vwma_c_s[0]:F4}) / (SMA(c,s) {sma_c_s[0]:F4}) = {vpr:F4}");

            double vm = 0;
            if (sma_v_l[0] != 0)
            {
                vm = sma_v_s[0] / sma_v_l[0];
            }
            Print($"VM = (SMA(v,s) {sma_v_s[0]:F4}) / (SMA(v,l) {sma_v_l[0]:F4}) = {vm:F4}");

            // Calculate the final VPCI value
            double vpciValue = vpc * vpr * vm;
            Print($"VPCI = {vpc:F4} * {vpr:F4} * {vm:F4} = {vpciValue:F4}");

            // Set the plot values
            Values[0][0] = vpciValue;
            Values[1][0] = 0;
            Values[2][0] = vpci_smooth[0];

            Print($"VPCI_Smooth = {Values[2][0]:F4}");
        }

        #region Properties
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Short Period", Description="Number of periods for the short-term trend.", Order=1, GroupName="Parameters")]
        public int ShortPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Long Period", Description="Number of periods for the long-term trend.", Order=2, GroupName="Parameters")]
        public int LongPeriod { get; set; }
        #endregion
    }
}