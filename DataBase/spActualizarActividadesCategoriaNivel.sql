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

-- Actualizar categorias segun la logica del negocio.
-- Se usa LIKE para soportar nombres abreviados, por ejemplo:
-- "Gimnasio 2 Vxs", "Gimnasio 3 Vxs" o "Gimnasio 3 Veces Por Semana".
UPDATE actividades SET categoria = 'Boxeo'
WHERE nombre LIKE '%Boxeo%';

UPDATE actividades SET categoria = 'Gimnasio'
WHERE nombre LIKE '%Gimnasio%' OR nombre LIKE '%Gym%';

UPDATE actividades SET categoria = 'Deportistas'
WHERE nombre LIKE '%Deportista%';

UPDATE actividades SET categoria = 'Clase'
WHERE nombre LIKE '%Clase%';

-- Verificacion
SELECT id, nombre, categoria, dias_sesiones, precio
FROM actividades
ORDER BY categoria, dias_sesiones;
GO
