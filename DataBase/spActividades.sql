-- ============================================================
--  STORED PROCEDURES — TABLA actividades
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
-- ============================================================

-- 1. OBTENER TODAS (con cantidad de socios activos en cada una)
CREATE OR ALTER PROCEDURE sp_ObtenerActividades
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.id, a.nombre, a.tipo, a.dias_sesiones, a.dias_semana,
           a.precio, a.activo, a.creado_en,
           (SELECT COUNT(*) FROM membresias m
            WHERE m.actividad_id = a.id AND m.estado = 'activa') AS cant_socios
    FROM actividades a
    ORDER BY a.nombre ASC;
END;
GO

-- 2. OBTENER POR ID
CREATE OR ALTER PROCEDURE sp_ObtenerActividadPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.id, a.nombre, a.tipo, a.dias_sesiones, a.dias_semana,
           a.precio, a.activo, a.creado_en,
           (SELECT COUNT(*) FROM membresias m
            WHERE m.actividad_id = a.id AND m.estado = 'activa') AS cant_socios
    FROM actividades a
    WHERE a.id = @Id;
END;
GO

-- 3. BUSCAR (texto + filtro estado)
CREATE OR ALTER PROCEDURE sp_BuscarActividades
    @Texto        NVARCHAR(100) = '',
    @FiltroEstado VARCHAR(20)   = 'todos'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.id, a.nombre, a.tipo, a.dias_sesiones, a.dias_semana,
           a.precio, a.activo, a.creado_en,
           (SELECT COUNT(*) FROM membresias m
            WHERE m.actividad_id = a.id AND m.estado = 'activa') AS cant_socios
    FROM actividades a
    WHERE (@Texto = '' OR a.nombre LIKE '%' + @Texto + '%')
      AND (@FiltroEstado = 'todos'
        OR (@FiltroEstado = 'activas'   AND a.activo = 1)
        OR (@FiltroEstado = 'inactivas' AND a.activo = 0))
    ORDER BY a.nombre ASC;
END;
GO

-- 4. INSERTAR (retorna ID o -1 si nombre duplicado)
CREATE OR ALTER PROCEDURE sp_InsertarActividad
    @Nombre       NVARCHAR(150),
    @Tipo         VARCHAR(30),
    @DiasSesiones TINYINT,
    @DiasSemana   NVARCHAR(MAX) = NULL,
    @Precio       DECIMAL(12,2)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM actividades WHERE nombre = @Nombre)
    BEGIN SELECT -1 AS id; RETURN; END

    INSERT INTO actividades (nombre, tipo, dias_sesiones, dias_semana, precio, activo)
    VALUES (@Nombre, @Tipo, @DiasSesiones, @DiasSemana, @Precio, 1);
    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- 5. MODIFICAR
CREATE OR ALTER PROCEDURE sp_ModificarActividad
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
    BEGIN RAISERROR('Ya existe otra actividad con ese nombre.', 16, 1); RETURN; END

    UPDATE actividades SET nombre = @Nombre, tipo = @Tipo,
        dias_sesiones = @DiasSesiones, dias_semana = @DiasSemana, precio = @Precio
    WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- 6. CAMBIAR ESTADO
CREATE OR ALTER PROCEDURE sp_CambiarEstadoActividad
    @Id BIGINT, @Activo BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE actividades SET activo = @Activo WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- 7. ELIMINAR (solo si no tiene membresías)
CREATE OR ALTER PROCEDURE sp_EliminarActividad
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM membresias WHERE actividad_id = @Id)
    BEGIN
        RAISERROR('No se puede eliminar: tiene membresías asociadas. Desactivala en su lugar.', 16, 1);
        RETURN;
    END
    DELETE FROM actividades WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

EXEC sp_ObtenerActividades;
GO
