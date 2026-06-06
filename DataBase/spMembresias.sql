-- ============================================================
--  STORED PROCEDURES - TABLA membresias
--  Sistema Gimnasio OptimusCAI - SQL Server / LocalDB
--  Reglas de negocio:
--    - Pago unico e irrevocable.
--    - Fechas solo avanzan.
--    - Historial de alta, modificacion y anulacion.
--    - tipo_plan: mensual, semanal o clase.
--    - Cambio de plan: misma categoria y dias_sesiones mayor.
-- ============================================================

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 0. ESTRUCTURA --- columnas y tabla historial (idempotentes)
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('membresias') AND name = 'tipo_plan'
)
    ALTER TABLE membresias ADD tipo_plan VARCHAR(20) NOT NULL DEFAULT 'mensual';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('membresias') AND name = 'upgrade_realizado'
)
    ALTER TABLE membresias ADD upgrade_realizado BIT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('membresias') AND name = 'actividad_original'
)
    ALTER TABLE membresias ADD actividad_original BIGINT NULL;
GO

-- Agregar columna categoria a actividades (para regla de cambio de plan)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('actividades') AND name = 'categoria'
)
    ALTER TABLE actividades ADD categoria VARCHAR(50) NULL;
GO

IF OBJECT_ID('membresia_historial') IS NULL
CREATE TABLE membresia_historial (
    id             BIGINT        IDENTITY(1,1) PRIMARY KEY,
    membresia_id   BIGINT        NOT NULL REFERENCES membresias(id),
    tipo_evento    VARCHAR(30)   NOT NULL,   -- 'alta' | 'renovacion' | 'modificacion' | 'anulacion'
    fecha_desde    DATE          NOT NULL,
    fecha_hasta    DATE          NOT NULL,
    importe        DECIMAL(10,2) NULL,
    metodo_pago    VARCHAR(30)   NULL,
    registrado_por BIGINT        NULL REFERENCES usuarios(id),
    creado_en      DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 1. ACTUALIZAR ESTADOS AUTOM--TICAMENTE
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_ActualizarEstadosMembresias', 'P') IS NOT NULL
    DROP PROCEDURE sp_ActualizarEstadosMembresias;
GO
CREATE PROCEDURE sp_ActualizarEstadosMembresias
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE membresias
    SET estado = 'vencida',
        actualizado_en = GETDATE()
    WHERE estado = 'activa' AND fecha_vencimiento < CAST(GETDATE() AS DATE);

    SELECT @@ROWCOUNT AS actualizadas;
END;
GO

IF OBJECT_ID('sp_ObtenerNotificacionesMembresiasPorVencer', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerNotificacionesMembresiasPorVencer;
GO
CREATE PROCEDURE sp_ObtenerNotificacionesMembresiasPorVencer
    @DiasAntes INT = 7
AS
BEGIN
    SET NOCOUNT ON;

    IF @DiasAntes < 0 SET @DiasAntes = 0;
    IF @DiasAntes > 30 SET @DiasAntes = 30;

    DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);
    DECLARE @Limite DATE = DATEADD(DAY, @DiasAntes, @Hoy);

    UPDATE membresias
    SET estado = 'vencida',
        actualizado_en = GETDATE()
    WHERE estado = 'activa'
      AND fecha_vencimiento < @Hoy;

    SELECT
        m.id AS membresia_id,
        s.id AS socio_id,
        s.numero_socio,
        s.nombre + ' ' + s.apellido AS socio_nombre,
        s.telefono,
        a.nombre AS actividad_nombre,
        m.fecha_vencimiento,
        DATEDIFF(DAY, @Hoy, m.fecha_vencimiento) AS dias_para_vencer
    FROM membresias m
    INNER JOIN socios s ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.estado = 'activa'
      AND m.fecha_vencimiento BETWEEN @Hoy AND @Limite
      AND s.activo = 1
      AND s.eliminado_en IS NULL
    ORDER BY m.fecha_vencimiento ASC, s.apellido ASC, s.nombre ASC;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 2. OBTENER TODAS
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_ObtenerMembresias', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerMembresias;
GO
CREATE PROCEDURE sp_ObtenerMembresias
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE membresias
    SET estado = 'vencida'
    WHERE estado = 'activa' AND fecha_vencimiento < CAST(GETDATE() AS DATE);

    SELECT
        m.id, m.socio_id, m.actividad_id, m.instructor_id,
        m.fecha_inicio, m.fecha_vencimiento,
        m.monto_pagado, m.metodo_pago, m.estado, m.tipo_plan,
        m.registrado_por, m.observaciones,
        m.creado_en, m.actualizado_en,
        s.numero_socio,
        s.nombre + ' ' + s.apellido AS socio_nombre,
        s.dni                       AS socio_dni,
        s.foto                      AS socio_foto,
        a.nombre                    AS actividad_nombre,
        a.tipo                      AS actividad_tipo,
        a.categoria                 AS actividad_categoria,
        a.dias_sesiones             AS actividad_nivel,
        ISNULL(i.nombre + ' ' + i.apellido, 'Sin asignar') AS instructor_nombre,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')     AS registrado_por_nombre,
        DATEDIFF(DAY, CAST(GETDATE() AS DATE), m.fecha_vencimiento) AS dias_para_vencer
    FROM membresias m
    INNER JOIN socios      s ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    LEFT  JOIN usuarios    i ON i.id = m.instructor_id
    LEFT  JOIN usuarios    u ON u.id = m.registrado_por
    ORDER BY m.creado_en DESC;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 3. OBTENER POR ID
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_ObtenerMembresiaPorId', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerMembresiaPorId;
GO
CREATE PROCEDURE sp_ObtenerMembresiaPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.id, m.socio_id, m.actividad_id, m.instructor_id,
        m.fecha_inicio, m.fecha_vencimiento,
        m.monto_pagado, m.metodo_pago, m.estado, m.tipo_plan,
        m.registrado_por, m.observaciones,
        m.creado_en, m.actualizado_en,
        s.numero_socio,
        s.nombre + ' ' + s.apellido AS socio_nombre,
        s.dni                       AS socio_dni,
        s.foto                      AS socio_foto,
        a.nombre                    AS actividad_nombre,
        a.tipo                      AS actividad_tipo,
        a.categoria                 AS actividad_categoria,
        a.dias_sesiones             AS actividad_nivel,
        ISNULL(i.nombre + ' ' + i.apellido, 'Sin asignar') AS instructor_nombre,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')     AS registrado_por_nombre,
        DATEDIFF(DAY, CAST(GETDATE() AS DATE), m.fecha_vencimiento) AS dias_para_vencer
    FROM membresias m
    INNER JOIN socios      s ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    LEFT  JOIN usuarios    i ON i.id = m.instructor_id
    LEFT  JOIN usuarios    u ON u.id = m.registrado_por
    WHERE m.id = @Id;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 4. BUSCAR
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_BuscarMembresias', 'P') IS NOT NULL
    DROP PROCEDURE sp_BuscarMembresias;
GO
CREATE PROCEDURE sp_BuscarMembresias
    @Texto        NVARCHAR(100) = '',
    @FiltroEstado VARCHAR(20)   = 'todos'
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE membresias
    SET estado = 'vencida'
    WHERE estado = 'activa' AND fecha_vencimiento < CAST(GETDATE() AS DATE);

    SELECT
        m.id, m.socio_id, m.actividad_id, m.instructor_id,
        m.fecha_inicio, m.fecha_vencimiento,
        m.monto_pagado, m.metodo_pago, m.estado, m.tipo_plan,
        m.registrado_por, m.observaciones,
        m.creado_en, m.actualizado_en,
        s.numero_socio,
        s.nombre + ' ' + s.apellido AS socio_nombre,
        s.dni                       AS socio_dni,
        s.foto                      AS socio_foto,
        a.nombre                    AS actividad_nombre,
        a.tipo                      AS actividad_tipo,
        a.categoria                 AS actividad_categoria,
        a.dias_sesiones             AS actividad_nivel,
        ISNULL(i.nombre + ' ' + i.apellido, 'Sin asignar') AS instructor_nombre,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')     AS registrado_por_nombre,
        DATEDIFF(DAY, CAST(GETDATE() AS DATE), m.fecha_vencimiento) AS dias_para_vencer
    FROM membresias m
    INNER JOIN socios      s ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    LEFT  JOIN usuarios    i ON i.id = m.instructor_id
    LEFT  JOIN usuarios    u ON u.id = m.registrado_por
    WHERE (
            @Texto = ''
          OR s.nombre   LIKE '%' + @Texto + '%'
          OR s.apellido LIKE '%' + @Texto + '%'
          OR s.dni      LIKE '%' + @Texto + '%'
          OR a.nombre   LIKE '%' + @Texto + '%'
          OR CAST(s.numero_socio AS VARCHAR(20)) LIKE '%' + @Texto + '%'
           )
      AND (
            @FiltroEstado = 'todos'
         OR (@FiltroEstado = 'por_vencer'
              AND m.estado = 'activa'
              AND DATEDIFF(DAY, CAST(GETDATE() AS DATE), m.fecha_vencimiento) BETWEEN 0 AND 7)
          OR m.estado = @FiltroEstado
           )
    ORDER BY m.creado_en DESC;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 5. INSERTAR --- cobrar cuota nueva
--    Reglas:
--      -- Valida que no exista membres--a activa de la misma actividad
--      -- Calcula vencimiento autom--tico seg--n el plan
--      -- Registra el cobro en caja autom--ticamente
--      -- Guarda en historial
--      -- fechas autom--ticas: inicio = hoy, vencimiento = hoy + 31 d--as
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_InsertarMembresia', 'P') IS NOT NULL
    DROP PROCEDURE sp_InsertarMembresia;
GO
CREATE PROCEDURE sp_InsertarMembresia
    @SocioId          BIGINT,
    @ActividadId      BIGINT,
    @InstructorId     BIGINT          = NULL,
    @FechaInicio      DATE,
    @FechaVencimiento DATE,
    @TipoPlan         VARCHAR(20)     = 'mensual',
    @MontoPagado      DECIMAL(12,2),
    @MetodoPago       VARCHAR(20)     = 'efectivo',
    @RegistradoPor    BIGINT,
    @Observaciones    VARCHAR(500)    = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Fechas autom--ticas (regla de negocio: siempre 31 d--as)
    SET @FechaInicio = CAST(GETDATE() AS DATE);
    SET @FechaVencimiento = DATEADD(DAY, 31, @FechaInicio);

    IF NOT EXISTS (SELECT 1 FROM socios WHERE id = @SocioId AND eliminado_en IS NULL)
    BEGIN
        RAISERROR('El socio no existe o fue eliminado.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM actividades WHERE id = @ActividadId AND activo = 1)
    BEGIN
        RAISERROR('La actividad no existe o estÃ¡ inactiva.', 16, 1);
        RETURN;
    END

    -- Validar 1: No permitir si ya tiene membres--a activa con la MISMA ACTIVIDAD
    DECLARE @MembresiaMismaActividadId BIGINT;
    DECLARE @ActividadNombre NVARCHAR(150);

    SELECT TOP 1 
        @MembresiaMismaActividadId = m.id,
        @ActividadNombre = a.nombre
    FROM membresias m
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.socio_id = @SocioId
      AND m.estado IN ('activa', 'vencida')
      AND m.actividad_id = @ActividadId;

    IF @MembresiaMismaActividadId IS NOT NULL
    BEGIN
        RAISERROR('El socio ya tiene una membresÃ­a activa con la actividad "%s". No se puede crear una nueva membresÃ­a con la misma actividad.', 16, 1, @ActividadNombre);
        RETURN;
    END

    -- Validar 2: No permitir si ya tiene membres--a activa en la misma CATEGOR--A (diferente actividad)
    DECLARE @CategoriaNueva VARCHAR(50);
    DECLARE @CategoriaExistente VARCHAR(50);
    DECLARE @MembresiaExistenteId BIGINT;

    SELECT @CategoriaNueva = categoria 
    FROM actividades 
    WHERE id = @ActividadId;

    SELECT TOP 1 
        @CategoriaExistente = a.categoria,
        @MembresiaExistenteId = m.id
    FROM membresias m
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.socio_id = @SocioId
      AND m.estado IN ('activa', 'vencida')
      AND a.categoria = @CategoriaNueva
      AND m.actividad_id <> @ActividadId;

    IF @MembresiaExistenteId IS NOT NULL
    BEGIN
        RAISERROR('El socio ya tiene una membresÃ­a activa en la categorÃ­a "%s". No se puede crear otra membresÃ­a en la misma categorÃ­a.', 16, 1, @CategoriaExistente);
        RETURN;
    END

    INSERT INTO membresias
        (socio_id, actividad_id, instructor_id, fecha_inicio, fecha_vencimiento,
         tipo_plan, monto_pagado, metodo_pago, estado, registrado_por, observaciones)
    VALUES
        (@SocioId, @ActividadId, @InstructorId, @FechaInicio, @FechaVencimiento,
         @TipoPlan, @MontoPagado, @MetodoPago, 'activa', @RegistradoPor, @Observaciones);

    DECLARE @NuevaId BIGINT = SCOPE_IDENTITY();

    -- Registrar el ingreso en caja autom--ticamente
    INSERT INTO caja_movimientos
        (tipo, subtipo, usuario_id, socio_id, membresia_id, actividad_id,
         detalle, metodo_pago, monto)
    SELECT
        'ingreso_cuota',
        'Pago de cuota',
        @RegistradoPor,
        @SocioId,
        @NuevaId,
        @ActividadId,
        a.nombre + ' (' + s.nombre + ' ' + s.apellido + ')',
        @MetodoPago,
        @MontoPagado
    FROM socios s, actividades a
    WHERE s.id = @SocioId AND a.id = @ActividadId;

    -- Historial
    INSERT INTO membresia_historial
        (membresia_id, tipo_evento, fecha_desde, fecha_hasta, importe, metodo_pago, registrado_por)
    VALUES
        (@NuevaId, 'alta', @FechaInicio, @FechaVencimiento, @MontoPagado, @MetodoPago, @RegistradoPor);

    SELECT @NuevaId AS id, @FechaVencimiento AS fecha_vencimiento;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 5.5 OBTENER CATEGOR--A DE MEMBRES--A ACTIVA POR SOCIO
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_ObtenerCategoriaMembresiaActiva', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerCategoriaMembresiaActiva;
GO
CREATE PROCEDURE sp_ObtenerCategoriaMembresiaActiva
    @SocioId     BIGINT,
    @ActividadId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CategoriaNueva VARCHAR(50);
    
    SELECT @CategoriaNueva = categoria 
    FROM actividades 
    WHERE id = @ActividadId;
    
    SELECT TOP 1 a.categoria
    FROM membresias m
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.socio_id = @SocioId
      AND m.estado IN ('activa', 'vencida')
      AND a.categoria = @CategoriaNueva
      AND m.actividad_id <> @ActividadId;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 6. MODIFICAR --- solo instructor, observaciones y fecha (solo adelante)
--    Regla: la fecha_vencimiento solo puede avanzar, nunca retroceder.
--    Regla de cambio de plan: solo misma categor--a y solo upgrade (dias_sesiones mayor)
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_ModificarMembresia', 'P') IS NOT NULL
    DROP PROCEDURE sp_ModificarMembresia;
GO
CREATE PROCEDURE sp_ModificarMembresia
    @Id               BIGINT,
    @InstructorId     BIGINT        = NULL,
    @ActividadId      BIGINT        = NULL,
    @FechaVencimiento DATE,
    @MontoPagado      DECIMAL(12,2) = NULL,
    @TipoPlan         VARCHAR(20)   = NULL,
    @MetodoPago       VARCHAR(20)   = NULL,
    @Observaciones    VARCHAR(500)  = NULL,
    @RegistradoPor    BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @VencActual   DATE;
    DECLARE @FechaInicio  DATE;
    DECLARE @ActividadActualId BIGINT;

    SELECT
        @VencActual  = fecha_vencimiento,
        @FechaInicio = fecha_inicio,
        @ActividadActualId = actividad_id
    FROM membresias WHERE id = @Id;

    IF @VencActual IS NULL
    BEGIN
        RAISERROR('La membresÃ­a no existe.', 16, 1);
        RETURN;
    END

    -- Fechas solo avanzan: no se puede retroceder el vencimiento
    IF @FechaVencimiento < @VencActual
    BEGIN
        RAISERROR('La fecha de vencimiento no puede ser anterior a la actual. Los dÃ­as solo pueden aumentar.', 16, 1);
        RETURN;
    END

    -- Validaci--n de cambio de plan (solo si se cambia la actividad)
    IF @ActividadId IS NOT NULL AND @ActividadId <> @ActividadActualId
    BEGIN
        DECLARE @CategoriaActual VARCHAR(50);
        DECLARE @DiasSesionesActual TINYINT;
        DECLARE @CategoriaNueva VARCHAR(50);
        DECLARE @DiasSesionesNuevo TINYINT;

        -- Obtener categoria y dias_sesiones actual
        SELECT 
            @CategoriaActual = a.categoria,
            @DiasSesionesActual = a.dias_sesiones
        FROM actividades a
        INNER JOIN membresias m ON m.actividad_id = a.id
        WHERE m.id = @Id;

        -- Obtener categoria y dias_sesiones nuevo
        SELECT 
            @CategoriaNueva = categoria,
            @DiasSesionesNuevo = dias_sesiones
        FROM actividades
        WHERE id = @ActividadId;

        -- Validar misma categor--a
        IF @CategoriaActual IS NOT NULL AND @CategoriaNueva IS NOT NULL 
           AND @CategoriaActual <> @CategoriaNueva
        BEGIN
            RAISERROR('No se puede cambiar a otra categorÃ­a. El cambio de plan solo estÃ¡ permitido dentro de la misma categorÃ­a.', 16, 1);
            RETURN;
        END

        -- Validar solo upgrade (dias_sesiones mayor)
        IF @DiasSesionesNuevo <= @DiasSesionesActual
        BEGIN
            RAISERROR('Solo se permite cambiar a un plan superior (upgrade). El downgrade no estÃ¡ permitido.', 16, 1);
            RETURN;
        END
    END

    UPDATE membresias SET
        instructor_id     = @InstructorId,
        actividad_id      = ISNULL(@ActividadId, actividad_id),
        fecha_vencimiento = @FechaVencimiento,
        monto_pagado      = ISNULL(@MontoPagado, monto_pagado),
        tipo_plan         = ISNULL(@TipoPlan, tipo_plan),
        metodo_pago       = ISNULL(@MetodoPago, metodo_pago),
        observaciones     = @Observaciones,
        estado = CASE
            WHEN estado = 'vencida' AND @FechaVencimiento >= CAST(GETDATE() AS DATE)
                THEN 'activa'
            ELSE estado
        END,
        actualizado_en = GETDATE()
    WHERE id = @Id;

    -- Capturar antes del IF porque el IF resetea @@ROWCOUNT a 0
    DECLARE @FilasAfectadas INT = @@ROWCOUNT;

    -- Historial (solo si la fecha avanz--)
    IF @FechaVencimiento > @VencActual
        INSERT INTO membresia_historial
            (membresia_id, tipo_evento, fecha_desde, fecha_hasta, registrado_por)
        VALUES
            (@Id, 'modificacion', @FechaInicio, @FechaVencimiento, @RegistradoPor);

    SELECT @FilasAfectadas AS filas_afectadas;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 7. CAMBIAR ESTADO --- solo activa / vencida / cancelada
--    Sin 'suspendida': el sistema no permite congelar membres--as.
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_CambiarEstadoMembresia', 'P') IS NOT NULL
    DROP PROCEDURE sp_CambiarEstadoMembresia;
GO
CREATE PROCEDURE sp_CambiarEstadoMembresia
    @Id     BIGINT,
    @Estado VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Estado NOT IN ('activa', 'vencida', 'cancelada')
    BEGIN
        RAISERROR('Estado invÃ¡lido. Los estados permitidos son: activa, vencida, cancelada.', 16, 1);
        RETURN;
    END

    UPDATE membresias
    SET estado = @Estado,
        actualizado_en = GETDATE()
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 8. RENOVAR --- suma d--as al vencimiento + cobra en caja
--    Si ya venci--, suma desde hoy. Si est-- vigente, suma desde vencimiento.
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_RenovarMembresia', 'P') IS NOT NULL
    DROP PROCEDURE sp_RenovarMembresia;
GO
CREATE PROCEDURE sp_RenovarMembresia
    @Id            BIGINT,
    @MontoPagado   DECIMAL(12,2),
    @MetodoPago    VARCHAR(20)  = 'efectivo',
    @RegistradoPor BIGINT,
    @DiasASumar    INT          = 0,
    @ActividadId   BIGINT       = NULL,
    @InstructorId  BIGINT       = NULL,
    @Observaciones NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SocioId     BIGINT;
    DECLARE @ActividadActualId BIGINT;
    DECLARE @ActividadFinalId BIGINT;
    DECLARE @TipoPlan    VARCHAR(20);
    DECLARE @Estado      VARCHAR(20);

    SELECT @SocioId = socio_id, @ActividadActualId = actividad_id,
           @TipoPlan = tipo_plan, @Estado = estado
    FROM membresias WHERE id = @Id;

    IF @TipoPlan IS NULL
    BEGIN
        RAISERROR('MembresÃ­a no encontrada.', 16, 1);
        RETURN;
    END

    SET @ActividadFinalId = ISNULL(@ActividadId, @ActividadActualId);

    IF NOT EXISTS (SELECT 1 FROM actividades WHERE id = @ActividadFinalId AND activo = 1)
    BEGIN
        RAISERROR('La actividad seleccionada no existe o esta inactiva.', 16, 1);
        RETURN;
    END

    -- Calcular d--as seg--n plan si no se pas-- expl--citamente
    DECLARE @Dias INT;
    IF @DiasASumar > 0
        SET @Dias = @DiasASumar;
    ELSE
        SET @Dias = CASE @TipoPlan
            WHEN 'clase_suelta'  THEN 1
            WHEN 'quincenal'     THEN 15
            WHEN 'mensual'       THEN 31
            WHEN 'trimestral'    THEN 90
            WHEN 'semestral'     THEN 180
            WHEN 'anual'         THEN 365
            WHEN 'clase'         THEN 1
            WHEN 'semanal'       THEN 7
            ELSE 31
        END;

    DECLARE @Hoy      DATE = CAST(GETDATE() AS DATE);
    DECLARE @Vencim   DATE;

    -- Obtener vencimiento actual
    SELECT @Vencim = fecha_vencimiento FROM membresias WHERE id = @Id;

    -- Si esta activa y vigente, sumar al vencimiento actual. Si no, arrancar desde hoy.
    IF @Estado = 'activa' AND @Vencim >= @Hoy
        SET @Vencim = DATEADD(DAY, @Dias, @Vencim);
    ELSE
        SET @Vencim = DATEADD(DAY, @Dias, @Hoy);

    UPDATE membresias SET
        actividad_id      = @ActividadFinalId,
        instructor_id     = @InstructorId,
        fecha_inicio      = @Hoy,
        fecha_vencimiento = @Vencim,
        monto_pagado      = @MontoPagado,
        metodo_pago       = @MetodoPago,
        estado            = 'activa',
        observaciones     = CASE
                                WHEN @Observaciones IS NULL OR LTRIM(RTRIM(@Observaciones)) = '' THEN observaciones
                                ELSE @Observaciones
                             END,
        actualizado_en    = GETDATE()
    WHERE id = @Id;

    INSERT INTO caja_movimientos
        (tipo, subtipo, usuario_id, socio_id, membresia_id, actividad_id,
         detalle, metodo_pago, monto)
    SELECT
        'ingreso_cuota', 'RenovaciÃ³n de cuota', @RegistradoPor, @SocioId,
        @Id, @ActividadFinalId,
        'RenovaciÃ³n de ' + a.nombre + ' (' + s.nombre + ' ' + s.apellido + ')',
        @MetodoPago, @MontoPagado
    FROM socios s, actividades a
    WHERE s.id = @SocioId AND a.id = @ActividadFinalId;

    INSERT INTO membresia_historial
        (membresia_id, tipo_evento, fecha_desde, fecha_hasta, importe, metodo_pago, registrado_por)
    VALUES
        (@Id, 'renovacion', @Hoy, @Vencim, @MontoPagado, @MetodoPago, @RegistradoPor);

    SELECT @Vencim AS nueva_fecha_vencimiento;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 9. ANULAR (soft cancel --- no borra hist--rico)
--    Solo el rol admin puede llegar a ejecutar esto (validado en UI/Controller).
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_EliminarMembresia', 'P') IS NOT NULL
    DROP PROCEDURE sp_EliminarMembresia;
GO
CREATE PROCEDURE sp_EliminarMembresia
    @Id            BIGINT,
    @RegistradoPor BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FechaInicio DATE;
    DECLARE @FechaVenc   DATE;

    SELECT @FechaInicio = fecha_inicio, @FechaVenc = fecha_vencimiento
    FROM membresias WHERE id = @Id;

    UPDATE membresias
    SET estado = 'cancelada',
        actualizado_en = GETDATE()
    WHERE id = @Id;

    IF @@ROWCOUNT > 0 AND @FechaInicio IS NOT NULL
        INSERT INTO membresia_historial
            (membresia_id, tipo_evento, fecha_desde, fecha_hasta, registrado_por)
        VALUES
            (@Id, 'anulacion', @FechaInicio, @FechaVenc, @RegistradoPor);

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 10. LISTAR SOCIOS PARA COMBOBOX
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_ListarSociosParaCombo', 'P') IS NOT NULL
    DROP PROCEDURE sp_ListarSociosParaCombo;
GO
CREATE PROCEDURE sp_ListarSociosParaCombo
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, numero_socio, nombre, apellido, dni
    FROM socios
    WHERE activo = 1 AND eliminado_en IS NULL
    ORDER BY apellido, nombre;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 11. LISTAR ACTIVIDADES PARA COMBOBOX
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_ListarActividadesParaCombo', 'P') IS NOT NULL
    DROP PROCEDURE sp_ListarActividadesParaCombo;
GO
CREATE PROCEDURE sp_ListarActividadesParaCombo
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, nombre, tipo, dias_sesiones, precio, categoria, dias_sesiones AS nivel
    FROM actividades
    WHERE activo = 1
    ORDER BY nombre;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- 12. LISTAR INSTRUCTORES PARA COMBOBOX
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_ListarInstructoresParaCombo', 'P') IS NOT NULL
    DROP PROCEDURE sp_ListarInstructoresParaCombo;
GO
CREATE PROCEDURE sp_ListarInstructoresParaCombo
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.id, u.nombre, u.apellido
    FROM usuarios u
    INNER JOIN roles r ON r.id = u.rol_id
    WHERE r.nombre = 'empleado'
      AND u.activo = 1
      AND u.eliminado_en IS NULL
    ORDER BY u.apellido, u.nombre;
END;
GO

-----------------  NUEVO -----------------
IF OBJECT_ID('sp_ObtenerMembresiasActivasPorDni', 'P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerMembresiasActivasPorDni;
GO
CREATE PROCEDURE sp_ObtenerMembresiasActivasPorDni
    @Dni CHAR(8)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        m.id              AS membresia_id,
        a.nombre          AS actividad_nombre,
        m.fecha_vencimiento,
        CASE WHEN a.tipo = 'mensual' THEN a.dias_sesiones ELSE NULL END AS limite_por_semana,
        CASE WHEN a.tipo = 'mensual_con_clases' THEN a.dias_sesiones ELSE NULL END AS limite_total
    FROM socios s
    INNER JOIN membresias  m ON m.socio_id    = s.id
    INNER JOIN actividades a ON a.id          = m.actividad_id
    WHERE s.dni          = @Dni
      AND s.eliminado_en IS NULL
      AND m.estado       = 'activa'
      AND m.fecha_vencimiento >= CAST(GETDATE() AS DATE)
    ORDER BY a.nombre;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- SP_CALCULARUPGRADE
-- Devuelve las actividades disponibles para upgrade y el monto
-- a pagar (diferencia entre precio actual y precio nuevo)
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_CalcularUpgrade', 'P') IS NOT NULL
    DROP PROCEDURE sp_CalcularUpgrade;
GO
CREATE PROCEDURE sp_CalcularUpgrade
    @MembresiaId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ActividadActualId BIGINT;
    DECLARE @CategoriaActual   VARCHAR(50);
    DECLARE @DiasSesionesActual TINYINT;
    DECLARE @PrecioActual      DECIMAL(12,2);
    DECLARE @UpgradeRealizado  BIT;

    -- Datos de la membres--a actual
    SELECT
        @ActividadActualId = m.actividad_id,
        @CategoriaActual   = a.categoria,
        @DiasSesionesActual = a.dias_sesiones,
        @PrecioActual      = a.precio,
        @UpgradeRealizado  = m.upgrade_realizado
    FROM membresias m
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.id = @MembresiaId AND m.estado = 'activa';

    IF @ActividadActualId IS NULL
    BEGIN
        RAISERROR('La membresÃ­a no existe o no estÃ¡ activa.', 16, 1);
        RETURN;
    END

    -- Validar que no haya hecho upgrade antes
    IF @UpgradeRealizado = 1
    BEGIN
        RAISERROR('Esta membresÃ­a ya tuvo un upgrade. Solo se permite un upgrade por membresÃ­a.', 16, 1);
        RETURN;
    END

    -- Devolver actividades disponibles con la diferencia de precio
    SELECT
        a.id                          AS actividad_id,
        a.nombre                      AS actividad_nombre,
        a.precio                      AS precio_nuevo,
        @PrecioActual                 AS precio_actual,
        a.precio - @PrecioActual      AS diferencia_a_pagar,
        a.dias_sesiones               AS nivel_nuevo,
        @DiasSesionesActual           AS nivel_actual
    FROM actividades a
    WHERE a.categoria = @CategoriaActual
      AND a.dias_sesiones > @DiasSesionesActual
      AND a.activo    = 1
      AND a.id       <> @ActividadActualId
    ORDER BY a.dias_sesiones;
END;
GO

-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
-- SP_EJECUTARUPGRADE
-- Aplica el upgrade: cambia la actividad, registra en caja
-- e historial y marca upgrade_realizado = 1
-- ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
IF OBJECT_ID('sp_EjecutarUpgrade', 'P') IS NOT NULL
    DROP PROCEDURE sp_EjecutarUpgrade;
GO
CREATE PROCEDURE sp_EjecutarUpgrade
    @MembresiaId      BIGINT,
    @NuevaActividadId BIGINT,
    @MetodoPago       VARCHAR(20) = 'efectivo',
    @RegistradoPor    BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SocioId           BIGINT;
    DECLARE @ActividadActualId BIGINT;
    DECLARE @CategoriaActual   VARCHAR(50);
    DECLARE @DiasSesionesActual TINYINT;
    DECLARE @PrecioActual      DECIMAL(12,2);
    DECLARE @CategoriaNueva    VARCHAR(50);
    DECLARE @DiasSesionesNuevo TINYINT;
    DECLARE @PrecioNuevo       DECIMAL(12,2);
    DECLARE @Diferencia        DECIMAL(12,2);
    DECLARE @UpgradeRealizado  BIT;
    DECLARE @FechaInicio       DATE;
    DECLARE @FechaVenc         DATE;

    -- Datos de la membres--a actual
    SELECT
        @SocioId           = m.socio_id,
        @ActividadActualId = m.actividad_id,
        @CategoriaActual   = a.categoria,
        @DiasSesionesActual = a.dias_sesiones,
        @PrecioActual      = a.precio,
        @UpgradeRealizado  = m.upgrade_realizado,
        @FechaInicio       = m.fecha_inicio,
        @FechaVenc         = m.fecha_vencimiento
    FROM membresias m
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.id = @MembresiaId AND m.estado = 'activa';

    IF @SocioId IS NULL
    BEGIN
        RAISERROR('La membresÃ­a no existe o no estÃ¡ activa.', 16, 1);
        RETURN;
    END

    IF @UpgradeRealizado = 1
    BEGIN
        RAISERROR('Esta membresÃ­a ya tuvo un upgrade. Solo se permite un upgrade por membresÃ­a.', 16, 1);
        RETURN;
    END

    -- Datos de la nueva actividad
    SELECT
        @CategoriaNueva = categoria,
        @DiasSesionesNuevo = dias_sesiones,
        @PrecioNuevo    = precio
    FROM actividades
    WHERE id = @NuevaActividadId AND activo = 1;

    IF @CategoriaNueva IS NULL
    BEGIN
        RAISERROR('La nueva actividad no existe o estÃ¡ inactiva.', 16, 1);
        RETURN;
    END

    -- Validar misma categor--a
    IF @CategoriaActual <> @CategoriaNueva
    BEGIN
        RAISERROR('Solo se puede hacer upgrade dentro de la misma categorÃ­a.', 16, 1);
        RETURN;
    END

    -- Validar que sea upgrade (dias_sesiones mayor)
    IF @DiasSesionesNuevo <= @DiasSesionesActual
    BEGIN
        RAISERROR('Solo se permite upgrade a una actividad de plan superior.', 16, 1);
        RETURN;
    END

    SET @Diferencia = @PrecioNuevo - @PrecioActual;

    -- Aplicar el upgrade
    UPDATE membresias SET
        actividad_id      = @NuevaActividadId,
        actividad_original = @ActividadActualId,  -- guardar la original
        upgrade_realizado  = 1,
        monto_pagado       = monto_pagado + @Diferencia,
        actualizado_en     = GETDATE(),
        observaciones      = ISNULL(observaciones + ' | ', '') +
                             'Upgrade realizado el ' + CONVERT(VARCHAR, GETDATE(), 103) +
                             '. Diferencia cobrada: $' + CAST(@Diferencia AS VARCHAR(20))
    WHERE id = @MembresiaId;

    -- Registrar en caja
    INSERT INTO caja_movimientos
        (tipo, subtipo, usuario_id, socio_id, membresia_id, actividad_id,
         detalle, metodo_pago, monto)
    SELECT
        'ingreso_cuota',
        'Upgrade de membresÃ­a',
        @RegistradoPor,
        @SocioId,
        @MembresiaId,
        @NuevaActividadId,
        'Upgrade a ' + a.nombre + ' (' + s.nombre + ' ' + s.apellido + ')',
        @MetodoPago,
        @Diferencia
    FROM socios s, actividades a
    WHERE s.id = @SocioId AND a.id = @NuevaActividadId;

    -- Historial
    INSERT INTO membresia_historial
        (membresia_id, tipo_evento, fecha_desde, fecha_hasta,
         importe, metodo_pago, registrado_por)
    VALUES
        (@MembresiaId, 'modificacion', @FechaInicio, @FechaVenc,
         @Diferencia, @MetodoPago, @RegistradoPor);

    SELECT
        @MembresiaId  AS membresia_id,
        @Diferencia   AS monto_cobrado,
        'Upgrade realizado correctamente.' AS mensaje;
END;
GO
