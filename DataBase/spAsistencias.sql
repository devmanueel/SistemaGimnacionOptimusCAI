-- ============================================================
--  STORED PROCEDURES — TABLA registros_acceso
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
--  v2.0 — Control de límites de asistencia por semana + domingo
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- 1. VALIDAR ACCESO POR DNI
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_ValidarAccesoPorDni', 'P') IS NOT NULL
    DROP PROCEDURE sp_ValidarAccesoPorDni;
GO
CREATE PROCEDURE sp_ValidarAccesoPorDni
    @Dni          CHAR(8),
    @MetodoAcceso VARCHAR(20) = 'dni_pin',
    @MembresiaId  BIGINT      = NULL   -- NULL = tomar la primera activa (socio con 1 sola membresía)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE membresias
    SET estado = 'vencida'
    WHERE estado = 'activa' AND fecha_vencimiento < CAST(GETDATE() AS DATE);

    DECLARE @SocioId       BIGINT;
    DECLARE @SocioActivo   BIT;
    DECLARE @SocioNombre   NVARCHAR(200);
    DECLARE @NumeroSocio   INT;
    DECLARE @Foto          VARBINARY(MAX);
    DECLARE @ActividadId   BIGINT;
    DECLARE @ActividadNom  NVARCHAR(150);
    DECLARE @DiasSemana    NVARCHAR(MAX);
    DECLARE @DiaActual     INT = DATEPART(WEEKDAY, GETDATE());
    DECLARE @VencActual    DATE;
    DECLARE @Resultado     VARCHAR(50);
    DECLARE @Mensaje       NVARCHAR(300);
    DECLARE @LimiteTotal   TINYINT;
    DECLARE @LimiteSemana  TINYINT;
    DECLARE @Lunes         DATE;
    DECLARE @Sabado        DATE;
    DECLARE @MembresiasActivas INT;

    DECLARE @DiaApp INT;
    SET @DiaApp = CASE @DiaActual
        WHEN 1 THEN 7 WHEN 2 THEN 1 WHEN 3 THEN 2
        WHEN 4 THEN 3 WHEN 5 THEN 4 WHEN 6 THEN 5 WHEN 7 THEN 6
    END;

    -- ── 1. Buscar socio ────────────────────────────────────
    SELECT
        @SocioId     = id,
        @SocioActivo = activo,
        @SocioNombre = nombre + ' ' + apellido,
        @NumeroSocio = numero_socio,
        @Foto        = foto
    FROM socios
    WHERE dni = @Dni AND eliminado_en IS NULL;

    IF @SocioId IS NULL
    BEGIN
        SELECT CAST(NULL AS BIGINT) AS socio_id, 'denegado_socio_inactivo' AS resultado,
               'No se encontró ningún socio con ese DNI.' AS mensaje,
               CAST(NULL AS NVARCHAR(200)) AS socio_nombre, CAST(NULL AS INT) AS numero_socio,
               CAST(NULL AS VARBINARY(MAX)) AS foto, CAST(NULL AS NVARCHAR(150)) AS actividad_nombre,
               CAST(NULL AS DATE) AS fecha_vencimiento, CAST(NULL AS BIGINT) AS registro_id,
               CAST(0 AS BIT) AS descuento_aplicado;
        RETURN;
    END

    -- ── 2. Validar socio activo ────────────────────────────
    IF @SocioActivo = 0
    BEGIN
        SET @Resultado = 'denegado_socio_inactivo';
        SET @Mensaje   = 'El socio está dado de baja.';
        INSERT INTO registros_acceso (socio_id, metodo_acceso, resultado)
        VALUES (@SocioId, @MetodoAcceso, @Resultado);
        SELECT @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
               @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio, @Foto AS foto,
               CAST(NULL AS NVARCHAR(150)) AS actividad_nombre, CAST(NULL AS DATE) AS fecha_vencimiento,
               CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id, CAST(0 AS BIT) AS descuento_aplicado;
        RETURN;
    END

    -- ── 3. Si no viene @MembresiaId, verificar cuántas activas tiene ──
    IF @MembresiaId IS NULL
    BEGIN
        SELECT @MembresiasActivas = COUNT(*)
        FROM membresias
        WHERE socio_id = @SocioId AND estado = 'activa';

        -- Si tiene más de una, devolver señal para que la app muestre el selector
        IF @MembresiasActivas > 1
        BEGIN
            SELECT @SocioId AS socio_id, 'seleccionar_membresia' AS resultado,
                   'El socio tiene más de una membresía activa. Seleccioná la actividad.' AS mensaje,
                   @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio, @Foto AS foto,
                   CAST(NULL AS NVARCHAR(150)) AS actividad_nombre, CAST(NULL AS DATE) AS fecha_vencimiento,
                   CAST(NULL AS BIGINT) AS registro_id, CAST(0 AS BIT) AS descuento_aplicado;
            RETURN;
        END

        -- Si tiene solo una, tomarla automáticamente
        SELECT TOP 1 @MembresiaId = id
        FROM membresias
        WHERE socio_id = @SocioId AND estado = 'activa'
        ORDER BY fecha_vencimiento ASC;
    END

    -- ── 4. Cargar datos de la membresía seleccionada ───────
    SELECT
        @ActividadId  = m.actividad_id,
        @ActividadNom = a.nombre,
        @DiasSemana   = a.dias_semana,
        @VencActual   = m.fecha_vencimiento,
        @LimiteTotal  = a.limite_total,
        @LimiteSemana = a.limite_por_semana
    FROM membresias m
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.id = @MembresiaId AND m.socio_id = @SocioId AND m.estado = 'activa';

    IF @ActividadId IS NULL
    BEGIN
        SET @Resultado = 'denegado_vencimiento';
        SET @Mensaje   = 'La membresía seleccionada no está activa o no pertenece a este socio.';
        INSERT INTO registros_acceso (socio_id, metodo_acceso, resultado)
        VALUES (@SocioId, @MetodoAcceso, @Resultado);
        SELECT @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
               @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio, @Foto AS foto,
               CAST(NULL AS NVARCHAR(150)) AS actividad_nombre, CAST(NULL AS DATE) AS fecha_vencimiento,
               CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id, CAST(0 AS BIT) AS descuento_aplicado;
        RETURN;
    END

    -- ── 5. ¿Es domingo? ────────────────────────────────────
    IF @DiaActual = 1
    BEGIN
        SET @Resultado = 'denegado_dia';
        SET @Mensaje   = 'El gimnasio no abre los domingos.';
        INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado)
        VALUES (@SocioId, @MembresiaId, @MetodoAcceso, @Resultado);
        SELECT @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
               @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio, @Foto AS foto,
               @ActividadNom AS actividad_nombre, @VencActual AS fecha_vencimiento,
               CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id, CAST(0 AS BIT) AS descuento_aplicado;
        RETURN;
    END

    -- ── 6. Validar día permitido por actividad ─────────────
    IF @DiasSemana IS NOT NULL
    BEGIN
        IF NOT (
            @DiasSemana LIKE '%[' + CAST(@DiaApp AS VARCHAR(2)) + ']%'
         OR @DiasSemana LIKE '%,' + CAST(@DiaApp AS VARCHAR(2)) + ',%'
         OR @DiasSemana LIKE '%[' + CAST(@DiaApp AS VARCHAR(2)) + ',%'
         OR @DiasSemana LIKE '%,' + CAST(@DiaApp AS VARCHAR(2)) + ']%'
        )
        BEGIN
            SET @Resultado = 'denegado_dia';
            SET @Mensaje   = 'Hoy no es un día permitido para esta actividad.';
            INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado)
            VALUES (@SocioId, @MembresiaId, @MetodoAcceso, @Resultado);
            SELECT @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
                   @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio, @Foto AS foto,
                   @ActividadNom AS actividad_nombre, @VencActual AS fecha_vencimiento,
                   CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id, CAST(0 AS BIT) AS descuento_aplicado;
            RETURN;
        END
    END

    -- ── 7. ¿Ya asistió hoy a ESTA membresía? ──────────────
    -- (por membresía, no por socio — puede ir a Gimnasio y Boxeo el mismo día)
    IF EXISTS (
        SELECT 1 FROM registros_acceso
        WHERE membresia_id = @MembresiaId
          AND resultado    = 'permitido'
          AND CAST(accedido_en AS DATE) = CAST(GETDATE() AS DATE)
    )
    BEGIN
        SET @Resultado = 'denegado_limite';
        SET @Mensaje   = 'El socio ya registró su ingreso hoy para esta actividad.';
        INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado)
        VALUES (@SocioId, @MembresiaId, @MetodoAcceso, @Resultado);
        SELECT @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
               @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio, @Foto AS foto,
               @ActividadNom AS actividad_nombre, @VencActual AS fecha_vencimiento,
               CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id, CAST(0 AS BIT) AS descuento_aplicado;
        RETURN;
    END

    -- ── 8. Validar límite total ────────────────────────────
    IF @LimiteTotal IS NOT NULL
    BEGIN
        IF (SELECT COUNT(*) FROM registros_acceso
            WHERE membresia_id = @MembresiaId AND resultado = 'permitido') >= @LimiteTotal
        BEGIN
            SET @Resultado = 'denegado_limite';
            SET @Mensaje   = 'Ya usaste tu única asistencia permitida para esta membresía.';
            INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado)
            VALUES (@SocioId, @MembresiaId, @MetodoAcceso, @Resultado);
            SELECT @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
                   @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio, @Foto AS foto,
                   @ActividadNom AS actividad_nombre, @VencActual AS fecha_vencimiento,
                   CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id, CAST(0 AS BIT) AS descuento_aplicado;
            RETURN;
        END
    END

    -- ── 9. Validar límite semanal ──────────────────────────
    IF @LimiteSemana IS NOT NULL
    BEGIN
        SET @Lunes  = DATEADD(DAY, 2 - DATEPART(WEEKDAY, CAST(GETDATE() AS DATE)), CAST(GETDATE() AS DATE));
        SET @Sabado = DATEADD(DAY, 7 - DATEPART(WEEKDAY, CAST(GETDATE() AS DATE)), CAST(GETDATE() AS DATE));

        IF (SELECT COUNT(*) FROM registros_acceso
            WHERE membresia_id = @MembresiaId AND resultado = 'permitido'
              AND CAST(accedido_en AS DATE) BETWEEN @Lunes AND @Sabado) >= @LimiteSemana
        BEGIN
            SET @Resultado = 'denegado_limite';
            SET @Mensaje   = 'Ya alcanzaste el límite de asistencias para esta semana.';
            INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado)
            VALUES (@SocioId, @MembresiaId, @MetodoAcceso, @Resultado);
            SELECT @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
                   @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio, @Foto AS foto,
                   @ActividadNom AS actividad_nombre, @VencActual AS fecha_vencimiento,
                   CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id, CAST(0 AS BIT) AS descuento_aplicado;
            RETURN;
        END
    END

    -- ── 10. ACCESO PERMITIDO ───────────────────────────────
    SET @Resultado = 'permitido';
    SET @Mensaje   = '¡Acceso permitido! A entrenar.';
    INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado)
    VALUES (@SocioId, @MembresiaId, @MetodoAcceso, @Resultado);
    SELECT @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
           @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio, @Foto AS foto,
           @ActividadNom AS actividad_nombre, @VencActual AS fecha_vencimiento,
           CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id, CAST(1 AS BIT) AS descuento_aplicado;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER REGISTROS DEL DÍA
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_ObtenerAccesosDelDia', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerAccesosDelDia;
GO
CREATE PROCEDURE sp_ObtenerAccesosDelDia
    @Limite INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Limite)
        r.id, r.socio_id, r.membresia_id, r.metodo_acceso, r.resultado, r.accedido_en,
        s.numero_socio,
        s.nombre + ' ' + s.apellido AS socio_nombre,
        s.foto                       AS socio_foto,
        s.dni                        AS socio_dni,
        ISNULL(a.nombre, '-')        AS actividad_nombre
    FROM registros_acceso r
    INNER JOIN socios      s ON s.id = r.socio_id
    LEFT  JOIN membresias  m ON m.id = r.membresia_id
    LEFT  JOIN actividades a ON a.id = m.actividad_id
    WHERE CAST(r.accedido_en AS DATE) = CAST(GETDATE() AS DATE)
    ORDER BY r.accedido_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. BUSCAR ACCESOS POR RANGO
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_BuscarAccesos', 'P') IS NOT NULL
    DROP PROCEDURE sp_BuscarAccesos;
GO
CREATE PROCEDURE sp_BuscarAccesos
    @Texto           NVARCHAR(100) = '',
    @FiltroResultado VARCHAR(30)   = 'todos',
    @FechaDesde      DATE          = NULL,
    @FechaHasta      DATE          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FechaDesde IS NULL SET @FechaDesde = DATEADD(DAY, -7, CAST(GETDATE() AS DATE));
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    SELECT
        r.id, r.socio_id, r.membresia_id, r.metodo_acceso, r.resultado, r.accedido_en,
        s.numero_socio,
        s.nombre + ' ' + s.apellido AS socio_nombre,
        s.foto                       AS socio_foto,
        s.dni                        AS socio_dni,
        ISNULL(a.nombre, '-')        AS actividad_nombre
    FROM registros_acceso r
    INNER JOIN socios      s ON s.id = r.socio_id
    LEFT  JOIN membresias  m ON m.id = r.membresia_id
    LEFT  JOIN actividades a ON a.id = m.actividad_id
    WHERE CAST(r.accedido_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND (
            @Texto = ''
         OR s.nombre   LIKE '%' + @Texto + '%'
         OR s.apellido LIKE '%' + @Texto + '%'
         OR s.dni      LIKE '%' + @Texto + '%'
         OR CAST(s.numero_socio AS VARCHAR(20)) LIKE '%' + @Texto + '%'
          )
      AND (
            @FiltroResultado = 'todos'
         OR (@FiltroResultado = 'permitido' AND r.resultado = 'permitido')
         OR (@FiltroResultado = 'denegado'  AND r.resultado LIKE 'denegado%')
          )
    ORDER BY r.accedido_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. COLUMNAS DE LÍMITES (si no existen)
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('actividades') AND name = 'limite_por_semana'
)
    ALTER TABLE actividades ADD limite_por_semana TINYINT NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('actividades') AND name = 'limite_total'
)
    ALTER TABLE actividades ADD limite_total TINYINT NULL;
GO

-- Cargar valores según cada actividad (ajustar nombres si difieren)
UPDATE actividades SET limite_por_semana = 2,    limite_total = NULL WHERE nombre = 'Gimnasio 2 Veces Por Semana';
UPDATE actividades SET limite_por_semana = 3,    limite_total = NULL WHERE nombre = 'Gimnasio 3 Veces Por Semana';
UPDATE actividades SET limite_por_semana = NULL, limite_total = NULL WHERE nombre = 'Gimnasio Todos Los Dias';
GO

-- Eliminar cualquier CHECK constraint existente sobre la columna resultado
DECLARE @ck NVARCHAR(200);
SELECT @ck = cc.name
FROM sys.check_constraints cc
INNER JOIN sys.columns c
    ON c.object_id = cc.parent_object_id
   AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID('registros_acceso')
  AND c.name = 'resultado';

IF @ck IS NOT NULL
    EXEC('ALTER TABLE registros_acceso DROP CONSTRAINT [' + @ck + ']');
GO

-- Recrear con nombre explícito y valores actualizados
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('registros_acceso')
      AND name = 'CK_registros_resultado'
)
ALTER TABLE registros_acceso
    ADD CONSTRAINT CK_registros_resultado
    CHECK (resultado IN (
        'permitido',
        'denegado_socio_inactivo',
        'denegado_dia',
        'denegado_vencimiento',
        'denegado_huella',
        'denegado_limite',
        'denegado_limite_semana',
        'seleccionar_membresia'
    ));