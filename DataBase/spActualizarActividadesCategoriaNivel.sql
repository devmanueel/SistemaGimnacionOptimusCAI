-- ============================================================
--  SCRIPT DE ACTUALIZACION - Columna categoria
--  Sistema Gimnasio OptimusCAI
--  Regla de upgrade: misma categoria y dias_sesiones mayor
-- ============================================================

-- Agregar categoria si no existe (idempotente)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('actividades') AND name = 'categoria'
)
    ALTER TABLE actividades ADD categoria VARCHAR(50) NULL;
GO

-- Actualizar categorias segun la logica del negocio
UPDATE actividades SET categoria = 'Boxeo' WHERE nombre = 'Boxeo Cai 2 Vxs';
UPDATE actividades SET categoria = 'Boxeo' WHERE nombre = 'Boxeo Cai 3 Vxs';
UPDATE actividades SET categoria = 'Boxeo' WHERE nombre = 'Boxeo Todos Los Dias';

UPDATE actividades SET categoria = 'Gimnasio' WHERE nombre = 'Gimnasio 2 Veces Por Semana';
UPDATE actividades SET categoria = 'Gimnasio' WHERE nombre = 'Gimnasio 3 Veces Por Semana';
UPDATE actividades SET categoria = 'Gimnasio' WHERE nombre = 'Gimnasio Todos Los Dias';

UPDATE actividades SET categoria = 'Deportistas' WHERE nombre = 'Deportistas Cai';
UPDATE actividades SET categoria = 'Deportistas' WHERE nombre = 'Deportistas Cai 3 Vxs';
UPDATE actividades SET categoria = 'Deportistas' WHERE nombre = 'Deportistas Cai Todos Los Dias';

UPDATE actividades SET categoria = 'Clase' WHERE nombre = 'Clase';

-- Verificacion
SELECT id, nombre, categoria, dias_sesiones, precio
FROM actividades
ORDER BY categoria, dias_sesiones;
GO
