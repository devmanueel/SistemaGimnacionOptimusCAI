// ============================================================
//  CAPA: Models / DAO
//  Archivo: AsistenciaDao.cs
//  Acceso a datos para registros_acceso, vía SP.
// ============================================================

using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class AsistenciaDao : ConnectionToDB
    {
        // ──────────────────────────────────────────────────────
        // VALIDAR ACCESO POR DNI
        // El SP hace todo: busca socio, valida activo, valida
        // membresía, valida día, registra el acceso y retorna.
        // ──────────────────────────────────────────────────────
        public ResultadoValidacion ValidarAccesoPorDni(string dni, string metodoAcceso = "dni_pin")
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ValidarAccesoPorDni", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Dni", dni);
                    cmd.Parameters.AddWithValue("@MetodoAcceso", metodoAcceso);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ResultadoValidacion
                            {
                                SocioId = reader["socio_id"] != DBNull.Value ? (long?)Convert.ToInt64(reader["socio_id"]) : null,
                                Resultado = reader["resultado"].ToString(),
                                Mensaje = reader["mensaje"].ToString(),
                                SocioNombre = reader["socio_nombre"] as string,
                                NumeroSocio = reader["numero_socio"] != DBNull.Value ? (int?)Convert.ToInt32(reader["numero_socio"]) : null,
                                Foto = reader["foto"] != DBNull.Value ? (byte[])reader["foto"] : null,
                                ActividadNombre = reader["actividad_nombre"] as string,
                                FechaVencimiento = reader["fecha_vencimiento"] != DBNull.Value ? (DateTime?)reader["fecha_vencimiento"] : null,
                                RegistroId = reader["registro_id"] != DBNull.Value ? (long?)Convert.ToInt64(reader["registro_id"]) : null
                            };
                        }
                    }
                }
            }

            return new ResultadoValidacion
            {
                Resultado = "denegado_socio_inactivo",
                Mensaje = "Error al validar el acceso."
            };
        }

        // ──────────────────────────────────────────────────────
        // ACCESOS DEL DÍA
        // ──────────────────────────────────────────────────────
        public List<RegistroAcceso> ObtenerAccesosDelDia(int limite = 50)
        {
            var lista = new List<RegistroAcceso>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerAccesosDelDia", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Limite", limite);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearRegistro(reader));
                }
            }
            return lista;
        }

        // ──────────────────────────────────────────────────────
        // BUSCAR ACCESOS (con filtros)
        // ──────────────────────────────────────────────────────
        public List<RegistroAcceso> BuscarAccesos(string texto, string filtroResultado,
                                                  DateTime? desde, DateTime? hasta)
        {
            var lista = new List<RegistroAcceso>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarAccesos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@FiltroResultado", filtroResultado);
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta ?? DBNull.Value);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearRegistro(reader));
                }
            }
            return lista;
        }

        // ──────────────────────────────────────────────────────
        // ESTADÍSTICAS
        // ──────────────────────────────────────────────────────
        public EstadisticasAcceso ObtenerEstadisticas()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasAccesos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        if (reader.Read())
                            return new EstadisticasAcceso
                            {
                                PermitidosHoy = Convert.ToInt32(reader["permitidos_hoy"]),
                                DenegadosHoy = Convert.ToInt32(reader["denegados_hoy"]),
                                SociosUnicosHoy = Convert.ToInt32(reader["socios_unicos_hoy"]),
                                AccesosSemana = Convert.ToInt32(reader["accesos_semana"])
                            };
                }
            }
            return new EstadisticasAcceso();
        }

        // ──────────────────────────────────────────────────────
        // MAPEO
        // ──────────────────────────────────────────────────────
        private static RegistroAcceso MapearRegistro(SqlDataReader r)
        {
            return new RegistroAcceso
            {
                Id = Convert.ToInt64(r["id"]),
                SocioId = Convert.ToInt64(r["socio_id"]),
                MembresiaId = r["membresia_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["membresia_id"]) : null,
                MetodoAcceso = r["metodo_acceso"].ToString(),
                Resultado = r["resultado"].ToString(),
                AccedidoEn = Convert.ToDateTime(r["accedido_en"]),
                NumeroSocio = Convert.ToInt32(r["numero_socio"]),
                SocioNombre = r["socio_nombre"].ToString(),
                SocioFoto = r["socio_foto"] != DBNull.Value ? (byte[])r["socio_foto"] : null,
                SocioDni = r["socio_dni"].ToString(),
                ActividadNombre = r["actividad_nombre"] as string
            };
        }
    }
}