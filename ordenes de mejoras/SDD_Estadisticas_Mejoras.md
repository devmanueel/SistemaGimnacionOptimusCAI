# SDD — Mejoras Módulo Estadísticas (Dashboard)
> Spec-Driven Development — Migración LiveCharts → OxyPlot + Nuevos KPIs + Fix Gastos  
> Versión 1.0 — Mayo 2026  
> Leer COMPLETO antes de tocar cualquier archivo

---

## 0. CONTEXTO VISUAL — Estado actual vs objetivo

### Lo que existe y funciona ✅
- TabControl con 3 pestañas: Caja, Asistencias, Socios
- Filtro de período con ComboBox (Esta semana, Este mes, etc.)
- Tab Caja: KPIs de ingresos/gastos/ganancias + gráfico de líneas de ingresos
- Tab Asistencias: barras por hora + pie chart por actividad
- Tab Socios: nuevos socios + socios inactivos + Top 5

### Lo que hay que cambiar ⚠️
| Problema | Fix |
|----------|-----|
| Gastos siempre en $0 | Leer `caja_movimientos` donde `tipo = 'egreso'` |
| LiveCharts → conflictos o limitaciones | Migrar a OxyPlot.Wpf (ya instalado) |
| Colores de gráficos mejorables | Paleta nueva: verde ingresos, rojo gastos, cyan barras, torta multicolor |
| Estadísticas es una Window separada | Convertir a Page y agregar al menú lateral |
| Faltan KPIs importantes | Agregar: socios activos, membresías por vencer, producto más vendido |

---

## 1. CONDICIONES — Reglas no negociables

```
- C# 7.3 estricto. Sin switch expressions, sin using simplificado
- SQL Server LocalDB: DROP + CREATE en todos los SPs (nunca CREATE OR ALTER)
- Sin SQL inline en DAOs — solo Stored Procedures
- OxyPlot.Wpf: versión 2.1.0 (ya instalada). NO usar LiveCharts en ningún gráfico nuevo
- Desinstalar LiveCharts.Wpf después de migrar todos los gráficos
- SesionManager.EsAdmin: el módulo solo es accesible si es true
- Moneda: FormatoARS.Moneda() para todos los valores en pesos
- SesionManager.UsuarioId en lugar de IDs hardcodeados
- La Page de Estadísticas se agrega al menú lateral de MainWindow
  SOLO si SesionManager.EsAdmin == true (igual que Auditoría)
```

---

## 2. CLASIFICACIÓN — Archivos a crear/modificar

| Archivo | Tipo | Descripción |
|---------|------|-------------|
| `SP_EstadisticasKPIs.sql` | 🟢 Nuevo | KPIs globales del dashboard |
| `SP_EstadisticasCaja.sql` | 🟡 Modificar | Agregar egresos desde caja_movimientos |
| `EstadisticasKPI.cs` | 🟢 Nuevo entity | POCOs de los nuevos KPIs |
| `EstadisticasDAO.cs` | 🟡 Modificar | Agregar métodos de KPIs y egresos |
| `EstadisticasController.cs` | 🟡 Modificar | Exponer KPIs al code-behind |
| `EstadisticasPage.xaml` | 🔴 Reescribir | Window → Page + OxyPlot + nuevos KPIs |
| `EstadisticasPage.xaml.cs` | 🔴 Reescribir | OxyPlot binding + fix gastos + KPIs |
| `MainWindow.xaml.cs` | 🟡 Modificar | Agregar "📊 Estadísticas" al menú admin |

---

## 3. PLAN DE EJECUCIÓN

```
PASO 1  → Ejecutar SP_EstadisticasKPIs.sql (nuevo)
PASO 2  → Ejecutar SP_EstadisticasCaja.sql (modificar para traer egresos)
PASO 3  → Crear EstadisticasKPI.cs en Entities/
PASO 4  → Actualizar EstadisticasDAO.cs
PASO 5  → Actualizar EstadisticasController.cs
PASO 6  → Convertir EstadisticasWindow → EstadisticasPage (cambiar clase base)
PASO 7  → Reemplazar todos los gráficos LiveCharts por OxyPlot en XAML
PASO 8  → Reescribir code-behind con OxyPlot
PASO 9  → Agregar "Estadísticas" al menú lateral en MainWindow.xaml.cs
PASO 10 → Desinstalar LiveCharts.Wpf si no lo usa ningún otro módulo
PASO 11 → Compilar y verificar con checklist
```

---

## 4. CÓDIGO — Implementar exactamente esto

---

### PASO 1 — `SP_EstadisticasKPIs.sql` (nuevo)

```sql
IF OBJECT_ID('sp_EstadisticasKPIs','P') IS NOT NULL DROP PROCEDURE sp_EstadisticasKPIs;
GO
CREATE PROCEDURE sp_EstadisticasKPIs
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);
    DECLARE @En7Dias DATE = DATEADD(DAY, 7, @Hoy);

    SELECT
        -- Total socios activos
        (SELECT COUNT(*) FROM socios
         WHERE activo = 1 AND eliminado_en IS NULL)             AS socios_activos,

        -- Membresías que vencen en los próximos 7 días
        (SELECT COUNT(*) FROM membresias
         WHERE estado = 'activa'
           AND fecha_vencimiento BETWEEN @Hoy AND @En7Dias)     AS membresias_por_vencer,

        -- Producto más vendido del mes actual
        (SELECT TOP 1 p.nombre
         FROM ventas_items vi
         INNER JOIN productos p ON p.id = vi.producto_id
         INNER JOIN ventas v ON v.id = vi.venta_id
         WHERE YEAR(v.creado_en) = YEAR(@Hoy)
           AND MONTH(v.creado_en) = MONTH(@Hoy)
         GROUP BY p.nombre
         ORDER BY SUM(vi.cantidad) DESC)                         AS producto_mas_vendido,

        -- Cantidad vendida del producto más vendido
        (SELECT TOP 1 SUM(vi.cantidad)
         FROM ventas_items vi
         INNER JOIN ventas v ON v.id = vi.venta_id
         WHERE YEAR(v.creado_en) = YEAR(@Hoy)
           AND MONTH(v.creado_en) = MONTH(@Hoy)
         GROUP BY vi.producto_id
         ORDER BY SUM(vi.cantidad) DESC)                         AS producto_mas_vendido_qty,

        -- Nuevos socios este mes
        (SELECT COUNT(*) FROM socios
         WHERE YEAR(creado_en) = YEAR(@Hoy)
           AND MONTH(creado_en) = MONTH(@Hoy)
           AND eliminado_en IS NULL)                             AS nuevos_socios_mes,

        -- Ingresos del mes actual
        (SELECT ISNULL(SUM(monto),0) FROM caja_movimientos
         WHERE LOWER(LTRIM(RTRIM(tipo))) = 'ingreso'
           AND YEAR(creado_en) = YEAR(@Hoy)
           AND MONTH(creado_en) = MONTH(@Hoy))                  AS ingresos_mes,

        -- Egresos del mes actual
        (SELECT ISNULL(SUM(monto),0) FROM caja_movimientos
         WHERE LOWER(LTRIM(RTRIM(tipo))) = 'egreso'
           AND YEAR(creado_en) = YEAR(@Hoy)
           AND MONTH(creado_en) = MONTH(@Hoy))                  AS egresos_mes;
END;
GO
```

---

### PASO 2 — Fix en `sp_ResumenCaja` (o el SP de caja actual)

Buscar el SP que usa `EstadisticasDAO` para los datos de caja y verificar que el cálculo de gastos usa:

```sql
-- INCORRECTO (devuelve siempre 0):
SUM(CASE WHEN tipo = 'Egreso' THEN monto ELSE 0 END)

-- CORRECTO (normalizado para cubrir variaciones):
SUM(CASE WHEN LOWER(LTRIM(RTRIM(tipo))) = 'egreso' THEN monto ELSE 0 END)
```

Aplicar `LOWER(LTRIM(RTRIM()))` en TODOS los CASE que filtran por `tipo` en los SPs de estadísticas. Es el mismo fix que se aplicó en `sp_ReporteTotales`.

Si el SP se llama `sp_EstadisticasCaja` o similar, **reescribirlo completo**:

```sql
IF OBJECT_ID('sp_EstadisticasCajaResumen','P') IS NOT NULL
    DROP PROCEDURE sp_EstadisticasCajaResumen;
GO
CREATE PROCEDURE sp_EstadisticasCajaResumen
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Resultset 1: Totales del período
    SELECT
        ISNULL(SUM(CASE WHEN LOWER(LTRIM(RTRIM(tipo))) = 'ingreso'
                        THEN monto ELSE 0 END), 0) AS total_ingresos,
        ISNULL(SUM(CASE WHEN LOWER(LTRIM(RTRIM(tipo))) = 'egreso'
                        THEN monto ELSE 0 END), 0) AS total_egresos
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta;

    -- Resultset 2: Ingresos agrupados por día
    SELECT
        CAST(creado_en AS DATE)                     AS fecha,
        ISNULL(SUM(monto), 0)                       AS monto
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND LOWER(LTRIM(RTRIM(tipo))) = 'ingreso'
    GROUP BY CAST(creado_en AS DATE)
    ORDER BY CAST(creado_en AS DATE);

    -- Resultset 3: Egresos agrupados por día
    SELECT
        CAST(creado_en AS DATE)                     AS fecha,
        ISNULL(SUM(monto), 0)                       AS monto
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND LOWER(LTRIM(RTRIM(tipo))) = 'egreso'
    GROUP BY CAST(creado_en AS DATE)
    ORDER BY CAST(creado_en AS DATE);
END;
GO
```

---

### PASO 3 — `Entities/EstadisticasKPI.cs` (nuevo)

```csharp
// Entities/EstadisticasKPI.cs — C# 7.3
using System;

namespace Entities
{
    /// <summary>KPIs globales del dashboard — fila única del sp_EstadisticasKPIs.</summary>
    public class EstadisticasKPI
    {
        public int     SociosActivos           { get; set; }
        public int     MembresiasPorVencer      { get; set; }
        public string  ProductoMasVendido       { get; set; }
        public int     ProductoMasVendidoQty    { get; set; }
        public int     NuevosSociosMes          { get; set; }
        public decimal IngresosMes              { get; set; }
        public decimal EgresosMes               { get; set; }

        // Calculadas
        public decimal GananciaMes              => IngresosMes - EgresosMes;

        public string ProductoTexto
        {
            get
            {
                if (string.IsNullOrEmpty(ProductoMasVendido)) return "Sin ventas este mes";
                return ProductoMasVendido + " (" + ProductoMasVendidoQty + " unid.)";
            }
        }

        public string MembresiasPorVencerTexto
        {
            get
            {
                if (MembresiasPorVencer == 0) return "Ninguna";
                if (MembresiasPorVencer == 1) return "1 vence esta semana";
                return MembresiasPorVencer + " vencen esta semana";
            }
        }
    }

    /// <summary>Punto de datos por día para los gráficos de líneas.</summary>
    public class PuntoDiario
    {
        public DateTime Fecha  { get; set; }
        public decimal  Monto  { get; set; }
        public string   FechaTexto => Fecha.ToString("dd/MM");
    }
}
```

---

### PASO 4 — `EstadisticasDAO.cs` — agregar métodos nuevos

Abrir el DAO existente y **agregar** estos métodos a la clase:

```csharp
// Agregar using al inicio si no están:
// using System.Data;
// using System.Data.SqlClient;
// using Entities;
// using System.Collections.Generic;

/// <summary>Obtiene los KPIs globales del dashboard.</summary>
public EstadisticasKPI ObtenerKPIs()
{
    using (var conn = GetConnection())
    {
        conn.Open();
        using (var cmd = new SqlCommand("sp_EstadisticasKPIs", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            using (var r = cmd.ExecuteReader())
                if (r.Read())
                    return new EstadisticasKPI
                    {
                        SociosActivos        = Convert.ToInt32(r["socios_activos"]),
                        MembresiasPorVencer  = Convert.ToInt32(r["membresias_por_vencer"]),
                        ProductoMasVendido   = r["producto_mas_vendido"] as string,
                        ProductoMasVendidoQty= r["producto_mas_vendido_qty"] != DBNull.Value
                                                ? Convert.ToInt32(r["producto_mas_vendido_qty"]) : 0,
                        NuevosSociosMes      = Convert.ToInt32(r["nuevos_socios_mes"]),
                        IngresosMes          = Convert.ToDecimal(r["ingresos_mes"]),
                        EgresosMes           = Convert.ToDecimal(r["egresos_mes"])
                    };
        }
    }
    return new EstadisticasKPI();
}

/// <summary>
/// Retorna (totales, puntosIngresos, puntosEgresos) para el período dado.
/// </summary>
public (decimal totalIngresos, decimal totalEgresos,
        List<PuntoDiario> ingresos, List<PuntoDiario> egresos)
    ObtenerCajaConEgresos(DateTime desde, DateTime hasta)
{
    decimal totalI = 0, totalE = 0;
    var ingresos = new List<PuntoDiario>();
    var egresos  = new List<PuntoDiario>();

    using (var conn = GetConnection())
    {
        conn.Open();
        using (var cmd = new SqlCommand("sp_EstadisticasCajaResumen", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@FechaDesde", desde.Date);
            cmd.Parameters.AddWithValue("@FechaHasta", hasta.Date);

            using (var r = cmd.ExecuteReader())
            {
                // Resultset 1: Totales
                if (r.Read())
                {
                    totalI = Convert.ToDecimal(r["total_ingresos"]);
                    totalE = Convert.ToDecimal(r["total_egresos"]);
                }

                // Resultset 2: Ingresos por día
                if (r.NextResult())
                    while (r.Read())
                        ingresos.Add(new PuntoDiario
                        {
                            Fecha = Convert.ToDateTime(r["fecha"]),
                            Monto = Convert.ToDecimal(r["monto"])
                        });

                // Resultset 3: Egresos por día
                if (r.NextResult())
                    while (r.Read())
                        egresos.Add(new PuntoDiario
                        {
                            Fecha = Convert.ToDateTime(r["fecha"]),
                            Monto = Convert.ToDecimal(r["monto"])
                        });
            }
        }
    }
    return (totalI, totalE, ingresos, egresos);
}
```

---

### PASO 5 — `EstadisticasController.cs` — agregar métodos públicos

```csharp
// Agregar a la clase EstadisticasController existente:

public EstadisticasKPI ObtenerKPIs()
{
    try { return _dao.ObtenerKPIs(); }
    catch { return new EstadisticasKPI(); }
}

public (decimal totalIngresos, decimal totalEgresos,
        List<PuntoDiario> ingresos, List<PuntoDiario> egresos)
    ObtenerCajaConEgresos(DateTime desde, DateTime hasta)
{
    try { return _dao.ObtenerCajaConEgresos(desde, hasta); }
    catch { return (0, 0, new List<PuntoDiario>(), new List<PuntoDiario>()); }
}
```

---

### PASO 6 — Convertir Window → Page

En `EstadisticasPage.xaml`:
```xml
<!-- CAMBIAR la apertura del archivo de: -->
<Window x:Class="SistemaGimnacionOptimusCAI.EstadisticasWindow" ...>
<!-- A: -->
<Page x:Class="SistemaGimnacionOptimusCAI.Paginas.EstadisticasPage" ...>
```

En `EstadisticasPage.xaml.cs`:
```csharp
// CAMBIAR de:
public partial class EstadisticasWindow : Window
// A:
public partial class EstadisticasPage : Page
```

Mover el archivo a la carpeta `Paginas/` si no está ahí.

---

### PASO 7 — Reemplazar gráficos LiveCharts por OxyPlot en XAML

#### Namespace a agregar en la apertura del `<Page>`:
```xml
xmlns:oxy="http://oxyplot.org/wpf"
```

#### Eliminar namespaces de LiveCharts:
```xml
<!-- ELIMINAR estas líneas: -->
xmlns:lvc="clr-namespace:LiveCharts.Wpf;assembly=LiveCharts.Wpf"
```

#### Gráfico de líneas INGRESOS — reemplazar el CartesianChart de LiveCharts:
```xml
<!-- REEMPLAZAR el lvc:CartesianChart de ingresos por: -->
<oxy:PlotView x:Name="plotIngresos"
              Height="200"
              Background="Transparent"
              Margin="0,0,0,8"/>
```

#### Gráfico de líneas GASTOS:
```xml
<oxy:PlotView x:Name="plotGastos"
              Height="200"
              Background="Transparent"/>
```

#### Gráfico de BARRAS por hora (Tab Asistencias):
```xml
<!-- Arriba izquierda — promedio por hora -->
<oxy:PlotView x:Name="plotPromedioHora"
              Height="200"
              Background="Transparent"/>

<!-- Abajo izquierda — porcentaje por hora -->
<oxy:PlotView x:Name="plotPorcentajeHora"
              Height="200"
              Background="Transparent"/>
```

#### Gráfico de PIE (asistencias por actividad):
```xml
<oxy:PlotView x:Name="plotPieActividades"
              Height="200"
              Background="Transparent"/>
```

#### Tab Socios — gráficos:
```xml
<!-- Nuevos socios -->
<oxy:PlotView x:Name="plotNuevosSocios"
              Height="200"
              Background="Transparent"/>

<!-- Socios inactivos -->
<oxy:PlotView x:Name="plotInactivos"
              Height="140"
              Background="Transparent"/>

<!-- Top 5 socios — barras horizontales -->
<oxy:PlotView x:Name="plotTop5"
              Background="Transparent"/>
```

#### Nuevos KPIs — agregar en la cabecera de la Page (antes del TabControl):
```xml
<!-- Panel de KPIs globales -->
<Border Background="#F5F5F5" BorderBrush="#DDDDDD"
        BorderThickness="1" CornerRadius="8"
        Padding="16,10" Margin="0,0,0,12">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- Socios activos -->
        <StackPanel Grid.Column="0" HorizontalAlignment="Center">
            <TextBlock Text="SOCIOS ACTIVOS" FontSize="9" FontWeight="Bold"
                       Foreground="#888" HorizontalAlignment="Center"/>
            <TextBlock x:Name="lblSociosActivos" Text="—"
                       FontSize="28" FontWeight="Bold" Foreground="#00897B"
                       HorizontalAlignment="Center"/>
        </StackPanel>

        <!-- Membresías por vencer -->
        <StackPanel Grid.Column="1" HorizontalAlignment="Center">
            <TextBlock Text="VENCEN ESTA SEMANA" FontSize="9" FontWeight="Bold"
                       Foreground="#888" HorizontalAlignment="Center"/>
            <TextBlock x:Name="lblMembresiasVencer" Text="—"
                       FontSize="22" FontWeight="Bold" Foreground="#E65100"
                       HorizontalAlignment="Center" TextWrapping="Wrap"
                       TextAlignment="Center"/>
        </StackPanel>

        <!-- Producto más vendido -->
        <StackPanel Grid.Column="2" HorizontalAlignment="Center">
            <TextBlock Text="PRODUCTO + VENDIDO (MES)" FontSize="9" FontWeight="Bold"
                       Foreground="#888" HorizontalAlignment="Center"/>
            <TextBlock x:Name="lblProductoTop" Text="—"
                       FontSize="13" FontWeight="Bold" Foreground="#1565C0"
                       HorizontalAlignment="Center" TextWrapping="Wrap"
                       TextAlignment="Center" MaxWidth="180"/>
        </StackPanel>

        <!-- Ingresos del mes -->
        <StackPanel Grid.Column="3" HorizontalAlignment="Center">
            <TextBlock Text="INGRESOS DEL MES" FontSize="9" FontWeight="Bold"
                       Foreground="#888" HorizontalAlignment="Center"/>
            <TextBlock x:Name="lblIngresosMes" Text="—"
                       FontSize="18" FontWeight="Bold" Foreground="#2E7D32"
                       HorizontalAlignment="Center"/>
        </StackPanel>
    </Grid>
</Border>
```

---

### PASO 8 — `EstadisticasPage.xaml.cs` — código completo de OxyPlot

#### Usings necesarios al inicio:
```csharp
using Controllers;
using Entities;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
```

#### Campos privados de la clase:
```csharp
private readonly EstadisticasController _controller = new EstadisticasController();
private DateTime _fechaDesde = DateTime.Today.AddDays(-30);
private DateTime _fechaHasta = DateTime.Today;

// Cultura ARS para formatear moneda
private static readonly CultureInfo _cultAR =
    new CultureInfo("es-AR");
```

#### Método de inicialización (llamar desde constructor o Loaded):
```csharp
private void CargarTodo()
{
    // Validar acceso admin
    if (!SesionManager.EsAdmin)
    {
        MessageBox.Show("Solo el administrador puede ver las estadísticas.",
            "Acceso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    CargarKPIs();
    CargarCaja();
    CargarAsistencias();
    CargarSocios();
}
```

#### Método `CargarKPIs()`:
```csharp
private void CargarKPIs()
{
    try
    {
        var kpi = _controller.ObtenerKPIs();

        lblSociosActivos.Text    = kpi.SociosActivos.ToString();
        lblMembresiasVencer.Text = kpi.MembresiasPorVencerTexto;
        lblProductoTop.Text      = kpi.ProductoTexto;
        lblIngresosMes.Text      = "$ " + kpi.IngresosMes.ToString("N0", _cultAR);
    }
    catch { }
}
```

#### Método `CargarCaja()` con OxyPlot:
```csharp
private void CargarCaja()
{
    try
    {
        var (totalI, totalE, puntosI, puntosE) =
            _controller.ObtenerCajaConEgresos(_fechaDesde, _fechaHasta);

        // KPIs de caja
        lblIngresos.Text  = "$ " + totalI.ToString("N0", _cultAR);
        lblGastos.Text    = "$ " + totalE.ToString("N0", _cultAR);
        lblGanancias.Text = "$ " + (totalI - totalE).ToString("N0", _cultAR);

        // Gráfico ingresos
        plotIngresos.Model = ConstruirGraficoLinea(
            puntosI, "Ingresos por día",
            OxyColor.FromRgb(46, 125, 50),    // verde oscuro
            OxyColor.FromRgb(200, 230, 201));  // verde claro de fondo

        // Gráfico egresos
        plotGastos.Model = ConstruirGraficoLinea(
            puntosE, "Gastos por día",
            OxyColor.FromRgb(183, 28, 28),    // rojo oscuro
            OxyColor.FromRgb(255, 205, 210));  // rojo claro de fondo
    }
    catch (Exception ex)
    {
        MessageBox.Show("Error al cargar caja:\n" + ex.Message);
    }
}

/// <summary>Construye un gráfico de línea OxyPlot con área de fondo.</summary>
private PlotModel ConstruirGraficoLinea(
    List<PuntoDiario> puntos, string titulo,
    OxyColor colorLinea, OxyColor colorArea)
{
    var modelo = new PlotModel
    {
        Title           = titulo,
        TitleFontSize   = 12,
        Background      = OxyColors.Transparent,
        PlotAreaBorderColor = OxyColor.FromRgb(200, 200, 200),
        TextColor       = OxyColor.FromRgb(60, 60, 60),
        Padding         = new OxyThickness(10, 10, 20, 10)
    };

    // Eje X — fechas
    var ejeX = new CategoryAxis
    {
        Position          = AxisPosition.Bottom,
        TextColor         = OxyColor.FromRgb(100, 100, 100),
        TicklineColor     = OxyColor.FromRgb(200, 200, 200),
        MajorGridlineStyle= LineStyle.Dot,
        MajorGridlineColor= OxyColor.FromRgb(220, 220, 220),
        FontSize          = 10,
        Title             = "Días"
    };
    foreach (var p in puntos)
        ejeX.Labels.Add(p.FechaTexto);
    modelo.Axes.Add(ejeX);

    // Eje Y — montos
    var ejeY = new LinearAxis
    {
        Position              = AxisPosition.Left,
        Minimum               = 0,
        MajorGridlineStyle    = LineStyle.Dot,
        MajorGridlineColor    = OxyColor.FromRgb(220, 220, 220),
        TicklineColor         = OxyColors.Transparent,
        TextColor             = OxyColor.FromRgb(100, 100, 100),
        FontSize              = 10,
        Title                 = "Pesos ARS",
        LabelFormatter        = v => "$ " + ((decimal)v).ToString("N0", _cultAR)
    };
    modelo.Axes.Add(ejeY);

    if (puntos.Count == 0)
    {
        // Sin datos — mostrar mensaje centrado
        modelo.Annotations.Add(new TextAnnotation
        {
            Text            = "Sin datos en el período seleccionado",
            TextPosition    = new DataPoint(0.5, 0.5),
            TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
            FontSize        = 11,
            TextColor       = OxyColor.FromRgb(150, 150, 150)
        });
        return modelo;
    }

    // Serie de área (fondo translúcido)
    var serieArea = new AreaSeries
    {
        Color       = colorLinea,
        Fill        = OxyColor.FromAColor(40, colorLinea),
        StrokeThickness = 0
    };

    // Serie de línea con marcadores y etiquetas
    var serieLinea = new LineSeries
    {
        Color           = colorLinea,
        StrokeThickness = 2.5,
        MarkerType      = MarkerType.Circle,
        MarkerSize      = 5,
        MarkerFill      = colorLinea,
        MarkerStroke    = OxyColors.White,
        MarkerStrokeThickness = 1.5,
        LabelFormatString = "$ {1:N0}",    // etiqueta sobre cada punto
        FontSize          = 9
    };

    for (int i = 0; i < puntos.Count; i++)
    {
        serieLinea.Points.Add(new DataPoint(i, (double)puntos[i].Monto));
        serieArea.Points.Add(new DataPoint(i, (double)puntos[i].Monto));
        serieArea.Points2.Add(new DataPoint(i, 0));
    }

    modelo.Series.Add(serieArea);
    modelo.Series.Add(serieLinea);

    return modelo;
}
```

#### Método `CargarAsistencias()` con OxyPlot:
```csharp
private void CargarAsistencias()
{
    try
    {
        // Llamar al método existente del controller que ya funciona
        var porHora      = _controller.ObtenerAsistenciasPorHora(_fechaDesde, _fechaHasta, null);
        var porActividad = _controller.ObtenerAsistenciasPorActividad(_fechaDesde, _fechaHasta);

        // ── Gráfico promedio por hora ─────────────────────────────────
        var modeloProm = new PlotModel
        {
            Title = "Promedio de asistencias por hora",
            TitleFontSize = 11,
            Background = OxyColors.Transparent,
            TextColor = OxyColor.FromRgb(60, 60, 60)
        };

        var ejeXProm = new CategoryAxis
        {
            Position = AxisPosition.Bottom, Title = "Hora",
            TextColor = OxyColor.FromRgb(100, 100, 100), FontSize = 9
        };
        var ejeYProm = new LinearAxis
        {
            Position = AxisPosition.Left, Minimum = 0,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(220, 220, 220),
            TextColor = OxyColor.FromRgb(100, 100, 100), FontSize = 9
        };

        var serieProm = new ColumnSeries
        {
            FillColor       = OxyColor.FromRgb(0, 150, 136),  // teal
            StrokeThickness = 0,
            LabelPlacement  = LabelPlacement.Outside,
            LabelFormatString = "{0:N1}",
            FontSize        = 9
        };

        foreach (var p in porHora)
        {
            ejeXProm.Labels.Add(p.Hora.ToString("D2") + "h");
            serieProm.Items.Add(new ColumnItem((double)p.Promedio));
        }

        modeloProm.Axes.Add(ejeXProm);
        modeloProm.Axes.Add(ejeYProm);
        modeloProm.Series.Add(serieProm);
        plotPromedioHora.Model = modeloProm;

        // ── Gráfico porcentaje por hora ───────────────────────────────
        var modeloPct = new PlotModel
        {
            Title = "Porcentaje de asistencias por hora",
            TitleFontSize = 11,
            Background = OxyColors.Transparent,
            TextColor = OxyColor.FromRgb(60, 60, 60)
        };

        var ejeXPct = new CategoryAxis
        {
            Position = AxisPosition.Bottom, Title = "Hora",
            TextColor = OxyColor.FromRgb(100, 100, 100), FontSize = 9
        };
        var ejeYPct = new LinearAxis
        {
            Position = AxisPosition.Left, Minimum = 0, Maximum = 100,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(220, 220, 220),
            LabelFormatter = v => v.ToString("N0") + "%",
            TextColor = OxyColor.FromRgb(100, 100, 100), FontSize = 9
        };

        var seriePct = new ColumnSeries
        {
            FillColor       = OxyColor.FromRgb(0, 150, 136),
            StrokeThickness = 0,
            LabelPlacement  = LabelPlacement.Outside,
            LabelFormatString = "{0:N1}%",
            FontSize        = 9
        };

        foreach (var p in porHora)
        {
            ejeXPct.Labels.Add(p.Hora.ToString("D2") + "h");
            seriePct.Items.Add(new ColumnItem((double)p.Porcentaje));
        }

        modeloPct.Axes.Add(ejeXPct);
        modeloPct.Axes.Add(ejeYPct);
        modeloPct.Series.Add(seriePct);
        plotPorcentajeHora.Model = modeloPct;

        // ── Pie chart por actividad ───────────────────────────────────
        OxyColor[] coloresPie =
        {
            OxyColor.FromRgb(0, 150, 136),   // teal
            OxyColor.FromRgb(33, 150, 243),  // azul
            OxyColor.FromRgb(255, 152, 0),   // naranja
            OxyColor.FromRgb(156, 39, 176),  // violeta
            OxyColor.FromRgb(244, 67, 54),   // rojo
            OxyColor.FromRgb(76, 175, 80),   // verde
            OxyColor.FromRgb(121, 85, 72),   // marrón
        };

        var modeloPie = new PlotModel
        {
            Title = "Asistencias por actividad",
            TitleFontSize = 11,
            Background = OxyColors.Transparent,
            TextColor = OxyColor.FromRgb(60, 60, 60)
        };

        var seriePie = new PieSeries
        {
            StrokeThickness  = 1.5,
            InsideLabelFormat= "",
            OutsideLabelFormat = "{1}: {0}",
            FontSize         = 10,
            Diameter         = 0.9
        };

        int ci = 0;
        foreach (var a in porActividad)
        {
            seriePie.Slices.Add(new PieSlice(a.Actividad, (double)a.Cantidad)
            {
                Fill = coloresPie[ci % coloresPie.Length]
            });
            ci++;
        }

        modeloPie.Series.Add(seriePie);
        plotPieActividades.Model = modeloPie;
    }
    catch (Exception ex)
    {
        MessageBox.Show("Error al cargar asistencias:\n" + ex.Message);
    }
}
```

#### Método `CargarSocios()` con OxyPlot:
```csharp
private void CargarSocios()
{
    try
    {
        var nuevosPorDia = _controller.ObtenerNuevosSocios(_fechaDesde, _fechaHasta);
        var top5         = _controller.ObtenerTop5Socios(_fechaDesde, _fechaHasta);
        int diasInactivo = ObtenerDiasInactivoSeleccionados();
        int cantInactivos= _controller.ObtenerSociosInactivos(diasInactivo);

        // ── Nuevos socios por día ─────────────────────────────────────
        var modeloNuevos = new PlotModel
        {
            Title = "Nuevos socios", TitleFontSize = 11,
            Background = OxyColors.Transparent,
            TextColor = OxyColor.FromRgb(60, 60, 60)
        };

        var ejeXN = new CategoryAxis
        {
            Position = AxisPosition.Bottom, Title = "Días",
            TextColor = OxyColor.FromRgb(100, 100, 100), FontSize = 9
        };
        var ejeYN = new LinearAxis
        {
            Position = AxisPosition.Left, Minimum = 0,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(220, 220, 220),
            TextColor = OxyColor.FromRgb(100, 100, 100), FontSize = 9
        };

        var serieNuevos = new ColumnSeries
        {
            FillColor = OxyColor.FromRgb(0, 150, 136),
            StrokeThickness = 0,
            LabelPlacement = LabelPlacement.Outside,
            LabelFormatString = "{0}",
            FontSize = 9
        };

        foreach (var n in nuevosPorDia)
        {
            ejeXN.Labels.Add(n.FechaTexto);
            serieNuevos.Items.Add(new ColumnItem((double)n.Cantidad));
        }

        modeloNuevos.Axes.Add(ejeXN);
        modeloNuevos.Axes.Add(ejeYN);
        modeloNuevos.Series.Add(serieNuevos);
        plotNuevosSocios.Model = modeloNuevos;

        // ── Socios inactivos (barra simple) ──────────────────────────
        var modeloInact = new PlotModel
        {
            Title = "Socios que dejaron de asistir",
            TitleFontSize = 11,
            Background = OxyColors.Transparent,
            TextColor = OxyColor.FromRgb(60, 60, 60)
        };

        var ejeXI = new CategoryAxis
        {
            Position = AxisPosition.Bottom, Title = "Días sin asistir",
            TextColor = OxyColor.FromRgb(100, 100, 100), FontSize = 9
        };
        ejeXI.Labels.Add("+" + diasInactivo + " días");

        var ejeYI = new LinearAxis
        {
            Position = AxisPosition.Left, Minimum = 0,
            MajorGridlineStyle = LineStyle.None,
            TextColor = OxyColor.FromRgb(100, 100, 100), FontSize = 9
        };

        var serieInact = new ColumnSeries
        {
            FillColor = OxyColor.FromRgb(158, 158, 158),
            StrokeThickness = 0,
            LabelPlacement = LabelPlacement.Outside,
            LabelFormatString = "{0}",
            FontSize = 12
        };
        serieInact.Items.Add(new ColumnItem(cantInactivos));

        modeloInact.Axes.Add(ejeXI);
        modeloInact.Axes.Add(ejeYI);
        modeloInact.Series.Add(serieInact);
        plotInactivos.Model = modeloInact;

        // ── Top 5 socios — barras horizontales ────────────────────────
        var modeloTop = new PlotModel
        {
            Title = "Los 5 socios que más asistieron",
            TitleFontSize = 11,
            Background = OxyColors.Transparent,
            TextColor = OxyColor.FromRgb(60, 60, 60)
        };

        var ejeXTop = new LinearAxis
        {
            Position = AxisPosition.Bottom, Title = "Asistencias",
            Minimum = 0,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(220, 220, 220),
            TextColor = OxyColor.FromRgb(100, 100, 100), FontSize = 9
        };

        var ejeYTop = new CategoryAxis
        {
            Position = AxisPosition.Left, Title = "Socios",
            TextColor = OxyColor.FromRgb(60, 60, 60), FontSize = 9
        };

        var serieTop = new BarSeries
        {
            FillColor       = OxyColor.FromRgb(211, 47, 47),  // rojo
            StrokeThickness = 0,
            LabelPlacement  = LabelPlacement.Outside,
            LabelFormatString = "{0}",
            FontSize        = 10
        };

        // Top 5 en orden ascendente (OxyPlot barras horizontales van de abajo a arriba)
        var top5Reversed = new List<SocioTop>(top5);
        top5Reversed.Reverse();
        foreach (var s in top5Reversed)
        {
            ejeYTop.Labels.Add(s.NombreCompleto.ToUpper());
            serieTop.Items.Add(new BarItem((double)s.TotalAsistencias));
        }

        modeloTop.Axes.Add(ejeXTop);
        modeloTop.Axes.Add(ejeYTop);
        modeloTop.Series.Add(serieTop);
        plotTop5.Model = modeloTop;
    }
    catch (Exception ex)
    {
        MessageBox.Show("Error al cargar socios:\n" + ex.Message);
    }
}

private int ObtenerDiasInactivoSeleccionados()
{
    // Leer del ComboBox o TextBox que tiene el valor de días
    // Ajustar el nombre del control según el XAML existente
    if (cmbDiasInactivo?.SelectedItem is ComboBoxItem item &&
        int.TryParse(item.Content?.ToString()?.Replace(" días","").Trim(),
                     out int dias))
        return dias;
    return 20; // default
}
```

---

### PASO 9 — `MainWindow.xaml.cs` — agregar Estadísticas al menú admin

En el método `ConstruirMenu()` donde está la lista de items, agregar después de Auditoría:

```csharp
// Agregar a la lista de items del menú (solo admin, igual que Auditoría):
new MenuItem
{
    Icono      = "📊",
    Texto      = "Estadísticas",
    TipoPagina = typeof(SistemaGimnacionOptimusCAI.Paginas.EstadisticasPage),
    SoloAdmin  = true
}
```

---

### PASO 10 — Desinstalar LiveCharts

Solo si ningún otro módulo lo usa. Verificar con `Ctrl+Shift+F` buscando `lvc:` en todos los archivos XAML. Si no hay ningún resultado:

```powershell
# En Package Manager Console:
Uninstall-Package LiveCharts.Wpf
Uninstall-Package LiveCharts
```

---

## 5. CHECKLIST DE VERIFICACIÓN

### ✅ Fix gastos
```
□ Tab Caja → seleccionar "Este mes" → click Mostrar
□ El campo "Gastos" muestra valor > $0 si hay egresos en caja_movimientos
□ El gráfico de Gastos muestra línea roja con puntos por día
□ Ejecutar en SQL: EXEC sp_EstadisticasCajaResumen
    @FechaDesde='2026-05-01', @FechaHasta='2026-05-31'
  → Resultset 3 debe tener filas si hay egresos registrados
```

### ✅ KPIs nuevos
```
□ Al abrir Estadísticas aparece la fila de 4 KPIs arriba del TabControl
□ "SOCIOS ACTIVOS" muestra número correcto
□ "VENCEN ESTA SEMANA" muestra cantidad de membresías próximas
□ "PRODUCTO + VENDIDO" muestra nombre del producto con cantidad
□ "INGRESOS DEL MES" muestra el total del mes actual
```

### ✅ Migración OxyPlot
```
□ Ningún error de compilación relacionado con LiveCharts
□ Los gráficos se renderizan correctamente
□ Gráfico ingresos: línea verde con puntos y etiquetas de valor
□ Gráfico gastos: línea roja (antes estaba en $0)
□ Tab Asistencias: barras teal por hora, pie chart con colores
□ Tab Socios: barras teal (nuevos), barra gris (inactivos), barras rojas horizontales (top 5)
```

### ✅ Integración en menú
```
□ Al loguear como admin → "📊 Estadísticas" aparece en el sidebar
□ Click en Estadísticas → carga EstadisticasPage en el Frame del MainWindow
□ Al loguear como empleado → "📊 Estadísticas" NO aparece en el sidebar
```

---

## 6. ERRORES COMUNES Y SOLUCIONES

| Error | Causa | Solución |
|-------|-------|----------|
| `PieSeries no encontrado` | Falta import de OxyPlot.Series | Agregar `using OxyPlot.Series;` |
| `BarSeries vs BarSeries` | OxyPlot tiene dos: `BarSeries` y `RowSeries` | Usar `BarSeries` con `CategoryAxis` en eje Y |
| Gráfico vacío sin error | El SP devuelve 0 filas | Verificar con query directo en SQL Explorer |
| `EstadisticasPage` no navega | Nombre de clase incorrecto en el menú | Verificar que el `typeof()` usa el namespace correcto |
| Gastos siguen en $0 tras el fix | El campo `tipo` tiene valor distinto a `'egreso'` | Ejecutar `SELECT DISTINCT tipo FROM caja_movimientos` y ver el valor real |

---

*SDD Mejoras Estadísticas Dashboard — OptimusCAI Gym v1.0 — Mayo 2026*  
*Migración LiveCharts→OxyPlot + Fix Gastos + KPIs Nuevos + Integración Menú*
