\# 📈 Repositorio Completo de Documentación NinjaScript para Indicadores



\## 🎯 Objetivo



Este repositorio es el recurso definitivo para desarrolladores que quieren crear indicadores personalizados en NinjaScript para NinjaTrader 8. Desde principiantes hasta expertos, encontrarás aquí toda la documentación, ejemplos y mejores prácticas necesarias.



\## 📁 Estructura del Repositorio



```

ninjaScript-indicators-guide/

├── README.md

├── docs/

│   ├── 01-fundamentos/

│   ├── 02-estructura-basica/

│   ├── 03-metodos-esenciales/

│   ├── 04-tipos-de-datos/

│   ├── 05-funciones-dibujo/

│   ├── 06-gestion-parametros/

│   ├── 07-debugging-testing/

│   ├── 08-optimizacion/

│   └── 09-recursos-comunidad/

├── examples/

│   ├── basic-indicators/

│   ├── advanced-indicators/

│   └── custom-drawing/

├── templates/

├── tools/

├── troubleshooting/

└── assets/

```



---



\## 📚 DOCUMENTACIÓN COMPLETA



\### 1. 🏗️ \*\*FUNDAMENTOS\*\* (Nivel: Principiante)



\#### 📂 `/docs/01-fundamentos/`



\*\*Descripción\*\*: Conceptos básicos de NinjaScript y configuración del entorno de desarrollo.



\*\*Contenido\*\*:

\- \*\*Introducción a NinjaScript\*\* - Qué es y por qué usarlo

\- \*\*Configuración del entorno\*\* - NinjaScript Editor y Visual Studio

\- \*\*Arquitectura básica\*\* - Cómo funciona NinjaTrader internamente

\- \*\*Tu primer indicador\*\* - Tutorial paso a paso



\*\*Enlaces oficiales\*\*:

\- NinjaTrader Developer Community: https://developer.ninjatrader.com/products/api

\- Documentación oficial: https://developer.ninjatrader.com/docs/desktop

\- Getting Started Guide: https://ninjatrader.com/support/helpguides/nt8/getting\_started\_operations.htm



---



\### 2. 🏛️ \*\*ESTRUCTURA BÁSICA DE INDICADORES\*\* (Nivel: Principiante)



\#### 📂 `/docs/02-estructura-basica/`



\*\*Descripción\*\*: Plantillas y estructura fundamental de un indicador NinjaScript.



\*\*Contenido\*\*:

\- \*\*Template básico\*\* - Estructura mínima de un indicador

\- \*\*Namespaces requeridos\*\* - Using declarations necesarios

\- \*\*Variables y constantes\*\* - Declaración y mejores prácticas

\- \*\*Configuración inicial\*\* - SetDefaults y propiedades básicas



\*\*Código de ejemplo\*\*:

```csharp

namespace NinjaTrader.NinjaScript.Indicators

{

&nbsp;   public class MyCustomIndicator : Indicator

&nbsp;   {

&nbsp;       private const string IndName = "MyCustomIndicator";

&nbsp;       private const string IndVersion = " v1.0";

&nbsp;       

&nbsp;       protected override void OnStateChange()

&nbsp;       {

&nbsp;           if (State == State.SetDefaults)

&nbsp;           {

&nbsp;               Name = IndName + IndVersion;

&nbsp;               IsOverlay = false;

&nbsp;               Calculate = Calculate.OnBarClose;

&nbsp;               DisplayInDataBox = true;

&nbsp;               BarsRequiredToPlot = 20;

&nbsp;           }

&nbsp;       }

&nbsp;       

&nbsp;       protected override void OnBarUpdate()

&nbsp;       {

&nbsp;           // Lógica del indicador aquí

&nbsp;       }

&nbsp;   }

}

```



\*\*Enlaces de referencia\*\*:

\- Plantilla completa de indicador: https://newsletter.huntgathertrade.com/p/indicator-template-for-ninjatrader



---



\### 3. ⚙️ \*\*MÉTODOS Y PROPIEDADES ESENCIALES\*\* (Nivel: Principiante-Intermedio)



\#### 📂 `/docs/03-metodos-esenciales/`



\*\*Descripción\*\*: Métodos fundamentales como OnStateChange(), OnBarUpdate() y propiedades clave.



\*\*Contenido\*\*:



\#### 🔧 \*\*OnStateChange()\*\*

\- \*\*Función\*\*: Método dirigido por eventos llamado cuando el script entra en un nuevo State

\- \*\*Estados principales\*\*:

&nbsp; - `State.SetDefaults` - Configuración inicial

&nbsp; - `State.Configure` - Configuración de datos adicionales  

&nbsp; - `State.DataLoaded` - Inicialización de Series y variables

&nbsp; - `State.Terminated` - Limpieza de recursos



\*\*Ejemplo práctico\*\*:

```csharp

protected override void OnStateChange()

{

&nbsp;   if (State == State.SetDefaults)

&nbsp;   {

&nbsp;       // Configuración por defecto

&nbsp;       SetDefaultSettings();

&nbsp;       SetParameters();

&nbsp;   }

&nbsp;   else if (State == State.Configure)

&nbsp;   {

&nbsp;       ClearOutputWindow(); // Para debugging

&nbsp;   }

&nbsp;   else if (State == State.DataLoaded)

&nbsp;   {

&nbsp;       // Inicializar Series<T> y variables aquí

&nbsp;       mySeries = new Series<double>(this);

&nbsp;   }

&nbsp;   else if (State == State.Terminated)

&nbsp;   {

&nbsp;       // Limpiar recursos no gestionados

&nbsp;   }

}

```



\#### 📊 \*\*OnBarUpdate()\*\*

\- \*\*Función\*\*: Método dirigido por eventos llamado cuando una barra se actualiza. La frecuencia depende de la propiedad "Calculate"

\- \*\*Mejores prácticas\*\*:

&nbsp; - Siempre verificar CurrentBar antes de cálculos

&nbsp; - Usar try-catch para manejo de errores

&nbsp; - Implementar validación de datos



\*\*Ejemplo con validaciones\*\*:

```csharp

protected override void OnBarUpdate()

{

&nbsp;   // Verificar barras suficientes

&nbsp;   if (CurrentBar < BarsRequiredToPlot)

&nbsp;       return;

&nbsp;   

&nbsp;   try

&nbsp;   {

&nbsp;       // Validar datos

&nbsp;       double currentClose = Close\[0];

&nbsp;       if (double.IsNaN(currentClose))

&nbsp;           return;

&nbsp;       

&nbsp;       // Lógica del indicador

&nbsp;       MyPlot\[0] = CalculateValue();

&nbsp;   }

&nbsp;   catch (Exception ex)

&nbsp;   {

&nbsp;       Print($"Error en OnBarUpdate: {ex.Message}");

&nbsp;   }

}

```



\#### 🔍 \*\*Propiedades Clave\*\*

\- \*\*CurrentBar\*\*: Valor representando la barra actual siendo procesada

\- \*\*BarsInProgress\*\*: Para indicadores multi-timeframe

\- \*\*Calculate\*\*: Frecuencia de cálculos (OnBarClose, OnEachTick, OnPriceChange)



\*\*Enlaces oficiales\*\*:

\- OnStateChange(): https://ninjatrader.com/support/helpguides/nt8/onstatechange.htm

\- OnBarUpdate(): https://ninjatrader.com/support/helpguides/nt8/onbarupdate.htm



---



\### 4. 📊 \*\*TIPOS DE DATOS Y VARIABLES\*\* (Nivel: Intermedio)



\#### 📂 `/docs/04-tipos-de-datos/`



\*\*Descripción\*\*: Series<double>, ISeries<T> y otros tipos de datos específicos de NinjaScript.



\#### 🏗️ \*\*Series<T> - Estructura Fundamental\*\*



Series<T> es una estructura de datos genérica especial que puede construirse con cualquier tipo de datos elegido y mantiene una serie de valores igual al número de elementos que barras en un gráfico.



\*\*Características\*\*:

\- Sincronizada automáticamente con barras

\- Acceso mediante índice \[BarsAgo]

\- Implementa ISeries<T> interface

\- Tipos comunes: `Series<double>`, `Series<bool>`, `Series<int>`, `Series<DateTime>`



\*\*Inicialización correcta\*\*:

```csharp

private Series<double> myCustomSeries;

private Series<bool> trendDirection;

private Series<DateTime> signalTimes;



protected override void OnStateChange()

{

&nbsp;   if (State == State.DataLoaded)

&nbsp;   {

&nbsp;       // CRÍTICO: Inicializar en State.DataLoaded

&nbsp;       myCustomSeries = new Series<double>(this);

&nbsp;       trendDirection = new Series<bool>(this);

&nbsp;       signalTimes = new Series<DateTime>(this);

&nbsp;   }

}



protected override void OnBarUpdate()

{

&nbsp;   // Asignar valores usando \[0] para barra actual

&nbsp;   myCustomSeries\[0] = Close\[0] - Open\[0];

&nbsp;   trendDirection\[0] = Close\[0] > Close\[1];

&nbsp;   signalTimes\[0] = Time\[0];

&nbsp;   

&nbsp;   // Acceder a valores históricos

&nbsp;   double previousValue = myCustomSeries\[1];

&nbsp;   bool wasBullish = trendDirection\[2]; // 2 barras atrás

}

```



\#### 🔗 \*\*ISeries<T> Interface\*\*



ISeries<T> permite usar Series<T> como input para métodos de indicadores.



```csharp

// Usar Series<T> como input para indicadores

double smaOfCustomSeries = SMA(myCustomSeries, 20)\[0];

double emaOfCustomSeries = EMA(myCustomSeries, 10)\[0];

```



\#### ⚠️ \*\*Errores Comunes y Soluciones\*\*



Error frecuente: "Cannot convert from 'double' to 'Series<double>'"



\*\*Problema\*\*: Intentar usar variable `double` donde se requiere `Series<T>`

```csharp

// ❌ INCORRECTO

double diff = Math.Abs(Close\[0] - Close\[1]);

double noise = SUM(diff, 10)\[0]; // ERROR!



// ✅ CORRECTO

private Series<double> diffSeries;



protected override void OnStateChange()

{

&nbsp;   if (State == State.DataLoaded)

&nbsp;       diffSeries = new Series<double>(this);

}



protected override void OnBarUpdate()

{

&nbsp;   diffSeries\[0] = Math.Abs(Close\[0] - Close\[1]);

&nbsp;   double noise = SUM(diffSeries, 10)\[0]; // ¡Funciona!

}

```



\#### 📋 \*\*Mejores Prácticas\*\*



1\. \*\*Inicialización\*\*: Siempre inicializar Series<T> en State.DataLoaded

2\. \*\*Sincronización\*\*: No sincronizar Series<T> a series secundarias de barras

3\. \*\*BarsInProgress\*\*: Solo asignar valores durante BarsInProgress == 0 para multi-timeframe



\*\*Enlaces de referencia\*\*:

\- Series<T> Documentation: https://ninjatrader.com/support/helpguides/nt8/seriest.htm

\- Using Series Objects: https://ninjatrader.com/support/helpguides/nt8/using\_a\_series\_or\_dataseries\_o.htm

\- Series Basics Tutorial: https://www.theindicatorclub.com/ninjatrader-training-part-7-series-basics/



---



\### 5. 🎨 \*\*FUNCIONES DE DIBUJO\*\* (Nivel: Intermedio-Avanzado)



\#### 📂 `/docs/05-funciones-dibujo/`



\*\*Descripción\*\*: DrawingTools, SharpDX para gráficos personalizados y métodos de renderizado.



\#### 🖌️ \*\*Draw Methods (Nivel: Intermedio)\*\*



\*\*Ventajas\*\*:

\- Fácil de usar

\- Manejo automático de coordenadas

\- Metadata disponible para manipulación posterior



\*\*Desventajas\*\*:

\- Menor performance: cada método Draw crea una nueva instancia del objeto

\- Limitaciones de personalización



\*\*Ejemplos básicos\*\*:

```csharp

// Dibujar líneas y formas básicas

Draw.Line(this, "TrendLine", false, 10, Low\[10], 0, Low\[0], Brushes.Blue, DashStyleHelper.Solid, 2);

Draw.Ray(this, "SupportRay", false, 0, Low\[0], 1, Low\[1], Brushes.Green, DashStyleHelper.Dash, 2);

Draw.Rectangle(this, "PriceBox", false, 5, High\[5], 0, Low\[0], Brushes.Yellow, Brushes.Transparent, 1);



// Texto y etiquetas

Draw.Text(this, "Signal" + CurrentBar, true, "BUY", 0, Low\[0] - TickSize \* 5, 0, 

&nbsp;        Brushes.White, new SimpleFont("Arial", 10), TextAlignment.Center, 

&nbsp;        Brushes.Green, Brushes.Transparent, 5);



// Dots para señales

Draw.Dot(this, "Entry" + CurrentBar, false, 0, High\[0] + TickSize \* 2, Brushes.Lime);

```



\#### 🚀 \*\*SharpDX Rendering (Nivel: Avanzado)\*\*



NinjaTrader usa SharpDX, una librería .NET open-source que proporciona un wrapper C# para la poderosa API Microsoft DirectX, conocida por su rendimiento acelerado por hardware.



\*\*Ventajas de SharpDX\*\*:

\- Mejor rendimiento: renderizado directo sin objetos intermedios

\- Mayor flexibilidad: control total sobre posición y apariencia

\- Soporte para transparencia/alpha

\- Puede dibujar en cualquier parte del gráfico en cualquier momento



\*\*Estructura básica\*\*:

```csharp

public override void OnRender(ChartControl chartControl, ChartScale chartScale)

{

&nbsp;   // Verificar que RenderTarget esté disponible

&nbsp;   if (RenderTarget == null || ChartBars == null)

&nbsp;       return;



&nbsp;   // Crear recursos SharpDX (solo en OnRender)

&nbsp;   using (var brush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.Blue))

&nbsp;   using (var textFormat = new SharpDX.DirectWrite.TextFormat(Core.Globals.DirectWriteFactory, 

&nbsp;                                                             "Arial", 12.0f))

&nbsp;   {

&nbsp;       // Convertir coordenadas precio/tiempo a píxeles

&nbsp;       float x = chartControl.GetXByBarIndex(ChartBars, CurrentBar);

&nbsp;       float y = chartScale.GetYByValue(Close\[0]);



&nbsp;       // Dibujar línea

&nbsp;       RenderTarget.DrawLine(new SharpDX.Vector2(x - 50, y), 

&nbsp;                            new SharpDX.Vector2(x + 50, y), brush, 2.0f);



&nbsp;       // Dibujar texto

&nbsp;       var textRect = new SharpDX.RectangleF(x - 30, y - 20, 60, 20);

&nbsp;       RenderTarget.DrawText($"{Close\[0]:F2}", textFormat, textRect, brush);

&nbsp;   }

}

```



\#### ⚡ \*\*Comparación de Performance\*\*



Guía de decisión:



| Método | Uso recomendado | Performance | Complejidad |

|--------|----------------|-------------|-------------|

| \*\*Draw Methods\*\* | <40 objetos, funcionalidad estándar | Moderada | Baja |

| \*\*SharpDX\*\* | 100+ objetos, personalización avanzada | Alta | Alta |



\#### 🎯 \*\*Cuándo usar cada método\*\*:



Si dibujas unas pocas docenas de objetos Draw y estás contento con el rendimiento y apariencia, usa métodos Draw. Si tienes cientos de objetos siendo dibujados, o si quieres personalizar la posición o apariencia del texto que los métodos Draw no pueden lograr, renderízalo custom.



\*\*Enlaces de referencia\*\*:

\- SharpDX Custom Rendering: https://ninjatrader.com/support/helpguides/nt8/using\_sharpdx\_for\_custom\_chart\_rendering.htm

\- SharpDX SDK Reference: https://host1.ninjatrader.com/support/helpGuides/nt8/sharpdx\_sdk\_reference.htm

\- Drawing Tools: https://ninjatrader.com/support/helpGuides/nt8/drawing\_tools.htm



---



\### 6. 🔧 \*\*GESTIÓN DE PARÁMETROS\*\* (Nivel: Principiante-Intermedio)



\#### 📂 `/docs/06-gestion-parametros/`



\*\*Descripción\*\*: Implementación de inputs del usuario, validación y configuración de parámetros.



\#### 📝 \*\*NinjaScript Properties\*\*



Los parámetros permiten a los usuarios personalizar indicadores sin modificar código.



\*\*Estructura básica\*\*:

```csharp

\#region Parameters

private int period = 14;

private double multiplier = 2.0;

private bool showSignals = true;



\[NinjaScriptProperty]

\[Range(1, int.MaxValue)]

\[Display(Name = "Period", GroupName = "Parameters", Order = 1)]

public int Period

{

&nbsp;   get { return period; }

&nbsp;   set { period = Math.Max(1, value); }

}



\[NinjaScriptProperty]

\[Range(0.1, 10.0)]

\[Display(Name = "Multiplier", GroupName = "Parameters", Order = 2)]

public double Multiplier

{

&nbsp;   get { return multiplier; }

&nbsp;   set { multiplier = Math.Max(0.1, value); }

}



\[NinjaScriptProperty]

\[Display(Name = "Show Signals", GroupName = "Visual", Order = 3)]

public bool ShowSignals

{

&nbsp;   get { return showSignals; }

&nbsp;   set { showSignals = value; }

}

\#endregion

```



\#### 🎨 \*\*Atributos Display Avanzados\*\*



```csharp

// Dropdown/ComboBox

public enum TrendMode

{

&nbsp;   Simple,

&nbsp;   Weighted,

&nbsp;   Exponential

}



private TrendMode trendMode = TrendMode.Simple;



\[NinjaScriptProperty]

\[Display(Name = "Trend Mode", GroupName = "Algorithm", Order = 1)]

public TrendMode TrendCalculation

{

&nbsp;   get { return trendMode; }

&nbsp;   set { trendMode = value; }

}



// Color picker

private Brush upColor = Brushes.Lime;



\[NinjaScriptProperty]

\[XmlIgnore]

\[Display(Name = "Up Color", GroupName = "Visual", Order = 2)]

public Brush UpColor

{

&nbsp;   get { return upColor; }

&nbsp;   set { upColor = value; }

}



\[Browsable(false)]

public string UpColorSerializable

{

&nbsp;   get { return Serialize.BrushToString(upColor); }

&nbsp;   set { upColor = Serialize.StringToBrush(value); }

}

```



\#### ✅ \*\*Validación de Parámetros\*\*



```csharp

// En SetDefaults

private void SetParameters()

{

&nbsp;   Period = 14;

&nbsp;   Multiplier = 2.0;

&nbsp;   ShowSignals = true;

}



// Validación en OnStateChange

protected override void OnStateChange()

{

&nbsp;   if (State == State.SetDefaults)

&nbsp;   {

&nbsp;       SetParameters();

&nbsp;   }

&nbsp;   else if (State == State.Configure)

&nbsp;   {

&nbsp;       // Validar parámetros críticos

&nbsp;       if (Period <= 0)

&nbsp;       {

&nbsp;           Period = 14;

&nbsp;           Print("Warning: Period must be positive. Reset to 14.");

&nbsp;       }

&nbsp;       

&nbsp;       if (Multiplier <= 0)

&nbsp;       {

&nbsp;           Multiplier = 2.0;

&nbsp;           Print("Warning: Multiplier must be positive. Reset to 2.0.");

&nbsp;       }

&nbsp;   }

}

```



\#### 🔄 \*\*Parámetros Dinámicos\*\*



```csharp

// Actualizar indicador cuando parámetros cambian

protected override void OnStateChange()

{

&nbsp;   if (State == State.Configure)

&nbsp;   {

&nbsp;       // Reconfigurar plots basado en parámetros

&nbsp;       if (ShowUpperBand \&\& ShowLowerBand)

&nbsp;       {

&nbsp;           AddPlot(Brushes.Blue, "MiddleLine");

&nbsp;           AddPlot(Brushes.Red, "UpperBand");

&nbsp;           AddPlot(Brushes.Red, "LowerBand");

&nbsp;       }

&nbsp;       else

&nbsp;       {

&nbsp;           AddPlot(Brushes.Blue, "MiddleLine");

&nbsp;       }

&nbsp;   }

}

```



---



\### 7. 🐛 \*\*DEBUGGING Y TESTING\*\* (Nivel: Intermedio-Avanzado)



\#### 📂 `/docs/07-debugging-testing/`



\*\*Descripción\*\*: Técnicas para depurar indicadores y mejores prácticas de testing.



\#### 🔍 \*\*Print() Debugging\*\*



El comando Print imprimirá el valor suministrado a la ventana New -> NinjaScript Output.



\*\*Estrategias de debugging\*\*:

```csharp

protected override void OnBarUpdate()

{

&nbsp;   // Debug básico con Print

&nbsp;   Print($"Bar {CurrentBar}: Close={Close\[0]:F2}, Volume={Volume\[0]}");

&nbsp;   

&nbsp;   // Debug condicional

&nbsp;   if (CurrentBar == 100)

&nbsp;   {

&nbsp;       Print($"Estado en barra 100:");

&nbsp;       Print($"  SMA(20) = {SMA(20)\[0]:F4}");

&nbsp;       Print($"  RSI(14) = {RSI(14, 3)\[0]:F2}");

&nbsp;   }

&nbsp;   

&nbsp;   // Tracing lógica

&nbsp;   Print($"Entrando en OnBarUpdate() - Barra: {CurrentBar}");

&nbsp;   

&nbsp;   if (CrossAbove(RSI(14, 3), 30))

&nbsp;   {

&nbsp;       Print("✓ Señal RSI detectada - CrossAbove 30");

&nbsp;       // Lógica de señal

&nbsp;   }

&nbsp;   

&nbsp;   Print($"Saliendo de OnBarUpdate()");

}

```



\#### 🎯 \*\*Visual Studio Debugging\*\*



Puedes debuggear tus objetos NinjaScript usando Microsoft Visual Studio.



\*\*Pasos para configurar VS debugging\*\*:



1\. \*\*Habilitar Debug Mode\*\*: En NinjaScript Editor, habilitar "Debug Mode" vía menú click-derecho

2\. \*\*Compilar\*\*: Compilar scripts para crear debug DLL

3\. \*\*Abrir VS\*\*: Desde NinjaScript Editor, click en ícono Visual Studio que cargará automáticamente el proyecto

4\. \*\*Attach Process\*\*: En VS, seleccionar Debug, luego Attach to Process, seleccionar NinjaTrader

5\. \*\*Set Breakpoints\*\*: Abrir archivo fuente y configurar breakpoints



\*\*Debugging condicional\*\*:

```csharp

protected override void OnBarUpdate()

{

&nbsp;   // Breakpoint condicional para tiempo específico

&nbsp;   if (ToTime(Time\[0]) == 093000) // 9:30 AM

&nbsp;   {

&nbsp;       // Breakpoint aquí para debugging en horario específico

&nbsp;       double currentPrice = Close\[0];

&nbsp;   }

&nbsp;   

&nbsp;   // Debugging con condiciones complejas

&nbsp;   if (CurrentBar > 50 \&\& RSI(14, 3)\[0] > 70 \&\& Volume\[0] > SMA(Volume, 20)\[0] \* 1.5)

&nbsp;   {

&nbsp;       // Breakpoint para condiciones específicas

&nbsp;       Print("Condición compleja detectada");

&nbsp;   }

}

```



\#### 🧪 \*\*Testing Sistemático\*\*



\*\*1. Unit Testing Pattern\*\*:

```csharp

public class MyIndicatorTests : Indicator

{

&nbsp;   protected override void OnStateChange()

&nbsp;   {

&nbsp;       if (State == State.SetDefaults)

&nbsp;       {

&nbsp;           Name = "MyIndicatorTests";

&nbsp;           IsOverlay = false;

&nbsp;       }

&nbsp;   }

&nbsp;   

&nbsp;   protected override void OnBarUpdate()

&nbsp;   {

&nbsp;       RunTests();

&nbsp;   }

&nbsp;   

&nbsp;   private void RunTests()

&nbsp;   {

&nbsp;       // Test 1: Verificar cálculo básico

&nbsp;       TestBasicCalculation();

&nbsp;       

&nbsp;       // Test 2: Verificar condiciones límite

&nbsp;       TestBoundaryConditions();

&nbsp;       

&nbsp;       // Test 3: Verificar manejo de errores

&nbsp;       TestErrorHandling();

&nbsp;   }

&nbsp;   

&nbsp;   private void TestBasicCalculation()

&nbsp;   {

&nbsp;       if (CurrentBar >= 20)

&nbsp;       {

&nbsp;           double expected = (High\[0] + Low\[0] + Close\[0]) / 3;

&nbsp;           double actual = TypicalPrice\[0];

&nbsp;           

&nbsp;           if (Math.Abs(expected - actual) > 0.0001)

&nbsp;           {

&nbsp;               Print($"❌ Test FAILED: Expected {expected:F4}, Got {actual:F4}");

&nbsp;           }

&nbsp;           else

&nbsp;           {

&nbsp;               Print($"✅ Test PASSED: TypicalPrice calculation correct");

&nbsp;           }

&nbsp;       }

&nbsp;   }

}

```



\#### 📊 \*\*Validación de Precisión\*\*



Para verificar precisión, puedes usar Print() junto con Data Box para comparar valores.



```csharp

protected override void OnBarUpdate()

{

&nbsp;   if (CurrentBar >= BarsRequiredToPlot)

&nbsp;   {

&nbsp;       // Comparar con indicador built-in

&nbsp;       double myMA = CalculateMovingAverage();

&nbsp;       double builtinMA = SMA(20)\[0];

&nbsp;       

&nbsp;       double difference = Math.Abs(myMA - builtinMA);

&nbsp;       if (difference > 0.0001)

&nbsp;       {

&nbsp;           Print($"⚠️  Diferencia detectada en barra {CurrentBar}:");

&nbsp;           Print($"   Mi cálculo: {myMA:F6}");

&nbsp;           Print($"   Built-in:   {builtinMA:F6}");

&nbsp;           Print($"   Diferencia: {difference:F6}");

&nbsp;       }

&nbsp;   }

}

```



\#### 🚨 \*\*Manejo de Errores\*\*



```csharp

protected override void OnBarUpdate()

{

&nbsp;   try

&nbsp;   {

&nbsp;       // Verificación de datos válidos

&nbsp;       if (double.IsNaN(Close\[0]) || double.IsInfinity(Close\[0]))

&nbsp;       {

&nbsp;           Print($"⚠️ Datos inválidos en barra {CurrentBar}: Close={Close\[0]}");

&nbsp;           return;

&nbsp;       }

&nbsp;       

&nbsp;       // Verificación de barras suficientes

&nbsp;       if (CurrentBar < BarsRequiredToPlot)

&nbsp;       {

&nbsp;           Print($"ℹ️ Esperando datos suficientes... {CurrentBar}/{BarsRequiredToPlot}");

&nbsp;           return;

&nbsp;       }

&nbsp;       

&nbsp;       // Lógica principal con manejo de errores

&nbsp;       double result = PerformComplexCalculation();

&nbsp;       

&nbsp;       // Validar resultado

&nbsp;       if (double.IsNaN(result))

&nbsp;       {

&nbsp;           Print($"❌ Cálculo retornó NaN en barra {CurrentBar}");

&nbsp;           return;

&nbsp;       }

&nbsp;       

&nbsp;       MyPlot\[0] = result;

&nbsp;   }

&nbsp;   catch (DivideByZeroException)

&nbsp;   {

&nbsp;       Print($"❌ División por cero en barra {CurrentBar}");

&nbsp;       MyPlot\[0] = MyPlot\[1]; // Usar valor anterior

&nbsp;   }

&nbsp;   catch (IndexOutOfRangeException ex)

&nbsp;   {

&nbsp;       Print($"❌ Índice fuera de rango: {ex.Message}");

&nbsp;   }

&nbsp;   catch (Exception ex)

&nbsp;   {

&nbsp;       Print($"❌ Error inesperado: {ex.GetType().Name} - {ex.Message}");

&nbsp;   }

}

```



\*\*Enlaces de referencia\*\*:

\- Debugging Guide: https://ninjatrader.com/support/helpguides/nt8/debugging\_your\_ninjascript\_cod.htm

\- Visual Studio Debugging: https://ninjatrader.com/support/helpGuides/nt8/visual\_studio\_debugging.htm

\- NinjaScript Output: https://ninjatrader.com/support/helpguides/nt8/output.htm



---



\### 8. ⚡ \*\*OPTIMIZACIÓN DE RENDIMIENTO\*\* (Nivel: Avanzado)



\#### 📂 `/docs/08-optimizacion/`



\*\*Descripción\*\*: Mejores prácticas para código eficiente en tiempo real.



\#### 🚀 \*\*Principios de Optimización\*\*



\*\*1. Minimizar cálculos repetitivos\*\*:

```csharp

// ❌ INEFICIENTE - Calcula SMA en cada acceso

protected override void OnBarUpdate()

{

&nbsp;   if (SMA(20)\[0] > SMA(50)\[0])

&nbsp;   {

&nbsp;       if (Close\[0] > SMA(20)\[0])

&nbsp;       {

&nbsp;           // SMA(20) se calcula múltiples veces

&nbsp;       }

&nbsp;   }

}



// ✅ EFICIENTE - Calcula una vez, usa múltiples veces

private Series<double> fastMA;

private Series<double> slowMA;



protected override void OnStateChange()

{

&nbsp;   if (State == State.DataLoaded)

&nbsp;   {

&nbsp;       fastMA = new Series<double>(this);

&nbsp;       slowMA = new Series<double>(this);

&nbsp;   }

}



protected override void OnBarUpdate()

{

&nbsp;   fastMA\[0] = SMA(20)\[0];

&nbsp;   slowMA\[0] = SMA(50)\[0];

&nbsp;   

&nbsp;   if (fastMA\[0] > slowMA\[0])

&nbsp;   {

&nbsp;       if (Close\[0] > fastMA\[0])

&nbsp;       {

&nbsp;           // Cálculo eficiente

&nbsp;       }

&nbsp;   }

}

```



\#### 💾 \*\*Gestión Inteligente de Memoria\*\*



```csharp

// Cache para cálculos complejos

private readonly Dictionary<string, double> calculationCache = new Dictionary<string, double>();



protected override void OnBarUpdate()

{

&nbsp;   // Usar cache para cálculos costosos

&nbsp;   string cacheKey = $"{CurrentBar}\_{Period}\_{Multiplier}";

&nbsp;   

&nbsp;   if (!calculationCache.ContainsKey(cacheKey))

&nbsp;   {

&nbsp;       calculationCache\[cacheKey] = ExpensiveCalculation();

&nbsp;       

&nbsp;       // Limpiar cache viejo para evitar memory leaks

&nbsp;       if (calculationCache.Count > 1000)

&nbsp;       {

&nbsp;           var oldKeys = calculationCache.Keys.Take(500).ToList();

&nbsp;           foreach (var key in oldKeys)

&nbsp;               calculationCache.Remove(key);

&nbsp;       }

&nbsp;   }

&nbsp;   

&nbsp;   double result = calculationCache\[cacheKey];

&nbsp;   MyPlot\[0] = result;

}

```



\#### 🔄 \*\*Optimización de Loops\*\*



```csharp

// ❌ INEFICIENTE

protected override void OnBarUpdate()

{

&nbsp;   double sum = 0;

&nbsp;   for (int i = 0; i < Period; i++)

&nbsp;   {

&nbsp;       sum += Close\[i] \* Math.Pow(0.9, i); // Pow es costoso

&nbsp;   }

}



// ✅ EFICIENTE - Pre-calcular valores

private double\[] weights;



protected override void OnStateChange()

{

&nbsp;   if (State == State.Configure)

&nbsp;   {

&nbsp;       // Pre-calcular pesos

&nbsp;       weights = new double\[Period];

&nbsp;       for (int i = 0; i < Period; i++)

&nbsp;       {

&nbsp;           weights\[i] = Math.Pow(0.9, i);

&nbsp;       }

&nbsp;   }

}



protected override void OnBarUpdate()

{

&nbsp;   double sum = 0;

&nbsp;   for (int i = 0; i < Period; i++)

&nbsp;   {

&nbsp;       sum += Close\[i] \* weights\[i]; // Sin cálculos costosos

&nbsp;   }

}

```



\#### ⚡ \*\*Calculate Property Optimization\*\*



```csharp

protected override void OnStateChange()

{

&nbsp;   if (State == State.SetDefaults)

&nbsp;   {

&nbsp;       // Elegir estrategia de cálculo basada en necesidades

&nbsp;       if (needsRealTimeUpdates)

&nbsp;       {

&nbsp;           Calculate = Calculate.OnEachTick; // Más CPU, actualizaciones inmediatas

&nbsp;       }

&nbsp;       else if (needsPriceChangeUpdates)

&nbsp;       {

&nbsp;           Calculate = Calculate.OnPriceChange; // Balance rendimiento/actualización

&nbsp;       }

&nbsp;       else

&nbsp;       {

&nbsp;           Calculate = Calculate.OnBarClose; // Menos CPU, datos estables

&nbsp;       }

&nbsp;   }

}

```



\#### 🎨 \*\*Optimización de Drawing\*\*



Usar estructuras de datos correctas es clave para performance. Arrays y dictionaries son a menudo las mejores opciones para indicadores NinjaScript.



```csharp

// ❌ INEFICIENTE - Crear drawing objects repetidamente

protected override void OnBarUpdate()

{

&nbsp;   RemoveDrawObject("TrendLine");

&nbsp;   Draw.Line(this, "TrendLine", false, 10, Low\[10], 0, Low\[0], Brushes.Blue, DashStyleHelper.Solid, 2);

}



// ✅ EFICIENTE - SharpDX para múltiples objetos

public override void OnRender(ChartControl chartControl, ChartScale chartScale)

{

&nbsp;   if (RenderTarget == null) return;

&nbsp;   

&nbsp;   using (var brush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.Blue))

&nbsp;   {

&nbsp;       // Dibujar múltiples elementos en una sola pasada

&nbsp;       for (int i = 0; i < trendLines.Count; i++)

&nbsp;       {

&nbsp;           var line = trendLines\[i];

&nbsp;           RenderTarget.DrawLine(line.Start, line.End, brush, 1.0f);

&nbsp;       }

&nbsp;   }

}

```



\#### 📊 \*\*Profiling y Monitoreo\*\*



```csharp

private System.Diagnostics.Stopwatch performanceTimer;

private double averageExecutionTime;

private int executionCount;



protected override void OnStateChange()

{

&nbsp;   if (State == State.DataLoaded)

&nbsp;   {

&nbsp;       performanceTimer = new System.Diagnostics.Stopwatch();

&nbsp;   }

}



protected override void OnBarUpdate()

{

&nbsp;   performanceTimer.Restart();

&nbsp;   

&nbsp;   // Tu código aquí

&nbsp;   PerformCalculations();

&nbsp;   

&nbsp;   performanceTimer.Stop();

&nbsp;   

&nbsp;   // Tracking de performance

&nbsp;   executionCount++;

&nbsp;   double currentTime = performanceTimer.Elapsed.TotalMilliseconds;

&nbsp;   averageExecutionTime = ((averageExecutionTime \* (executionCount - 1)) + currentTime) / executionCount;

&nbsp;   

&nbsp;   // Alert si performance degrada

&nbsp;   if (executionCount % 1000 == 0)

&nbsp;   {

&nbsp;       Print($"Performance Stats - Avg: {averageExecutionTime:F3}ms, Last: {currentTime:F3}ms");

&nbsp;       

&nbsp;       if (averageExecutionTime > 1.0) // 1ms threshold

&nbsp;       {

&nbsp;           Print("⚠️ Performance warning: Average execution time exceeding 1ms");

&nbsp;       }

&nbsp;   }

}

```



\#### 🔧 \*\*Optimizaciones Específicas NinjaScript\*\*



```csharp

protected override void OnBarUpdate()

{

&nbsp;   // ✅ Salir temprano para ahorrar CPU

&nbsp;   if (CurrentBar < BarsRequiredToPlot)

&nbsp;       return;

&nbsp;   

&nbsp;   // ✅ Usar BarsInProgress para multi-timeframe

&nbsp;   if (BarsInProgress != 0)

&nbsp;       return;

&nbsp;   

&nbsp;   // ✅ Caché valores frecuentemente usados

&nbsp;   double currentClose = Close\[0];

&nbsp;   double previousClose = Close\[1];

&nbsp;   

&nbsp;   // ✅ Evitar accesos repetidos a propiedades

&nbsp;   int currentBarIndex = CurrentBar;

&nbsp;   

&nbsp;   // ✅ Usar operadores eficientes

&nbsp;   bool isUpBar = currentClose > previousClose; // Más rápido que Close\[0] > Close\[1]

&nbsp;   

&nbsp;   // Tu lógica optimizada aquí

&nbsp;   if (isUpBar \&\& currentBarIndex > 20)

&nbsp;   {

&nbsp;       MyPlot\[0] = CalculateOptimizedValue(currentClose, previousClose);

&nbsp;   }

}

```



---



\### 9. 🌐 \*\*RECURSOS DE LA COMUNIDAD\*\* (Todos los niveles)



\#### 📂 `/docs/09-recursos-comunidad/`



\*\*Descripción\*\*: Foros, tutoriales y recursos adicionales de terceros.



\#### 🏛️ \*\*Recursos Oficiales NinjaTrader\*\*



\*\*Documentación Principal\*\*:

\- \*\*NinjaTrader Developer Community\*\*: https://developer.ninjatrader.com/docs/desktop

&nbsp; - Documentación completa y actualizada

&nbsp; - Tutoriales paso a paso

&nbsp; - Referencias API completas

&nbsp; - Nivel: Todos



\- \*\*Getting Started Guide\*\*: https://ninjatrader.com/support/helpguides/nt8/getting\_started\_operations.htm

&nbsp; - Educational Resources

&nbsp; - Language Reference

&nbsp; - Strategy Analyzer

&nbsp; - Nivel: Principiante



\*\*Foros de Soporte Oficial\*\*:

\- \*\*NinjaTrader Support Forum\*\*: https://support.ninjatrader.com/s/?language=en\_US

\- \*\*Indicator Development\*\*: Soporte específico para desarrollo de indicadores

\- \*\*Strategy Development\*\*: Ayuda con estrategias automatizadas

\- \*\*NinjaScript Educational Resources\*\*: Ejemplos y tutoriales

\- \*\*NinjaScript File Sharing\*\*: Compartir y descargar scripts



\#### 📚 \*\*Recursos Educativos Externos\*\*



\*\*Tutoriales Especializados\*\*:



1\. \*\*LuxAlgo - NinjaScript Basics\*\*: https://www.luxalgo.com/blog/ninjascript-basics-for-custom-indicators/

&nbsp;  - Conceptos fundamentales

&nbsp;  - Técnicas avanzadas como órdenes no gestionadas

&nbsp;  - Dynamic position sizing

&nbsp;  - Nivel: Principiante a Avanzado



2\. \*\*Hunt Gather Trade - Templates\*\*: https://newsletter.huntgathertrade.com/

&nbsp;  - Plantillas profesionales de indicadores

&nbsp;  - Plantillas de estrategias

&nbsp;  - Mejores prácticas de estructura

&nbsp;  - Nivel: Intermedio



3\. \*\*TradingDJ - Inside Bar Tutorial\*\*: https://blog.tradingdj.com/tutorial-writing-a-ninjatrader-8-inside-bar-indicator/

&nbsp;  - Tutorial detallado paso a paso

&nbsp;  - Lógica de detección de patrones

&nbsp;  - Nivel: Principiante



4\. \*\*The Indicator Club - Series Training\*\*: https://www.theindicatorclub.com/ninjatrader-training-part-7-series-basics/

&nbsp;  - Series<T> fundamentals

&nbsp;  - Manejo de datos históricos

&nbsp;  - Nivel: Intermedio



5\. \*\*InvestingRobots Programming Guide\*\*: https://investingrobots.com/ninjatrader-programming/

&nbsp;  - Guía comprehensiva de programación

&nbsp;  - Custom indicators y strategies

&nbsp;  - Técnicas de automatización

&nbsp;  - Nivel: Todos



\#### 🐙 \*\*Repositorios GitHub Destacados\*\*



\*\*Código Fuente Abierto\*\*:



1\. \*\*jDoom/NinjaScripts\*\*: https://github.com/jDoom/NinjaScripts

&nbsp;  - Colección de ejemplos y conversiones

&nbsp;  - Proyectos NT7 y NT8

&nbsp;  - 29 stars, activamente mantenido

&nbsp;  - Nivel: Todos



2\. \*\*Oliyad16/Ninja-Trader\*\*: https://github.com/Oliyad16/Ninja-Trader

&nbsp;  - Estrategias automatizadas

&nbsp;  - Focus en backtesting y optimización

&nbsp;  - Ejecución de órdenes en tiempo real

&nbsp;  - Nivel: Avanzado



3\. \*\*twdsje/Ninjatrader-scripts\*\*: https://github.com/twdsje/Ninjatrader-scripts

&nbsp;  - Colección de scripts personalizados

&nbsp;  - Fácil instalación e integración

&nbsp;  - Nivel: Intermedio



4\. \*\*beckerben/NinjaTrader\*\*: https://github.com/beckerben/NinjaTrader

&nbsp;  - Sandbox experimental

&nbsp;  - Estrategias de trading algorítmico

&nbsp;  - Código educativo y experimental

&nbsp;  - Nivel: Avanzado



\*\*Proyectos Especializados\*\*:



Temas destacados en GitHub - ninjatrader:

\- \*\*Unit Testing Framework\*\*: Framework para testing de código NinjaScript

\- \*\*OrderFlowBot\*\*: Bot para trading de order flow

\- \*\*Custom AddOns\*\*: Ventanas y tabs personalizadas

\- \*\*AVWAP Anchored\*\*: Indicador VWAP anclado

\- \*\*52-Week Highs Analysis\*\*: Análisis de máximos anuales



\#### 🔧 \*\*Herramientas de Desarrollo\*\*



\*\*IDEs y Editores\*\*:

\- \*\*Visual Studio Community\*\* (Gratis) - Full debugging support

\- \*\*Visual Studio Code\*\* - Lightweight, extensions disponibles

\- \*\*JetBrains Rider\*\* - IDE profesional para C#



\*\*Herramientas Auxiliares\*\*:

\- \*\*NinjaScript Editor\*\* - Built-in, soporte básico

\- \*\*Git\*\* - Control de versiones (configurar manualmente)

\- \*\*NUnit/xUnit\*\* - Frameworks de testing (para proyectos externos)



\#### 📖 \*\*Recursos de Aprendizaje C#\*\*



Recursos útiles para aprender C#:

\- \*\*Dot Net Perls\*\*: Ejemplos prácticos de C#

\- \*\*W3Schools\*\*: Tutorial interactivo

\- \*\*C# Station\*\*: Fundamentos del lenguaje

\- \*\*Channel 9 C# Fundamentals\*\*: Videos de Microsoft

\- \*\*Pluralsight\*\*: Cursos profesionales (pagado)



\#### 🤝 \*\*Comunidades Activas\*\*



\*\*Foros de Terceros\*\*:

\- \*\*Reddit - r/algotrading\*\*: Discusiones de trading algorítmico

\- \*\*Elite Trader Forums\*\*: Comunidad de traders profesionales

\- \*\*Stack Overflow\*\*: Preguntas técnicas de programación



\*\*Discord/Telegram\*\*:

\- Varios grupos de trading algorítmico

\- Comunidades de desarrolladores NinjaScript

\- Canales de sharing de código



\#### 📊 \*\*Ejemplos de Código Comunitario\*\*



\*\*Indicadores Populares Compartidos\*\*:

\- \*\*Custom Moving Averages\*\*: Variaciones de MA

\- \*\*Volume Profile\*\*: Análisis de volumen por precio

\- \*\*Market Structure\*\*: Identificación de HH, HL, LH, LL

\- \*\*Support/Resistance\*\*: Detección automática de niveles

\- \*\*Pattern Recognition\*\*: Detección de patrones chartistas



\#### 🎯 \*\*Guía de Navegación de Recursos\*\*



\*\*Para Principiantes\*\*:

1\. Empezar con documentación oficial NinjaTrader

2\. Seguir tutoriales básicos de LuxAlgo

3\. Practicar con ejemplos simples de GitHub

4\. Participar en foros oficiales



\*\*Para Intermedios\*\*:

1\. Explorar plantillas de Hunt Gather Trade

2\. Estudiar código en repositorios GitHub activos  

3\. Experimentar con SharpDX tutorials

4\. Contribuir a proyectos open source



\*\*Para Avanzados\*\*:

1\. Revisar código de proyectos complejos

2\. Crear herramientas propias

3\. Mentorizar a desarrolladores junior

4\. Publicar proyectos en GitHub



---



\## 🚀 \*\*EJEMPLOS PRÁCTICOS POR NIVEL\*\*



\### 📂 `/examples/basic-indicators/`



\#### 1. \*\*Simple Moving Average personalizada\*\* (Principiante)

```csharp

// Implementación básica con validación

```



\#### 2. \*\*RSI con señales visuales\*\* (Principiante-Intermedio)  

```csharp

// RSI con draw objects para señales

```



\#### 3. \*\*Bollinger Bands con alertas\*\* (Intermedio)

```csharp

// Bands con sistema de alertas

```



\### 📂 `/examples/advanced-indicators/`



\#### 1. \*\*MACD con histograma personalizado\*\* (Intermedio)

```csharp

// MACD completo con SharpDX rendering

```



\#### 2. \*\*Volume Profile\*\* (Avanzado)

```csharp  

// Análisis de volumen por precio

```



\#### 3. \*\*Multi-Timeframe Indicator\*\* (Avanzado)

```csharp

// Indicador que combina múltiples timeframes

```



\### 📂 `/examples/custom-drawing/`



\#### 1. \*\*Custom Chart Patterns\*\* (Avanzado)

```csharp

// Detección y dibujo de patrones

```



\#### 2. \*\*Dynamic Support/Resistance\*\* (Avanzado) 

```csharp

// Niveles dinámicos con SharpDX

```



---



\## 🛠️ \*\*PLANTILLAS Y HERRAMIENTAS\*\*



\### 📂 `/templates/`



\- \*\*basic-indicator-template.cs\*\*: Plantilla básica

\- \*\*advanced-indicator-template.cs\*\*: Plantilla con Series<T>

\- \*\*sharpdx-template.cs\*\*: Plantilla con renderizado custom

\- \*\*multi-plot-template.cs\*\*: Múltiples plots y líneas

\- \*\*parameter-rich-template.cs\*\*: Muchos parámetros de usuario



\### 📂 `/tools/`



\- \*\*performance-profiler.cs\*\*: Herramienta de profiling

\- \*\*data-validator.cs\*\*: Validador de datos de mercado

\- \*\*test-framework.cs\*\*: Framework básico de testing

\- \*\*code-snippets/\*\*: Snippets comunes de código



---



\## 🚨 \*\*TROUBLESHOOTING\*\*



\### 📂 `/troubleshooting/`



\#### Errores Comunes:

1\. \*\*"Cannot convert from 'double' to 'Series<double>'"\*\*

&nbsp;  - Causa y solución detallada

&nbsp;  

2\. \*\*"Object reference not set to an instance"\*\*

&nbsp;  - Debugging paso a paso

&nbsp;  

3\. \*\*Performance issues\*\*

&nbsp;  - Checklist de optimización

&nbsp;  

4\. \*\*Compilation errors\*\*

&nbsp;  - Guía de resolución



---



\## 📈 \*\*ROADMAP DE APRENDIZAJE\*\*



\### Semana 1-2: Fundamentos

\- \[ ] Configurar entorno

\- \[ ] Crear primer indicador

\- \[ ] Entender OnStateChange y OnBarUpdate

\- \[ ] Dominar Print() debugging



\### Semana 3-4: Conceptos Intermedios  

\- \[ ] Trabajar con Series<T>

\- \[ ] Implementar parámetros de usuario

\- \[ ] Usar Draw methods

\- \[ ] Crear indicador multi-plot



\### Mes 2: Técnicas Avanzadas

\- \[ ] SharpDX rendering

\- \[ ] Multi-timeframe indicators  

\- \[ ] Optimización de performance

\- \[ ] Visual Studio debugging



\### Mes 3+: Maestría

\- \[ ] Contribuir a proyectos open source

\- \[ ] Crear framework propio

\- \[ ] Mentorizar otros desarrolladores

\- \[ ] Publicar indicadores comerciales



---



\## 🤝 \*\*CONTRIBUIR AL REPOSITORIO\*\*



\### Cómo contribuir:

1\. \*\*Fork\*\* el repositorio

2\. \*\*Crear branch\*\* para nueva feature

3\. \*\*Documentar\*\* código extensivamente  

4\. \*\*Testear\*\* en múltiples timeframes

5\. \*\*Crear pull request\*\* con descripción detallada



\### Estándares de código:

\- Seguir convenciones de naming C#

\- Incluir comentarios XML documentation

\- Validar todos los inputs de usuario

\- Implementar manejo de errores robusto

\- Incluir ejemplos de uso



---



\## 📞 \*\*SOPORTE Y CONTACTO\*\*



\- \*\*Issues\*\*: Usar GitHub Issues para reportar bugs

\- \*\*Discussions\*\*: GitHub Discussions para preguntas  

\- \*\*Wiki\*\*: Documentación extendida en Wiki

\- \*\*Releases\*\*: Descargar versiones estables



---



\## 📄 \*\*LICENCIA\*\*



MIT License - Ver archivo LICENSE para detalles completos.



---



\## 🙏 \*\*AGRADECIMIENTOS\*\*



Especial agradecimiento a:

\- \*\*NinjaTrader LLC\*\* por la plataforma y documentación

\- \*\*Comunidad NinjaScript\*\* por compartir conocimiento

\- \*\*Contribuidores open source\*\* por ejemplos y código

\- \*\*Desarrolladores independientes\*\* por tutoriales y guías



---



\*\*🔥 ¡Este repositorio está vivo! Se actualiza constantemente con nuevos ejemplos, mejores prácticas y recursos de la comunidad. ¡Star el repo para estar al tanto de las actualizaciones!\*\*

