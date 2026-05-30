-- ============================================================
--  spHuellas.sql — Sistema Gimnasio OptimusCAI
--  Soporte de huellas digitales con el SDK DigitalPersona.
--
--  Modelo:
--   · huella_guid     = identidad lógica del socio (la que devuelve
--                       la identificación y usa el control de acceso)
--   · huella_template = template biométrico serializado por el SDK
--                       (DPFP.Template.Bytes). Se compara 1:N en la app.
--
--  Reglas SPEC: LocalDB NO soporta CREATE OR ALTER → DROP + CREATE.
-- ============================================================

-- IMPORTANTE: la tabla socios tiene un índice filtrado
-- (IX_socios_huella_guid). SQL Server exige que todo procedimiento que
-- haga UPDATE sobre ella se CREE con QUOTED_IDENTIFIER ON y ANSI_NULLS ON;
-- la opción se "hornea" al crear el SP. Por eso fijamos la sesión acá.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Columna huella_guid (identidad lógica) -----------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'socios' AND COLUMN_NAME = 'huella_guid'
)
BEGIN
    ALTER TABLE socios ADD huella_guid UNIQUEIDENTIFIER NULL;
END
GO

-- 2. Columna huella_template (template biométrico serializado) ----------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'socios' AND COLUMN_NAME = 'huella_template'
)
BEGIN
    ALTER TABLE socios ADD huella_template VARBINARY(MAX) NULL;
END
GO

-- Índice único (sparse: solo los socios con huella registrada)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_socios_huella_guid' AND object_id = OBJECT_ID('socios')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_socios_huella_guid
        ON socios (huella_guid)
        WHERE huella_guid IS NOT NULL;
END
GO

-- ============================================================
-- 3. Guardar la huella de un socio (guid + template juntos)
-- ============================================================
IF OBJECT_ID('sp_SocioGuardarHuella', 'P') IS NOT NULL
    DROP PROCEDURE sp_SocioGuardarHuella;
GO
CREATE PROCEDURE sp_SocioGuardarHuella
    @Id         BIGINT,
    @HuellaGuid UNIQUEIDENTIFIER,
    @Template   VARBINARY(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE socios
       SET huella_guid     = @HuellaGuid,
           huella_template = @Template
     WHERE id = @Id;
END
GO

-- ============================================================
-- 4. Actualizar / borrar la huella de un socio
--    @HuellaGuid = NULL  →  borra también el template.
-- ============================================================
IF OBJECT_ID('sp_SocioActualizarHuellaGuid', 'P') IS NOT NULL
    DROP PROCEDURE sp_SocioActualizarHuellaGuid;
GO
CREATE PROCEDURE sp_SocioActualizarHuellaGuid
    @Id         BIGINT,
    @HuellaGuid UNIQUEIDENTIFIER   -- NULL para borrar la huella
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE socios
       SET huella_guid     = @HuellaGuid,
           huella_template = CASE WHEN @HuellaGuid IS NULL
                                  THEN NULL ELSE huella_template END
     WHERE id = @Id;
END
GO

-- ============================================================
-- 5. Obtener el DNI de un socio dado su GUID de huella
--    Retorna una fila (id, dni) o ninguna si no existe
-- ============================================================
IF OBJECT_ID('sp_SocioDniPorHuellaGuid', 'P') IS NOT NULL
    DROP PROCEDURE sp_SocioDniPorHuellaGuid;
GO
CREATE PROCEDURE sp_SocioDniPorHuellaGuid
    @HuellaGuid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, dni
      FROM socios
     WHERE huella_guid = @HuellaGuid
       AND activo = 1;
END
GO

-- ============================================================
-- 6. Todas las huellas activas (para la identificación 1:N)
--    Devuelve guid + template de cada socio activo enrolado.
-- ============================================================
IF OBJECT_ID('sp_SociosConHuella', 'P') IS NOT NULL
    DROP PROCEDURE sp_SociosConHuella;
GO
CREATE PROCEDURE sp_SociosConHuella
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, dni, huella_guid, huella_template
      FROM socios
     WHERE activo = 1
       AND huella_guid     IS NOT NULL
       AND huella_template IS NOT NULL;
END
GO
