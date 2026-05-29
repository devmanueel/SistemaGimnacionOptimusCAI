# SDD — Filtros Avanzados Sección Socios
## Sistema Gimnasio OptimusCAI · SQL Server + WPF C# 7.3

---

## Contexto

La sección Socios ya tiene implementado:
- Chips: Todos / Activos / Inactivos (basados en estado de membresía)
- Filtro avanzado desplegable con combo principal
- Filtro por **Actividad** (único implementado)
- SP: `sp_ListarSociosConMembresias`
- Entity: `SocioConMembresia`
- DAO: `SocioDao.ListarSociosConMembresias(...)`
- Controller: `SocioController.ListarSociosConMembresias(...)`

**Faltan implementar:** Cuota vencida, Profesor, Sexo, Dejaron de venir.

---

## Datos confirmados

| Dato | Valor |
|---|---|
| Columna sexo en socios | Ya existe. Valores: `'M'`, `'F'`, `'otro'` |
| Roles en el sistema | `1 = Admin`, `2 = Instructor/Empleado` |
| rolId para instructores | `2` |

### Nombres de controles XAML confirmados

| Control | x:Name |
|---|---|
| Combo principal filtros avanzados | `cmbFiltroAvanzado` |
| Combo actividad | `cmbFiltroActividad` |
| Combo instructor/profesor | `cmbFiltroInstructor` |
| Combo sexo | `cmbFiltroSexo` |
| Combo días sin venir | `cmbFiltroDias` |

> **Nota:** El filtro Cuota vencida no tiene combo secundario. Se activa solo con elegir esa opción en `cmbFiltroAvanzado`.

---

## Reglas generales

- Los filtros avanzados se aplican **solo al presionar "Filtrar"**, no en tiempo real.
- La búsqueda por texto sigue siendo en tiempo real.
- Los chips (Todos/Activos/Inactivos) y los filtros avanzados son **acumulativos** (se aplican juntos).
- Los stats cards y contadores de chips son **globales**, no se afectan por filtros avanzados.
- La tabla muestra **una fila por membresía**. Un socio con 2 membresías aparece 2 veces.

---

## PASO 1 — SQL Server (ejecutar en SSMS)

### 1.1 SP modificado — reemplazar el existente

```sql
CREATE OR ALTER PROCEDURE sp_ListarSociosConMembresias
    @Texto               NVARCHAR(100) = '',
    @FiltroEstado        VARCHAR(20)   = 'todos',
    @FiltroActividadId   BIGINT        = NULL,
    @FiltroCuotaVencida  BIT           = NULL,   -- 1 = solo membresías vencidas
    @FiltroInstructorId  BIGINT        = NULL,   -- id del instructor
    @FiltroSexo          VARCHAR(10)   = NULL,   -- 'M' | 'F' | 'otro'
    @FiltroDejaronVenir  INT           = NULL    -- días sin asistir (7, 15, 30, 60, 90)
AS
BEGIN
    SET NOCOUNT ON;

    -- Actualizar estados vencidos
    UPDATE membresias
    SET estado = 'vencida'
    WHERE estado = 'activa' AND fecha_vencimiento < CAST(GETDATE() AS DATE);

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
        -- Última asistencia permitida del socio
        (SELECT MAX(ra.accedido_en)
         FROM registros_acceso ra
         WHERE ra.socio_id = s.id
           AND ra.resultado = 'permitido') AS ultima_asistencia,
        -- Días sin asistir (NULL si nunca asistió)
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
      -- Chip Todos/Activos/Inactivos
      AND (
            @FiltroEstado = 'todos'
         OR (@FiltroEstado = 'activos'   AND m.estado = 'activa')
         OR (@FiltroEstado = 'inactivos' AND m.estado IN ('vencida', 'cancelada'))
          )
      -- Búsqueda por texto
      AND (
            @Texto = ''
         OR s.nombre   LIKE '%' + @Texto + '%'
         OR s.apellido LIKE '%' + @Texto + '%'
         OR s.dni      LIKE '%' + @Texto + '%'
         OR CAST(s.numero_socio AS VARCHAR(20)) LIKE '%' + @Texto + '%'
          )
      -- Filtro actividad (ya implementado)
      AND (@FiltroActividadId IS NULL OR m.actividad_id = @FiltroActividadId)
      -- Filtro cuota vencida
      AND (@FiltroCuotaVencida IS NULL
           OR (@FiltroCuotaVencida = 1 AND m.estado = 'vencida'))
      -- Filtro instructor
      AND (@FiltroInstructorId IS NULL OR m.instructor_id = @FiltroInstructorId)
      -- Filtro sexo
      AND (@FiltroSexo IS NULL OR s.sexo = @FiltroSexo)
      -- Filtro dejaron de venir: no tienen acceso permitido en los últimos N días
      AND (@FiltroDejaronVenir IS NULL
           OR NOT EXISTS (
               SELECT 1 FROM registros_acceso ra
               WHERE ra.socio_id  = s.id
                 AND ra.resultado = 'permitido'
                 AND ra.accedido_en >= DATEADD(DAY, -@FiltroDejaronVenir, GETDATE())
           ))
    ORDER BY s.apellido, s.nombre, m.fecha_vencimiento DESC;
END;
GO
```

### 1.2 Probar cada filtro

```sql
-- Cuota vencida
EXEC sp_ListarSociosConMembresias @FiltroCuotaVencida = 1;

-- Por instructor (rolId = 2, reemplazá con un id real de usuarios)
EXEC sp_ListarSociosConMembresias @FiltroInstructorId = 3;

-- Por sexo femenino
EXEC sp_ListarSociosConMembresias @FiltroSexo = 'F';

-- Por sexo masculino
EXEC sp_ListarSociosConMembresias @FiltroSexo = 'M';

-- Dejaron de venir hace más de 15 días
EXEC sp_ListarSociosConMembresias @FiltroDejaronVenir = 15;

-- Combinado: activos + femenino
EXEC sp_ListarSociosConMembresias @FiltroEstado = 'activos', @FiltroSexo = 'F';
```

---

## PASO 2 — Entity (`SocioConMembresia.cs`)

Agregar las siguientes propiedades a la clase existente:

```csharp
// ── Propiedades nuevas a agregar ──────────────────────────
public string    Sexo             { get; set; }   // 'M' | 'F' | 'otro'
public DateTime? UltimaAsistencia { get; set; }
public int?      DiasSinAsistir   { get; set; }
public string    InstructorNombre { get; set; }

// ── Propiedades calculadas para mostrar en la UI ──────────
public string SexoTexto =>
    Sexo == "M" ? "Masculino" :
    Sexo == "F" ? "Femenino"  : "Otro";

public string UltimaAsistenciaTexto =>
    UltimaAsistencia.HasValue
        ? UltimaAsistencia.Value.ToString("dd/MM/yyyy")
        : "Sin asistencias";

public string DiasSinAsistirTexto =>
    DiasSinAsistir.HasValue
        ? DiasSinAsistir.Value + " días"
        : "—";
```

---

## PASO 3 — DAO (`SocioDao.cs`)

### 3.1 Actualizar firma de `ListarSociosConMembresias`

Reemplazar la firma existente con los nuevos parámetros:

```csharp
public List<SocioConMembresia> ListarSociosConMembresias(
    string texto              = "",
    string filtroEstado       = "todos",
    long?  filtroActividadId  = null,
    bool?  filtroCuotaVencida = null,
    long?  filtroInstructorId = null,
    string filtroSexo         = null,
    int?   filtroDejaronVenir = null)
{
    var lista = new List<SocioConMembresia>();
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

            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    lista.Add(MapearSocioConMembresia(reader));
        }
    }
    return lista;
}
```

### 3.2 Actualizar `MapearSocioConMembresia`

Agregar estos campos al mapeo existente:

```csharp
Sexo             = reader["sexo"] as string,
InstructorNombre = reader["instructor_nombre"] as string,
UltimaAsistencia = reader["ultima_asistencia"] != DBNull.Value
                       ? (DateTime?)Convert.ToDateTime(reader["ultima_asistencia"])
                       : null,
DiasSinAsistir   = reader["dias_sin_asistir"] != DBNull.Value
                       ? (int?)Convert.ToInt32(reader["dias_sin_asistir"])
                       : null,
```

---

## PASO 4 — Controller (`SocioController.cs`)

Actualizar la firma del método existente:

```csharp
public List<SocioConMembresia> ListarSociosConMembresias(
    string texto              = "",
    string filtroEstado       = "todos",
    long?  filtroActividadId  = null,
    bool?  filtroCuotaVencida = null,
    long?  filtroInstructorId = null,
    string filtroSexo         = null,
    int?   filtroDejaronVenir = null)
{
    try
    {
        return _dao.ListarSociosConMembresias(
            texto, filtroEstado, filtroActividadId,
            filtroCuotaVencida, filtroInstructorId,
            filtroSexo, filtroDejaronVenir);
    }
    catch (Exception ex)
    {
        throw new Exception("Error al listar socios.\n" + ex.Message);
    }
}
```

---

## PASO 5 — XAML (`SociosPage.xaml`)

Agregar los controles secundarios dentro del panel de filtros avanzados existente,
**después del `cmbFiltroActividad`**. Todos inician con `Visibility="Collapsed"`.

```xml
<!-- CUOTA VENCIDA — no necesita combo, se activa sola -->
<!-- No agregar nada en XAML para este filtro -->

<!-- PROFESOR — cargado dinámicamente desde BD (rol 2) -->
<ComboBox x:Name="cmbFiltroInstructor"
          Visibility="Collapsed"
          DisplayMemberPath="NombreCompleto"
          SelectedValuePath="Id"
          Height="36"
          Margin="0,0,0,8"/>

<!-- SEXO — opciones fijas -->
<ComboBox x:Name="cmbFiltroSexo"
          Visibility="Collapsed"
          Height="36"
          Margin="0,0,0,8">
    <ComboBoxItem Content="Masculino" Tag="M"/>
    <ComboBoxItem Content="Femenino"  Tag="F"/>
    <ComboBoxItem Content="Otro"      Tag="otro"/>
</ComboBox>

<!-- DEJARON DE VENIR — opciones fijas de días -->
<ComboBox x:Name="cmbFiltroDias"
          Visibility="Collapsed"
          Height="36"
          Margin="0,0,0,8">
    <ComboBoxItem Content="Más de 7 días"  Tag="7"/>
    <ComboBoxItem Content="Más de 15 días" Tag="15"/>
    <ComboBoxItem Content="Más de 30 días" Tag="30"/>
    <ComboBoxItem Content="Más de 60 días" Tag="60"/>
    <ComboBoxItem Content="Más de 90 días" Tag="90"/>
</ComboBox>
```

---

## PASO 6 — Code-behind (`SociosPage.xaml.cs`)

### 6.1 Variables nuevas — agregar junto a las existentes

```csharp
private bool?  _filtroCuotaVencida = null;
private long?  _filtroInstructorId = null;
private string _filtroSexo         = null;
private int?   _filtroDejaronVenir = null;
```

### 6.2 Cargar instructores — llamar en el constructor

```csharp
private void CargarInstructores()
{
    try
    {
        // rolId = 2 (Instructor/Empleado)
        var instructores = _usuarioController.ObtenerUsuariosActivosPorRol(2);
        cmbFiltroInstructor.ItemsSource = instructores;
    }
    catch { /* silencioso — si falla no rompe la pantalla */ }
}
```

### 6.3 Actualizar `cmbFiltroAvanzado_SelectionChanged`

Reemplazar el contenido del evento existente:

```csharp
private void cmbFiltroAvanzado_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    // Ocultar todos los controles secundarios
    if (cmbFiltroActividad  != null) cmbFiltroActividad.Visibility  = Visibility.Collapsed;
    if (cmbFiltroInstructor != null) cmbFiltroInstructor.Visibility = Visibility.Collapsed;
    if (cmbFiltroSexo       != null) cmbFiltroSexo.Visibility       = Visibility.Collapsed;
    if (cmbFiltroDias       != null) cmbFiltroDias.Visibility       = Visibility.Collapsed;

    var item = cmbFiltroAvanzado.SelectedItem as ComboBoxItem;
    if (item == null) return;

    switch (item.Content?.ToString())
    {
        case "Actividad":
            cmbFiltroActividad.Visibility = Visibility.Visible;
            break;
        case "Cuota vencida":
            // No necesita control secundario — se filtra directo al presionar Filtrar
            break;
        case "Profesor":
            cmbFiltroInstructor.Visibility = Visibility.Visible;
            break;
        case "Sexo":
            cmbFiltroSexo.Visibility = Visibility.Visible;
            break;
        case "Dejaron de venir":
            cmbFiltroDias.Visibility = Visibility.Visible;
            break;
    }
}
```

### 6.4 Actualizar `btnFiltrar_Click`

Reemplazar el método existente:

```csharp
private void btnFiltrar_Click(object sender, RoutedEventArgs e)
{
    var item = cmbFiltroAvanzado.SelectedItem as ComboBoxItem;
    string filtroActivo = item?.Content?.ToString();

    // Resetear todos los filtros avanzados
    _filtroActividadId  = null;
    _filtroCuotaVencida = null;
    _filtroInstructorId = null;
    _filtroSexo         = null;
    _filtroDejaronVenir = null;

    switch (filtroActivo)
    {
        case "Actividad":
            if (cmbFiltroActividad.SelectedValue != null)
                _filtroActividadId = (long?)cmbFiltroActividad.SelectedValue;
            break;

        case "Cuota vencida":
            _filtroCuotaVencida = true;
            break;

        case "Profesor":
            if (cmbFiltroInstructor.SelectedValue != null)
                _filtroInstructorId = (long?)cmbFiltroInstructor.SelectedValue;
            break;

        case "Sexo":
            if (cmbFiltroSexo.SelectedItem is ComboBoxItem itemSexo)
                _filtroSexo = itemSexo.Tag?.ToString();
            break;

        case "Dejaron de venir":
            if (cmbFiltroDias.SelectedItem is ComboBoxItem itemDias
                && int.TryParse(itemDias.Tag?.ToString(), out int dias))
                _filtroDejaronVenir = dias;
            break;
    }

    CargarSocios();
}
```

### 6.5 Actualizar `CargarSocios()`

Reemplazar la llamada existente:

```csharp
private void CargarSocios()
{
    try
    {
        var lista = _controller.ListarSociosConMembresias(
            texto:              txtBuscar.Text.Trim(),
            filtroEstado:       _filtroEstado,
            filtroActividadId:  _filtroActividadId,
            filtroCuotaVencida: _filtroCuotaVencida,
            filtroInstructorId: _filtroInstructorId,
            filtroSexo:         _filtroSexo,
            filtroDejaronVenir: _filtroDejaronVenir);

        gridSocios.ItemsSource = lista;
    }
    catch (Exception ex)
    {
        NotificacionWindow.MostrarError(ex.Message, "Error al cargar socios");
    }
}
```

### 6.6 Actualizar `btnLimpiarFiltros_Click`

```csharp
private void btnLimpiarFiltros_Click(object sender, RoutedEventArgs e)
{
    // Resetear variables
    _filtroActividadId  = null;
    _filtroCuotaVencida = null;
    _filtroInstructorId = null;
    _filtroSexo         = null;
    _filtroDejaronVenir = null;

    // Resetear controles
    cmbFiltroAvanzado.SelectedIndex   = -1;
    cmbFiltroActividad.SelectedIndex  = -1;
    cmbFiltroInstructor.SelectedIndex = -1;
    cmbFiltroSexo.SelectedIndex       = -1;
    cmbFiltroDias.SelectedIndex       = -1;

    // Ocultar todos los secundarios
    cmbFiltroActividad.Visibility  = Visibility.Collapsed;
    cmbFiltroInstructor.Visibility = Visibility.Collapsed;
    cmbFiltroSexo.Visibility       = Visibility.Collapsed;
    cmbFiltroDias.Visibility       = Visibility.Collapsed;

    CargarSocios();
}
```

---

## Orden de ejecución

```
SSMS
  1. Ejecutar sp_ListarSociosConMembresias modificado
  2. Probar cada filtro con EXEC para verificar resultados

Visual Studio
  3. SocioConMembresia.cs   → agregar Sexo, UltimaAsistencia, DiasSinAsistir, InstructorNombre
  4. SocioDao.cs            → actualizar firma + MapearSocioConMembresia
  5. SocioController.cs     → actualizar firma
  6. SociosPage.xaml        → agregar cmbFiltroInstructor, cmbFiltroSexo, cmbFiltroDias
  7. SociosPage.xaml.cs     → agregar variables nuevas
  8. SociosPage.xaml.cs     → agregar CargarInstructores() y llamarla en el constructor
  9. SociosPage.xaml.cs     → reemplazar cmbFiltroAvanzado_SelectionChanged
  10. SociosPage.xaml.cs    → reemplazar btnFiltrar_Click
  11. SociosPage.xaml.cs    → reemplazar CargarSocios()
  12. SociosPage.xaml.cs    → reemplazar btnLimpiarFiltros_Click

Pruebas
  13. Filtro Cuota vencida   → debe mostrar membresías con estado 'vencida'
  14. Filtro Profesor        → debe filtrar por instructor_id
  15. Filtro Sexo M          → debe mostrar solo socios masculinos
  16. Filtro Sexo F          → debe mostrar solo socios femeninos
  17. Filtro Dejaron 15 días → debe mostrar socios sin acceso en 15 días
  18. Filtro combinado       → ej: chip Activos + filtro Sexo F
  19. Limpiar filtros        → debe restaurar la tabla completa
  20. Búsqueda + filtro      → texto en tiempo real + filtro avanzado simultáneo
```

---

## Notas importantes

- **Cuota vencida** no necesita control secundario en XAML. Al seleccionarlo en `cmbFiltroAvanzado` y presionar Filtrar, se manda `@FiltroCuotaVencida = 1` directamente.
- **Dejaron de venir** incluye socios que **nunca asistieron** porque no tienen registros en `registros_acceso`. Es comportamiento esperado.
- **`_filtroActividadId`** — usar la variable ya existente en el code-behind, no crear una nueva.
- **NombreCompleto** en el combo de instructores — verificar que la entity de usuario que devuelve `ObtenerUsuariosActivosPorRol` tenga esa propiedad. Si se llama distinto, ajustar `DisplayMemberPath` en el XAML.
