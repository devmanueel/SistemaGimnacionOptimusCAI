-- ============================================================
--  SP_Whatsapp.sql
--  CRUD + generador automatico de avisos de vencimiento
-- ============================================================

IF OBJECT_ID('sp_ObtenerWhatsappMensajes',     'P') IS NOT NULL DROP PROCEDURE sp_ObtenerWhatsappMensajes;
IF OBJECT_ID('sp_ObtenerWhatsappPorId',        'P') IS NOT NULL DROP PROCEDURE sp_ObtenerWhatsappPorId;
IF OBJECT_ID('sp_BuscarWhatsappMensajes',      'P') IS NOT NULL DROP PROCEDURE sp_BuscarWhatsappMensajes;
IF OBJECT_ID('sp_InsertarWhatsappMensaje',     'P') IS NOT NULL DROP PROCEDURE sp_InsertarWhatsappMensaje;
IF OBJECT_ID('sp_MarcarComoEnviado',           'P') IS NOT NULL DROP PROCEDURE sp_MarcarComoEnviado;
IF OBJECT_ID('sp_MarcarComoError',             'P') IS NOT NULL DROP PROCEDURE sp_MarcarComoError;
IF OBJECT_ID('sp_EliminarWhatsappMensaje',     'P') IS NOT NULL DROP PROCEDURE sp_EliminarWhatsappMensaje;
IF OBJECT_ID('sp_GenerarAvisosVencimiento',    'P') IS NOT NULL DROP PROCEDURE sp_GenerarAvisosVencimiento;
IF OBJECT_ID('sp_EstadisticasWhatsapp',        'P') IS NOT NULL DROP PROCEDURE sp_EstadisticasWhatsapp;
GO

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER (con filtros)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerWhatsappMensajes
    @Estado VARCHAR(20) = 'todos'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        w.id, w.tipo, w.disparador, w.socio_id, w.telefono, w.mensaje,
        w.estado, w.enviado_por, w.creado_en, w.enviado_en,
        ISNULL(s.nombre + ' ' + s.apellido, 'Numero externo') AS socio_nombre,
        s.numero_socio,
        s.foto AS socio_foto,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS enviado_por_nombre
    FROM whatsapp_mensajes w
    LEFT JOIN socios   s ON s.id = w.socio_id
    LEFT JOIN usuarios u ON u.id = w.enviado_por
    WHERE (@Estado = 'todos' OR w.estado = @Estado)
    ORDER BY w.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER POR ID
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerWhatsappPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        w.id, w.tipo, w.disparador, w.socio_id, w.telefono, w.mensaje,
        w.estado, w.enviado_por, w.creado_en, w.enviado_en,
        ISNULL(s.nombre + ' ' + s.apellido, 'Numero externo') AS socio_nombre,
        s.numero_socio,
        s.foto AS socio_foto,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS enviado_por_nombre
    FROM whatsapp_mensajes w
    LEFT JOIN socios   s ON s.id = w.socio_id
    LEFT JOIN usuarios u ON u.id = w.enviado_por
    WHERE w.id = @Id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. BUSCAR
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_BuscarWhatsappMensajes
    @Texto  NVARCHAR(150) = '',
    @Estado VARCHAR(20)   = 'todos',
    @Tipo   VARCHAR(20)   = 'todos'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        w.id, w.tipo, w.disparador, w.socio_id, w.telefono, w.mensaje,
        w.estado, w.enviado_por, w.creado_en, w.enviado_en,
        ISNULL(s.nombre + ' ' + s.apellido, 'Numero externo') AS socio_nombre,
        s.numero_socio,
        s.foto AS socio_foto,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS enviado_por_nombre
    FROM whatsapp_mensajes w
    LEFT JOIN socios   s ON s.id = w.socio_id
    LEFT JOIN usuarios u ON u.id = w.enviado_por
    WHERE (@Estado = 'todos' OR w.estado = @Estado)
      AND (@Tipo   = 'todos' OR w.tipo   = @Tipo)
      AND (@Texto  = ''
           OR s.nombre   LIKE '%' + @Texto + '%'
           OR s.apellido LIKE '%' + @Texto + '%'
           OR w.telefono LIKE '%' + @Texto + '%'
           OR w.mensaje  LIKE '%' + @Texto + '%')
    ORDER BY w.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. INSERTAR
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_InsertarWhatsappMensaje
    @Tipo        VARCHAR(20),
    @Disparador  VARCHAR(100) = NULL,
    @SocioId     BIGINT       = NULL,
    @Telefono    VARCHAR(20),
    @Mensaje     NVARCHAR(MAX),
    @EnviadoPor  BIGINT       = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Tipo NOT IN ('automatico', 'masivo', 'rutina')
    BEGIN
        RAISERROR('Tipo invalido. Debe ser: automatico, masivo o rutina.', 16, 1);
        RETURN;
    END

    IF LEN(@Telefono) < 8
    BEGIN
        RAISERROR('El telefono es invalido.', 16, 1);
        RETURN;
    END

    IF LEN(@Mensaje) < 1
    BEGIN
        RAISERROR('El mensaje no puede estar vacio.', 16, 1);
        RETURN;
    END

    INSERT INTO whatsapp_mensajes
        (tipo, disparador, socio_id, telefono, mensaje, estado, enviado_por)
    VALUES
        (@Tipo, @Disparador, @SocioId, @Telefono, @Mensaje, 'pendiente', @EnviadoPor);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. MARCAR COMO ENVIADO
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_MarcarComoEnviado
    @Id          BIGINT,
    @EnviadoPor  BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE whatsapp_mensajes SET
        estado      = 'enviado',
        enviado_en  = GETDATE(),
        enviado_por = ISNULL(@EnviadoPor, enviado_por)
    WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. MARCAR COMO ERROR
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_MarcarComoError
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE whatsapp_mensajes SET estado = 'error' WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. ELIMINAR
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_EliminarWhatsappMensaje
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM whatsapp_mensajes WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 8. GENERAR AVISOS DE VENCIMIENTO (BATCH)
--    Busca membresias activas que vencen entre HOY y HOY+@DiasAntes,
--    y crea un mensaje pendiente para cada socio que no lo tenga ya.
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_GenerarAvisosVencimiento
    @DiasAntes INT    = 3,
    @CreadoPor BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Hoy   DATE = CAST(GETDATE() AS DATE);
    DECLARE @Limite DATE = DATEADD(DAY, @DiasAntes, @Hoy);

    -- Tabla temporal con socios que vencen en el rango y NO tienen aviso reciente
    DECLARE @ASInsertar TABLE (
        socio_id  BIGINT,
        telefono  VARCHAR(20),
        nombre    VARCHAR(200),
        vencim    DATE,
        actividad VARCHAR(150)
    );

    INSERT INTO @ASInsertar (socio_id, telefono, nombre, vencim, actividad)
    SELECT DISTINCT
        s.id,
        s.telefono,
        s.nombre + ' ' + s.apellido,
        m.fecha_vencimiento,
        a.nombre
    FROM membresias m
    INNER JOIN socios     s ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.estado = 'activa'
      AND m.fecha_vencimiento BETWEEN @Hoy AND @Limite
      AND s.activo = 1
      AND s.telefono IS NOT NULL
      AND LEN(s.telefono) >= 8
      AND NOT EXISTS (
          -- No duplicar: ya hay un aviso pendiente o enviado en los ultimos 5 dias
          SELECT 1 FROM whatsapp_mensajes w
          WHERE w.socio_id   = s.id
            AND w.disparador = 'vencimiento_membresia'
            AND w.creado_en  >= DATEADD(DAY, -5, GETDATE())
      );

    -- Insertar los mensajes
    DECLARE @SocioId BIGINT, @Tel VARCHAR(20), @Nombre VARCHAR(200),
            @Vencim DATE, @Activ VARCHAR(150);

    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT socio_id, telefono, nombre, vencim, actividad FROM @ASInsertar;

    OPEN cur;
    FETCH NEXT FROM cur INTO @SocioId, @Tel, @Nombre, @Vencim, @Activ;

    DECLARE @Generados INT = 0;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @PrimerNombre VARCHAR(100);
        SET @PrimerNombre = LTRIM(RTRIM(LEFT(@Nombre, ISNULL(NULLIF(CHARINDEX(' ', @Nombre) - 1, -1), LEN(@Nombre)))));

        DECLARE @Msg NVARCHAR(MAX);
        SET @Msg = N'Hola ' + @PrimerNombre + N'! 👋' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
                   N'Te recordamos que tu membresia de ' + @Activ + N' vence el ' +
                   CONVERT(VARCHAR(10), @Vencim, 103) + N'.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
                   N'Te esperamos para renovarla y seguir entrenando! 💪' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
                   N'_OptimusCAI Gym_';

        INSERT INTO whatsapp_mensajes
            (tipo, disparador, socio_id, telefono, mensaje, estado, enviado_por)
        VALUES
            ('automatico', 'vencimiento_membresia', @SocioId, @Tel, @Msg, 'pendiente', @CreadoPor);

        SET @Generados = @Generados + 1;
        FETCH NEXT FROM cur INTO @SocioId, @Tel, @Nombre, @Vencim, @Activ;
    END

    CLOSE cur; DEALLOCATE cur;

    SELECT @Generados AS generados;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 9. ESTADÍSTICAS
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_EstadisticasWhatsapp
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);
    DECLARE @PrimerDiaMes DATE = DATEFROMPARTS(YEAR(@Hoy), MONTH(@Hoy), 1);

    SELECT
        (SELECT COUNT(*) FROM whatsapp_mensajes)                                            AS total,
        (SELECT COUNT(*) FROM whatsapp_mensajes WHERE estado = 'pendiente')                 AS pendientes,
        (SELECT COUNT(*) FROM whatsapp_mensajes WHERE estado = 'enviado')                   AS enviados,
        (SELECT COUNT(*) FROM whatsapp_mensajes WHERE estado = 'error')                     AS errores,
        (SELECT COUNT(*) FROM whatsapp_mensajes
         WHERE estado = 'enviado' AND CAST(enviado_en AS DATE) = @Hoy)                       AS enviados_hoy,
        (SELECT COUNT(*) FROM whatsapp_mensajes
         WHERE estado = 'enviado' AND CAST(enviado_en AS DATE) >= @PrimerDiaMes)             AS enviados_mes;
END;
GO