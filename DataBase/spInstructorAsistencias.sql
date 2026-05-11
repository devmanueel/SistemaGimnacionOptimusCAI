-- ============================================================
--  SP_InstructorAsistencias.sql
--  Registro de presencia de instructores en sus turnos
-- ============================================================

IF OBJECT_ID('sp_ObtenerInstructorAsistencias',     'P') IS NOT NULL DROP PROCEDURE sp_ObtenerInstructorAsistencias;
IF OBJECT_ID('sp_BuscarInstructorAsistencias',      'P') IS NOT NULL DROP PROCEDURE sp_BuscarInstructorAsistencias;
IF OBJECT_ID('sp_RegistrarEntradaInstructor',       'P') IS NOT NULL DROP PROCEDURE sp_RegistrarEntradaInstructor;
IF OBJECT_ID('sp_RegistrarSalidaInstructor',        'P') IS NOT NULL DROP PROCEDURE sp_RegistrarSalidaInstructor;
IF OBJECT_ID('sp_EliminarInstructorAsistencia',     'P') IS NOT NULL DROP PROCEDURE sp_EliminarInstructorAsistencia;
IF OBJECT_ID('sp_ActualizarInstructorAsistencia',   'P') IS NOT NULL DROP PROCEDURE sp_ActualizarInstructorAsistencia;
IF OBJECT_ID('sp_TurnosDeHoy',                       'P') IS NOT NULL DROP PROCEDURE sp_TurnosDeHoy;
IF OBJECT_ID('sp_EstadisticasInstructorAsistencias','P') IS NOT NULL DROP PROCEDURE sp_EstadisticasInstructorAsistencias;
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
        ia.hora_entrada, ia.hora_salida, ia.observaciones,
        ia.registrado_por, ia.creado_en,
        u.nombre + ' ' + u.apellido          AS instructor_nombre,
        u.foto                                AS instructor_foto,
        ISNULL(a.nombre, '—')                 AS actividad_nombre,
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
        ia.hora_entrada, ia.hora_salida, ia.observaciones,
        ia.registrado_por, ia.creado_en,
        u.nombre + ' ' + u.apellido          AS instructor_nombre,
        u.foto                                AS instructor_foto,
        ISNULL(a.nombre, '—')                 AS actividad_nombre,
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
-- 3. REGISTRAR ENTRADA
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

    -- Validar que el usuario exista
    IF NOT EXISTS (SELECT 1 FROM usuarios
                   WHERE id = @InstructorId AND activo = 1 AND eliminado_en IS NULL)
    BEGIN
        RAISERROR('El instructor no existe o esta inactivo.', 16, 1);
        RETURN;
    END

    -- No permitir doble entrada para el mismo instructor + turno + fecha
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
-- 4. REGISTRAR SALIDA
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

    UPDATE instructor_asistencias
    SET hora_salida = CAST(GETDATE() AS TIME)
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. ACTUALIZAR (editar manual)
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
    SET turno_id      = @TurnoId,
        hora_entrada  = ISNULL(@HoraEntrada, hora_entrada),
        hora_salida   = @HoraSalida,
        observaciones = @Observaciones
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
-- 7. TURNOS DE HOY (para el panel de "fichaje rápido")
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_TurnosDeHoy
AS
BEGIN
    SET NOCOUNT ON;

    -- Convertir DayOfWeek SQL Server (1=Dom..7=Sab) → app (1=Lun..7=Dom)
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
        -- Si ya tiene una asistencia hoy, traerla
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