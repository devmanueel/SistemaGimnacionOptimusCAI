-- ============================================================
--  SP_Auditoria.sql
--  Log de cambios del sistema (solo lectura desde la UI)
-- ============================================================

IF OBJECT_ID('sp_RegistrarAuditoria',     'P') IS NOT NULL DROP PROCEDURE sp_RegistrarAuditoria;
IF OBJECT_ID('sp_ObtenerAuditoria',       'P') IS NOT NULL DROP PROCEDURE sp_ObtenerAuditoria;
IF OBJECT_ID('sp_ObtenerAuditoriaPorId',  'P') IS NOT NULL DROP PROCEDURE sp_ObtenerAuditoriaPorId;
IF OBJECT_ID('sp_BuscarAuditoria',        'P') IS NOT NULL DROP PROCEDURE sp_BuscarAuditoria;
IF OBJECT_ID('sp_EstadisticasAuditoria',  'P') IS NOT NULL DROP PROCEDURE sp_EstadisticasAuditoria;
IF OBJECT_ID('sp_TopUsuariosAuditoria',   'P') IS NOT NULL DROP PROCEDURE sp_TopUsuariosAuditoria;
GO

-- ─────────────────────────────────────────────────────────────
-- 1. REGISTRAR (usado desde el codigo)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_RegistrarAuditoria
    @ActorId   BIGINT,
    @Accion    VARCHAR(100),
    @Entidad   VARCHAR(50),
    @EntidadId BIGINT        = NULL,
    @Detalle   NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO auditoria (actor_id, accion, entidad, entidad_id, detalle)
    VALUES (@ActorId, @Accion, @Entidad, @EntidadId, @Detalle);
    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER (con filtro por rango de fechas)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerAuditoria
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -7, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT TOP 500
        a.id, a.actor_id, a.accion, a.entidad, a.entidad_id,
        a.detalle, a.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS actor_nombre,
        u.foto AS actor_foto,
        ISNULL(r.nombre, '—')                          AS actor_rol
    FROM auditoria a
    LEFT JOIN usuarios u ON u.id = a.actor_id
    LEFT JOIN roles    r ON r.id = u.rol_id
    WHERE CAST(a.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
    ORDER BY a.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. OBTENER POR ID
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerAuditoriaPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        a.id, a.actor_id, a.accion, a.entidad, a.entidad_id,
        a.detalle, a.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS actor_nombre,
        u.foto AS actor_foto,
        ISNULL(r.nombre, '—')                          AS actor_rol
    FROM auditoria a
    LEFT JOIN usuarios u ON u.id = a.actor_id
    LEFT JOIN roles    r ON r.id = u.rol_id
    WHERE a.id = @Id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. BUSCAR
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_BuscarAuditoria
    @Texto      NVARCHAR(150) = '',
    @ActorId    BIGINT        = NULL,
    @Entidad    VARCHAR(50)   = NULL,
    @Accion     VARCHAR(100)  = NULL,
    @FechaDesde DATE          = NULL,
    @FechaHasta DATE          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -7, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT TOP 500
        a.id, a.actor_id, a.accion, a.entidad, a.entidad_id,
        a.detalle, a.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS actor_nombre,
        u.foto AS actor_foto,
        ISNULL(r.nombre, '—')                          AS actor_rol
    FROM auditoria a
    LEFT JOIN usuarios u ON u.id = a.actor_id
    LEFT JOIN roles    r ON r.id = u.rol_id
    WHERE CAST(a.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND (@ActorId  IS NULL OR a.actor_id = @ActorId)
      AND (@Entidad  IS NULL OR a.entidad  = @Entidad)
      AND (@Accion   IS NULL OR a.accion   = @Accion)
      AND (@Texto = ''
           OR a.accion   LIKE '%' + @Texto + '%'
           OR a.entidad  LIKE '%' + @Texto + '%'
           OR a.detalle  LIKE '%' + @Texto + '%'
           OR u.nombre   LIKE '%' + @Texto + '%'
           OR u.apellido LIKE '%' + @Texto + '%')
    ORDER BY a.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. ESTADÍSTICAS
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_EstadisticasAuditoria
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);
    DECLARE @PrimerDiaMes DATE = DATEFROMPARTS(YEAR(@Hoy), MONTH(@Hoy), 1);

    SELECT
        (SELECT COUNT(*) FROM auditoria)                                                   AS total,
        (SELECT COUNT(*) FROM auditoria WHERE CAST(creado_en AS DATE) = @Hoy)              AS hoy,
        (SELECT COUNT(*) FROM auditoria WHERE CAST(creado_en AS DATE) >= @PrimerDiaMes)    AS mes,
        (SELECT COUNT(DISTINCT actor_id) FROM auditoria
            WHERE CAST(creado_en AS DATE) >= @PrimerDiaMes)                                AS usuarios_activos_mes;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. TOP USUARIOS DEL MES
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_TopUsuariosAuditoria
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @PrimerDiaMes DATE = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

    SELECT TOP 5
        a.actor_id,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS nombre,
        u.foto,
        COUNT(*) AS acciones
    FROM auditoria a
    LEFT JOIN usuarios u ON u.id = a.actor_id
    WHERE CAST(a.creado_en AS DATE) >= @PrimerDiaMes
    GROUP BY a.actor_id, u.nombre, u.apellido, u.foto
    ORDER BY COUNT(*) DESC;
END;
GO