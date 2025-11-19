// Copyright 2024 My NinjaScript
// www.example.com
//
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

//NinjaScript generated code. DO NOT EDIT.

namespace NinjaTrader.NinjaScript.Indicators
{
	public class RthVolumeEma : Indicator
	{
		private double lastEmaValue;
		private TimeZoneInfo targetTimeZone;
		private double multiplier;
		private bool isFirstRthBar;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"EMA configurable que solo usa valores de la sesión RTH.";
				Name										= "RthVolumeEma";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= true;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				IsSuspendedWhileInactive					= true;

				EmaPeriod = 14;
				StartTime = 1530;
				EndTime = 2200;
				TargetTimeZoneId = "Central European Standard Time";

				AddPlot(new Stroke(Brushes.DodgerBlue, 2), PlotStyle.Line, "EmaValue");
			}
			else if (State == State.Configure)
			{
			}
			else if (State == State.DataLoaded)
			{
				lastEmaValue = 0;
				multiplier = 2.0 / (EmaPeriod + 1);
				isFirstRthBar = true;
				try
				{
					targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TargetTimeZoneId);
				}
				catch (TimeZoneNotFoundException)
				{
					Log("Error: Zona horaria no encontrada: " + TargetTimeZoneId, LogLevel.Error);
				}
			}
		}

		protected override void OnBarUpdate()
		{
			if (targetTimeZone == null)
			{
				return; // No continuar si la zona horaria no es válida
			}

			DateTime barTimeInTargetZone = TimeZoneInfo.ConvertTimeFromUtc(Time[0], targetTimeZone);
		int timeAsInt = barTimeInTargetZone.Hour * 100 + barTimeInTargetZone.Minute;

			if (timeAsInt >= StartTime && timeAsInt <= EndTime)
			{
				if (isFirstRthBar)
				{
					lastEmaValue = Volume[0];
					isFirstRthBar = false;
				}
				else
				{
					double currentVolume = Volume[0];
					lastEmaValue = (currentVolume * multiplier) + (lastEmaValue * (1 - multiplier));
				}
			}

			if (!isFirstRthBar)
			{
				Value[0] = lastEmaValue;
			}
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Periodo EMA", Description="Número de periodos para la EMA.", Order=1, GroupName="Parámetros")]
		public int EmaPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(0, 2359)]
		[Display(Name="Hora Inicio (HHmm)", Description="Hora de inicio para el cálculo (formato HHmm).", Order=2, GroupName="Parámetros")]
		public int StartTime { get; set; }

		[NinjaScriptProperty]
		[Range(0, 2359)]
		[Display(Name="Hora Fin (HHmm)", Description="Hora de fin para el cálculo (formato HHmm).", Order=3, GroupName="Parámetros")]
		public int EndTime { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Zona Horaria", Description="Zona horaria para el cálculo (ej. 'Central European Standard Time').", Order=4, GroupName="Parámetros")]
		public string TargetTimeZoneId { get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> EmaValue
		{
			get { return Values[0]; }
		}
		#endregion
	}
}

#region NinjaScript generated code. DO NOT EDIT.
// Omitido por brevedad
#endregion
