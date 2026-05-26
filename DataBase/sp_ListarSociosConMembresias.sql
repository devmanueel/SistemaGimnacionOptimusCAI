-- ============================================================
--  STORED PROCEDURE — Listar socios con membresías
--  Sistema Gimnasio OptimusCAI · SQL Server / LocalDB
--
--  Retorna UNA FILA POR CADA MEMBRESÍA del socio.
--  Filtra por estado de membresía y filtros avanzados.
-- ============================================================

IF OBJECT_ID('sp_ListarSociosConMembresias', 'P') IS NOT NULL
    DROP PROCEDURE sp_ListarSociosConMembresias;
GO

CREATE PROCEDURE sp_ListarSociosConMembresias
    @Texto           NVARCHAR(100) = '',
    @FiltroEstado    VARCHAR(20)   = 'todos',
    @FiltroAvanzado  VARCHAR(30)   = 'todos',
    @ActividadId     BIGINT        = NULL,
    @FechaDesde      DATE          = NULL,
    @FechaHasta      DATE          = NULL,
    @DiasSinAsistencia INT         = NULL,
    @InstructorId    BIGINT        = NULL,
    @Sexo            VARCHAR(10)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    WITH UltimaAsistenciaSocio AS (
        SELECT
            r.socio_id,
            MAX(r.accedido_en) AS ultima_asistencia
        FROM registros_acceso r
        WHERE r.resultado = 'permitido'
        GROUP BY r.socio_id
    )
    SELECT
        s.id,
        s.numero_socio,
        s.nombre,
        s.apellido,
        s.dni,
        s.dni_pin,
        s.foto,
        s.fecha_nacimiento,
        s.sexo,
        s.telefono,
        s.domicilio,
        s.profesion,
        s.email,
        s.como_nos_conocio,
        s.observaciones,
        s.activo,
        s.registrado_por,
        u_reg.nombre + ' ' + u_reg.apellido AS registrado_por_nombre,
        s.creado_en,
        s.actualizado_en,
        a.nombre                            AS actividad_nombre,
        m.fecha_vencimiento,
        m.estado                            AS membresia_estado,
        ua.ultima_asistencia,
        ISNULL(i.nombre + ' ' + i.apellido, 'Sin asignar') AS instructor_nombre
    FROM socios s
    INNER JOIN membresias m ON m.socio_id = s.id
    INNER JOIN actividades a ON a.id = m.actividad_id
    LEFT JOIN usuarios u_reg ON u_reg.id = s.registrado_por
    LEFT JOIN usuarios i ON i.id = m.instructor_id
    LEFT JOIN UltimaAsistenciaSocio ua ON ua.socio_id = s.id
    WHERE s.eliminado_en IS NULL
      -- Filtro por texto
      AND (
            @Texto = ''
         OR s.nombre   LIKE '%' + @Texto + '%'
         OR s.apellido LIKE '%' + @Texto + '%'
         OR s.dni      LIKE '%' + @Texto + '%'
         OR CAST(s.numero_socio AS VARCHAR(20)) LIKE '%' + @Texto + '%'
           )
      -- Filtro por estado de membresía
      AND (
            @FiltroEstado = 'todos'
         OR (@FiltroEstado = 'activos' AND m.estado = 'activa')
         OR (@FiltroEstado = 'inactivos' AND m.estado IN ('cancelada', 'vencida', 'suspendida'))
          )
      -- Filtro por sexo
      AND (@Sexo IS NULL OR s.sexo = @Sexo)
      -- Filtro por actividad
      AND (@FiltroAvanzado <> 'actividad' OR @ActividadId IS NULL OR m.actividad_id = @ActividadId)
      -- Filtro por instructor
      AND (@FiltroAvanzado <> 'instructor' OR @InstructorId IS NULL OR m.instructor_id = @InstructorId)
      -- Filtro por vencimiento
      AND (
            @FiltroAvanzado <> 'vencimiento'
         OR @FechaDesde IS NULL
         OR @FechaHasta IS NULL
         OR m.fecha_vencimiento BETWEEN @FechaDesde AND @FechaHasta
           )
      -- Filtro por inactividad
      AND (
            @FiltroAvanzado <> 'inactividad'
         OR @DiasSinAsistencia IS NULL
         OR (
               ua.ultima_asistencia IS NULL
            OR CAST(ua.ultima_asistencia AS DATE) <= DATEADD(DAY, -@DiasSinAsistencia, CAST(GETDATE() AS DATE))
            )
           )
    ORDER BY s.apellido, s.nombre, a.nombre;
END;
GO
