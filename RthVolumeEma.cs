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
		// Variables para el modo EMA contínua
		private double lastEmaValue;
		private double multiplier;
		private bool isFirstRthBar;

		// Variables para el modo de reinicio por sesión
		private double sessionAccumulatedVolume;
		private int sessionBarCount;

		// Variables comunes
		private TimeZoneInfo targetTimeZone;
		private bool wasInRth;


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
				ResetOnSession = false;

				AddPlot(new Stroke(Brushes.DodgerBlue, 2), PlotStyle.Line, "EmaValue");
			}
			else if (State == State.Configure)
			{
			}
			else if (State == State.DataLoaded)
			{
				// Inicialización para el modo EMA
				lastEmaValue = 0;
				multiplier = 2.0 / (EmaPeriod + 1);
				isFirstRthBar = true;

				// Inicialización para el modo de reinicio
				sessionAccumulatedVolume = 0;
				sessionBarCount = 0;
				wasInRth = false;

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
			bool isInRth = (timeAsInt >= StartTime && timeAsInt <= EndTime);

			if (ResetOnSession)
			{
				// --- MODO: REINICIO POR SESIÓN (Media de Volumen Acumulado) ---

				// Detectar el inicio de una nueva sesión RTH
				if (isInRth && !wasInRth)
				{
					// Es la primera barra de la sesión, reiniciar contadores
					sessionAccumulatedVolume = Volume[0];
					sessionBarCount = 1;
					isFirstRthBar = false; // Indica que ya podemos empezar a dibujar
				}
				// Si estamos dentro de una sesión ya iniciada
				else if (isInRth && wasInRth)
				{
					sessionAccumulatedVolume += Volume[0];
					sessionBarCount++;
				}

				// Calcular el valor solo si estamos en la sesión
				if (isInRth)
				{
					 if (sessionBarCount > 0)
						lastEmaValue = sessionAccumulatedVolume / sessionBarCount;
				}
			}
			else
			{
				// --- MODO: EMA CONTINUA (Lógica Original) ---
				if (isInRth)
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
			}

			// Actualizar el estado para la próxima barra
			wasInRth = isInRth;

			// Dibujar el valor si ya ha sido calculado al menos una vez
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

		[NinjaScriptProperty]
		[Display(Name="Reiniciar en cada sesión", Description="Si es verdadero, reinicia el cálculo al inicio de cada sesión RTH. Si es falso, continúa el cálculo de la EMA del día anterior.", Order=5, GroupName="Parámetros")]
		public bool ResetOnSession { get; set; }

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
