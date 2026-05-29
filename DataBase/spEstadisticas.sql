-- ============================================================
--  STORED PROCEDURES — MODULO ESTADISTICAS (Dashboard)
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
--  v1.0 — Mayo 2026
--
--  IMPORTANTE: los valores reales de caja_movimientos.tipo son
--    'ingreso_cuota', 'ingreso_venta', 'ingreso_clase' (ingresos)
--    'gasto', 'movimiento_interno' (egresos)
--  Usamos LIKE 'ingreso%' para ingresos e IN() para egresos.
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- 1. KPIs GLOBALES DEL DASHBOARD
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_EstadisticasKPIs','P') IS NOT NULL DROP PROCEDURE sp_EstadisticasKPIs;
GO
CREATE PROCEDURE sp_EstadisticasKPIs
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);
    DECLARE @En7Dias DATE = DATEADD(DAY, 7, @Hoy);

    SELECT
        (SELECT COUNT(*) FROM socios
         WHERE activo = 1 AND eliminado_en IS NULL)             AS socios_activos,

        (SELECT COUNT(*) FROM membresias
         WHERE estado = 'activa'
           AND fecha_vencimiento BETWEEN @Hoy AND @En7Dias)     AS membresias_por_vencer,

        (SELECT TOP 1 p.nombre
         FROM ventas_items vi
         INNER JOIN productos p ON p.id = vi.producto_id
         INNER JOIN ventas v ON v.id = vi.venta_id
         WHERE YEAR(v.creado_en) = YEAR(@Hoy)
           AND MONTH(v.creado_en) = MONTH(@Hoy)
         GROUP BY p.nombre
         ORDER BY SUM(vi.cantidad) DESC)                         AS producto_mas_vendido,

        (SELECT TOP 1 SUM(vi.cantidad)
         FROM ventas_items vi
         INNER JOIN ventas v ON v.id = vi.venta_id
         WHERE YEAR(v.creado_en) = YEAR(@Hoy)
           AND MONTH(v.creado_en) = MONTH(@Hoy)
         GROUP BY vi.producto_id
         ORDER BY SUM(vi.cantidad) DESC)                         AS producto_mas_vendido_qty,

        (SELECT COUNT(*) FROM socios
         WHERE YEAR(creado_en) = YEAR(@Hoy)
           AND MONTH(creado_en) = MONTH(@Hoy)
           AND eliminado_en IS NULL)                             AS nuevos_socios_mes,

        (SELECT ISNULL(SUM(monto),0) FROM caja_movimientos
         WHERE LOWER(LTRIM(RTRIM(tipo))) LIKE 'ingreso%'
           AND YEAR(creado_en) = YEAR(@Hoy)
           AND MONTH(creado_en) = MONTH(@Hoy))                  AS ingresos_mes,

        (SELECT ISNULL(SUM(monto),0) FROM caja_movimientos
         WHERE LOWER(LTRIM(RTRIM(tipo))) IN ('gasto','egreso','movimiento_interno')
           AND YEAR(creado_en) = YEAR(@Hoy)
           AND MONTH(creado_en) = MONTH(@Hoy))                  AS egresos_mes;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. RESUMEN DE CAJA CON INGRESOS Y EGRESOS POR DIA
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_EstadisticasCajaResumen','P') IS NOT NULL
    DROP PROCEDURE sp_EstadisticasCajaResumen;
GO
CREATE PROCEDURE sp_EstadisticasCajaResumen
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Resultset 1: Totales del periodo
    SELECT
        ISNULL(SUM(CASE WHEN LOWER(LTRIM(RTRIM(tipo))) LIKE 'ingreso%'
                        THEN monto ELSE 0 END), 0) AS total_ingresos,
        ISNULL(SUM(CASE WHEN LOWER(LTRIM(RTRIM(tipo))) IN ('gasto','egreso','movimiento_interno')
                        THEN monto ELSE 0 END), 0) AS total_egresos
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta;

    -- Resultset 2: Ingresos agrupados por dia
    SELECT
        CAST(creado_en AS DATE)                     AS fecha,
        ISNULL(SUM(monto), 0)                       AS monto
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND LOWER(LTRIM(RTRIM(tipo))) LIKE 'ingreso%'
    GROUP BY CAST(creado_en AS DATE)
    ORDER BY CAST(creado_en AS DATE);

    -- Resultset 3: Egresos agrupados por dia
    SELECT
        CAST(creado_en AS DATE)                     AS fecha,
        ISNULL(SUM(monto), 0)                       AS monto
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND LOWER(LTRIM(RTRIM(tipo))) IN ('gasto','egreso','movimiento_interno')
    GROUP BY CAST(creado_en AS DATE)
    ORDER BY CAST(creado_en AS DATE);
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. ASISTENCIAS POR HORA (promedio y porcentaje, filtrable por actividad)
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_EstadisticasAsistenciasPorHora','P') IS NOT NULL
    DROP PROCEDURE sp_EstadisticasAsistenciasPorHora;
GO
CREATE PROCEDURE sp_EstadisticasAsistenciasPorHora
    @FechaDesde DATE,
    @FechaHasta DATE,
    @ActividadId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TotalAccesos DECIMAL(12,2);
    DECLARE @TotalDias    INT;

    SELECT @TotalAccesos = COUNT(*)
    FROM registros_acceso r
    LEFT JOIN membresias m ON m.id = r.membresia_id
    WHERE r.resultado = 'permitido'
      AND CAST(r.accedido_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND (@ActividadId IS NULL OR m.actividad_id = @ActividadId);

    SELECT @TotalDias = DATEDIFF(DAY, @FechaDesde, @FechaHasta) + 1;
    IF @TotalDias < 1 SET @TotalDias = 1;
    IF @TotalAccesos < 1 SET @TotalAccesos = 1;

    SELECT
        DATEPART(HOUR, r.accedido_en) AS hora,
        CAST(COUNT(*) * 1.0 / @TotalDias AS DECIMAL(10,2)) AS promedio,
        CAST(COUNT(*) * 100.0 / @TotalAccesos AS DECIMAL(10,2)) AS porcentaje
    FROM registros_acceso r
    LEFT JOIN membresias m ON m.id = r.membresia_id
    WHERE r.resultado = 'permitido'
      AND CAST(r.accedido_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND (@ActividadId IS NULL OR m.actividad_id = @ActividadId)
    GROUP BY DATEPART(HOUR, r.accedido_en)
    ORDER BY DATEPART(HOUR, r.accedido_en);
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. ASISTENCIAS POR ACTIVIDAD
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_EstadisticasAsistenciasPorActividad','P') IS NOT NULL
    DROP PROCEDURE sp_EstadisticasAsistenciasPorActividad;
GO
CREATE PROCEDURE sp_EstadisticasAsistenciasPorActividad
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ISNULL(a.nombre, 'Sin actividad') AS actividad,
        COUNT(*) AS cantidad
    FROM registros_acceso r
    LEFT JOIN membresias m ON m.id = r.membresia_id
    LEFT JOIN actividades a ON a.id = m.actividad_id
    WHERE r.resultado = 'permitido'
      AND CAST(r.accedido_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
    GROUP BY a.nombre
    ORDER BY cantidad DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. NUEVOS SOCIOS POR DIA
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_EstadisticasNuevosSocios','P') IS NOT NULL
    DROP PROCEDURE sp_EstadisticasNuevosSocios;
GO
CREATE PROCEDURE sp_EstadisticasNuevosSocios
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CAST(creado_en AS DATE) AS fecha,
        COUNT(*) AS cantidad
    FROM socios
    WHERE eliminado_en IS NULL
      AND CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
    GROUP BY CAST(creado_en AS DATE)
    ORDER BY CAST(creado_en AS DATE);
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. TOP 5 SOCIOS CON MAS ASISTENCIAS
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_EstadisticasTop5Socios','P') IS NOT NULL
    DROP PROCEDURE sp_EstadisticasTop5Socios;
GO
CREATE PROCEDURE sp_EstadisticasTop5Socios
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 5
        s.nombre + ' ' + s.apellido AS nombre_completo,
        COUNT(*) AS total_asistencias
    FROM registros_acceso r
    INNER JOIN socios s ON s.id = r.socio_id
    WHERE r.resultado = 'permitido'
      AND CAST(r.accedido_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
    GROUP BY s.nombre, s.apellido
    ORDER BY total_asistencias DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. SOCIOS INACTIVOS (sin asistencia en N dias)
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_EstadisticasSociosInactivos','P') IS NOT NULL
    DROP PROCEDURE sp_EstadisticasSociosInactivos;
GO
CREATE PROCEDURE sp_EstadisticasSociosInactivos
    @DiasInactivo INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FechaLimite DATE = DATEADD(DAY, -@DiasInactivo, CAST(GETDATE() AS DATE));

    SELECT COUNT(*) AS cantidad
    FROM socios s
    WHERE s.activo = 1
      AND s.eliminado_en IS NULL
      AND NOT EXISTS (
          SELECT 1 FROM registros_acceso r
          WHERE r.socio_id = s.id
            AND r.resultado = 'permitido'
            AND CAST(r.accedido_en AS DATE) >= @FechaLimite
      );
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 8. LISTA DE ACTIVIDADES PARA FILTRO
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_EstadisticasActividades','P') IS NOT NULL
    DROP PROCEDURE sp_EstadisticasActividades;
GO
CREATE PROCEDURE sp_EstadisticasActividades
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, nombre FROM actividades WHERE activo = 1 ORDER BY nombre;
END;
GO
