-- ============================================================
--  STORED PROCEDURES - TABLA actividades
--  Sistema Gimnasio OptimusCAI - SQL Server / LocalDB
-- ============================================================

IF OBJECT_ID('sp_ObtenerActividades', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerActividades;
GO
CREATE PROCEDURE sp_ObtenerActividades
AS
BEGIN
    SET NOCOUNT ON;

    SELECT a.id, a.nombre, a.tipo, a.dias_sesiones, a.dias_semana,
           a.precio, a.activo, a.creado_en,
           (SELECT COUNT(DISTINCT m.socio_id) FROM membresias m
            WHERE m.actividad_id = a.id AND m.estado = 'activa') AS cant_socios
    FROM actividades a
    ORDER BY a.nombre ASC;
END;
GO

IF OBJECT_ID('sp_ObtenerActividadesActivas', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerActividadesActivas;
GO
CREATE PROCEDURE sp_ObtenerActividadesActivas
AS
BEGIN
    SET NOCOUNT ON;

    SELECT a.id, a.nombre, a.tipo, a.dias_sesiones, a.dias_semana,
           a.precio, a.activo, a.creado_en,
           (SELECT COUNT(DISTINCT m.socio_id) FROM membresias m
            WHERE m.actividad_id = a.id AND m.estado = 'activa') AS cant_socios
    FROM actividades a
    WHERE a.activo = 1
    ORDER BY a.nombre ASC;
END;
GO

IF OBJECT_ID('sp_ObtenerActividadPorId', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerActividadPorId;
GO
CREATE PROCEDURE sp_ObtenerActividadPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT a.id, a.nombre, a.tipo, a.dias_sesiones, a.dias_semana,
           a.precio, a.activo, a.creado_en,
           (SELECT COUNT(DISTINCT m.socio_id) FROM membresias m
            WHERE m.actividad_id = a.id AND m.estado = 'activa') AS cant_socios
    FROM actividades a
    WHERE a.id = @Id;
END;
GO

IF OBJECT_ID('sp_BuscarActividades', 'P') IS NOT NULL
    DROP PROCEDURE sp_BuscarActividades;
GO
CREATE PROCEDURE sp_BuscarActividades
    @Texto        NVARCHAR(100) = '',
    @FiltroEstado VARCHAR(20)   = 'todos'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT a.id, a.nombre, a.tipo, a.dias_sesiones, a.dias_semana,
           a.precio, a.activo, a.creado_en,
           (SELECT COUNT(DISTINCT m.socio_id) FROM membresias m
            WHERE m.actividad_id = a.id AND m.estado = 'activa') AS cant_socios
    FROM actividades a
    WHERE (@Texto = '' OR a.nombre LIKE '%' + @Texto + '%')
      AND (@FiltroEstado = 'todos'
        OR (@FiltroEstado = 'activas'   AND a.activo = 1)
        OR (@FiltroEstado = 'inactivas' AND a.activo = 0))
    ORDER BY a.nombre ASC;
END;
GO

IF OBJECT_ID('sp_InsertarActividad', 'P') IS NOT NULL
    DROP PROCEDURE sp_InsertarActividad;
GO
CREATE PROCEDURE sp_InsertarActividad
    @Nombre       NVARCHAR(150),
    @Tipo         VARCHAR(30),
    @DiasSesiones TINYINT,
    @DiasSemana   NVARCHAR(MAX) = NULL,
    @Precio       DECIMAL(12,2)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM actividades WHERE nombre = @Nombre)
    BEGIN
        SELECT -1 AS id;
        RETURN;
    END

    INSERT INTO actividades
        (nombre, tipo, dias_sesiones, dias_semana, precio, activo)
    VALUES
        (@Nombre, @Tipo, @DiasSesiones, @DiasSemana, @Precio, 1);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

IF OBJECT_ID('sp_ModificarActividad', 'P') IS NOT NULL
    DROP PROCEDURE sp_ModificarActividad;
GO
CREATE PROCEDURE sp_ModificarActividad
    @Id           BIGINT,
    @Nombre       NVARCHAR(150),
    @Tipo         VARCHAR(30),
    @DiasSesiones TINYINT,
    @DiasSemana   NVARCHAR(MAX) = NULL,
    @Precio       DECIMAL(12,2)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM actividades WHERE nombre = @Nombre AND id <> @Id)
    BEGIN
        RAISERROR('Ya existe otra actividad con ese nombre.', 16, 1);
        RETURN;
    END

    UPDATE actividades
    SET nombre = @Nombre,
        tipo = @Tipo,
        dias_sesiones = @DiasSesiones,
        dias_semana = @DiasSemana,
        precio = @Precio
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

IF OBJECT_ID('sp_CambiarEstadoActividad', 'P') IS NOT NULL
    DROP PROCEDURE sp_CambiarEstadoActividad;
GO
CREATE PROCEDURE sp_CambiarEstadoActividad
    @Id BIGINT,
    @Activo BIT
AS
BEGIN
    SET NOCOUNT ON;

    -- Al DAR DE BAJA (desactivar) una actividad, no permitir si tiene
    -- socios activos (membresías en estado 'activa'). Primero hay que dar
    -- de baja a esos socios o cambiarles la membresía.
    IF @Activo = 0
    BEGIN
        DECLARE @SociosActivos INT;
        SELECT @SociosActivos = COUNT(DISTINCT m.socio_id)
        FROM membresias m
        WHERE m.actividad_id = @Id AND m.estado = 'activa';

        IF @SociosActivos > 0
        BEGIN
            DECLARE @Msg NVARCHAR(400) =
                N'No se puede dar de baja la actividad: tiene ' +
                CAST(@SociosActivos AS NVARCHAR(10)) +
                N' socio(s) activo(s). Primero dá de baja a esos socios ' +
                N'(o cambiá su membresía a otra actividad).';
            RAISERROR(@Msg, 16, 1);
            RETURN;
        END
    END

    UPDATE actividades SET activo = @Activo WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

IF OBJECT_ID('sp_EliminarActividad', 'P') IS NOT NULL
    DROP PROCEDURE sp_EliminarActividad;
GO
CREATE PROCEDURE sp_EliminarActividad
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM membresias WHERE actividad_id = @Id)
    BEGIN
        RAISERROR('No se puede eliminar: tiene membresias asociadas. Desactivala en su lugar.', 16, 1);
        RETURN;
    END

    DELETE FROM actividades WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO
