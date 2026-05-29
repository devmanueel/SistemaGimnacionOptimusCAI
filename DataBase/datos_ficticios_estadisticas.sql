-- ============================================================
--  DATOS FICTICIOS PARA PROBAR MODULO ESTADISTICAS
--  Sistema Gimnasio OptimusCAI
--
--  EJECUTAR UNA SOLA VEZ. Inserta datos en:
--    - socios (10 socios ficticios)
--    - membresias (activas para esos socios)
--    - registros_acceso (asistencias variadas ultimo mes)
--    - caja_movimientos (ingresos y gastos ultimo mes)
--    - productos + ventas + ventas_items (para KPI producto top)
--
--  IMPORTANTE: Requiere que exista al menos 1 usuario (id=1)
--  y las actividades del seed original.
--
--  Para LIMPIAR los datos ficticios ejecutar:
--    DELETE FROM ventas_items WHERE venta_id IN (SELECT id FROM ventas WHERE observaciones LIKE '%FICTICIO%');
--    DELETE FROM ventas WHERE observaciones LIKE '%FICTICIO%';
--    DELETE FROM caja_movimientos WHERE detalle LIKE '%FICTICIO%';
--    DELETE FROM registros_acceso WHERE socio_id IN (SELECT id FROM socios WHERE dni LIKE '4000000%');
--    DELETE FROM membresias WHERE socio_id IN (SELECT id FROM socios WHERE dni LIKE '4000000%');
--    DELETE FROM socios WHERE dni LIKE '4000000%';
--    DELETE FROM productos WHERE nombre IN ('Proteina Whey 1kg','Guantes Boxeo 12oz','Shaker 700ml','Toalla Gym');
-- ============================================================

SET NOCOUNT ON;

DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);
DECLARE @UsuarioId BIGINT = 1;

-- Calcular numero_socio base alto para no pisar datos reales
DECLARE @NumBase INT;
SELECT @NumBase = ISNULL(MAX(numero_socio), 0) FROM socios;
IF @NumBase < 90000 SET @NumBase = 90000;
SET @NumBase = @NumBase + 1;

-- ─────────────────────────────────────────────────────────────
-- 1. SOCIOS FICTICIOS (con numero_socio explicito)
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM socios WHERE dni = '40000001')
BEGIN
    INSERT INTO socios (numero_socio, nombre, apellido, dni, dni_pin, activo, creado_en) VALUES
        (@NumBase + 0,  'Carlos',    'Gonzalez',   '40000001', '0000000000000000000000000000000000000000000000000000000000000001', 1, DATEADD(DAY, -45, @Hoy)),
        (@NumBase + 1,  'Maria',     'Lopez',      '40000002', '0000000000000000000000000000000000000000000000000000000000000002', 1, DATEADD(DAY, -40, @Hoy)),
        (@NumBase + 2,  'Juan',      'Martinez',   '40000003', '0000000000000000000000000000000000000000000000000000000000000003', 1, DATEADD(DAY, -35, @Hoy)),
        (@NumBase + 3,  'Laura',     'Fernandez',  '40000004', '0000000000000000000000000000000000000000000000000000000000000004', 1, DATEADD(DAY, -30, @Hoy)),
        (@NumBase + 4,  'Pedro',     'Rodriguez',  '40000005', '0000000000000000000000000000000000000000000000000000000000000005', 1, DATEADD(DAY, -25, @Hoy)),
        (@NumBase + 5,  'Ana',       'Garcia',     '40000006', '0000000000000000000000000000000000000000000000000000000000000006', 1, DATEADD(DAY, -20, @Hoy)),
        (@NumBase + 6,  'Diego',     'Ruiz',       '40000007', '0000000000000000000000000000000000000000000000000000000000000007', 1, DATEADD(DAY, -15, @Hoy)),
        (@NumBase + 7,  'Sofia',     'Diaz',       '40000008', '0000000000000000000000000000000000000000000000000000000000000008', 1, DATEADD(DAY, -10, @Hoy)),
        (@NumBase + 8,  'Martin',    'Alvarez',    '40000009', '0000000000000000000000000000000000000000000000000000000000000009', 1, DATEADD(DAY, -5, @Hoy)),
        (@NumBase + 9,  'Valentina', 'Torres',     '40000010', '0000000000000000000000000000000000000000000000000000000000000010', 1, DATEADD(DAY, -2, @Hoy));
END;

-- Obtener IDs de los socios ficticios
DECLARE @S1 BIGINT, @S2 BIGINT, @S3 BIGINT, @S4 BIGINT, @S5 BIGINT,
        @S6 BIGINT, @S7 BIGINT, @S8 BIGINT, @S9 BIGINT, @S10 BIGINT;

SELECT @S1  = id FROM socios WHERE dni = '40000001';
SELECT @S2  = id FROM socios WHERE dni = '40000002';
SELECT @S3  = id FROM socios WHERE dni = '40000003';
SELECT @S4  = id FROM socios WHERE dni = '40000004';
SELECT @S5  = id FROM socios WHERE dni = '40000005';
SELECT @S6  = id FROM socios WHERE dni = '40000006';
SELECT @S7  = id FROM socios WHERE dni = '40000007';
SELECT @S8  = id FROM socios WHERE dni = '40000008';
SELECT @S9  = id FROM socios WHERE dni = '40000009';
SELECT @S10 = id FROM socios WHERE dni = '40000010';

-- Abortar si los socios no se crearon
IF @S1 IS NULL OR @S2 IS NULL OR @S3 IS NULL
BEGIN
    PRINT 'ERROR: No se pudieron crear/encontrar los socios ficticios. Abortando.';
    RETURN;
END;

-- Obtener IDs de actividades existentes
DECLARE @ActGim3 BIGINT, @ActBoxeo BIGINT, @ActDeport BIGINT;
SELECT TOP 1 @ActGim3   = id FROM actividades WHERE nombre LIKE 'Gimnasio 3%' AND activo = 1;
SELECT TOP 1 @ActBoxeo  = id FROM actividades WHERE nombre LIKE 'Boxeo Cai 3%' AND activo = 1;
SELECT TOP 1 @ActDeport = id FROM actividades WHERE nombre LIKE 'Deportistas Cai' AND nombre NOT LIKE '%3%' AND nombre NOT LIKE '%Todos%' AND activo = 1;

-- Fallback
IF @ActGim3   IS NULL SELECT TOP 1 @ActGim3   = id FROM actividades WHERE activo = 1;
IF @ActBoxeo  IS NULL SELECT TOP 1 @ActBoxeo  = id FROM actividades WHERE activo = 1 AND id <> @ActGim3;
IF @ActDeport IS NULL SELECT TOP 1 @ActDeport = id FROM actividades WHERE activo = 1 AND id <> @ActGim3 AND id <> @ActBoxeo;

IF @ActGim3 IS NULL
BEGIN
    PRINT 'ERROR: No se encontraron actividades activas. Abortando.';
    RETURN;
END;

-- ─────────────────────────────────────────────────────────────
-- 2. MEMBRESIAS ACTIVAS
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM membresias WHERE socio_id = @S1 AND estado = 'activa')
BEGIN
    INSERT INTO membresias (socio_id, actividad_id, fecha_inicio, fecha_vencimiento, monto_pagado, metodo_pago, estado, registrado_por) VALUES
        (@S1,  @ActGim3,   DATEADD(DAY, -30, @Hoy), DATEADD(DAY, 5, @Hoy),  25000, 'efectivo',      'activa', @UsuarioId),
        (@S2,  @ActBoxeo,  DATEADD(DAY, -25, @Hoy), DATEADD(DAY, 10, @Hoy), 22000, 'transferencia', 'activa', @UsuarioId),
        (@S3,  @ActGim3,   DATEADD(DAY, -20, @Hoy), DATEADD(DAY, 15, @Hoy), 25000, 'efectivo',      'activa', @UsuarioId),
        (@S4,  @ActDeport, DATEADD(DAY, -28, @Hoy), DATEADD(DAY, 3, @Hoy),  14000, 'tarjeta',       'activa', @UsuarioId),
        (@S5,  @ActGim3,   DATEADD(DAY, -15, @Hoy), DATEADD(DAY, 20, @Hoy), 25000, 'efectivo',      'activa', @UsuarioId),
        (@S6,  @ActBoxeo,  DATEADD(DAY, -10, @Hoy), DATEADD(DAY, 25, @Hoy), 22000, 'transferencia', 'activa', @UsuarioId),
        (@S7,  @ActDeport, DATEADD(DAY, -12, @Hoy), DATEADD(DAY, 22, @Hoy), 14000, 'efectivo',      'activa', @UsuarioId),
        (@S8,  @ActGim3,   DATEADD(DAY, -8, @Hoy),  DATEADD(DAY, 27, @Hoy), 25000, 'tarjeta',       'activa', @UsuarioId),
        (@S9,  @ActBoxeo,  DATEADD(DAY, -5, @Hoy),  DATEADD(DAY, 28, @Hoy), 22000, 'efectivo',      'activa', @UsuarioId),
        (@S10, @ActGim3,   DATEADD(DAY, -2, @Hoy),  DATEADD(DAY, 30, @Hoy), 25000, 'efectivo',      'activa', @UsuarioId);

    -- Membresias que vencen esta semana (para KPI)
    INSERT INTO membresias (socio_id, actividad_id, fecha_inicio, fecha_vencimiento, monto_pagado, metodo_pago, estado, registrado_por) VALUES
        (@S1, @ActBoxeo, DATEADD(DAY, -28, @Hoy), DATEADD(DAY, 2, @Hoy), 22000, 'efectivo', 'activa', @UsuarioId),
        (@S4, @ActGim3,  DATEADD(DAY, -28, @Hoy), DATEADD(DAY, 4, @Hoy), 25000, 'efectivo', 'activa', @UsuarioId);
END;

-- Obtener IDs de membresias
DECLARE @M1 BIGINT, @M2 BIGINT, @M3 BIGINT, @M4 BIGINT, @M5 BIGINT,
        @M6 BIGINT, @M7 BIGINT, @M8 BIGINT, @M9 BIGINT, @M10 BIGINT;

SELECT TOP 1 @M1  = id FROM membresias WHERE socio_id = @S1  AND estado = 'activa' ORDER BY id;
SELECT TOP 1 @M2  = id FROM membresias WHERE socio_id = @S2  AND estado = 'activa' ORDER BY id;
SELECT TOP 1 @M3  = id FROM membresias WHERE socio_id = @S3  AND estado = 'activa' ORDER BY id;
SELECT TOP 1 @M4  = id FROM membresias WHERE socio_id = @S4  AND estado = 'activa' ORDER BY id;
SELECT TOP 1 @M5  = id FROM membresias WHERE socio_id = @S5  AND estado = 'activa' ORDER BY id;
SELECT TOP 1 @M6  = id FROM membresias WHERE socio_id = @S6  AND estado = 'activa' ORDER BY id;
SELECT TOP 1 @M7  = id FROM membresias WHERE socio_id = @S7  AND estado = 'activa' ORDER BY id;
SELECT TOP 1 @M8  = id FROM membresias WHERE socio_id = @S8  AND estado = 'activa' ORDER BY id;
SELECT TOP 1 @M9  = id FROM membresias WHERE socio_id = @S9  AND estado = 'activa' ORDER BY id;
SELECT TOP 1 @M10 = id FROM membresias WHERE socio_id = @S10 AND estado = 'activa' ORDER BY id;

-- ─────────────────────────────────────────────────────────────
-- 3. REGISTROS DE ACCESO (asistencias variadas, ultimos 30 dias)
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM registros_acceso WHERE socio_id = @S1 AND accedido_en >= DATEADD(DAY, -30, GETDATE()))
BEGIN
    -- Carlos: 15 asistencias (top asistente) - horario manana
    INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado, accedido_en) VALUES
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 8,  CAST(DATEADD(DAY, -28, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 9,  CAST(DATEADD(DAY, -26, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 8,  CAST(DATEADD(DAY, -24, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 10, CAST(DATEADD(DAY, -22, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 8,  CAST(DATEADD(DAY, -20, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 9,  CAST(DATEADD(DAY, -18, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 8,  CAST(DATEADD(DAY, -16, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 10, CAST(DATEADD(DAY, -14, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 8,  CAST(DATEADD(DAY, -12, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 9,  CAST(DATEADD(DAY, -10, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 8,  CAST(DATEADD(DAY, -8, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 10, CAST(DATEADD(DAY, -6, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 8,  CAST(DATEADD(DAY, -4, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 9,  CAST(DATEADD(DAY, -2, @Hoy) AS DATETIME))),
        (@S1, @M1, 'dni_pin', 'permitido', DATEADD(HOUR, 8,  CAST(DATEADD(DAY, -1, @Hoy) AS DATETIME)));

    -- Maria: 12 asistencias - horario tarde
    INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado, accedido_en) VALUES
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 17, CAST(DATEADD(DAY, -25, @Hoy) AS DATETIME))),
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 18, CAST(DATEADD(DAY, -23, @Hoy) AS DATETIME))),
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 17, CAST(DATEADD(DAY, -21, @Hoy) AS DATETIME))),
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 19, CAST(DATEADD(DAY, -19, @Hoy) AS DATETIME))),
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 17, CAST(DATEADD(DAY, -17, @Hoy) AS DATETIME))),
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 18, CAST(DATEADD(DAY, -15, @Hoy) AS DATETIME))),
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 17, CAST(DATEADD(DAY, -13, @Hoy) AS DATETIME))),
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 19, CAST(DATEADD(DAY, -11, @Hoy) AS DATETIME))),
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 17, CAST(DATEADD(DAY, -9, @Hoy) AS DATETIME))),
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 18, CAST(DATEADD(DAY, -7, @Hoy) AS DATETIME))),
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 17, CAST(DATEADD(DAY, -5, @Hoy) AS DATETIME))),
        (@S2, @M2, 'dni_pin', 'permitido', DATEADD(HOUR, 17, CAST(DATEADD(DAY, -3, @Hoy) AS DATETIME)));

    -- Juan: 10 asistencias - horario temprano
    INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado, accedido_en) VALUES
        (@S3, @M3, 'dni_pin', 'permitido', DATEADD(HOUR, 7,  CAST(DATEADD(DAY, -20, @Hoy) AS DATETIME))),
        (@S3, @M3, 'dni_pin', 'permitido', DATEADD(HOUR, 7,  CAST(DATEADD(DAY, -18, @Hoy) AS DATETIME))),
        (@S3, @M3, 'dni_pin', 'permitido', DATEADD(HOUR, 8,  CAST(DATEADD(DAY, -16, @Hoy) AS DATETIME))),
        (@S3, @M3, 'dni_pin', 'permitido', DATEADD(HOUR, 7,  CAST(DATEADD(DAY, -14, @Hoy) AS DATETIME))),
        (@S3, @M3, 'dni_pin', 'permitido', DATEADD(HOUR, 7,  CAST(DATEADD(DAY, -12, @Hoy) AS DATETIME))),
        (@S3, @M3, 'dni_pin', 'permitido', DATEADD(HOUR, 8,  CAST(DATEADD(DAY, -10, @Hoy) AS DATETIME))),
        (@S3, @M3, 'dni_pin', 'permitido', DATEADD(HOUR, 7,  CAST(DATEADD(DAY, -8, @Hoy) AS DATETIME))),
        (@S3, @M3, 'dni_pin', 'permitido', DATEADD(HOUR, 7,  CAST(DATEADD(DAY, -6, @Hoy) AS DATETIME))),
        (@S3, @M3, 'dni_pin', 'permitido', DATEADD(HOUR, 8,  CAST(DATEADD(DAY, -4, @Hoy) AS DATETIME))),
        (@S3, @M3, 'dni_pin', 'permitido', DATEADD(HOUR, 7,  CAST(DATEADD(DAY, -2, @Hoy) AS DATETIME)));

    -- Laura: 8 asistencias - horario siesta
    INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado, accedido_en) VALUES
        (@S4, @M4, 'dni_pin', 'permitido', DATEADD(HOUR, 16, CAST(DATEADD(DAY, -28, @Hoy) AS DATETIME))),
        (@S4, @M4, 'dni_pin', 'permitido', DATEADD(HOUR, 16, CAST(DATEADD(DAY, -24, @Hoy) AS DATETIME))),
        (@S4, @M4, 'dni_pin', 'permitido', DATEADD(HOUR, 15, CAST(DATEADD(DAY, -20, @Hoy) AS DATETIME))),
        (@S4, @M4, 'dni_pin', 'permitido', DATEADD(HOUR, 16, CAST(DATEADD(DAY, -16, @Hoy) AS DATETIME))),
        (@S4, @M4, 'dni_pin', 'permitido', DATEADD(HOUR, 15, CAST(DATEADD(DAY, -12, @Hoy) AS DATETIME))),
        (@S4, @M4, 'dni_pin', 'permitido', DATEADD(HOUR, 16, CAST(DATEADD(DAY, -8, @Hoy) AS DATETIME))),
        (@S4, @M4, 'dni_pin', 'permitido', DATEADD(HOUR, 15, CAST(DATEADD(DAY, -4, @Hoy) AS DATETIME))),
        (@S4, @M4, 'dni_pin', 'permitido', DATEADD(HOUR, 16, CAST(DATEADD(DAY, -1, @Hoy) AS DATETIME)));

    -- Pedro: 9 asistencias - horario noche
    INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado, accedido_en) VALUES
        (@S5, @M5, 'dni_pin', 'permitido', DATEADD(HOUR, 20, CAST(DATEADD(DAY, -15, @Hoy) AS DATETIME))),
        (@S5, @M5, 'dni_pin', 'permitido', DATEADD(HOUR, 20, CAST(DATEADD(DAY, -14, @Hoy) AS DATETIME))),
        (@S5, @M5, 'dni_pin', 'permitido', DATEADD(HOUR, 21, CAST(DATEADD(DAY, -12, @Hoy) AS DATETIME))),
        (@S5, @M5, 'dni_pin', 'permitido', DATEADD(HOUR, 20, CAST(DATEADD(DAY, -10, @Hoy) AS DATETIME))),
        (@S5, @M5, 'dni_pin', 'permitido', DATEADD(HOUR, 20, CAST(DATEADD(DAY, -8, @Hoy) AS DATETIME))),
        (@S5, @M5, 'dni_pin', 'permitido', DATEADD(HOUR, 21, CAST(DATEADD(DAY, -6, @Hoy) AS DATETIME))),
        (@S5, @M5, 'dni_pin', 'permitido', DATEADD(HOUR, 20, CAST(DATEADD(DAY, -4, @Hoy) AS DATETIME))),
        (@S5, @M5, 'dni_pin', 'permitido', DATEADD(HOUR, 20, CAST(DATEADD(DAY, -2, @Hoy) AS DATETIME))),
        (@S5, @M5, 'dni_pin', 'permitido', DATEADD(HOUR, 21, CAST(DATEADD(DAY, -1, @Hoy) AS DATETIME)));

    -- Ana: 5 asistencias, Diego: 4, Sofia: 3
    INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado, accedido_en) VALUES
        (@S6, @M6, 'dni_pin', 'permitido', DATEADD(HOUR, 18, CAST(DATEADD(DAY, -10, @Hoy) AS DATETIME))),
        (@S6, @M6, 'dni_pin', 'permitido', DATEADD(HOUR, 18, CAST(DATEADD(DAY, -8, @Hoy) AS DATETIME))),
        (@S6, @M6, 'dni_pin', 'permitido', DATEADD(HOUR, 19, CAST(DATEADD(DAY, -6, @Hoy) AS DATETIME))),
        (@S6, @M6, 'dni_pin', 'permitido', DATEADD(HOUR, 18, CAST(DATEADD(DAY, -4, @Hoy) AS DATETIME))),
        (@S6, @M6, 'dni_pin', 'permitido', DATEADD(HOUR, 18, CAST(DATEADD(DAY, -2, @Hoy) AS DATETIME))),

        (@S7, @M7, 'dni_pin', 'permitido', DATEADD(HOUR, 11, CAST(DATEADD(DAY, -12, @Hoy) AS DATETIME))),
        (@S7, @M7, 'dni_pin', 'permitido', DATEADD(HOUR, 11, CAST(DATEADD(DAY, -10, @Hoy) AS DATETIME))),
        (@S7, @M7, 'dni_pin', 'permitido', DATEADD(HOUR, 12, CAST(DATEADD(DAY, -8, @Hoy) AS DATETIME))),
        (@S7, @M7, 'dni_pin', 'permitido', DATEADD(HOUR, 11, CAST(DATEADD(DAY, -6, @Hoy) AS DATETIME))),

        (@S8, @M8, 'dni_pin', 'permitido', DATEADD(HOUR, 14, CAST(DATEADD(DAY, -7, @Hoy) AS DATETIME))),
        (@S8, @M8, 'dni_pin', 'permitido', DATEADD(HOUR, 14, CAST(DATEADD(DAY, -5, @Hoy) AS DATETIME))),
        (@S8, @M8, 'dni_pin', 'permitido', DATEADD(HOUR, 15, CAST(DATEADD(DAY, -3, @Hoy) AS DATETIME)));

    -- Martin y Valentina: pocas asistencias (recien inscriptos)
    INSERT INTO registros_acceso (socio_id, membresia_id, metodo_acceso, resultado, accedido_en) VALUES
        (@S9,  @M9,  'dni_pin', 'permitido', DATEADD(HOUR, 9,  CAST(DATEADD(DAY, -3, @Hoy) AS DATETIME))),
        (@S9,  @M9,  'dni_pin', 'permitido', DATEADD(HOUR, 10, CAST(DATEADD(DAY, -1, @Hoy) AS DATETIME))),
        (@S10, @M10, 'dni_pin', 'permitido', DATEADD(HOUR, 17, CAST(DATEADD(DAY, -1, @Hoy) AS DATETIME)));
END;

-- ─────────────────────────────────────────────────────────────
-- 4. CAJA — INGRESOS Y GASTOS
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM caja_movimientos WHERE detalle LIKE '%FICTICIO%')
BEGIN
    -- Ingresos por cuotas
    INSERT INTO caja_movimientos (tipo, subtipo, usuario_id, socio_id, membresia_id, detalle, metodo_pago, monto, creado_en) VALUES
        ('ingreso_cuota', 'cuota_mensual', @UsuarioId, @S1,  @M1,  'Cuota Gimnasio 3vxs - FICTICIO', 'efectivo',      25000, DATEADD(DAY, -28, @Hoy)),
        ('ingreso_cuota', 'cuota_mensual', @UsuarioId, @S2,  @M2,  'Cuota Boxeo 3vxs - FICTICIO',    'transferencia', 22000, DATEADD(DAY, -25, @Hoy)),
        ('ingreso_cuota', 'cuota_mensual', @UsuarioId, @S3,  @M3,  'Cuota Gimnasio 3vxs - FICTICIO', 'efectivo',      25000, DATEADD(DAY, -20, @Hoy)),
        ('ingreso_cuota', 'cuota_mensual', @UsuarioId, @S4,  @M4,  'Cuota Deportistas - FICTICIO',   'tarjeta',       14000, DATEADD(DAY, -18, @Hoy)),
        ('ingreso_cuota', 'cuota_mensual', @UsuarioId, @S5,  @M5,  'Cuota Gimnasio 3vxs - FICTICIO', 'efectivo',      25000, DATEADD(DAY, -15, @Hoy)),
        ('ingreso_cuota', 'cuota_mensual', @UsuarioId, @S6,  @M6,  'Cuota Boxeo 3vxs - FICTICIO',    'transferencia', 22000, DATEADD(DAY, -10, @Hoy)),
        ('ingreso_cuota', 'cuota_mensual', @UsuarioId, @S7,  @M7,  'Cuota Deportistas - FICTICIO',   'efectivo',      14000, DATEADD(DAY, -12, @Hoy)),
        ('ingreso_cuota', 'cuota_mensual', @UsuarioId, @S8,  @M8,  'Cuota Gimnasio 3vxs - FICTICIO', 'tarjeta',       25000, DATEADD(DAY, -8, @Hoy)),
        ('ingreso_cuota', 'cuota_mensual', @UsuarioId, @S9,  @M9,  'Cuota Boxeo 3vxs - FICTICIO',    'efectivo',      22000, DATEADD(DAY, -5, @Hoy)),
        ('ingreso_cuota', 'cuota_mensual', @UsuarioId, @S10, @M10, 'Cuota Gimnasio 3vxs - FICTICIO', 'efectivo',      25000, DATEADD(DAY, -2, @Hoy));

    -- Ingresos por ventas
    INSERT INTO caja_movimientos (tipo, subtipo, usuario_id, detalle, metodo_pago, monto, creado_en) VALUES
        ('ingreso_venta', 'venta_producto', @UsuarioId, 'Venta proteina - FICTICIO',      'efectivo',      8500,  DATEADD(DAY, -22, @Hoy)),
        ('ingreso_venta', 'venta_producto', @UsuarioId, 'Venta guantes boxeo - FICTICIO', 'tarjeta',       12000, DATEADD(DAY, -16, @Hoy)),
        ('ingreso_venta', 'venta_producto', @UsuarioId, 'Venta proteina - FICTICIO',      'efectivo',      8500,  DATEADD(DAY, -11, @Hoy)),
        ('ingreso_venta', 'venta_producto', @UsuarioId, 'Venta shaker - FICTICIO',        'transferencia', 3500,  DATEADD(DAY, -7, @Hoy)),
        ('ingreso_venta', 'venta_producto', @UsuarioId, 'Venta proteina - FICTICIO',      'efectivo',      8500,  DATEADD(DAY, -3, @Hoy)),
        ('ingreso_venta', 'venta_producto', @UsuarioId, 'Venta toalla gym - FICTICIO',    'efectivo',      2500,  DATEADD(DAY, -1, @Hoy));

    -- Gastos
    INSERT INTO caja_movimientos (tipo, subtipo, usuario_id, detalle, metodo_pago, monto, creado_en) VALUES
        ('gasto', 'servicios',   @UsuarioId, 'Luz del mes - FICTICIO',              'transferencia', 45000, DATEADD(DAY, -27, @Hoy)),
        ('gasto', 'limpieza',    @UsuarioId, 'Productos limpieza - FICTICIO',       'efectivo',      8000,  DATEADD(DAY, -20, @Hoy)),
        ('gasto', 'equipamiento',@UsuarioId, 'Mantenimiento cinta - FICTICIO',      'transferencia', 15000, DATEADD(DAY, -14, @Hoy)),
        ('gasto', 'servicios',   @UsuarioId, 'Internet - FICTICIO',                 'transferencia', 12000, DATEADD(DAY, -10, @Hoy)),
        ('gasto', 'insumos',     @UsuarioId, 'Agua dispenser - FICTICIO',           'efectivo',      5000,  DATEADD(DAY, -6, @Hoy)),
        ('gasto', 'equipamiento',@UsuarioId, 'Mancuernas nuevas 10kg - FICTICIO',   'tarjeta',       35000, DATEADD(DAY, -3, @Hoy)),
        ('gasto', 'limpieza',    @UsuarioId, 'Lavandina y desinfectante - FICTICIO', 'efectivo',      3500,  DATEADD(DAY, -1, @Hoy));
END;

-- ─────────────────────────────────────────────────────────────
-- 5. PRODUCTOS + VENTAS + VENTAS_ITEMS (para KPI producto top)
-- ─────────────────────────────────────────────────────────────
DECLARE @ProdProteina BIGINT, @ProdGuantes BIGINT, @ProdShaker BIGINT;

IF NOT EXISTS (SELECT 1 FROM productos WHERE nombre = 'Proteina Whey 1kg')
BEGIN
    INSERT INTO productos (nombre, descripcion, precio, stock, stock_min) VALUES
        ('Proteina Whey 1kg',  'Proteina sabor vainilla', 8500,  20, 5),
        ('Guantes Boxeo 12oz', 'Guantes entrenamiento',   12000, 10, 2),
        ('Shaker 700ml',       'Vaso mezclador',          3500,  15, 3),
        ('Toalla Gym',         'Toalla microfibra',       2500,  25, 5);
END;

SELECT @ProdProteina = id FROM productos WHERE nombre = 'Proteina Whey 1kg';
SELECT @ProdGuantes  = id FROM productos WHERE nombre = 'Guantes Boxeo 12oz';
SELECT @ProdShaker   = id FROM productos WHERE nombre = 'Shaker 700ml';

IF @ProdProteina IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ventas WHERE observaciones LIKE '%FICTICIO%')
BEGIN
    DECLARE @V1 BIGINT, @V2 BIGINT, @V3 BIGINT, @V4 BIGINT, @V5 BIGINT;

    INSERT INTO ventas (usuario_id, socio_id, total, metodo_pago, observaciones, creado_en)
    VALUES (@UsuarioId, @S1, 8500, 'efectivo', 'Venta test FICTICIO', DATEADD(DAY, -22, @Hoy));
    SET @V1 = SCOPE_IDENTITY();

    INSERT INTO ventas (usuario_id, socio_id, total, metodo_pago, observaciones, creado_en)
    VALUES (@UsuarioId, @S2, 12000, 'tarjeta', 'Venta test FICTICIO', DATEADD(DAY, -16, @Hoy));
    SET @V2 = SCOPE_IDENTITY();

    INSERT INTO ventas (usuario_id, socio_id, total, metodo_pago, observaciones, creado_en)
    VALUES (@UsuarioId, @S5, 17000, 'efectivo', 'Venta test FICTICIO', DATEADD(DAY, -11, @Hoy));
    SET @V3 = SCOPE_IDENTITY();

    INSERT INTO ventas (usuario_id, socio_id, total, metodo_pago, observaciones, creado_en)
    VALUES (@UsuarioId, @S3, 8500, 'efectivo', 'Venta test FICTICIO', DATEADD(DAY, -3, @Hoy));
    SET @V4 = SCOPE_IDENTITY();

    INSERT INTO ventas (usuario_id, total, metodo_pago, observaciones, creado_en)
    VALUES (@UsuarioId, 8500, 'efectivo', 'Venta test FICTICIO', DATEADD(DAY, -1, @Hoy));
    SET @V5 = SCOPE_IDENTITY();

    INSERT INTO ventas_items (venta_id, producto_id, descripcion, cantidad, precio_unitario, subtotal) VALUES
        (@V1, @ProdProteina, 'Proteina Whey 1kg',  1, 8500,  8500),
        (@V2, @ProdGuantes,  'Guantes Boxeo 12oz', 1, 12000, 12000),
        (@V3, @ProdProteina, 'Proteina Whey 1kg',  1, 8500,  8500),
        (@V3, @ProdShaker,   'Shaker 700ml',       1, 3500,  3500),
        (@V4, @ProdProteina, 'Proteina Whey 1kg',  1, 8500,  8500),
        (@V5, @ProdProteina, 'Proteina Whey 1kg',  1, 8500,  8500);
END;

PRINT '========================================';
PRINT 'Datos ficticios insertados correctamente';
PRINT '  - 10 socios (DNI 40000001-40000010)';
PRINT '  - 12 membresias activas';
PRINT '  - ~80 registros de acceso';
PRINT '  - 23 movimientos de caja';
PRINT '  - 4 productos, 5 ventas';
PRINT '========================================';
GO
