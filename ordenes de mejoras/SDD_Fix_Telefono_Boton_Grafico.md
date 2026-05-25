# SDD — Fix Validación Teléfono + Botón Reportes + Gráfico Ingresos
> Spec-Driven Development — Mejoras UI/UX detectadas en revisión  
> Versión 1.0 — Mayo 2026  
> **Leer COMPLETO antes de tocar cualquier archivo**

---

## 0. CONTEXTO — Qué hay que corregir

| Problema | Síntoma | Solución |
|----------|---------|----------|
| Validación teléfono | Se aceptan letras y cualquier cantidad de dígitos | Solo 10 dígitos numéricos exactos, sin el 0 inicial |
| Botón "CONSULTAR" / "ACTUALIZAR" en reportes | No se ve bien, poco contraste o tamaño incorrecto | Rediseñar con estilo consistente del sistema |
| Gráfico de ingresos | Se ve una sola línea recta plana | Implementar barras por mes + línea de tendencia superpuesta con OxyPlot |

---

## 1. CONDICIONES — Reglas no negociables

```
- C# 7.3 estricto. Sin switch expressions ni features de C# 8+
- Sin SQL inline en DAOs
- Sin LetterSpacing ni DropShadowEffect en Triggers XAML
- El validador de teléfono va en Validador.cs (Controllers/) — reutilizable desde todos los módulos
- OxyPlot.Wpf para el gráfico (ya instalado). Si da conflicto de versión usar Canvas WPF nativo
- Moneda en FormatoARS (ya implementado en SDD anterior)
- La validación bloquea caracteres en tiempo real (PreviewTextInput) Y valida al perder foco (LostFocus)
```

---

## 2. CLASIFICACIÓN — Archivos a modificar

| Archivo | Cambio |
|---------|--------|
| `Controllers/Validador.cs` | Agregar método `ValidarTelefono()` |
| `SociosPage.xaml` | Agregar `PreviewTextInput` y `LostFocus` al campo teléfono |
| `SociosPage.xaml.cs` | Conectar validación en tiempo real |
| `UsuariosPage.xaml` | Mismo fix que Socios |
| `UsuariosPage.xaml.cs` | Mismo fix que Socios |
| Cualquier otro `.xaml` con campo teléfono | Mismo fix |
| `ReportesPage.xaml` | Rediseñar botón CONSULTAR/ACTUALIZAR |
| `ReportesPage.xaml.cs` | Implementar gráfico OxyPlot con barras + línea tendencia |

---

## 3. PLAN DE EJECUCIÓN

```
PASO 1 → Agregar ValidarTelefono() en Validador.cs
PASO 2 → Aplicar validación en SociosPage (XAML + code-behind)
PASO 3 → Aplicar validación en UsuariosPage (XAML + code-behind)
PASO 4 → Buscar otros formularios con teléfono y aplicar lo mismo
PASO 5 → Rediseñar botón CONSULTAR en ReportesPage.xaml
PASO 6 → Implementar gráfico con OxyPlot en ReportesPage.xaml + .cs
PASO 7 → Compilar y verificar con checklist
```

---

## 4. CÓDIGO — Implementar exactamente esto

---

### PASO 1 — `Controllers/Validador.cs` — agregar `ValidarTelefono()`

Abrir el archivo `Validador.cs` existente y **agregar** este método estático a la clase:

```csharp
/// <summary>
/// Valida teléfono celular argentino.
/// Reglas:
///   - Exactamente 10 dígitos numéricos
///   - No empieza con 0 (sin prefijo 0)
///   - No empieza con 15 (el 15 ya no se usa con código de área)
///   - Solo dígitos, sin letras, espacios ni guiones
/// Ejemplos válidos:   3884123456 / 2994567890
/// Ejemplos inválidos: 03884123456 (tiene 0) / 1512345678 (empieza con 15)
///                     388412345 (9 dígitos) / 38841234567 (11 dígitos)
/// </summary>
public static string ValidarTelefono(string telefono)
{
    if (string.IsNullOrWhiteSpace(telefono))
        return "El número de celular es obligatorio.";

    // Solo dígitos — rechazar cualquier letra, espacio o símbolo
    foreach (char c in telefono)
    {
        if (!char.IsDigit(c))
            return "El celular solo puede contener números, sin letras ni símbolos.";
    }

    if (telefono.Length != 10)
        return "El celular debe tener exactamente 10 dígitos (sin el 0 inicial). Ejemplo: 3884123456";

    if (telefono[0] == '0')
        return "No ingreses el 0 inicial. Ejemplo: 3884123456 (no 03884123456)";

    if (telefono.StartsWith("15"))
        return "No ingreses el 15. Ejemplo: 3884123456 (no 1512345678)";

    return null; // null = válido
}

/// <summary>
/// Devuelve true si el caracter ingresado es válido para un campo teléfono.
/// Usar en PreviewTextInput para bloquear en tiempo real.
/// </summary>
public static bool EsCaracterTelefonoValido(string texto)
{
    if (string.IsNullOrEmpty(texto)) return false;
    // Solo permite dígitos — bloquea letras, espacios, guiones, puntos, etc.
    foreach (char c in texto)
        if (!char.IsDigit(c)) return false;
    return true;
}
```

---

### PASO 2 — `SociosPage.xaml` — campo teléfono con validación

**Buscar** el campo de teléfono en el formulario lateral y **reemplazarlo** con:

```xml
<!-- Etiqueta del campo -->
<TextBlock Text="CELULAR * (10 dígitos, sin 0 inicial)"
           Foreground="#4A4A7A" FontSize="10" FontWeight="Bold"
           Margin="2,0,0,6"/>

<!-- TextBox con validación en tiempo real -->
<TextBox x:Name="txtTelefono"
         Style="{StaticResource InputEstilo}"
         MaxLength="10"
         PreviewTextInput="txtTelefono_PreviewTextInput"
         DataObject.Pasting="txtTelefono_Pasting"
         LostFocus="txtTelefono_LostFocus"
         ToolTip="Ingresá 10 dígitos sin el 0 inicial. Ejemplo: 3884123456"/>

<!-- Label de error — se muestra/oculta dinámicamente -->
<TextBlock x:Name="errTelefono"
           FontSize="11" Foreground="#FF5555" FontStyle="Italic"
           Margin="4,-8,0,10" TextWrapping="Wrap"
           Visibility="Collapsed"/>

<!-- Hint debajo del campo (siempre visible) -->
<TextBlock Text="Ejemplo: 3884123456"
           FontSize="10" Foreground="#3A3A5C" FontStyle="Italic"
           Margin="4,-6,0,14"/>
```

---

### PASO 3 — `SociosPage.xaml.cs` — eventos de validación

**Agregar o reemplazar** estos métodos en el code-behind:

```csharp
// ── VALIDACIÓN TELÉFONO EN TIEMPO REAL ───────────────────────────────

/// <summary>
/// Bloquea en tiempo real cualquier carácter que no sea dígito.
/// </summary>
private void txtTelefono_PreviewTextInput(object sender, TextCompositionEventArgs e)
{
    // Si el caracter no es válido para teléfono → bloquearlo
    e.Handled = !Validador.EsCaracterTelefonoValido(e.Text);
}

/// <summary>
/// Bloquea el pegado de texto con caracteres inválidos (letras, espacios, etc.)
/// </summary>
private void txtTelefono_Pasting(object sender, DataObjectPastingEventArgs e)
{
    if (e.DataObject.GetDataPresent(typeof(string)))
    {
        string texto = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
        // Filtrar solo dígitos del texto pegado
        var soloDigitos = new System.Text.StringBuilder();
        foreach (char c in texto)
            if (char.IsDigit(c)) soloDigitos.Append(c);

        string resultado = soloDigitos.ToString();
        if (resultado.Length > 10)
            resultado = resultado.Substring(0, 10);

        // Si quedó algo válido, pegarlo limpio
        if (resultado.Length > 0)
        {
            var tb = sender as TextBox;
            if (tb != null)
            {
                tb.Text = resultado;
                tb.CaretIndex = tb.Text.Length;
            }
        }
        // Cancelar el pegado original en todos los casos (usamos el limpio)
        e.CancelCommand();
    }
    else
    {
        e.CancelCommand();
    }
}

/// <summary>
/// Valida al perder el foco: muestra error si el formato es incorrecto.
/// </summary>
private void txtTelefono_LostFocus(object sender, RoutedEventArgs e)
{
    string error = Validador.ValidarTelefono(txtTelefono.Text);
    if (error != null)
    {
        errTelefono.Text       = error;
        errTelefono.Visibility = Visibility.Visible;
        // Aplicar estilo de error al TextBox
        txtTelefono.BorderBrush     = new SolidColorBrush(Color.FromRgb(255, 68, 68));
        txtTelefono.BorderThickness = new Thickness(1.5);
        txtTelefono.Background      = new SolidColorBrush(Color.FromRgb(30, 10, 10));
    }
    else
    {
        errTelefono.Visibility = Visibility.Collapsed;
        // Restaurar estilo normal
        txtTelefono.BorderBrush     = new SolidColorBrush(Color.FromRgb(37, 37, 64));
        txtTelefono.BorderThickness = new Thickness(1.5);
        txtTelefono.Background      = new SolidColorBrush(Color.FromRgb(22, 22, 42));
    }
}
```

**Agregar al inicio del método `btnGuardar_Click`** — antes de llamar al controller:

```csharp
// Validar teléfono antes de guardar
string errTel = Validador.ValidarTelefono(txtTelefono.Text);
if (errTel != null)
{
    errTelefono.Text       = errTel;
    errTelefono.Visibility = Visibility.Visible;
    txtTelefono.Focus();
    return;
}
```

---

### PASO 4 — `UsuariosPage.xaml` y `UsuariosPage.xaml.cs`

Aplicar **exactamente el mismo patrón** que Socios:

En `UsuariosPage.xaml` — buscar el campo `txtTelefono` y agregar:
```xml
MaxLength="10"
PreviewTextInput="txtTelefono_PreviewTextInput"
DataObject.Pasting="txtTelefono_Pasting"
LostFocus="txtTelefono_LostFocus"
```

En `UsuariosPage.xaml.cs` — copiar los 3 métodos del PASO 3 exactamente igual.

**Búsqueda global:** Hacer `Ctrl+Shift+F` en Visual Studio buscando `txtTelefono` en todos los archivos `.xaml`. Cada uno que aparezca debe tener los 3 atributos: `MaxLength="10"`, `PreviewTextInput`, `DataObject.Pasting`, `LostFocus`.

---

### PASO 5 — `ReportesPage.xaml` — Rediseño del botón CONSULTAR

**Buscar** el botón "CONSULTAR" / "APLICAR FILTROS" en cada tab y **reemplazar** con este diseño consistente:

```xml
<!-- ═══ BOTÓN CONSULTAR — estilo unificado para todos los tabs ═══ -->
<Button x:Name="btnConsultar"
        Click="btnConsultar_Click"
        Width="160" Height="44"
        Cursor="Hand">
    <Button.Template>
        <ControlTemplate TargetType="Button">
            <Border x:Name="borderBtn" CornerRadius="10"
                    BorderThickness="0">
                <Border.Background>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                        <GradientStop Color="#00CFFF" Offset="0"/>
                        <GradientStop Color="#A78BFA" Offset="1"/>
                    </LinearGradientBrush>
                </Border.Background>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="🔍"
                               FontSize="14"
                               VerticalAlignment="Center"
                               Margin="14,0,8,0"/>
                    <TextBlock Grid.Column="1"
                               Text="CONSULTAR"
                               FontFamily="Bahnschrift SemiBold, Segoe UI"
                               FontSize="12" FontWeight="Bold"
                               Foreground="#0A0A14"
                               VerticalAlignment="Center"
                               Margin="0,0,14,0"/>
                </Grid>
                <Border.Style>
                    <Style TargetType="Border">
                        <Style.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Opacity" Value="0.85"/>
                            </Trigger>
                        </Style.Triggers>
                    </Style>
                </Border.Style>
            </Border>
        </ControlTemplate>
    </Button.Template>
</Button>

<!-- Botón EXPORTAR PDF — mismo ancho, estilo secundario -->
<Button x:Name="btnExportarPDF"
        Click="btnExportarPDF_Click"
        Width="160" Height="44"
        Cursor="Hand" Margin="10,0,0,0">
    <Button.Template>
        <ControlTemplate TargetType="Button">
            <Border CornerRadius="10"
                    Background="Transparent"
                    BorderBrush="#FF6B35"
                    BorderThickness="1.5">
                <StackPanel Orientation="Horizontal"
                            HorizontalAlignment="Center"
                            VerticalAlignment="Center">
                    <TextBlock Text="📄" FontSize="14" Margin="0,0,8,0"/>
                    <TextBlock Text="EXPORTAR PDF"
                               FontFamily="Bahnschrift SemiBold"
                               FontSize="11" FontWeight="Bold"
                               Foreground="#FF6B35"/>
                </StackPanel>
            </Border>
        </ControlTemplate>
    </Button.Template>
</Button>

<!-- Botón EXPORTAR EXCEL — estilo verde -->
<Button x:Name="btnExportarExcel"
        Click="btnExportarExcel_Click"
        Width="170" Height="44"
        Cursor="Hand" Margin="10,0,0,0">
    <Button.Template>
        <ControlTemplate TargetType="Button">
            <Border CornerRadius="10"
                    Background="Transparent"
                    BorderBrush="#00E676"
                    BorderThickness="1.5">
                <StackPanel Orientation="Horizontal"
                            HorizontalAlignment="Center"
                            VerticalAlignment="Center">
                    <TextBlock Text="📊" FontSize="14" Margin="0,0,8,0"/>
                    <TextBlock Text="EXPORTAR EXCEL"
                               FontFamily="Bahnschrift SemiBold"
                               FontSize="11" FontWeight="Bold"
                               Foreground="#00E676"/>
                </StackPanel>
            </Border>
        </ControlTemplate>
    </Button.Template>
</Button>
```

---

### PASO 6 — Gráfico con OxyPlot — XAML + code-behind

#### 6A — Agregar NuGet si no está instalado

```
Tools → NuGet Package Manager → Package Manager Console:
Install-Package OxyPlot.Wpf -Version 2.1.0
```

#### 6B — Namespace en `ReportesPage.xaml`

Agregar en la apertura del `<Page>`:

```xml
xmlns:oxy="http://oxyplot.org/wpf"
```

#### 6C — Sección del gráfico en `ReportesPage.xaml`

**Reemplazar** la sección del gráfico actual con:

```xml
<!-- ══ SECCIÓN GRÁFICO DE INGRESOS POR MES ══ -->
<Border Background="#12121E" CornerRadius="12"
        BorderBrush="#252540" BorderThickness="1"
        Padding="20,16" Margin="0,16,0,0">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="320"/>
        </Grid.RowDefinitions>

        <!-- Header del gráfico -->
        <Grid Grid.Row="0" Margin="0,0,0,16">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column="0">
                <TextBlock Text="INGRESOS POR MES"
                           FontFamily="Bahnschrift SemiBold"
                           FontSize="13" FontWeight="Bold"
                           Foreground="#E8E8FF"/>
                <TextBlock x:Name="lblAnioGrafico"
                           Text="Año 2026"
                           FontSize="11" Foreground="#6A6A9A"
                           Margin="0,2,0,0"/>
            </StackPanel>

            <!-- Selector de año -->
            <StackPanel Grid.Column="1" Orientation="Horizontal"
                        VerticalAlignment="Center">
                <Button x:Name="btnAnioAnterior"
                        Content="◀" Click="btnAnioAnterior_Click"
                        Background="Transparent" Foreground="#6A6A9A"
                        BorderThickness="0" FontSize="16"
                        Cursor="Hand" Width="32" Height="32"/>
                <TextBlock x:Name="lblAnioSelector"
                           Text="2026"
                           FontFamily="Consolas" FontSize="14"
                           FontWeight="Bold" Foreground="#E8E8FF"
                           VerticalAlignment="Center" Margin="8,0"/>
                <Button x:Name="btnAnioSiguiente"
                        Content="▶" Click="btnAnioSiguiente_Click"
                        Background="Transparent" Foreground="#6A6A9A"
                        BorderThickness="0" FontSize="16"
                        Cursor="Hand" Width="32" Height="32"/>
            </StackPanel>
        </Grid>

        <!-- Leyenda manual -->
        <StackPanel Grid.Row="0" Orientation="Horizontal"
                    HorizontalAlignment="Right" Margin="0,0,120,0"
                    VerticalAlignment="Center">
            <Border Width="14" Height="14" CornerRadius="3"
                    Margin="0,0,6,0">
                <Border.Background>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                        <GradientStop Color="#00CFFF" Offset="0"/>
                        <GradientStop Color="#A78BFA" Offset="1"/>
                    </LinearGradientBrush>
                </Border.Background>
            </Border>
            <TextBlock Text="Ingresos" FontSize="11" Foreground="#A0A0C0"
                       VerticalAlignment="Center" Margin="0,0,16,0"/>
            <Ellipse Width="10" Height="10" Fill="#FF6B35" Margin="0,0,6,0"/>
            <TextBlock Text="Tendencia" FontSize="11" Foreground="#A0A0C0"
                       VerticalAlignment="Center"/>
        </StackPanel>

        <!-- Plot OxyPlot -->
        <oxy:PlotView Grid.Row="1" x:Name="plotIngresos"
                      Background="Transparent"
                      Model="{Binding GraficoModel}"/>
    </Grid>
</Border>
```

#### 6D — `ReportesPage.xaml.cs` — lógica completa del gráfico

**Agregar** estos campos y métodos al code-behind:

```csharp
// ── CAMPOS PRIVADOS ────────────────────────────────────────────────────
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;
using System.Globalization;

// Dentro de la clase ReportesPage:
private int _anioGrafico = DateTime.Today.Year;
private PlotModel _graficoModel;

// Propiedad para el binding del PlotView
public PlotModel GraficoModel
{
    get { return _graficoModel; }
    private set
    {
        _graficoModel = value;
        // Notificar al PlotView que cambiaron los datos
        if (plotIngresos != null)
        {
            plotIngresos.Model = _graficoModel;
            _graficoModel.InvalidatePlot(true);
        }
    }
}

// ── INICIALIZACIÓN ────────────────────────────────────────────────────

// Llamar desde el constructor o desde el evento Loaded:
private void InicializarGrafico()
{
    _anioGrafico = DateTime.Today.Year;
    lblAnioSelector.Text = _anioGrafico.ToString();
    lblAnioGrafico.Text  = "Año " + _anioGrafico;
    CargarGrafico();
}

// ── CARGA DEL GRÁFICO ─────────────────────────────────────────────────

private void CargarGrafico()
{
    try
    {
        // Obtener datos del SP
        var datos = _controller.ObtenerGraficoPorMes(_anioGrafico);

        // Asegurarse de tener los 12 meses (rellenar con 0 si faltan)
        var datosPorMes = new decimal[12];
        foreach (var d in datos)
        {
            int idx = d.Mes - 1; // mes 1-12 → índice 0-11
            if (idx >= 0 && idx < 12)
                datosPorMes[idx] = d.Ingresos;
        }

        GraficoModel = ConstruirModelo(datosPorMes);

        // Actualizar selector de año
        lblAnioSelector.Text = _anioGrafico.ToString();
        lblAnioGrafico.Text  = "Año " + _anioGrafico;
    }
    catch (Exception ex)
    {
        // Si falla el gráfico, mostrar advertencia pero no crashear
        NotificacionWindow.MostrarAdvertencia(
            "No se pudo cargar el gráfico.\n" + ex.Message);
    }
}

private PlotModel ConstruirModelo(decimal[] datosPorMes)
{
    // Nombres de meses en español
    string[] meses = { "Ene","Feb","Mar","Abr","May","Jun",
                        "Jul","Ago","Sep","Oct","Nov","Dic" };

    // ── Configuración del modelo ─────────────────────────────────────
    var modelo = new PlotModel
    {
        Background        = OxyColor.FromArgb(0, 0, 0, 0),    // transparente
        PlotAreaBackground= OxyColor.FromArgb(0, 0, 0, 0),
        TextColor         = OxyColor.FromRgb(160, 160, 192),
        PlotAreaBorderColor = OxyColor.FromRgb(37, 37, 64),
        PlotAreaBorderThickness = new OxyThickness(0, 0, 0, 1),
        Padding           = new OxyThickness(10, 10, 20, 10)
    };

    // ── Eje X — meses ────────────────────────────────────────────────
    var ejeX = new CategoryAxis
    {
        Position          = AxisPosition.Bottom,
        ItemsSource       = meses,
        TextColor         = OxyColor.FromRgb(106, 106, 154),
        TicklineColor     = OxyColor.FromRgb(37, 37, 64),
        MajorGridlineStyle= LineStyle.None,
        MinorGridlineStyle= LineStyle.None,
        FontSize          = 11,
        Angle             = 0
    };
    modelo.Axes.Add(ejeX);

    // ── Eje Y — montos en pesos ───────────────────────────────────────
    decimal maxValor = 0;
    foreach (var v in datosPorMes)
        if (v > maxValor) maxValor = v;

    // Si no hay datos, usar un máximo de ejemplo para que se vea el eje
    if (maxValor == 0) maxValor = 10000;

    var ejeY = new LinearAxis
    {
        Position              = AxisPosition.Left,
        Minimum               = 0,
        Maximum               = (double)(maxValor * 1.2m), // 20% de margen arriba
        MajorGridlineStyle    = LineStyle.Dot,
        MajorGridlineColor    = OxyColor.FromRgb(37, 37, 64),
        MinorGridlineStyle    = LineStyle.None,
        TicklineColor         = OxyColor.FromArgb(0, 0, 0, 0),
        TextColor             = OxyColor.FromRgb(106, 106, 154),
        FontSize              = 10,
        // Formato ARS: $ 10.000
        LabelFormatter        = v =>
        {
            if (v >= 1_000_000)
                return "$ " + (v / 1_000_000).ToString("N1",
                    new CultureInfo("es-AR")) + "M";
            if (v >= 1000)
                return "$ " + (v / 1000).ToString("N0",
                    new CultureInfo("es-AR")) + "K";
            return "$ " + v.ToString("N0", new CultureInfo("es-AR"));
        }
    };
    modelo.Axes.Add(ejeY);

    // ── Serie de barras (ingresos) ────────────────────────────────────
    var serieBarras = new BarSeries
    {
        Title              = "Ingresos",
        StrokeThickness    = 0,
        BarWidth           = 0.6,
        FillColor          = OxyColor.FromRgb(0, 207, 255),   // cyan base
        // Tooltip al pasar el mouse
        TrackerFormatString = "{0}\n{1}: {2:$ #,##0.00}"
    };

    // Colores degradados por mes (cyan → violeta)
    OxyColor[] coloresBarra =
    {
        OxyColor.FromRgb(0, 207, 255),    // cyan
        OxyColor.FromRgb(32, 190, 255),
        OxyColor.FromRgb(64, 173, 255),
        OxyColor.FromRgb(96, 156, 255),
        OxyColor.FromRgb(118, 148, 255),
        OxyColor.FromRgb(131, 145, 253),
        OxyColor.FromRgb(144, 142, 252),
        OxyColor.FromRgb(152, 140, 251),
        OxyColor.FromRgb(160, 139, 251),
        OxyColor.FromRgb(163, 139, 250),  // violeta
        OxyColor.FromRgb(167, 139, 250),
        OxyColor.FromRgb(170, 139, 250)
    };

    for (int i = 0; i < 12; i++)
    {
        serieBarras.Items.Add(new BarItem
        {
            Value = (double)datosPorMes[i],
            Color = coloresBarra[i]
        });
    }
    modelo.Series.Add(serieBarras);

    // ── Serie línea de tendencia ──────────────────────────────────────
    // Solo mostrar hasta el mes actual si es el año en curso
    int mesLimite = _anioGrafico == DateTime.Today.Year
                        ? DateTime.Today.Month
                        : 12;

    var serieLinea = new LineSeries
    {
        Title           = "Tendencia",
        Color           = OxyColor.FromRgb(255, 107, 53),   // naranja
        StrokeThickness = 2.5,
        MarkerType      = MarkerType.Circle,
        MarkerSize      = 5,
        MarkerFill      = OxyColor.FromRgb(255, 107, 53),
        MarkerStroke    = OxyColor.FromRgb(18, 18, 30),
        MarkerStrokeThickness = 1.5,
        LineStyle       = LineStyle.Solid,
        TrackerFormatString = "Tendencia\nMes: {2:0}\nTotal: {4:$ #,##0.00}"
    };

    for (int i = 0; i < mesLimite; i++)
    {
        // Solo agregar punto si hay datos ese mes
        if (datosPorMes[i] > 0 || i == 0)
        {
            serieLinea.Points.Add(
                new DataPoint(i - 0.5 + 0.3, (double)datosPorMes[i]));
        }
    }

    // Si todos son 0, agregar dos puntos en 0 para que la línea se vea
    if (serieLinea.Points.Count == 0)
    {
        serieLinea.Points.Add(new DataPoint(-0.2, 0));
        serieLinea.Points.Add(new DataPoint(11.3, 0));
    }

    modelo.Series.Add(serieLinea);

    // ── Anotación: mes actual ─────────────────────────────────────────
    if (_anioGrafico == DateTime.Today.Year)
    {
        int mesActual = DateTime.Today.Month - 1; // 0-indexed
        modelo.Annotations.Add(new RectangleAnnotation
        {
            MinimumX       = mesActual - 0.5,
            MaximumX       = mesActual + 0.5,
            MinimumY       = 0,
            MaximumY       = (double)(maxValor * 1.2m),
            Fill           = OxyColor.FromArgb(20, 167, 139, 250),
            Stroke         = OxyColor.FromArgb(60, 167, 139, 250),
            StrokeThickness= 1,
            Layer          = AnnotationLayer.BelowSeries
        });
    }

    return modelo;
}

// ── NAVEGACIÓN DE AÑO ─────────────────────────────────────────────────

private void btnAnioAnterior_Click(object sender, RoutedEventArgs e)
{
    _anioGrafico--;
    CargarGrafico();
}

private void btnAnioSiguiente_Click(object sender, RoutedEventArgs e)
{
    // No permitir ir al futuro
    if (_anioGrafico >= DateTime.Today.Year) return;
    _anioGrafico++;
    CargarGrafico();
}
```

**En el constructor o en el evento `Page_Loaded`**, agregar la llamada:

```csharp
// Después de inicializar los demás componentes:
InicializarGrafico();
```

---

## 5. CASOS BORDE DEL GRÁFICO

### Si OxyPlot da conflicto de versión con .NET Framework

Usar Canvas WPF nativo como fallback. Reemplazar el `<oxy:PlotView>` con:

```xml
<Canvas x:Name="canvasGrafico" Background="Transparent"/>
```

Y en el code-behind, dibujar con rectángulos y líneas WPF puras:

```csharp
private void DibujarGraficoCanvas(decimal[] datosPorMes)
{
    canvasGrafico.Children.Clear();

    double ancho  = canvasGrafico.ActualWidth;
    double alto   = canvasGrafico.ActualHeight;
    double margenIzq = 60, margenDer = 20, margenSup = 20, margenInf = 40;

    double areaAncho = ancho - margenIzq - margenDer;
    double areaAlto  = alto  - margenSup - margenInf;

    decimal maxVal = 10000;
    foreach (var v in datosPorMes)
        if (v > maxVal) maxVal = v;

    double anchoBarra = (areaAncho / 12) * 0.6;
    double espacio    = areaAncho / 12;

    // Línea base Y
    var lineaBase = new System.Windows.Shapes.Line
    {
        X1 = margenIzq, Y1 = margenSup + areaAlto,
        X2 = margenIzq + areaAncho, Y2 = margenSup + areaAlto,
        Stroke = new SolidColorBrush(Color.FromRgb(37, 37, 64)),
        StrokeThickness = 1
    };
    canvasGrafico.Children.Add(lineaBase);

    // Gradiente de colores
    Color[] colores =
    {
        Color.FromRgb(0, 207, 255), Color.FromRgb(32, 190, 255),
        Color.FromRgb(64, 173, 255), Color.FromRgb(96, 156, 255),
        Color.FromRgb(118, 148, 255), Color.FromRgb(131, 145, 253),
        Color.FromRgb(144, 142, 252), Color.FromRgb(152, 140, 251),
        Color.FromRgb(160, 139, 251), Color.FromRgb(163, 139, 250),
        Color.FromRgb(167, 139, 250), Color.FromRgb(170, 139, 250)
    };

    string[] nombresMes = {"Ene","Feb","Mar","Abr","May","Jun",
                            "Jul","Ago","Sep","Oct","Nov","Dic"};

    var puntosLinea = new System.Windows.Media.PointCollection();

    for (int i = 0; i < 12; i++)
    {
        double x     = margenIzq + i * espacio + (espacio - anchoBarra) / 2;
        double pct   = maxVal > 0 ? (double)datosPorMes[i] / (double)maxVal : 0;
        double hBarra= pct * areaAlto;
        double y     = margenSup + areaAlto - hBarra;

        // Barra
        var rect = new System.Windows.Shapes.Rectangle
        {
            Width  = anchoBarra,
            Height = Math.Max(hBarra, 2), // mínimo 2px para que se vea
            Fill   = new SolidColorBrush(colores[i]),
            RadiusX = 4, RadiusY = 4
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        canvasGrafico.Children.Add(rect);

        // Etiqueta mes
        var lblMes = new TextBlock
        {
            Text       = nombresMes[i],
            FontSize   = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154))
        };
        Canvas.SetLeft(lblMes, x + anchoBarra / 2 - 10);
        Canvas.SetTop(lblMes, margenSup + areaAlto + 6);
        canvasGrafico.Children.Add(lblMes);

        // Punto para la línea de tendencia
        double xCentro = x + anchoBarra / 2;
        double yCentro = margenSup + areaAlto - hBarra;
        puntosLinea.Add(new System.Windows.Point(xCentro, yCentro));
    }

    // Línea de tendencia
    if (puntosLinea.Count >= 2)
    {
        var polilinea = new System.Windows.Shapes.Polyline
        {
            Points          = puntosLinea,
            Stroke          = new SolidColorBrush(Color.FromRgb(255, 107, 53)),
            StrokeThickness = 2.5,
            StrokeLineJoin  = System.Windows.Media.PenLineJoin.Round
        };
        canvasGrafico.Children.Add(polilinea);

        // Puntos circulares en la línea
        foreach (var pt in puntosLinea)
        {
            var circulo = new System.Windows.Shapes.Ellipse
            {
                Width  = 8, Height = 8,
                Fill   = new SolidColorBrush(Color.FromRgb(255, 107, 53)),
                Stroke = new SolidColorBrush(Color.FromRgb(18, 18, 30)),
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(circulo, pt.X - 4);
            Canvas.SetTop(circulo, pt.Y - 4);
            canvasGrafico.Children.Add(circulo);
        }
    }
}
```

Llamar a `DibujarGraficoCanvas(datosPorMes)` después de que el Canvas tenga tamaño:

```csharp
// En el evento SizeChanged del Canvas:
canvasGrafico.SizeChanged += (s, e) => CargarGrafico();
```

---

## 6. CHECKLIST DE VERIFICACIÓN

### ✅ Validación teléfono
```
□ Ingresar una letra en el campo teléfono → no aparece en el TextBox
□ Pegar "abc123def456" → solo queda "123456"
□ Ingresar 9 dígitos y hacer click afuera → error "debe tener exactamente 10 dígitos"
□ Ingresar 11 dígitos → el MaxLength="10" lo bloquea antes de que llegue al LostFocus
□ Ingresar "0388412345" → error "No ingreses el 0 inicial"
□ Ingresar "1512345678" → error "No ingreses el 15"
□ Ingresar "3884123456" → sin error, campo en verde/normal
□ Clickear GUARDAR con teléfono inválido → no guarda, muestra error
□ Verificar en Usuarios que aplica la misma validación
```

### ✅ Botón CONSULTAR
```
□ El botón tiene fondo degradado cyan→violeta
□ Tiene ícono 🔍 a la izquierda del texto
□ Al hover se oscurece levemente (opacity 0.85)
□ Los botones EXPORTAR PDF y EXPORTAR EXCEL tienen borde de color (naranja/verde)
□ Todos los tabs tienen el mismo diseño de botones
```

### ✅ Gráfico de ingresos
```
□ Se ven 12 barras (una por mes de enero a diciembre)
□ Las barras van de cyan a violeta (degradado izquierda a derecha)
□ Se ve una línea naranja de tendencia con puntos circulares
□ El eje Y muestra montos en pesos: "$ 10K", "$ 50K", etc.
□ El eje X muestra nombres de meses: "Ene", "Feb", ..., "Dic"
□ Los meses sin datos muestran barra en 0 (mínimo 2px de altura)
□ El mes actual tiene un fondo violeta sutil detrás de la barra
□ Los botones ◀ y ▶ cambian el año y recargan el gráfico
□ El botón ▶ está deshabilitado si ya estás en el año actual
□ Al pasar el mouse sobre una barra aparece tooltip con el monto
```

---

## 7. ERRORES COMUNES Y SOLUCIONES

| Error | Causa | Solución |
|-------|-------|----------|
| `OxyPlot no encontrado` | NuGet no instalado | `Install-Package OxyPlot.Wpf -Version 2.1.0` |
| `xmlns:oxy no resuelve` | Namespace incorrecto | Verificar que sea `http://oxyplot.org/wpf` exacto |
| `GraficoModel null` | El binding falla | Setear `plotIngresos.Model` directamente en code-behind en vez de usar binding |
| `El gráfico sigue plano` | Datos del SP todos en 0 | Ejecutar `EXEC sp_GraficoIngresosPorMes @Anio = 2026` en SQL Explorer y ver si retorna filas |
| `CategoryAxis no existe` | Falta import de OxyPlot | Agregar `using OxyPlot.Axes;` en el code-behind |
| `DataObject.Pasting no compila` | Falta namespace | Agregar `using System.Windows;` en el code-behind |
| Teléfono valida bien pero igual guarda | `btnGuardar_Click` no llama al validador | Verificar que el bloque de validación esté ANTES de llamar al controller |

---

*SDD Fix Validación + Botones + Gráfico — OptimusCAI Gym v1.0 — Mayo 2026*
