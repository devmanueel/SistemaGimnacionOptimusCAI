# SDD — Paginación infinita (infinite scroll) en Sección Socios
## Sistema Gimnasio OptimusCAI · SQL Server + WPF C# 7.3

---

## Contexto

Actualmente `sp_ListarSociosConMembresias` devuelve **todos** los registros de una vez.
Este SDD reemplaza eso por carga paginada de 10 en 10, activada al llegar al final del scroll.

### Comportamiento esperado
- Al abrir la sección → carga los primeros 10 socios
- Al hacer scroll hasta el último → carga los siguientes 10
- Si no hay más socios → no hace más requests
- Al aplicar un filtro o búsqueda → resetea a página 1 y recarga
- Los chips y stats cards **no se paginan** — siempre muestran totales globales

---

## Datos confirmados

| Dato | Valor |
|---|---|
| Tamaño de página | 10 socios |
| SP a modificar | `sp_ListarSociosConMembresias` |
| DataGrid | `gridSocios` en `SociosPage.xaml` |
| ScrollViewer del DataGrid | se obtiene en code-behind via VisualTreeHelper |
| Filtros existentes | texto, estado (chip), actividad, cuota vencida, instructor, sexo, días sin venir |

---

## PASO 1 — SQL Server

### 1.1 Modificar `sp_ListarSociosConMembresias`

Agregar dos parámetros nuevos y la cláusula de paginación.
El SP devuelve **dos result sets**: los datos paginados + el total de registros.

```sql
CREATE OR ALTER PROCEDURE sp_ListarSociosConMembresias
    @Texto               NVARCHAR(100) = '',
    @FiltroEstado        VARCHAR(20)   = 'todos',
    @FiltroActividadId   BIGINT        = NULL,
    @FiltroCuotaVencida  BIT           = NULL,
    @FiltroInstructorId  BIGINT        = NULL,
    @FiltroSexo          VARCHAR(10)   = NULL,
    @FiltroDejaronVenir  INT           = NULL,
    @Pagina              INT           = 1,     -- nueva
    @TamPagina           INT           = 10     -- nueva
AS
BEGIN
    SET NOCOUNT ON;

    -- Actualizar estados vencidos
    UPDATE membresias
    SET estado = 'vencida'
    WHERE estado = 'activa' AND fecha_vencimiento < CAST(GETDATE() AS DATE);

    -- ── Result set 1: total de registros (para saber si hay más) ──
    SELECT COUNT(*) AS total
    FROM socios s
    INNER JOIN membresias  m ON m.socio_id = s.id
    INNER JOIN actividades a ON a.id       = m.actividad_id
    WHERE s.eliminado_en IS NULL
      AND (
            @FiltroEstado = 'todos'
         OR (@FiltroEstado = 'activos'   AND m.estado = 'activa')
         OR (@FiltroEstado = 'inactivos' AND m.estado IN ('vencida', 'cancelada'))
          )
      AND (
            @Texto = ''
         OR s.nombre   LIKE '%' + @Texto + '%'
         OR s.apellido LIKE '%' + @Texto + '%'
         OR s.dni      LIKE '%' + @Texto + '%'
         OR CAST(s.numero_socio AS VARCHAR(20)) LIKE '%' + @Texto + '%'
          )
      AND (@FiltroActividadId  IS NULL OR m.actividad_id    = @FiltroActividadId)
      AND (@FiltroCuotaVencida IS NULL OR (@FiltroCuotaVencida = 1 AND m.estado = 'vencida'))
      AND (@FiltroInstructorId IS NULL OR m.instructor_id   = @FiltroInstructorId)
      AND (@FiltroSexo         IS NULL OR s.sexo            = @FiltroSexo)
      AND (@FiltroDejaronVenir IS NULL
           OR NOT EXISTS (
               SELECT 1 FROM registros_acceso ra
               WHERE ra.socio_id  = s.id
                 AND ra.resultado = 'permitido'
                 AND ra.accedido_en >= DATEADD(DAY, -@FiltroDejaronVenir, GETDATE())
           ));

    -- ── Result set 2: datos paginados ─────────────────────────────
    SELECT
        s.id                                        AS socio_id,
        s.numero_socio,
        s.nombre,
        s.apellido,
        s.nombre + ' ' + s.apellido                AS socio_nombre,
        s.dni,
        s.telefono,
        s.email,
        s.sexo,
        s.foto,
        s.activo                                    AS socio_activo,
        m.id                                        AS membresia_id,
        m.actividad_id,
        m.instructor_id,
        m.fecha_inicio,
        m.fecha_vencimiento,
        m.monto_pagado,
        m.metodo_pago,
        m.estado                                    AS membresia_estado,
        m.tipo_plan,
        m.upgrade_realizado,
        a.nombre                                    AS actividad_nombre,
        a.categoria                                 AS actividad_categoria,
        a.nivel                                     AS actividad_nivel,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sin asignar') AS instructor_nombre,
        DATEDIFF(DAY, CAST(GETDATE() AS DATE), m.fecha_vencimiento) AS dias_para_vencer,
        (SELECT MAX(ra.accedido_en)
         FROM registros_acceso ra
         WHERE ra.socio_id = s.id
           AND ra.resultado = 'permitido') AS ultima_asistencia,
        DATEDIFF(DAY,
            (SELECT MAX(CAST(ra.accedido_en AS DATE))
             FROM registros_acceso ra
             WHERE ra.socio_id = s.id
               AND ra.resultado = 'permitido'),
            CAST(GETDATE() AS DATE)
        ) AS dias_sin_asistir
    FROM socios s
    INNER JOIN membresias  m ON m.socio_id = s.id
    INNER JOIN actividades a ON a.id       = m.actividad_id
    LEFT  JOIN usuarios    u ON u.id       = m.instructor_id
    WHERE s.eliminado_en IS NULL
      AND (
            @FiltroEstado = 'todos'
         OR (@FiltroEstado = 'activos'   AND m.estado = 'activa')
         OR (@FiltroEstado = 'inactivos' AND m.estado IN ('vencida', 'cancelada'))
          )
      AND (
            @Texto = ''
         OR s.nombre   LIKE '%' + @Texto + '%'
         OR s.apellido LIKE '%' + @Texto + '%'
         OR s.dni      LIKE '%' + @Texto + '%'
         OR CAST(s.numero_socio AS VARCHAR(20)) LIKE '%' + @Texto + '%'
          )
      AND (@FiltroActividadId  IS NULL OR m.actividad_id    = @FiltroActividadId)
      AND (@FiltroCuotaVencida IS NULL OR (@FiltroCuotaVencida = 1 AND m.estado = 'vencida'))
      AND (@FiltroInstructorId IS NULL OR m.instructor_id   = @FiltroInstructorId)
      AND (@FiltroSexo         IS NULL OR s.sexo            = @FiltroSexo)
      AND (@FiltroDejaronVenir IS NULL
           OR NOT EXISTS (
               SELECT 1 FROM registros_acceso ra
               WHERE ra.socio_id  = s.id
                 AND ra.resultado = 'permitido'
                 AND ra.accedido_en >= DATEADD(DAY, -@FiltroDejaronVenir, GETDATE())
           ))
    ORDER BY s.apellido, s.nombre, m.fecha_vencimiento DESC
    OFFSET (@Pagina - 1) * @TamPagina ROWS
    FETCH NEXT @TamPagina ROWS ONLY;
END;
GO
```

### 1.2 Probar la paginación

```sql
-- Página 1 (primeros 10)
EXEC sp_ListarSociosConMembresias @Pagina = 1, @TamPagina = 10;

-- Página 2 (siguientes 10)
EXEC sp_ListarSociosConMembresias @Pagina = 2, @TamPagina = 10;

-- Con filtro activos, página 1
EXEC sp_ListarSociosConMembresias @FiltroEstado = 'activos', @Pagina = 1, @TamPagina = 10;

-- Verificar que el primer result set devuelve el total correcto
-- y el segundo devuelve exactamente 10 filas (o menos si es la última página)
```

---

## PASO 2 — DAO (`SocioDao.cs`)

### 2.1 Crear clase de resultado paginado

Agregar esta clase al final de `SocioDao.cs` o en un archivo nuevo `ResultadoPaginado.cs` en Entities:

```csharp
// Agregar en Entities o al final de SocioDao.cs
public class ResultadoPaginado<T>
{
    public List<T> Items     { get; set; } = new List<T>();
    public int     Total     { get; set; }
    public bool    HayMas    { get; set; }
    public int     Pagina    { get; set; }
    public int     TamPagina { get; set; }
}
```

### 2.2 Modificar `ListarSociosConMembresias` en `SocioDao.cs`

Reemplazar la firma y el cuerpo del método existente:

```csharp
public ResultadoPaginado<SocioConMembresia> ListarSociosConMembresias(
    string texto              = "",
    string filtroEstado       = "todos",
    long?  filtroActividadId  = null,
    bool?  filtroCuotaVencida = null,
    long?  filtroInstructorId = null,
    string filtroSexo         = null,
    int?   filtroDejaronVenir = null,
    int    pagina             = 1,
    int    tamPagina          = 10)
{
    var resultado = new ResultadoPaginado<SocioConMembresia>
    {
        Pagina    = pagina,
        TamPagina = tamPagina
    };

    using (var conn = GetConnection())
    {
        conn.Open();
        using (var cmd = new SqlCommand("sp_ListarSociosConMembresias", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@Texto",               texto ?? string.Empty);
            cmd.Parameters.AddWithValue("@FiltroEstado",        filtroEstado ?? "todos");
            cmd.Parameters.AddWithValue("@FiltroActividadId",   (object)filtroActividadId  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FiltroCuotaVencida",  (object)filtroCuotaVencida ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FiltroInstructorId",  (object)filtroInstructorId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FiltroSexo",          (object)filtroSexo         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FiltroDejaronVenir",  (object)filtroDejaronVenir ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Pagina",              pagina);
            cmd.Parameters.AddWithValue("@TamPagina",           tamPagina);

            using (var reader = cmd.ExecuteReader())
            {
                // ── Result set 1: total ──
                if (reader.Read())
                    resultado.Total = Convert.ToInt32(reader["total"]);

                // ── Result set 2: datos paginados ──
                reader.NextResult();
                while (reader.Read())
                    resultado.Items.Add(MapearSocioConMembresia(reader));
            }
        }
    }

    resultado.HayMas = (pagina * tamPagina) < resultado.Total;
    return resultado;
}
```

---

## PASO 3 — Controller (`SocioController.cs`)

### 3.1 Modificar `ListarSociosConMembresias`

Reemplazar la firma y el cuerpo del método existente:

```csharp
public ResultadoPaginado<SocioConMembresia> ListarSociosConMembresias(
    string texto              = "",
    string filtroEstado       = "todos",
    long?  filtroActividadId  = null,
    bool?  filtroCuotaVencida = null,
    long?  filtroInstructorId = null,
    string filtroSexo         = null,
    int?   filtroDejaronVenir = null,
    int    pagina             = 1,
    int    tamPagina          = 10)
{
    try
    {
        return _dao.ListarSociosConMembresias(
            texto, filtroEstado, filtroActividadId,
            filtroCuotaVencida, filtroInstructorId,
            filtroSexo, filtroDejaronVenir,
            pagina, tamPagina);
    }
    catch (Exception ex)
    {
        throw new Exception("Error al listar socios.\n" + ex.Message);
    }
}
```

---

## PASO 4 — XAML (`SociosPage.xaml`)

### 4.1 Envolver el DataGrid en un ScrollViewer con nombre

Buscar el `DataGrid` con `x:Name="gridSocios"` y agregar el evento `ScrollChanged` al `ScrollViewer` que lo contiene. Como el DataGrid tiene su propio scroll interno, hay que obtenerlo via code-behind (ver paso 5).

No se requieren cambios en el XAML del DataGrid. Solo agregar un indicador de carga al pie de la tabla:

```xml
<!-- Agregar DEBAJO del DataGrid, dentro del mismo Border que lo contiene -->
<Border x:Name="panelCargando"
        Grid.Row="1"
        Visibility="Collapsed"
        Background="{StaticResource Bg1}"
        Padding="0,10">
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
        <TextBlock Text="Cargando más socios..."
                   Foreground="{StaticResource TextMuted}"
                   FontSize="12" FontStyle="Italic"
                   VerticalAlignment="Center"/>
    </StackPanel>
</Border>
```

Para que esto funcione, el `Grid` interno del `Border` de la tabla necesita una fila extra:

```xml
<!-- Modificar el Grid interno del Border de la tabla -->
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="3"/>    <!-- barra verde (ya existe) -->
        <RowDefinition Height="*"/>    <!-- DataGrid (ya existe) -->
        <RowDefinition Height="Auto"/> <!-- indicador de carga (NUEVO) -->
    </Grid.RowDefinitions>

    <!-- barra verde — sin cambios -->
    <Border Grid.Row="0" .../>

    <!-- DataGrid — sin cambios, solo agregar Grid.Row="1" si no lo tiene -->
    <DataGrid Grid.Row="1" x:Name="gridSocios" .../>

    <!-- NUEVO: indicador de carga -->
    <Border x:Name="panelCargando" Grid.Row="2"
            Visibility="Collapsed"
            Padding="0,10">
        <TextBlock Text="Cargando más socios..."
                   Foreground="{StaticResource TextMuted}"
                   FontSize="12" FontStyle="Italic"
                   HorizontalAlignment="Center"/>
    </Border>
</Grid>
```

---

## PASO 5 — Code-behind (`SociosPage.xaml.cs`)

### 5.1 Variables nuevas — agregar junto a las existentes

```csharp
// ── Paginación ────────────────────────────────────────────
private int  _paginaActual = 1;
private bool _hayMas       = true;
private bool _cargando     = false;
private const int TAM_PAGINA = 10;
```

### 5.2 Suscribir al evento de scroll del DataGrid — en el constructor

Agregar al final del constructor de `SociosPage`, después de `CargarSocios()`:

```csharp
// Suscribir al scroll del DataGrid para infinite scroll
Loaded += (s, e) => SuscribirScrollDataGrid();
```

### 5.3 Agregar método `SuscribirScrollDataGrid`

```csharp
private void SuscribirScrollDataGrid()
{
    var scrollViewer = ObtenerScrollViewer(gridSocios);
    if (scrollViewer != null)
        scrollViewer.ScrollChanged += OnScrollChanged;
}

// Obtener el ScrollViewer interno del DataGrid via VisualTreeHelper
private static ScrollViewer ObtenerScrollViewer(DependencyObject obj)
{
    if (obj is ScrollViewer) return (ScrollViewer)obj;

    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
    {
        var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
        var result = ObtenerScrollViewer(child);
        if (result != null) return result;
    }
    return null;
}
```

### 5.4 Agregar el evento `OnScrollChanged`

```csharp
private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
{
    // Verificar si llegó cerca del final (margen de 50px)
    bool llegoAlFinal = e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 50;

    if (llegoAlFinal && _hayMas && !_cargando)
    {
        _paginaActual++;
        CargarSociosPagina(_paginaActual, agregar: true);
    }
}
```

### 5.5 Reemplazar `CargarSocios()` con dos métodos

**Reemplazar** el método `CargarSocios()` existente con estos dos:

```csharp
// Llamado al cambiar filtros, búsqueda o chips → resetea a página 1
private void CargarSocios()
{
    _paginaActual = 1;
    _hayMas       = true;
    CargarSociosPagina(1, agregar: false);
}

// Llamado tanto para la primera carga como para las siguientes páginas
private void CargarSociosPagina(int pagina, bool agregar)
{
    if (_cargando) return;
    _cargando = true;

    if (panelCargando != null)
        panelCargando.Visibility = Visibility.Visible;

    try
    {
        // IMPORTANTE: pasar _filtroEstado al SP para que filtre ANTES de paginar.
        // Si se pasa "todos" y se filtra en C# después, el infinite scroll
        // nunca llegaría al final cuando hay pocas filas visibles en la página.
        var resultado = _controller.ListarSociosConMembresias(
            texto:              txtBuscar != null ? txtBuscar.Text.Trim() : "",
            filtroEstado:       _filtroEstado,   // ← chip real, no hardcodear "todos"
            filtroActividadId:  _filtroActividadId,
            filtroCuotaVencida: _filtroCuotaVencida,
            filtroInstructorId: _filtroInstructorId,
            filtroSexo:         _filtroSexo,
            filtroDejaronVenir: _filtroDejaronVenir,
            pagina:             pagina,
            tamPagina:          TAM_PAGINA);

        _hayMas = resultado.HayMas;

        if (agregar)
        {
            // Agregar los nuevos items a la lista existente
            var listaActual = gridSocios.ItemsSource as List<SocioConMembresia>
                              ?? new List<SocioConMembresia>();
            listaActual.AddRange(resultado.Items);

            // Reasignar para forzar el refresco del DataGrid
            gridSocios.ItemsSource = null;
            gridSocios.ItemsSource = listaActual;
        }
        else
        {
            // Primera carga o reset por filtro — no filtrar en C# después,
            // el SP ya devuelve solo los registros del chip seleccionado
            gridSocios.ItemsSource = resultado.Items;

            // Actualizar contadores de chips con el total
            ActualizarContadoresChipsConTotal(resultado.Total, resultado.Items);
        }

        ActualizarResumenFiltros(resultado.Items);
    }
    catch (Exception ex)
    {
        NotificacionWindow.MostrarError(ex.Message, "Error al cargar socios");
    }
    finally
    {
        _cargando = false;
        if (panelCargando != null)
            panelCargando.Visibility = Visibility.Collapsed;
    }
}
```

### 5.6 Actualizar `ActualizarContadoresChips`

El método actual recibe `List<SocioConMembresia>`. Como ahora solo tenemos la página actual, 
los contadores deben basarse en el `total` que devuelve el SP, no en los items cargados.

**Reemplazar** `ActualizarContadoresChips` con:

```csharp
private void ActualizarContadoresChipsConTotal(int total, List<SocioConMembresia> itemsPagina)
{
    try
    {
        // Para los chips necesitamos los totales reales
        // Hacemos una consulta extra de solo conteos (sin paginación, solo count)
        // Los chips siempre muestran totales sobre TODOS los estados
        // independientemente del chip seleccionado, por eso pasamos "todos"
        var todosParaContar = _controller.ListarSociosConMembresias(
            texto:              txtBuscar != null ? txtBuscar.Text.Trim() : "",
            filtroEstado:       "todos",   // siempre "todos" para contar correctamente
            filtroActividadId:  _filtroActividadId,
            filtroCuotaVencida: _filtroCuotaVencida,
            filtroInstructorId: _filtroInstructorId,
            filtroSexo:         _filtroSexo,
            filtroDejaronVenir: _filtroDejaronVenir,
            pagina:             1,
            tamPagina:          99999); // traer todo solo para contar los 3 chips

        int totalActivos   = 0;
        int totalInactivos = 0;
        foreach (var s in todosParaContar.Items)
        {
            if (s.MembresiaEstado == "activa") totalActivos++;
            else totalInactivos++;
        }

        chipTodosNum.Text     = "(" + todosParaContar.Total + ")";
        chipActivosNum.Text   = "(" + totalActivos + ")";
        chipInactivosNum.Text = "(" + totalInactivos + ")";
    }
    catch
    {
        chipTodosNum.Text = chipActivosNum.Text = chipInactivosNum.Text = "(0)";
    }
}
```

> **Nota:** Si esta doble consulta es lenta, se puede optimizar con un SP separado de conteo.
> Por ahora es la solución más simple y correcta.

### 5.7 Resetear paginación al cambiar filtros

Verificar que estos métodos llaman a `CargarSocios()` (que ya resetea la página).
**No agregar filtrado en C# en ninguno de estos métodos** — el SP ya filtra antes de paginar:

```csharp
// Estos métodos ya llaman CargarSocios() — no requieren cambios:
// - txtBuscar_TextChanged      → resetea a página 1
// - chipFiltro_Click           → actualiza _filtroEstado y resetea a página 1
// - btnFiltrar_Click           → actualiza filtros avanzados y resetea a página 1
// - btnLimpiarFiltros_Click    → limpia filtros y resetea a página 1

// IMPORTANTE: eliminar cualquier bloque de filtrado en C# que exista
// en CargarSocios() del tipo:
//   if (_filtroEstado == "activos")
//       listaFiltrada = listaCompleta.FindAll(s => s.MembresiaEstado == "activa");
// Ese bloque ya no tiene sentido — el SP filtra antes de paginar.
```

---

## Orden de ejecución

```
SQL Server
  1. Ejecutar sp_ListarSociosConMembresias modificado (con @Pagina y @TamPagina)
  2. Verificar que devuelve 2 result sets:
     - Primero: una fila con columna "total"
     - Segundo: hasta 10 filas de datos
  3. Probar: EXEC sp_ListarSociosConMembresias @Pagina=1, @TamPagina=10
  4. Probar: EXEC sp_ListarSociosConMembresias @Pagina=2, @TamPagina=10

Visual Studio
  5. Agregar clase ResultadoPaginado<T> en Entities
  6. Reemplazar ListarSociosConMembresias en SocioDao.cs (lee 2 result sets)
  7. Reemplazar ListarSociosConMembresias en SocioController.cs (firma nueva)
  8. Agregar panelCargando al XAML (con la fila extra en el Grid interno)
  9. Agregar variables de paginación en SociosPage.xaml.cs
  10. Agregar SuscribirScrollDataGrid() y llamarla en Loaded
  11. Agregar ObtenerScrollViewer() (helper estático)
  12. Agregar OnScrollChanged()
  13. Reemplazar CargarSocios() con CargarSocios() + CargarSociosPagina()
  14. Reemplazar ActualizarContadoresChips con ActualizarContadoresChipsConTotal()
  15. Compilar y corregir errores

Pruebas
  16. Abrir sección Socios → debe cargar solo 10 socios
  17. Hacer scroll hasta el último socio → debe aparecer "Cargando..."
      y agregar los siguientes 10
  18. Llegar al último socio disponible → no debe cargar más
  19. Escribir en el buscador → debe resetear a página 1 y mostrar resultados filtrados
  20. Cambiar chip (Activos/Inactivos) → debe resetear a página 1
  21. Aplicar filtro avanzado → debe resetear a página 1
  22. Limpiar filtros → debe resetear a página 1 y cargar todos
  23. Verificar que los contadores de chips muestran totales correctos
      (no solo los 10 cargados)
  24. Verificar que stats cards no se afectan por la paginación
```

---

## Notas importantes

- **`OFFSET/FETCH`** requiere SQL Server 2012 o superior. LocalDB lo soporta.
- **La lista al hacer scroll** — se reasigna `ItemsSource` completa para forzar el refresco del DataGrid. Esto es necesario porque `List<T>` no notifica cambios. Si en el futuro se quiere optimizar, se puede usar `ObservableCollection<T>`.
- **Filtro por chip (Activos/Inactivos)** — corregido en este SDD. `CargarSociosPagina()` pasa `_filtroEstado` real al SP. El SP filtra antes de paginar, por eso el infinite scroll funciona correctamente con cualquier chip. Los contadores de chips usan una segunda llamada con `"todos"` para mostrar los 3 totales reales independientemente del chip activo.
- **Double query para contadores** — la llamada extra con `tamPagina: 99999` es una solución temporal. Si el gimnasio crece mucho, reemplazar con un SP separado `sp_ContarSociosConMembresias` que solo devuelva los 3 contadores.
