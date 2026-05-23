-- ============================================================
--  DataBase/spConfiguracion.sql
--  Tabla clave-valor para parámetros del sistema.
--  Usado por: sueldos docentes (tarifa global), datos del gimnasio.
-- ============================================================

-- ─── Tabla ───────────────────────────────────────────────────
IF OBJECT_ID('configuracion_sistema') IS NULL
CREATE TABLE configuracion_sistema (
    clave          VARCHAR(100)   NOT NULL PRIMARY KEY,
    valor          NVARCHAR(500)  NOT NULL,
    descripcion    NVARCHAR(300)  NULL,
    actualizado_en DATETIME       NOT NULL DEFAULT GETDATE(),
    actualizado_por BIGINT        NULL REFERENCES usuarios(id)
);
GO

-- ─── Semillas (solo si no existen) ───────────────────────────
IF NOT EXISTS (SELECT 1 FROM configuracion_sistema WHERE clave = 'tarifa_hora_docentes')
    INSERT INTO configuracion_sistema (clave, valor, descripcion)
    VALUES ('tarifa_hora_docentes', '4000',
            'Tarifa por hora en pesos ARS aplicada a todos los instructores');

IF NOT EXISTS (SELECT 1 FROM configuracion_sistema WHERE clave = 'nombre_gimnasio')
    INSERT INTO configuracion_sistema (clave, valor, descripcion)
    VALUES ('nombre_gimnasio', 'OptimusCAI Gym',
            'Nombre del gimnasio para reportes PDF');

IF NOT EXISTS (SELECT 1 FROM configuracion_sistema WHERE clave = 'direccion_gimnasio')
    INSERT INTO configuracion_sistema (clave, valor, descripcion)
    VALUES ('direccion_gimnasio', 'Jujuy, Argentina',
            'Dirección para encabezado PDF');

IF NOT EXISTS (SELECT 1 FROM configuracion_sistema WHERE clave = 'telefono_gimnasio')
    INSERT INTO configuracion_sistema (clave, valor, descripcion)
    VALUES ('telefono_gimnasio', '+54 388 000-0000',
            'Teléfono para encabezado PDF');
GO

-- ─── SP: leer un valor ───────────────────────────────────────
IF OBJECT_ID('sp_ObtenerConfiguracion','P') IS NOT NULL DROP PROCEDURE sp_ObtenerConfiguracion;
GO
CREATE PROCEDURE sp_ObtenerConfiguracion
    @Clave VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT clave, valor, descripcion, actualizado_en
    FROM configuracion_sistema
    WHERE clave = @Clave;
END;
GO

-- ─── SP: actualizar un valor ─────────────────────────────────
IF OBJECT_ID('sp_ActualizarConfiguracion','P') IS NOT NULL DROP PROCEDURE sp_ActualizarConfiguracion;
GO
CREATE PROCEDURE sp_ActualizarConfiguracion
    @Clave          VARCHAR(100),
    @Valor          NVARCHAR(500),
    @ActualizadoPor BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM configuracion_sistema WHERE clave = @Clave)
    BEGIN
        RAISERROR('La clave de configuración no existe.', 16, 1);
        RETURN;
    END

    UPDATE configuracion_sistema
    SET valor           = @Valor,
        actualizado_en  = GETDATE(),
        actualizado_por = @ActualizadoPor
    WHERE clave = @Clave;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO
