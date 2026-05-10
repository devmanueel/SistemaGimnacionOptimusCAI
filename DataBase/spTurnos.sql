-- ============================================================
--  SP_Turnos.sql
--  CRUD de turnos del gimnasio (horario semanal por actividad)
-- ============================================================

IF OBJECT_ID('sp_ObtenerTurnos',           'P') IS NOT NULL DROP PROCEDURE sp_ObtenerTurnos;
IF OBJECT_ID('sp_ObtenerTurnoPorId',       'P') IS NOT NULL DROP PROCEDURE sp_ObtenerTurnoPorId;
IF OBJECT_ID('sp_BuscarTurnos',            'P') IS NOT NULL DROP PROCEDURE sp_BuscarTurnos;
IF OBJECT_ID('sp_InsertarTurno',           'P') IS NOT NULL DROP PROCEDURE sp_InsertarTurno;
IF OBJECT_ID('sp_ModificarTurno',          'P') IS NOT NULL DROP PROCEDURE sp_ModificarTurno;
IF OBJECT_ID('sp_CambiarEstadoTurno',      'P') IS NOT NULL DROP PROCEDURE sp_CambiarEstadoTurno;
IF OBJECT_ID('sp_EliminarTurno',           'P') IS NOT NULL DROP PROCEDURE sp_EliminarTurno;
IF OBJECT_ID('sp_EstadisticasTurnos',      'P') IS NOT NULL DROP PROCEDURE sp_EstadisticasTurnos;
GO

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER TODOS
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerTurnos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        t.id, t.actividad_id, t.instructor_id,
        t.dia_semana, t.hora_inicio, t.hora_fin,
        t.cupo_maximo, t.activo,
        a.nombre AS actividad_nombre,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sin asignar') AS instructor_nombre
    FROM turnos t
    INNER JOIN actividades a ON a.id = t.actividad_id
    LEFT  JOIN usuarios    u ON u.id = t.instructor_id
    ORDER BY t.dia_semana ASC, t.hora_inicio ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER POR ID
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerTurnoPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        t.id, t.actividad_id, t.instructor_id,
        t.dia_semana, t.hora_inicio, t.hora_fin,
        t.cupo_maximo, t.activo,
        a.nombre AS actividad_nombre,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sin asignar') AS instructor_nombre
    FROM turnos t
    INNER JOIN actividades a ON a.id = t.actividad_id
    LEFT  JOIN usuarios    u ON u.id = t.instructor_id
    WHERE t.id = @Id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. BUSCAR (filtros)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_BuscarTurnos
    @Texto       NVARCHAR(150) = '',
    @ActividadId BIGINT        = NULL,
    @DiaSemana   TINYINT       = NULL,
    @SoloActivos BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        t.id, t.actividad_id, t.instructor_id,
        t.dia_semana, t.hora_inicio, t.hora_fin,
        t.cupo_maximo, t.activo,
        a.nombre AS actividad_nombre,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sin asignar') AS instructor_nombre
    FROM turnos t
    INNER JOIN actividades a ON a.id = t.actividad_id
    LEFT  JOIN usuarios    u ON u.id = t.instructor_id
    WHERE (@Texto = ''
           OR a.nombre   LIKE '%' + @Texto + '%'
           OR u.nombre   LIKE '%' + @Texto + '%'
           OR u.apellido LIKE '%' + @Texto + '%')
      AND (@ActividadId IS NULL OR t.actividad_id = @ActividadId)
      AND (@DiaSemana   IS NULL OR t.dia_semana   = @DiaSemana)
      AND (@SoloActivos = 0    OR t.activo = 1)
    ORDER BY t.dia_semana ASC, t.hora_inicio ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. INSERTAR
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_InsertarTurno
    @ActividadId  BIGINT,
    @InstructorId BIGINT       = NULL,
    @DiaSemana    TINYINT,
    @HoraInicio   TIME,
    @HoraFin      TIME,
    @CupoMaximo   SMALLINT     = 30
AS
BEGIN
    SET NOCOUNT ON;

    IF @DiaSemana < 1 OR @DiaSemana > 7
    BEGIN
        RAISERROR('Dia de semana invalido (1-7).', 16, 1);
        RETURN;
    END

    IF @HoraInicio >= @HoraFin
    BEGIN
        RAISERROR('La hora de inicio debe ser anterior a la hora de fin.', 16, 1);
        RETURN;
    END

    -- Validar superposicion: misma actividad + mismo dia + horarios que pisan
    IF EXISTS (
        SELECT 1 FROM turnos
        WHERE actividad_id = @ActividadId
          AND dia_semana   = @DiaSemana
          AND activo       = 1
          AND (
                (@HoraInicio >= hora_inicio AND @HoraInicio <  hora_fin)
             OR (@HoraFin    >  hora_inicio AND @HoraFin    <= hora_fin)
             OR (@HoraInicio <= hora_inicio AND @HoraFin    >= hora_fin)
              )
    )
    BEGIN
        RAISERROR('Ya existe otro turno de esta actividad en ese horario.', 16, 1);
        RETURN;
    END

    INSERT INTO turnos
        (actividad_id, instructor_id, dia_semana, hora_inicio, hora_fin, cupo_maximo, activo)
    VALUES
        (@ActividadId, @InstructorId, @DiaSemana, @HoraInicio, @HoraFin, @CupoMaximo, 1);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. MODIFICAR
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ModificarTurno
    @Id           BIGINT,
    @ActividadId  BIGINT,
    @InstructorId BIGINT       = NULL,
    @DiaSemana    TINYINT,
    @HoraInicio   TIME,
    @HoraFin      TIME,
    @CupoMaximo   SMALLINT     = 30
AS
BEGIN
    SET NOCOUNT ON;

    IF @DiaSemana < 1 OR @DiaSemana > 7
    BEGIN
        RAISERROR('Dia de semana invalido (1-7).', 16, 1);
        RETURN;
    END

    IF @HoraInicio >= @HoraFin
    BEGIN
        RAISERROR('La hora de inicio debe ser anterior a la hora de fin.', 16, 1);
        RETURN;
    END

    -- Validar superposicion (excluyendo el propio turno)
    IF EXISTS (
        SELECT 1 FROM turnos
        WHERE actividad_id = @ActividadId
          AND dia_semana   = @DiaSemana
          AND id           <> @Id
          AND activo       = 1
          AND (
                (@HoraInicio >= hora_inicio AND @HoraInicio <  hora_fin)
             OR (@HoraFin    >  hora_inicio AND @HoraFin    <= hora_fin)
             OR (@HoraInicio <= hora_inicio AND @HoraFin    >= hora_fin)
              )
    )
    BEGIN
        RAISERROR('Ya existe otro turno de esta actividad en ese horario.', 16, 1);
        RETURN;
    END

    UPDATE turnos SET
        actividad_id  = @ActividadId,
        instructor_id = @InstructorId,
        dia_semana    = @DiaSemana,
        hora_inicio   = @HoraInicio,
        hora_fin      = @HoraFin,
        cupo_maximo   = @CupoMaximo
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. ACTIVAR / DESACTIVAR
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_CambiarEstadoTurno
    @Id     BIGINT,
    @Activo BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE turnos SET activo = @Activo WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. ELIMINAR (solo si no tiene asistencias)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_EliminarTurno
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM instructor_asistencias WHERE turno_id = @Id)
    BEGIN
        RAISERROR('No se puede eliminar: el turno tiene asistencias registradas. Podes desactivarlo.', 16, 1);
        RETURN;
    END

    DELETE FROM turnos WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 8. ESTADÍSTICAS
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_EstadisticasTurnos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        COUNT(*)                                                    AS total,
        ISNULL(SUM(CASE WHEN activo = 1 THEN 1 ELSE 0 END), 0)      AS activos,
        ISNULL(SUM(CASE WHEN instructor_id IS NULL AND activo = 1
                        THEN 1 ELSE 0 END), 0)                       AS sin_instructor,
        ISNULL(SUM(CASE WHEN activo = 1 THEN cupo_maximo ELSE 0 END), 0) AS cupo_total
    FROM turnos;
END;
GO
