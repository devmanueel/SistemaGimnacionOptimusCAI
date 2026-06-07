// Models/DAO/ReporteDao.cs — C# 7.3
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

        public (List<SocioConDeuda> vencidas, List<SocioConDeuda> proximas)
            ObtenerSociosDeuda(int diasProximos)
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

        public (List<VentaEmpleado> ventas, decimal totalDia, int cantidadVentas)
            ObtenerMisVentasDelDia(long empleadoId)
        {
            var ventas = new List<VentaEmpleado>();
            decimal totalDia = 0;
            int cantidadVentas = 0;
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_MisVentasDelDia", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@EmpleadoId", empleadoId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            ventas.Add(new VentaEmpleado
                            {
                                Id            = Convert.ToInt64(r["id"]),
                                Total         = Convert.ToDecimal(r["total"]),
                                MetodoPago    = r["metodo_pago"] as string,
                                CreadoEn      = Convert.ToDateTime(r["creado_en"]),
                                CantidadItems = Convert.ToInt32(r["cantidad_items"])
                            });

                        if (r.NextResult() && r.Read())
                        {
                            totalDia       = Convert.ToDecimal(r["total_dia"]);
                            cantidadVentas = Convert.ToInt32(r["cantidad_ventas"]);
                        }
                    }
                }
            }
            return (ventas, totalDia, cantidadVentas);
        }

        // ── Actualizar tarifa por hora de un instructor (SDD_Fix_Reportes) ──
        public bool ActualizarTarifaInstructor(long instructorId, decimal tarifaHora)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ActualizarTarifaInstructor", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@InstructorId", instructorId);
                    cmd.Parameters.AddWithValue("@TarifaHora",   tarifaHora);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        private static SocioConDeuda MapearDeuda(SqlDataReader r)
        {
            return new SocioConDeuda
            {
                SocioId          = Convert.ToInt64(r["socio_id"]),
                NombreCompleto   = r["nombre_completo"].ToString(),
                NumeroSocio      = r["numero_socio"] != DBNull.Value ? (int?)Convert.ToInt32(r["numero_socio"]) : null,
                Telefono         = r["telefono"] as string,
                Foto             = r["foto"] != DBNull.Value ? (byte[])r["foto"] : null,
                MembresiaId      = Convert.ToInt64(r["membresia_id"]),
                TipoPlan         = r["tipo_plan"].ToString(),
                ActividadNombre  = r["actividad_nombre"].ToString(),
                FechaVencimiento = Convert.ToDateTime(r["fecha_vencimiento"]),
                EstadoDeuda      = r["estado_deuda"].ToString(),
                DiasVencida      = r["estado_deuda"].ToString() == "vencida"
                                     ? Convert.ToInt32(r["dias_vencida"]) : 0,
                DiasParaVencer   = r["estado_deuda"].ToString() == "proxima_a_vencer"
                                     ? Convert.ToInt32(r["dias_para_vencer"]) : 0
            };
        }
    }
}
