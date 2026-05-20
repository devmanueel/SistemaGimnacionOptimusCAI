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
    @MetodoAcceso VARCHAR(20) = 'dni_pin'
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE membresias
    SET estado = 'vencida'
    WHERE estado = 'activa' AND fecha_vencimiento < CAST(GETDATE() AS DATE);

    DECLARE @SocioId      BIGINT;
    DECLARE @SocioActivo  BIT;
    DECLARE @SocioNombre  NVARCHAR(200);
    DECLARE @NumeroSocio  INT;
    DECLARE @Foto         VARBINARY(MAX);
    DECLARE @MembresiaId  BIGINT;
    DECLARE @ActividadId  BIGINT;
    DECLARE @ActividadNom NVARCHAR(150);
    DECLARE @DiasSemana   NVARCHAR(MAX);
    DECLARE @VencActual   DATE;
    DECLARE @DiaActual    INT = DATEPART(WEEKDAY, GETDATE());
    DECLARE @Resultado    VARCHAR(50);
    DECLARE @Mensaje      NVARCHAR(300);
    DECLARE @DiasSesiones TINYINT;
    DECLARE @Lunes        DATE;
    DECLARE @Sabado       DATE;
    DECLARE @AsistenciasSemana TINYINT;

    DECLARE @DiaApp INT;
    SET @DiaApp = CASE @DiaActual
        WHEN 1 THEN 7
        WHEN 2 THEN 1
        WHEN 3 THEN 2
        WHEN 4 THEN 3
        WHEN 5 THEN 4
        WHEN 6 THEN 5
        WHEN 7 THEN 6
    END;

    -- 1. Buscar socio
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
        SELECT
            CAST(NULL AS BIGINT)        AS socio_id,
            'denegado_socio_inactivo'   AS resultado,
            'No se encontró ningún socio con ese DNI.' AS mensaje,
            CAST(NULL AS NVARCHAR(200)) AS socio_nombre,
            CAST(NULL AS INT)           AS numero_socio,
            CAST(NULL AS VARBINARY(MAX))AS foto,
            CAST(NULL AS NVARCHAR(150)) AS actividad_nombre,
            CAST(NULL AS DATE)          AS fecha_vencimiento,
            CAST(NULL AS BIGINT)        AS registro_id,
            CAST(0 AS BIT)              AS descuento_aplicado;
        RETURN;
    END

    -- 2. Validar socio activo
    IF @SocioActivo = 0
    BEGIN
        SET @Resultado = 'denegado_socio_inactivo';
        SET @Mensaje   = 'El socio está dado de baja.';

        INSERT INTO registros_acceso (socio_id, metodo_acceso, resultado)
        VALUES (@SocioId, @MetodoAcceso, @Resultado);

        SELECT
            @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
            @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio,
            @Foto AS foto,
            CAST(NULL AS NVARCHAR(150)) AS actividad_nombre,
            CAST(NULL AS DATE) AS fecha_vencimiento,
            CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id,
            CAST(0 AS BIT) AS descuento_aplicado;
        RETURN;
    END

    -- 3. Buscar membresía activa
    SELECT TOP 1
        @MembresiaId  = m.id,
        @ActividadId  = m.actividad_id,
        @ActividadNom = a.nombre,
        @DiasSemana   = a.dias_semana,
        @VencActual   = m.fecha_vencimiento,
        @DiasSesiones = a.dias_sesiones
    FROM membresias m
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.socio_id = @SocioId AND m.estado = 'activa'
    ORDER BY m.fecha_vencimiento ASC;

    IF @MembresiaId IS NULL
    BEGIN
        SET @Resultado = 'denegado_vencimiento';
        SET @Mensaje   = 'El socio no tiene ninguna membresía activa.';

        INSERT INTO registros_acceso (socio_id, metodo_acceso, resultado)
        VALUES (@SocioId, @MetodoAcceso, @Resultado);

        SELECT
            @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
            @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio,
            @Foto AS foto,
            CAST(NULL AS NVARCHAR(150)) AS actividad_nombre,
            CAST(NULL AS DATE) AS fecha_vencimiento,
            CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id,
            CAST(0 AS BIT) AS descuento_aplicado;
        RETURN;
    END

    -- 4. ¿Es domingo? (el gimnasio no abre)
    IF @DiaActual = 1
    BEGIN
        SET @Resultado = 'denegado_dia';
        SET @Mensaje   = 'El gimnasio no abre los domingos.';

        INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado)
        VALUES (@SocioId, @MembresiaId, @MetodoAcceso, @Resultado);

        SELECT
            @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
            @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio,
            @Foto AS foto,
            @ActividadNom AS actividad_nombre, @VencActual AS fecha_vencimiento,
            CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id,
            CAST(0 AS BIT) AS descuento_aplicado;
        RETURN;
    END

    -- 5. Validar día permitido por actividad
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

            SELECT
                @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
                @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio,
                @Foto AS foto,
                @ActividadNom AS actividad_nombre, @VencActual AS fecha_vencimiento,
                CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id,
                CAST(0 AS BIT) AS descuento_aplicado;
            RETURN;
        END
    END

    -- 6. Validar límite de asistencias por semana (usando dias_sesiones)
    IF @DiasSesiones IS NOT NULL AND @DiasSesiones > 0
    BEGIN
        SET @Lunes  = DATEADD(DAY, 2 - @DiaActual, CAST(GETDATE() AS DATE));
        SET @Sabado = DATEADD(DAY, 7 - @DiaActual, CAST(GETDATE() AS DATE));

        SELECT @AsistenciasSemana = COUNT(*)
        FROM registros_acceso
        WHERE socio_id  = @SocioId
          AND resultado = 'permitido'
          AND CAST(accedido_en AS DATE) BETWEEN @Lunes AND @Sabado;

        IF @AsistenciasSemana >= @DiasSesiones
        BEGIN
            SET @Resultado = 'denegado_limite_semana';
            SET @Mensaje   = 'Ya alcanzaste el límite de asistencias para esta semana.';

            INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado)
            VALUES (@SocioId, @MembresiaId, @MetodoAcceso, @Resultado);

            SELECT
                @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
                @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio,
                @Foto AS foto,
                @ActividadNom AS actividad_nombre, @VencActual AS fecha_vencimiento,
                CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id,
                CAST(0 AS BIT) AS descuento_aplicado;
            RETURN;
        END
    END

    -- 7. ACCESO PERMITIDO
    --    Verificar si ya marcó hoy (descuento_aplicado = 0 si ya lo hizo)
    DECLARE @YaMarcoHoy BIT = 0;
    IF EXISTS (
        SELECT 1 FROM registros_acceso
        WHERE socio_id  = @SocioId
          AND resultado = 'permitido'
          AND CAST(accedido_en AS DATE) = CAST(GETDATE() AS DATE)
    )
        SET @YaMarcoHoy = 1;

    SET @Resultado = 'permitido';
    SET @Mensaje   = CASE @YaMarcoHoy
        WHEN 1 THEN '¡Ya registraste tu entrada hoy! Bienvenido de nuevo.'
        ELSE         '¡Acceso permitido! A entrenar.'
    END;

    INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado)
    VALUES (@SocioId, @MembresiaId, @MetodoAcceso, @Resultado);

    SELECT
        @SocioId AS socio_id, @Resultado AS resultado, @Mensaje AS mensaje,
        @SocioNombre AS socio_nombre, @NumeroSocio AS numero_socio,
        @Foto AS foto,
        @ActividadNom AS actividad_nombre, @VencActual AS fecha_vencimiento,
        CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id,
        CAST(1 - @YaMarcoHoy AS BIT) AS descuento_aplicado;
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
        ISNULL(a.nombre, '—')        AS actividad_nombre
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
        ISNULL(a.nombre, '—')        AS actividad_nombre
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
-- 4. ESTADÍSTICAS DEL DÍA
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_EstadisticasAccesos', 'P') IS NOT NULL
    DROP PROCEDURE sp_EstadisticasAccesos;
GO
CREATE PROCEDURE sp_EstadisticasAccesos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ISNULL(SUM(CASE WHEN resultado = 'permitido'     THEN 1 ELSE 0 END), 0) AS permitidos_hoy,
        ISNULL(SUM(CASE WHEN resultado LIKE 'denegado%'  THEN 1 ELSE 0 END), 0) AS denegados_hoy,
        ISNULL(COUNT(DISTINCT CASE WHEN resultado = 'permitido' THEN socio_id END), 0) AS socios_unicos_hoy,
        ISNULL((SELECT COUNT(*) FROM registros_acceso
                WHERE resultado = 'permitido'
                  AND CAST(accedido_en AS DATE) >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE))), 0) AS accesos_semana
    FROM registros_acceso
    WHERE CAST(accedido_en AS DATE) = CAST(GETDATE() AS DATE);
END;
GO
