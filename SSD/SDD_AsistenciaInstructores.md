# SDD — Módulo Asistencia de Instructores
> Spec-Driven Development — Requisitos relevados en entrevista con el dueño  
> Versión 1.0 — Mayo 2026  
> Para ser leído por Claude Code antes de escribir cualquier archivo

---

## 1. CONDICIONES — Reglas no negociables (heredadas del SPEC.md global)

- C# 7.3 estricto. Sin features de C# 8+.
- SQL Server LocalDB. Patrón DROP + CREATE en todos los SPs.
- Sin SQL inline en los DAOs. Todo via Stored Procedures.
- Sin `LetterSpacing` ni `DropShadowEffect` en Triggers en XAML.
- `Auditor.Registrar()` en todos los métodos de escritura del Controller.
- `SesionManager.UsuarioId` en lugar de cualquier ID hardcodeado.

---

## 2. ESPECIFICACIÓN — Qué construir y para quién

### Contexto del negocio
Los instructores llegan al gimnasio en días y horarios variables (no fijos). 
Cada instructor dicta una sola actividad. El gimnasio tiene entre 4 y 8 instructores.
El pago se calcula directamente de las horas trabajadas registradas en el sistema.

### Quién usa este módulo
| Actor | Qué puede hacer |
|-------|----------------|
| **Instructor** | Solo ficha: registra su entrada y salida con huella dactilar o contraseña |
| **Admin** | Ve TODO: historial completo, reporte semanal/mensual, calcula sueldo, corrige fichajes erróneos |
| **Empleado** | No tiene acceso a este módulo |

### Reglas de negocio relevadas en entrevista

| # | Regla | Origen |
|---|-------|--------|
| RN-01 | El instructor se autentica igual que los socios: huella dactilar o contraseña | Entrevista |
| RN-02 | Se registran hora de entrada exacta y hora de salida exacta | Entrevista |
| RN-03 | Si un instructor no vino, no hay registro. No se marca ausencia | Entrevista |
| RN-04 | El sueldo se calcula directamente de las horas trabajadas registradas | Entrevista |
| RN-05 | El modelo de pago es por hora trabajada (no por clase ni fijo mensual) | Entrevista |
| RN-06 | El admin puede editar hora de entrada y/o salida si se registró mal | Entrevista |
| RN-07 | Solo el admin ve los registros de asistencia. El instructor no ve los suyos | Entrevista |
| RN-08 | El reporte se ve en pantalla dentro del sistema. No se exporta por ahora | Entrevista |
| RN-09 | El admin necesita reporte semanal y mensual (mensual para liquidar) | Entrevista |
| RN-10 | El horario es variable semana a semana — no hay turnos fijos para instructores | Entrevista |
| RN-11 | Cada instructor da una sola actividad | Entrevista |

### Features funcionales requeridos

#### F-01 — Fichar entrada
- El instructor se identifica con huella dactilar o contraseña en el sistema
- El sistema valida que el usuario existe, está activo y tiene rol instructor/empleado
- Registra: `instructor_id`, `fecha`, `hora_entrada` (timestamp exacto), `actividad_id`
- Si ya tiene una entrada abierta sin salida ese día → mostrar alerta, no permitir segunda entrada

#### F-02 — Fichar salida
- El instructor vuelve a autenticarse al irse
- El sistema busca la entrada abierta del día actual para ese instructor
- Registra `hora_salida` y calcula `horas_trabajadas` = hora_salida - hora_entrada
- Si no hay entrada abierta → mostrar error

#### F-03 — Corrección de fichaje (solo admin)
- El admin puede seleccionar cualquier registro y modificar `hora_entrada` y/o `hora_salida`
- Recalcula automáticamente `horas_trabajadas`
- Queda registrado en auditoría quién editó y cuándo

#### F-04 — Reporte semanal (solo admin)
- Filtro por semana (con selector de fecha)
- Muestra por instructor: nombre, actividad, días asistidos, total horas
- Ordenado por nombre de instructor

#### F-05 — Reporte mensual para liquidación (solo admin)
- Filtro por mes y año
- Muestra por instructor: nombre, actividad, días asistidos, total horas del mes
- Columna calculada: `total_horas × tarifa_hora = sueldo_estimado`
- La tarifa por hora se configura por instructor (ver F-06)

#### F-06 — Tarifa por hora de instructor (solo admin)
- Cada usuario con rol instructor tiene una tarifa horaria configurable
- Se guarda en la tabla `usuarios` como columna `tarifa_hora DECIMAL(10,2)`
- El admin puede editarla desde la pantalla de Usuarios o desde el reporte

#### F-07 — Historial completo (solo admin)
- Lista de todos los fichajes con filtros: instructor, fecha desde/hasta, actividad
- Columnas: instructor, actividad, fecha, hora entrada, hora salida, horas trabajadas, estado
- Estado: "Abierto" (sin salida) o "Cerrado" (con salida)

---

## 3. CLASIFICACIÓN — Estado y lo que falta

### Estado actual del módulo en el sistema
El módulo `InstructorAsistenciasPage` ya existe pero fue diseñado con supuestos distintos:
- ❌ El fichaje era manual por el admin (cargaba él por el instructor)
- ❌ No hay lógica de huella dactilar integrada al fichaje
- ❌ No hay cálculo de sueldo
- ❌ No hay tarifa por hora en la tabla de usuarios
- ❌ No hay reporte mensual con sueldo estimado

### Qué falta implementar (delta)

| Item | Descripción | Prioridad |
|------|-------------|-----------|
| Columna `tarifa_hora` en tabla `usuarios` | ALTER TABLE o en creación si es nueva BD | 🔴 Alta |
| SP `sp_FicharEntradaInstructor` | Autenticación + registro con validación de entrada duplicada | 🔴 Alta |
| SP `sp_FicharSalidaInstructor` | Busca entrada abierta y registra salida + calcula horas | 🔴 Alta |
| SP `sp_ReporteMensualInstructores` | Agrupado por instructor, suma horas, calcula sueldo estimado | 🔴 Alta |
| SP `sp_ReporteSemanálInstructores` | Igual pero por semana | 🟡 Media |
| Pantalla de fichaje rápido | Panel donde el instructor pone su DNI/contraseña para fichar | 🔴 Alta |
| Panel admin — historial + corrección | Edición de hora entrada/salida por admin | 🟡 Media |
| Panel admin — reporte mensual con sueldo | Vista de liquidación mensual | 🔴 Alta |
| Campo tarifa_hora en formulario de Usuarios | Para que admin configure el valor | 🟡 Media |

### Dependencias con otros módulos
- Requiere que `usuarios` tenga `tarifa_hora DECIMAL(10,2) NULL DEFAULT 0`
- Requiere que el módulo de huella dactilar esté conectado (o usar contraseña como fallback)
- El `sp_ValidarUsuario` del login es reutilizable para el fichaje por contraseña

---

## 4. PLAN — Arquitectura del módulo

### Estructura de datos

```
usuarios
├── id
├── rol_id              → 1=admin, 2=empleado/instructor
├── nombre, apellido
├── dni
├── password_hash
├── tarifa_hora         ← NUEVA columna DECIMAL(10,2) DEFAULT 0
└── activo

instructor_asistencias  (tabla existente, revisar columnas)
├── id                  BIGINT IDENTITY
├── instructor_id       BIGINT FK → usuarios.id
├── actividad_id        BIGINT FK → actividades.id (NULL si no hay turno asignado)
├── fecha               DATE
├── hora_entrada        TIME
├── hora_salida         TIME NULL
├── horas_trabajadas    DECIMAL(5,2) NULL  ← calcular al registrar salida
├── observaciones       NVARCHAR(300) NULL
├── registrado_por      BIGINT FK → usuarios.id (quien editó, si admin corrigió)
└── creado_en           DATETIME DEFAULT GETDATE()
```

### Flujo técnico de fichaje de entrada
```
1. Instructor ingresa DNI + contraseña en pantalla de fichaje
2. Sistema llama sp_FicharEntradaInstructor(@Dni, @PasswordHash)
3. SP valida credenciales (similar a sp_ValidarUsuario)
4. SP verifica que NO haya una entrada abierta hoy para ese instructor
5. Si ok → INSERT en instructor_asistencias con hora_entrada = GETDATE()
6. Si ya fichó → RAISERROR 'Ya registraste tu entrada hoy'
7. Sistema muestra confirmación con hora registrada
```

### Flujo técnico de fichaje de salida
```
1. Instructor vuelve a ingresar DNI + contraseña
2. Sistema llama sp_FicharSalidaInstructor(@Dni, @PasswordHash)
3. SP valida credenciales
4. SP busca la entrada abierta del día (hora_salida IS NULL, fecha = hoy)
5. Si existe → UPDATE con hora_salida = GETDATE(), horas_trabajadas = DATEDIFF(MINUTE)/60.0
6. Si no hay entrada abierta → RAISERROR 'No tenés una entrada registrada hoy'
7. Sistema muestra confirmación con horas trabajadas
```

### Cálculo de sueldo en reporte mensual
```sql
-- Por instructor en el mes:
SUM(horas_trabajadas) * u.tarifa_hora AS sueldo_estimado
```

---

## 5. CÓDIGO — Tareas para Claude Code

> **INSTRUCCIÓN PARA CLAUDE CODE**: Implementar en este orden exacto. 
> Verificar el SPEC.md global para las reglas de cada archivo antes de escribir.

---

### TAREA 1 — Agregar `tarifa_hora` a la tabla usuarios

```sql
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('usuarios') AND name = 'tarifa_hora'
)
    ALTER TABLE usuarios ADD tarifa_hora DECIMAL(10,2) NOT NULL DEFAULT 0;
```

Agregar también al SP `sp_ObtenerUsuarios`, `sp_InsertarUsuario` y `sp_ModificarUsuario`:
- `@TarifaHora DECIMAL(10,2) = 0` como parámetro
- Incluirlo en el SELECT y en el INSERT/UPDATE correspondiente

---

### TAREA 2 — SP `sp_FicharEntradaInstructor`

```sql
IF OBJECT_ID('sp_FicharEntradaInstructor', 'P') IS NOT NULL 
    DROP PROCEDURE sp_FicharEntradaInstructor;
GO
CREATE PROCEDURE sp_FicharEntradaInstructor
    @Dni          VARCHAR(15),
    @PasswordHash VARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    -- Validar credenciales
    DECLARE @InstructorId BIGINT;
    DECLARE @Nombre       VARCHAR(100);
    DECLARE @Apellido     VARCHAR(100);

    SELECT @InstructorId = id,
           @Nombre       = nombre,
           @Apellido     = apellido
    FROM usuarios
    WHERE dni = @Dni
      AND password_hash = @PasswordHash
      AND activo = 1
      AND eliminado_en IS NULL;

    IF @InstructorId IS NULL
    BEGIN
        RAISERROR('DNI o contraseña incorrectos.', 16, 1);
        RETURN;
    END

    -- Verificar si ya fichó hoy
    IF EXISTS (
        SELECT 1 FROM instructor_asistencias
        WHERE instructor_id = @InstructorId
          AND fecha = CAST(GETDATE() AS DATE)
    )
    BEGIN
        -- Verificar si ya tiene salida también
        DECLARE @TieneSalida BIT;
        SELECT @TieneSalida = CASE WHEN hora_salida IS NOT NULL THEN 1 ELSE 0 END
        FROM instructor_asistencias
        WHERE instructor_id = @InstructorId
          AND fecha = CAST(GETDATE() AS DATE);

        IF @TieneSalida = 0
            RAISERROR('Ya registraste tu entrada hoy y aún no fichaste salida.', 16, 1);
        ELSE
            RAISERROR('Ya completaste tu jornada de hoy (entrada y salida registradas).', 16, 1);
        RETURN;
    END

    -- Registrar entrada
    INSERT INTO instructor_asistencias
        (instructor_id, fecha, hora_entrada)
    VALUES
        (@InstructorId, CAST(GETDATE() AS DATE), CAST(GETDATE() AS TIME));

    SELECT
        SCOPE_IDENTITY()            AS id,
        @InstructorId               AS instructor_id,
        @Nombre + ' ' + @Apellido   AS nombre_completo,
        CAST(GETDATE() AS TIME)     AS hora_entrada,
        CAST(GETDATE() AS DATE)     AS fecha;
END;
GO
```

---

### TAREA 3 — SP `sp_FicharSalidaInstructor`

```sql
IF OBJECT_ID('sp_FicharSalidaInstructor', 'P') IS NOT NULL 
    DROP PROCEDURE sp_FicharSalidaInstructor;
GO
CREATE PROCEDURE sp_FicharSalidaInstructor
    @Dni          VARCHAR(15),
    @PasswordHash VARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    -- Validar credenciales
    DECLARE @InstructorId BIGINT;
    SELECT @InstructorId = id
    FROM usuarios
    WHERE dni = @Dni
      AND password_hash = @PasswordHash
      AND activo = 1
      AND eliminado_en IS NULL;

    IF @InstructorId IS NULL
    BEGIN
        RAISERROR('DNI o contraseña incorrectos.', 16, 1);
        RETURN;
    END

    -- Buscar entrada abierta de hoy
    DECLARE @AsistenciaId BIGINT;
    DECLARE @HoraEntrada  TIME;

    SELECT @AsistenciaId = id,
           @HoraEntrada  = hora_entrada
    FROM instructor_asistencias
    WHERE instructor_id = @InstructorId
      AND fecha         = CAST(GETDATE() AS DATE)
      AND hora_salida   IS NULL;

    IF @AsistenciaId IS NULL
    BEGIN
        RAISERROR('No tenés una entrada registrada hoy sin salida.', 16, 1);
        RETURN;
    END

    -- Calcular horas trabajadas
    DECLARE @HoraSalida    TIME      = CAST(GETDATE() AS TIME);
    DECLARE @MinutosTrabaj INT       = DATEDIFF(MINUTE, @HoraEntrada, @HoraSalida);
    DECLARE @HorasTrabaj   DECIMAL(5,2) = @MinutosTrabaj / 60.0;

    UPDATE instructor_asistencias SET
        hora_salida      = @HoraSalida,
        horas_trabajadas = @HorasTrabaj
    WHERE id = @AsistenciaId;

    SELECT
        @AsistenciaId     AS id,
        @HoraEntrada      AS hora_entrada,
        @HoraSalida       AS hora_salida,
        @HorasTrabaj      AS horas_trabajadas,
        @MinutosTrabaj    AS minutos_trabajados;
END;
GO
```

---

### TAREA 4 — SP `sp_ReporteMensualInstructores`

```sql
IF OBJECT_ID('sp_ReporteMensualInstructores', 'P') IS NOT NULL 
    DROP PROCEDURE sp_ReporteMensualInstructores;
GO
CREATE PROCEDURE sp_ReporteMensualInstructores
    @Anio INT,
    @Mes  INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.id                                                          AS instructor_id,
        u.nombre + ' ' + u.apellido                                   AS nombre_completo,
        u.tarifa_hora,
        ISNULL(a.nombre, '—')                                         AS actividad_nombre,
        COUNT(DISTINCT ia.fecha)                                       AS dias_asistidos,
        ISNULL(SUM(ia.horas_trabajadas), 0)                           AS total_horas,
        ISNULL(SUM(ia.horas_trabajadas), 0) * u.tarifa_hora           AS sueldo_estimado,
        MIN(ia.fecha)                                                  AS primer_dia,
        MAX(ia.fecha)                                                  AS ultimo_dia
    FROM usuarios u
    LEFT JOIN instructor_asistencias ia
           ON ia.instructor_id = u.id
          AND YEAR(ia.fecha)   = @Anio
          AND MONTH(ia.fecha)  = @Mes
    LEFT JOIN actividades a
           ON a.id = ia.actividad_id
    WHERE u.rol_id  = 2         -- empleados/instructores
      AND u.activo  = 1
      AND u.eliminado_en IS NULL
    GROUP BY u.id, u.nombre, u.apellido, u.tarifa_hora, a.nombre
    ORDER BY u.apellido ASC, u.nombre ASC;
END;
GO
```

---

### TAREA 5 — SP `sp_ReporteSemanalInstructores`

```sql
IF OBJECT_ID('sp_ReporteSemanalInstructores', 'P') IS NOT NULL 
    DROP PROCEDURE sp_ReporteSemanalInstructores;
GO
CREATE PROCEDURE sp_ReporteSemanalInstructores
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.id                                                AS instructor_id,
        u.nombre + ' ' + u.apellido                         AS nombre_completo,
        u.tarifa_hora,
        ia.fecha,
        ia.hora_entrada,
        ia.hora_salida,
        ISNULL(ia.horas_trabajadas, 0)                      AS horas_trabajadas,
        CASE WHEN ia.hora_salida IS NULL THEN 'Abierto'
             ELSE 'Cerrado' END                              AS estado
    FROM usuarios u
    LEFT JOIN instructor_asistencias ia
           ON ia.instructor_id = u.id
          AND ia.fecha BETWEEN @FechaDesde AND @FechaHasta
    WHERE u.rol_id = 2
      AND u.activo = 1
      AND u.eliminado_en IS NULL
    ORDER BY ia.fecha DESC, u.apellido ASC;
END;
GO
```

---

### TAREA 6 — Actualizar la Entity `InstructorAsistencia.cs`

Agregar al POCO existente:
```csharp
public decimal HorasTrabajadas { get; set; }

public string HorasTrabajadasTexto
{
    get
    {
        if (HorasTrabajadas <= 0) return AsistenciaAbierta ? "en curso" : "—";
        int h = (int)HorasTrabajadas;
        int m = (int)((HorasTrabajadas - h) * 60);
        return h > 0 ? $"{h}h {m:D2}m" : $"{m} min";
    }
}
```

Agregar entidad para el reporte:
```csharp
public class ReporteInstructor
{
    public long     InstructorId      { get; set; }
    public string   NombreCompleto    { get; set; }
    public decimal  TarifaHora        { get; set; }
    public string   ActividadNombre   { get; set; }
    public int      DiasAsistidos     { get; set; }
    public decimal  TotalHoras        { get; set; }
    public decimal  SueldoEstimado    { get; set; }

    public string TotalHorasTexto
    {
        get
        {
            int h = (int)TotalHoras;
            int m = (int)((TotalHoras - h) * 60);
            return h > 0 ? $"{h}h {m:D2}m" : $"{m} min";
        }
    }

    public string SueldoTexto => "$" + SueldoEstimado.ToString("N2");
    public string TarifaTexto => "$" + TarifaHora.ToString("N2") + "/h";
}
```

---

### TAREA 7 — Pantalla `InstructorAsistenciasPage` — rediseño

La pantalla tiene **dos secciones** con tabs o toggle:

#### Sección A — Fichaje rápido (visible para todos)
- Campo "DNI" (TextBox)
- Campo "Contraseña" (PasswordBox)
- Botón "REGISTRAR ENTRADA" (verde)
- Botón "REGISTRAR SALIDA" (naranja)
- Panel de resultado: muestra nombre del instructor + hora registrada + horas trabajadas (en salida)

#### Sección B — Panel admin (solo si `SesionManager.EsAdmin`)
- Tab "Historial" — DataGrid con todos los registros + filtros fecha/instructor
- Tab "Reporte semanal" — DatePicker semana + tabla resumen
- Tab "Reporte mensual" — Selector mes/año + tabla con sueldo estimado
- Botón "Editar" en cada fila del historial → abre formulario para corregir hora entrada/salida

---

### TAREA 8 — Registrar en auditoría

En `InstructorAsistenciaController`, agregar en cada método:

```csharp
// Al registrar entrada:
Auditor.Registrar("crear", "asistencia", nuevoId, new Dictionary<string, object> {
    { "instructor_id", instructorId },
    { "hora_entrada", horaEntrada.ToString() },
    { "tipo", "entrada" }
});

// Al registrar salida:
Auditor.Registrar("editar", "asistencia", id, new Dictionary<string, object> {
    { "hora_salida", horaSalida.ToString() },
    { "horas_trabajadas", horasTrabajadas },
    { "tipo", "salida" }
});

// Al corregir admin:
Auditor.Registrar("editar", "asistencia", id, new Dictionary<string, object> {
    { "corregido_por_admin", true },
    { "hora_entrada_nueva", horaEntrada.ToString() },
    { "hora_salida_nueva", horaSalida.ToString() }
});
```

---

*SDD Módulo Asistencia Instructores — OptimusCAI v1.1 — Mayo 2026*
