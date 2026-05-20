-- ============================================================
--  GIMNASIO MANAGEMENT SYSTEM  v2.0 (VERSIÓN SQL SERVER)
--  Adaptado para Microsoft SQL Server / Azure SQL
--  Ideal para usar con C# .NET (Entity Framework / Dapper)
-- ============================================================

-- ─────────────────────────────────────────────
-- 1. ROLES
-- ─────────────────────────────────────────────
CREATE TABLE roles (
  id          TINYINT IDENTITY(1,1) NOT NULL,
  nombre      VARCHAR(50)           NOT NULL,
  descripcion VARCHAR(255),
  CONSTRAINT pk_roles PRIMARY KEY (id),
  CONSTRAINT uq_roles_nombre UNIQUE (nombre)
);

INSERT INTO roles (nombre, descripcion) VALUES
  ('admin',      'Administrador con acceso total al sistema'),
  ('empleado',   'Instructor / profesor con acceso restringido (solo ve caja del dia)');

-- ─────────────────────────────────────────────
-- 2. USUARIOS DEL SISTEMA
-- ─────────────────────────────────────────────
CREATE TABLE usuarios (
  id               BIGINT IDENTITY(1,1) NOT NULL,
  rol_id           TINYINT          NOT NULL, -- admin o empleado
  nombre           VARCHAR(100)     NOT NULL,
  apellido         VARCHAR(100)     NOT NULL,
  dni              CHAR(8)          NOT NULL,
  domicilio        VARCHAR(200),
  telefono         VARCHAR(20),
  email            VARCHAR(191),
  password_hash    CHAR(64)         NOT NULL, -- SHA-256 de la contrasena
  activo           BIT              NOT NULL DEFAULT 1,
  creado_en        DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  actualizado_en   DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP, -- Actualizar desde C# .NET en el UPDATE
  eliminado_en     DATETIME,                  -- Soft-delete
  CONSTRAINT pk_usuarios PRIMARY KEY (id),
  CONSTRAINT uq_usuarios_dni UNIQUE (dni)
);
CREATE INDEX idx_usuarios_rol ON usuarios (rol_id);
CREATE INDEX idx_usuarios_activo ON usuarios (activo);
CREATE INDEX idx_usuarios_soft_del ON usuarios (eliminado_en);

-- ─────────────────────────────────────────────
-- 3. SOCIOS
-- ─────────────────────────────────────────────
CREATE TABLE socios (
  id                BIGINT IDENTITY(1,1) NOT NULL,
  numero_socio      INT,                      -- Numero visible en ficha
  nombre            VARCHAR(100)     NOT NULL,
  apellido          VARCHAR(100)     NOT NULL,
  dni               CHAR(8)          NOT NULL,
  dni_pin           CHAR(64)         NOT NULL,
  foto_path         VARCHAR(500),
  fecha_nacimiento  DATE,
  sexo              VARCHAR(10)      DEFAULT 'Otro' CHECK (sexo IN ('M','F','Otro')),
  telefono          VARCHAR(20),
  domicilio         VARCHAR(200),
  profesion         VARCHAR(100),
  email             VARCHAR(191),
  como_nos_conocio  VARCHAR(200),
  observaciones     VARCHAR(MAX),             -- Equivale a TEXT
  activo            BIT              NOT NULL DEFAULT 1,
  registrado_por    BIGINT,
  creado_en         DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  actualizado_en    DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  eliminado_en      DATETIME,
  CONSTRAINT pk_socios PRIMARY KEY (id),
  CONSTRAINT uq_socios_dni UNIQUE (dni),
  CONSTRAINT uq_socios_numero UNIQUE (numero_socio)
);
CREATE INDEX idx_socios_registrado_por ON socios (registrado_por);
CREATE INDEX idx_socios_activo ON socios (activo);
CREATE INDEX idx_socios_soft_del ON socios (eliminado_en);

-- ─────────────────────────────────────────────
-- 4. HUELLAS DACTILARES
-- ─────────────────────────────────────────────
CREATE TABLE huellas_dactilares (
  id            BIGINT IDENTITY(1,1) NOT NULL,
  socio_id      BIGINT           NOT NULL,
  dedo          TINYINT          NOT NULL, -- 1=Pulgar_der 2=Indice_der...
  template      VARBINARY(MAX)   NOT NULL, -- Para blobs del SDK (MEDIUMBLOB)
  activo        BIT              NOT NULL DEFAULT 1,
  registrado_en DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT pk_huellas PRIMARY KEY (id)
);
CREATE INDEX idx_huellas_socio ON huellas_dactilares (socio_id);
CREATE INDEX idx_huellas_socio_activo ON huellas_dactilares (socio_id, activo);

-- ─────────────────────────────────────────────
-- 5. ACTIVIDADES
-- ─────────────────────────────────────────────
CREATE TABLE actividades (
  id               BIGINT IDENTITY(1,1) NOT NULL,
  nombre           VARCHAR(150)     NOT NULL,
  tipo             VARCHAR(30)      NOT NULL DEFAULT 'mensual' CHECK (tipo IN ('mensual','mensual_con_clases')),
  dias_sesiones    TINYINT          NOT NULL,
  dias_semana      NVARCHAR(MAX),             -- Para guardar JSON
  precio           DECIMAL(12,2)    NOT NULL,
  activo           BIT              NOT NULL DEFAULT 1,
  creado_en        DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT pk_actividades PRIMARY KEY (id),
  CONSTRAINT uq_actividades_nombre UNIQUE (nombre)
);
CREATE INDEX idx_actividades_activo ON actividades (activo);

INSERT INTO actividades (nombre, tipo, dias_sesiones, dias_semana, precio) VALUES
  ('Boxeo Cai 2 Vxs',              'mensual', 2, '[1,3]',     19000),
  ('Boxeo Cai 3 Vxs',              'mensual', 3, '[1,3,5]',   22000),
  ('Boxeo Todos Los Dias',         'mensual', 6, '[1,2,3,4,5,6]', 25000),
  ('Clase',                        'mensual_con_clases', 1, NULL, 4000),
  ('Deportistas Cai',              'mensual', 2, '[1,3]',     14000),
  ('Deportistas Cai 3 Vxs',       'mensual', 3, '[1,3,5]',   17000),
  ('Deportistas Cai Todos Los Dias','mensual',5, '[1,2,3,4,5]',20000),
  ('Gimnasio 2 Veces Por Semana',  'mensual', 2, '[2,5]',     22000),
  ('Gimnasio 3 Veces Por Semana',  'mensual', 3, '[1,3,5]',   25000),
  ('Gimnasio Todos Los Dias',      'mensual', 5, '[1,2,3,4,5]',28000);

-- ─────────────────────────────────────────────
-- 6. MEMBRESÍAS
-- ─────────────────────────────────────────────
CREATE TABLE membresias (
  id                BIGINT IDENTITY(1,1) NOT NULL,
  socio_id          BIGINT           NOT NULL,
  actividad_id      BIGINT           NOT NULL,
  instructor_id     BIGINT,
  fecha_inicio      DATE             NOT NULL,
  fecha_vencimiento DATE             NOT NULL,
  monto_pagado      DECIMAL(12,2)    NOT NULL,
  metodo_pago       VARCHAR(20)      NOT NULL DEFAULT 'efectivo' CHECK (metodo_pago IN ('efectivo','transferencia','tarjeta','otro')),
  estado            VARCHAR(20)      NOT NULL DEFAULT 'activa' CHECK (estado IN ('activa','vencida','cancelada','suspendida')),
  registrado_por    BIGINT           NOT NULL,
  observaciones     VARCHAR(500),
  creado_en         DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  actualizado_en    DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT pk_membresias PRIMARY KEY (id)
);
CREATE INDEX idx_membresias_socio ON membresias (socio_id);
CREATE INDEX idx_membresias_actividad ON membresias (actividad_id);
CREATE INDEX idx_membresias_instructor ON membresias (instructor_id);
CREATE INDEX idx_membresias_vencimiento ON membresias (socio_id, fecha_vencimiento);
CREATE INDEX idx_membresias_estado ON membresias (estado);
CREATE INDEX idx_membresias_reg_por ON membresias (registrado_por);

-- ─────────────────────────────────────────────
-- 7. REGISTROS DE ACCESO
-- ─────────────────────────────────────────────
CREATE TABLE registros_acceso (
  id             BIGINT IDENTITY(1,1) NOT NULL,
  socio_id       BIGINT           NOT NULL,
  membresia_id   BIGINT,
  metodo_acceso  VARCHAR(20)      NOT NULL CHECK (metodo_acceso IN ('huella','dni_pin','manual')),
  resultado      VARCHAR(50)      NOT NULL CHECK (resultado IN ('permitido','denegado_huella','denegado_vencimiento','denegado_dia','denegado_socio_inactivo','denegado_limite_semana')),
  accedido_en    DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT pk_registros_acceso PRIMARY KEY (id)
);
CREATE INDEX idx_acceso_socio_fecha ON registros_acceso (socio_id, accedido_en);
CREATE INDEX idx_acceso_membresia ON registros_acceso (membresia_id);
CREATE INDEX idx_acceso_fecha ON registros_acceso (accedido_en);
CREATE INDEX idx_acceso_resultado ON registros_acceso (resultado);

-- ─────────────────────────────────────────────
-- 8. CAJA (movimientos)
-- ─────────────────────────────────────────────
CREATE TABLE caja_movimientos (
  id             BIGINT IDENTITY(1,1) NOT NULL,
  tipo           VARCHAR(30)      NOT NULL CHECK (tipo IN ('ingreso_cuota','ingreso_clase','ingreso_venta','gasto','movimiento_interno')),
  subtipo        VARCHAR(100),
  usuario_id     BIGINT           NOT NULL,
  socio_id       BIGINT,
  membresia_id   BIGINT,
  actividad_id   BIGINT,
  venta_id       BIGINT,
  detalle        VARCHAR(500),
  metodo_pago    VARCHAR(20)      NOT NULL DEFAULT 'efectivo' CHECK (metodo_pago IN ('efectivo','transferencia','tarjeta','otro')),
  monto          DECIMAL(12,2)    NOT NULL,
  creado_en      DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT pk_caja_movimientos PRIMARY KEY (id)
);
CREATE INDEX idx_caja_usuario ON caja_movimientos (usuario_id);
CREATE INDEX idx_caja_socio ON caja_movimientos (socio_id);
CREATE INDEX idx_caja_membresia ON caja_movimientos (membresia_id);
CREATE INDEX idx_caja_venta ON caja_movimientos (venta_id);
CREATE INDEX idx_caja_tipo_fecha ON caja_movimientos (tipo, creado_en);
CREATE INDEX idx_caja_fecha ON caja_movimientos (creado_en);

-- ─────────────────────────────────────────────
-- 9. TURNOS
-- ─────────────────────────────────────────────
CREATE TABLE turnos (
  id            BIGINT IDENTITY(1,1) NOT NULL,
  actividad_id  BIGINT           NOT NULL,
  instructor_id BIGINT,
  dia_semana    TINYINT          NOT NULL,
  hora_inicio   TIME             NOT NULL,
  hora_fin      TIME             NOT NULL,
  cupo_maximo   SMALLINT         DEFAULT 30,
  activo        BIT              NOT NULL DEFAULT 1,
  CONSTRAINT pk_turnos PRIMARY KEY (id)
);
CREATE INDEX idx_turnos_actividad ON turnos (actividad_id);
CREATE INDEX idx_turnos_instructor ON turnos (instructor_id);
CREATE INDEX idx_turnos_dia ON turnos (dia_semana);
CREATE INDEX idx_turnos_dia_hora ON turnos (dia_semana, hora_inicio);

-- ─────────────────────────────────────────────
-- 10. INSTRUCTOR ASISTENCIAS
-- ─────────────────────────────────────────────
CREATE TABLE instructor_asistencias (
  id             BIGINT IDENTITY(1,1) NOT NULL,
  instructor_id  BIGINT           NOT NULL,
  turno_id       BIGINT,
  fecha          DATE             NOT NULL,
  hora_entrada   TIME,
  hora_salida    TIME,
  observaciones  VARCHAR(300),
  registrado_por BIGINT,
  creado_en      DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT pk_instructor_asist PRIMARY KEY (id)
);
CREATE INDEX idx_inst_asist_instructor ON instructor_asistencias (instructor_id);
CREATE INDEX idx_inst_asist_fecha ON instructor_asistencias (instructor_id, fecha);
CREATE INDEX idx_inst_asist_turno ON instructor_asistencias (turno_id);

-- ─────────────────────────────────────────────
-- 11. RUTINAS
-- ─────────────────────────────────────────────
CREATE TABLE rutinas (
  id               BIGINT IDENTITY(1,1) NOT NULL,
  nombre           VARCHAR(150)     NOT NULL,
  detalles         VARCHAR(MAX),
  duracion_semanas TINYINT          NOT NULL DEFAULT 4,
  links_videos     NVARCHAR(MAX),             -- Para guardar JSON
  creado_por       BIGINT           NOT NULL,
  activo           BIT              NOT NULL DEFAULT 1,
  creado_en        DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  actualizado_en   DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT pk_rutinas PRIMARY KEY (id)
);
CREATE INDEX idx_rutinas_creado_por ON rutinas (creado_por);
CREATE INDEX idx_rutinas_activo ON rutinas (activo);

-- ─────────────────────────────────────────────
-- 12. RUTINA BLOQUES
-- ─────────────────────────────────────────────
CREATE TABLE rutina_bloques (
  id        BIGINT IDENTITY(1,1) NOT NULL,
  rutina_id BIGINT           NOT NULL,
  nombre    VARCHAR(100)     NOT NULL DEFAULT 'Bloque 1',
  orden     TINYINT          NOT NULL DEFAULT 1,
  CONSTRAINT pk_rutina_bloques PRIMARY KEY (id)
);
CREATE INDEX idx_bloques_rutina ON rutina_bloques (rutina_id);

-- ─────────────────────────────────────────────
-- 13. RUTINA EJERCICIOS
-- ─────────────────────────────────────────────
CREATE TABLE rutina_ejercicios (
  id          BIGINT IDENTITY(1,1) NOT NULL,
  bloque_id   BIGINT           NOT NULL,
  nombre      VARCHAR(150)     NOT NULL,
  series      TINYINT,
  repeticiones VARCHAR(50),
  peso        VARCHAR(50),
  descanso_seg SMALLINT,
  notas       VARCHAR(500),
  link_video  VARCHAR(500),
  orden       TINYINT          NOT NULL DEFAULT 1,
  CONSTRAINT pk_rutina_ejercicios PRIMARY KEY (id)
);
CREATE INDEX idx_ejercicios_bloque ON rutina_ejercicios (bloque_id);

-- ─────────────────────────────────────────────
-- 14. RUTINA ASIGNACIONES
-- ─────────────────────────────────────────────
CREATE TABLE rutina_asignaciones (
  id           BIGINT IDENTITY(1,1) NOT NULL,
  rutina_id    BIGINT           NOT NULL,
  socio_id     BIGINT           NOT NULL,
  asignado_por BIGINT           NOT NULL,
  enviado_wp   BIT              NOT NULL DEFAULT 0,
  asignado_en  DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT pk_rutina_asign PRIMARY KEY (id)
);
CREATE INDEX idx_asign_rutina ON rutina_asignaciones (rutina_id);
CREATE INDEX idx_asign_socio ON rutina_asignaciones (socio_id);
CREATE INDEX idx_asign_por ON rutina_asignaciones (asignado_por);

-- ─────────────────────────────────────────────
-- 15. CASILLEROS
-- ─────────────────────────────────────────────
CREATE TABLE casilleros (
  id           BIGINT IDENTITY(1,1) NOT NULL,
  numero       SMALLINT         NOT NULL,
  socio_id     BIGINT,
  estado       VARCHAR(20)      NOT NULL DEFAULT 'libre' CHECK (estado IN ('libre','ocupado','mantenimiento')),
  precio_mes   DECIMAL(10,2),
  observaciones VARCHAR(300),
  asignado_en  DATETIME,
  CONSTRAINT pk_casilleros PRIMARY KEY (id),
  CONSTRAINT uq_casillero_numero UNIQUE (numero)
);
CREATE INDEX idx_casilleros_socio ON casilleros (socio_id);
CREATE INDEX idx_casilleros_estado ON casilleros (estado);

-- ─────────────────────────────────────────────
-- 16. PRODUCTOS
-- ─────────────────────────────────────────────
CREATE TABLE productos (
  id          BIGINT IDENTITY(1,1) NOT NULL,
  nombre      VARCHAR(150)     NOT NULL,
  descripcion VARCHAR(300),
  precio      DECIMAL(12,2)    NOT NULL,
  stock       INT              NOT NULL DEFAULT 0,
  stock_min   INT              NOT NULL DEFAULT 0,
  activo      BIT              NOT NULL DEFAULT 1,
  creado_en   DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT pk_productos PRIMARY KEY (id)
);
CREATE INDEX idx_productos_activo ON productos (activo);

-- ─────────────────────────────────────────────
-- 17. VENTAS
-- ─────────────────────────────────────────────
CREATE TABLE ventas (
  id           BIGINT IDENTITY(1,1) NOT NULL,
  usuario_id   BIGINT           NOT NULL,
  socio_id     BIGINT,
  total        DECIMAL(12,2)    NOT NULL,
  metodo_pago  VARCHAR(20)      NOT NULL DEFAULT 'efectivo' CHECK (metodo_pago IN ('efectivo','transferencia','tarjeta','otro')),
  observaciones VARCHAR(300),
  creado_en    DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT pk_ventas PRIMARY KEY (id)
);
CREATE INDEX idx_ventas_usuario ON ventas (usuario_id);
CREATE INDEX idx_ventas_socio ON ventas (socio_id);
CREATE INDEX idx_ventas_fecha ON ventas (creado_en);

-- ─────────────────────────────────────────────
-- 18. VENTAS ITEMS
-- ─────────────────────────────────────────────
CREATE TABLE ventas_items (
  id              BIGINT IDENTITY(1,1) NOT NULL,
  venta_id        BIGINT           NOT NULL,
  producto_id     BIGINT,
  descripcion     VARCHAR(200)     NOT NULL,
  cantidad        INT              NOT NULL DEFAULT 1,
  precio_unitario DECIMAL(12,2)    NOT NULL,
  subtotal        DECIMAL(12,2)    NOT NULL,
  CONSTRAINT pk_ventas_items PRIMARY KEY (id)
);
CREATE INDEX idx_vitems_venta ON ventas_items (venta_id);
CREATE INDEX idx_vitems_producto ON ventas_items (producto_id);

-- ─────────────────────────────────────────────
-- 19. WHATSAPP MENSAJES
-- ─────────────────────────────────────────────
CREATE TABLE whatsapp_mensajes (
  id          BIGINT IDENTITY(1,1) NOT NULL,
  tipo        VARCHAR(20)      NOT NULL CHECK (tipo IN ('automatico','masivo','rutina')),
  disparador  VARCHAR(100),
  socio_id    BIGINT,
  telefono    VARCHAR(20)      NOT NULL,
  mensaje     VARCHAR(MAX)     NOT NULL, -- Equivale a TEXT
  estado      VARCHAR(20)      NOT NULL DEFAULT 'pendiente' CHECK (estado IN ('pendiente','enviado','error')),
  enviado_por BIGINT,
  creado_en   DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  enviado_en  DATETIME,
  CONSTRAINT pk_whatsapp PRIMARY KEY (id)
);
CREATE INDEX idx_wp_socio ON whatsapp_mensajes (socio_id);
CREATE INDEX idx_wp_estado ON whatsapp_mensajes (estado);
CREATE INDEX idx_wp_tipo ON whatsapp_mensajes (tipo);
CREATE INDEX idx_wp_fecha ON whatsapp_mensajes (creado_en);

-- ─────────────────────────────────────────────
-- 20. AUDITORÍA
-- ─────────────────────────────────────────────
CREATE TABLE auditoria (
  id          BIGINT IDENTITY(1,1) NOT NULL,
  actor_id    BIGINT           NOT NULL,
  accion      VARCHAR(100)     NOT NULL,
  entidad     VARCHAR(50)      NOT NULL,
  entidad_id  BIGINT,
  detalle     NVARCHAR(MAX),             -- Para guardar JSON
  creado_en   DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT pk_auditoria PRIMARY KEY (id)
);
CREATE INDEX idx_audit_actor ON auditoria (actor_id);
CREATE INDEX idx_audit_entidad ON auditoria (entidad, entidad_id);
CREATE INDEX idx_audit_fecha ON auditoria (creado_en);