-- ============================================================
--  STORED PROCEDURES - TABLA usuarios
--  Sistema Gimnasio OptimusCAI
--  SQL Server / LocalDB
--
--  EJECUTAR ESTE SCRIPT UNA SOLA VEZ sobre tu .mdf
--  Luego agregar la columna Foto si no la tiene la tabla:
-- ============================================================

-- Si la columna Foto no existe aún, agregarla:

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'usuarios' AND COLUMN_NAME = 'foto'
)
BEGIN
    ALTER TABLE usuarios ADD foto VARBINARY(MAX) NULL;
END
GO

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER TODOS LOS USUARIOS ACTIVOS
--    Trae todos sin soft-delete, ordenados alfabéticamente.
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
        u.creado_en
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
        u.creado_en
    FROM usuarios u
    INNER JOIN roles r ON r.id = u.rol_id
    WHERE u.id = @Id AND u.eliminado_en IS NULL;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. BUSCAR USUARIOS  (por nombre, apellido o DNI)
--    Usado por la barra de búsqueda en tiempo real.
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
        u.creado_en
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
--    Verifica DNI + password_hash (SHA-256 del DNI o la clave).
--    Si no existe o está inactivo, no devuelve filas.
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
--    Retorna el nuevo ID generado.
--    Retorna -1 si el DNI ya existe.
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
    @Foto         VARBINARY(MAX)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Verificar DNI duplicado
    IF EXISTS (
        SELECT 1 FROM usuarios
        WHERE dni = @Dni AND eliminado_en IS NULL
    )
    BEGIN
        SELECT -1 AS id;   -- Señal de duplicado para manejar en C#
        RETURN;
    END

    INSERT INTO usuarios
        (rol_id, nombre, apellido, dni, domicilio, telefono, email, password_hash, foto, activo)
    VALUES
        (@RolId, @Nombre, @Apellido, @Dni, @Domicilio, @Telefono, @Email, @PasswordHash, @Foto, 1);

    SELECT SCOPE_IDENTITY() AS id;  -- Devuelve el ID generado
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. MODIFICAR USUARIO
--    Si se pasa @Foto = NULL, mantiene la foto existente.
--    Si @PasswordHash = NULL, mantiene la contraseña existente.
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
    @Foto         VARBINARY(MAX)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Verificar que el DNI no esté usado por OTRO usuario
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
        -- Solo actualiza password si se pasa uno nuevo
        password_hash = ISNULL(@PasswordHash, password_hash),
        -- Solo actualiza foto si se pasa una nueva
        foto          = ISNULL(@Foto, foto),
        actualizado_en = GETDATE()
    WHERE id = @Id AND eliminado_en IS NULL;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. CAMBIAR ESTADO (activar / desactivar)
--    Eliminación lógica: pone Activo = 0.
--    Reactivación:       pone Activo = 1.
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
-- 8. ELIMINAR (soft-delete real)
--    Pone eliminado_en = GETDATE() en vez de borrar la fila.
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
-- DATO INICIAL: Usuario Administrador
--   Ejecutar una sola vez para tener acceso al sistema.
--   password_hash es SHA-256 de 'admin123'
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM usuarios WHERE dni = '00000001')
BEGIN
    -- 1. Declaramos una variable y calculamos el Hash primero
    DECLARE @HashAdmin CHAR(64);
    SET @HashAdmin = CONVERT(CHAR(64), HASHBYTES('SHA2_256', 'admin123'), 2);

    -- 2. Ahora sí ejecutamos el procedure pasándole la variable
    EXEC sp_InsertarUsuario
        @RolId        = 1,         -- 1 = admin
        @Nombre       = 'Super',
        @Apellido     = 'Administrador',
        @Dni          = '00000001',
        @Email        = 'admin@gym.com',
        @PasswordHash = @HashAdmin,  -- Usamos la variable aquí
        @Foto         = NULL;
END
GO

-- Verificar que quedaron bien:
EXEC sp_ObtenerUsuarios;
GO