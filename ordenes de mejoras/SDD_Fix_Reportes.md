# SDD — Fix Módulo Reportes: Balance Negativo + Tarifa Docentes
> Spec-Driven Development — Corrección de bugs detectados en producción  
> Versión 1.0 — Mayo 2026  
> **Leer COMPLETO antes de tocar cualquier archivo**

---

## 0. CONTEXTO VISUAL — Qué se ve mal

### Pantalla 1 — Tab "Ingresos y Ganancias"
- `INGRESOS TOTALES: $0,00`
- `EGRESOS TOTALES: $0,00`  
- `BALANCE: -$834.667,00` ← **negativo a pesar de que hay ingresos reales**
- Columna "Concepto" en el DataGrid aparece vacía en varias filas

### Pantalla 2 — Tab "Sueldos Docentes"
- `TOTAL A PAGAR: $0,00` para todos los instructores
- Columnas "SUELDO ESTIMADO" e "INGRESOS GENERADOS" todas en `$0,00`
- No existe campo editable para cargar la tarifa por hora de cada instructor

---

## 1. DIAGNÓSTICO — Causa raíz de cada bug

### Bug 1 — Balance negativo / totales en $0
**Causa:** El CASE en `sp_ReporteTotales` compara `tipo = 'ingreso'` pero los valores reales en la tabla `caja_movimientos` pueden tener mayúsculas, espacios o valores distintos. Si el campo `tipo` tiene `'Ingreso'` o `' ingreso '` el CASE no matchea y suma todo como egreso.

**Causa secundaria:** El JOIN con `membresias` a través de `referencia_id`/`referencia_tipo` falla cuando esas columnas son NULL en movimientos viejos, dejando el concepto vacío.

### Bug 2 — Sueldo $0 en docentes
**Causa:** La columna `tarifa_hora` en la tabla `usuarios` no existe, o existe pero tiene valor `0` o `NULL` en todos los instructores. No hay ninguna pantalla en el sistema que permita cargarla.

---

## 2. CONDICIONES — Reglas no negociables

```
- C# 7.3 estricto
- SQL Server LocalDB: DROP + CREATE (nunca CREATE OR ALTER)
- Sin SQL inline en DAOs — solo Stored Procedures
- Sin LetterSpacing ni DropShadowEffect en Triggers en XAML
- SesionManager.UsuarioId en lugar de IDs hardcodeados
- Auditor.Registrar() al actualizar tarifa_hora de un instructor
```

---

## 3. CLASIFICACIÓN — Archivos a tocar

| Archivo | Tipo de cambio |
|---------|---------------|
| `sp_ReporteTotales` | 🔴 Reescribir completo |
| `sp_ReporteIngresos` | 🔴 Reescribir completo |
| `sp_ActualizarTarifaInstructor` | 🟢 Crear nuevo |
| `ALTER TABLE usuarios` | 🟡 Agregar columna si no existe |
| `ReporteDao.cs` | 🟡 Agregar método `ActualizarTarifa()` |
| `ReporteController.cs` | 🟡 Agregar método `ActualizarTarifaInstructor()` |
| `ReportesPage.xaml` | 🟡 Columna tarifa editable en tab Sueldos |
| `ReportesPage.xaml.cs` | 🟡 Evento `TarifaHora_LostFocus()` |

---

## 4. PLAN DE EJECUCIÓN

```
PASO 1: Correr query de diagnóstico en SQL Explorer
         → ver valores reales en columna tipo
PASO 2: ALTER TABLE usuarios (agregar tarifa_hora si no existe)
PASO 3: Ejecutar sp_ActualizarTarifaInstructor (nuevo SP)
PASO 4: Ejecutar sp_ReporteTotales (reescritura con LOWER + LTRIM)
PASO 5: Ejecutar sp_ReporteIngresos (reescritura con concepto auto)
PASO 6: Cargar tarifa de prueba para verificar
PASO 7: Modificar ReporteDao.cs
PASO 8: Modificar ReporteController.cs
PASO 9: Modificar ReportesPage.xaml (columna tarifa editable)
PASO 10: Modificar ReportesPage.xaml.cs (evento LostFocus)
PASO 11: Compilar y probar
```

---

## 5. CÓDIGO — Implementar exactamente esto

---

### PASO 1 — Query de diagnóstico (ejecutar primero, no es un SP)

```sql
-- Correr en SQL Server Object Explorer → New Query → seleccionar DB_CAI_Optimus.mdf
-- Muestra los valores reales de las columnas clave

SELECT DISTINCT 
    tipo, 
    referencia_tipo, 
    ISNULL(metodo_pago,'NULL') AS metodo_pago,
    ISNULL(subtipo,'NULL')    AS subtipo
FROM caja_movimientos
ORDER BY tipo;

-- También ver si hay conceptos vacíos:
SELECT id, tipo, concepto, monto, creado_en
FROM caja_movimientos
WHERE LTRIM(RTRIM(ISNULL(concepto,''))) = ''
ORDER BY creado_en DESC;
```

> **IMPORTANTE:** Los resultados de este query definen si hay variaciones de mayúsculas.
> El SP ya usa `LOWER()` + `LTRIM()` + `RTRIM()` para cubrirlas todas, pero
> si el valor real es algo completamente distinto (ej: `'entrada'` o `'cobro'`)
> hay que ajustar el CASE en sp_ReporteTotales según corresponda.

---

### PASO 2 — ALTER TABLE usuarios

```sql
-- Agregar columna tarifa_hora si no existe
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('usuarios') 
      AND name = 'tarifa_hora'
)
BEGIN
    ALTER TABLE usuarios 
    ADD tarifa_hora DECIMAL(10,2) NOT NULL DEFAULT 0;
    PRINT 'Columna tarifa_hora agregada correctamente.';
END
ELSE
BEGIN
    PRINT 'La columna tarifa_hora ya existe.';
END

-- Verificar resultado:
SELECT id, nombre, apellido, rol_id, tarifa_hora 
FROM usuarios 
WHERE rol_id = 2
ORDER BY apellido;
```

---

### PASO 3 — SP `sp_ActualizarTarifaInstructor` (nuevo)

```sql
IF OBJECT_ID('sp_ActualizarTarifaInstructor','P') IS NOT NULL 
    DROP PROCEDURE sp_ActualizarTarifaInstructor;
GO
CREATE PROCEDURE sp_ActualizarTarifaInstructor
    @InstructorId BIGINT,
    @TarifaHora   DECIMAL(10,2)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = @InstructorId AND rol_id = 2)
    BEGIN
        RAISERROR('El usuario no existe o no es un instructor.', 16, 1);
        RETURN;
    END

    IF @TarifaHora < 0
    BEGIN
        RAISERROR('La tarifa no puede ser negativa.', 16, 1);
        RETURN;
    END

    UPDATE usuarios 
    SET tarifa_hora = @TarifaHora 
    WHERE id = @InstructorId;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO
```

---

### PASO 4 — SP `sp_ReporteTotales` (reescritura completa)

```sql
IF OBJECT_ID('sp_ReporteTotales','P') IS NOT NULL DROP PROCEDURE sp_ReporteTotales;
GO
CREATE PROCEDURE sp_ReporteTotales
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FechaDesde IS NULL
        SET @FechaDesde = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
    IF @FechaHasta IS NULL
        SET @FechaHasta = CAST(GETDATE() AS DATE);

    -- ─── Resultset 1: Totales generales ───────────────────────────────────
    -- Usa LOWER + LTRIM + RTRIM para cubrir variaciones de mayúsculas/espacios
    SELECT
        ISNULL(SUM(CASE 
            WHEN LOWER(LTRIM(RTRIM(tipo))) = 'ingreso' THEN monto 
            ELSE 0 END), 0)                                 AS total_ingresos,
        ISNULL(SUM(CASE 
            WHEN LOWER(LTRIM(RTRIM(tipo))) = 'egreso' THEN monto 
            ELSE 0 END), 0)                                 AS total_egresos,
        ISNULL(SUM(CASE 
            WHEN LOWER(LTRIM(RTRIM(tipo))) = 'ingreso' THEN monto 
            ELSE -monto END), 0)                            AS balance,
        COUNT(CASE 
            WHEN LOWER(LTRIM(RTRIM(tipo))) = 'ingreso' THEN 1 END) AS cantidad_ingresos,
        COUNT(CASE 
            WHEN LOWER(LTRIM(RTRIM(tipo))) = 'egreso'  THEN 1 END) AS cantidad_egresos
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta;

    -- ─── Resultset 2: Ingresos por actividad ─────────────────────────────
    SELECT
        ISNULL(a.nombre, 'Otro ingreso')        AS actividad,
        ISNULL(SUM(cm.monto), 0)                AS total,
        COUNT(*)                                 AS cantidad
    FROM caja_movimientos cm
    LEFT JOIN membresias m
           ON m.id = cm.referencia_id
          AND LOWER(LTRIM(RTRIM(ISNULL(cm.referencia_tipo,'')))) = 'membresia'
    LEFT JOIN actividades a ON a.id = m.actividad_id
    WHERE CAST(cm.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND LOWER(LTRIM(RTRIM(tipo))) = 'ingreso'
    GROUP BY a.nombre
    ORDER BY total DESC;

    -- ─── Resultset 3: Ingresos por método de pago ────────────────────────
    SELECT
        ISNULL(NULLIF(LTRIM(RTRIM(metodo_pago)), ''), 'No especificado') AS metodo_pago,
        ISNULL(SUM(monto), 0)                   AS total,
        COUNT(*)                                 AS cantidad
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND LOWER(LTRIM(RTRIM(tipo))) = 'ingreso'
    GROUP BY LTRIM(RTRIM(metodo_pago))
    ORDER BY total DESC;
END;
GO
```

---

### PASO 5 — SP `sp_ReporteIngresos` (reescritura completa)

```sql
IF OBJECT_ID('sp_ReporteIngresos','P') IS NOT NULL DROP PROCEDURE sp_ReporteIngresos;
GO
CREATE PROCEDURE sp_ReporteIngresos
    @FechaDesde   DATE        = NULL,
    @FechaHasta   DATE        = NULL,
    @ActividadId  BIGINT      = NULL,
    @MetodoPago   VARCHAR(30) = NULL,
    @InstructorId BIGINT      = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FechaDesde IS NULL
        SET @FechaDesde = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
    IF @FechaHasta IS NULL
        SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        cm.id,
        LOWER(LTRIM(RTRIM(cm.tipo)))                            AS tipo,
        ISNULL(cm.subtipo, '')                                  AS subtipo,

        -- Concepto: si está vacío, generar uno descriptivo automáticamente
        CASE
            WHEN LTRIM(RTRIM(ISNULL(cm.concepto, ''))) <> ''
                THEN cm.concepto
            WHEN m.id IS NOT NULL AND s.id IS NOT NULL
                THEN 'Membresía ' + ISNULL(m.tipo_plan, '') +
                     ' — ' + s.nombre + ' ' + s.apellido +
                     ' (#' + CAST(ISNULL(s.numero_socio, s.id) AS VARCHAR) + ')'
            WHEN m.id IS NOT NULL
                THEN 'Membresía ' + ISNULL(m.tipo_plan, '') +
                     ' — Socio #' + CAST(m.socio_id AS VARCHAR)
            ELSE UPPER(LEFT(LOWER(LTRIM(RTRIM(cm.tipo))),1)) +
                 SUBSTRING(LOWER(LTRIM(RTRIM(cm.tipo))),2,100) +
                 ISNULL(' — ' + cm.subtipo, '')
        END                                                     AS concepto,

        cm.monto,
        ISNULL(NULLIF(LTRIM(RTRIM(cm.metodo_pago)),''),
               'No especificado')                               AS metodo_pago,
        ISNULL(cm.referencia_tipo, '')                          AS referencia_tipo,
        CAST(cm.creado_en AS DATE)                              AS fecha,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')          AS registrado_por_nombre,
        ISNULL(a.nombre, '—')                                   AS actividad_nombre,
        ISNULL(ui.nombre + ' ' + ui.apellido, '—')              AS instructor_nombre

    FROM caja_movimientos cm
    LEFT JOIN usuarios u
           ON u.id = cm.registrado_por
    LEFT JOIN membresias m
           ON m.id = cm.referencia_id
          AND LOWER(LTRIM(RTRIM(ISNULL(cm.referencia_tipo,'')))) = 'membresia'
    LEFT JOIN socios s
           ON s.id = m.socio_id
    LEFT JOIN actividades a
           ON a.id = m.actividad_id
    -- Buscar el instructor del turno de esa actividad
    LEFT JOIN (
        SELECT DISTINCT actividad_id, instructor_id
        FROM turnos
        WHERE activo = 1 AND instructor_id IS NOT NULL
    ) t ON t.actividad_id = a.id
    LEFT JOIN usuarios ui
           ON ui.id = t.instructor_id

    WHERE CAST(cm.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND (@ActividadId  IS NULL OR a.id  = @ActividadId)
      AND (@MetodoPago   IS NULL OR LOWER(LTRIM(RTRIM(cm.metodo_pago)))
                                 = LOWER(LTRIM(RTRIM(@MetodoPago))))
      AND (@InstructorId IS NULL OR ui.id = @InstructorId)

    ORDER BY cm.creado_en DESC;
END;
GO
```

---

### PASO 6 — Cargar tarifas de prueba

```sql
-- Después de ejecutar los SPs, cargar tarifas reales para cada instructor.
-- Primero ver qué instructores hay:
SELECT id, nombre, apellido, tarifa_hora FROM usuarios WHERE rol_id = 2;

-- Luego actualizar cada uno (reemplazar X con el ID real y Y con la tarifa real):
UPDATE usuarios SET tarifa_hora = 1500.00 WHERE id = X; -- Lorena Martínez
UPDATE usuarios SET tarifa_hora = 1200.00 WHERE id = Y; -- Manuel Mendoza
UPDATE usuarios SET tarifa_hora = 1800.00 WHERE id = Z; -- Santiago Sánchez
```

---

### PASO 7 — `ReporteDao.cs` — agregar método `ActualizarTarifa`

Ubicación: `Models/DAO/ReporteDao.cs`

Agregar este método a la clase `ReporteDao` existente:

```csharp
/// <summary>
/// Actualiza la tarifa por hora de un instructor.
/// Retorna true si se afectó al menos 1 fila.
/// </summary>
public bool ActualizarTarifaInstructor(long instructorId, decimal tarifaHora)
{
    using (var conn = GetConnection())
    {
        conn.Open();
        using (var cmd = new SqlCommand("sp_ActualizarTarifaInstructor", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@InstructorId", instructorId);
            cmd.Parameters.AddWithValue("@TarifaHora",   tarifaHora);
            var filas = cmd.ExecuteScalar();
            return filas != null && Convert.ToInt32(filas) > 0;
        }
    }
}
```

---

### PASO 8 — `ReporteController.cs` — agregar método público

Ubicación: `Controllers/ReporteController.cs`

Agregar a la clase `ReporteController` existente:

```csharp
/// <summary>
/// Actualiza la tarifa/hora de un instructor y registra en auditoría.
/// </summary>
public (bool ok, string mensaje) ActualizarTarifaInstructor(
    long instructorId, decimal tarifaHora)
{
    if (instructorId <= 0)
        return (false, "Instructor inválido.");

    if (tarifaHora < 0)
        return (false, "La tarifa no puede ser negativa.");

    try
    {
        bool ok = _dao.ActualizarTarifaInstructor(instructorId, tarifaHora);

        if (ok)
        {
            // Registrar en auditoría
            Auditor.Registrar("editar", "usuario", instructorId,
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "campo",        "tarifa_hora" },
                    { "valor_nuevo",  tarifaHora }
                });
            return (true, "Tarifa actualizada correctamente.");
        }

        return (false, "No se encontró el instructor.");
    }
    catch (Exception ex)
    {
        return (false, "Error al actualizar tarifa.\n" + ex.Message);
    }
}
```

---

### PASO 9 — `ReportesPage.xaml` — columna tarifa editable en tab Sueldos

Ubicación: `SistemaGimnacionOptimusCAI/Paginas/ReportesPage.xaml`

Buscar la columna de tarifa en el DataGrid de sueldos y **reemplazar** con:

```xml
<!-- REEMPLAZAR la columna "TARIFA/H" existente por esta versión editable -->
<DataGridTemplateColumn Header="TARIFA/H" Width="120">

    <!-- Vista normal: muestra el valor formateado -->
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <Border Background="Transparent" Padding="8,0"
                    ToolTip="Doble click para editar">
                <TextBlock Text="{Binding TarifaTexto}"
                           FontFamily="Consolas" FontSize="12"
                           FontWeight="SemiBold"
                           Foreground="#A78BFA"
                           VerticalAlignment="Center"/>
            </Border>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>

    <!-- Vista edición: se activa con doble click sobre la celda -->
    <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
            <TextBox x:Name="txtTarifaEdit"
                     Text="{Binding TarifaHora, StringFormat=N2,
                                    UpdateSourceTrigger=LostFocus}"
                     FontFamily="Consolas" FontSize="12"
                     Background="#16162A" Foreground="#E8E8FF"
                     BorderBrush="#A78BFA" BorderThickness="1.5"
                     Padding="8,4"
                     LostFocus="TarifaHora_LostFocus"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>

</DataGridTemplateColumn>
```

Agregar también un tooltip instructivo encima del DataGrid:

```xml
<!-- Agregar justo antes del DataGrid de sueldos -->
<Border Background="#1A1840" CornerRadius="8" Padding="12,8"
        Margin="0,0,0,12" BorderBrush="#252540" BorderThickness="1">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="💡" FontSize="14" Margin="0,0,8,0"/>
        <TextBlock Text="Hacé doble click en la columna TARIFA/H para editar el valor de cada instructor. El sueldo se recalcula automáticamente."
                   FontSize="11" Foreground="#6A6A9A" TextWrapping="Wrap"/>
    </StackPanel>
</Border>
```

---

### PASO 10 — `ReportesPage.xaml.cs` — evento LostFocus y refresh

Ubicación: `SistemaGimnacionOptimusCAI/Paginas/ReportesPage.xaml.cs`

Agregar estos métodos al code-behind de la página:

```csharp
// ── EDICIÓN INLINE DE TARIFA POR HORA ────────────────────────────────

/// <summary>
/// Se dispara cuando el TextBox de tarifa pierde el foco.
/// Valida, guarda y recarga el reporte.
/// </summary>
private void TarifaHora_LostFocus(object sender, RoutedEventArgs e)
{
    var txt = sender as TextBox;
    if (txt == null) return;

    // Obtener el docente del contexto de la fila
    var row    = FindParent<DataGridRow>(txt);
    var docente = row?.DataContext as ResumenDocente;
    if (docente == null) return;

    // Parsear el valor ingresado
    string textoLimpio = (txt.Text ?? string.Empty)
                            .Replace("$", "")
                            .Replace(".", "")
                            .Replace(",", ".")
                            .Trim();

    decimal nuevaTarifa;
    if (!decimal.TryParse(textoLimpio,
                           System.Globalization.NumberStyles.Any,
                           System.Globalization.CultureInfo.InvariantCulture,
                           out nuevaTarifa) || nuevaTarifa < 0)
    {
        NotificacionWindow.MostrarError(
            "Ingresá un número válido y positivo para la tarifa.");
        txt.Text = docente.TarifaHora.ToString("N2");
        return;
    }

    // Si no cambió el valor, no hacer nada
    if (nuevaTarifa == docente.TarifaHora) return;

    try
    {
        var resultado = _controller.ActualizarTarifaInstructor(
            docente.InstructorId, nuevaTarifa);

        if (resultado.ok)
        {
            // Actualizar el objeto en memoria para que el binding refleje el cambio
            docente.TarifaHora     = nuevaTarifa;
            docente.SueldoEstimado = docente.HorasTotales * nuevaTarifa;

            // Refrescar el total a pagar en el panel de resumen
            ActualizarTotalSueldos();

            NotificacionWindow.MostrarExito(resultado.mensaje);
        }
        else
        {
            NotificacionWindow.MostrarError(resultado.mensaje);
            txt.Text = docente.TarifaHora.ToString("N2");
        }
    }
    catch (Exception ex)
    {
        NotificacionWindow.MostrarError("Error al guardar la tarifa.\n" + ex.Message);
        txt.Text = docente.TarifaHora.ToString("N2");
    }
}

/// <summary>
/// Recalcula y muestra el total de sueldos en el panel de resumen.
/// </summary>
private void ActualizarTotalSueldos()
{
    if (_docentesActuales == null) return;

    decimal total = 0;
    foreach (var d in _docentesActuales)
        total += d.SueldoEstimado;

    // lblTotalSueldos es el TextBlock del panel de resumen
    // Ajustar el nombre si es distinto en tu implementación
    if (lblTotalSueldos != null)
        lblTotalSueldos.Text = "$" + total.ToString("N2");
}

/// <summary>
/// Helper para encontrar el elemento padre de un tipo dado en el árbol visual.
/// Necesario para obtener el DataGridRow desde el TextBox editado.
/// </summary>
private static T FindParent<T>(DependencyObject child)
    where T : DependencyObject
{
    DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
    while (parent != null)
    {
        if (parent is T resultado) return resultado;
        parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
    }
    return null;
}
```

Agregar también el campo privado para la lista de docentes en la clase:

```csharp
// Agregar como campo privado de la clase ReportesPage:
private List<ResumenDocente> _docentesActuales = new List<ResumenDocente>();
```

Y asegurarse de que cuando se carga el tab de sueldos se guarda en ese campo:

```csharp
// En el método que carga los sueldos (buscar el método existente y agregar):
private void CargarSueldosDocentes()
{
    try
    {
        _docentesActuales = _controller.ObtenerSueldosDocentes(
            dpSueldosDesde.SelectedDate, dpSueldosHasta.SelectedDate);

        gridSueldos.ItemsSource = _docentesActuales;
        ActualizarTotalSueldos();
    }
    catch (Exception ex)
    {
        NotificacionWindow.MostrarError(ex.Message);
    }
}
```

---

## 6. CHECKLIST DE VERIFICACIÓN

Después de implementar todo, verificar estos casos en orden:

### ✅ Bug 1 — Balance
```
1. Abrir Reportes → tab "Ingresos y Ganancias"
2. Seleccionar fechas que incluyan movimientos conocidos
3. Click "APLICAR FILTROS"
4. Verificar:
   □ "INGRESOS TOTALES" muestra un valor > $0
   □ "BALANCE" muestra valor positivo (o correcto)
   □ Columna "CONCEPTO" no tiene filas vacías
   □ El DataGrid muestra los movimientos con concepto descriptivo
```

### ✅ Bug 2 — Tarifa docentes
```
1. Abrir Reportes → tab "Sueldos Docentes"
2. Ver el banner azul con instrucciones de doble click
3. Hacer doble click en la celda TARIFA/H de un instructor
4. Ingresar un valor (ej: 1500)
5. Presionar Tab o hacer click afuera
6. Verificar:
   □ La celda vuelve a modo lectura con el nuevo valor "$1.500,00"
   □ La columna "SUELDO ESTIMADO" se actualiza con Horas × Tarifa
   □ El panel "TOTAL A PAGAR" se actualiza
   □ Aparece notificación verde "Tarifa actualizada correctamente"
7. Recargar la página y verificar que el valor persiste (está guardado en BD)
```

### ✅ Verificación final en SQL
```sql
-- Confirmar que los valores se guardaron:
SELECT id, nombre, apellido, tarifa_hora
FROM usuarios
WHERE rol_id = 2
ORDER BY apellido;

-- Confirmar que el SP calcula bien:
EXEC sp_ReporteSueldosDocentes 
    @FechaDesde = '2026-05-01', 
    @FechaHasta = '2026-05-31';
```

---

## 7. ERRORES COMUNES Y SUS SOLUCIONES

| Error | Causa | Solución |
|-------|-------|----------|
| `"La columna 'tarifa_hora' no existe"` | No se ejecutó el ALTER TABLE | Ejecutar PASO 2 |
| `"sp_ActualizarTarifaInstructor no encontrado"` | No se ejecutó el SP | Ejecutar PASO 3 |
| `Balance sigue negativo` | El valor en `tipo` es distinto a `'ingreso'` | Ejecutar query diagnóstico del PASO 1 y ver el valor real, ajustar el SP |
| `FindParent no existe en WPF` | Import faltante | Agregar `using System.Windows;` en el code-behind |
| `ResumenDocente no tiene setter en TarifaHora` | La entity tiene solo getter | Cambiar a `{ get; set; }` en `Entities/ReporteFinanciero.cs` |
| `lblTotalSueldos no encontrado` | El nombre del TextBlock es distinto | Buscar en el XAML el TextBlock del total y usar su nombre real |

---

## 8. NOTA PARA EL FUTURO

Una vez que el bug del balance esté corregido, si los ingresos siguen en $0 después de aplicar filtros, correr este query para verificar que hay datos en el período seleccionado:

```sql
-- Ver todos los movimientos del mes actual:
SELECT id, tipo, concepto, monto, metodo_pago, creado_en
FROM caja_movimientos
WHERE CAST(creado_en AS DATE) >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
ORDER BY creado_en DESC;
```

Si la tabla está vacía, los ingresos de membresías no se están generando en `caja_movimientos` al crear membresías. En ese caso revisar `sp_InsertarMembresia` y confirmar que hace el INSERT en `caja_movimientos` dentro de la misma transacción.

---

*SDD Fix Reportes — OptimusCAI Gym v1.0 — Mayo 2026*  
*Bugs detectados en pantalla: balance negativo + sueldo docentes en $0*
