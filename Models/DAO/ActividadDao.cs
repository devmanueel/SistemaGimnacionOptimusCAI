// ============================================================
//  CAPA: Models / DAO
//  Archivo: ActividadDao.cs
//  Acceso a datos para Actividad. Todo vía Stored Procedures.
// ============================================================

using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class ActividadDao : ConnectionToDB
    {
        private static Actividad MapearActividad(SqlDataReader r)
        {
            return new Actividad
            {
                Id = Convert.ToInt64(r["id"]),
                Nombre = r["nombre"].ToString(),
                Tipo = r["tipo"].ToString(),
                DiasSesiones = Convert.ToInt32(r["dias_sesiones"]),
                DiasSemana = r["dias_semana"] as string,
                Precio = Convert.ToDecimal(r["precio"]),
                Activo = Convert.ToBoolean(r["activo"]),
                CreadoEn = Convert.ToDateTime(r["creado_en"]),
                CantSocios = Convert.ToInt32(r["cant_socios"])
            };
        }

        public List<Actividad> ObtenerActividades()
        {
            var lista = new List<Actividad>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerActividades", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearActividad(reader));
                }
            }
            return lista;
        }

        public List<Actividad> ObtenerActividadesActivas()
        {
            var lista = new List<Actividad>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerActividadesActivas", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearActividad(reader));
                }
            }
            return lista;
        }

        public Actividad ObtenerActividadPorId(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerActividadPorId", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                        if (reader.Read()) return MapearActividad(reader);
                }
            }
            return null;
        }

        public List<Actividad> BuscarActividades(string texto, string filtroEstado = "todos")
        {
            var lista = new List<Actividad>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarActividades", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@FiltroEstado", filtroEstado);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearActividad(reader));
                }
            }
            return lista;
        }

        public long InsertarActividad(Actividad a)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_InsertarActividad", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Nombre", a.Nombre);
                    cmd.Parameters.AddWithValue("@Tipo", a.Tipo);
                    cmd.Parameters.AddWithValue("@DiasSesiones", a.DiasSesiones);
                    cmd.Parameters.AddWithValue("@DiasSemana", (object)a.DiasSemana ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Precio", a.Precio);

                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? Convert.ToInt64(resultado) : 0;
                }
            }
        }

        public bool ModificarActividad(Actividad a)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ModificarActividad", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", a.Id);
                    cmd.Parameters.AddWithValue("@Nombre", a.Nombre);
                    cmd.Parameters.AddWithValue("@Tipo", a.Tipo);
                    cmd.Parameters.AddWithValue("@DiasSesiones", a.DiasSesiones);
                    cmd.Parameters.AddWithValue("@DiasSemana", (object)a.DiasSemana ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Precio", a.Precio);

                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        public bool CambiarEstadoActividad(long id, bool nuevoEstado)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CambiarEstadoActividad", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Activo", nuevoEstado);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        public bool EliminarActividad(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarActividad", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }
    }
}