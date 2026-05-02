-- ============================================================
--  STORED PROCEDURES — TABLA membresias
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- 0. ACTUALIZAR ESTADOS AUTOMÁTICAMENTE
--    Marca como 'vencida' toda membresía 'activa' cuyo
--    vencimiento ya pasó. Se llama al cargar la pantalla.
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ActualizarEstadosMembresias
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
-- 1. OBTENER TODAS (con datos joineados)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerMembresias
AS
BEGIN
    SET NOCOUNT ON;
    -- Antes de listar, refrescar estados vencidos
    UPDATE membresias
    SET estado = 'vencida'
    WHERE estado = 'activa' AND fecha_vencimiento < CAST(GETDATE() AS DATE);

    SELECT
        m.id, m.socio_id, m.actividad_id, m.instructor_id,
        m.fecha_inicio, m.fecha_vencimiento,
        m.monto_pagado, m.metodo_pago, m.estado,
        m.registrado_por, m.observaciones,
        m.creado_en, m.actualizado_en,
        s.numero_socio,
        s.nombre + ' ' + s.apellido AS socio_nombre,
        s.dni                       AS socio_dni,
        s.foto                      AS socio_foto,
        a.nombre                    AS actividad_nombre,
        a.tipo                      AS actividad_tipo,
        ISNULL(i.nombre + ' ' + i.apellido, 'Sin asignar') AS instructor_nombre,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')     AS registrado_por_nombre,
        DATEDIFF(DAY, CAST(GETDATE() AS DATE), m.fecha_vencimiento) AS dias_para_vencer
    FROM membresias m
    INNER JOIN socios     s ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    LEFT  JOIN usuarios   i ON i.id = m.instructor_id
    LEFT  JOIN usuarios   u ON u.id = m.registrado_por
    ORDER BY m.creado_en DESC;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OBTENER POR ID
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ObtenerMembresiaPorId
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.id, m.socio_id, m.actividad_id, m.instructor_id,
        m.fecha_inicio, m.fecha_vencimiento,
        m.monto_pagado, m.metodo_pago, m.estado,
        m.registrado_por, m.observaciones,
        m.creado_en, m.actualizado_en,
        s.numero_socio,
        s.nombre + ' ' + s.apellido AS socio_nombre,
        s.dni                       AS socio_dni,
        s.foto                      AS socio_foto,
        a.nombre                    AS actividad_nombre,
        a.tipo                      AS actividad_tipo,
        ISNULL(i.nombre + ' ' + i.apellido, 'Sin asignar') AS instructor_nombre,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')     AS registrado_por_nombre,
        DATEDIFF(DAY, CAST(GETDATE() AS DATE), m.fecha_vencimiento) AS dias_para_vencer
    FROM membresias m
    INNER JOIN socios     s ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    LEFT  JOIN usuarios   i ON i.id = m.instructor_id
    LEFT  JOIN usuarios   u ON u.id = m.registrado_por
    WHERE m.id = @Id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. BUSCAR (texto + filtro estado)
--   FiltroEstado: 'todos' / 'activa' / 'vencida' / 'cancelada' /
--                 'suspendida' / 'por_vencer' (vence en <= 7 días)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_BuscarMembresias
    @Texto         NVARCHAR(100) = '',
    @FiltroEstado  VARCHAR(20)   = 'todos'
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE membresias
    SET estado = 'vencida'
    WHERE estado = 'activa' AND fecha_vencimiento < CAST(GETDATE() AS DATE);

    SELECT
        m.id, m.socio_id, m.actividad_id, m.instructor_id,
        m.fecha_inicio, m.fecha_vencimiento,
        m.monto_pagado, m.metodo_pago, m.estado,
        m.registrado_por, m.observaciones,
        m.creado_en, m.actualizado_en,
        s.numero_socio,
        s.nombre + ' ' + s.apellido AS socio_nombre,
        s.dni                       AS socio_dni,
        s.foto                      AS socio_foto,
        a.nombre                    AS actividad_nombre,
        a.tipo                      AS actividad_tipo,
        ISNULL(i.nombre + ' ' + i.apellido, 'Sin asignar') AS instructor_nombre,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')     AS registrado_por_nombre,
        DATEDIFF(DAY, CAST(GETDATE() AS DATE), m.fecha_vencimiento) AS dias_para_vencer
    FROM membresias m
    INNER JOIN socios     s ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    LEFT  JOIN usuarios   i ON i.id = m.instructor_id
    LEFT  JOIN usuarios   u ON u.id = m.registrado_por
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
-- 4. INSERTAR MEMBRESÍA  (= cobrar cuota nueva)
--    Si el socio ya tiene una membresía activa de esa actividad,
--    se cancela la anterior antes de insertar la nueva.
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_InsertarMembresia
    @SocioId          BIGINT,
    @ActividadId      BIGINT,
    @InstructorId     BIGINT          = NULL,
    @FechaInicio      DATE,
    @FechaVencimiento DATE,
    @MontoPagado      DECIMAL(12,2),
    @MetodoPago       VARCHAR(20)     = 'efectivo',
    @RegistradoPor    BIGINT,
    @Observaciones    VARCHAR(500)    = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Validaciones básicas
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

    IF @FechaVencimiento <= @FechaInicio
    BEGIN
        RAISERROR('La fecha de vencimiento debe ser posterior a la de inicio.', 16, 1);
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

    -- Insertar la nueva
    INSERT INTO membresias
        (socio_id, actividad_id, instructor_id, fecha_inicio, fecha_vencimiento,
         monto_pagado, metodo_pago, estado, registrado_por, observaciones)
    VALUES
        (@SocioId, @ActividadId, @InstructorId, @FechaInicio, @FechaVencimiento,
         @MontoPagado, @MetodoPago, 'activa', @RegistradoPor, @Observaciones);

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

    SELECT @NuevaId AS id;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. MODIFICAR (datos secundarios — no monto ni socio)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ModificarMembresia
    @Id               BIGINT,
    @InstructorId     BIGINT          = NULL,
    @FechaVencimiento DATE,
    @Observaciones    VARCHAR(500)    = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE membresias
    SET instructor_id     = @InstructorId,
        fecha_vencimiento = @FechaVencimiento,
        observaciones     = @Observaciones,
        -- Si se extiende la fecha y estaba vencida, vuelve a activa
        estado = CASE
            WHEN estado = 'vencida' AND @FechaVencimiento >= CAST(GETDATE() AS DATE)
                THEN 'activa'
            ELSE estado
        END,
        actualizado_en = GETDATE()
    WHERE id = @Id;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 6. CAMBIAR ESTADO (cancelar / suspender / reactivar)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_CambiarEstadoMembresia
    @Id     BIGINT,
    @Estado VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Estado NOT IN ('activa', 'vencida', 'cancelada', 'suspendida')
    BEGIN
        RAISERROR('Estado inválido.', 16, 1);
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
-- 7. RENOVAR MEMBRESÍA (suma 30 días al vencimiento + cobra)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_RenovarMembresia
    @Id            BIGINT,
    @MontoPagado   DECIMAL(12,2),
    @MetodoPago    VARCHAR(20)     = 'efectivo',
    @RegistradoPor BIGINT,
    @DiasASumar    INT             = 30
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SocioId      BIGINT;
    DECLARE @ActividadId  BIGINT;
    DECLARE @VencActual   DATE;
    DECLARE @NuevoVenc    DATE;

    SELECT @SocioId = socio_id, @ActividadId = actividad_id, @VencActual = fecha_vencimiento
    FROM membresias WHERE id = @Id;

    IF @SocioId IS NULL
    BEGIN
        RAISERROR('La membresía no existe.', 16, 1);
        RETURN;
    END

    -- Si ya venció, suma desde hoy. Si está vigente, suma desde el vencimiento actual.
    IF @VencActual < CAST(GETDATE() AS DATE)
        SET @NuevoVenc = DATEADD(DAY, @DiasASumar, CAST(GETDATE() AS DATE));
    ELSE
        SET @NuevoVenc = DATEADD(DAY, @DiasASumar, @VencActual);

    UPDATE membresias
    SET fecha_vencimiento = @NuevoVenc,
        estado            = 'activa',
        actualizado_en    = GETDATE()
    WHERE id = @Id;

    -- Registrar en caja
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

    SELECT @NuevoVenc AS nueva_fecha_vencimiento;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 8. ELIMINAR (solo cancela — no se borra histórico)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_EliminarMembresia
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE membresias
    SET estado = 'cancelada',
        actualizado_en = GETDATE()
    WHERE id = @Id;
    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 9. LISTAR SOCIOS PARA COMBOBOX (solo activos, ligero)
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ListarSociosParaCombo
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
-- 10. LISTAR ACTIVIDADES PARA COMBOBOX
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ListarActividadesParaCombo
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id, nombre, tipo, dias_sesiones, precio
    FROM actividades
    WHERE activo = 1
    ORDER BY nombre;
END;
GO

-- ─────────────────────────────────────────────────────────────
-- 11. LISTAR INSTRUCTORES PARA COMBOBOX
-- ─────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_ListarInstructoresParaCombo
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

-- Verificación
EXEC sp_ObtenerMembresias;
GO
