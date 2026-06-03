// Models/Dao/WhatsappDao.cs — C# 7.3
using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class WhatsappDao : ConnectionToDB
    {
        private static WhatsappMensaje Mapear(SqlDataReader r)
        {
            return new WhatsappMensaje
            {
                Id = Convert.ToInt64(r["id"]),
                Tipo = r["tipo"].ToString(),
                Disparador = r["disparador"] as string,
                SocioId = r["socio_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["socio_id"]) : null,
                Telefono = r["telefono"].ToString(),
                Mensaje = r["mensaje"].ToString(),
                Estado = r["estado"].ToString(),
                EnviadoPor = r["enviado_por"] != DBNull.Value ? (long?)Convert.ToInt64(r["enviado_por"]) : null,
                CreadoEn = Convert.ToDateTime(r["creado_en"]),
                EnviadoEn = r["enviado_en"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["enviado_en"]) : null,

                SocioNombre = r["socio_nombre"] as string,
                NumeroSocio = r["numero_socio"] != DBNull.Value ? (int?)Convert.ToInt32(r["numero_socio"]) : null,
                SocioFoto = r["socio_foto"] != DBNull.Value ? (byte[])r["socio_foto"] : null,
                EnviadoPorNombre = r["enviado_por_nombre"] as string
            };
        }

        public List<WhatsappMensaje> ObtenerMensajes(string estado = "todos")
        {
            var lista = new List<WhatsappMensaje>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerWhatsappMensajes", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Estado", estado ?? "todos");
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(Mapear(r));
                }
            }
            return lista;
        }

        public WhatsappMensaje ObtenerPorId(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerWhatsappPorId", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read()) return Mapear(r);
                }
            }
            return null;
        }

        public List<WhatsappMensaje> Buscar(string texto, string estado, string tipo)
        {
            var lista = new List<WhatsappMensaje>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarWhatsappMensajes", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Estado", estado ?? "todos");
                    cmd.Parameters.AddWithValue("@Tipo", tipo ?? "todos");
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(Mapear(r));
                }
            }
            return lista;
        }

        public long Insertar(string tipo, string disparador, long? socioId,
                             string telefono, string mensaje, long? enviadoPor)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_InsertarWhatsappMensaje", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Tipo", tipo);
                    cmd.Parameters.AddWithValue("@Disparador", (object)disparador ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SocioId", (object)socioId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Telefono", telefono);
                    cmd.Parameters.AddWithValue("@Mensaje", mensaje);
                    cmd.Parameters.AddWithValue("@EnviadoPor", (object)enviadoPor ?? DBNull.Value);
                    var res = cmd.ExecuteScalar();
                    return res != null ? Convert.ToInt64(res) : 0;
                }
            }
        }

        public bool MarcarComoEnviado(long id, long? enviadoPor)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_MarcarComoEnviado", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@EnviadoPor", (object)enviadoPor ?? DBNull.Value);
                    var f = cmd.ExecuteScalar();
                    return f != null && Convert.ToInt32(f) > 0;
                }
            }
        }

        public bool MarcarComoError(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_MarcarComoError", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var f = cmd.ExecuteScalar();
                    return f != null && Convert.ToInt32(f) > 0;
                }
            }
        }

        public bool Eliminar(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarWhatsappMensaje", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var f = cmd.ExecuteScalar();
                    return f != null && Convert.ToInt32(f) > 0;
                }
            }
        }

        public int GenerarAvisosVencimiento(int diasAntes, long? creadoPor)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_GenerarAvisosVencimiento", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@DiasAntes", diasAntes);
                    cmd.Parameters.AddWithValue("@CreadoPor", (object)creadoPor ?? DBNull.Value);
                    var res = cmd.ExecuteScalar();
                    return res != null ? Convert.ToInt32(res) : 0;
                }
            }
        }

        public EstadisticasWhatsapp ObtenerEstadisticas()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasWhatsapp", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                            return new EstadisticasWhatsapp
                            {
                                Total = Convert.ToInt32(r["total"]),
                                Pendientes = Convert.ToInt32(r["pendientes"]),
                                Enviados = Convert.ToInt32(r["enviados"]),
                                Errores = Convert.ToInt32(r["errores"]),
                                EnviadosHoy = Convert.ToInt32(r["enviados_hoy"]),
                                EnviadosMes = Convert.ToInt32(r["enviados_mes"])
                            };
                }
            }
            return new EstadisticasWhatsapp();
        }

        public List<SocioMasivoItem> ListarSociosParaMasivo()
        {
            var lista = new List<SocioMasivoItem>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ListarSociosParaWhatsappMasivo", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            lista.Add(new SocioMasivoItem
                            {
                                Id = Convert.ToInt64(r["id"]),
                                NombreCompleto = r["nombre_completo"].ToString(),
                                Telefono = r["telefono"].ToString(),
                                EstadoMembresia = r["estado_membresia"].ToString()
                            });
                }
            }
            return lista;
        }

        public int InsertarMasivo(string socioIds, string mensaje, long? enviadoPor)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_InsertarWhatsappMensajeMasivo", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SocioIds", socioIds);
                    cmd.Parameters.AddWithValue("@Mensaje", mensaje);
                    cmd.Parameters.AddWithValue("@EnviadoPor", (object)enviadoPor ?? DBNull.Value);
                    var res = cmd.ExecuteScalar();
                    return res != null ? Convert.ToInt32(res) : 0;
                }
            }
        }
    }
}