# PROPUESTA — Fixes y mejoras OptimusCAI
> Versión 1.1 — Mayo 2026  
> Documento técnico para Claude Code

---

## PROBLEMA 1 — Control de socios inactivos (baja automática)

**Qué se pide:** Una sección dentro de Socios que liste los alumnos sin actividad hace más de 2 meses y permita pasarlos a inactivos con un click.

### SP nuevo — `sp_SociosParaDarDeBaja`
```sql
IF OBJECT_ID('sp_SociosParaDarDeBaja', 'P') IS NOT NULL DROP PROCEDURE sp_SociosParaDarDeBaja;
GO
CREATE PROCEDURE sp_SociosParaDarDeBaja
    @MesesSinActividad INT = 2
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Limite DATE = DATEADD(MONTH, -@MesesSinActividad, CAST(GETDATE() AS DATE));

    SELECT
        s.id, s.nombre, s.apellido, s.dni, s.numero_socio, s.foto, s.activo,
        MAX(a.fecha_hora) AS ultima_asistencia,
        DATEDIFF(DAY, MAX(a.fecha_hora), GETDATE()) AS dias_inactivo
    FROM socios s
    LEFT JOIN asistencias a ON a.socio_id = s.id
    WHERE s.activo = 1
      AND s.eliminado_en IS NULL
    GROUP BY s.id, s.nombre, s.apellido, s.dni, s.numero_socio, s.foto, s.activo
    HAVING MAX(a.fecha_hora) < @Limite OR MAX(a.fecha_hora) IS NULL
    ORDER BY dias_inactivo DESC;
END;
GO
```

### SP nuevo — `sp_DarDeBajaSocios` (batch)
```sql
IF OBJECT_ID('sp_DarDeBajaSocios', 'P') IS NOT NULL DROP PROCEDURE sp_DarDeBajaSocios;
GO
CREATE PROCEDURE sp_DarDeBajaSocios
    @Ids NVARCHAR(MAX)   -- lista separada por comas: "1,5,12,33"
AS
BEGIN
    SET NOCOUNT ON;

    -- Parsear la lista en una tabla temporal
    CREATE TABLE #ids (id BIGINT);

    DECLARE @pos INT = 1, @next INT, @val NVARCHAR(20);
    SET @Ids = LTRIM(RTRIM(@Ids)) + ',';
    WHILE @pos <= LEN(@Ids)
    BEGIN
        SET @next = CHARINDEX(',', @Ids, @pos);
        IF @next = 0 BREAK;
        SET @val = LTRIM(RTRIM(SUBSTRING(@Ids, @pos, @next - @pos)));
        IF ISNUMERIC(@val) = 1 INSERT INTO #ids VALUES (CAST(@val AS BIGINT));
        SET @pos = @next + 1;
    END

    UPDATE socios SET activo = 0 WHERE id IN (SELECT id FROM #ids);
    SELECT @@ROWCOUNT AS afectados;
END;
GO
```

### Dónde agregar en SociosPage.xaml
Agregar una pestaña o botón "Inactivos" en el header que abra un panel con la lista de candidatos a dar de baja. El panel muestra: foto, nombre, número socio, última asistencia, días inactivo. Botón "DAR DE BAJA A TODOS" y checkboxes para selección individual.

---

## PROBLEMA 2 — Teléfono repetido en socios

**Qué se pide:** Permitir que el mismo número de teléfono figure en más de un socio (ej: la mamá comparte su teléfono con el hijo).

### Fix en SP `sp_InsertarSocio` y `sp_ModificarSocio`
Quitar cualquier validación de teléfono único. Si existe un `UNIQUE INDEX` en la columna `telefono` de la tabla `socios`, eliminarlo:

```sql
-- Verificar si existe y eliminarlo
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_socios_telefono'
    AND object_id = OBJECT_ID('socios')
)
    DROP INDEX UQ_socios_telefono ON socios;
```

No agregar ninguna restricción de unicidad en teléfono en ningún SP futuro.

---

## PROBLEMA 3 — Registrar quién dio de alta al socio

**Qué se pide:** Al crear un socio, guardar el usuario logueado como responsable del registro.

### Verificar columna en tabla `socios`
Si no existe la columna `registrado_por`:
```sql
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('socios') AND name = 'registrado_por'
)
    ALTER TABLE socios ADD registrado_por BIGINT NULL
        REFERENCES usuarios(id);
```

### Fix en `sp_InsertarSocio`
```sql
-- Agregar al INSERT:
CREATE PROCEDURE sp_InsertarSocio
    ...
    @RegistradoPor BIGINT = NULL,
    ...
AS
BEGIN
    INSERT INTO socios (..., registrado_por)
    VALUES (..., @RegistradoPor);
END;
```

### Fix en SocioController.Insertar()
```csharp
// Pasar el usuario logueado:
long regPor = SesionManager.HaySesion ? SesionManager.UsuarioId : 1;
_dao.InsertarSocio(nombre, apellido, dni, ..., registradoPor: regPor);
```

### Fix en SociosPage.xaml.cs
```csharp
// Ya existe USUARIO_ACTUAL_ID como property:
private long USUARIO_ACTUAL_ID => SesionManager.UsuarioId;
// Usarlo al llamar al controller.
```

### Mostrar en la UI
En la card/fila de cada socio, mostrar "Registrado por: [nombre del usuario]" obteniendo el nombre con JOIN en el SP `sp_ObtenerSocios`.

---

## PROBLEMA 4 — "ID de membresía inválido" al modificar

**Qué se pide:** Poder modificar una membresía existente (cambiar plan, método de pago) sin crear una nueva.

### Diagnóstico probable
El SP `sp_ModificarMembresia` recibe `@Id BIGINT` pero en `MembresiasPage.xaml.cs` se está pasando `0` o un valor no inicializado. 

### Fix en MembresiasPage.xaml.cs
```csharp
// ANTES (bug):
private long _idEditar = 0;  // nunca se asigna al abrir edición

// DESPUÉS (fix):
private void AbrirParaEditar(Membresia m)
{
    _idEditar = m.Id;   // ← ESTA LÍNEA FALTABA
    _esNuevo  = false;
    
    // Cargar datos del formulario:
    cmbActividad.SelectedItem = ... // buscar la actividad de m.ActividadId
    cmbMetodoPago.SelectedItem = m.MetodoPago;
    // etc.
}
```

### Validación en MembresiaController.Modificar()
```csharp
public (bool ok, string mensaje) Modificar(long id, ...)
{
    if (id <= 0) return (false, "ID de membresía inválido.");
    // ...
}
```

---

## PROBLEMA 5 — Cambiar membresía en lugar de crear nueva

**Qué se pide:** Modificar la membresía activa del socio (tipo, método de pago) en vez de crear una nueva cada vez.

### SP nuevo — `sp_ModificarMembresia`
```sql
IF OBJECT_ID('sp_ModificarMembresia', 'P') IS NOT NULL DROP PROCEDURE sp_ModificarMembresia;
GO
CREATE PROCEDURE sp_ModificarMembresia
    @Id           BIGINT,
    @ActividadId  BIGINT,
    @MetodoPago   VARCHAR(30),
    @FechaVencim  DATE         = NULL,
    @Observaciones NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM membresias WHERE id = @Id)
    BEGIN
        RAISERROR('ID de membresia invalido.', 16, 1);
        RETURN;
    END

    UPDATE membresias SET
        actividad_id   = @ActividadId,
        metodo_pago    = @MetodoPago,
        fecha_vencimiento = ISNULL(@FechaVencim, fecha_vencimiento),
        observaciones  = @Observaciones
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO
```

---

## PROBLEMA 6 — Membresía de 1 clase (dura 1 día)

**Qué se pide:** Soporte para un plan "clase suelta" que solo dura 1 día y se cobra por clase.

### Agregar en la tabla `membresias` si no existe
```sql
-- Columna tipo_plan: 'mensual' | 'semanal' | 'clase'
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('membresias') AND name = 'tipo_plan'
)
    ALTER TABLE membresias ADD tipo_plan VARCHAR(20) NOT NULL DEFAULT 'mensual';
```

### Lógica de fechas según tipo_plan
En `sp_InsertarMembresia`, calcular `fecha_vencimiento` automáticamente:
```sql
-- Dentro del SP:
DECLARE @FechaVencim DATE;
SET @FechaVencim = CASE @TipoPlan
    WHEN 'clase'   THEN CAST(GETDATE() AS DATE)           -- vence hoy
    WHEN 'semanal' THEN DATEADD(DAY, 7, CAST(GETDATE() AS DATE))
    ELSE DATEADD(MONTH, 1, CAST(GETDATE() AS DATE))       -- mensual por defecto
END;
```

---

## PROBLEMA 7 — Tabla de fechas de membresía

**Qué se pide:** Una tabla que registre el historial de fechas de cada membresía (renovaciones).

### Crear tabla `membresia_historial`
```sql
IF OBJECT_ID('membresia_historial') IS NULL
CREATE TABLE membresia_historial (
    id            BIGINT IDENTITY(1,1) PRIMARY KEY,
    membresia_id  BIGINT        NOT NULL REFERENCES membresias(id),
    tipo_evento   VARCHAR(30)   NOT NULL,  -- 'alta' | 'renovacion' | 'modificacion' | 'anulacion'
    fecha_desde   DATE          NOT NULL,
    fecha_hasta   DATE          NOT NULL,
    importe       DECIMAL(10,2) NULL,
    metodo_pago   VARCHAR(30)   NULL,
    registrado_por BIGINT       NULL REFERENCES usuarios(id),
    creado_en     DATETIME      NOT NULL DEFAULT GETDATE()
);
```

### SP para insertar en historial
```sql
IF OBJECT_ID('sp_InsertarHistorialMembresia', 'P') IS NOT NULL 
    DROP PROCEDURE sp_InsertarHistorialMembresia;
GO
CREATE PROCEDURE sp_InsertarHistorialMembresia
    @MembresiaId  BIGINT,
    @TipoEvento   VARCHAR(30),
    @FechaDesde   DATE,
    @FechaHasta   DATE,
    @Importe      DECIMAL(10,2) = NULL,
    @MetodoPago   VARCHAR(30)   = NULL,
    @RegistradoPor BIGINT       = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO membresia_historial
        (membresia_id, tipo_evento, fecha_desde, fecha_hasta,
         importe, metodo_pago, registrado_por)
    VALUES
        (@MembresiaId, @TipoEvento, @FechaDesde, @FechaHasta,
         @Importe, @MetodoPago, @RegistradoPor);
    SELECT SCOPE_IDENTITY() AS id;
END;
GO
```

Llamar a este SP siempre que se crea o modifica una membresía.

---

## PROBLEMA 8 — Descuento de días (regla del diagrama de flujo)

**Regla de negocio crítica:** Si el socio marca asistencia N veces en el mismo día, solo se descuenta 1 día de su membresía. Si ya marcó hoy, avisarle en lugar de volver a descontar.

### Fix en `sp_RegistrarAsistencia`
```sql
-- ANTES de descontar, verificar si ya marcó HOY:
IF EXISTS (
    SELECT 1 FROM asistencias
    WHERE socio_id = @SocioId
      AND CAST(fecha_hora AS DATE) = CAST(GETDATE() AS DATE)
)
BEGIN
    -- Ya marcó hoy → registrar la asistencia pero NO descontar
    INSERT INTO asistencias (socio_id, tipo_entrada, observaciones)
    VALUES (@SocioId, @TipoEntrada, 'Ya marcó hoy — sin descuento adicional');
    
    SELECT 0 AS descuento_aplicado, 'Ya marcaste hoy. No se descontó un día.' AS mensaje;
    RETURN;
END

-- Primera marca del día → descontar
-- (lógica existente de descuento)
SELECT 1 AS descuento_aplicado, 'Bienvenido!' AS mensaje;
```

### Fix en AsistenciasPage.xaml.cs
```csharp
// Leer el campo descuento_aplicado del resultado:
if (!resultado.DescuentoAplicado)
{
    NotificacionWindow.MostrarAdvertencia(resultado.Mensaje);
    // No bloquear el ingreso — solo avisar
}
else
{
    NotificacionWindow.MostrarExito("Bienvenido " + socio.NombreCompleto);
}
```

---

## ORDEN DE IMPLEMENTACIÓN SUGERIDO

| Prioridad | Problema | Estimación |
|-----------|----------|------------|
| 🔴 1 | Fix "ID de membresía inválido" (Problema 4) | 30 min |
| 🔴 2 | Descuento único por día (Problema 8) | 1 h |
| 🔴 3 | Registrar quién dio de alta al socio (Problema 3) | 45 min |
| 🟡 4 | Modificar membresía (Problema 5) | 1 h |
| 🟡 5 | Membresía de 1 clase (Problema 6) | 45 min |
| 🟡 6 | Control de socios inactivos (Problema 1) | 2 h |
| 🟢 7 | Teléfono repetido (Problema 2) | 15 min |
| 🟢 8 | Tabla historial membresías (Problema 7) | 1 h |

---

*Propuesta generada Mayo 2026 — Sistema OptimusCAI Gym v1.1*
