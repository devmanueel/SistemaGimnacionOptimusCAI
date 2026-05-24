# INSTRUCCIONES PARA CLAUDE CODE — OptimusCAI Gym v1.2
> Leer este archivo COMPLETO antes de escribir cualquier línea de código.
> Todas las decisiones de diseño ya están tomadas. No inventar, no asumir, implementar exactamente lo especificado.

---

## REGLAS ABSOLUTAS (nunca violarlas)

```
- C# 7.3 estricto. Sin switch expressions, sin using simplificado, sin nullable reference types
- SQL Server LocalDB: siempre DROP + CREATE en los SPs (nunca CREATE OR ALTER)
- Sin SQL inline en los DAOs. Solo Stored Procedures
- Sin LetterSpacing en XAML. Sin DropShadowEffect dentro de Triggers
- Tag XAML siempre es string → Convert.ToInt32(item.Tag) en code-behind
- SesionManager.UsuarioId en lugar de cualquier ID hardcodeado
- Auditor.Registrar() en todos los métodos de escritura de Controllers
- Auditor.Registrar() falla silenciosamente — nunca puede romper la operación principal
```

---

## CONTEXTO DEL PROYECTO

**Stack:** C# 7.3 + .NET Framework + WPF + XAML + SQL Server LocalDB (`DB_CAI_Optimus.mdf`)

**4 proyectos en la solución:**
- `Entities/` — POCOs sin lógica
- `Models/DAO/` — acceso a datos solo via SPs
- `Controllers/` — lógica de negocio + validaciones
- `SistemaGimnacionOptimusCAI/` — UI WPF (Pages, Windows, Helpers)

**Roles del sistema:**
- `admin` (rol_id=1) — acceso total
- `empleado` (rol_id=2) — acceso operativo limitado

**SesionManager (static):** `UsuarioId`, `NombreCompleto`, `RolNombre`, `EsAdmin`, `HaySesion`, `Iniciar()`, `Cerrar()`

---

## MÓDULOS A IMPLEMENTAR — PRIORIDAD ORDENADA

---

### MÓDULO 1 — FIX CRÍTICO: Validación membresía única por socio

**Regla de negocio:** Un socio puede tener MÚLTIPLES membresías (una por cada actividad diferente), pero NO puede tener DOS membresías de la MISMA actividad activas al mismo tiempo.

#### SP — Fix en `sp_InsertarMembresia`

Agregar ANTES del INSERT:
```sql
-- Validar que no existe membresía activa de la misma actividad para este socio
IF EXISTS (
    SELECT 1 FROM membresias
    WHERE socio_id     = @SocioId
      AND actividad_id = @ActividadId
      AND estado       = 'activa'
)
BEGIN
    RAISERROR('Este socio ya tiene una membresía activa de esta actividad. Debe renovarla o darla de baja primero.', 16, 1);
    RETURN;
END
```

#### Controller — Fix en `MembresiaController.Insertar()`

```csharp
// El SP ya valida, pero el Controller debe capturar el mensaje y devolverlo limpio:
catch (Exception ex)
{
    if (ex.Message.Contains("ya tiene una membresía activa"))
        return (false, ex.Message, 0);
    return (false, "Error al crear la membresía.\n" + ex.Message, 0);
}
```

---

### MÓDULO 2 — FIX CRÍTICO: Planes con vencimiento automático

**Regla de negocio:** El sistema calcula automáticamente la fecha de vencimiento según el plan. El usuario solo elige el plan y la fecha de inicio (que por defecto es hoy).

#### Planes disponibles y sus días:

| Plan | Días |
|------|------|
| Clase suelta | 1 |
| Quincenal | 15 |
| Mensual | 31 |
| Trimestral | 90 |
| Semestral | 180 |
| Anual | 365 |

#### SP — Fix en `sp_InsertarMembresia`

```sql
CREATE PROCEDURE sp_InsertarMembresia
    @SocioId      BIGINT,
    @ActividadId  BIGINT,
    @TipoPlan     VARCHAR(20),   -- 'clase_suelta' | 'quincenal' | 'mensual' | 'trimestral' | 'semestral' | 'anual'
    @FechaInicio  DATE = NULL,   -- NULL = hoy
    @MetodoPago   VARCHAR(30),
    @Monto        DECIMAL(10,2),
    @Observaciones NVARCHAR(300) = NULL,
    @RegistradoPor BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaInicio IS NULL SET @FechaInicio = CAST(GETDATE() AS DATE);

    -- Calcular vencimiento automático según plan
    DECLARE @DiasVigencia INT;
    SET @DiasVigencia = CASE @TipoPlan
        WHEN 'clase_suelta'  THEN 1
        WHEN 'quincenal'     THEN 15
        WHEN 'mensual'       THEN 31
        WHEN 'trimestral'    THEN 90
        WHEN 'semestral'     THEN 180
        WHEN 'anual'         THEN 365
        ELSE 31
    END;

    DECLARE @FechaVencimiento DATE = DATEADD(DAY, @DiasVigencia - 1, @FechaInicio);

    -- Validar membresía activa duplicada
    IF EXISTS (
        SELECT 1 FROM membresias
        WHERE socio_id     = @SocioId
          AND actividad_id = @ActividadId
          AND estado       = 'activa'
    )
    BEGIN
        RAISERROR('Este socio ya tiene una membresía activa de esta actividad.', 16, 1);
        RETURN;
    END

    -- Insertar membresía
    INSERT INTO membresias
        (socio_id, actividad_id, tipo_plan, fecha_inicio, fecha_vencimiento,
         metodo_pago, monto, estado, observaciones, registrado_por)
    VALUES
        (@SocioId, @ActividadId, @TipoPlan, @FechaInicio, @FechaVencimiento,
         @MetodoPago, @Monto, 'activa', @Observaciones, @RegistradoPor);

    DECLARE @NuevoId BIGINT = SCOPE_IDENTITY();

    -- Generar movimiento automático en caja
    INSERT INTO caja_movimientos
        (tipo, concepto, monto, metodo_pago, referencia_id, referencia_tipo, registrado_por)
    VALUES
        ('ingreso', 'Membresía ' + @TipoPlan + ' - Socio #' + CAST(@SocioId AS VARCHAR),
         @Monto, @MetodoPago, @NuevoId, 'membresia', @RegistradoPor);

    -- Registrar en historial
    INSERT INTO membresia_historial
        (membresia_id, tipo_evento, fecha_desde, fecha_hasta, importe, metodo_pago, registrado_por)
    VALUES
        (@NuevoId, 'alta', @FechaInicio, @FechaVencimiento, @Monto, @MetodoPago, @RegistradoPor);

    SELECT @NuevoId AS id, @FechaVencimiento AS fecha_vencimiento;
END;
GO
```

#### Columnas a verificar/agregar en tabla `membresias`:
```sql
-- Verificar y agregar si no existen:
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('membresias') AND name='tipo_plan')
    ALTER TABLE membresias ADD tipo_plan VARCHAR(20) NOT NULL DEFAULT 'mensual';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('membresias') AND name='registrado_por')
    ALTER TABLE membresias ADD registrado_por BIGINT NULL REFERENCES usuarios(id);
```

#### Entity — Agregar a `Membresia.cs`:
```csharp
public string TipoPlan       { get; set; }
public string TipoPlanTexto
{
    get
    {
        switch (TipoPlan)
        {
            case "clase_suelta":  return "Clase suelta";
            case "quincenal":     return "Quincenal";
            case "mensual":       return "Mensual";
            case "trimestral":    return "Trimestral";
            case "semestral":     return "Semestral";
            case "anual":         return "Anual";
            default: return TipoPlan;
        }
    }
}
```

---

### MÓDULO 3 — FIX: "ID de membresía inválido" al modificar

**Causa:** `_idEditar` no se asigna al abrir el formulario de edición.

#### Fix en `MembresiasPage.xaml.cs`:
```csharp
// Buscar el método que abre el formulario en modo edición y asegurarse de que tiene:
private void AbrirParaEditar(Membresia m)
{
    _esNuevo  = false;
    _idEditar = m.Id;   // ← ESTA LÍNEA ES EL FIX — verificar que existe

    lblTituloFormulario.Text = "EDITAR MEMBRESÍA";
    // Cargar datos en los campos del formulario...
}
```

#### Fix en `MembresiaController.Modificar()`:
```csharp
public (bool ok, string mensaje) Modificar(long id, ...)
{
    if (id <= 0) return (false, "Seleccioná una membresía de la lista primero.");
    // resto del método...
}
```

---

### MÓDULO 4 — Renovar membresía (restaurar plan)

**Regla:** "Restaurar plan" = renovar la membresía vencida por el mismo período. La nueva fecha de inicio es hoy y el vencimiento se calcula igual que al dar de alta.

#### SP nuevo — `sp_RenovarMembresia`:
```sql
IF OBJECT_ID('sp_RenovarMembresia','P') IS NOT NULL DROP PROCEDURE sp_RenovarMembresia;
GO
CREATE PROCEDURE sp_RenovarMembresia
    @Id           BIGINT,
    @MetodoPago   VARCHAR(30),
    @Monto        DECIMAL(10,2),
    @RegistradoPor BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TipoPlan    VARCHAR(20);
    DECLARE @ActividadId BIGINT;
    DECLARE @SocioId     BIGINT;

    SELECT @TipoPlan = tipo_plan, @ActividadId = actividad_id, @SocioId = socio_id
    FROM membresias WHERE id = @Id;

    IF @TipoPlan IS NULL
    BEGIN
        RAISERROR('Membresía no encontrada.', 16, 1);
        RETURN;
    END

    DECLARE @Dias INT;
    SET @Dias = CASE @TipoPlan
        WHEN 'clase_suelta' THEN 1   WHEN 'quincenal'  THEN 15
        WHEN 'mensual'      THEN 31  WHEN 'trimestral' THEN 90
        WHEN 'semestral'    THEN 180 WHEN 'anual'      THEN 365
        ELSE 31
    END;

    DECLARE @Hoy      DATE = CAST(GETDATE() AS DATE);
    DECLARE @Vencim   DATE = DATEADD(DAY, @Dias - 1, @Hoy);

    UPDATE membresias SET
        fecha_inicio      = @Hoy,
        fecha_vencimiento = @Vencim,
        metodo_pago       = @MetodoPago,
        estado            = 'activa'
    WHERE id = @Id;

    -- Movimiento en caja
    INSERT INTO caja_movimientos
        (tipo, concepto, monto, metodo_pago, referencia_id, referencia_tipo, registrado_por)
    VALUES
        ('ingreso', 'Renovación membresía - Socio #' + CAST(@SocioId AS VARCHAR),
         @Monto, @MetodoPago, @Id, 'membresia', @RegistradoPor);

    -- Historial
    INSERT INTO membresia_historial
        (membresia_id, tipo_evento, fecha_desde, fecha_hasta, importe, metodo_pago, registrado_por)
    VALUES
        (@Id, 'renovacion', @Hoy, @Vencim, @Monto, @MetodoPago, @RegistradoPor);

    SELECT @Id AS id, @Vencim AS nueva_fecha_vencimiento;
END;
GO
```

---

### MÓDULO 5 — Dar de baja membresía (empleado)

**Regla:** El empleado puede dar de baja la membresía activa de un socio. El socio queda activo en el sistema pero sin membresía activa. El empleado NO puede eliminar al socio.

#### Permisos en la UI:
- Botón "DAR DE BAJA MEMBRESÍA" — visible para admin Y empleado
- Botón "ELIMINAR SOCIO" — visible SOLO para admin
- En `MembresiasPage.xaml.cs`:

```csharp
btnEliminarSocio.Visibility = SesionManager.EsAdmin
    ? Visibility.Visible : Visibility.Collapsed;
btnDarDeBajaMembresia.Visibility = Visibility.Visible; // todos
```

---

### MÓDULO 6 — SOCIOS: campo celular obligatorio

**Regla:** El campo `telefono` (celular) es OBLIGATORIO al crear o modificar un socio.

#### Fix en tabla `socios`:
```sql
-- Solo si la columna admite NULL actualmente:
ALTER TABLE socios ALTER COLUMN telefono VARCHAR(20) NOT NULL;
```

#### Fix en `SocioController.Validar()`:
```csharp
if (string.IsNullOrWhiteSpace(telefono))
    return "El número de celular es obligatorio.";
if (telefono.Trim().Length < 8)
    return "El número de celular es inválido.";
```

#### Fix en `SociosPage.xaml`:
Cambiar la etiqueta del campo:
```xml
<TextBlock Text="CELULAR *" .../>  <!-- era "TELÉFONO (opcional)" -->
```

---

### MÓDULO 7 — SOCIOS: registrar quién dio de alta

#### Fix en tabla `socios`:
```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('socios') AND name='registrado_por')
    ALTER TABLE socios ADD registrado_por BIGINT NULL REFERENCES usuarios(id);
```

#### Fix en `sp_InsertarSocio`:
```sql
-- Agregar parámetro y columna al INSERT:
@RegistradoPor BIGINT = NULL
-- En el INSERT: incluir registrado_por = @RegistradoPor
```

#### Fix en `SocioController.Insertar()`:
```csharp
long regPor = SesionManager.HaySesion ? SesionManager.UsuarioId : 1;
// Pasar regPor al DAO
```

---

### MÓDULO 8 — SOCIOS: socios inactivos +2 meses

**Pantalla:** Sección dentro de `SociosPage` (tab o panel separado).  
**Función:** Lista socios activos sin asistencia hace más de 2 meses y permite darlos de baja con un click.

#### SP — `sp_SociosParaDarDeBaja`:
```sql
IF OBJECT_ID('sp_SociosParaDarDeBaja','P') IS NOT NULL DROP PROCEDURE sp_SociosParaDarDeBaja;
GO
CREATE PROCEDURE sp_SociosParaDarDeBaja
    @MesesSinActividad INT = 2
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Limite DATE = DATEADD(MONTH, -@MesesSinActividad, CAST(GETDATE() AS DATE));

    SELECT
        s.id, s.nombre, s.apellido, s.dni, s.numero_socio, s.foto,
        MAX(a.fecha_hora)                               AS ultima_asistencia,
        DATEDIFF(DAY, MAX(a.fecha_hora), GETDATE())     AS dias_inactivo
    FROM socios s
    LEFT JOIN asistencias a ON a.socio_id = s.id
    WHERE s.activo = 1 AND s.eliminado_en IS NULL
    GROUP BY s.id, s.nombre, s.apellido, s.dni, s.numero_socio, s.foto
    HAVING MAX(a.fecha_hora) < @Limite OR MAX(a.fecha_hora) IS NULL
    ORDER BY dias_inactivo DESC;
END;
GO
```

#### SP — `sp_DarDeBajaSociosBatch`:
```sql
IF OBJECT_ID('sp_DarDeBajaSociosBatch','P') IS NOT NULL DROP PROCEDURE sp_DarDeBajaSociosBatch;
GO
CREATE PROCEDURE sp_DarDeBajaSociosBatch
    @Ids NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    CREATE TABLE #tmp (id BIGINT);
    DECLARE @pos INT=1, @next INT, @val NVARCHAR(20);
    SET @Ids = LTRIM(RTRIM(@Ids)) + ',';
    WHILE @pos <= LEN(@Ids)
    BEGIN
        SET @next = CHARINDEX(',', @Ids, @pos);
        IF @next = 0 BREAK;
        SET @val = LTRIM(RTRIM(SUBSTRING(@Ids, @pos, @next - @pos)));
        IF ISNUMERIC(@val) = 1 INSERT INTO #tmp VALUES(CAST(@val AS BIGINT));
        SET @pos = @next + 1;
    END
    UPDATE socios SET activo = 0 WHERE id IN (SELECT id FROM #tmp);
    SELECT @@ROWCOUNT AS afectados;
END;
GO
```

---

### MÓDULO 9 — BUSCADOR RÁPIDO en el MainWindow

**Comportamiento:**
- Barra de búsqueda siempre visible en la barra superior del MainWindow (a la izquierda del título de página)
- Busca por nombre, apellido o DNI mientras el usuario escribe
- Al presionar Enter o seleccionar un resultado → abre la FichaSocioWindow (modal)
- El buscador NO reemplaza al módulo Socios — son independientes

#### En `MainWindow.xaml` — agregar en la barra superior:
```xml
<Border Grid.Column="0" Background="#16162A" CornerRadius="8"
        BorderBrush="#252540" BorderThickness="1"
        Width="320" Height="32" Margin="20,0,0,0"
        VerticalAlignment="Center">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="36"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <TextBlock Grid.Column="0" Text="🔍" FontSize="13"
                   HorizontalAlignment="Center" VerticalAlignment="Center"/>
        <TextBox x:Name="txtBuscadorGlobal" Grid.Column="1"
                 BorderThickness="0" Background="Transparent"
                 Foreground="#E8E8FF" CaretBrush="#00CFFF"
                 FontSize="12" VerticalContentAlignment="Center"
                 KeyDown="txtBuscadorGlobal_KeyDown"
                 TextChanged="txtBuscadorGlobal_TextChanged"/>
    </Grid>
</Border>
```

#### `FichaSocioWindow.xaml` — nueva Window (modal)

Es una `Window` con `WindowStyle="None"`, `ShowInTaskbar="False"`, `Owner` = MainWindow.  
Tamaño: 900×640 centrada sobre el MainWindow.

**Secciones de la ficha:**
1. **Header** — foto circular, nombre completo, número socio, badge estado, celular
2. **Tab Membresías** — lista de membresías con estado (activa/vencida), botón "RENOVAR" por cada una, botón "DAR DE BAJA" (empleado y admin)
3. **Tab Historial de pagos** — DataGrid con fecha, concepto, monto, método de pago
4. **Tab Asistencias** — DataGrid con fecha y hora, filtro por mes
5. **Botón EDITAR** (esquina superior derecha) — navega al módulo Socios con ese socio preseleccionado. Solo para admin.
6. **Botón X** — cierra la modal y vuelve al menú

---

### MÓDULO 10 — CARNET PDF imprimible

**Datos del carnet:**
- Nombre y apellido
- Foto (circular)
- Número de socio (#XXXX)
- Actividad / plan actual
- Fecha de vencimiento de membresía
- QR con el DNI (para escanear en entrada)
- Logo OptimusCAI + nombre del gimnasio

**Formato:** A6 horizontal (148×105mm) — tamaño carnet estándar.

**Librería:** `iTextSharp` (ya disponible en NuGet para .NET Framework).  
Referencia NuGet: `iTextSharp` versión 5.5.13.3

#### Estructura del botón en `FichaSocioWindow`:
```xml
<Button Content="🪪 GENERAR CARNET"
        Click="btnGenerarCarnet_Click"
        .../>
```

#### Lógica en `FichaSocioWindow.xaml.cs`:
```csharp
private void btnGenerarCarnet_Click(object sender, RoutedEventArgs e)
{
    var gen = new CarnetGenerator();
    string path = gen.GenerarCarnet(_socio, _membresiaActiva);
    if (path != null)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            { UseShellExecute = true });
    }
}
```

#### `Helpers/CarnetGenerator.cs` — clase nueva:
Implementar con iTextSharp:
- Crear PDF tamaño A6 landscape
- Fondo degradado oscuro (color primario del sistema)
- Foto circular del socio (convertir bytes → Image iTextSharp)
- QR generado con `ZXing.Net` (NuGet: `ZXing.Net` 0.16.9) con el DNI como contenido
- Guardar en `Path.GetTempPath()` con nombre `Carnet_NroSocio_XXXX.pdf`
- Retornar la ruta del archivo generado

---

### MÓDULO 11 — PLANILLA MÉDICA

**Campos médicos completos a incluir:**

```sql
-- Tabla nueva: socios_ficha_medica
IF OBJECT_ID('socios_ficha_medica') IS NULL
CREATE TABLE socios_ficha_medica (
    id                    BIGINT IDENTITY(1,1) PRIMARY KEY,
    socio_id              BIGINT NOT NULL REFERENCES socios(id),
    peso_kg               DECIMAL(5,2) NULL,
    altura_cm             SMALLINT NULL,
    grupo_sanguineo       VARCHAR(5) NULL,
    enfermedades          NVARCHAR(500) NULL,
    medicamentos          NVARCHAR(500) NULL,
    restricciones_fisicas NVARCHAR(500) NULL,
    contacto_emergencia   VARCHAR(150) NULL,
    telefono_emergencia   VARCHAR(20) NULL,
    apto_fisico           BIT NOT NULL DEFAULT 0,
    fecha_apto            DATE NULL,
    observaciones         NVARCHAR(300) NULL,
    actualizado_en        DATETIME NOT NULL DEFAULT GETDATE(),
    actualizado_por       BIGINT NULL REFERENCES usuarios(id)
);
```

**SPs necesarios:**
- `sp_ObtenerFichaMedica @SocioId`
- `sp_GuardarFichaMedica` (INSERT si no existe, UPDATE si existe)

**UI:** Botón "📋 FICHA MÉDICA" en la `FichaSocioWindow` abre un formulario dentro de la misma modal o en un panel deslizable.

---

### MÓDULO 12 — ACTIVIDADES: contador de socios y permisos

**Reglas:**
- Solo admin puede crear/modificar actividades (ya implementado en el menú, verificar en el módulo)
- La lista de actividades debe mostrar cuántos socios activos tiene cada actividad

#### Fix en `sp_ObtenerActividades`:
```sql
-- Agregar al SELECT:
(SELECT COUNT(DISTINCT m.socio_id)
 FROM membresias m
 WHERE m.actividad_id = a.id
   AND m.estado = 'activa') AS socios_activos
```

#### Entity — Agregar a `Actividad.cs`:
```csharp
public int SociosActivos { get; set; }
public string SociosActivosTexto => SociosActivos == 1 ? "1 socio" : SociosActivos + " socios";
```

#### En `ActividadesPage.xaml` — agregar columna en el DataGrid:
```xml
<DataGridTextColumn Header="SOCIOS ACTIVOS"
                    Binding="{Binding SociosActivosTexto}"
                    Width="140"/>
```

---

### MÓDULO 13 — CAJA: mejoras completas

#### Permisos:
```csharp
// En CajaPage.xaml.cs, al cargar:
panelGanancias.Visibility = SesionManager.EsAdmin
    ? Visibility.Visible : Visibility.Collapsed;
```

#### Tipos de movimiento — nueva clasificación:

| Tipo | Subtipo | Quién puede |
|------|---------|-------------|
| Ingreso | Membresía (automático) | Sistema |
| Ingreso | Venta kiosco (automático) | Sistema |
| Ingreso | Cobro único (manual) | Admin y Empleado |
| Egreso interno | Depósito | Solo Admin |
| Egreso interno | Retiro | Solo Admin |
| Egreso interno | Guardar | Solo Admin |
| Egreso externo | Gasto / Compra | Solo Admin |

#### Columnas a verificar/agregar en `caja_movimientos`:
```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('caja_movimientos') AND name='subtipo')
    ALTER TABLE caja_movimientos ADD subtipo VARCHAR(30) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('caja_movimientos') AND name='nombre_externo')
    ALTER TABLE caja_movimientos ADD nombre_externo VARCHAR(150) NULL;
```

#### SP nuevo — `sp_FiltrarCaja`:
```sql
IF OBJECT_ID('sp_FiltrarCaja','P') IS NOT NULL DROP PROCEDURE sp_FiltrarCaja;
GO
CREATE PROCEDURE sp_FiltrarCaja
    @FechaDesde  DATE    = NULL,
    @FechaHasta  DATE    = NULL,
    @Tipo        VARCHAR(20) = NULL,
    @Subtipo     VARCHAR(30) = NULL,
    @MetodoPago  VARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY,-30,CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        cm.id, cm.tipo, cm.subtipo, cm.concepto, cm.monto, cm.metodo_pago,
        cm.referencia_id, cm.referencia_tipo, cm.nombre_externo,
        cm.registrado_por, cm.creado_en,
        ISNULL(u.nombre+' '+u.apellido,'Sistema') AS registrado_por_nombre,
        SUM(CASE WHEN cm.tipo='ingreso' THEN cm.monto ELSE 0 END) OVER() AS total_ingresos,
        SUM(CASE WHEN cm.tipo='egreso'  THEN cm.monto ELSE 0 END) OVER() AS total_egresos
    FROM caja_movimientos cm
    LEFT JOIN usuarios u ON u.id = cm.registrado_por
    WHERE CAST(cm.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND (@Tipo      IS NULL OR cm.tipo      = @Tipo)
      AND (@Subtipo   IS NULL OR cm.subtipo   = @Subtipo)
      AND (@MetodoPago IS NULL OR cm.metodo_pago = @MetodoPago)
    ORDER BY cm.creado_en DESC;
END;
GO
```

#### Exportar PDF de caja — `Helpers/CajaExportador.cs`:

Usar `iTextSharp`. Generar tabla con columnas: Fecha, Concepto, Tipo, Monto, Método de pago. Footer con totales: Total ingresos, Total egresos, Balance. Guardar en temp y abrir con `Process.Start`.

#### En `CajaPage.xaml` — agregar:
- DatePicker Desde / DatePicker Hasta
- ComboBox filtro por tipo (Todos / Membresía / Venta / Cobro único / Gasto / Interno)
- ComboBox filtro por método de pago (Todos / Efectivo / Tarjeta / Transferencia)
- Botón "📄 EXPORTAR PDF" (solo visible para admin)
- Panel de totales: Total ingresos del período / Total egresos / Balance

#### Modal "Cobro único" (movimiento externo):
Campos: Nombre del visitante (opcional), concepto (ej: "Clase suelta visita"), monto, método de pago.

---

### MÓDULO 14 — ASISTENCIA INSTRUCTORES: mejoras

#### Filtros requeridos en la UI:
- ComboBox instructor (todos / uno específico)
- DatePicker desde / hasta
- Al aplicar filtros → recalcular:
  - Total horas trabajadas en el período
  - Total días asistidos en el período
  - Sueldo estimado (horas × tarifa/hora)

#### SP — `sp_ResumenInstructor`:
```sql
IF OBJECT_ID('sp_ResumenInstructor','P') IS NOT NULL DROP PROCEDURE sp_ResumenInstructor;
GO
CREATE PROCEDURE sp_ResumenInstructor
    @InstructorId BIGINT = NULL,
    @FechaDesde   DATE   = NULL,
    @FechaHasta   DATE   = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FechaDesde IS NULL SET @FechaDesde = DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1);
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        u.id                                            AS instructor_id,
        u.nombre+' '+u.apellido                         AS nombre_completo,
        ISNULL(u.tarifa_hora, 0)                        AS tarifa_hora,
        COUNT(DISTINCT ia.fecha)                        AS dias_asistidos,
        ISNULL(SUM(ia.horas_trabajadas),0)              AS total_horas,
        ISNULL(SUM(ia.horas_trabajadas),0)
            * ISNULL(u.tarifa_hora,0)                   AS sueldo_estimado
    FROM usuarios u
    LEFT JOIN instructor_asistencias ia
           ON ia.instructor_id = u.id
          AND ia.fecha BETWEEN @FechaDesde AND @FechaHasta
    WHERE u.rol_id = 2 AND u.activo = 1
      AND (@InstructorId IS NULL OR u.id = @InstructorId)
    GROUP BY u.id, u.nombre, u.apellido, u.tarifa_hora
    ORDER BY u.apellido;
END;
GO
```

#### Columna `tarifa_hora` en `usuarios`:
```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('usuarios') AND name='tarifa_hora')
    ALTER TABLE usuarios ADD tarifa_hora DECIMAL(10,2) NOT NULL DEFAULT 0;
```

---

### MÓDULO 15 — USUARIOS: cambio de contraseña

Verificar que existe `sp_CambiarPasswordUsuario`. Si no existe, crearlo:

```sql
IF OBJECT_ID('sp_CambiarPasswordUsuario','P') IS NOT NULL DROP PROCEDURE sp_CambiarPasswordUsuario;
GO
CREATE PROCEDURE sp_CambiarPasswordUsuario
    @Id              BIGINT,
    @NuevoHashSHA256 VARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE usuarios SET password_hash = @NuevoHashSHA256 WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO
```

En `UsuariosPage.xaml.cs` el hash se calcula ANTES de llamar al SP:
```csharp
private string HashearSHA256(string texto)
{
    using (var sha = System.Security.Cryptography.SHA256.Create())
    {
        byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(texto));
        var sb = new System.Text.StringBuilder();
        foreach (byte b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
```

---

### MÓDULO 16 — FIXES GLOBALES PENDIENTES

#### 16.1 — USUARIO_ACTUAL_ID hardcodeado
En `MembresiasPage.xaml.cs`, `CajaPage.xaml.cs`, `VentasPage.xaml.cs`:
```csharp
// CAMBIAR:
private const long USUARIO_ACTUAL_ID = 1;
// POR:
private long USUARIO_ACTUAL_ID => SesionManager.UsuarioId;
```

#### 16.2 — columna `domicilio` en UsuarioDao
En `MapearUsuario()`, usar `LeerColumnaSegura()` para la columna `domicilio`.

#### 16.3 — Auditor.Registrar() faltante
Agregar en cada Controller en los métodos Insertar/Modificar/Eliminar:
```csharp
Auditor.Registrar("crear", "socio", id, new Dictionary<string, object> {
    { "nombre", nombre }, { "apellido", apellido }, { "dni", dni }
});
```
Ver tabla completa en `SPEC.md` sección Tarea 3.

#### 16.4 — SocioComboItem
Si no existe en `Entities/`:
```csharp
namespace Entities
{
    public class SocioComboItem
    {
        public long   Id         { get; set; }
        public string TextoCombo { get; set; }
    }
}
```

#### 16.5 — NotificacionWindow.MostrarAdvertencia
Si no existe:
```csharp
public static void MostrarAdvertencia(string mensaje)
{
    MessageBox.Show(mensaje, "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
}
```

#### 16.6 — MiDiccionario.xaml: BotonChipEstilo
Si no existe, agregar estilo para los chips de filtro (WhatsApp, Auditoría, Caja):
```xml
<Style x:Key="BotonChipEstilo" TargetType="Button">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Foreground" Value="#6A6A9A"/>
    <Setter Property="BorderBrush" Value="#252540"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="FontSize" Value="11"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Padding" Value="12,5"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="20" Padding="{TemplateBinding Padding}">
                    <ContentPresenter HorizontalAlignment="Center"
                                      VerticalAlignment="Center"/>
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

---

## ORDEN DE IMPLEMENTACIÓN RECOMENDADO

| # | Tarea | Prioridad | Tiempo est. |
|---|-------|-----------|-------------|
| 1 | Fixes globales 16.1 a 16.6 | 🔴 Crítica | 30 min |
| 2 | Fix "ID membresía inválido" (Módulo 3) | 🔴 Crítica | 20 min |
| 3 | Celular obligatorio en socios (Módulo 6) | 🔴 Crítica | 20 min |
| 4 | Validación membresía única por socio (Módulo 1) | 🔴 Crítica | 45 min |
| 5 | Planes con vencimiento automático (Módulo 2) | 🔴 Crítica | 1 h |
| 6 | Renovar membresía / restaurar plan (Módulo 4) | 🟡 Alta | 45 min |
| 7 | Dar de baja membresía por empleado (Módulo 5) | 🟡 Alta | 30 min |
| 8 | Filtros y exportar PDF en Caja (Módulo 13) | 🟡 Alta | 2 h |
| 9 | Buscador global + FichaSocioWindow (Módulo 9) | 🟡 Alta | 3 h |
| 10 | Socios inactivos +2 meses (Módulo 8) | 🟡 Alta | 1.5 h |
| 11 | Contador socios en Actividades (Módulo 12) | 🟢 Media | 30 min |
| 12 | Filtros en Asistencia Instructores (Módulo 14) | 🟢 Media | 1 h |
| 13 | Registrar quién dio de alta al socio (Módulo 7) | 🟢 Media | 45 min |
| 14 | Planilla médica (Módulo 11) | 🟢 Media | 2 h |
| 15 | Carnet PDF imprimible (Módulo 10) | 🟢 Media | 2 h |
| 16 | Cambio de contraseña usuarios (Módulo 15) | 🟢 Media | 30 min |

---

## DEPENDENCIAS ENTRE MÓDULOS

```
Módulo 2 (planes)        → prerequisito para Módulo 4 (renovar)
Módulo 1 (validación)    → prerequisito para Módulo 4 (renovar)
Módulo 9 (ficha modal)   → requiere Módulo 4 (botón renovar dentro de la ficha)
Módulo 13 (caja PDF)     → requiere iTextSharp instalado en el proyecto
Módulo 10 (carnet PDF)   → requiere iTextSharp + ZXing.Net instalados
Fix 16.4 (SocioComboItem)→ prerequisito para que compilen Rutinas y WhatsApp
```

---

## PAQUETES NUGET NECESARIOS

```
iTextSharp         → versión 5.5.13.3  (PDF: caja + carnets)
ZXing.Net          → versión 0.16.9    (QR para carnets)
```

Instalar con: `Tools → NuGet Package Manager → Package Manager Console`:
```
Install-Package iTextSharp -Version 5.5.13.3
Install-Package ZXing.Net -Version 0.16.9
```

---

*Instrucciones generadas Mayo 2026 — OptimusCAI Gym v1.2*
*Basadas en entrevista con el propietario del gimnasio*
