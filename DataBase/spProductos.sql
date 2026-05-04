-- ============================================================
--  STORED PROCEDURES — TABLA productos
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
-- ============================================================

-- Agregar columna foto si no existe (para mostrar imagen del producto)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'productos' AND COLUMN_NAME = 'foto'
)
BEGIN
    ALTER TABLE productos ADD foto VARBINARY(MAX) NULL;
END
GO

-- Agregar columna categoria si no existe
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'productos' AND COLUMN_NAME = 'categoria'
)
BEGIN
    ALTER TABLE productos ADD categoria NVARCHAR(50) NULL;
END
GO

-- ─────────────────────────────────────────────────────────────
-- 1. OBTENER TODOS LOS PRODUCTOS
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerProductos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.id, p.nombre, p.descripcion, p.precio,
        p.stock, p.stock_min, p.activo, p.creado_en,
        p.foto, p.categoria,
        -- Cantidad vendida histórica
        ISNULL((SELECT SUM(vi.cantidad) FROM ventas_items vi
                WHERE vi.producto_id = p.id), 0) AS cantidad_vendida
    FROM productos p
    ORDER BY p.nombre ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER POR ID
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerProductoPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.id, p.nombre, p.descripcion, p.precio,
        p.stock, p.stock_min, p.activo, p.creado_en,
        p.foto, p.categoria,
        ISNULL((SELECT SUM(vi.cantidad) FROM ventas_items vi
                WHERE vi.producto_id = p.id), 0) AS cantidad_vendida
    FROM productos p
    WHERE p.id = @Id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. BUSCAR (texto + categoría + filtro stock)
--   FiltroStock: 'todos' / 'sin_stock' / 'bajo_stock' / 'con_stock'
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_BuscarProductos
    @Texto         NVARCHAR(150) = '',
    @Categoria     NVARCHAR(50)  = NULL,
    @FiltroStock   VARCHAR(20)   = 'todos',
    @SoloActivos   BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.id, p.nombre, p.descripcion, p.precio,
        p.stock, p.stock_min, p.activo, p.creado_en,
        p.foto, p.categoria,
        ISNULL((SELECT SUM(vi.cantidad) FROM ventas_items vi
                WHERE vi.producto_id = p.id), 0) AS cantidad_vendida
    FROM productos p
    WHERE (
            @Texto = ''
         OR p.nombre      LIKE '%' + @Texto + '%'
         OR p.descripcion LIKE '%' + @Texto + '%'
          )
      AND (@Categoria IS NULL OR p.categoria = @Categoria)
      AND (@SoloActivos = 0 OR p.activo = 1)
      AND (
            @FiltroStock = 'todos'
         OR (@FiltroStock = 'sin_stock'  AND p.stock <= 0)
         OR (@FiltroStock = 'bajo_stock' AND p.stock > 0 AND p.stock <= p.stock_min)
         OR (@FiltroStock = 'con_stock'  AND p.stock > p.stock_min)
          )
    ORDER BY p.nombre ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. INSERTAR PRODUCTO
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_InsertarProducto
    @Nombre      NVARCHAR(150),
    @Descripcion NVARCHAR(300)  = NULL,
    @Categoria   NVARCHAR(50)   = NULL,
    @Precio      DECIMAL(12,2),
    @Stock       INT            = 0,
    @StockMin    INT            = 0,
    @Foto        VARBINARY(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM productos WHERE nombre = @Nombre)
    BEGIN
        SELECT -1 AS id;
        RETURN;
    END

    INSERT INTO productos
        (nombre, descripcion, categoria, precio, stock, stock_min, activo, foto)
    VALUES
        (@Nombre, @Descripcion, @Categoria, @Precio, @Stock, @StockMin, 1, @Foto);

    SELECT SCOPE_IDENTITY() AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. MODIFICAR PRODUCTO
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ModificarProducto
    @Id          BIGINT,
    @Nombre      NVARCHAR(150),
    @Descripcion NVARCHAR(300)  = NULL,
    @Categoria   NVARCHAR(50)   = NULL,
    @Precio      DECIMAL(12,2),
    @StockMin    INT            = 0,
    @Foto        VARBINARY(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM productos WHERE nombre = @Nombre AND id <> @Id)
    BEGIN
        RAISERROR('Ya existe otro producto con ese nombre.', 16, 1);
        RETURN;
    END

    UPDATE productos SET
        nombre      = @Nombre,
        descripcion = @Descripcion,
        categoria   = @Categoria,
        precio      = @Precio,
        stock_min   = @StockMin,
        foto        = ISNULL(@Foto, foto)
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. AJUSTAR STOCK (manual)
--    Tipo: 'sumar' / 'restar' / 'ajustar' (setea el valor exacto)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_AjustarStock
    @Id        BIGINT,
    @Tipo      VARCHAR(10),
    @Cantidad  INT
AS
BEGIN
    SET NOCOUNT ON;

    IF @Tipo NOT IN ('sumar', 'restar', 'ajustar')
    BEGIN
        RAISERROR('Tipo de ajuste inválido. Usar: sumar, restar o ajustar.', 16, 1);
        RETURN;
    END

    IF @Cantidad < 0
    BEGIN
        RAISERROR('La cantidad no puede ser negativa.', 16, 1);
        RETURN;
    END

    IF @Tipo = 'sumar'
        UPDATE productos SET stock = stock + @Cantidad WHERE id = @Id;
    ELSE IF @Tipo = 'restar'
    BEGIN
        -- No permitir stock negativo
        DECLARE @StockActual INT;
        SELECT @StockActual = stock FROM productos WHERE id = @Id;
        IF @StockActual - @Cantidad < 0
        BEGIN
            RAISERROR('No hay stock suficiente para descontar esa cantidad.', 16, 1);
            RETURN;
        END
        UPDATE productos SET stock = stock - @Cantidad WHERE id = @Id;
    END
    ELSE IF @Tipo = 'ajustar'
        UPDATE productos SET stock = @Cantidad WHERE id = @Id;

    -- Retornar el stock final
    SELECT stock AS stock_actual FROM productos WHERE id = @Id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. CAMBIAR ESTADO (activar / desactivar)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_CambiarEstadoProducto
    @Id     BIGINT,
    @Activo BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE productos SET activo = @Activo WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 8. ELIMINAR (solo si nunca se vendió)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_EliminarProducto
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM ventas_items WHERE producto_id = @Id)
    BEGIN
        RAISERROR('No se puede eliminar: el producto tiene ventas registradas. Podés desactivarlo.', 16, 1);
        RETURN;
    END

    DELETE FROM productos WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 9. LISTAR CATEGORÍAS (para el combobox)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ListarCategorias
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT categoria
    FROM productos
    WHERE categoria IS NOT NULL AND categoria <> ''
    ORDER BY categoria ASC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 10. ESTADÍSTICAS
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_EstadisticasProductos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        COUNT(*)                                                               AS total,
        ISNULL(SUM(CASE WHEN activo = 1                       THEN 1 ELSE 0 END), 0) AS activos,
        ISNULL(SUM(CASE WHEN stock <= 0                       THEN 1 ELSE 0 END), 0) AS sin_stock,
        ISNULL(SUM(CASE WHEN stock > 0 AND stock <= stock_min THEN 1 ELSE 0 END), 0) AS bajo_stock,
        ISNULL(SUM(stock * precio), 0)                                          AS valor_inventario
    FROM productos;
END;
GO

-- Verificación
EXEC sp_EstadisticasProductos;
EXEC sp_ObtenerProductos;
GO  
