-- ============================================================
--  STORED PROCEDURE — Listar socios con membresías (filtros avanzados + paginación)
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
--
--  Retorna DOS result sets:
--    1) COUNT(*) AS total  (para saber si hay más páginas)
--    2) Datos paginados con OFFSET/FETCH
--
--  Filtros: texto, estado membresía, actividad, cuota vencida,
--           instructor, sexo, días sin asistir.
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
    @TamPagina           INT           = 8
AS
BEGIN
    SET NOCOUNT ON;

    -- Actualizar estados vencidos
    UPDATE membresias
    SET estado = 'vencida'
    WHERE estado = 'activa' AND fecha_vencimiento < CAST(GETDATE() AS DATE);

    -- ── Result set 1: total de registros ──
    SELECT COUNT(*) AS total
    FROM socios s
    INNER JOIN membresias  m ON m.socio_id = s.id
    INNER JOIN actividades a ON a.id       = m.actividad_id
    WHERE s.eliminado_en IS NULL
      AND (
            @FiltroEstado = 'todos'
         OR (@FiltroEstado = 'activos'   AND m.estado = 'activa')
         OR (@FiltroEstado = 'inactivos' AND m.estado IN ('vencida', 'cancelada'))
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
           OR NOT EXISTS (
               SELECT 1 FROM registros_acceso ra
               WHERE ra.socio_id  = s.id
                 AND ra.resultado = 'permitido'
                 AND ra.accedido_en >= DATEADD(DAY, -@FiltroDejaronVenir, GETDATE())
           ));

    -- ── Result set 2: datos paginados ──
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
        a.nivel                                     AS actividad_nivel,
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
        ) AS dias_sin_asistir
    FROM socios s
    INNER JOIN membresias  m ON m.socio_id = s.id
    INNER JOIN actividades a ON a.id       = m.actividad_id
    LEFT  JOIN usuarios    u ON u.id       = m.instructor_id
    WHERE s.eliminado_en IS NULL
      AND (
            @FiltroEstado = 'todos'
         OR (@FiltroEstado = 'activos'   AND m.estado = 'activa')
         OR (@FiltroEstado = 'inactivos' AND m.estado IN ('vencida', 'cancelada'))
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
           OR NOT EXISTS (
               SELECT 1 FROM registros_acceso ra
               WHERE ra.socio_id  = s.id
                 AND ra.resultado = 'permitido'
                 AND ra.accedido_en >= DATEADD(DAY, -@FiltroDejaronVenir, GETDATE())
           ))
    ORDER BY s.apellido, s.nombre, m.fecha_vencimiento DESC
    OFFSET (@Pagina - 1) * @TamPagina ROWS
    FETCH NEXT @TamPagina ROWS ONLY;
END;
GO
