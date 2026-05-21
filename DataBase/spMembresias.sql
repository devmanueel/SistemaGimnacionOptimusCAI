-- ============================================================
--  STORED PROCEDURES — TABLA membresias
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
--  v1.2 — Reglas de negocio actualizadas:
--    · Pago único e irrevocable (no se puede bajar el plan)
--    · Fechas solo avanzan (nunca retroceden)
--    · Sin congelamiento (estado 'suspendida' eliminado)
--    · Historial de cada alta / modificación / anulación
--    · tipo_plan: 'mensual' | 'semanal' | 'clase'
--    · Cambio de plan: solo misma categoría y solo upgrade
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- 0. ESTRUCTURA — columnas y tabla historial (idempotentes)
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('membresias') AND name = 'tipo_plan'
)
    ALTER TABLE membresias ADD tipo_plan VARCHAR(20) NOT NULL DEFAULT 'mensual';
GO

-- Agregar columnas categoria y nivel a actividades (para regla de cambio de plan)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('actividades') AND name = 'categoria'
)
    ALTER TABLE actividades ADD categoria VARCHAR(50) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('actividades') AND name = 'nivel'
)
    ALTER TABLE actividades ADD nivel TINYINT NULL;
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

-- ─────────────────────────────────────────────────────────────
-- 1. ACTUALIZAR ESTADOS AUTOMÁTICAMENTE
-- ─────────────────────────────────────────────────────────────
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

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER TODAS
-- ─────────────────────────────────────────────────────────────
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
        a.nivel                     AS actividad_nivel,
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

-- ─────────────────────────────────────────────────────────────
-- 3. OBTENER POR ID
-- ─────────────────────────────────────────────────────────────
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
        a.nivel                     AS actividad_nivel,
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

-- ─────────────────────────────────────────────────────────────
-- 4. BUSCAR
-- ─────────────────────────────────────────────────────────────
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
        a.nivel                     AS actividad_nivel,
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

-- ─────────────────────────────────────────────────────────────
-- 5. INSERTAR — cobrar cuota nueva
--    Reglas:
--      · Cancela la membresía activa anterior del mismo socio+actividad
--      · Registra el cobro en caja automáticamente
--      · Guarda en historial
--      · fechas automáticas: inicio = hoy, vencimiento = hoy + 31 días
-- ─────────────────────────────────────────────────────────────
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

    -- Fechas automáticas (regla de negocio: siempre 31 días)
    SET @FechaInicio = CAST(GETDATE() AS DATE);
    SET @FechaVencimiento = DATEADD(DAY, 31, @FechaInicio);

    IF NOT EXISTS (SELECT 1 FROM socios WHERE id = @SocioId AND eliminado_en IS NULL)
    BEGIN
        RAISERROR('El socio no existe o fue eliminado.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM actividades WHERE id = @ActividadId AND activo = 1)
    BEGIN
        RAISERROR('La actividad no existe o está inactiva.', 16, 1);
        RETURN;
    END

    -- Validar 1: No permitir si ya tiene membresía activa con la MISMA ACTIVIDAD
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
        RAISERROR('El socio ya tiene una membresía activa con la actividad "%s". No se puede crear una nueva membresía con la misma actividad.', 16, 1, @ActividadNombre);
        RETURN;
    END

    -- Validar 2: No permitir si ya tiene membresía activa en la misma CATEGORÍA (diferente actividad)
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
        RAISERROR('El socio ya tiene una membresía activa en la categoría "%s". No se puede crear otra membresía en la misma categoría.', 16, 1, @CategoriaExistente);
        RETURN;
    END

    -- Cancelar membresías activas anteriores del mismo socio + actividad
    UPDATE membresias
    SET estado = 'cancelada',
        actualizado_en = GETDATE(),
        observaciones = ISNULL(observaciones + ' | ', '') + 'Reemplazada por nueva membresía'
    WHERE socio_id = @SocioId
      AND actividad_id = @ActividadId
      AND estado IN ('activa', 'vencida');

    INSERT INTO membresias
        (socio_id, actividad_id, instructor_id, fecha_inicio, fecha_vencimiento,
         tipo_plan, monto_pagado, metodo_pago, estado, registrado_por, observaciones)
    VALUES
        (@SocioId, @ActividadId, @InstructorId, @FechaInicio, @FechaVencimiento,
         @TipoPlan, @MontoPagado, @MetodoPago, 'activa', @RegistradoPor, @Observaciones);

    DECLARE @NuevaId BIGINT = SCOPE_IDENTITY();

    -- Registrar el ingreso en caja automáticamente
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

    SELECT @NuevaId AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5.5 OBTENER CATEGORÍA DE MEMBRESÍA ACTIVA POR SOCIO
-- ─────────────────────────────────────────────────────────────
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

-- ─────────────────────────────────────────────────────────────
-- 6. MODIFICAR — solo instructor, observaciones y fecha (solo adelante)
--    Regla: la fecha_vencimiento solo puede avanzar, nunca retroceder.
--    Regla de cambio de plan: solo misma categoría y solo upgrade (nivel mayor)
-- ─────────────────────────────────────────────────────────────
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
        RAISERROR('La membresía no existe.', 16, 1);
        RETURN;
    END

    -- Fechas solo avanzan: no se puede retroceder el vencimiento
    IF @FechaVencimiento < @VencActual
    BEGIN
        RAISERROR('La fecha de vencimiento no puede ser anterior a la actual. Los días solo pueden aumentar.', 16, 1);
        RETURN;
    END

    -- Validación de cambio de plan (solo si se cambia la actividad)
    IF @ActividadId IS NOT NULL AND @ActividadId <> @ActividadActualId
    BEGIN
        DECLARE @CategoriaActual VARCHAR(50);
        DECLARE @NivelActual TINYINT;
        DECLARE @CategoriaNueva VARCHAR(50);
        DECLARE @NivelNuevo TINYINT;

        -- Obtener categoría y nivel actual
        SELECT 
            @CategoriaActual = a.categoria,
            @NivelActual = a.nivel
        FROM actividades a
        INNER JOIN membresias m ON m.actividad_id = a.id
        WHERE m.id = @Id;

        -- Obtener categoría y nivel nuevo
        SELECT 
            @CategoriaNueva = categoria,
            @NivelNuevo = nivel
        FROM actividades
        WHERE id = @ActividadId;

        -- Validar misma categoría
        IF @CategoriaActual IS NOT NULL AND @CategoriaNueva IS NOT NULL 
           AND @CategoriaActual <> @CategoriaNueva
        BEGIN
            RAISERROR('No se puede cambiar a otra categoría. El cambio de plan solo está permitido dentro de la misma categoría.', 16, 1);
            RETURN;
        END

        -- Validar solo upgrade (nivel mayor)
        IF @NivelActual IS NOT NULL AND @NivelNuevo IS NOT NULL 
           AND @NivelNuevo <= @NivelActual
        BEGIN
            RAISERROR('Solo se permite cambiar a un plan superior (upgrade). El downgrade no está permitido.', 16, 1);
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

    -- Historial (solo si la fecha avanzó)
    IF @FechaVencimiento > @VencActual
        INSERT INTO membresia_historial
            (membresia_id, tipo_evento, fecha_desde, fecha_hasta, registrado_por)
        VALUES
            (@Id, 'modificacion', @FechaInicio, @FechaVencimiento, @RegistradoPor);

    SELECT @FilasAfectadas AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 7. CAMBIAR ESTADO — solo activa / vencida / cancelada
--    Sin 'suspendida': el sistema no permite congelar membresías.
-- ─────────────────────────────────────────────────────────────
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
        RAISERROR('Estado inválido. Los estados permitidos son: activa, vencida, cancelada.', 16, 1);
        RETURN;
    END

    UPDATE membresias
    SET estado = @Estado,
        actualizado_en = GETDATE()
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 8. RENOVAR — suma días al vencimiento + cobra en caja
--    Si ya venció, suma desde hoy. Si está vigente, suma desde vencimiento.
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_RenovarMembresia', 'P') IS NOT NULL
    DROP PROCEDURE sp_RenovarMembresia;
GO
CREATE PROCEDURE sp_RenovarMembresia
    @Id            BIGINT,
    @MontoPagado   DECIMAL(12,2),
    @MetodoPago    VARCHAR(20)  = 'efectivo',
    @RegistradoPor BIGINT,
    @DiasASumar    INT          = 31
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SocioId     BIGINT;
    DECLARE @ActividadId BIGINT;
    DECLARE @VencActual  DATE;
    DECLARE @NuevoVenc   DATE;
    DECLARE @TipoPlan    VARCHAR(20);

    SELECT @SocioId = socio_id, @ActividadId = actividad_id,
           @VencActual = fecha_vencimiento, @TipoPlan = tipo_plan
    FROM membresias WHERE id = @Id;

    IF @SocioId IS NULL
    BEGIN
        RAISERROR('La membresía no existe.', 16, 1);
        RETURN;
    END

    IF @VencActual < CAST(GETDATE() AS DATE)
        SET @NuevoVenc = DATEADD(DAY, @DiasASumar, CAST(GETDATE() AS DATE));
    ELSE
        SET @NuevoVenc = DATEADD(DAY, @DiasASumar, @VencActual);

    UPDATE membresias
    SET fecha_vencimiento = @NuevoVenc,
        estado            = 'activa',
        actualizado_en    = GETDATE()
    WHERE id = @Id;

    INSERT INTO caja_movimientos
        (tipo, subtipo, usuario_id, socio_id, membresia_id, actividad_id,
         detalle, metodo_pago, monto)
    SELECT
        'ingreso_cuota', 'Renovación de cuota', @RegistradoPor, @SocioId,
        @Id, @ActividadId,
        'Renovación de ' + a.nombre + ' (' + s.nombre + ' ' + s.apellido + ')',
        @MetodoPago, @MontoPagado
    FROM socios s, actividades a
    WHERE s.id = @SocioId AND a.id = @ActividadId;

    INSERT INTO membresia_historial
        (membresia_id, tipo_evento, fecha_desde, fecha_hasta, importe, metodo_pago, registrado_por)
    VALUES
        (@Id, 'renovacion', @VencActual, @NuevoVenc, @MontoPagado, @MetodoPago, @RegistradoPor);

    SELECT @NuevoVenc AS nueva_fecha_vencimiento;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 9. ANULAR (soft cancel — no borra histórico)
--    Solo el rol admin puede llegar a ejecutar esto (validado en UI/Controller).
-- ─────────────────────────────────────────────────────────────
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

-- ─────────────────────────────────────────────────────────────
-- 10. LISTAR SOCIOS PARA COMBOBOX
-- ─────────────────────────────────────────────────────────────
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

-- ─────────────────────────────────────────────────────────────
-- 11. LISTAR ACTIVIDADES PARA COMBOBOX
-- ─────────────────────────────────────────────────────────────
IF OBJECT_ID('sp_ListarActividadesParaCombo', 'P') IS NOT NULL
    DROP PROCEDURE sp_ListarActividadesParaCombo;
GO
CREATE PROCEDURE sp_ListarActividadesParaCombo
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, nombre, tipo, dias_sesiones, precio, categoria, nivel
    FROM actividades
    WHERE activo = 1
    ORDER BY nombre;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 12. LISTAR INSTRUCTORES PARA COMBOBOX
-- ─────────────────────────────────────────────────────────────
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
