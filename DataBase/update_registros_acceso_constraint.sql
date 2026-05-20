-- ============================================================
--  UPDATE: Agregar nuevo resultado 'denegado_limite_semana'
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
-- ============================================================

-- 1. Eliminar el CHECK constraint existente (sin importar el nombre)
DECLARE @ConstraintName NVARCHAR(128);
DECLARE @Sql NVARCHAR(MAX);

SELECT @ConstraintName = name
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('registros_acceso')
  AND type = 'C';

IF @ConstraintName IS NOT NULL
BEGIN
    SET @Sql = 'ALTER TABLE registros_acceso DROP CONSTRAINT ' + QUOTENAME(@ConstraintName);
    EXEC sp_executesql @Sql;
    PRINT '✓ Constraint eliminado: ' + @ConstraintName;
END
GO

-- 2. Actualizar valores antiguos 'denegado_limite' a 'denegado_limite_semana'
IF EXISTS (SELECT 1 FROM registros_acceso WHERE resultado = 'denegado_limite')
BEGIN
    UPDATE registros_acceso SET resultado = 'denegado_limite_semana' WHERE resultado = 'denegado_limite';
    PRINT '✓ Valores actualizados de denegado_limite a denegado_limite_semana';
END
GO

-- 3. Agregar nuevo CHECK constraint con el valor 'denegado_limite_semana'
ALTER TABLE registros_acceso
ADD CONSTRAINT CK_registros_acceso_resultado
CHECK (resultado IN (
    'permitido',
    'denegado_huella',
    'denegado_vencimiento',
    'denegado_dia',
    'denegado_socio_inactivo',
    'denegado_limite_semana'
));
GO

PRINT '✓ Constraint actualizado correctamente';
GO
