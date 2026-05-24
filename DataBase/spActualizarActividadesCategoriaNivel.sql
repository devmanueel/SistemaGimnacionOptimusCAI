-- ============================================================
--  SCRIPT DE ACTUALIZACIÓN — Columnas categoria y nivel
--  Sistema Gimnasio OptimusCAI
--  Para usar con la regla de cambio de plan
-- ============================================================

USE [DB_CAI_Optimus];
GO

-- Agregar columnas si no existen (idempotente)
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

-- Actualizar categorías y niveles según la lógica del negocio
-- BOXEO (categoría: Boxeo)
UPDATE actividades SET categoria = 'Boxeo', nivel = 1 WHERE nombre = 'Boxeo Cai 2 Vxs';
UPDATE actividades SET categoria = 'Boxeo', nivel = 2 WHERE nombre = 'Boxeo Cai 3 Vxs';
UPDATE actividades SET categoria = 'Boxeo', nivel = 3 WHERE nombre = 'Boxeo Todos Los Dias';

-- GIMNASIO (categoría: Gimnasio)
UPDATE actividades SET categoria = 'Gimnasio', nivel = 1 WHERE nombre = 'Gimnasio 2 Veces Por Semana';
UPDATE actividades SET categoria = 'Gimnasio', nivel = 2 WHERE nombre = 'Gimnasio 3 Veces Por Semana';
UPDATE actividades SET categoria = 'Gimnasio', nivel = 3 WHERE nombre = 'Gimnasio Todos Los Dias';

-- DEPORTISTAS (categoría: Deportistas)
UPDATE actividades SET categoria = 'Deportistas', nivel = 1 WHERE nombre = 'Deportistas Cai';
UPDATE actividades SET categoria = 'Deportistas', nivel = 2 WHERE nombre = 'Deportistas Cai 3 Vxs';
UPDATE actividades SET categoria = 'Deportistas', nivel = 3 WHERE nombre = 'Deportistas Cai Todos Los Dias';

-- CLASE (categoría: Clase, nivel único)
UPDATE actividades SET categoria = 'Clase', nivel = 1 WHERE nombre = 'Clase';

-- Verificación
SELECT id, nombre, categoria, nivel, precio 
FROM actividades 
ORDER BY categoria, nivel;
GO
