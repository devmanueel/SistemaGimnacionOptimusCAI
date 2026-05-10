// Models/Dao/AuditoriaDao.cs — C# 7.3
using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class AuditoriaDao : ConnectionToDB
    {
        private static AuditoriaEntry Mapear(SqlDataReader r)
        {
            return new AuditoriaEntry
            {
                Id = Convert.ToInt64(r["id"]),
                ActorId = Convert.ToInt64(r["actor_id"]),
                Accion = r["accion"].ToString(),
                Entidad = r["entidad"].ToString(),
                EntidadId = r["entidad_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["entidad_id"]) : null,
                Detalle = r["detalle"] as string,
                CreadoEn = Convert.ToDateTime(r["creado_en"]),
                ActorNombre = r["actor_nombre"] as string,
                ActorFoto = r["actor_foto"] != DBNull.Value ? (byte[])r["actor_foto"] : null,
                ActorRol = r["actor_rol"] as string
            };
        }

        public long Registrar(long actorId, string accion, string entidad,
                              long? entidadId, string detalleJson)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_RegistrarAuditoria", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ActorId", actorId);
                    cmd.Parameters.AddWithValue("@Accion", accion);
                    cmd.Parameters.AddWithValue("@Entidad", entidad);
                    cmd.Parameters.AddWithValue("@EntidadId", (object)entidadId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Detalle", (object)detalleJson ?? DBNull.Value);
                    var res = cmd.ExecuteScalar();
                    return res != null ? Convert.ToInt64(res) : 0;
                }
            }
        }

        public List<AuditoriaEntry> Obtener(DateTime? desde, DateTime? hasta)
        {
            var lista = new List<AuditoriaEntry>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerAuditoria", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta ?? DBNull.Value);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(Mapear(r));
                }
            }
            return lista;
        }

        public AuditoriaEntry ObtenerPorId(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerAuditoriaPorId", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read()) return Mapear(r);
                }
            }
            return null;
        }

        public List<AuditoriaEntry> Buscar(string texto, long? actorId, string entidad,
                                            string accion, DateTime? desde, DateTime? hasta)
        {
            var lista = new List<AuditoriaEntry>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarAuditoria", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@ActorId", (object)actorId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Entidad", string.IsNullOrEmpty(entidad) ? (object)DBNull.Value : entidad);
                    cmd.Parameters.AddWithValue("@Accion", string.IsNullOrEmpty(accion) ? (object)DBNull.Value : accion);
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta ?? DBNull.Value);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(Mapear(r));
                }
            }
            return lista;
        }

        public EstadisticasAuditoria ObtenerEstadisticas()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasAuditoria", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                            return new EstadisticasAuditoria
                            {
                                Total = Convert.ToInt32(r["total"]),
                                Hoy = Convert.ToInt32(r["hoy"]),
                                Mes = Convert.ToInt32(r["mes"]),
                                UsuariosActivosMes = Convert.ToInt32(r["usuarios_activos_mes"])
                            };
                }
            }
            return new EstadisticasAuditoria();
        }

        public List<TopUsuarioAuditoria> ObtenerTopUsuarios()
        {
            var lista = new List<TopUsuarioAuditoria>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_TopUsuariosAuditoria", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            lista.Add(new TopUsuarioAuditoria
                            {
                                ActorId = r["actor_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["actor_id"]) : null,
                                Nombre = r["nombre"] as string,
                                Foto = r["foto"] != DBNull.Value ? (byte[])r["foto"] : null,
                                Acciones = Convert.ToInt32(r["acciones"])
                            });
                }
            }
            return lista;
        }
    }
}