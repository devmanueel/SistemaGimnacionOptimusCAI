-- ============================================================
--  STORED PROCEDURES — TABLA ventas + ventas_items
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
--
--  IMPORTANTE: La inserción de una venta usa TRANSACTION para
--  garantizar atomicidad: o se inserta la venta + todos sus
--  items + se descuenta stock + se registra en caja, o no se
--  inserta nada.
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- TIPO DE TABLA para pasar los items del carrito al SP
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.types WHERE name = 'TipoVentaItem' AND is_table_type = 1)
BEGIN
    CREATE TYPE TipoVentaItem AS TABLE (
        producto_id  BIGINT       NOT NULL,
        cantidad     INT          NOT NULL,
        precio_unit  DECIMAL(12,2) NOT NULL
    );
END
GO

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER VENTAS (con datos joineados)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerVentas
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -7, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        v.id, v.usuario_id, v.socio_id, v.metodo_pago, v.total, v.estado, v.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')  AS usuario_nombre,
        ISNULL(s.nombre + ' ' + s.apellido, '')         AS socio_nombre,
        s.numero_socio,
        (SELECT COUNT(*)             FROM ventas_items WHERE venta_id = v.id) AS cantidad_items,
        (SELECT ISNULL(SUM(cantidad), 0) FROM ventas_items WHERE venta_id = v.id) AS unidades_totales
    FROM ventas v
    LEFT JOIN usuarios u ON u.id = v.usuario_id
    LEFT JOIN socios   s ON s.id = v.socio_id
    WHERE CAST(v.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
    ORDER BY v.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. BUSCAR VENTAS (texto + filtro estado + rango)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_BuscarVentas
    @Texto         NVARCHAR(150) = '',
    @FiltroEstado  VARCHAR(20)   = 'todas',
    @FechaDesde    DATE          = NULL,
    @FechaHasta    DATE          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -7, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        v.id, v.usuario_id, v.socio_id, v.metodo_pago, v.total, v.estado, v.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS usuario_nombre,
        ISNULL(s.nombre + ' ' + s.apellido, '')        AS socio_nombre,
        s.numero_socio,
        (SELECT COUNT(*)             FROM ventas_items WHERE venta_id = v.id) AS cantidad_items,
        (SELECT ISNULL(SUM(cantidad), 0) FROM ventas_items WHERE venta_id = v.id) AS unidades_totales
    FROM ventas v
    LEFT JOIN usuarios u ON u.id = v.usuario_id
    LEFT JOIN socios   s ON s.id = v.socio_id
    WHERE CAST(v.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND (
            @Texto = ''
         OR s.nombre   LIKE '%' + @Texto + '%'
         OR s.apellido LIKE '%' + @Texto + '%'
         OR s.dni      LIKE '%' + @Texto + '%'
         OR CAST(s.numero_socio AS VARCHAR(20)) LIKE '%' + @Texto + '%'
         OR CAST(v.id AS VARCHAR(20))           LIKE '%' + @Texto + '%'
          )
      AND (
            @FiltroEstado = 'todas'
         OR v.estado = @FiltroEstado
          )
    ORDER BY v.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. OBTENER VENTA POR ID (incluye items)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerVentaPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- Resultset 1: la venta
    SELECT
        v.id, v.usuario_id, v.socio_id, v.metodo_pago, v.total, v.estado, v.creado_en,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema') AS usuario_nombre,
        ISNULL(s.nombre + ' ' + s.apellido, '')        AS socio_nombre,
        s.numero_socio
    FROM ventas v
    LEFT JOIN usuarios u ON u.id = v.usuario_id
    LEFT JOIN socios   s ON s.id = v.socio_id
    WHERE v.id = @Id;

    -- Resultset 2: los items
    SELECT
        vi.id, vi.venta_id, vi.producto_id, vi.cantidad, vi.precio_unit, vi.subtotal,
        p.nombre AS producto_nombre,
        p.foto   AS producto_foto
    FROM ventas_items vi
    INNER JOIN productos p ON p.id = vi.producto_id
    WHERE vi.venta_id = @Id
    ORDER BY vi.id ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. REGISTRAR VENTA — el corazón del módulo
--    Recibe los items vía TVP. Hace:
--      1. Valida stock disponible de cada item
--      2. Inserta la venta
--      3. Inserta cada item
--      4. Descuenta stock de cada producto
--      5. Inserta movimiento en caja_movimientos
--    Todo dentro de una TRANSACTION.
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_RegistrarVenta
    @UsuarioId   BIGINT,
    @SocioId     BIGINT          = NULL,
    @MetodoPago  VARCHAR(20)     = 'efectivo',
    @Items       TipoVentaItem   READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Validar que haya items
    IF NOT EXISTS (SELECT 1 FROM @Items)
    BEGIN
        RAISERROR('La venta no tiene items. Agregá al menos un producto.', 16, 1);
        RETURN;
    END

    -- Validar stock disponible
    DECLARE @ProductoSinStock NVARCHAR(150);
    SELECT TOP 1 @ProductoSinStock = p.nombre
    FROM @Items i
    INNER JOIN productos p ON p.id = i.producto_id
    WHERE p.stock < i.cantidad;

    IF @ProductoSinStock IS NOT NULL
    BEGIN
        DECLARE @msg NVARCHAR(300) = 'Stock insuficiente del producto: ' + @ProductoSinStock;
        RAISERROR(@msg, 16, 1);
        RETURN;
    END

    -- Validar que todos los productos estén activos
    DECLARE @ProductoInactivo NVARCHAR(150);
    SELECT TOP 1 @ProductoInactivo = p.nombre
    FROM @Items i
    INNER JOIN productos p ON p.id = i.producto_id
    WHERE p.activo = 0;

    IF @ProductoInactivo IS NOT NULL
    BEGIN
        DECLARE @msg2 NVARCHAR(300) = 'El producto está desactivado: ' + @ProductoInactivo;
        RAISERROR(@msg2, 16, 1);
        RETURN;
    END

    -- Calcular total
    DECLARE @Total DECIMAL(12,2);
    SELECT @Total = SUM(cantidad * precio_unit) FROM @Items;

    BEGIN TRANSACTION;

    BEGIN TRY
        -- Insertar la venta
        INSERT INTO ventas (usuario_id, socio_id, metodo_pago, total, estado)
        VALUES (@UsuarioId, @SocioId, @MetodoPago, @Total, 'completada');

        DECLARE @VentaId BIGINT = SCOPE_IDENTITY();

        -- Insertar items
        INSERT INTO ventas_items (venta_id, producto_id, cantidad, precio_unit, subtotal)
        SELECT @VentaId, producto_id, cantidad, precio_unit, cantidad * precio_unit
        FROM @Items;

        -- Descontar stock
        UPDATE p
        SET p.stock = p.stock - i.cantidad
        FROM productos p
        INNER JOIN @Items i ON i.producto_id = p.id;

        -- Registrar movimiento de caja
        DECLARE @DetalleCaja NVARCHAR(500);
        DECLARE @CantItems INT;
        SELECT @CantItems = COUNT(*) FROM @Items;

        SET @DetalleCaja = 'Venta de ' + CAST(@CantItems AS VARCHAR(10)) +
                           CASE WHEN @CantItems = 1 THEN ' producto' ELSE ' productos' END;

        IF @SocioId IS NOT NULL
        BEGIN
            DECLARE @SocioNom NVARCHAR(200);
            SELECT @SocioNom = nombre + ' ' + apellido FROM socios WHERE id = @SocioId;
            SET @DetalleCaja = @DetalleCaja + ' a ' + ISNULL(@SocioNom, 'socio');
        END

        INSERT INTO caja_movimientos
            (tipo, subtipo, usuario_id, socio_id, venta_id, detalle, metodo_pago, monto)
        VALUES
            ('ingreso_venta', 'Venta de productos', @UsuarioId, @SocioId, @VentaId,
             @DetalleCaja, @MetodoPago, @Total);

        COMMIT TRANSACTION;

        SELECT @VentaId AS id, @Total AS total;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @ErrMsg NVARCHAR(500) = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. ANULAR VENTA
--    Marca la venta como anulada, repone el stock y cancela
--    el movimiento de caja correspondiente.
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_AnularVenta
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Estado VARCHAR(20);
    SELECT @Estado = estado FROM ventas WHERE id = @Id;

    IF @Estado IS NULL
    BEGIN
        RAISERROR('La venta no existe.', 16, 1);
        RETURN;
    END

    IF @Estado = 'anulada'
    BEGIN
        RAISERROR('Esta venta ya está anulada.', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;

    BEGIN TRY
        -- Reponer stock de cada producto
        UPDATE p
        SET p.stock = p.stock + vi.cantidad
        FROM productos p
        INNER JOIN ventas_items vi ON vi.producto_id = p.id
        WHERE vi.venta_id = @Id;

        -- Marcar venta como anulada
        UPDATE ventas SET estado = 'anulada' WHERE id = @Id;

        -- Eliminar el movimiento de caja correspondiente
        DELETE FROM caja_movimientos WHERE venta_id = @Id AND tipo = 'ingreso_venta';

        COMMIT TRANSACTION;

        SELECT 1 AS ok;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @ErrMsg NVARCHAR(500) = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. ESTADÍSTICAS DE VENTAS
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_EstadisticasVentas
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Hoy DATE       = CAST(GETDATE() AS DATE);
    DECLARE @PrimerDiaMes DATE = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

    SELECT
        ISNULL(SUM(CASE WHEN CAST(creado_en AS DATE) = @Hoy AND estado = 'completada'
                        THEN total ELSE 0 END), 0) AS total_dia,
        ISNULL(SUM(CASE WHEN CAST(creado_en AS DATE) >= @PrimerDiaMes AND estado = 'completada'
                        THEN total ELSE 0 END), 0) AS total_mes,
        ISNULL(SUM(CASE WHEN CAST(creado_en AS DATE) = @Hoy AND estado = 'completada'
                        THEN 1 ELSE 0 END), 0) AS cantidad_dia,
        ISNULL(SUM(CASE WHEN CAST(creado_en AS DATE) >= @PrimerDiaMes AND estado = 'completada'
                        THEN 1 ELSE 0 END), 0) AS cantidad_mes
    FROM ventas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. TOP PRODUCTOS VENDIDOS DEL MES
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_TopProductosDelMes
    @Cantidad INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PrimerDiaMes DATE = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

    SELECT TOP (@Cantidad)
        p.id, p.nombre, p.foto,
        ISNULL(SUM(vi.cantidad), 0)              AS unidades_vendidas,
        ISNULL(SUM(vi.subtotal), 0)              AS total_vendido
    FROM ventas_items vi
    INNER JOIN ventas    v ON v.id = vi.venta_id
    INNER JOIN productos p ON p.id = vi.producto_id
    WHERE v.estado = 'completada'
      AND CAST(v.creado_en AS DATE) >= @PrimerDiaMes
    GROUP BY p.id, p.nombre, p.foto
    ORDER BY unidades_vendidas DESC;
END;
GO

-- Verificación
EXEC sp_EstadisticasVentas;
GO