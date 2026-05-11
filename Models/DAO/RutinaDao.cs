// Models/Dao/RutinaDao.cs — C# 7.3
using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class RutinaDao : ConnectionToDB
    {
        // ── MAPEOS ────────────────────────────────────────────
        private static Rutina MapearRutina(SqlDataReader r, bool conContadores)
        {
            var rt = new Rutina
            {
                Id = Convert.ToInt64(r["id"]),
                Nombre = r["nombre"].ToString(),
                Detalles = r["detalles"] as string,
                DuracionSemanas = Convert.ToByte(r["duracion_semanas"]),
                CreadoPor = Convert.ToInt64(r["creado_por"]),
                Activo = Convert.ToBoolean(r["activo"]),
                CreadoEn = Convert.ToDateTime(r["creado_en"]),
                ActualizadoEn = Convert.ToDateTime(r["actualizado_en"]),
                CreadorNombre = r["creador_nombre"] as string
            };
            if (conContadores)
            {
                rt.TotalBloques = Convert.ToInt32(r["total_bloques"]);
                rt.TotalEjercicios = Convert.ToInt32(r["total_ejercicios"]);
                rt.TotalAsignaciones = Convert.ToInt32(r["total_asignaciones"]);
            }
            return rt;
        }

        private static RutinaBloque MapearBloque(SqlDataReader r)
        {
            return new RutinaBloque
            {
                Id = Convert.ToInt64(r["id"]),
                RutinaId = Convert.ToInt64(r["rutina_id"]),
                Nombre = r["nombre"].ToString(),
                Orden = Convert.ToByte(r["orden"])
            };
        }

        private static RutinaEjercicio MapearEjercicio(SqlDataReader r)
        {
            return new RutinaEjercicio
            {
                Id = Convert.ToInt64(r["id"]),
                BloqueId = Convert.ToInt64(r["bloque_id"]),
                Nombre = r["nombre"].ToString(),
                Series = r["series"] != DBNull.Value ? (byte?)Convert.ToByte(r["series"]) : null,
                Repeticiones = r["repeticiones"] as string,
                Peso = r["peso"] as string,
                DescansoSeg = r["descanso_seg"] != DBNull.Value ? (short?)Convert.ToInt16(r["descanso_seg"]) : null,
                Notas = r["notas"] as string,
                LinkVideo = r["link_video"] as string,
                Orden = Convert.ToByte(r["orden"])
            };
        }

        // ── RUTINAS ───────────────────────────────────────────
        public List<Rutina> ObtenerRutinas()
        {
            var lista = new List<Rutina>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerRutinas", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(MapearRutina(r, true));
                }
            }
            return lista;
        }

        public List<Rutina> BuscarRutinas(string texto, bool soloActivas)
        {
            var lista = new List<Rutina>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarRutinas", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@SoloActivas", soloActivas);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(MapearRutina(r, true));
                }
            }
            return lista;
        }

        /// <summary>Carga la rutina con todos sus bloques y ejercicios.</summary>
        public Rutina ObtenerConDetalle(long id)
        {
            Rutina rutina = null;
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerRutinaConDetalle", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        // 1) Rutina
                        if (r.Read()) rutina = MapearRutina(r, false);
                        if (rutina == null) return null;

                        // 2) Bloques
                        if (r.NextResult())
                            while (r.Read()) rutina.Bloques.Add(MapearBloque(r));

                        // 3) Ejercicios → asignar a su bloque
                        if (r.NextResult())
                        {
                            while (r.Read())
                            {
                                var ej = MapearEjercicio(r);
                                foreach (var b in rutina.Bloques)
                                    if (b.Id == ej.BloqueId) { b.Ejercicios.Add(ej); break; }
                            }
                        }
                    }
                }
            }
            return rutina;
        }

        public long InsertarRutina(string nombre, string detalles, byte duracionSemanas, long creadoPor)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_InsertarRutina", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Nombre", nombre);
                    cmd.Parameters.AddWithValue("@Detalles", (object)detalles ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DuracionSemanas", duracionSemanas);
                    cmd.Parameters.AddWithValue("@CreadoPor", creadoPor);
                    var res = cmd.ExecuteScalar();
                    return res != null ? Convert.ToInt64(res) : 0;
                }
            }
        }

        public bool ModificarRutina(long id, string nombre, string detalles, byte duracionSemanas)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ModificarRutina", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Nombre", nombre);
                    cmd.Parameters.AddWithValue("@Detalles", (object)detalles ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DuracionSemanas", duracionSemanas);
                    var f = cmd.ExecuteScalar();
                    return f != null && Convert.ToInt32(f) > 0;
                }
            }
        }

        public bool EliminarRutina(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarRutina", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var res = cmd.ExecuteScalar();
                    return res != null && Convert.ToInt32(res) == 1;
                }
            }
        }

        public bool CambiarEstado(long id, bool activo)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CambiarEstadoRutina", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Activo", activo);
                    var f = cmd.ExecuteScalar();
                    return f != null && Convert.ToInt32(f) > 0;
                }
            }
        }

        // ── BLOQUES ───────────────────────────────────────────
        public long InsertarBloque(long rutinaId, string nombre, byte orden)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_InsertarBloque", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@RutinaId", rutinaId);
                    cmd.Parameters.AddWithValue("@Nombre", nombre);
                    cmd.Parameters.AddWithValue("@Orden", orden);
                    var res = cmd.ExecuteScalar();
                    return res != null ? Convert.ToInt64(res) : 0;
                }
            }
        }

        public bool ModificarBloque(long id, string nombre, byte orden)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ModificarBloque", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Nombre", nombre);
                    cmd.Parameters.AddWithValue("@Orden", orden);
                    var f = cmd.ExecuteScalar();
                    return f != null && Convert.ToInt32(f) > 0;
                }
            }
        }

        public bool EliminarBloque(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarBloque", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var f = cmd.ExecuteScalar();
                    return f != null && Convert.ToInt32(f) > 0;
                }
            }
        }

        // ── EJERCICIOS ────────────────────────────────────────
        public long InsertarEjercicio(RutinaEjercicio e)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_InsertarEjercicio", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@BloqueId", e.BloqueId);
                    cmd.Parameters.AddWithValue("@Nombre", e.Nombre);
                    cmd.Parameters.AddWithValue("@Series", (object)e.Series ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Repeticiones", (object)e.Repeticiones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Peso", (object)e.Peso ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DescansoSeg", (object)e.DescansoSeg ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Notas", (object)e.Notas ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@LinkVideo", (object)e.LinkVideo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Orden", e.Orden);
                    var res = cmd.ExecuteScalar();
                    return res != null ? Convert.ToInt64(res) : 0;
                }
            }
        }

        public bool ModificarEjercicio(RutinaEjercicio e)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ModificarEjercicio", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", e.Id);
                    cmd.Parameters.AddWithValue("@Nombre", e.Nombre);
                    cmd.Parameters.AddWithValue("@Series", (object)e.Series ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Repeticiones", (object)e.Repeticiones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Peso", (object)e.Peso ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DescansoSeg", (object)e.DescansoSeg ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Notas", (object)e.Notas ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@LinkVideo", (object)e.LinkVideo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Orden", e.Orden);
                    var f = cmd.ExecuteScalar();
                    return f != null && Convert.ToInt32(f) > 0;
                }
            }
        }

        public bool EliminarEjercicio(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarEjercicio", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var f = cmd.ExecuteScalar();
                    return f != null && Convert.ToInt32(f) > 0;
                }
            }
        }

        // ── ASIGNACIONES ──────────────────────────────────────
        public long AsignarRutina(long rutinaId, long socioId, long asignadoPor)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_AsignarRutina", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@RutinaId", rutinaId);
                    cmd.Parameters.AddWithValue("@SocioId", socioId);
                    cmd.Parameters.AddWithValue("@AsignadoPor", asignadoPor);
                    var res = cmd.ExecuteScalar();
                    return res != null ? Convert.ToInt64(res) : 0;
                }
            }
        }

        public bool DesasignarRutina(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_DesasignarRutina", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var f = cmd.ExecuteScalar();
                    return f != null && Convert.ToInt32(f) > 0;
                }
            }
        }

        public List<RutinaAsignacion> AsignacionesDeRutina(long rutinaId)
        {
            var lista = new List<RutinaAsignacion>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_AsignacionesDeRutina", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@RutinaId", rutinaId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            lista.Add(new RutinaAsignacion
                            {
                                Id = Convert.ToInt64(r["id"]),
                                RutinaId = Convert.ToInt64(r["rutina_id"]),
                                SocioId = Convert.ToInt64(r["socio_id"]),
                                AsignadoPor = Convert.ToInt64(r["asignado_por"]),
                                EnviadoWp = Convert.ToBoolean(r["enviado_wp"]),
                                AsignadoEn = Convert.ToDateTime(r["asignado_en"]),
                                SocioNombre = r["socio_nombre"] as string,
                                NumeroSocio = r["numero_socio"] != DBNull.Value ? (int?)Convert.ToInt32(r["numero_socio"]) : null,
                                SocioFoto = r["socio_foto"] != DBNull.Value ? (byte[])r["socio_foto"] : null,
                                AsignadoPorNombre = r["asignado_por_nombre"] as string
                            });
                }
            }
            return lista;
        }

        // ── ESTADÍSTICAS ──────────────────────────────────────
        public EstadisticasRutinas ObtenerEstadisticas()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasRutinas", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                            return new EstadisticasRutinas
                            {
                                Total = Convert.ToInt32(r["total"]),
                                Activas = Convert.ToInt32(r["activas"]),
                                TotalEjercicios = Convert.ToInt32(r["total_ejercicios"]),
                                TotalAsignaciones = Convert.ToInt32(r["total_asignaciones"]),
                                SociosAsignados = Convert.ToInt32(r["socios_asignados"])
                            };
                }
            }
            return new EstadisticasRutinas();
        }
    }
}