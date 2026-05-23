# SDD — Módulo Reportes Financieros y Sueldos Docentes
> Spec-Driven Development — Requisitos relevados en entrevista con el dueño  
> Versión 1.0 — Mayo 2026  
> Para ser leído por Claude Code ANTES de escribir cualquier archivo

---

## 1. CONDICIONES — Reglas no negociables

```
- C# 7.3 estricto. Sin switch expressions, sin using simplificado
- SQL Server LocalDB: siempre DROP + CREATE (nunca CREATE OR ALTER)
- Sin SQL inline en los DAOs. Solo Stored Procedures
- Sin LetterSpacing en XAML. Sin DropShadowEffect dentro de Triggers
- SesionManager.UsuarioId en lugar de cualquier ID hardcodeado
- Auditor.Registrar() en métodos de escritura (exportar PDF/Excel cuenta como lectura, no requiere auditoría)
- iTextSharp v5.5.13.3 para PDF. ClosedXML para Excel (.NET Framework compatible)
- Gráfico de barras: usar la librería OxyPlot.Wpf (NuGet) o dibujar con WPF Canvas nativo si OxyPlot da conflictos
```

**Paquetes NuGet necesarios:**
```
Install-Package iTextSharp -Version 5.5.13.3
Install-Package ClosedXML -Version 0.95.4
Install-Package OxyPlot.Wpf -Version 2.1.0
```

---

## 2. ESPECIFICACIÓN — Qué construir

### Contexto del negocio (relevado en entrevista)

| Pregunta | Respuesta del dueño |
|----------|-------------------|
| ¿Quién ve los reportes? | Admin ve todo. Empleado solo ve sus propias ventas del día |
| ¿Frecuencia de reporte? | Por rango de fechas libre (el usuario elige desde/hasta) |
| ¿Formato preferido? | Reporte combinado con secciones separadas |
| ¿Qué forma las ganancias? | Solo ingresos por membresías |
| ¿Se calcula ganancia neta? | Sí, pero el sistema muestra ingresos y egresos por separado. El admin resta manualmente |
| ¿Cómo acceder al reporte? | Pantalla en el sistema + PDF imprimible + Excel editable |
| ¿Cómo se paga a los docentes? | Horas trabajadas × tarifa/hora |
| ¿Registrar pago de sueldo? | Solo lectura — el sistema muestra cuánto corresponde, no registra el pago |
| ¿Desglose de ingresos? | Por instructor (cuánto generó cada profe con sus alumnos) |
| ¿Gráfico visual? | Barras con ingresos por mes |
| ¿Reporte de deudas? | Sí, socios con membresía vencida o próxima a vencer |
| ¿Encabezado del PDF? | Nombre + logo + dirección + teléfono del gimnasio |

---

## 3. ARQUITECTURA — Estructura del módulo

### Nueva página: `ReportesPage.xaml`

La página tiene **4 secciones** navegables con tabs internos:

```
ReportesPage
├── Tab 1: Ingresos y Ganancias
│   ├── Selector de fechas (desde / hasta)
│   ├── Filtros (actividad, método de pago, instructor)
│   ├── Tabla de movimientos agrupados
│   ├── Panel de totales (ingresos, egresos, balance)
│   ├── Gráfico de barras (ingresos por mes)
│   └── Botones: Exportar PDF | Exportar Excel
│
├── Tab 2: Sueldos de Docentes
│   ├── Selector mes/año O rango libre
│   ├── Tabla por instructor: días, horas, tarifa, total a pagar
│   ├── Desglose: cuánto generó cada profe con sus alumnos
│   └── Botones: Exportar PDF | Exportar Excel
│
├── Tab 3: Socios con Deuda
│   ├── Lista de socios con membresía vencida
│   ├── Lista de socios con membresía próxima a vencer (próximos 7 días)
│   └── Botón: Exportar PDF | Ir al módulo WhatsApp para avisar
│
└── Tab 4: Mis Ventas (solo visible para empleado)
    ├── Ventas del día del empleado logueado
    └── Total recaudado por él en el día
```

### Permisos por rol

| Tab | Admin | Empleado |
|-----|-------|----------|
| Ingresos y Ganancias | ✅ Ver completo | ❌ Oculto |
| Sueldos de Docentes | ✅ Ver completo | ❌ Oculto |
| Socios con Deuda | ✅ Ver completo | ❌ Oculto |
| Mis Ventas | ✅ Ve las suyas | ✅ Solo las suyas |

---

## 4. BASE DE DATOS — SPs necesarios

### SP 1 — `sp_ReporteIngresos`

```sql
IF OBJECT_ID('sp_ReporteIngresos','P') IS NOT NULL DROP PROCEDURE sp_ReporteIngresos;
GO
CREATE PROCEDURE sp_ReporteIngresos
    @FechaDesde   DATE = NULL,
    @FechaHasta   DATE = NULL,
    @ActividadId  BIGINT = NULL,
    @MetodoPago   VARCHAR(30) = NULL,
    @InstructorId BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FechaDesde IS NULL SET @FechaDesde = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    -- ─── Movimientos de caja en el período ───
    SELECT
        cm.id,
        cm.tipo,
        cm.subtipo,
        cm.concepto,
        cm.monto,
        cm.metodo_pago,
        cm.referencia_tipo,
        CAST(cm.creado_en AS DATE)                          AS fecha,
        ISNULL(u.nombre + ' ' + u.apellido, 'Sistema')      AS registrado_por_nombre,
        -- Actividad vinculada (solo para membresías)
        ISNULL(a.nombre, '—')                               AS actividad_nombre,
        -- Instructor de la membresía (el que tiene socios de esa actividad)
        ISNULL(ui.nombre + ' ' + ui.apellido, '—')          AS instructor_nombre
    FROM caja_movimientos cm
    LEFT JOIN usuarios u ON u.id = cm.registrado_por
    -- JOIN para traer actividad e instructor cuando el movimiento viene de membresía
    LEFT JOIN membresias m ON m.id = cm.referencia_id AND cm.referencia_tipo = 'membresia'
    LEFT JOIN actividades a ON a.id = m.actividad_id
    LEFT JOIN turnos t ON t.actividad_id = a.id AND t.activo = 1
    LEFT JOIN usuarios ui ON ui.id = t.instructor_id
    WHERE CAST(cm.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND (@ActividadId  IS NULL OR a.id          = @ActividadId)
      AND (@MetodoPago   IS NULL OR cm.metodo_pago = @MetodoPago)
      AND (@InstructorId IS NULL OR ui.id          = @InstructorId)
    ORDER BY cm.creado_en DESC;
END;
GO
```

### SP 2 — `sp_ReporteTotales`

```sql
IF OBJECT_ID('sp_ReporteTotales','P') IS NOT NULL DROP PROCEDURE sp_ReporteTotales;
GO
CREATE PROCEDURE sp_ReporteTotales
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FechaDesde IS NULL SET @FechaDesde = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    -- ─── Totales generales ───
    SELECT
        ISNULL(SUM(CASE WHEN tipo = 'ingreso' THEN monto ELSE 0 END), 0) AS total_ingresos,
        ISNULL(SUM(CASE WHEN tipo = 'egreso'  THEN monto ELSE 0 END), 0) AS total_egresos,
        ISNULL(SUM(CASE WHEN tipo = 'ingreso' THEN monto ELSE -monto END), 0) AS balance,
        COUNT(CASE WHEN tipo = 'ingreso' THEN 1 END)                     AS cantidad_ingresos,
        COUNT(CASE WHEN tipo = 'egreso'  THEN 1 END)                     AS cantidad_egresos
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta;

    -- ─── Ingresos por actividad ───
    SELECT
        ISNULL(a.nombre, 'Sin actividad')                   AS actividad,
        ISNULL(SUM(cm.monto), 0)                            AS total,
        COUNT(*)                                             AS cantidad
    FROM caja_movimientos cm
    LEFT JOIN membresias m ON m.id = cm.referencia_id AND cm.referencia_tipo = 'membresia'
    LEFT JOIN actividades a ON a.id = m.actividad_id
    WHERE CAST(cm.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND cm.tipo = 'ingreso'
    GROUP BY a.nombre
    ORDER BY total DESC;

    -- ─── Ingresos por método de pago ───
    SELECT
        metodo_pago,
        ISNULL(SUM(monto), 0) AS total,
        COUNT(*) AS cantidad
    FROM caja_movimientos
    WHERE CAST(creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
      AND tipo = 'ingreso'
    GROUP BY metodo_pago
    ORDER BY total DESC;
END;
GO
```

### SP 3 — `sp_GraficoIngresosPorMes`

```sql
IF OBJECT_ID('sp_GraficoIngresosPorMes','P') IS NOT NULL DROP PROCEDURE sp_GraficoIngresosPorMes;
GO
CREATE PROCEDURE sp_GraficoIngresosPorMes
    @Anio INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Anio IS NULL SET @Anio = YEAR(GETDATE());

    SELECT
        MONTH(creado_en)                        AS mes,
        DATENAME(MONTH, DATEFROMPARTS(@Anio, MONTH(creado_en), 1)) AS mes_nombre,
        ISNULL(SUM(CASE WHEN tipo='ingreso' THEN monto ELSE 0 END), 0) AS ingresos,
        ISNULL(SUM(CASE WHEN tipo='egreso'  THEN monto ELSE 0 END), 0) AS egresos
    FROM caja_movimientos
    WHERE YEAR(creado_en) = @Anio
    GROUP BY MONTH(creado_en)
    ORDER BY MONTH(creado_en);
END;
GO
```

### SP 4 — `sp_ReporteSueldosDocentes`

```sql
IF OBJECT_ID('sp_ReporteSueldosDocentes','P') IS NOT NULL DROP PROCEDURE sp_ReporteSueldosDocentes;
GO
CREATE PROCEDURE sp_ReporteSueldosDocentes
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FechaDesde IS NULL SET @FechaDesde = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
    IF @FechaHasta IS NULL SET @FechaHasta = CAST(GETDATE() AS DATE);

    -- ─── Resumen por instructor ───
    SELECT
        u.id                                                    AS instructor_id,
        u.nombre + ' ' + u.apellido                             AS nombre_completo,
        u.foto,
        ISNULL(u.tarifa_hora, 0)                                AS tarifa_hora,
        ISNULL(a.nombre, '—')                                   AS actividad_nombre,
        COUNT(DISTINCT ia.fecha)                                AS dias_trabajados,
        ISNULL(SUM(ia.horas_trabajadas), 0)                     AS horas_totales,
        ISNULL(SUM(ia.horas_trabajadas), 0) * ISNULL(u.tarifa_hora, 0) AS sueldo_estimado,
        -- Ingresos generados por los socios de su actividad en el período
        ISNULL((
            SELECT SUM(cm2.monto)
            FROM caja_movimientos cm2
            INNER JOIN membresias m2 ON m2.id = cm2.referencia_id
                AND cm2.referencia_tipo = 'membresia'
            INNER JOIN turnos t2 ON t2.actividad_id = m2.actividad_id
                AND t2.instructor_id = u.id AND t2.activo = 1
            WHERE cm2.tipo = 'ingreso'
              AND CAST(cm2.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
        ), 0)                                                   AS ingresos_generados
    FROM usuarios u
    LEFT JOIN instructor_asistencias ia
           ON ia.instructor_id = u.id
          AND ia.fecha BETWEEN @FechaDesde AND @FechaHasta
    LEFT JOIN turnos t ON t.instructor_id = u.id AND t.activo = 1
    LEFT JOIN actividades a ON a.id = t.actividad_id
    WHERE u.rol_id = 2
      AND u.activo = 1
      AND u.eliminado_en IS NULL
    GROUP BY u.id, u.nombre, u.apellido, u.foto, u.tarifa_hora, a.nombre
    ORDER BY u.apellido ASC;

    -- ─── Detalle de asistencias por instructor ───
    SELECT
        ia.instructor_id,
        ia.fecha,
        ia.hora_entrada,
        ia.hora_salida,
        ISNULL(ia.horas_trabajadas, 0) AS horas_trabajadas
    FROM instructor_asistencias ia
    WHERE ia.fecha BETWEEN @FechaDesde AND @FechaHasta
    ORDER BY ia.instructor_id, ia.fecha;
END;
GO
```

### SP 5 — `sp_ReporteSociosDeuda`

```sql
IF OBJECT_ID('sp_ReporteSociosDeuda','P') IS NOT NULL DROP PROCEDURE sp_ReporteSociosDeuda;
GO
CREATE PROCEDURE sp_ReporteSociosDeuda
    @DiasProximos INT = 7
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);
    DECLARE @Limite DATE = DATEADD(DAY, @DiasProximos, @Hoy);

    -- ─── Membresías ya vencidas ───
    SELECT
        s.id AS socio_id,
        s.nombre + ' ' + s.apellido AS nombre_completo,
        s.numero_socio,
        s.telefono,
        s.foto,
        m.id AS membresia_id,
        m.tipo_plan,
        a.nombre AS actividad_nombre,
        m.fecha_vencimiento,
        DATEDIFF(DAY, m.fecha_vencimiento, @Hoy) AS dias_vencida,
        'vencida' AS estado_deuda
    FROM membresias m
    INNER JOIN socios s ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.estado = 'activa'
      AND m.fecha_vencimiento < @Hoy
      AND s.activo = 1
    ORDER BY dias_vencida DESC;

    -- ─── Membresías próximas a vencer ───
    SELECT
        s.id AS socio_id,
        s.nombre + ' ' + s.apellido AS nombre_completo,
        s.numero_socio,
        s.telefono,
        s.foto,
        m.id AS membresia_id,
        m.tipo_plan,
        a.nombre AS actividad_nombre,
        m.fecha_vencimiento,
        DATEDIFF(DAY, @Hoy, m.fecha_vencimiento) AS dias_para_vencer,
        'proxima_a_vencer' AS estado_deuda
    FROM membresias m
    INNER JOIN socios s ON s.id = m.socio_id
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.estado = 'activa'
      AND m.fecha_vencimiento BETWEEN @Hoy AND @Limite
      AND s.activo = 1
    ORDER BY dias_para_vencer ASC;
END;
GO
```

### SP 6 — `sp_MisVentasDelDia` (para empleado)

```sql
IF OBJECT_ID('sp_MisVentasDelDia','P') IS NOT NULL DROP PROCEDURE sp_MisVentasDelDia;
GO
CREATE PROCEDURE sp_MisVentasDelDia
    @EmpleadoId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);

    SELECT
        v.id,
        v.total,
        v.metodo_pago,
        v.creado_en,
        COUNT(vi.id) AS cantidad_items
    FROM ventas v
    INNER JOIN ventas_items vi ON vi.venta_id = v.id
    WHERE v.registrado_por = @EmpleadoId
      AND CAST(v.creado_en AS DATE) = @Hoy
    GROUP BY v.id, v.total, v.metodo_pago, v.creado_en
    ORDER BY v.creado_en DESC;

    -- Total del día
    SELECT
        ISNULL(SUM(v.total), 0) AS total_dia,
        COUNT(v.id)             AS cantidad_ventas
    FROM ventas v
    WHERE v.registrado_por = @EmpleadoId
      AND CAST(v.creado_en AS DATE) = @Hoy;
END;
GO
```

---

## 5. ENTITIES — Clases nuevas

### `Entities/ReporteFinanciero.cs`

```csharp
// Entities/ReporteFinanciero.cs — C# 7.3
using System;

namespace Entities
{
    public class MovimientoReporte
    {
        public long     Id              { get; set; }
        public string   Tipo            { get; set; }
        public string   Subtipo         { get; set; }
        public string   Concepto        { get; set; }
        public decimal  Monto           { get; set; }
        public string   MetodoPago      { get; set; }
        public string   ReferenciaKipo  { get; set; }
        public DateTime Fecha           { get; set; }
        public string   RegistradoPor   { get; set; }
        public string   ActividadNombre { get; set; }
        public string   InstructorNombre{ get; set; }

        public string FechaTexto   => Fecha.ToString("dd/MM/yyyy");
        public string MontoTexto   => "$" + Monto.ToString("N2");
        public bool   EsIngreso    => Tipo == "ingreso";
    }

    public class TotalesReporte
    {
        public decimal TotalIngresos    { get; set; }
        public decimal TotalEgresos     { get; set; }
        public decimal Balance          { get; set; }
        public int     CantidadIngresos { get; set; }
        public int     CantidadEgresos  { get; set; }

        public string TotalIngresosTexto => "$" + TotalIngresos.ToString("N2");
        public string TotalEgresosTexto  => "$" + TotalEgresos.ToString("N2");
        public string BalanceTexto       => "$" + Balance.ToString("N2");
        public bool   BalancePositivo    => Balance >= 0;
    }

    public class IngresosPorMes
    {
        public int     Mes       { get; set; }
        public string  MesNombre { get; set; }
        public decimal Ingresos  { get; set; }
        public decimal Egresos   { get; set; }
    }

    public class ResumenDocente
    {
        public long    InstructorId      { get; set; }
        public string  NombreCompleto    { get; set; }
        public byte[]  Foto              { get; set; }
        public decimal TarifaHora        { get; set; }
        public string  ActividadNombre   { get; set; }
        public int     DiasTrabajados    { get; set; }
        public decimal HorasTotales      { get; set; }
        public decimal SueldoEstimado    { get; set; }
        public decimal IngresosGenerados { get; set; }

        public string HorasTexto
        {
            get
            {
                int h = (int)HorasTotales;
                int m = (int)((HorasTotales - h) * 60);
                return h > 0 ? h + "h " + m.ToString("D2") + "m" : m + " min";
            }
        }
        public string SueldoTexto        => "$" + SueldoEstimado.ToString("N2");
        public string TarifaTexto        => "$" + TarifaHora.ToString("N2") + "/h";
        public string IngresosGenerTexto => "$" + IngresosGenerados.ToString("N2");
        public string DiasTrabajTexto    => DiasTrabajados + (DiasTrabajados == 1 ? " día" : " días");
    }

    public class SocioConDeuda
    {
        public long    SocioId         { get; set; }
        public string  NombreCompleto  { get; set; }
        public int?    NumeroSocio     { get; set; }
        public string  Telefono        { get; set; }
        public byte[]  Foto            { get; set; }
        public long    MembresiaId     { get; set; }
        public string  TipoPlan        { get; set; }
        public string  ActividadNombre { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public int     DiasVencida     { get; set; }
        public int     DiasParaVencer  { get; set; }
        public string  EstadoDeuda     { get; set; }

        public bool   EsVencida        => EstadoDeuda == "vencida";
        public string NumeroSocioTexto => NumeroSocio.HasValue ? "#" + NumeroSocio.Value.ToString("D4") : "—";
        public string VencimientoTexto => FechaVencimiento.ToString("dd/MM/yyyy");

        public string AlertaTexto
        {
            get
            {
                if (EsVencida)
                    return "Vencida hace " + DiasVencida + (DiasVencida == 1 ? " día" : " días");
                return "Vence en " + DiasParaVencer + (DiasParaVencer == 1 ? " día" : " días");
            }
        }
    }
}
```

---

## 6. DAO — `ReporteDao.cs`

```csharp
// Models/Dao/ReporteDao.cs — C# 7.3
using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class ReporteDao : ConnectionToDB
    {
        public List<MovimientoReporte> ObtenerMovimientos(
            DateTime? desde, DateTime? hasta, long? actividadId,
            string metodoPago, long? instructorId)
        {
            var lista = new List<MovimientoReporte>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ReporteIngresos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde",   (object)desde?.Date      ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta",   (object)hasta?.Date      ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ActividadId",  (object)actividadId      ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@MetodoPago",   (object)metodoPago       ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@InstructorId", (object)instructorId     ?? DBNull.Value);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            lista.Add(new MovimientoReporte
                            {
                                Id               = Convert.ToInt64(r["id"]),
                                Tipo             = r["tipo"].ToString(),
                                Subtipo          = r["subtipo"] as string,
                                Concepto         = r["concepto"].ToString(),
                                Monto            = Convert.ToDecimal(r["monto"]),
                                MetodoPago       = r["metodo_pago"] as string,
                                ReferenciaKipo   = r["referencia_tipo"] as string,
                                Fecha            = Convert.ToDateTime(r["fecha"]),
                                RegistradoPor    = r["registrado_por_nombre"] as string,
                                ActividadNombre  = r["actividad_nombre"] as string,
                                InstructorNombre = r["instructor_nombre"] as string
                            });
                }
            }
            return lista;
        }

        public (TotalesReporte totales,
                List<(string actividad, decimal total, int cantidad)> porActividad,
                List<(string metodo, decimal total, int cantidad)> porMetodo)
            ObtenerTotales(DateTime? desde, DateTime? hasta)
        {
            TotalesReporte totales = new TotalesReporte();
            var porActividad = new List<(string, decimal, int)>();
            var porMetodo    = new List<(string, decimal, int)>();

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ReporteTotales", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde?.Date ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta?.Date ?? DBNull.Value);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                            totales = new TotalesReporte
                            {
                                TotalIngresos    = Convert.ToDecimal(r["total_ingresos"]),
                                TotalEgresos     = Convert.ToDecimal(r["total_egresos"]),
                                Balance          = Convert.ToDecimal(r["balance"]),
                                CantidadIngresos = Convert.ToInt32(r["cantidad_ingresos"]),
                                CantidadEgresos  = Convert.ToInt32(r["cantidad_egresos"])
                            };

                        if (r.NextResult())
                            while (r.Read())
                                porActividad.Add((
                                    r["actividad"].ToString(),
                                    Convert.ToDecimal(r["total"]),
                                    Convert.ToInt32(r["cantidad"])));

                        if (r.NextResult())
                            while (r.Read())
                                porMetodo.Add((
                                    r["metodo_pago"].ToString(),
                                    Convert.ToDecimal(r["total"]),
                                    Convert.ToInt32(r["cantidad"])));
                    }
                }
            }
            return (totales, porActividad, porMetodo);
        }

        public List<IngresosPorMes> ObtenerGraficoPorMes(int anio)
        {
            var lista = new List<IngresosPorMes>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_GraficoIngresosPorMes", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Anio", anio);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            lista.Add(new IngresosPorMes
                            {
                                Mes       = Convert.ToInt32(r["mes"]),
                                MesNombre = r["mes_nombre"].ToString(),
                                Ingresos  = Convert.ToDecimal(r["ingresos"]),
                                Egresos   = Convert.ToDecimal(r["egresos"])
                            });
                }
            }
            return lista;
        }

        public List<ResumenDocente> ObtenerSueldosDocentes(DateTime? desde, DateTime? hasta)
        {
            var lista = new List<ResumenDocente>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ReporteSueldosDocentes", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde?.Date ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta?.Date ?? DBNull.Value);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            lista.Add(new ResumenDocente
                            {
                                InstructorId      = Convert.ToInt64(r["instructor_id"]),
                                NombreCompleto    = r["nombre_completo"].ToString(),
                                Foto              = r["foto"] != DBNull.Value ? (byte[])r["foto"] : null,
                                TarifaHora        = Convert.ToDecimal(r["tarifa_hora"]),
                                ActividadNombre   = r["actividad_nombre"].ToString(),
                                DiasTrabajados    = Convert.ToInt32(r["dias_trabajados"]),
                                HorasTotales      = Convert.ToDecimal(r["horas_totales"]),
                                SueldoEstimado    = Convert.ToDecimal(r["sueldo_estimado"]),
                                IngresosGenerados = Convert.ToDecimal(r["ingresos_generados"])
                            });
                }
            }
            return lista;
        }

        public (List<SocioConDeuda> vencidas, List<SocioConDeuda> proximas) ObtenerSociosDeuda(int diasProximos)
        {
            var vencidas = new List<SocioConDeuda>();
            var proximas = new List<SocioConDeuda>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ReporteSociosDeuda", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@DiasProximos", diasProximos);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read()) vencidas.Add(MapearDeuda(r));
                        if (r.NextResult())
                            while (r.Read()) proximas.Add(MapearDeuda(r));
                    }
                }
            }
            return (vencidas, proximas);
        }

        private static SocioConDeuda MapearDeuda(SqlDataReader r)
        {
            return new SocioConDeuda
            {
                SocioId         = Convert.ToInt64(r["socio_id"]),
                NombreCompleto  = r["nombre_completo"].ToString(),
                NumeroSocio     = r["numero_socio"] != DBNull.Value ? (int?)Convert.ToInt32(r["numero_socio"]) : null,
                Telefono        = r["telefono"] as string,
                Foto            = r["foto"] != DBNull.Value ? (byte[])r["foto"] : null,
                MembresiaId     = Convert.ToInt64(r["membresia_id"]),
                TipoPlan        = r["tipo_plan"].ToString(),
                ActividadNombre = r["actividad_nombre"].ToString(),
                FechaVencimiento = Convert.ToDateTime(r["fecha_vencimiento"]),
                EstadoDeuda     = r["estado_deuda"].ToString(),
                DiasVencida     = r["estado_deuda"].ToString() == "vencida"
                                    ? Convert.ToInt32(r["dias_vencida"]) : 0,
                DiasParaVencer  = r["estado_deuda"].ToString() == "proxima_a_vencer"
                                    ? Convert.ToInt32(r["dias_para_vencer"]) : 0
            };
        }
    }
}
```

---

## 7. CONTROLLER — `ReporteController.cs`

```csharp
// Controllers/ReporteController.cs — C# 7.3
using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class ReporteController
    {
        private readonly ReporteDao    _dao    = new ReporteDao();
        private readonly ActividadDao  _actDao = new ActividadDao();
        private readonly UsuarioDao    _usuDao = new UsuarioDao();

        public List<MovimientoReporte> ObtenerMovimientos(
            DateTime? desde, DateTime? hasta,
            long? actividadId = null, string metodoPago = null, long? instructorId = null)
        {
            try { return _dao.ObtenerMovimientos(desde, hasta, actividadId, metodoPago, instructorId); }
            catch (Exception ex) { throw new Exception("Error al cargar movimientos.\n" + ex.Message); }
        }

        public (TotalesReporte totales,
                List<(string actividad, decimal total, int cantidad)> porActividad,
                List<(string metodo, decimal total, int cantidad)> porMetodo)
            ObtenerTotales(DateTime? desde, DateTime? hasta)
        {
            try { return _dao.ObtenerTotales(desde, hasta); }
            catch { return (new TotalesReporte(), new List<(string,decimal,int)>(), new List<(string,decimal,int)>()); }
        }

        public List<IngresosPorMes> ObtenerGraficoPorMes(int anio)
        {
            try { return _dao.ObtenerGraficoPorMes(anio); }
            catch { return new List<IngresosPorMes>(); }
        }

        public List<ResumenDocente> ObtenerSueldosDocentes(DateTime? desde, DateTime? hasta)
        {
            try { return _dao.ObtenerSueldosDocentes(desde, hasta); }
            catch (Exception ex) { throw new Exception("Error al cargar sueldos.\n" + ex.Message); }
        }

        public (List<SocioConDeuda> vencidas, List<SocioConDeuda> proximas)
            ObtenerSociosDeuda(int diasProximos = 7)
        {
            try { return _dao.ObtenerSociosDeuda(diasProximos); }
            catch { return (new List<SocioConDeuda>(), new List<SocioConDeuda>()); }
        }

        public List<Actividad> ListarActividadesParaFiltro()
        {
            try { return _actDao.ObtenerActividades(); }
            catch { return new List<Actividad>(); }
        }

        public List<Usuario> ListarInstructoresParaFiltro()
        {
            try { return _usuDao.ObtenerUsuarios(); }
            catch { return new List<Usuario>(); }
        }
    }
}
```

---

## 8. EXPORTADORES

### `Helpers/ReportePdfExportador.cs`

```csharp
// SistemaGimnacionOptimusCAI/Helpers/ReportePdfExportador.cs — C# 7.3
// Requiere: iTextSharp 5.5.13.3
using Entities;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    public class ReportePdfExportador
    {
        // Datos del gimnasio que van en el encabezado
        private const string NOMBRE_GIM   = "OptimusCAI Gym";
        private const string DIRECCION    = "Av. Ejemplo 1234, Jujuy";
        private const string TELEFONO     = "+54 388 000-0000";

        // Colores corporativos
        private static readonly BaseColor ColorPrimario  = new BaseColor(0, 207, 255);    // cyan
        private static readonly BaseColor ColorSecundario= new BaseColor(167,139,250);   // violeta
        private static readonly BaseColor ColorFondo     = new BaseColor(18, 18, 30);
        private static readonly BaseColor ColorTexto     = new BaseColor(232,232,255);
        private static readonly BaseColor ColorGris      = new BaseColor(106,106,154);

        // ── REPORTE DE INGRESOS ───────────────────────────────────────────
        public string ExportarIngresos(
            List<MovimientoReporte> movimientos,
            TotalesReporte totales,
            DateTime desde, DateTime hasta)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "Reporte_Ingresos_" + desde.ToString("yyyyMM") + ".pdf");

            using (var doc = new Document(PageSize.A4, 36, 36, 60, 36))
            {
                PdfWriter.GetInstance(doc, new FileStream(path, FileMode.Create));
                doc.Open();

                AgregarEncabezado(doc, "REPORTE DE INGRESOS",
                    "Período: " + desde.ToString("dd/MM/yyyy") + " al " + hasta.ToString("dd/MM/yyyy"));

                // Panel de totales
                AgregarPanelTotales(doc, totales);

                // Tabla de movimientos
                doc.Add(new Paragraph("\n"));
                var tabla = new PdfPTable(5) { WidthPercentage = 100 };
                tabla.SetWidths(new float[] { 2, 4, 2, 2, 2 });

                AgregarFila(tabla, true, "FECHA", "CONCEPTO", "TIPO", "MÉTODO", "MONTO");
                foreach (var m in movimientos)
                    AgregarFila(tabla, false,
                        m.FechaTexto, m.Concepto,
                        m.Tipo.ToUpper(), m.MetodoPago ?? "—", m.MontoTexto);

                doc.Add(tabla);
                doc.Close();
            }
            return path;
        }

        // ── REPORTE DE SUELDOS ────────────────────────────────────────────
        public string ExportarSueldos(
            List<ResumenDocente> docentes,
            DateTime desde, DateTime hasta)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "Reporte_Sueldos_" + desde.ToString("yyyyMM") + ".pdf");

            using (var doc = new Document(PageSize.A4, 36, 36, 60, 36))
            {
                PdfWriter.GetInstance(doc, new FileStream(path, FileMode.Create));
                doc.Open();

                AgregarEncabezado(doc, "LIQUIDACIÓN DE SUELDOS",
                    "Período: " + desde.ToString("dd/MM/yyyy") + " al " + hasta.ToString("dd/MM/yyyy"));

                var tabla = new PdfPTable(6) { WidthPercentage = 100 };
                tabla.SetWidths(new float[] { 3, 2, 2, 2, 2, 2 });

                AgregarFila(tabla, true, "INSTRUCTOR", "ACTIVIDAD",
                    "DÍAS", "HORAS", "TARIFA", "SUELDO");

                decimal totalSueldos = 0;
                foreach (var d in docentes)
                {
                    AgregarFila(tabla, false,
                        d.NombreCompleto, d.ActividadNombre,
                        d.DiasTrabajTexto, d.HorasTexto,
                        d.TarifaTexto, d.SueldoTexto);
                    totalSueldos += d.SueldoEstimado;
                }

                // Fila de total
                var celdaTotal = new PdfPCell(new Phrase("TOTAL A PAGAR"))
                { Colspan = 5, HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 8 };
                tabla.AddCell(celdaTotal);
                tabla.AddCell(new PdfPCell(new Phrase("$" + totalSueldos.ToString("N2")))
                { HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 8 });

                doc.Add(tabla);
                doc.Close();
            }
            return path;
        }

        // ── HELPERS INTERNOS ─────────────────────────────────────────────
        private void AgregarEncabezado(Document doc, string titulo, string subtitulo)
        {
            var fontTitulo   = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, ColorPrimario);
            var fontSub      = FontFactory.GetFont(FontFactory.HELVETICA, 10, ColorGris);
            var fontGim      = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, ColorTexto);
            var fontDatos    = FontFactory.GetFont(FontFactory.HELVETICA, 9, ColorGris);

            doc.Add(new Paragraph(NOMBRE_GIM, fontGim));
            doc.Add(new Paragraph(DIRECCION + "  |  " + TELEFONO, fontDatos));
            doc.Add(new Paragraph(" "));
            doc.Add(new Paragraph(titulo, fontTitulo));
            doc.Add(new Paragraph(subtitulo, fontSub));
            doc.Add(new Paragraph("Generado el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"), fontDatos));

            var linea = new LineSeparator(1f, 100f, ColorSecundario, Element.ALIGN_CENTER, -2);
            doc.Add(new Chunk(linea));
            doc.Add(new Paragraph(" "));
        }

        private void AgregarPanelTotales(Document doc, TotalesReporte t)
        {
            var tabla = new PdfPTable(3) { WidthPercentage = 100 };
            var fontLbl = FontFactory.GetFont(FontFactory.HELVETICA, 9, ColorGris);
            var fontVal = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, ColorPrimario);

            AgregarCeldaTotal(tabla, "INGRESOS TOTALES", t.TotalIngresosTexto, fontLbl, fontVal);
            AgregarCeldaTotal(tabla, "EGRESOS TOTALES",  t.TotalEgresosTexto,  fontLbl, fontVal);
            AgregarCeldaTotal(tabla, "BALANCE",          t.BalanceTexto,        fontLbl, fontVal);
            doc.Add(tabla);
        }

        private void AgregarCeldaTotal(PdfPTable t, string label, string valor,
                                        Font fontL, Font fontV)
        {
            var cell = new PdfPCell();
            cell.AddElement(new Paragraph(label, fontL));
            cell.AddElement(new Paragraph(valor, fontV));
            cell.Padding = 12;
            t.AddCell(cell);
        }

        private void AgregarFila(PdfPTable tabla, bool esHeader, params string[] valores)
        {
            var font = esHeader
                ? FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BaseColor.WHITE)
                : FontFactory.GetFont(FontFactory.HELVETICA, 9, ColorTexto);

            foreach (var val in valores)
            {
                var cell = new PdfPCell(new Phrase(val, font))
                {
                    Padding           = 6,
                    BackgroundColor   = esHeader ? ColorFondo : BaseColor.WHITE,
                    BorderColor       = new BaseColor(37, 37, 64)
                };
                tabla.AddCell(cell);
            }
        }
    }
}
```

### `Helpers/ReporteExcelExportador.cs`

```csharp
// SistemaGimnacionOptimusCAI/Helpers/ReporteExcelExportador.cs — C# 7.3
// Requiere: ClosedXML 0.95.4
using ClosedXML.Excel;
using Entities;
using System;
using System.Collections.Generic;
using System.IO;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    public class ReporteExcelExportador
    {
        public string ExportarIngresos(
            List<MovimientoReporte> movimientos,
            TotalesReporte totales,
            DateTime desde, DateTime hasta)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "Reporte_Ingresos_" + desde.ToString("yyyyMM") + ".xlsx");

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Ingresos");

                // Encabezado
                ws.Cell("A1").Value = "OptimusCAI Gym — Reporte de Ingresos";
                ws.Cell("A2").Value = "Período: " + desde.ToString("dd/MM/yyyy") +
                                      " al " + hasta.ToString("dd/MM/yyyy");
                ws.Cell("A3").Value = "Generado: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");

                // Totales
                ws.Cell("A5").Value = "TOTAL INGRESOS"; ws.Cell("B5").Value = totales.TotalIngresos;
                ws.Cell("A6").Value = "TOTAL EGRESOS";  ws.Cell("B6").Value = totales.TotalEgresos;
                ws.Cell("A7").Value = "BALANCE";        ws.Cell("B7").Value = totales.Balance;
                ws.Range("B5:B7").Style.NumberFormat.Format = "$#,##0.00";

                // Tabla de movimientos
                int fila = 9;
                ws.Cell(fila, 1).Value = "FECHA";
                ws.Cell(fila, 2).Value = "CONCEPTO";
                ws.Cell(fila, 3).Value = "TIPO";
                ws.Cell(fila, 4).Value = "ACTIVIDAD";
                ws.Cell(fila, 5).Value = "INSTRUCTOR";
                ws.Cell(fila, 6).Value = "MÉTODO";
                ws.Cell(fila, 7).Value = "MONTO";
                ws.Range(fila, 1, fila, 7).Style.Font.Bold = true;

                foreach (var m in movimientos)
                {
                    fila++;
                    ws.Cell(fila, 1).Value = m.Fecha.ToString("dd/MM/yyyy");
                    ws.Cell(fila, 2).Value = m.Concepto;
                    ws.Cell(fila, 3).Value = m.Tipo;
                    ws.Cell(fila, 4).Value = m.ActividadNombre;
                    ws.Cell(fila, 5).Value = m.InstructorNombre;
                    ws.Cell(fila, 6).Value = m.MetodoPago;
                    ws.Cell(fila, 7).Value = m.Monto;
                    ws.Cell(fila, 7).Style.NumberFormat.Format = "$#,##0.00";
                }

                ws.Columns().AdjustToContents();
                wb.SaveAs(path);
            }
            return path;
        }

        public string ExportarSueldos(List<ResumenDocente> docentes, DateTime desde, DateTime hasta)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "Sueldos_" + desde.ToString("yyyyMM") + ".xlsx");

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Sueldos");
                ws.Cell("A1").Value = "OptimusCAI Gym — Liquidación de Sueldos";
                ws.Cell("A2").Value = "Período: " + desde.ToString("dd/MM/yyyy") +
                                      " al " + hasta.ToString("dd/MM/yyyy");

                int fila = 4;
                ws.Cell(fila, 1).Value = "INSTRUCTOR";
                ws.Cell(fila, 2).Value = "ACTIVIDAD";
                ws.Cell(fila, 3).Value = "DÍAS";
                ws.Cell(fila, 4).Value = "HORAS TOTALES";
                ws.Cell(fila, 5).Value = "TARIFA/HORA";
                ws.Cell(fila, 6).Value = "SUELDO ESTIMADO";
                ws.Cell(fila, 7).Value = "INGRESOS GENERADOS";
                ws.Range(fila, 1, fila, 7).Style.Font.Bold = true;

                decimal totalSueldos = 0;
                foreach (var d in docentes)
                {
                    fila++;
                    ws.Cell(fila, 1).Value = d.NombreCompleto;
                    ws.Cell(fila, 2).Value = d.ActividadNombre;
                    ws.Cell(fila, 3).Value = d.DiasTrabajados;
                    ws.Cell(fila, 4).Value = (double)d.HorasTotales;
                    ws.Cell(fila, 5).Value = (double)d.TarifaHora;
                    ws.Cell(fila, 6).Value = (double)d.SueldoEstimado;
                    ws.Cell(fila, 7).Value = (double)d.IngresosGenerados;
                    ws.Range(fila, 5, fila, 7).Style.NumberFormat.Format = "$#,##0.00";
                    totalSueldos += d.SueldoEstimado;
                }

                fila++;
                ws.Cell(fila, 5).Value = "TOTAL";
                ws.Cell(fila, 6).Value = (double)totalSueldos;
                ws.Cell(fila, 6).Style.NumberFormat.Format = "$#,##0.00";
                ws.Cell(fila, 5).Style.Font.Bold = true;
                ws.Cell(fila, 6).Style.Font.Bold = true;

                ws.Columns().AdjustToContents();
                wb.SaveAs(path);
            }
            return path;
        }
    }
}
```

---

## 9. UI — `ReportesPage.xaml` — estructura de tabs

La página tiene 4 tabs internos usando `TabControl` o `Border` + `StackPanel` según el estilo del proyecto.

**Tab 1 — Ingresos y Ganancias (solo admin):**
- `DatePicker` desde / `DatePicker` hasta
- `ComboBox` actividad (todas / por actividad)
- `ComboBox` método de pago (todos / efectivo / tarjeta / transferencia)
- `ComboBox` instructor (todos / por instructor)
- `DataGrid` con columnas: Fecha, Concepto, Tipo, Actividad, Instructor, Método, Monto
- Panel de totales: 3 cards con Total Ingresos (cyan) / Total Egresos (rojo) / Balance (verde o rojo)
- Gráfico de barras OxyPlot con ingresos por mes del año en curso
- Botones: `📄 EXPORTAR PDF` | `📊 EXPORTAR EXCEL`

**Tab 2 — Sueldos Docentes (solo admin):**
- `DatePicker` desde / `DatePicker` hasta (default: primer día del mes / hoy)
- `DataGrid` con columnas: Foto, Instructor, Actividad, Días, Horas, Tarifa/h, Sueldo estimado, Ingresos generados
- Fila de totales al pie: suma de sueldos estimados
- Botones: `📄 EXPORTAR PDF` | `📊 EXPORTAR EXCEL`

**Tab 3 — Socios con Deuda (solo admin):**
- Dos secciones: "MEMBRESÍAS VENCIDAS" (rojo) y "PRÓXIMAS A VENCER" (naranja)
- Cada socio muestra: foto, nombre, número socio, actividad, fecha vencimiento, días vencida/para vencer
- Botón por socio: `💬 AVISAR POR WHATSAPP` → abre wa.me con mensaje de alerta
- Botón global: `📄 EXPORTAR PDF`

**Tab 4 — Mis Ventas (visible para todos):**
- Solo muestra ventas del empleado logueado (`SesionManager.UsuarioId`) del día de hoy
- `DataGrid`: hora, cantidad de ítems, total, método de pago
- Card de resumen: "Hoy vendiste X veces por un total de $Y"

---

## 10. REQUISITOS NO FUNCIONALES

| # | Requisito | Detalle |
|---|-----------|---------|
| RNF-01 | **Rendimiento** | Los reportes con rango > 1 mes deben cargar en < 3 segundos. Si hay más de 1000 filas, mostrar las primeras 500 y avisar |
| RNF-02 | **Seguridad** | Los tabs de admin se ocultan en la UI Y se bloquean en el Controller con `if (!SesionManager.EsAdmin) return` |
| RNF-03 | **Formato PDF** | A4 vertical, márgenes de 36pt, fuente mínima 9pt, logo en encabezado (imagen del proyecto) |
| RNF-04 | **Formato Excel** | Columnas ajustadas automáticamente, celdas de moneda con formato `$#,##0.00`, primera fila en negrita |
| RNF-05 | **Encabezado PDF** | Nombre del gimnasio + dirección + teléfono. Estas constantes van en `ReportePdfExportador.cs` para fácil edición |
| RNF-06 | **Exportación** | Al exportar, el archivo se guarda en `Path.GetTempPath()` y se abre automáticamente con `Process.Start` |
| RNF-07 | **Gráfico** | OxyPlot con fondo oscuro (`#12121E`), barras en degradado cyan→violeta. Si OxyPlot da conflictos de versión, usar Canvas WPF nativo con rectángulos dibujados proporcionalmente |
| RNF-08 | **Usabilidad** | Los DatePicker defaul al primer día del mes actual (desde) y hoy (hasta). El rango se puede cambiar libremente |

---

## 11. ORDEN DE IMPLEMENTACIÓN

| # | Tarea | Tiempo estimado |
|---|-------|----------------|
| 1 | Instalar NuGet: iTextSharp + ClosedXML + OxyPlot.Wpf | 5 min |
| 2 | Ejecutar los 6 SPs en SQL Server | 10 min |
| 3 | Crear `ReporteFinanciero.cs` en Entities | 15 min |
| 4 | Crear `ReporteDao.cs` | 30 min |
| 5 | Crear `ReporteController.cs` | 15 min |
| 6 | Crear `ReportePdfExportador.cs` | 45 min |
| 7 | Crear `ReporteExcelExportador.cs` | 30 min |
| 8 | Crear `ReportesPage.xaml` + `.cs` con los 4 tabs | 3 h |
| 9 | Agregar "Reportes" al menú en `MainWindow.xaml.cs` (solo admin) | 5 min |

---

*SDD Módulo Reportes — OptimusCAI Gym v1.0 — Mayo 2026*
*Basado en entrevista con el propietario del gimnasio*
