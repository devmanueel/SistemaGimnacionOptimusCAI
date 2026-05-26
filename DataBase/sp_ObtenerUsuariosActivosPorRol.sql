-- ============================================================
--  STORED PROCEDURE — Obtener usuarios activos por rol
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
-- ============================================================

IF OBJECT_ID('sp_ObtenerUsuariosActivosPorRol', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerUsuariosActivosPorRol;
GO

CREATE PROCEDURE sp_ObtenerUsuariosActivosPorRol
    @RolId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.id,
        u.rol_id,
        r.nombre AS rol_nombre,
        u.nombre,
        u.apellido,
        u.dni,
        u.domicilio,
        u.telefono,
        u.email,
        u.password_hash,
        u.foto,
        u.activo,
        u.tarifa_hora
    FROM usuarios u
    INNER JOIN roles r ON r.id = u.rol_id
    WHERE u.activo = 1
      AND u.rol_id = @RolId
    ORDER BY u.apellido, u.nombre;
END;
GO
