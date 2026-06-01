-- ============================================================
--  SP_InstructorAsistencias.sql
--  Registro de presencia de instructores en sus turnos
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('instructor_asistencias') AND name = 'horas_trabajadas'
)
    ALTER TABLE instructor_asistencias ADD horas_trabajadas DECIMAL(5,2) NULL;
GO

IF OBJECT_ID('sp_ObtenerInstructorAsistencias',     'P') IS NOT NULL DROP PROCEDURE sp_ObtenerInstructorAsistencias;
IF OBJECT_ID('sp_BuscarInstructorAsistencias',      'P') IS NOT NULL DROP PROCEDURE sp_BuscarInstructorAsistencias;
IF OBJECT_ID('sp_RegistrarEntradaInstructor',       'P') IS NOT NULL DROP PROCEDURE sp_RegistrarEntradaInstructor;
IF OBJECT_ID('sp_RegistrarSalidaInstructor',        'P') IS NOT NULL DROP PROCEDURE sp_RegistrarSalidaInstructor;
IF OBJECT_ID('sp_EliminarInstructorAsistencia',     'P') IS NOT NULL DROP PROCEDURE sp_EliminarInstructorAsistencia;
IF OBJECT_ID('sp_ActualizarInstructorAsistencia',   'P') IS NOT NULL DROP PROCEDURE sp_ActualizarInstructorAsistencia;
IF OBJECT_ID('sp_TurnosDeHoy',                      'P') IS NOT NULL DROP PROCEDURE sp_TurnosDeHoy;
IF OBJECT_ID('sp_EstadisticasInstructorAsistencias','P') IS NOT NULL DROP PROCEDURE sp_EstadisticasInstructorAsistencias;
IF OBJECT_ID('sp_FicharEntradaInstructor',          'P') IS NOT NULL DROP PROCEDURE sp_FicharEntradaInstructor;
IF OBJECT_ID('sp_FicharSalidaInstructor',           'P') IS NOT NULL DROP PROCEDURE sp_FicharSalidaInstructor;
IF OBJECT_ID('sp_BuscarMisAsistencias',             'P') IS NOT NULL DROP PROCEDURE sp_BuscarMisAsistencias;
IF OBJECT_ID('sp_ReporteMensualInstructores',       'P') IS NOT NULL DROP PROCEDURE sp_ReporteMensualInstructores;
IF OBJECT_ID('sp_ReporteSemanalInstructores',       'P') IS NOT NULL DROP PROCEDURE sp_ReporteSemanalInstructores;
IF OBJECT_ID('sp_FicharInstructorDashboard',         'P') IS NOT NULL DROP PROCEDURE sp_FicharInstructorDashboard;
GO

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER (con filtro por rango)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerInstructorAsistencias
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -7, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        ia.id, ia.instructor_id, ia.turno_id, ia.fecha,
        ia.hora_entrada, ia.hora_salida, ia.horas_trabajadas, ia.observaciones,
        ia.registrado_por, ia.creado_en,
        u.nombre + ' ' + u.apellido          AS instructor_nombre,
        u.foto                                AS instructor_foto,
        ISNULL(a.nombre, '-')                 AS actividad_nombre,
        t.dia_semana, t.hora_inicio, t.hora_fin,
        ISNULL(uReg.nombre + ' ' + uReg.apellido, 'Sistema') AS registrado_por_nombre
    FROM instructor_asistencias ia
    INNER JOIN usuarios     u    ON u.id = ia.instructor_id
    LEFT  JOIN turnos       t    ON t.id = ia.turno_id
    LEFT  JOIN actividades  a    ON a.id = t.actividad_id
    LEFT  JOIN usuarios     uReg ON uReg.id = ia.registrado_por
    WHERE ia.fecha BETWEEN @FechaDesde AND @FechaHasta
    ORDER BY ia.fecha DESC, ia.hora_entrada DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. BUSCAR
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_BuscarInstructorAsistencias
    @Texto        NVARCHAR(150) = '',
    @InstructorId BIGINT        = NULL,
    @FechaDesde   DATE          = NULL,
    @FechaHasta   DATE          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -7, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        ia.id, ia.instructor_id, ia.turno_id, ia.fecha,
        ia.hora_entrada, ia.hora_salida, ia.horas_trabajadas, ia.observaciones,
        ia.registrado_por, ia.creado_en,
        u.nombre + ' ' + u.apellido          AS instructor_nombre,
        u.foto                                AS instructor_foto,
        ISNULL(a.nombre, '-')                 AS actividad_nombre,
        t.dia_semana, t.hora_inicio, t.hora_fin,
        ISNULL(uReg.nombre + ' ' + uReg.apellido, 'Sistema') AS registrado_por_nombre
    FROM instructor_asistencias ia
    INNER JOIN usuarios     u    ON u.id = ia.instructor_id
    LEFT  JOIN turnos       t    ON t.id = ia.turno_id
    LEFT  JOIN actividades  a    ON a.id = t.actividad_id
    LEFT  JOIN usuarios     uReg ON uReg.id = ia.registrado_por
    WHERE ia.fecha BETWEEN @FechaDesde AND @FechaHasta
      AND (@Texto = ''
           OR u.nombre   LIKE '%' + @Texto + '%'
           OR u.apellido LIKE '%' + @Texto + '%'
           OR a.nombre   LIKE '%' + @Texto + '%')
      AND (@InstructorId IS NULL OR ia.instructor_id = @InstructorId)
    ORDER BY ia.fecha DESC, ia.hora_entrada DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. REGISTRAR ENTRADA (por admin, vinculado a turno)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_RegistrarEntradaInstructor
    @InstructorId   BIGINT,
    @TurnoId        BIGINT       = NULL,
    @Fecha          DATE         = NULL,
    @Observaciones  NVARCHAR(300) = NULL,
    @RegistradoPor  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @Fecha IS NULL SET @Fecha = CAST(GETDATE() AS DATE);

    IF NOT EXISTS (SELECT 1 FROM usuarios
                   WHERE id = @InstructorId AND activo = 1 AND eliminado_en IS NULL)
    BEGIN
        RAISERROR('El instructor no existe o esta inactivo.', 16, 1);
        RETURN;
    END

    IF EXISTS (
        SELECT 1 FROM instructor_asistencias
        WHERE instructor_id = @InstructorId
          AND fecha         = @Fecha
          AND ((@TurnoId IS NULL AND turno_id IS NULL)
               OR turno_id = @TurnoId)
          AND hora_salida IS NULL
    )
    BEGIN
        RAISERROR('Ya existe una entrada abierta sin salida registrada.', 16, 1);
        RETURN;
    END

    DECLARE @Hora TIME;
    SET @Hora = CAST(GETDATE() AS TIME);

    INSERT INTO instructor_asistencias
        (instructor_id, turno_id, fecha, hora_entrada, observaciones, registrado_por)
    VALUES
        (@InstructorId, @TurnoId, @Fecha, @Hora, @Observaciones, @RegistradoPor);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. REGISTRAR SALIDA (por admin, por ID de asistencia)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_RegistrarSalidaInstructor
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @HoraEntrada TIME;
    DECLARE @HoraSalida  TIME;

    SELECT @HoraEntrada = hora_entrada, @HoraSalida = hora_salida
    FROM instructor_asistencias WHERE id = @Id;

    IF @HoraEntrada IS NULL
    BEGIN
        RAISERROR('Asistencia no encontrada.', 16, 1);
        RETURN;
    END

    IF @HoraSalida IS NOT NULL
    BEGIN
        RAISERROR('Esta asistencia ya tiene salida registrada.', 16, 1);
        RETURN;
    END

    DECLARE @NuevaHoraSalida TIME = CAST(GETDATE() AS TIME);
    DECLARE @Minutos         INT  = DATEDIFF(MINUTE, @HoraEntrada, @NuevaHoraSalida);

    UPDATE instructor_asistencias
    SET hora_salida      = @NuevaHoraSalida,
        horas_trabajadas = @Minutos / 60.0
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. ACTUALIZAR (corrección admin — recalcula horas_trabajadas)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ActualizarInstructorAsistencia
    @Id            BIGINT,
    @TurnoId       BIGINT       = NULL,
    @HoraEntrada   TIME         = NULL,
    @HoraSalida    TIME         = NULL,
    @Observaciones NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @HoraEntrada IS NOT NULL AND @HoraSalida IS NOT NULL AND @HoraEntrada > @HoraSalida
    BEGIN
        RAISERROR('La hora de entrada debe ser anterior a la hora de salida.', 16, 1);
        RETURN;
    END

    UPDATE instructor_asistencias
    SET turno_id         = @TurnoId,
        hora_entrada     = ISNULL(@HoraEntrada, hora_entrada),
        hora_salida      = @HoraSalida,
        observaciones    = @Observaciones,
        horas_trabajadas = CASE
            WHEN ISNULL(@HoraEntrada, hora_entrada) IS NOT NULL AND @HoraSalida IS NOT NULL
            THEN DATEDIFF(MINUTE, ISNULL(@HoraEntrada, hora_entrada), @HoraSalida) / 60.0
            ELSE NULL
        END
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. ELIMINAR
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_EliminarInstructorAsistencia
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM instructor_asistencias WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. TURNOS DE HOY
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_TurnosDeHoy
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DiaApp INT;
    SET @DiaApp = CASE DATEPART(WEEKDAY, GETDATE())
        WHEN 1 THEN 7 WHEN 2 THEN 1 WHEN 3 THEN 2 WHEN 4 THEN 3
        WHEN 5 THEN 4 WHEN 6 THEN 5 WHEN 7 THEN 6
    END;

    DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);

    SELECT
        t.id            AS turno_id,
        t.actividad_id,
        t.instructor_id,
        t.dia_semana,
        t.hora_inicio,
        t.hora_fin,
        t.cupo_maximo,
        a.nombre        AS actividad_nombre,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sin asignar') AS instructor_nombre,
        u.foto          AS instructor_foto,
        ia.id           AS asistencia_id,
        ia.hora_entrada,
        ia.hora_salida
    FROM turnos t
    INNER JOIN actividades a ON a.id = t.actividad_id
    LEFT  JOIN usuarios    u ON u.id = t.instructor_id
    LEFT  JOIN instructor_asistencias ia
                                 ON ia.turno_id = t.id
                                AND ia.fecha    = @Hoy
                                AND ia.instructor_id = t.instructor_id
    WHERE t.dia_semana = @DiaApp
      AND t.activo     = 1
    ORDER BY t.hora_inicio ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 8. ESTADÍSTICAS
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_EstadisticasInstructorAsistencias
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);
    DECLARE @PrimerDiaMes DATE = DATEFROMPARTS(YEAR(@Hoy), MONTH(@Hoy), 1);

    SELECT
        ISNULL((SELECT COUNT(*) FROM instructor_asistencias
                WHERE fecha = @Hoy), 0) AS asistencias_hoy,
        ISNULL((SELECT COUNT(*) FROM instructor_asistencias
                WHERE fecha = @Hoy AND hora_salida IS NULL), 0) AS abiertas_hoy,
        ISNULL((SELECT COUNT(DISTINCT instructor_id) FROM instructor_asistencias
                WHERE fecha = @Hoy), 0) AS instructores_hoy,
        ISNULL((SELECT COUNT(*) FROM instructor_asistencias
                WHERE fecha >= @PrimerDiaMes), 0) AS asistencias_mes;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 9. FICHAR ENTRADA (validado por ID de sesión, multi-turno
--    con cooldown de 10 minutos entre sesiones del mismo día)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_FicharEntradaInstructor
    @InstructorId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Nombre   NVARCHAR(100);
    DECLARE @Apellido NVARCHAR(100);

    SELECT @Nombre = nombre, @Apellido = apellido
    FROM usuarios
    WHERE id = @InstructorId
      AND activo = 1
      AND eliminado_en IS NULL;

    IF @Nombre IS NULL
    BEGIN
        RAISERROR('Instructor no encontrado o inactivo.', 16, 1);
        RETURN;
    END

    -- Bloquear si hay una entrada abierta (sin salida)
    IF EXISTS (
        SELECT 1 FROM instructor_asistencias
        WHERE instructor_id = @InstructorId
          AND fecha = CAST(GETDATE() AS DATE)
          AND hora_salida IS NULL
    )
    BEGIN
        RAISERROR('Tenés una entrada abierta sin salida. Registrá tu salida primero.', 16, 1);
        RETURN;
    END

    -- Cooldown de 10 minutos desde la última salida del día
    DECLARE @UltimaSalidaDT DATETIME;
    SELECT TOP 1
        @UltimaSalidaDT = DATEADD(MILLISECOND,
                              DATEDIFF(MILLISECOND, 0, hora_salida),
                              CAST(fecha AS DATETIME))
    FROM instructor_asistencias
    WHERE instructor_id = @InstructorId
      AND fecha = CAST(GETDATE() AS DATE)
      AND hora_salida IS NOT NULL
    ORDER BY id DESC;

    IF @UltimaSalidaDT IS NOT NULL
    BEGIN
        DECLARE @MinutosDesde INT = DATEDIFF(MINUTE, @UltimaSalidaDT, GETDATE());
        IF @MinutosDesde < 10
        BEGIN
            DECLARE @Restantes INT = 10 - @MinutosDesde;
            DECLARE @MsgEspera NVARCHAR(200) =
                N'Debés esperar ' + CAST(@Restantes AS NVARCHAR) +
                N' minuto(s) antes de registrar una nueva entrada.';
            RAISERROR(@MsgEspera, 16, 1);
            RETURN;
        END
    END

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

-- ─────────────────────────────────────────────────────────────
-- 10. FICHAR SALIDA (toma la última entrada abierta del día)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_FicharSalidaInstructor
    @InstructorId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Nombre   NVARCHAR(100);
    DECLARE @Apellido NVARCHAR(100);

    SELECT @Nombre = nombre, @Apellido = apellido
    FROM usuarios
    WHERE id = @InstructorId
      AND activo = 1
      AND eliminado_en IS NULL;

    IF @Nombre IS NULL
    BEGIN
        RAISERROR('Instructor no encontrado o inactivo.', 16, 1);
        RETURN;
    END

    DECLARE @AsistenciaId BIGINT;
    DECLARE @HoraEntrada  TIME;

    -- Última entrada abierta del día
    SELECT TOP 1
        @AsistenciaId = id,
        @HoraEntrada  = hora_entrada
    FROM instructor_asistencias
    WHERE instructor_id = @InstructorId
      AND fecha = CAST(GETDATE() AS DATE)
      AND hora_salida IS NULL
    ORDER BY id DESC;

    IF @AsistenciaId IS NULL
    BEGIN
        RAISERROR('No tenés una entrada abierta hoy. Registrá tu entrada primero.', 16, 1);
        RETURN;
    END

    DECLARE @HoraSalida    TIME         = CAST(GETDATE() AS TIME);
    DECLARE @MinutosTrabaj INT          = DATEDIFF(MINUTE, @HoraEntrada, @HoraSalida);
    DECLARE @HorasTrabaj   DECIMAL(5,2) = @MinutosTrabaj / 60.0;

    UPDATE instructor_asistencias SET
        hora_salida      = @HoraSalida,
        horas_trabajadas = @HorasTrabaj
    WHERE id = @AsistenciaId;

    SELECT
        @AsistenciaId               AS id,
        @InstructorId               AS instructor_id,
        @Nombre + ' ' + @Apellido   AS nombre_completo,
        @HoraEntrada                AS hora_entrada,
        @HoraSalida                 AS hora_salida,
        @HorasTrabaj                AS horas_trabajadas,
        @MinutosTrabaj              AS minutos_trabajados;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 13. MIS ASISTENCIAS (filtro por instructor logueado)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_BuscarMisAsistencias
    @InstructorId BIGINT,
    @FechaDesde   DATE = NULL,
    @FechaHasta   DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -30, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        ia.id, ia.instructor_id, ia.turno_id, ia.fecha,
        ia.hora_entrada, ia.hora_salida, ia.horas_trabajadas, ia.observaciones,
        ia.registrado_por, ia.creado_en,
        u.nombre + ' ' + u.apellido          AS instructor_nombre,
        u.foto                                AS instructor_foto,
        ISNULL(a.nombre, '-')                 AS actividad_nombre,
        t.dia_semana, t.hora_inicio, t.hora_fin,
        ISNULL(uReg.nombre + ' ' + uReg.apellido, 'Sistema') AS registrado_por_nombre
    FROM instructor_asistencias ia
    INNER JOIN usuarios     u    ON u.id = ia.instructor_id
    LEFT  JOIN turnos       t    ON t.id = ia.turno_id
    LEFT  JOIN actividades  a    ON a.id = t.actividad_id
    LEFT  JOIN usuarios     uReg ON uReg.id = ia.registrado_por
    WHERE ia.instructor_id = @InstructorId
      AND ia.fecha BETWEEN @FechaDesde AND @FechaHasta
    ORDER BY ia.fecha DESC, ia.hora_entrada DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 11. REPORTE MENSUAL DE INSTRUCTORES (para liquidación)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ReporteMensualInstructores
    @Anio INT,
    @Mes  INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.id                                                        AS instructor_id,
        u.nombre + ' ' + u.apellido                                 AS nombre_completo,
        u.tarifa_hora,
        ISNULL(a.nombre, '-')                                       AS actividad_nombre,
        COUNT(DISTINCT ia.fecha)                                     AS dias_asistidos,
        ISNULL(SUM(ia.horas_trabajadas), 0)                         AS total_horas,
        ISNULL(SUM(ia.horas_trabajadas), 0) * u.tarifa_hora         AS sueldo_estimado,
        MIN(ia.fecha)                                                AS primer_dia,
        MAX(ia.fecha)                                                AS ultimo_dia
    FROM usuarios u
    LEFT JOIN instructor_asistencias ia
           ON ia.instructor_id = u.id
          AND YEAR(ia.fecha)   = @Anio
          AND MONTH(ia.fecha)  = @Mes
    LEFT JOIN turnos t
           ON t.id = ia.turno_id
    LEFT JOIN actividades a
           ON a.id = t.actividad_id
    WHERE u.rol_id  = 2
      AND u.activo  = 1
      AND u.eliminado_en IS NULL
    GROUP BY u.id, u.nombre, u.apellido, u.tarifa_hora, a.nombre
    ORDER BY u.apellido ASC, u.nombre ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 12. REPORTE SEMANAL DE INSTRUCTORES (detalle por día)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ReporteSemanalInstructores
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.id                                            AS instructor_id,
        u.nombre + ' ' + u.apellido                     AS nombre_completo,
        u.tarifa_hora,
        ia.fecha,
        ia.hora_entrada,
        ia.hora_salida,
        ISNULL(ia.horas_trabajadas, 0)                  AS horas_trabajadas,
        CASE WHEN ia.hora_salida IS NULL THEN 'Abierto'
             ELSE 'Cerrado' END                          AS estado
    FROM usuarios u
    INNER JOIN instructor_asistencias ia
            ON ia.instructor_id = u.id
           AND ia.fecha BETWEEN @FechaDesde AND @FechaHasta
    WHERE u.rol_id = 2
      AND u.activo = 1
      AND u.eliminado_en IS NULL
    ORDER BY ia.fecha DESC, u.apellido ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 14. FICHAR INSTRUCTOR DESDE DASHBOARD (por DNI, auto toggle)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_FicharInstructorDashboard
    @Dni            CHAR(8),
    @RegistradoPor  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @InstructorId   BIGINT;
    DECLARE @Nombre         NVARCHAR(100);
    DECLARE @Apellido       NVARCHAR(100);
    DECLARE @Foto           VARBINARY(MAX);

    SELECT @InstructorId = id, @Nombre = nombre, @Apellido = apellido, @Foto = foto
    FROM usuarios
    WHERE dni = @Dni AND rol_id = 2 AND activo = 1 AND eliminado_en IS NULL;

    IF @InstructorId IS NULL
    BEGIN
        SELECT CAST(NULL AS BIGINT) AS id,
               CAST(NULL AS BIGINT) AS instructor_id,
               CAST(NULL AS NVARCHAR(200)) AS nombre_completo,
               CAST(NULL AS VARBINARY(MAX)) AS foto,
               CAST(NULL AS TIME) AS hora,
               CAST(NULL AS DECIMAL(5,2)) AS horas_trabajadas,
               'no_encontrado' AS operacion,
               'Instructor no encontrado o inactivo.' AS mensaje;
        RETURN;
    END

    DECLARE @AsistenciaId BIGINT;
    SELECT TOP 1 @AsistenciaId = id
    FROM instructor_asistencias
    WHERE instructor_id = @InstructorId
      AND fecha = CAST(GETDATE() AS DATE)
      AND hora_salida IS NULL
    ORDER BY id DESC;

    IF @AsistenciaId IS NULL
    BEGIN
        -- Registrar entrada
        INSERT INTO instructor_asistencias
            (instructor_id, fecha, hora_entrada, registrado_por)
        VALUES
            (@InstructorId, CAST(GETDATE() AS DATE), CAST(GETDATE() AS TIME), @RegistradoPor);

        SELECT SCOPE_IDENTITY() AS id,
               @InstructorId AS instructor_id,
               @Nombre + ' ' + @Apellido AS nombre_completo,
               @Foto AS foto,
               CAST(GETDATE() AS TIME) AS hora,
               CAST(NULL AS DECIMAL(5,2)) AS horas_trabajadas,
               'entrada' AS operacion,
               'Entrada registrada correctamente.' AS mensaje;
    END
    ELSE
    BEGIN
        -- Registrar salida
        DECLARE @HoraEntrada TIME;
        SELECT @HoraEntrada = hora_entrada
        FROM instructor_asistencias
        WHERE id = @AsistenciaId;

        DECLARE @HoraSalida TIME = CAST(GETDATE() AS TIME);
        DECLARE @Horas DECIMAL(5,2) = DATEDIFF(MINUTE, @HoraEntrada, @HoraSalida) / 60.0;

        UPDATE instructor_asistencias
        SET hora_salida = @HoraSalida,
            horas_trabajadas = @Horas
        WHERE id = @AsistenciaId;

        SELECT @AsistenciaId AS id,
               @InstructorId AS instructor_id,
               @Nombre + ' ' + @Apellido AS nombre_completo,
               @Foto AS foto,
               @HoraSalida AS hora,
               @Horas AS horas_trabajadas,
               'salida' AS operacion,
               'Salida registrada correctamente.' AS mensaje;
    END
END;
GO
