-- ============================================================
--  STORED PROCEDURES — TABLA casilleros
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER TODOS (con datos del socio si está asignado)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerCasilleros
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.id, c.numero, c.socio_id, c.estado, c.precio_mes,
        c.observaciones, c.asignado_en,
        ISNULL(s.nombre + ' ' + s.apellido, '') AS socio_nombre,
        s.numero_socio,
        s.dni AS socio_dni,
        s.foto AS socio_foto
    FROM casilleros c
    LEFT JOIN socios s ON s.id = c.socio_id
    ORDER BY c.numero ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER CASILLERO POR ID
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerCasilleroPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.id, c.numero, c.socio_id, c.estado, c.precio_mes,
        c.observaciones, c.asignado_en,
        ISNULL(s.nombre + ' ' + s.apellido, '') AS socio_nombre,
        s.numero_socio,
        s.dni AS socio_dni,
        s.foto AS socio_foto
    FROM casilleros c
    LEFT JOIN socios s ON s.id = c.socio_id
    WHERE c.id = @Id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. CREAR CASILLERO INDIVIDUAL
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_CrearCasillero
    @Numero        SMALLINT,
    @PrecioMes     DECIMAL(10,2)   = NULL,
    @Observaciones NVARCHAR(300)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM casilleros WHERE numero = @Numero)
    BEGIN
        SELECT -1 AS id;
        RETURN;
    END

    INSERT INTO casilleros (numero, estado, precio_mes, observaciones)
    VALUES (@Numero, 'libre', @PrecioMes, @Observaciones);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. CREAR CASILLEROS EN MASA (de número X a Y)
--    Útil para inicializar el gimnasio: "creame del 1 al 50".
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_CrearCasillerosEnMasa
    @NumeroDesde SMALLINT,
    @NumeroHasta SMALLINT,
    @PrecioMes   DECIMAL(10,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @NumeroDesde > @NumeroHasta
    BEGIN
        RAISERROR('El número inicial debe ser menor o igual al final.', 16, 1);
        RETURN;
    END

    IF @NumeroHasta - @NumeroDesde > 200
    BEGIN
        RAISERROR('No se pueden crear más de 200 casilleros a la vez.', 16, 1);
        RETURN;
    END

    DECLARE @i SMALLINT = @NumeroDesde;
    DECLARE @creados INT = 0;

    WHILE @i <= @NumeroHasta
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM casilleros WHERE numero = @i)
        BEGIN
            INSERT INTO casilleros (numero, estado, precio_mes)
            VALUES (@i, 'libre', @PrecioMes);
            SET @creados = @creados + 1;
        END
        SET @i = @i + 1;
    END

    SELECT @creados AS creados;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. ASIGNAR CASILLERO A SOCIO
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_AsignarCasillero
    @Id            BIGINT,
    @SocioId       BIGINT,
    @Observaciones NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Validar que el casillero existe y está libre
    DECLARE @EstadoActual VARCHAR(20);
    SELECT @EstadoActual = estado FROM casilleros WHERE id = @Id;

    IF @EstadoActual IS NULL
    BEGIN
        RAISERROR('El casillero no existe.', 16, 1);
        RETURN;
    END

    IF @EstadoActual = 'ocupado'
    BEGIN
        RAISERROR('Este casillero ya está asignado a otro socio.', 16, 1);
        RETURN;
    END

    IF @EstadoActual = 'mantenimiento'
    BEGIN
        RAISERROR('El casillero está en mantenimiento. Cambiá el estado primero.', 16, 1);
        RETURN;
    END

    -- Validar que el socio existe
    IF NOT EXISTS (SELECT 1 FROM socios WHERE id = @SocioId AND eliminado_en IS NULL AND activo = 1)
    BEGIN
        RAISERROR('El socio no existe o está inactivo.', 16, 1);
        RETURN;
    END

    -- Validar que el socio no tenga ya otro casillero asignado
    IF EXISTS (SELECT 1 FROM casilleros WHERE socio_id = @SocioId AND estado = 'ocupado')
    BEGIN
        RAISERROR('Este socio ya tiene un casillero asignado. Liberalo primero.', 16, 1);
        RETURN;
    END

    UPDATE casilleros
    SET socio_id      = @SocioId,
        estado        = 'ocupado',
        asignado_en   = GETDATE(),
        observaciones = ISNULL(@Observaciones, observaciones)
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. LIBERAR CASILLERO (saca al socio asignado)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_LiberarCasillero
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE casilleros
    SET socio_id    = NULL,
        estado      = 'libre',
        asignado_en = NULL
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. CAMBIAR ESTADO (libre / mantenimiento)
--    No se usa para "ocupado" — para eso usar Asignar.
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_CambiarEstadoCasillero
    @Id     BIGINT,
    @Estado VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Estado NOT IN ('libre', 'mantenimiento')
    BEGIN
        RAISERROR('Estado inválido. Solo libre o mantenimiento.', 16, 1);
        RETURN;
    END

    -- Si lo pasamos a mantenimiento o libre, sacamos el socio
    UPDATE casilleros
    SET estado      = @Estado,
        socio_id    = NULL,
        asignado_en = NULL
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 8. ACTUALIZAR PRECIO / OBSERVACIONES
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ActualizarCasillero
    @Id            BIGINT,
    @PrecioMes     DECIMAL(10,2) = NULL,
    @Observaciones NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE casilleros
    SET precio_mes    = @PrecioMes,
        observaciones = @Observaciones
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 9. ELIMINAR CASILLERO
--    Solo permite si está libre (no se borra uno asignado).
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_EliminarCasillero
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Estado VARCHAR(20);
    SELECT @Estado = estado FROM casilleros WHERE id = @Id;

    IF @Estado IS NULL
    BEGIN
        RAISERROR('El casillero no existe.', 16, 1);
        RETURN;
    END

    IF @Estado = 'ocupado'
    BEGIN
        RAISERROR('No se puede eliminar un casillero ocupado. Liberalo primero.', 16, 1);
        RETURN;
    END

    DELETE FROM casilleros WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 10. ESTADÍSTICAS
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_EstadisticasCasilleros
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        COUNT(*)                                                              AS total,
        ISNULL(SUM(CASE WHEN estado = 'libre'         THEN 1 ELSE 0 END), 0) AS libres,
        ISNULL(SUM(CASE WHEN estado = 'ocupado'       THEN 1 ELSE 0 END), 0) AS ocupados,
        ISNULL(SUM(CASE WHEN estado = 'mantenimiento' THEN 1 ELSE 0 END), 0) AS mantenimiento,
        ISNULL(SUM(CASE WHEN estado = 'ocupado' AND precio_mes IS NOT NULL
                        THEN precio_mes ELSE 0 END), 0)                       AS ingreso_potencial_mes
    FROM casilleros;
END;
GO

-- Verificación
EXEC sp_EstadisticasCasilleros;
EXEC sp_ObtenerCasilleros;
GO
