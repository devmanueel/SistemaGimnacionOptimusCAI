-- ============================================================
--  STORED PROCEDURES — TABLA caja_movimientos
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
--
--  Recordá: los ingresos por cuota ya se insertan automático
--  desde sp_InsertarMembresia y sp_RenovarMembresia.
--  Acá agregamos: gastos, movimientos manuales, listados y
--  resúmenes contables.
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER MOVIMIENTOS (con datos joineados)
--    Soporta filtro por rango de fechas.
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerMovimientos
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Default: últimos 30 días si no especifica
    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -30, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        c.id, c.tipo, c.subtipo,
        c.usuario_id, c.socio_id, c.membresia_id, c.actividad_id, c.venta_id,
        c.detalle, c.metodo_pago, c.monto, c.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')         AS usuario_nombre,
        ISNULL(s.nombre + ' ' + s.apellido, '')                AS socio_nombre,
        ISNULL(a.nombre, '')                                   AS actividad_nombre,
        s.numero_socio
    FROM caja_movimientos c
    LEFT JOIN usuarios     u ON u.id = c.usuario_id
    LEFT JOIN socios       s ON s.id = c.socio_id
    LEFT JOIN actividades  a ON a.id = c.actividad_id
    WHERE CAST(c.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
    ORDER BY c.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. BUSCAR MOVIMIENTOS (texto + tipo + rango fechas)
-- FiltroTipo: 'todos' / 'ingreso' (cualquier ingreso_*) /
--             'gasto' / 'movimiento_interno'
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_BuscarMovimientos
    @Texto       NVARCHAR(150) = '',
    @FiltroTipo  VARCHAR(30)   = 'todos',
    @FechaDesde  DATE          = NULL,
    @FechaHasta  DATE          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -30, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        c.id, c.tipo, c.subtipo,
        c.usuario_id, c.socio_id, c.membresia_id, c.actividad_id, c.venta_id,
        c.detalle, c.metodo_pago, c.monto, c.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')   AS usuario_nombre,
        ISNULL(s.nombre + ' ' + s.apellido, '')          AS socio_nombre,
        ISNULL(a.nombre, '')                             AS actividad_nombre,
        s.numero_socio
    FROM caja_movimientos c
    LEFT JOIN usuarios     u ON u.id = c.usuario_id
    LEFT JOIN socios       s ON s.id = c.socio_id
    LEFT JOIN actividades  a ON a.id = c.actividad_id
    WHERE CAST(c.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND (
            @Texto = ''
         OR c.detalle  LIKE '%' + @Texto + '%'
         OR c.subtipo  LIKE '%' + @Texto + '%'
         OR s.nombre   LIKE '%' + @Texto + '%'
         OR s.apellido LIKE '%' + @Texto + '%'
         OR a.nombre   LIKE '%' + @Texto + '%'
          )
      AND (
            @FiltroTipo = 'todos'
         OR (@FiltroTipo = 'ingreso' AND c.tipo LIKE 'ingreso%')
         OR c.tipo = @FiltroTipo
          )
    ORDER BY c.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. RESUMEN DE CAJA (totales por rango)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ResumenCaja
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = CAST(GETDATE() AS DATE);
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        ISNULL(SUM(CASE WHEN tipo LIKE 'ingreso%'        THEN monto ELSE 0 END), 0) AS total_ingresos,
        ISNULL(SUM(CASE WHEN tipo = 'gasto'              THEN monto ELSE 0 END), 0) AS total_gastos,
        ISNULL(SUM(CASE WHEN tipo LIKE 'ingreso%' AND metodo_pago = 'efectivo'      THEN monto ELSE 0 END), 0) AS ingresos_efectivo,
        ISNULL(SUM(CASE WHEN tipo LIKE 'ingreso%' AND metodo_pago = 'transferencia' THEN monto ELSE 0 END), 0) AS ingresos_transferencia,
        ISNULL(SUM(CASE WHEN tipo LIKE 'ingreso%' AND metodo_pago = 'tarjeta'       THEN monto ELSE 0 END), 0) AS ingresos_tarjeta,
        ISNULL(SUM(CASE WHEN tipo = 'ingreso_cuota'      THEN monto ELSE 0 END), 0) AS ingresos_cuotas,
        ISNULL(SUM(CASE WHEN tipo = 'ingreso_venta'      THEN monto ELSE 0 END), 0) AS ingresos_ventas,
        ISNULL(SUM(CASE WHEN tipo = 'ingreso_clase'      THEN monto ELSE 0 END), 0) AS ingresos_clases,
        COUNT(*)                                                                    AS cantidad_movimientos
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. REGISTRAR GASTO MANUAL
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_RegistrarGasto
    @UsuarioId   BIGINT,
    @Subtipo     NVARCHAR(100),     -- "Alquiler", "Sueldos", "Limpieza", etc.
    @Detalle     NVARCHAR(500)  = NULL,
    @Monto       DECIMAL(12,2),
    @MetodoPago  VARCHAR(20)    = 'efectivo'
AS
BEGIN
    SET NOCOUNT ON;

    IF @Monto <= 0
    BEGIN
        RAISERROR('El monto del gasto debe ser mayor a 0.', 16, 1);
        RETURN;
    END

    INSERT INTO caja_movimientos
        (tipo, subtipo, usuario_id, detalle, metodo_pago, monto)
    VALUES
        ('gasto', @Subtipo, @UsuarioId, @Detalle, @MetodoPago, @Monto);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. REGISTRAR INGRESO MANUAL (clases sueltas, otros)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_RegistrarIngresoManual
    @UsuarioId   BIGINT,
    @Tipo        VARCHAR(30),       -- 'ingreso_clase' / 'movimiento_interno'
    @Subtipo     NVARCHAR(100),
    @SocioId     BIGINT         = NULL,
    @Detalle     NVARCHAR(500)  = NULL,
    @Monto       DECIMAL(12,2),
    @MetodoPago  VARCHAR(20)    = 'efectivo'
AS
BEGIN
    SET NOCOUNT ON;

    IF @Tipo NOT IN ('ingreso_clase', 'movimiento_interno')
    BEGIN
        RAISERROR('Tipo de ingreso manual inválido.', 16, 1);
        RETURN;
    END

    IF @Monto <= 0
    BEGIN
        RAISERROR('El monto debe ser mayor a 0.', 16, 1);
        RETURN;
    END

    INSERT INTO caja_movimientos
        (tipo, subtipo, usuario_id, socio_id, detalle, metodo_pago, monto)
    VALUES
        (@Tipo, @Subtipo, @UsuarioId, @SocioId, @Detalle, @MetodoPago, @Monto);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. ELIMINAR MOVIMIENTO (solo gastos manuales — los ingresos
--    de cuotas no se borran nunca por trazabilidad).
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_EliminarMovimiento
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Tipo VARCHAR(30);
    SELECT @Tipo = tipo FROM caja_movimientos WHERE id = @Id;

    IF @Tipo IS NULL
    BEGIN
        RAISERROR('El movimiento no existe.', 16, 1);
        RETURN;
    END

    IF @Tipo IN ('ingreso_cuota', 'ingreso_venta')
    BEGIN
        RAISERROR('No se pueden eliminar ingresos de cuotas ni de ventas. Esos movimientos se cancelan desde el módulo correspondiente.', 16, 1);
        RETURN;
    END

    DELETE FROM caja_movimientos WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. INGRESOS POR DÍA (últimos 7 días para gráfico)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_IngresosUltimos7Dias
AS
BEGIN
    SET NOCOUNT ON;

    -- Generar serie de los últimos 7 días con LEFT JOIN
    ;WITH dias AS (
        SELECT CAST(GETDATE() AS DATE) AS dia
        UNION ALL SELECT DATEADD(DAY, -1, CAST(GETDATE() AS DATE))
        UNION ALL SELECT DATEADD(DAY, -2, CAST(GETDATE() AS DATE))
        UNION ALL SELECT DATEADD(DAY, -3, CAST(GETDATE() AS DATE))
        UNION ALL SELECT DATEADD(DAY, -4, CAST(GETDATE() AS DATE))
        UNION ALL SELECT DATEADD(DAY, -5, CAST(GETDATE() AS DATE))
        UNION ALL SELECT DATEADD(DAY, -6, CAST(GETDATE() AS DATE))
    )
    SELECT
        d.dia AS fecha,
        ISNULL(SUM(CASE WHEN c.tipo LIKE 'ingreso%' THEN c.monto ELSE 0 END), 0) AS ingresos,
        ISNULL(SUM(CASE WHEN c.tipo = 'gasto'       THEN c.monto ELSE 0 END), 0) AS gastos
    FROM dias d
    LEFT JOIN caja_movimientos c ON CAST(c.creado_en AS DATE) = d.dia
    GROUP BY d.dia
    ORDER BY d.dia ASC;
END;
GO

-- Verificación
EXEC sp_ResumenCaja;
EXEC sp_IngresosUltimos7Dias;
GO
