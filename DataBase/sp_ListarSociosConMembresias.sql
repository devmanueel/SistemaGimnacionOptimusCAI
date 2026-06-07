-- ============================================================
--  STORED PROCEDURE - Listar socios con una membresia relevante
--  Sistema Gimnasio OptimusCAI - SQL Server / LocalDB
--
--  Retorna DOS result sets:
--    1) COUNT(*) AS total
--    2) Datos paginados
--
--  Ordenamiento:
--    Se aplica en SQL antes del OFFSET/FETCH para que la paginacion
--    infinita respete el orden global, no solo la pagina cargada.
--
--  Regla UX:
--    La grilla de Socios muestra una sola fila por socio.
--    Los chips Activos/Inactivos filtran por socios.activo.
--    Prioridad de membresia visible:
--      activa > vencida > suspendida > cancelada > sin membresia > resto
-- ============================================================

IF OBJECT_ID('sp_ListarSociosConMembresias', 'P') IS NOT NULL
    DROP PROCEDURE sp_ListarSociosConMembresias;
GO

CREATE PROCEDURE sp_ListarSociosConMembresias
    @Texto               NVARCHAR(100) = '',
    @FiltroEstado        VARCHAR(20)   = 'todos',
    @FiltroActividadId   BIGINT        = NULL,
    @FiltroCuotaVencida  BIT           = NULL,
    @FiltroInstructorId  BIGINT        = NULL,
    @FiltroSexo          VARCHAR(10)   = NULL,
    @FiltroDejaronVenir  INT           = NULL,
    @Pagina              INT           = 1,
    @TamPagina           INT           = 8,
    @Ordenamiento        VARCHAR(30)   = 'nombre_asc'
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE membresias
    SET estado = 'vencida',
        actualizado_en = GETDATE()
    WHERE estado = 'activa'
      AND fecha_vencimiento < CAST(GETDATE() AS DATE);

    ;WITH Base AS
    (
        SELECT
            s.id                                        AS socio_id,
            s.numero_socio,
            s.nombre,
            s.apellido,
            s.nombre + ' ' + s.apellido                AS socio_nombre,
            s.dni,
            s.telefono,
            s.email,
            s.sexo,
            s.fecha_nacimiento,
            s.foto,
            s.activo                                    AS socio_activo,
            m.id                                        AS membresia_id,
            m.actividad_id,
            m.instructor_id,
            m.fecha_inicio,
            m.fecha_vencimiento,
            m.monto_pagado,
            m.metodo_pago,
            m.estado                                    AS membresia_estado,
            m.tipo_plan,
            m.upgrade_realizado,
            a.nombre                                    AS actividad_nombre,
            a.categoria                                 AS actividad_categoria,
            a.dias_sesiones                             AS actividad_nivel,
            ISNULL(u.nombre + ' ' + u.apellido, 'Sin asignar') AS instructor_nombre,
            DATEDIFF(DAY, CAST(GETDATE() AS DATE), m.fecha_vencimiento) AS dias_para_vencer,
            (SELECT MAX(ra.accedido_en)
             FROM registros_acceso ra
             WHERE ra.socio_id = s.id
               AND ra.resultado = 'permitido') AS ultima_asistencia,
            DATEDIFF(DAY,
                (SELECT MAX(CAST(ra.accedido_en AS DATE))
                 FROM registros_acceso ra
                 WHERE ra.socio_id = s.id
                   AND ra.resultado = 'permitido'),
                CAST(GETDATE() AS DATE)
            ) AS dias_sin_asistir,
            ROW_NUMBER() OVER
            (
                PARTITION BY s.id
                ORDER BY
                    CASE m.estado
                        WHEN 'activa' THEN 1
                        WHEN 'vencida' THEN 2
                        WHEN 'suspendida' THEN 3
                        WHEN 'cancelada' THEN 4
                        ELSE 5
                    END,
                    m.fecha_vencimiento DESC,
                    m.id DESC
            ) AS rn
        FROM socios s
        LEFT JOIN membresias   m ON m.socio_id = s.id
        LEFT JOIN actividades  a ON a.id       = m.actividad_id
        LEFT  JOIN usuarios    u ON u.id       = m.instructor_id
        WHERE s.eliminado_en IS NULL
          AND (
                @FiltroEstado = 'todos'
             OR (@FiltroEstado = 'activos'   AND s.activo = 1)
             OR (@FiltroEstado = 'inactivos' AND s.activo = 0)
              )
          AND (
                @Texto = ''
             OR s.nombre   LIKE '%' + @Texto + '%'
             OR s.apellido LIKE '%' + @Texto + '%'
             OR s.dni      LIKE '%' + @Texto + '%'
             OR CAST(s.numero_socio AS VARCHAR(20)) LIKE '%' + @Texto + '%'
              )
          AND (@FiltroActividadId  IS NULL OR m.actividad_id    = @FiltroActividadId)
          AND (@FiltroCuotaVencida IS NULL OR (@FiltroCuotaVencida = 1 AND m.estado = 'vencida'))
          AND (@FiltroInstructorId IS NULL OR m.instructor_id   = @FiltroInstructorId)
          AND (@FiltroSexo         IS NULL OR s.sexo            = @FiltroSexo)
          AND (@FiltroDejaronVenir IS NULL
               OR (
                   (SELECT MAX(CAST(ra.accedido_en AS DATE))
                    FROM registros_acceso ra
                    WHERE ra.socio_id = s.id
                      AND ra.resultado = 'permitido')
                   <= DATEADD(DAY, -@FiltroDejaronVenir, CAST(GETDATE() AS DATE))
                   OR (
                       m.fecha_inicio <= DATEADD(DAY, -@FiltroDejaronVenir, CAST(GETDATE() AS DATE))
                       AND NOT EXISTS (
                           SELECT 1 FROM registros_acceso ra
                           WHERE ra.socio_id = s.id
                             AND ra.resultado = 'permitido'
                       )
                   )
               ))
    )
    SELECT COUNT(*) AS total
    FROM Base
    WHERE rn = 1;

    ;WITH Base AS
    (
        SELECT
            s.id                                        AS socio_id,
            s.numero_socio,
            s.nombre,
            s.apellido,
            s.nombre + ' ' + s.apellido                AS socio_nombre,
            s.dni,
            s.telefono,
            s.email,
            s.sexo,
            s.fecha_nacimiento,
            s.foto,
            s.activo                                    AS socio_activo,
            m.id                                        AS membresia_id,
            m.actividad_id,
            m.instructor_id,
            m.fecha_inicio,
            m.fecha_vencimiento,
            m.monto_pagado,
            m.metodo_pago,
            m.estado                                    AS membresia_estado,
            m.tipo_plan,
            m.upgrade_realizado,
            a.nombre                                    AS actividad_nombre,
            a.categoria                                 AS actividad_categoria,
            a.dias_sesiones                             AS actividad_nivel,
            ISNULL(u.nombre + ' ' + u.apellido, 'Sin asignar') AS instructor_nombre,
            DATEDIFF(DAY, CAST(GETDATE() AS DATE), m.fecha_vencimiento) AS dias_para_vencer,
            (SELECT MAX(ra.accedido_en)
             FROM registros_acceso ra
             WHERE ra.socio_id = s.id
               AND ra.resultado = 'permitido') AS ultima_asistencia,
            DATEDIFF(DAY,
                (SELECT MAX(CAST(ra.accedido_en AS DATE))
                 FROM registros_acceso ra
                 WHERE ra.socio_id = s.id
                   AND ra.resultado = 'permitido'),
                CAST(GETDATE() AS DATE)
            ) AS dias_sin_asistir,
            ROW_NUMBER() OVER
            (
                PARTITION BY s.id
                ORDER BY
                    CASE m.estado
                        WHEN 'activa' THEN 1
                        WHEN 'vencida' THEN 2
                        WHEN 'suspendida' THEN 3
                        WHEN 'cancelada' THEN 4
                        ELSE 5
                    END,
                    m.fecha_vencimiento DESC,
                    m.id DESC
            ) AS rn
        FROM socios s
        LEFT JOIN membresias   m ON m.socio_id = s.id
        LEFT JOIN actividades  a ON a.id       = m.actividad_id
        LEFT  JOIN usuarios    u ON u.id       = m.instructor_id
        WHERE s.eliminado_en IS NULL
          AND (
                @FiltroEstado = 'todos'
             OR (@FiltroEstado = 'activos'   AND s.activo = 1)
             OR (@FiltroEstado = 'inactivos' AND s.activo = 0)
              )
          AND (
                @Texto = ''
             OR s.nombre   LIKE '%' + @Texto + '%'
             OR s.apellido LIKE '%' + @Texto + '%'
             OR s.dni      LIKE '%' + @Texto + '%'
             OR CAST(s.numero_socio AS VARCHAR(20)) LIKE '%' + @Texto + '%'
              )
          AND (@FiltroActividadId  IS NULL OR m.actividad_id    = @FiltroActividadId)
          AND (@FiltroCuotaVencida IS NULL OR (@FiltroCuotaVencida = 1 AND m.estado = 'vencida'))
          AND (@FiltroInstructorId IS NULL OR m.instructor_id   = @FiltroInstructorId)
          AND (@FiltroSexo         IS NULL OR s.sexo            = @FiltroSexo)
          AND (@FiltroDejaronVenir IS NULL
               OR (
                   (SELECT MAX(CAST(ra.accedido_en AS DATE))
                    FROM registros_acceso ra
                    WHERE ra.socio_id = s.id
                      AND ra.resultado = 'permitido')
                   <= DATEADD(DAY, -@FiltroDejaronVenir, CAST(GETDATE() AS DATE))
                   OR (
                       m.fecha_inicio <= DATEADD(DAY, -@FiltroDejaronVenir, CAST(GETDATE() AS DATE))
                       AND NOT EXISTS (
                           SELECT 1 FROM registros_acceso ra
                           WHERE ra.socio_id = s.id
                             AND ra.resultado = 'permitido'
                       )
                   )
               ))
    )
    SELECT
        socio_id,
        numero_socio,
        nombre,
        apellido,
        socio_nombre,
        dni,
        telefono,
        email,
        sexo,
        fecha_nacimiento,
        foto,
        socio_activo,
        membresia_id,
        actividad_id,
        instructor_id,
        fecha_inicio,
        fecha_vencimiento,
        monto_pagado,
        metodo_pago,
        membresia_estado,
        tipo_plan,
        upgrade_realizado,
        actividad_nombre,
        actividad_categoria,
        actividad_nivel,
        instructor_nombre,
        dias_para_vencer,
        ultima_asistencia,
        dias_sin_asistir
    FROM Base
    WHERE rn = 1
    ORDER BY
        CASE WHEN @Ordenamiento = 'nombre_asc' THEN apellido END ASC,
        CASE WHEN @Ordenamiento = 'nombre_asc' THEN nombre END ASC,
        CASE WHEN @Ordenamiento = 'nombre_desc' THEN apellido END DESC,
        CASE WHEN @Ordenamiento = 'nombre_desc' THEN nombre END DESC,
        CASE WHEN @Ordenamiento = 'vencimiento_desc' THEN CASE WHEN fecha_vencimiento IS NULL THEN 1 ELSE 0 END END ASC,
        CASE WHEN @Ordenamiento = 'vencimiento_desc' THEN fecha_vencimiento END DESC,
        CASE WHEN @Ordenamiento = 'vencimiento_asc' THEN CASE WHEN fecha_vencimiento IS NULL THEN 1 ELSE 0 END END ASC,
        CASE WHEN @Ordenamiento = 'vencimiento_asc' THEN fecha_vencimiento END ASC,
        apellido ASC,
        nombre ASC
    OFFSET (@Pagina - 1) * @TamPagina ROWS
    FETCH NEXT @TamPagina ROWS ONLY;
END;
GO
