-- ============================================================
--  SP_Login.sql
--  Patron: DROP + CREATE (compatible con LocalDB)
-- ============================================================

IF OBJECT_ID('sp_Login',              'P') IS NOT NULL DROP PROCEDURE sp_Login;
IF OBJECT_ID('sp_ObtenerSesionUsuario','P') IS NOT NULL DROP PROCEDURE sp_ObtenerSesionUsuario;
GO

-- ─────────────────────────────────────────────────────────────
-- 1. LOGIN
--    Recibe DNI + hash SHA-256 de la contrasena.
--    Retorna los datos del usuario si las credenciales son validas.
--    No retorna password_hash por seguridad.
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_Login
    @Dni          CHAR(8),
    @PasswordHash CHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    -- Verificar que el usuario existe y esta activo
    IF NOT EXISTS (
        SELECT 1 FROM usuarios
        WHERE dni = @Dni
          AND activo = 1
          AND eliminado_en IS NULL
    )
    BEGIN
        -- No revelar si el DNI existe o no (seguridad)
        SELECT
            CAST(0 AS BIT)          AS ok,
            'Credenciales incorrectas.' AS mensaje,
            CAST(NULL AS BIGINT)    AS id,
            CAST(NULL AS VARCHAR(100)) AS nombre,
            CAST(NULL AS VARCHAR(100)) AS apellido,
            CAST(NULL AS CHAR(8))   AS dni,
            CAST(NULL AS TINYINT)   AS rol_id,
            CAST(NULL AS VARCHAR(50)) AS rol_nombre;
        RETURN;
    END

    -- Verificar contrasena
    IF NOT EXISTS (
        SELECT 1 FROM usuarios
        WHERE dni = @Dni
          AND password_hash = @PasswordHash
          AND activo = 1
          AND eliminado_en IS NULL
    )
    BEGIN
        SELECT
            CAST(0 AS BIT)          AS ok,
            'Credenciales incorrectas.' AS mensaje,
            CAST(NULL AS BIGINT)    AS id,
            CAST(NULL AS VARCHAR(100)) AS nombre,
            CAST(NULL AS VARCHAR(100)) AS apellido,
            CAST(NULL AS CHAR(8))   AS dni,
            CAST(NULL AS TINYINT)   AS rol_id,
            CAST(NULL AS VARCHAR(50)) AS rol_nombre;
        RETURN;
    END

    -- Login exitoso
    SELECT
        CAST(1 AS BIT)     AS ok,
        'Bienvenido.'      AS mensaje,
        u.id,
        u.nombre,
        u.apellido,
        u.dni,
        u.rol_id,
        r.nombre           AS rol_nombre
    FROM usuarios u
    INNER JOIN roles r ON r.id = u.rol_id
    WHERE u.dni = @Dni
      AND u.password_hash = @PasswordHash
      AND u.activo = 1
      AND u.eliminado_en IS NULL;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER DATOS DE SESION POR ID
--    Usado al revalidar la sesion activa.
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerSesionUsuario
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.id, u.nombre, u.apellido, u.dni, u.rol_id,
        r.nombre AS rol_nombre,
        u.activo
    FROM usuarios u
    INNER JOIN roles r ON r.id = u.rol_id
    WHERE u.id = @Id
      AND u.eliminado_en IS NULL;
END;
GO