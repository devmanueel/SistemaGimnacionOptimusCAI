// Models/Dao/TurnoDao.cs — C# 7.3
using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class TurnoDao : ConnectionToDB
    {
        private static Turno MapearTurno(SqlDataReader r)
        {
            return new Turno
            {
                Id = Convert.ToInt64(r["id"]),
                ActividadId = Convert.ToInt64(r["actividad_id"]),
                InstructorId = r["instructor_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["instructor_id"]) : null,
                DiaSemana = Convert.ToByte(r["dia_semana"]),
                HoraInicio = (TimeSpan)r["hora_inicio"],
                HoraFin = (TimeSpan)r["hora_fin"],
                CupoMaximo = r["cupo_maximo"] != DBNull.Value ? Convert.ToInt16(r["cupo_maximo"]) : (short)30,
                Activo = Convert.ToBoolean(r["activo"]),
                ActividadNombre = r["actividad_nombre"] as string,
                InstructorNombre = r["instructor_nombre"] as string
            };
        }

        public List<Turno> ObtenerTurnos()
        {
            var lista = new List<Turno>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerTurnos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(MapearTurno(r));
                }
            }
            return lista;
        }

        public Turno ObtenerTurnoPorId(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerTurnoPorId", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read()) return MapearTurno(r);
                }
            }
            return null;
        }

        public List<Turno> BuscarTurnos(string texto, long? actividadId, byte? diaSemana, bool soloActivos)
        {
            var lista = new List<Turno>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarTurnos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@ActividadId", (object)actividadId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DiaSemana", (object)diaSemana ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SoloActivos", soloActivos);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(MapearTurno(r));
                }
            }
            return lista;
        }

        public long InsertarTurno(Turno t)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_InsertarTurno", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ActividadId", t.ActividadId);
                    cmd.Parameters.AddWithValue("@InstructorId", (object)t.InstructorId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DiaSemana", t.DiaSemana);
                    cmd.Parameters.AddWithValue("@HoraInicio", t.HoraInicio);
                    cmd.Parameters.AddWithValue("@HoraFin", t.HoraFin);
                    cmd.Parameters.AddWithValue("@CupoMaximo", t.CupoMaximo);
                    var res = cmd.ExecuteScalar();
                    return res != null ? Convert.ToInt64(res) : 0;
                }
            }
        }

        public bool ModificarTurno(Turno t)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ModificarTurno", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", t.Id);
                    cmd.Parameters.AddWithValue("@ActividadId", t.ActividadId);
                    cmd.Parameters.AddWithValue("@InstructorId", (object)t.InstructorId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DiaSemana", t.DiaSemana);
                    cmd.Parameters.AddWithValue("@HoraInicio", t.HoraInicio);
                    cmd.Parameters.AddWithValue("@HoraFin", t.HoraFin);
                    cmd.Parameters.AddWithValue("@CupoMaximo", t.CupoMaximo);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        public bool CambiarEstado(long id, bool activo)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CambiarEstadoTurno", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Activo", activo);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        public bool EliminarTurno(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarTurno", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        public EstadisticasTurnos ObtenerEstadisticas()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasTurnos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                            return new EstadisticasTurnos
                            {
                                Total = Convert.ToInt32(r["total"]),
                                Activos = Convert.ToInt32(r["activos"]),
                                SinInstructor = Convert.ToInt32(r["sin_instructor"]),
                                CupoTotal = Convert.ToInt32(r["cupo_total"])
                            };
                }
            }
            return new EstadisticasTurnos();
        }
    }
}