-- ============================================================
--  DataBase/spReportes.sql
--  Módulo Reportes Financieros y Sueldos Docentes
--  Columnas según estructura real de DB_CAI_Optimus.mdf
-- ============================================================

-- ─── SP 1: sp_ReporteIngresos ────────────────────────────────
IF OBJECT_ID('sp_ReporteIngresos','P') IS NOT NULL DROP PROCEDURE sp_ReporteIngresos;
GO
CREATE PROCEDURE sp_ReporteIngresos
    @FechaDesde   DATE = NULL,
    @FechaHasta   DATE = NULL,
    @ActividadId  BIGINT = NULL,
    @MetodoPago   VARCHAR(30) = NULL,
    @InstructorId BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FechaDesde IS NULL SET @FechaDesde = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        cm.id,
        LOWER(LTRIM(RTRIM(cm.tipo)))                            AS tipo,
        ISNULL(cm.subtipo, '')                                  AS subtipo,

        -- Concepto: si está vacío, generar descripción automática.
        -- Membresía  → "Membresía {plan} — {Socio} (#numero)"
        -- Venta      → "Venta de productos #{venta_id}"
        -- Clase      → "Clase suelta — {subtipo}"
        -- Gasto      → "{subtipo}" o "Gasto"
        -- Fallback   → tipo capitalizado + subtipo
        CASE
            WHEN LTRIM(RTRIM(ISNULL(cm.detalle, ''))) <> ''
                THEN cm.detalle
            WHEN cm.membresia_id IS NOT NULL AND s.id IS NOT NULL
                THEN 'Membresía ' + ISNULL(m.tipo_plan, '') +
                     ' - ' + s.nombre + ' ' + s.apellido +
                     ' (#' + CAST(ISNULL(s.numero_socio, s.id) AS VARCHAR) + ')'
            WHEN cm.membresia_id IS NOT NULL
                THEN 'Membresía ' + ISNULL(m.tipo_plan, '') +
                     ' - Socio #' + CAST(m.socio_id AS VARCHAR)
            WHEN cm.venta_id IS NOT NULL
                THEN 'Venta de productos #' + CAST(cm.venta_id AS VARCHAR)
            WHEN LOWER(LTRIM(RTRIM(cm.tipo))) = 'ingreso_clase'
                THEN 'Clase suelta' + ISNULL(' - ' + cm.subtipo, '')
            WHEN LOWER(LTRIM(RTRIM(cm.tipo))) = 'gasto'
                THEN ISNULL(cm.subtipo, 'Gasto')
            WHEN LOWER(LTRIM(RTRIM(cm.tipo))) = 'movimiento_interno'
                THEN ISNULL(cm.subtipo, 'Movimiento interno')
            ELSE UPPER(LEFT(LOWER(LTRIM(RTRIM(cm.tipo))),1)) +
                 SUBSTRING(LOWER(LTRIM(RTRIM(cm.tipo))),2,100) +
                 ISNULL(' - ' + cm.subtipo, '')
        END                                                     AS concepto,

        cm.monto,
        ISNULL(NULLIF(LTRIM(RTRIM(cm.metodo_pago)), ''),
               'No especificado')                               AS metodo_pago,
        CASE
            WHEN cm.membresia_id IS NOT NULL THEN 'membresia'
            WHEN cm.venta_id     IS NOT NULL THEN 'venta'
            ELSE 'otro'
        END                                                     AS referencia_tipo,
        CAST(cm.creado_en AS DATE)                              AS fecha,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')          AS registrado_por_nombre,
        CASE
            WHEN LOWER(LTRIM(RTRIM(cm.tipo))) = 'ingreso_venta'  THEN 'Venta de productos'
            WHEN LOWER(LTRIM(RTRIM(cm.tipo))) = 'ingreso_cuota'  THEN ISNULL(a.nombre, 'Cuota')
            WHEN LOWER(LTRIM(RTRIM(cm.tipo))) = 'ingreso_clase'  THEN ISNULL(cm.subtipo, 'Clase suelta')
            WHEN LOWER(LTRIM(RTRIM(cm.tipo))) = 'gasto'          THEN ISNULL(cm.subtipo, 'Gasto')
            WHEN LOWER(LTRIM(RTRIM(cm.tipo))) = 'movimiento_interno' THEN ISNULL(cm.subtipo, 'Movimiento interno')
            ELSE ISNULL(a.nombre, '-')
        END                                                     AS actividad_nombre,
        ISNULL(ui.nombre + ' ' + ui.apellido, '-')              AS instructor_nombre
    FROM caja_movimientos cm
    LEFT JOIN usuarios u  ON u.id  = cm.usuario_id
    LEFT JOIN membresias m ON m.id = cm.membresia_id
    LEFT JOIN socios s    ON s.id  = m.socio_id
    LEFT JOIN actividades a ON a.id = ISNULL(m.actividad_id, cm.actividad_id)
    LEFT JOIN usuarios ui ON ui.id = m.instructor_id
    WHERE CAST(cm.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND (@ActividadId  IS NULL OR ISNULL(m.actividad_id, cm.actividad_id) = @ActividadId)
      AND (@MetodoPago   IS NULL
           OR LOWER(LTRIM(RTRIM(cm.metodo_pago))) = LOWER(LTRIM(RTRIM(@MetodoPago))))
      AND (@InstructorId IS NULL OR m.instructor_id = @InstructorId)
    ORDER BY cm.creado_en DESC;
END;
GO

-- ─── SP 2: sp_ReporteTotales ─────────────────────────────────
-- FIX (SDD_Fix_Reportes): los valores reales de cm.tipo son
-- 'ingreso_cuota', 'ingreso_venta', 'ingreso_clase' (NO 'ingreso')
-- y los egresos son 'gasto' (NO 'egreso'). Por eso el balance
-- daba siempre negativo: total_ingresos era 0 y todo restaba.
-- También cubrimos variaciones de case/espacios con LOWER+LTRIM+RTRIM.
IF OBJECT_ID('sp_ReporteTotales','P') IS NOT NULL DROP PROCEDURE sp_ReporteTotales;
GO
CREATE PROCEDURE sp_ReporteTotales
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FechaDesde IS NULL SET @FechaDesde = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    -- Totales generales
    SELECT
        ISNULL(SUM(CASE
            WHEN LOWER(LTRIM(RTRIM(tipo))) LIKE 'ingreso%' THEN monto
            ELSE 0 END), 0)                                              AS total_ingresos,
        ISNULL(SUM(CASE
            WHEN LOWER(LTRIM(RTRIM(tipo))) IN ('gasto','egreso','movimiento_interno') THEN monto
            ELSE 0 END), 0)                                              AS total_egresos,
        ISNULL(SUM(CASE
            WHEN LOWER(LTRIM(RTRIM(tipo))) LIKE 'ingreso%' THEN monto
            WHEN LOWER(LTRIM(RTRIM(tipo))) IN ('gasto','egreso','movimiento_interno') THEN -monto
            ELSE 0 END), 0)                                              AS balance,
        COUNT(CASE
            WHEN LOWER(LTRIM(RTRIM(tipo))) LIKE 'ingreso%' THEN 1 END)   AS cantidad_ingresos,
        COUNT(CASE
            WHEN LOWER(LTRIM(RTRIM(tipo))) IN ('gasto','egreso','movimiento_interno') THEN 1 END) AS cantidad_egresos
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta;

    -- Ingresos por actividad (membresía → actividad, o categoría general)
    SELECT
        ISNULL(a.nombre,
               CASE
                   WHEN LOWER(LTRIM(RTRIM(cm.tipo))) = 'ingreso_venta' THEN 'Venta de productos'
                   WHEN LOWER(LTRIM(RTRIM(cm.tipo))) = 'ingreso_clase' THEN 'Clases sueltas'
                   ELSE 'Otro ingreso'
               END)                                                      AS actividad,
        ISNULL(SUM(cm.monto), 0)                                          AS total,
        COUNT(*)                                                          AS cantidad
    FROM caja_movimientos cm
    LEFT JOIN membresias m  ON m.id = cm.membresia_id
    LEFT JOIN actividades a ON a.id = ISNULL(m.actividad_id, cm.actividad_id)
    WHERE CAST(cm.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND LOWER(LTRIM(RTRIM(cm.tipo))) LIKE 'ingreso%'
    GROUP BY a.nombre, cm.tipo
    ORDER BY total DESC;

    -- Ingresos por método de pago
    SELECT
        ISNULL(NULLIF(LTRIM(RTRIM(metodo_pago)), ''), 'No especificado') AS metodo_pago,
        ISNULL(SUM(monto), 0) AS total,
        COUNT(*)               AS cantidad
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND LOWER(LTRIM(RTRIM(tipo))) LIKE 'ingreso%'
    GROUP BY LTRIM(RTRIM(metodo_pago))
    ORDER BY total DESC;
END;
GO

-- ─── SP 3: sp_GraficoIngresosPorMes ──────────────────────────
IF OBJECT_ID('sp_GraficoIngresosPorMes','P') IS NOT NULL DROP PROCEDURE sp_GraficoIngresosPorMes;
GO
CREATE PROCEDURE sp_GraficoIngresosPorMes
    @Anio INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Anio IS NULL SET @Anio = YEAR(GETDATE());

    SELECT
        MONTH(creado_en)                        AS mes,
        DATENAME(MONTH, DATEFROMPARTS(@Anio, MONTH(creado_en), 1)) AS mes_nombre,
        ISNULL(SUM(CASE WHEN tipo LIKE 'ingreso%'                        THEN monto ELSE 0 END), 0) AS ingresos,
        ISNULL(SUM(CASE WHEN tipo IN ('gasto','movimiento_interno')    THEN monto ELSE 0 END), 0) AS egresos
    FROM caja_movimientos
    WHERE YEAR(creado_en) = @Anio
    GROUP BY MONTH(creado_en)
    ORDER BY MONTH(creado_en);
END;
GO

-- ─── SP 4: sp_ReporteSueldosDocentes (tarifa global SDD_Fix_Sueldos) ──
-- Lee @TarifaGlobal de configuracion_sistema y la aplica a todos.
-- La tabla usuarios.tarifa_hora ya NO se usa (queda en BD por compatibilidad).
-- Devuelve 3 resultsets:
--   1) Resumen por instructor
--   2) Detalle de asistencias
--   3) Tarifa global + alerta aguinaldo (junio/diciembre)
IF OBJECT_ID('sp_ReporteSueldosDocentes','P') IS NOT NULL DROP PROCEDURE sp_ReporteSueldosDocentes;
GO
CREATE PROCEDURE sp_ReporteSueldosDocentes
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FechaDesde IS NULL SET @FechaDesde = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    -- Tarifa global desde configuracion_sistema (default 4000 si falta)
    DECLARE @TarifaGlobal DECIMAL(10,2) = 4000;
    SELECT @TarifaGlobal = TRY_CAST(valor AS DECIMAL(10,2))
    FROM configuracion_sistema
    WHERE clave = 'tarifa_hora_docentes';
    IF @TarifaGlobal IS NULL SET @TarifaGlobal = 4000;

    -- Alerta aguinaldo en junio / diciembre
    DECLARE @AlertaAguinaldo BIT =
        CASE WHEN MONTH(GETDATE()) IN (6, 12) THEN 1 ELSE 0 END;

    -- ─── Resultset 1: Resumen por instructor ─────────────────────────
    SELECT
        u.id                                                    AS instructor_id,
        u.nombre + ' ' + u.apellido                             AS nombre_completo,
        u.foto,
        @TarifaGlobal                                           AS tarifa_hora,
        ISNULL(a.nombre, '-')                                   AS actividad_nombre,
        COUNT(DISTINCT ia.fecha)                                AS dias_trabajados,
        ISNULL(SUM(ia.horas_trabajadas), 0)                     AS horas_totales,
        ISNULL(SUM(ia.horas_trabajadas), 0) * @TarifaGlobal     AS sueldo_estimado,
        ISNULL((
            SELECT SUM(cm2.monto)
            FROM caja_movimientos cm2
            INNER JOIN membresias m2 ON m2.id = cm2.membresia_id
            WHERE m2.instructor_id = u.id
              AND LOWER(LTRIM(RTRIM(cm2.tipo))) LIKE 'ingreso%'
              AND CAST(cm2.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
        ), 0)                                                   AS ingresos_generados,
        @AlertaAguinaldo                                         AS alerta_aguinaldo
    FROM usuarios u
    LEFT JOIN instructor_asistencias ia
           ON ia.instructor_id = u.id
          AND ia.fecha BETWEEN @FechaDesde AND @FechaHasta
    LEFT JOIN turnos t ON t.instructor_id = u.id AND t.activo = 1
    LEFT JOIN actividades a ON a.id = t.actividad_id
    WHERE u.rol_id = 2
      AND u.activo = 1
      AND u.eliminado_en IS NULL
    GROUP BY u.id, u.nombre, u.apellido, u.foto, a.nombre
    ORDER BY u.apellido ASC;

    -- ─── Resultset 2: Detalle de asistencias ────────────────────────
    SELECT
        ia.instructor_id,
        ia.fecha,
        ia.hora_entrada,
        ia.hora_salida,
        ISNULL(ia.horas_trabajadas, 0) AS horas_trabajadas
    FROM instructor_asistencias ia
    WHERE ia.fecha BETWEEN @FechaDesde AND @FechaHasta
    ORDER BY ia.instructor_id, ia.fecha;

    -- ─── Resultset 3: Tarifa global + alerta ────────────────────────
    SELECT @TarifaGlobal AS tarifa_global, @AlertaAguinaldo AS alerta_aguinaldo;
END;
GO

-- ─── SP 5: sp_ReporteSociosDeuda ─────────────────────────────
IF OBJECT_ID('sp_ReporteSociosDeuda','P') IS NOT NULL DROP PROCEDURE sp_ReporteSociosDeuda;
GO
CREATE PROCEDURE sp_ReporteSociosDeuda
    @DiasProximos INT = 7
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy   DATE = CAST(GETDATE() AS DATE);
    DECLARE @Limite DATE = DATEADD(DAY, @DiasProximos, @Hoy);

    -- Membresías ya vencidas
    SELECT
        s.id                                                AS socio_id,
        s.nombre + ' ' + s.apellido                         AS nombre_completo,
        s.numero_socio,
        s.telefono,
        s.foto,
        m.id                                                AS membresia_id,
        m.tipo_plan,
        a.nombre                                            AS actividad_nombre,
        m.fecha_vencimiento,
        DATEDIFF(DAY, m.fecha_vencimiento, @Hoy)            AS dias_vencida,
        'vencida'                                           AS estado_deuda
    FROM membresias m
    INNER JOIN socios s     ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.estado = 'activa'
      AND m.fecha_vencimiento < @Hoy
      AND s.activo = 1
    ORDER BY dias_vencida DESC;

    -- Membresías próximas a vencer
    SELECT
        s.id                                                AS socio_id,
        s.nombre + ' ' + s.apellido                         AS nombre_completo,
        s.numero_socio,
        s.telefono,
        s.foto,
        m.id                                                AS membresia_id,
        m.tipo_plan,
        a.nombre                                            AS actividad_nombre,
        m.fecha_vencimiento,
        DATEDIFF(DAY, @Hoy, m.fecha_vencimiento)            AS dias_para_vencer,
        'proxima_a_vencer'                                  AS estado_deuda
    FROM membresias m
    INNER JOIN socios s     ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.estado = 'activa'
      AND m.fecha_vencimiento BETWEEN @Hoy AND @Limite
      AND s.activo = 1
    ORDER BY dias_para_vencer ASC;
END;
GO

-- ─── SP 6: sp_MisVentasDelDia ─────────────────────────────────
IF OBJECT_ID('sp_MisVentasDelDia','P') IS NOT NULL DROP PROCEDURE sp_MisVentasDelDia;
GO
CREATE PROCEDURE sp_MisVentasDelDia
    @EmpleadoId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);

    SELECT
        v.id,
        v.total,
        v.metodo_pago,
        v.creado_en,
        COUNT(vi.id) AS cantidad_items
    FROM ventas v
    INNER JOIN ventas_items vi ON vi.venta_id = v.id
    WHERE v.usuario_id = @EmpleadoId
      AND CAST(v.creado_en AS DATE) = @Hoy
    GROUP BY v.id, v.total, v.metodo_pago, v.creado_en
    ORDER BY v.creado_en DESC;

    SELECT
        ISNULL(SUM(v.total), 0) AS total_dia,
        COUNT(v.id)             AS cantidad_ventas
    FROM ventas v
    WHERE v.usuario_id = @EmpleadoId
      AND CAST(v.creado_en AS DATE) = @Hoy;
END;
GO

-- ─── SP 7: sp_ActualizarTarifaInstructor ─────────────────────
-- Permite cargar/editar la tarifa por hora de un instructor.
-- Valida que sea instructor (rol_id = 2) y tarifa no negativa.
IF OBJECT_ID('sp_ActualizarTarifaInstructor','P') IS NOT NULL
    DROP PROCEDURE sp_ActualizarTarifaInstructor;
GO
CREATE PROCEDURE sp_ActualizarTarifaInstructor
    @InstructorId BIGINT,
    @TarifaHora   DECIMAL(10,2)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = @InstructorId AND rol_id = 2)
    BEGIN
        RAISERROR('El usuario no existe o no es un instructor.', 16, 1);
        RETURN;
    END

    IF @TarifaHora < 0
    BEGIN
        RAISERROR('La tarifa no puede ser negativa.', 16, 1);
        RETURN;
    END

    UPDATE usuarios
    SET tarifa_hora = @TarifaHora
    WHERE id = @InstructorId;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO
