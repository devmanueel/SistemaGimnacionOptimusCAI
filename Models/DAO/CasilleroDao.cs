// ============================================================
//  CAPA: Models / DAO
//  Archivo: CasilleroDao.cs
// ============================================================

using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class CasilleroDao : ConnectionToDB
    {
        private static Casillero MapearCasillero(SqlDataReader r)
        {
            return new Casillero
            {
                Id = Convert.ToInt64(r["id"]),
                Numero = Convert.ToInt16(r["numero"]),
                SocioId = r["socio_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["socio_id"]) : null,
                Estado = r["estado"].ToString(),
                PrecioMes = r["precio_mes"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["precio_mes"]) : null,
                Observaciones = r["observaciones"] as string,
                AsignadoEn = r["asignado_en"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["asignado_en"]) : null,

                SocioNombre = r["socio_nombre"] as string,
                NumeroSocio = r["numero_socio"] != DBNull.Value ? (int?)Convert.ToInt32(r["numero_socio"]) : null,
                SocioDni = r["socio_dni"] as string,
                SocioFoto = r["socio_foto"] != DBNull.Value ? (byte[])r["socio_foto"] : null
            };
        }

        // ──────────────────────────────────────────────────────
        // OBTENER TODOS
        // ──────────────────────────────────────────────────────
        public List<Casillero> ObtenerCasilleros()
        {
            var lista = new List<Casillero>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerCasilleros", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearCasillero(reader));
                }
            }
            return lista;
        }

        public Casillero ObtenerCasilleroPorId(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerCasilleroPorId", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                        if (reader.Read()) return MapearCasillero(reader);
                }
            }
            return null;
        }

        // ──────────────────────────────────────────────────────
        // CREAR
        // ──────────────────────────────────────────────────────
        public long CrearCasillero(short numero, decimal? precio, string observaciones)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CrearCasillero", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Numero", numero);
                    cmd.Parameters.AddWithValue("@PrecioMes", (object)precio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Observaciones", (object)observaciones ?? DBNull.Value);
                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? Convert.ToInt64(resultado) : 0;
                }
            }
        }

        public int CrearCasillerosEnMasa(short desde, short hasta, decimal? precio)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CrearCasillerosEnMasa", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@NumeroDesde", desde);
                    cmd.Parameters.AddWithValue("@NumeroHasta", hasta);
                    cmd.Parameters.AddWithValue("@PrecioMes", (object)precio ?? DBNull.Value);
                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? Convert.ToInt32(resultado) : 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // ASIGNAR / LIBERAR
        // ──────────────────────────────────────────────────────
        public bool AsignarCasillero(long id, long socioId, string observaciones)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_AsignarCasillero", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@SocioId", socioId);
                    cmd.Parameters.AddWithValue("@Observaciones", (object)observaciones ?? DBNull.Value);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        public bool LiberarCasillero(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_LiberarCasillero", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // CAMBIAR ESTADO / ACTUALIZAR / ELIMINAR
        // ──────────────────────────────────────────────────────
        public bool CambiarEstado(long id, string nuevoEstado)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CambiarEstadoCasillero", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Estado", nuevoEstado);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        public bool ActualizarCasillero(long id, decimal? precio, string observaciones)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ActualizarCasillero", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@PrecioMes", (object)precio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Observaciones", (object)observaciones ?? DBNull.Value);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        public bool EliminarCasillero(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarCasillero", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // ESTADÍSTICAS
        // ──────────────────────────────────────────────────────
        public EstadisticasCasilleros ObtenerEstadisticas()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasCasilleros", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        if (reader.Read())
                            return new EstadisticasCasilleros
                            {
                                Total = Convert.ToInt32(reader["total"]),
                                Libres = Convert.ToInt32(reader["libres"]),
                                Ocupados = Convert.ToInt32(reader["ocupados"]),
                                Mantenimiento = Convert.ToInt32(reader["mantenimiento"]),
                                IngresoPotencialMes = Convert.ToDecimal(reader["ingreso_potencial_mes"])
                            };
                }
            }
            return new EstadisticasCasilleros();
        }
    }
}