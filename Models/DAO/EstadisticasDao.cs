// Models/DAO/EstadisticasDao.cs — C# 7.3
using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class EstadisticasDao : ConnectionToDB
    {
        public EstadisticasKPI ObtenerKPIs()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasKPIs", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            return new EstadisticasKPI
                            {
                                SociosActivos = Convert.ToInt32(r["socios_activos"]),
                                MembresiasPorVencer = Convert.ToInt32(r["membresias_por_vencer"]),
                                ProductoMasVendido = r["producto_mas_vendido"] as string,
                                ProductoMasVendidoQty = r["producto_mas_vendido_qty"] != DBNull.Value
                                    ? Convert.ToInt32(r["producto_mas_vendido_qty"]) : 0,
                                NuevosSociosMes = Convert.ToInt32(r["nuevos_socios_mes"]),
                                IngresosMes = Convert.ToDecimal(r["ingresos_mes"]),
                                EgresosMes = Convert.ToDecimal(r["egresos_mes"])
                            };
                        }
                    }
                }
            }
            return new EstadisticasKPI();
        }

        public (decimal totalIngresos, decimal totalEgresos,
                List<PuntoDiario> ingresos, List<PuntoDiario> egresos)
            ObtenerCajaConEgresos(DateTime desde, DateTime hasta)
        {
            decimal totalI = 0, totalE = 0;
            var ingresos = new List<PuntoDiario>();
            var egresos = new List<PuntoDiario>();

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasCajaResumen", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", desde.Date);
                    cmd.Parameters.AddWithValue("@FechaHasta", hasta.Date);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            totalI = Convert.ToDecimal(r["total_ingresos"]);
                            totalE = Convert.ToDecimal(r["total_egresos"]);
                        }

                        if (r.NextResult())
                        {
                            while (r.Read())
                            {
                                ingresos.Add(new PuntoDiario
                                {
                                    Fecha = Convert.ToDateTime(r["fecha"]),
                                    Monto = Convert.ToDecimal(r["monto"])
                                });
                            }
                        }

                        if (r.NextResult())
                        {
                            while (r.Read())
                            {
                                egresos.Add(new PuntoDiario
                                {
                                    Fecha = Convert.ToDateTime(r["fecha"]),
                                    Monto = Convert.ToDecimal(r["monto"])
                                });
                            }
                        }
                    }
                }
            }
            return (totalI, totalE, ingresos, egresos);
        }

        public List<AsistenciaPorHora> ObtenerAsistenciasPorHora(DateTime desde, DateTime hasta, int? actividadId)
        {
            var lista = new List<AsistenciaPorHora>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasAsistenciasPorHora", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", desde.Date);
                    cmd.Parameters.AddWithValue("@FechaHasta", hasta.Date);
                    cmd.Parameters.AddWithValue("@ActividadId",
                        actividadId.HasValue ? (object)actividadId.Value : DBNull.Value);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            lista.Add(new AsistenciaPorHora
                            {
                                Hora = Convert.ToInt32(r["hora"]),
                                Promedio = Convert.ToDecimal(r["promedio"]),
                                Porcentaje = Convert.ToDecimal(r["porcentaje"])
                            });
                        }
                    }
                }
            }
            return lista;
        }

        public List<AsistenciaPorActividad> ObtenerAsistenciasPorActividad(DateTime desde, DateTime hasta)
        {
            var lista = new List<AsistenciaPorActividad>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasAsistenciasPorActividad", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", desde.Date);
                    cmd.Parameters.AddWithValue("@FechaHasta", hasta.Date);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            lista.Add(new AsistenciaPorActividad
                            {
                                Actividad = r["actividad"].ToString(),
                                Cantidad = Convert.ToInt32(r["cantidad"])
                            });
                        }
                    }
                }
            }
            return lista;
        }

        public List<NuevoSocioPorDia> ObtenerNuevosSocios(DateTime desde, DateTime hasta)
        {
            var lista = new List<NuevoSocioPorDia>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasNuevosSocios", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", desde.Date);
                    cmd.Parameters.AddWithValue("@FechaHasta", hasta.Date);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            lista.Add(new NuevoSocioPorDia
                            {
                                Fecha = Convert.ToDateTime(r["fecha"]),
                                Cantidad = Convert.ToInt32(r["cantidad"])
                            });
                        }
                    }
                }
            }
            return lista;
        }

        public List<SocioTop> ObtenerTop5Socios(DateTime desde, DateTime hasta)
        {
            var lista = new List<SocioTop>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasTop5Socios", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", desde.Date);
                    cmd.Parameters.AddWithValue("@FechaHasta", hasta.Date);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            lista.Add(new SocioTop
                            {
                                NombreCompleto = r["nombre_completo"].ToString(),
                                TotalAsistencias = Convert.ToInt32(r["total_asistencias"])
                            });
                        }
                    }
                }
            }
            return lista;
        }

        public List<(int Id, string Nombre)> ObtenerActividades()
        {
            var lista = new List<(int, string)>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasActividades", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            lista.Add((Convert.ToInt32(r["id"]), r["nombre"].ToString()));
                    }
                }
            }
            return lista;
        }

        public int ObtenerSociosInactivos(int diasInactivo)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasSociosInactivos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@DiasInactivo", diasInactivo);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                            return Convert.ToInt32(r["cantidad"]);
                    }
                }
            }
            return 0;
        }
    }
}
