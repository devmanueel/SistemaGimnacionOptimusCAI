-- ============================================================
--  SP_Ventas.sql
--  Patron: DROP IF EXISTS + CREATE PROCEDURE
--  (compatible con todas las versiones de SQL Server)
-- ============================================================

-- ── Limpiar SPs si existen ───────────────────────────────────
IF OBJECT_ID('sp_ObtenerVentas',        'P') IS NOT NULL DROP PROCEDURE sp_ObtenerVentas;
IF OBJECT_ID('sp_ObtenerVentaPorId',    'P') IS NOT NULL DROP PROCEDURE sp_ObtenerVentaPorId;
IF OBJECT_ID('sp_BuscarVentas',         'P') IS NOT NULL DROP PROCEDURE sp_BuscarVentas;
IF OBJECT_ID('sp_BuscarVentasPorUsuario','P') IS NOT NULL DROP PROCEDURE sp_BuscarVentasPorUsuario;
IF OBJECT_ID('sp_RegistrarVenta',       'P') IS NOT NULL DROP PROCEDURE sp_RegistrarVenta;
IF OBJECT_ID('sp_AnularVenta',          'P') IS NOT NULL DROP PROCEDURE sp_AnularVenta;
IF OBJECT_ID('sp_EstadisticasVentas',   'P') IS NOT NULL DROP PROCEDURE sp_EstadisticasVentas;
IF OBJECT_ID('sp_TopProductosVendidos', 'P') IS NOT NULL DROP PROCEDURE sp_TopProductosVendidos;
GO

-- ── Limpiar TYPE si existe ───────────────────────────────────
IF EXISTS (SELECT 1 FROM sys.types WHERE name = 'TipoVentaItem' AND is_user_defined = 1)
    DROP TYPE TipoVentaItem;
GO

CREATE TYPE TipoVentaItem AS TABLE
(
    producto_id     BIGINT        NULL,
    descripcion     NVARCHAR(200) NOT NULL,
    cantidad        INT           NOT NULL,
    precio_unitario DECIMAL(12,2) NOT NULL
);
GO

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER VENTAS
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerVentas
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -30, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        v.id, v.usuario_id, v.socio_id, v.total, v.metodo_pago,
        v.observaciones, v.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')          AS usuario_nombre,
        ISNULL(s.nombre + ' ' + s.apellido, 'Publico general')  AS socio_nombre,
        s.numero_socio,
        s.foto AS socio_foto,
        (SELECT COUNT(*) FROM ventas_items vi WHERE vi.venta_id = v.id) AS cantidad_items
    FROM ventas v
    LEFT JOIN usuarios u ON u.id = v.usuario_id
    LEFT JOIN socios   s ON s.id = v.socio_id
    WHERE CAST(v.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
    ORDER BY v.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER VENTA POR ID (con items)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_ObtenerVentaPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        v.id, v.usuario_id, v.socio_id, v.total, v.metodo_pago,
        v.observaciones, v.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')          AS usuario_nombre,
        ISNULL(s.nombre + ' ' + s.apellido, 'Publico general')  AS socio_nombre,
        s.numero_socio,
        s.foto AS socio_foto
    FROM ventas v
    LEFT JOIN usuarios u ON u.id = v.usuario_id
    LEFT JOIN socios   s ON s.id = v.socio_id
    WHERE v.id = @Id;

    SELECT
        vi.id, vi.venta_id, vi.producto_id, vi.descripcion,
        vi.cantidad, vi.precio_unitario, vi.subtotal,
        ISNULL(p.nombre, vi.descripcion) AS producto_nombre,
        p.foto                           AS producto_foto
    FROM ventas_items vi
    LEFT JOIN productos p ON p.id = vi.producto_id
    WHERE vi.venta_id = @Id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. BUSCAR VENTAS
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_BuscarVentas
    @Texto       NVARCHAR(150) = '',
    @MetodoPago  VARCHAR(20)   = 'todos',
    @FechaDesde  DATE          = NULL,
    @FechaHasta  DATE          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -30, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        v.id, v.usuario_id, v.socio_id, v.total, v.metodo_pago,
        v.observaciones, v.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')          AS usuario_nombre,
        ISNULL(s.nombre + ' ' + s.apellido, 'Publico general')  AS socio_nombre,
        s.numero_socio,
        s.foto AS socio_foto,
        (SELECT COUNT(*) FROM ventas_items vi WHERE vi.venta_id = v.id) AS cantidad_items
    FROM ventas v
    LEFT JOIN usuarios u ON u.id = v.usuario_id
    LEFT JOIN socios   s ON s.id = v.socio_id
    WHERE CAST(v.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND (
            @Texto = ''
         OR s.nombre    LIKE '%' + @Texto + '%'
         OR s.apellido  LIKE '%' + @Texto + '%'
         OR s.dni       LIKE '%' + @Texto + '%'
         OR CAST(v.id AS VARCHAR(20)) LIKE '%' + @Texto + '%'
          )
      AND (@MetodoPago = 'todos' OR v.metodo_pago = @MetodoPago)
    ORDER BY v.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3b. BUSCAR VENTAS POR USUARIO (para instructor)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_BuscarVentasPorUsuario
    @Texto       NVARCHAR(150) = '',
    @MetodoPago  VARCHAR(20)   = 'todos',
    @FechaDesde  DATE          = NULL,
    @FechaHasta  DATE          = NULL,
    @UsuarioId   BIGINT        = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = CAST(GETDATE() AS DATE);
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        v.id, v.usuario_id, v.socio_id, v.total, v.metodo_pago,
        v.observaciones, v.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')          AS usuario_nombre,
        ISNULL(s.nombre + ' ' + s.apellido, 'Publico general')  AS socio_nombre,
        s.numero_socio,
        s.foto AS socio_foto,
        (SELECT COUNT(*) FROM ventas_items vi WHERE vi.venta_id = v.id) AS cantidad_items
    FROM ventas v
    LEFT JOIN usuarios u ON u.id = v.usuario_id
    LEFT JOIN socios   s ON s.id = v.socio_id
    WHERE CAST(v.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND v.usuario_id = @UsuarioId
      AND (
            @Texto = ''
         OR s.nombre    LIKE '%' + @Texto + '%'
         OR s.apellido  LIKE '%' + @Texto + '%'
         OR s.dni       LIKE '%' + @Texto + '%'
         OR CAST(v.id AS VARCHAR(20)) LIKE '%' + @Texto + '%'
          )
      AND (@MetodoPago = 'todos' OR v.metodo_pago = @MetodoPago)
    ORDER BY v.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. REGISTRAR VENTA (con transaccion)
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_RegistrarVenta
    @UsuarioId     BIGINT,
    @SocioId       BIGINT          = NULL,
    @MetodoPago    VARCHAR(20)     = 'efectivo',
    @Observaciones NVARCHAR(300)   = NULL,
    @Items         TipoVentaItem   READONLY
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM @Items)
    BEGIN
        RAISERROR('La venta no puede estar vacia.', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;

    BEGIN TRY
        DECLARE @ProductoId  BIGINT;
        DECLARE @Cantidad    INT;
        DECLARE @StockActual INT;
        DECLARE @ProdNombre  NVARCHAR(200);

        DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT producto_id, cantidad FROM @Items WHERE producto_id IS NOT NULL;

        OPEN cur;
        FETCH NEXT FROM cur INTO @ProductoId, @Cantidad;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            SELECT @StockActual = stock, @ProdNombre = nombre
            FROM productos WHERE id = @ProductoId;

            IF @StockActual IS NULL
            BEGIN
                CLOSE cur; DEALLOCATE cur;
                ROLLBACK TRANSACTION;
                RAISERROR('Uno de los productos no existe.', 16, 1);
                RETURN;
            END

            IF @StockActual < @Cantidad
            BEGIN
                CLOSE cur; DEALLOCATE cur;
                ROLLBACK TRANSACTION;
                DECLARE @Msg NVARCHAR(300);
                SET @Msg = 'Stock insuficiente para "' + @ProdNombre +
                           '". Disponible: ' + CAST(@StockActual AS VARCHAR(10)) +
                           ', solicitado: '  + CAST(@Cantidad AS VARCHAR(10)) + '.';
                RAISERROR(@Msg, 16, 1);
                RETURN;
            END

            FETCH NEXT FROM cur INTO @ProductoId, @Cantidad;
        END

        CLOSE cur; DEALLOCATE cur;

        DECLARE @Total DECIMAL(12,2);
        SELECT @Total = SUM(cantidad * precio_unitario) FROM @Items;

        IF ISNULL(@Total, 0) <= 0
        BEGIN
            ROLLBACK TRANSACTION;
            RAISERROR('El total debe ser mayor a 0.', 16, 1);
            RETURN;
        END

        INSERT INTO ventas (usuario_id, socio_id, total, metodo_pago, observaciones)
        VALUES (@UsuarioId, @SocioId, @Total, @MetodoPago, @Observaciones);

        DECLARE @VentaId BIGINT;
        SET @VentaId = SCOPE_IDENTITY();

        INSERT INTO ventas_items (venta_id, producto_id, descripcion, cantidad, precio_unitario, subtotal)
        SELECT
            @VentaId,
            producto_id,
            descripcion,
            cantidad,
            precio_unitario,
            cantidad * precio_unitario
        FROM @Items;

        UPDATE p
        SET p.stock = p.stock - i.cantidad
        FROM productos p
        INNER JOIN @Items i ON i.producto_id = p.id
        WHERE i.producto_id IS NOT NULL;

        DECLARE @DetalleCaja NVARCHAR(200);
        SET @DetalleCaja = 'Venta #' + CAST(@VentaId AS VARCHAR(20)) +
                           ' (' + CAST((SELECT COUNT(*) FROM @Items) AS VARCHAR(10)) + ' items)';

        INSERT INTO caja_movimientos
            (tipo, subtipo, usuario_id, socio_id, venta_id, detalle, metodo_pago, monto)
        VALUES
            ('ingreso_venta', 'Venta de productos',
             @UsuarioId, @SocioId, @VentaId,
             @DetalleCaja, @MetodoPago, @Total);

        COMMIT TRANSACTION;
        SELECT @VentaId AS id, @Total AS total;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        DECLARE @ErrMsg NVARCHAR(2000);
        SET @ErrMsg = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. ANULAR VENTA
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_AnularVenta
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM ventas WHERE id = @Id)
    BEGIN
        RAISERROR('La venta no existe.', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;

    BEGIN TRY
        UPDATE p
        SET p.stock = p.stock + vi.cantidad
        FROM productos p
        INNER JOIN ventas_items vi ON vi.producto_id = p.id
        WHERE vi.venta_id = @Id AND vi.producto_id IS NOT NULL;

        DELETE FROM caja_movimientos WHERE venta_id = @Id;
        DELETE FROM ventas_items     WHERE venta_id = @Id;
        DELETE FROM ventas           WHERE id = @Id;

        COMMIT TRANSACTION;
        SELECT 1 AS ok;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        DECLARE @ErrMsg NVARCHAR(2000);
        SET @ErrMsg = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. ESTADÍSTICAS
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_EstadisticasVentas
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Hoy          DATE;
    DECLARE @PrimerDiaMes DATE;

    SET @Hoy          = CAST(GETDATE() AS DATE);
    SET @PrimerDiaMes = DATEFROMPARTS(YEAR(@Hoy), MONTH(@Hoy), 1);

    SELECT
        ISNULL((SELECT COUNT(*) FROM ventas
                WHERE CAST(creado_en AS DATE) = @Hoy), 0)            AS ventas_hoy,
        ISNULL((SELECT SUM(total)  FROM ventas
                WHERE CAST(creado_en AS DATE) = @Hoy), 0)            AS total_hoy,
        ISNULL((SELECT COUNT(*) FROM ventas
                WHERE CAST(creado_en AS DATE) >= @PrimerDiaMes), 0)  AS ventas_mes,
        ISNULL((SELECT SUM(total)  FROM ventas
                WHERE CAST(creado_en AS DATE) >= @PrimerDiaMes), 0)  AS total_mes;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. TOP PRODUCTOS DEL MES
-- ─────────────────────────────────────────────────────────────
CREATE PROCEDURE sp_TopProductosVendidos
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PrimerDiaMes DATE;
    SET @PrimerDiaMes = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

    SELECT TOP 5
        p.id, p.nombre, p.foto,
        SUM(vi.cantidad) AS unidades_vendidas,
        SUM(vi.subtotal) AS total_facturado
    FROM ventas_items vi
    INNER JOIN ventas    v ON v.id = vi.venta_id
    INNER JOIN productos p ON p.id = vi.producto_id
    WHERE CAST(v.creado_en AS DATE) >= @PrimerDiaMes
    GROUP BY p.id, p.nombre, p.foto
    ORDER BY SUM(vi.cantidad) DESC;
END;
GO
