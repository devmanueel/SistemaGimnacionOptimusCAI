-- ============================================================
--  STORED PROCEDURES — TABLA socios
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
--  v1.2 — Cambios:
--    · numero_socio ahora excluye eliminados al calcular siguiente
--    · Teléfono ya no es único (Problema 2)
--    · registrado_por ya incluido en sp_InsertarSocio (estaba OK)
--    · Nuevos SPs: sp_SociosParaDarDeBaja + sp_DarDeBajaSocios
-- ============================================================

-- Agregar columna numero_socio si no existe
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'socios' AND COLUMN_NAME = 'numero_socio'
)
BEGIN
    ALTER TABLE socios ADD numero_socio INT NULL;
END
GO

-- Agregar columna foto si no existe (para guardar imagen como binario)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'socios' AND COLUMN_NAME = 'foto'
)
BEGIN
    ALTER TABLE socios ADD foto VARBINARY(MAX) NULL;
END
GO

-- Eliminar columna foto_path si existe (ya no se usa, ahora guardamos binario)
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'socios' AND COLUMN_NAME = 'foto_path'
)
BEGIN
    ALTER TABLE socios DROP COLUMN foto_path;
END
GO

-- Eliminar unique constraint de telefono si existe (Problema 2)
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_socios_telefono'
    AND object_id = OBJECT_ID('socios')
)
    DROP INDEX UQ_socios_telefono ON socios;
GO

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER TODOS
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_ObtenerSocios', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerSocios;
GO
CREATE PROCEDURE sp_ObtenerSocios
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
IF OBJECT_ID('sp_ObtenerSocioPorId', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerSocioPorId;
GO
CREATE PROCEDURE sp_ObtenerSocioPorId
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
-- 3. BUSCAR
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_BuscarSocios', 'P') IS NOT NULL
    DROP PROCEDURE sp_BuscarSocios;
GO
CREATE PROCEDURE sp_BuscarSocios
    @Texto        NVARCHAR(100) = '',
    @FiltroEstado VARCHAR(20)   = 'todos'
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
IF OBJECT_ID('sp_ObtenerSiguienteNumeroSocio', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerSiguienteNumeroSocio;
GO
CREATE PROCEDURE sp_ObtenerSiguienteNumeroSocio
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(MAX(numero_socio), 0) + 1 AS siguiente FROM socios WHERE eliminado_en IS NULL;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. INSERTAR
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_InsertarSocio', 'P') IS NOT NULL
    DROP PROCEDURE sp_InsertarSocio;
GO
CREATE PROCEDURE sp_InsertarSocio
    @Nombre           NVARCHAR(100),
    @Apellido         NVARCHAR(100),
    @Dni              CHAR(8),
    @DniPin           CHAR(64),
    @FechaNacimiento  DATE           = NULL,
    @Sexo             VARCHAR(10)    = 'Otro',
    @Telefono         NVARCHAR(20)   = NULL,
    @Domicilio        NVARCHAR(200)  = NULL,
    @Profesion        NVARCHAR(100)  = NULL,
    @Email            NVARCHAR(191)  = NULL,
    @ComoNosConocio   NVARCHAR(200)  = NULL,
    @Observaciones    NVARCHAR(MAX)  = NULL,
    @Foto             VARBINARY(MAX) = NULL,
    @RegistradoPor    BIGINT         = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM socios WHERE dni = @Dni AND eliminado_en IS NULL)
    BEGIN
        SELECT -1 AS id;
        RETURN;
    END

    DECLARE @NumeroSocio INT;
    SELECT @NumeroSocio = ISNULL(MAX(numero_socio), 0) + 1 FROM socios WHERE eliminado_en IS NULL;

    INSERT INTO socios
        (numero_socio, nombre, apellido, dni, dni_pin, fecha_nacimiento, sexo,
         telefono, domicilio, profesion, email, como_nos_conocio, observaciones,
         foto, activo, registrado_por)
    VALUES
        (@NumeroSocio, @Nombre, @Apellido, @Dni, @DniPin, @FechaNacimiento, @Sexo,
         @Telefono, @Domicilio, @Profesion, @Email, @ComoNosConocio, @Observaciones,
         @Foto, 1, @RegistradoPor);

    DECLARE @NuevoId BIGINT = SCOPE_IDENTITY();

    SELECT
        @NuevoId                        AS id,
        @NumeroSocio                    AS numero_socio,
        @Nombre + ' ' + @Apellido       AS nombre_completo;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. MODIFICAR
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_ModificarSocio', 'P') IS NOT NULL
    DROP PROCEDURE sp_ModificarSocio;
GO
CREATE PROCEDURE sp_ModificarSocio
    @Id               BIGINT,
    @Nombre           NVARCHAR(100),
    @Apellido         NVARCHAR(100),
    @Dni              CHAR(8),
    @DniPin           CHAR(64)       = NULL,
    @FechaNacimiento  DATE           = NULL,
    @Sexo             VARCHAR(10)    = 'Otro',
    @Telefono         NVARCHAR(20)   = NULL,
    @Domicilio        NVARCHAR(200)  = NULL,
    @Profesion        NVARCHAR(100)  = NULL,
    @Email            NVARCHAR(191)  = NULL,
    @ComoNosConocio   NVARCHAR(200)  = NULL,
    @Observaciones    NVARCHAR(MAX)  = NULL,
    @Foto             VARBINARY(MAX) = NULL
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
        telefono         = @Telefono,
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
IF OBJECT_ID('sp_CambiarEstadoSocio', 'P') IS NOT NULL
    DROP PROCEDURE sp_CambiarEstadoSocio;
GO
CREATE PROCEDURE sp_CambiarEstadoSocio
    @Id     BIGINT,
    @Activo BIT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MembresiasCanceladas TABLE (id BIGINT);

    UPDATE socios
    SET activo = @Activo, actualizado_en = GETDATE()
    WHERE id = @Id AND eliminado_en IS NULL;

    DECLARE @FilasAfectadas INT = @@ROWCOUNT;

    IF @FilasAfectadas > 0 AND @Activo = 0
    BEGIN
        UPDATE membresias
        SET estado = 'cancelada',
            actualizado_en = GETDATE()
        OUTPUT inserted.id INTO @MembresiasCanceladas
        WHERE socio_id = @Id
          AND estado = 'activa';

        INSERT INTO membresia_historial
            (membresia_id, tipo_evento, fecha_desde, fecha_hasta, registrado_por)
        SELECT
            m.id, 'anulacion', m.fecha_inicio, m.fecha_vencimiento, NULL
        FROM membresias m
        INNER JOIN @MembresiasCanceladas mc ON mc.id = m.id;
    END

    SELECT
        @FilasAfectadas AS filas_afectadas,
        (SELECT COUNT(*) FROM @MembresiasCanceladas) AS membresias_canceladas;

    SELECT id FROM @MembresiasCanceladas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 8. ELIMINAR (soft-delete)
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_EliminarSocio', 'P') IS NOT NULL
    DROP PROCEDURE sp_EliminarSocio;
GO
CREATE PROCEDURE sp_EliminarSocio
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

-- ─────────────────────────────────────────────────────────────
-- 9. SOCIOS CANDIDATOS A DAR DE BAJA (sin actividad >= N meses)
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_SociosParaDarDeBaja', 'P') IS NOT NULL
    DROP PROCEDURE sp_SociosParaDarDeBaja;
GO
CREATE PROCEDURE sp_SociosParaDarDeBaja
    @MesesSinActividad INT = 2
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Limite DATE = DATEADD(MONTH, -@MesesSinActividad, CAST(GETDATE() AS DATE));

    SELECT
        s.id, s.nombre, s.apellido, s.dni, s.numero_socio, s.foto, s.activo,
        MAX(r.accedido_en)                                                   AS ultima_asistencia,
        DATEDIFF(DAY, COALESCE(MAX(r.accedido_en), s.creado_en), GETDATE()) AS dias_inactivo
    FROM socios s
    LEFT JOIN registros_acceso r ON r.socio_id = s.id AND r.resultado = 'permitido'
    WHERE s.activo = 1
      AND s.eliminado_en IS NULL
    GROUP BY s.id, s.nombre, s.apellido, s.dni, s.numero_socio, s.foto, s.activo, s.creado_en
    HAVING COALESCE(MAX(r.accedido_en), s.creado_en) < @Limite
    ORDER BY dias_inactivo DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 10. DAR DE BAJA EN LOTE (lista de IDs separada por comas)
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_DarDeBajaSocios', 'P') IS NOT NULL
    DROP PROCEDURE sp_DarDeBajaSocios;
GO
CREATE PROCEDURE sp_DarDeBajaSocios
    @Ids NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #ids (id BIGINT);
    DECLARE @SociosAfectados TABLE (id BIGINT);
    DECLARE @MembresiasCanceladas TABLE (id BIGINT);

    DECLARE @pos INT = 1, @next INT, @val NVARCHAR(20);
    SET @Ids = LTRIM(RTRIM(@Ids)) + ',';
    WHILE @pos <= LEN(@Ids)
    BEGIN
        SET @next = CHARINDEX(',', @Ids, @pos);
        IF @next = 0 BREAK;
        SET @val = LTRIM(RTRIM(SUBSTRING(@Ids, @pos, @next - @pos)));
        IF ISNUMERIC(@val) = 1 INSERT INTO #ids VALUES (CAST(@val AS BIGINT));
        SET @pos = @next + 1;
    END

    UPDATE socios
    SET activo = 0,
        actualizado_en = GETDATE()
    OUTPUT inserted.id INTO @SociosAfectados
    WHERE id IN (SELECT id FROM #ids)
      AND eliminado_en IS NULL
      AND activo = 1;

    UPDATE membresias
    SET estado = 'cancelada',
        actualizado_en = GETDATE()
    OUTPUT inserted.id INTO @MembresiasCanceladas
    WHERE socio_id IN (SELECT id FROM @SociosAfectados)
      AND estado = 'activa';

    INSERT INTO membresia_historial
        (membresia_id, tipo_evento, fecha_desde, fecha_hasta, registrado_por)
    SELECT
        m.id, 'anulacion', m.fecha_inicio, m.fecha_vencimiento, NULL
    FROM membresias m
    INNER JOIN @MembresiasCanceladas mc ON mc.id = m.id;

    SELECT
        (SELECT COUNT(*) FROM @SociosAfectados) AS afectados,
        (SELECT COUNT(*) FROM @MembresiasCanceladas) AS membresias_canceladas;

    SELECT id FROM @MembresiasCanceladas;
END;
GO
