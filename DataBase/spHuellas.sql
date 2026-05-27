-- ============================================================
--  spHuellas.sql — Sistema Gimnasio OptimusCAI
--  Cambios en la tabla socios para soporte de huellas digitales
-- ============================================================

-- 1. Agregar columna huella_guid si no existe
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'socios' AND COLUMN_NAME = 'huella_guid'
)
BEGIN
    ALTER TABLE socios ADD huella_guid UNIQUEIDENTIFIER NULL;
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
-- 2. Actualizar / borrar el GUID de huella de un socio
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SocioActualizarHuellaGuid
    @Id       BIGINT,
    @HuellaGuid UNIQUEIDENTIFIER   -- NULL para borrar la huella
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE socios
       SET huella_guid = @HuellaGuid
     WHERE id = @Id;
END
GO

-- ============================================================
-- 3. Obtener el DNI de un socio dado su GUID de huella
--    Retorna una fila (dni, id) o ninguna si no existe
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SocioDniPorHuellaGuid
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
