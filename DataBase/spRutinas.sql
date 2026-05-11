-- ============================================================
--  SP_Rutinas.sql
--  CRUD de rutinas + bloques + ejercicios + asignaciones a socios
-- ============================================================

IF OBJECT_ID('sp_ObtenerRutinas',          'P') IS NOT NULL DROP PROCEDURE sp_ObtenerRutinas;
IF OBJECT_ID('sp_ObtenerRutinaConDetalle', 'P') IS NOT NULL DROP PROCEDURE sp_ObtenerRutinaConDetalle;
IF OBJECT_ID('sp_BuscarRutinas',           'P') IS NOT NULL DROP PROCEDURE sp_BuscarRutinas;
IF OBJECT_ID('sp_InsertarRutina',          'P') IS NOT NULL DROP PROCEDURE sp_InsertarRutina;
IF OBJECT_ID('sp_ModificarRutina',         'P') IS NOT NULL DROP PROCEDURE sp_ModificarRutina;
IF OBJECT_ID('sp_EliminarRutina',          'P') IS NOT NULL DROP PROCEDURE sp_EliminarRutina;
IF OBJECT_ID('sp_CambiarEstadoRutina',     'P') IS NOT NULL DROP PROCEDURE sp_CambiarEstadoRutina;

IF OBJECT_ID('sp_InsertarBloque',          'P') IS NOT NULL DROP PROCEDURE sp_InsertarBloque;
IF OBJECT_ID('sp_ModificarBloque',         'P') IS NOT NULL DROP PROCEDURE sp_ModificarBloque;
IF OBJECT_ID('sp_EliminarBloque',          'P') IS NOT NULL DROP PROCEDURE sp_EliminarBloque;

IF OBJECT_ID('sp_InsertarEjercicio',       'P') IS NOT NULL DROP PROCEDURE sp_InsertarEjercicio;
IF OBJECT_ID('sp_ModificarEjercicio',      'P') IS NOT NULL DROP PROCEDURE sp_ModificarEjercicio;
IF OBJECT_ID('sp_EliminarEjercicio',       'P') IS NOT NULL DROP PROCEDURE sp_EliminarEjercicio;

IF OBJECT_ID('sp_AsignarRutina',           'P') IS NOT NULL DROP PROCEDURE sp_AsignarRutina;
IF OBJECT_ID('sp_DesasignarRutina',        'P') IS NOT NULL DROP PROCEDURE sp_DesasignarRutina;
IF OBJECT_ID('sp_AsignacionesDeRutina',    'P') IS NOT NULL DROP PROCEDURE sp_AsignacionesDeRutina;

IF OBJECT_ID('sp_EstadisticasRutinas',     'P') IS NOT NULL DROP PROCEDURE sp_EstadisticasRutinas;
GO

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER RUTINAS (lista)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerRutinas
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        r.id, r.nombre, r.detalles, r.duracion_semanas,
        r.creado_por, r.activo, r.creado_en, r.actualizado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS creador_nombre,
        (SELECT COUNT(*) FROM rutina_bloques     b WHERE b.rutina_id = r.id) AS total_bloques,
        (SELECT COUNT(*) FROM rutina_ejercicios  e
            INNER JOIN rutina_bloques b ON b.id = e.bloque_id
            WHERE b.rutina_id = r.id) AS total_ejercicios,
        (SELECT COUNT(*) FROM rutina_asignaciones a WHERE a.rutina_id = r.id) AS total_asignaciones
    FROM rutinas r
    LEFT JOIN usuarios u ON u.id = r.creado_por
    ORDER BY r.actualizado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER RUTINA CON DETALLE (3 resultsets: rutina, bloques, ejercicios)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerRutinaConDetalle
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- Rutina
    SELECT
        r.id, r.nombre, r.detalles, r.duracion_semanas,
        r.creado_por, r.activo, r.creado_en, r.actualizado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS creador_nombre
    FROM rutinas r
    LEFT JOIN usuarios u ON u.id = r.creado_por
    WHERE r.id = @Id;

    -- Bloques
    SELECT id, rutina_id, nombre, orden
    FROM rutina_bloques
    WHERE rutina_id = @Id
    ORDER BY orden ASC, id ASC;

    -- Ejercicios (de todos los bloques de esta rutina)
    SELECT
        e.id, e.bloque_id, e.nombre, e.series, e.repeticiones,
        e.peso, e.descanso_seg, e.notas, e.link_video, e.orden
    FROM rutina_ejercicios e
    INNER JOIN rutina_bloques b ON b.id = e.bloque_id
    WHERE b.rutina_id = @Id
    ORDER BY e.bloque_id ASC, e.orden ASC, e.id ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. BUSCAR
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_BuscarRutinas
    @Texto       NVARCHAR(150) = '',
    @SoloActivas BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        r.id, r.nombre, r.detalles, r.duracion_semanas,
        r.creado_por, r.activo, r.creado_en, r.actualizado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS creador_nombre,
        (SELECT COUNT(*) FROM rutina_bloques    b WHERE b.rutina_id = r.id) AS total_bloques,
        (SELECT COUNT(*) FROM rutina_ejercicios e
            INNER JOIN rutina_bloques b ON b.id = e.bloque_id
            WHERE b.rutina_id = r.id) AS total_ejercicios,
        (SELECT COUNT(*) FROM rutina_asignaciones a WHERE a.rutina_id = r.id) AS total_asignaciones
    FROM rutinas r
    LEFT JOIN usuarios u ON u.id = r.creado_por
    WHERE (@Texto = ''
           OR r.nombre   LIKE '%' + @Texto + '%'
           OR r.detalles LIKE '%' + @Texto + '%')
      AND (@SoloActivas = 0 OR r.activo = 1)
    ORDER BY r.actualizado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. INSERTAR RUTINA
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_InsertarRutina
    @Nombre           VARCHAR(150),
    @Detalles         NVARCHAR(MAX) = NULL,
    @DuracionSemanas  TINYINT       = 4,
    @CreadoPor        BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO rutinas (nombre, detalles, duracion_semanas, creado_por, activo)
    VALUES (@Nombre, @Detalles, @DuracionSemanas, @CreadoPor, 1);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. MODIFICAR RUTINA
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ModificarRutina
    @Id              BIGINT,
    @Nombre          VARCHAR(150),
    @Detalles        NVARCHAR(MAX) = NULL,
    @DuracionSemanas TINYINT       = 4
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE rutinas SET
        nombre           = @Nombre,
        detalles         = @Detalles,
        duracion_semanas = @DuracionSemanas,
        actualizado_en   = GETDATE()
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. ELIMINAR RUTINA (cascada manual)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_EliminarRutina
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;
    BEGIN TRY
        -- Borrar ejercicios de los bloques de esta rutina
        DELETE e FROM rutina_ejercicios e
        INNER JOIN rutina_bloques b ON b.id = e.bloque_id
        WHERE b.rutina_id = @Id;

        -- Borrar bloques
        DELETE FROM rutina_bloques WHERE rutina_id = @Id;

        -- Borrar asignaciones
        DELETE FROM rutina_asignaciones WHERE rutina_id = @Id;

        -- Borrar rutina
        DELETE FROM rutinas WHERE id = @Id;

        COMMIT TRANSACTION;
        SELECT 1 AS ok;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @Err NVARCHAR(2000);
        SET @Err = ERROR_MESSAGE();
        RAISERROR(@Err, 16, 1);
    END CATCH
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. CAMBIAR ESTADO RUTINA
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_CambiarEstadoRutina
    @Id     BIGINT,
    @Activo BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE rutinas SET activo = @Activo, actualizado_en = GETDATE()
    WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ═════════════════════════════════════════════════════════════
--   BLOQUES
-- ═════════════════════════════════════════════════════════════

CREATE PROCEDURE sp_InsertarBloque
    @RutinaId BIGINT,
    @Nombre   VARCHAR(100),
    @Orden    TINYINT = 1
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO rutina_bloques (rutina_id, nombre, orden)
    VALUES (@RutinaId, @Nombre, @Orden);

    UPDATE rutinas SET actualizado_en = GETDATE() WHERE id = @RutinaId;
    SELECT SCOPE_IDENTITY() AS id;
END;
GO

CREATE PROCEDURE sp_ModificarBloque
    @Id     BIGINT,
    @Nombre VARCHAR(100),
    @Orden  TINYINT = 1
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE rutina_bloques SET nombre = @Nombre, orden = @Orden WHERE id = @Id;

    -- Actualizar timestamp de la rutina padre
    UPDATE rutinas SET actualizado_en = GETDATE()
    WHERE id = (SELECT rutina_id FROM rutina_bloques WHERE id = @Id);

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

CREATE PROCEDURE sp_EliminarBloque
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @RutinaId BIGINT;
    SELECT @RutinaId = rutina_id FROM rutina_bloques WHERE id = @Id;

    DELETE FROM rutina_ejercicios WHERE bloque_id = @Id;
    DELETE FROM rutina_bloques    WHERE id = @Id;

    UPDATE rutinas SET actualizado_en = GETDATE() WHERE id = @RutinaId;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ═════════════════════════════════════════════════════════════
--   EJERCICIOS
-- ═════════════════════════════════════════════════════════════

CREATE PROCEDURE sp_InsertarEjercicio
    @BloqueId     BIGINT,
    @Nombre       VARCHAR(150),
    @Series       TINYINT      = NULL,
    @Repeticiones VARCHAR(50)  = NULL,
    @Peso         VARCHAR(50)  = NULL,
    @DescansoSeg  SMALLINT     = NULL,
    @Notas        VARCHAR(500) = NULL,
    @LinkVideo    VARCHAR(500) = NULL,
    @Orden        TINYINT      = 1
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO rutina_ejercicios
        (bloque_id, nombre, series, repeticiones, peso, descanso_seg, notas, link_video, orden)
    VALUES
        (@BloqueId, @Nombre, @Series, @Repeticiones, @Peso, @DescansoSeg, @Notas, @LinkVideo, @Orden);

    -- Actualizar timestamp rutina
    UPDATE rutinas SET actualizado_en = GETDATE()
    WHERE id = (SELECT rutina_id FROM rutina_bloques WHERE id = @BloqueId);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

CREATE PROCEDURE sp_ModificarEjercicio
    @Id           BIGINT,
    @Nombre       VARCHAR(150),
    @Series       TINYINT      = NULL,
    @Repeticiones VARCHAR(50)  = NULL,
    @Peso         VARCHAR(50)  = NULL,
    @DescansoSeg  SMALLINT     = NULL,
    @Notas        VARCHAR(500) = NULL,
    @LinkVideo    VARCHAR(500) = NULL,
    @Orden        TINYINT      = 1
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE rutina_ejercicios SET
        nombre       = @Nombre,
        series       = @Series,
        repeticiones = @Repeticiones,
        peso         = @Peso,
        descanso_seg = @DescansoSeg,
        notas        = @Notas,
        link_video   = @LinkVideo,
        orden        = @Orden
    WHERE id = @Id;

    -- Actualizar timestamp rutina
    UPDATE rutinas SET actualizado_en = GETDATE()
    WHERE id = (SELECT b.rutina_id FROM rutina_bloques b
                INNER JOIN rutina_ejercicios e ON e.bloque_id = b.id
                WHERE e.id = @Id);

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

CREATE PROCEDURE sp_EliminarEjercicio
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RutinaId BIGINT;
    SELECT @RutinaId = b.rutina_id FROM rutina_bloques b
    INNER JOIN rutina_ejercicios e ON e.bloque_id = b.id
    WHERE e.id = @Id;

    DELETE FROM rutina_ejercicios WHERE id = @Id;

    UPDATE rutinas SET actualizado_en = GETDATE() WHERE id = @RutinaId;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ═════════════════════════════════════════════════════════════
--   ASIGNACIONES A SOCIOS
-- ═════════════════════════════════════════════════════════════

CREATE PROCEDURE sp_AsignarRutina
    @RutinaId    BIGINT,
    @SocioId     BIGINT,
    @AsignadoPor BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- No duplicar
    IF EXISTS (SELECT 1 FROM rutina_asignaciones
               WHERE rutina_id = @RutinaId AND socio_id = @SocioId)
    BEGIN
        RAISERROR('El socio ya tiene asignada esta rutina.', 16, 1);
        RETURN;
    END

    INSERT INTO rutina_asignaciones (rutina_id, socio_id, asignado_por, enviado_wp)
    VALUES (@RutinaId, @SocioId, @AsignadoPor, 0);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

CREATE PROCEDURE sp_DesasignarRutina
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM rutina_asignaciones WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

CREATE PROCEDURE sp_AsignacionesDeRutina
    @RutinaId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        a.id, a.rutina_id, a.socio_id, a.asignado_por, a.enviado_wp, a.asignado_en,
        s.nombre + ' ' + s.apellido AS socio_nombre,
        s.numero_socio,
        s.foto                       AS socio_foto,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS asignado_por_nombre
    FROM rutina_asignaciones a
    INNER JOIN socios   s ON s.id = a.socio_id
    LEFT  JOIN usuarios u ON u.id = a.asignado_por
    WHERE a.rutina_id = @RutinaId
    ORDER BY a.asignado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- ESTADÍSTICAS
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_EstadisticasRutinas
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(*) FROM rutinas)                                AS total,
        (SELECT COUNT(*) FROM rutinas WHERE activo = 1)               AS activas,
        (SELECT COUNT(*) FROM rutina_ejercicios)                      AS total_ejercicios,
        (SELECT COUNT(*) FROM rutina_asignaciones)                    AS total_asignaciones,
        (SELECT COUNT(DISTINCT socio_id) FROM rutina_asignaciones)    AS socios_asignados;
END;
GO