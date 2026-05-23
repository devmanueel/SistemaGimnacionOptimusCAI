-- ============================================================
--  STORED PROCEDURES — TABLA socios_ficha_medica
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
-- ============================================================

-- 0. CREAR TABLA (idempotente)
IF OBJECT_ID('socios_ficha_medica') IS NULL
CREATE TABLE socios_ficha_medica (
    id                    BIGINT IDENTITY(1,1) PRIMARY KEY,
    socio_id              BIGINT NOT NULL REFERENCES socios(id),
    peso_kg               DECIMAL(5,2) NULL,
    altura_cm             SMALLINT NULL,
    grupo_sanguineo       VARCHAR(5) NULL,
    enfermedades          NVARCHAR(500) NULL,
    medicamentos          NVARCHAR(500) NULL,
    restricciones_fisicas NVARCHAR(500) NULL,
    contacto_emergencia   VARCHAR(150) NULL,
    telefono_emergencia   VARCHAR(20) NULL,
    apto_fisico           BIT NOT NULL DEFAULT 0,
    fecha_apto            DATE NULL,
    observaciones         NVARCHAR(300) NULL,
    actualizado_en        DATETIME NOT NULL DEFAULT GETDATE(),
    actualizado_por       BIGINT NULL REFERENCES usuarios(id)
);
GO

-- 1. OBTENER FICHA MÉDICA POR SOCIO
IF OBJECT_ID('sp_ObtenerFichaMedica','P') IS NOT NULL DROP PROCEDURE sp_ObtenerFichaMedica;
GO
CREATE PROCEDURE sp_ObtenerFichaMedica
    @SocioId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        fm.id, fm.socio_id, fm.peso_kg, fm.altura_cm, fm.grupo_sanguineo,
        fm.enfermedades, fm.medicamentos, fm.restricciones_fisicas,
        fm.contacto_emergencia, fm.telefono_emergencia,
        fm.apto_fisico, fm.fecha_apto, fm.observaciones,
        fm.actualizado_en, fm.actualizado_por,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS actualizado_por_nombre
    FROM socios_ficha_medica fm
    LEFT JOIN usuarios u ON u.id = fm.actualizado_por
    WHERE fm.socio_id = @SocioId;
END;
GO

-- 2. GUARDAR FICHA MÉDICA (INSERT si no existe, UPDATE si existe)
IF OBJECT_ID('sp_GuardarFichaMedica','P') IS NOT NULL DROP PROCEDURE sp_GuardarFichaMedica;
GO
CREATE PROCEDURE sp_GuardarFichaMedica
    @SocioId              BIGINT,
    @PesoKg               DECIMAL(5,2)  = NULL,
    @AlturaCm             SMALLINT      = NULL,
    @GrupoSanguineo       VARCHAR(5)    = NULL,
    @Enfermedades         NVARCHAR(500) = NULL,
    @Medicamentos         NVARCHAR(500) = NULL,
    @RestriccionesFisicas NVARCHAR(500) = NULL,
    @ContactoEmergencia   VARCHAR(150)  = NULL,
    @TelefonoEmergencia   VARCHAR(20)   = NULL,
    @AptoFisico           BIT           = 0,
    @FechaApto            DATE          = NULL,
    @Observaciones        NVARCHAR(300) = NULL,
    @ActualizadoPor       BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM socios_ficha_medica WHERE socio_id = @SocioId)
    BEGIN
        UPDATE socios_ficha_medica SET
            peso_kg               = @PesoKg,
            altura_cm             = @AlturaCm,
            grupo_sanguineo       = @GrupoSanguineo,
            enfermedades          = @Enfermedades,
            medicamentos          = @Medicamentos,
            restricciones_fisicas = @RestriccionesFisicas,
            contacto_emergencia   = @ContactoEmergencia,
            telefono_emergencia   = @TelefonoEmergencia,
            apto_fisico           = @AptoFisico,
            fecha_apto            = @FechaApto,
            observaciones         = @Observaciones,
            actualizado_en        = GETDATE(),
            actualizado_por       = @ActualizadoPor
        WHERE socio_id = @SocioId;

        SELECT (SELECT id FROM socios_ficha_medica WHERE socio_id = @SocioId) AS id;
    END
    ELSE
    BEGIN
        INSERT INTO socios_ficha_medica
            (socio_id, peso_kg, altura_cm, grupo_sanguineo, enfermedades, medicamentos,
             restricciones_fisicas, contacto_emergencia, telefono_emergencia,
             apto_fisico, fecha_apto, observaciones, actualizado_por)
        VALUES
            (@SocioId, @PesoKg, @AlturaCm, @GrupoSanguineo, @Enfermedades, @Medicamentos,
             @RestriccionesFisicas, @ContactoEmergencia, @TelefonoEmergencia,
             @AptoFisico, @FechaApto, @Observaciones, @ActualizadoPor);

        SELECT SCOPE_IDENTITY() AS id;
    END
END;
GO
