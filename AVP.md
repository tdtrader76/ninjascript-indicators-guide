# AVP - Advanced Volume/Price Levels

## Historial de Modificaciones

### 📅 02/10/2025 - 12:01
**Modificación**: Lógica condicional para PDH/PDL en modo 3 días

**Cambio implementado**:
- PDH y PDL ahora solo se muestran cuando son **distintos** de Q1 y Q4 respectivamente
- Se agrega validación con `Math.Abs()` para comparación precisa de valores double
- Ubicación: Líneas 335-345 en `CalculateAndCreateLevels()`

**Lógica**:
```csharp
// Solo agregar PDH si es diferente de Q1
if (Math.Abs(pdh - q1) > double.Epsilon)
{
    AddLevel(dayLevels.Levels, "PDH", pdh, Brushes.Yellow);
}

// Solo agregar PDL si es diferente de Q4
if (Math.Abs(pdl - q4) > double.Epsilon)
{
    AddLevel(dayLevels.Levels, "PDL", pdl, Brushes.Yellow);
}
```

**Resultado**:
- Cuando PDH = Q1 → PDH no aparece (evita duplicados)
- Cuando PDL = Q4 → PDL no aparece (evita duplicados)
- Solo se muestran cuando aportan información adicional

**Copia de seguridad**: `Copias\AVP0210_1201.cs`

---

### 📅 01/10/2025 - 12:00
**Modificación**: Niveles de extensión adicionales R+/R- con control de visibilidad

**Cambios implementados**:
1. **Nueva propiedad**: `ShowExtraLevels` (boolean)
   - Display Name: "Show Extra Levels"
   - GroupName: "Visual"
   - Order: 5
   - Default: `false`

2. **Niveles R+ adicionales**:
   - R+1: Q1 + (dayRange × 0.625) - Color: Gold
   - R+2: Q1 + (dayRange × 0.75) - Color: Gold
   - R+3: Q1 + (dayRange × 0.875) - Color: Gold

3. **Niveles R- adicionales**:
   - R-1: Q4 - (dayRange × 0.625) - Color: Gold
   - R-2: Q4 - (dayRange × 0.75) - Color: Gold
   - R-3: Q4 - (dayRange × 0.875) - Color: Gold

4. **Renderizado condicional**: Líneas 579-581
   - Si `ShowExtraLevels = false` → No se dibujan R+1, R+2, R+3, R-1, R-2, R-3
   - Si `ShowExtraLevels = true` → Se dibujan todos los niveles

**Copia de seguridad**: `Copias\AVP0110_1200.cs`

---

## Niveles Estándar del Indicador

### Niveles Principales (desde Q1 hacia abajo)
 Name  "Q1"     "Maximo día anterior"
 Name  "TC"     "Q1 - rango por 0.0625"
 Name  "ZSell"  "Q1 - rango por 0.125"
 Name  "NR1"    "Q1 - rango por 0.159"
 Name  "Q2"     "Q1 - rango por 0.25"
 Name  "M+"     "Q1 - rango por 0.375"
 Name  "NR2"    "base = Q1 - (Rango/2)"

### Niveles Principales (desde Q4 hacia arriba)
 Name  "M-"     "Q4 + rango por 0.375"
 Name  "Q3"     "Q4 + rango por 0.25"
 Name  "ZBuy"   "Q4 + rango por 0.125"
 Name  "TV"     "Q4 + rango por 0.0625"
 Name  "NR3"    "Q4 + rango por 0.159"
 Name  "Q4"     "mínimo día anterior"

### Extensiones Positivas (desde Q1 hacia arriba)
 Name  "Std1+"  "Q1 + rango por 0.0855"
 Name  "Std2+"  "Q1 + rango por 0.125"
 Name  "Std3+"  "Q1 + rango por 0.25"
 Name  "Std4+"  "Q1 + rango por 0.375"
 Name  "R+"     "Q1 + rango por 0.5"
 Name  "R+1"    "Q1 + rango por 0.625" (Extra - opcional)
 Name  "R+2"    "Q1 + rango por 0.75" (Extra - opcional)
 Name  "R+3"    "Q1 + rango por 0.875" (Extra - opcional)

### Extensiones Negativas (desde Q4 hacia abajo)
 Name  "Std1-"  "Q4 - rango por 0.0855"
 Name  "Std2-"  "Q4 - rango por 0.125"
 Name  "Std3-"  "Q4 - rango por 0.25"
 Name  "Std4-"  "Q4 - rango por 0.375"
 Name  "R-"     "Q4 - rango por 0.50"
 Name  "R-1"    "Q4 - rango por 0.625" (Extra - opcional)
 Name  "R-2"    "Q4 - rango por 0.75" (Extra - opcional)
 Name  "R-3"    "Q4 - rango por 0.875" (Extra - opcional)

### Niveles Especiales (Modo 3 Días)
 Name  "PDH"    "Prior Day High" (solo si PDH ≠ Q1)
 Name  "PDL"    "Prior Day Low" (solo si PDL ≠ Q4)

