-- ============================================================
--  STORED PROCEDURES - TABLA usuarios
--  Sistema Gimnasio OptimusCAI
--  SQL Server / LocalDB
-- ============================================================

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'usuarios' AND COLUMN_NAME = 'foto'
)
BEGIN
    ALTER TABLE usuarios ADD foto VARBINARY(MAX) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('usuarios') AND name = 'tarifa_hora'
)
    ALTER TABLE usuarios ADD tarifa_hora DECIMAL(10,2) NOT NULL DEFAULT 0;
GO

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER TODOS LOS USUARIOS ACTIVOS
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerUsuarios
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.id,
        u.nombre,
        u.apellido,
        u.dni,
        u.domicilio,
        u.telefono,
        u.email,
        u.password_hash,
        u.foto,
        u.activo,
        u.rol_id,
        r.nombre AS rol_nombre,
        u.creado_en,
        u.tarifa_hora
    FROM usuarios u
    INNER JOIN roles r ON r.id = u.rol_id
    WHERE u.eliminado_en IS NULL
    ORDER BY u.apellido ASC, u.nombre ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER USUARIO POR ID
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerUsuarioPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.id,
        u.nombre,
        u.apellido,
        u.dni,
        u.domicilio,
        u.telefono,
        u.email,
        u.password_hash,
        u.foto,
        u.activo,
        u.rol_id,
        r.nombre AS rol_nombre,
        u.creado_en,
        u.tarifa_hora
    FROM usuarios u
    INNER JOIN roles r ON r.id = u.rol_id
    WHERE u.id = @Id AND u.eliminado_en IS NULL;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. BUSCAR USUARIOS
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_BuscarUsuarios
    @Texto NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.id,
        u.nombre,
        u.apellido,
        u.dni,
        u.domicilio,
        u.telefono,
        u.email,
        u.password_hash,
        u.foto,
        u.activo,
        u.rol_id,
        r.nombre AS rol_nombre,
        u.creado_en,
        u.tarifa_hora
    FROM usuarios u
    INNER JOIN roles r ON r.id = u.rol_id
    WHERE u.eliminado_en IS NULL
      AND (
           u.nombre    LIKE '%' + @Texto + '%'
        OR u.apellido  LIKE '%' + @Texto + '%'
        OR u.dni       LIKE '%' + @Texto + '%'
      )
    ORDER BY u.apellido ASC, u.nombre ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. VALIDAR LOGIN
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ValidarUsuario
    @Dni          NVARCHAR(8),
    @PasswordHash CHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.id,
        u.nombre,
        u.apellido,
        u.dni,
        u.email,
        u.foto,
        u.activo,
        u.rol_id,
        r.nombre AS rol_nombre
    FROM usuarios u
    INNER JOIN roles r ON r.id = u.rol_id
    WHERE u.dni           = @Dni
      AND u.password_hash = @PasswordHash
      AND u.activo        = 1
      AND u.eliminado_en IS NULL;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. INSERTAR USUARIO
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_InsertarUsuario
    @RolId        TINYINT,
    @Nombre       NVARCHAR(100),
    @Apellido     NVARCHAR(100),
    @Dni          CHAR(8),
    @Domicilio    NVARCHAR(200)    = NULL,
    @Telefono     NVARCHAR(20)     = NULL,
    @Email        NVARCHAR(191)    = NULL,
    @PasswordHash CHAR(64),
    @Foto         VARBINARY(MAX)   = NULL,
    @TarifaHora   DECIMAL(10,2)    = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1 FROM usuarios
        WHERE dni = @Dni AND eliminado_en IS NULL
    )
    BEGIN
        SELECT -1 AS id;
        RETURN;
    END

    INSERT INTO usuarios
        (rol_id, nombre, apellido, dni, domicilio, telefono, email, password_hash, foto, activo, tarifa_hora)
    VALUES
        (@RolId, @Nombre, @Apellido, @Dni, @Domicilio, @Telefono, @Email, @PasswordHash, @Foto, 1, @TarifaHora);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. MODIFICAR USUARIO
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ModificarUsuario
    @Id           BIGINT,
    @RolId        TINYINT,
    @Nombre       NVARCHAR(100),
    @Apellido     NVARCHAR(100),
    @Dni          CHAR(8),
    @Domicilio    NVARCHAR(200)    = NULL,
    @Telefono     NVARCHAR(20)     = NULL,
    @Email        NVARCHAR(191)    = NULL,
    @PasswordHash CHAR(64)         = NULL,
    @Foto         VARBINARY(MAX)   = NULL,
    @TarifaHora   DECIMAL(10,2)    = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1 FROM usuarios
        WHERE dni = @Dni AND id <> @Id AND eliminado_en IS NULL
    )
    BEGIN
        RAISERROR('El DNI ya está siendo utilizado por otro usuario.', 16, 1);
        RETURN;
    END

    UPDATE usuarios SET
        rol_id        = @RolId,
        nombre        = @Nombre,
        apellido      = @Apellido,
        dni           = @Dni,
        domicilio     = ISNULL(@Domicilio, domicilio),
        telefono      = ISNULL(@Telefono,  telefono),
        email         = ISNULL(@Email,     email),
        password_hash = ISNULL(@PasswordHash, password_hash),
        foto          = ISNULL(@Foto, foto),
        tarifa_hora   = ISNULL(@TarifaHora, tarifa_hora),
        actualizado_en = GETDATE()
    WHERE id = @Id AND eliminado_en IS NULL;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. CAMBIAR ESTADO
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_CambiarEstadoUsuario
    @Id     BIGINT,
    @Activo BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE usuarios
    SET activo         = @Activo,
        actualizado_en = GETDATE()
    WHERE id = @Id AND eliminado_en IS NULL;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 8. ELIMINAR (soft-delete)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_EliminarUsuario
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE usuarios
    SET eliminado_en   = GETDATE(),
        activo         = 0,
        actualizado_en = GETDATE()
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 9. CAMBIAR CONTRASEÑA
-- ─────────────────────────────────────────────────────────────
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

IF NOT EXISTS (SELECT 1 FROM usuarios WHERE dni = '00000001')
BEGIN
    DECLARE @HashAdmin CHAR(64);
    SET @HashAdmin = CONVERT(CHAR(64), HASHBYTES('SHA2_256', 'admin123'), 2);

    EXEC sp_InsertarUsuario
        @RolId        = 1,
        @Nombre       = 'Super',
        @Apellido     = 'Administrador',
        @Dni          = '00000001',
        @Email        = 'admin@gym.com',
        @PasswordHash = @HashAdmin,
        @Foto         = NULL;
END
GO

EXEC sp_ObtenerUsuarios;
GO
