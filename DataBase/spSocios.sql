-- ============================================================
--  STORED PROCEDURES — TABLA socios
--  Sistema Gimnasio OptimusCAI
--  SQL Server / LocalDB
-- ============================================================

-- Agregar columna foto (VARBINARY) si no existe
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'socios' AND COLUMN_NAME = 'foto'
)
BEGIN
    ALTER TABLE socios ADD foto VARBINARY(MAX) NULL;
END
GO

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER TODOS LOS SOCIOS
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerSocios
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        s.id, s.numero_socio, s.nombre, s.apellido, s.dni, s.dni_pin,
        s.foto, s.fecha_nacimiento, s.sexo, s.telefono, s.domicilio,
        s.profesion, s.email, s.como_nos_conocio, s.observaciones,
        s.activo, s.registrado_por,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS registrado_por_nombre,
        s.creado_en, s.actualizado_en
    FROM socios s
    LEFT JOIN usuarios u ON u.id = s.registrado_por
    WHERE s.eliminado_en IS NULL
    ORDER BY s.apellido ASC, s.nombre ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER POR ID
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerSocioPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        s.id, s.numero_socio, s.nombre, s.apellido, s.dni, s.dni_pin,
        s.foto, s.fecha_nacimiento, s.sexo, s.telefono, s.domicilio,
        s.profesion, s.email, s.como_nos_conocio, s.observaciones,
        s.activo, s.registrado_por,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS registrado_por_nombre,
        s.creado_en, s.actualizado_en
    FROM socios s
    LEFT JOIN usuarios u ON u.id = s.registrado_por
    WHERE s.id = @Id AND s.eliminado_en IS NULL;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. BUSCAR SOCIOS  (texto + filtro de estado)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_BuscarSocios
    @Texto         NVARCHAR(100) = '',
    @FiltroEstado  VARCHAR(20)   = 'todos'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        s.id, s.numero_socio, s.nombre, s.apellido, s.dni, s.dni_pin,
        s.foto, s.fecha_nacimiento, s.sexo, s.telefono, s.domicilio,
        s.profesion, s.email, s.como_nos_conocio, s.observaciones,
        s.activo, s.registrado_por,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS registrado_por_nombre,
        s.creado_en, s.actualizado_en
    FROM socios s
    LEFT JOIN usuarios u ON u.id = s.registrado_por
    WHERE s.eliminado_en IS NULL
      AND (
            @Texto = ''
         OR s.nombre   LIKE '%' + @Texto + '%'
         OR s.apellido LIKE '%' + @Texto + '%'
         OR s.dni      LIKE '%' + @Texto + '%'
         OR CAST(s.numero_socio AS VARCHAR(20)) LIKE '%' + @Texto + '%'
          )
      AND (
            @FiltroEstado = 'todos'
         OR (@FiltroEstado = 'activos'   AND s.activo = 1)
         OR (@FiltroEstado = 'inactivos' AND s.activo = 0)
          )
    ORDER BY s.apellido ASC, s.nombre ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. SIGUIENTE NÚMERO DE SOCIO
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerSiguienteNumeroSocio
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(MAX(numero_socio), 0) + 1 AS siguiente FROM socios;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. INSERTAR
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_InsertarSocio
    @Nombre           NVARCHAR(100),
    @Apellido         NVARCHAR(100),
    @Dni              CHAR(8),
    @DniPin           CHAR(64),
    @FechaNacimiento  DATE            = NULL,
    @Sexo             VARCHAR(10)     = 'Otro',
    @Telefono         NVARCHAR(20)    = NULL,
    @Domicilio        NVARCHAR(200)   = NULL,
    @Profesion        NVARCHAR(100)   = NULL,
    @Email            NVARCHAR(191)   = NULL,
    @ComoNosConocio   NVARCHAR(200)   = NULL,
    @Observaciones    NVARCHAR(MAX)   = NULL,
    @Foto             VARBINARY(MAX)  = NULL,
    @RegistradoPor    BIGINT          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM socios WHERE dni = @Dni AND eliminado_en IS NULL)
    BEGIN
        SELECT -1 AS id;
        RETURN;
    END

    DECLARE @NumeroSocio INT;
    SELECT @NumeroSocio = ISNULL(MAX(numero_socio), 0) + 1 FROM socios;

    INSERT INTO socios
        (numero_socio, nombre, apellido, dni, dni_pin, fecha_nacimiento, sexo,
         telefono, domicilio, profesion, email, como_nos_conocio, observaciones,
         foto, activo, registrado_por)
    VALUES
        (@NumeroSocio, @Nombre, @Apellido, @Dni, @DniPin, @FechaNacimiento, @Sexo,
         @Telefono, @Domicilio, @Profesion, @Email, @ComoNosConocio, @Observaciones,
         @Foto, 1, @RegistradoPor);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. MODIFICAR
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ModificarSocio
    @Id               BIGINT,
    @Nombre           NVARCHAR(100),
    @Apellido         NVARCHAR(100),
    @Dni              CHAR(8),
    @DniPin           CHAR(64)        = NULL,
    @FechaNacimiento  DATE            = NULL,
    @Sexo             VARCHAR(10)     = 'Otro',
    @Telefono         NVARCHAR(20)    = NULL,
    @Domicilio        NVARCHAR(200)   = NULL,
    @Profesion        NVARCHAR(100)   = NULL,
    @Email            NVARCHAR(191)   = NULL,
    @ComoNosConocio   NVARCHAR(200)   = NULL,
    @Observaciones    NVARCHAR(MAX)   = NULL,
    @Foto             VARBINARY(MAX)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1 FROM socios
        WHERE dni = @Dni AND id <> @Id AND eliminado_en IS NULL
    )
    BEGIN
        RAISERROR('El DNI ya está siendo utilizado por otro socio.', 16, 1);
        RETURN;
    END

    UPDATE socios SET
        nombre           = @Nombre,
        apellido         = @Apellido,
        dni              = @Dni,
        dni_pin          = ISNULL(@DniPin,          dni_pin),
        fecha_nacimiento = ISNULL(@FechaNacimiento, fecha_nacimiento),
        sexo             = @Sexo,
        telefono         = ISNULL(@Telefono,        telefono),
        domicilio        = ISNULL(@Domicilio,       domicilio),
        profesion        = ISNULL(@Profesion,       profesion),
        email            = ISNULL(@Email,           email),
        como_nos_conocio = ISNULL(@ComoNosConocio,  como_nos_conocio),
        observaciones    = ISNULL(@Observaciones,   observaciones),
        foto             = ISNULL(@Foto,            foto),
        actualizado_en   = GETDATE()
    WHERE id = @Id AND eliminado_en IS NULL;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. CAMBIAR ESTADO
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_CambiarEstadoSocio
    @Id     BIGINT,
    @Activo BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE socios
    SET activo = @Activo, actualizado_en = GETDATE()
    WHERE id = @Id AND eliminado_en IS NULL;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 8. ELIMINAR (soft-delete)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_EliminarSocio
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE socios
    SET eliminado_en = GETDATE(),
        activo = 0,
        actualizado_en = GETDATE()
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- Verificar
EXEC sp_ObtenerSiguienteNumeroSocio;
EXEC sp_ObtenerSocios;
GO
