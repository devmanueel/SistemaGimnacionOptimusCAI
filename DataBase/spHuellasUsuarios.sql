-- ============================================================
--  spHuellasUsuarios.sql — Sistema Gimnasio OptimusCAI
--  Soporte de huellas digitales para USUARIOS (docentes/instructores)
--  con el SDK DigitalPersona. Mismo modelo que spHuellas.sql (socios):
--
--   · huella_guid     = identidad lógica del docente (la que devuelve
--                       la identificación y usa el fichaje de asistencia)
--   · huella_template = template biométrico serializado por el SDK
--                       (DPFP.Template.Bytes). Se compara 1:N en la app.
--
--  Reglas de negocio: solo los docentes (rol_id = 2) registran huella,
--  porque son los únicos que fichan asistencia (un docente no puede ser
--  socio — ver sp_BuscarPersonaPorDni).
--
--  Reglas SPEC: LocalDB NO soporta CREATE OR ALTER → DROP + CREATE.
--
--  IMPORTANTE: ejecutar este script ANTES que spUsuario.sql, ya que las
--  vistas/SP de usuarios devuelven la columna huella_guid.
-- ============================================================

-- La tabla usuarios tendrá un índice filtrado (IX_usuarios_huella_guid).
-- SQL Server exige QUOTED_IDENTIFIER ON y ANSI_NULLS ON para todo SP que
-- haga UPDATE sobre ella; la opción se "hornea" al crear el SP.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Columna huella_guid (identidad lógica) -----------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'usuarios' AND COLUMN_NAME = 'huella_guid'
)
BEGIN
    ALTER TABLE usuarios ADD huella_guid UNIQUEIDENTIFIER NULL;
END
GO

-- 2. Columna huella_template (template biométrico serializado) ----------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'usuarios' AND COLUMN_NAME = 'huella_template'
)
BEGIN
    ALTER TABLE usuarios ADD huella_template VARBINARY(MAX) NULL;
END
GO

-- Índice único (sparse: solo los usuarios con huella registrada)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_usuarios_huella_guid' AND object_id = OBJECT_ID('usuarios')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_usuarios_huella_guid
        ON usuarios (huella_guid)
        WHERE huella_guid IS NOT NULL;
END
GO

-- ============================================================
-- 3. Guardar la huella de un usuario (guid + template juntos)
-- ============================================================
IF OBJECT_ID('sp_UsuarioGuardarHuella', 'P') IS NOT NULL
    DROP PROCEDURE sp_UsuarioGuardarHuella;
GO
CREATE PROCEDURE sp_UsuarioGuardarHuella
    @Id         BIGINT,
    @HuellaGuid UNIQUEIDENTIFIER,
    @Template   VARBINARY(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE usuarios
       SET huella_guid     = @HuellaGuid,
           huella_template = @Template
     WHERE id = @Id;
END
GO

-- ============================================================
-- 4. Actualizar / borrar la huella de un usuario
--    @HuellaGuid = NULL  →  borra también el template.
-- ============================================================
IF OBJECT_ID('sp_UsuarioActualizarHuellaGuid', 'P') IS NOT NULL
    DROP PROCEDURE sp_UsuarioActualizarHuellaGuid;
GO
CREATE PROCEDURE sp_UsuarioActualizarHuellaGuid
    @Id         BIGINT,
    @HuellaGuid UNIQUEIDENTIFIER   -- NULL para borrar la huella
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE usuarios
       SET huella_guid     = @HuellaGuid,
           huella_template = CASE WHEN @HuellaGuid IS NULL
                                  THEN NULL ELSE huella_template END
     WHERE id = @Id;
END
GO

-- ============================================================
-- 5. Obtener el DNI de un docente dado su GUID de huella
--    Solo docentes activos (rol_id = 2). Retorna (id, dni) o nada.
-- ============================================================
IF OBJECT_ID('sp_UsuarioDniPorHuellaGuid', 'P') IS NOT NULL
    DROP PROCEDURE sp_UsuarioDniPorHuellaGuid;
GO
CREATE PROCEDURE sp_UsuarioDniPorHuellaGuid
    @HuellaGuid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, dni
      FROM usuarios
     WHERE huella_guid = @HuellaGuid
       AND rol_id = 2
       AND activo = 1
       AND eliminado_en IS NULL;
END
GO

-- ============================================================
-- 6. Todas las huellas de docentes activos (identificación 1:N)
--    Devuelve guid + template de cada docente enrolado.
-- ============================================================
IF OBJECT_ID('sp_UsuariosConHuella', 'P') IS NOT NULL
    DROP PROCEDURE sp_UsuariosConHuella;
GO
CREATE PROCEDURE sp_UsuariosConHuella
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, dni, huella_guid, huella_template
      FROM usuarios
     WHERE rol_id = 2
       AND activo = 1
       AND eliminado_en IS NULL
       AND huella_guid     IS NOT NULL
       AND huella_template IS NOT NULL;
END
GO
