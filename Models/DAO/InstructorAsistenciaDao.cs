// Models/Dao/InstructorAsistenciaDao.cs — C# 7.3
using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class InstructorAsistenciaDao : ConnectionToDB
    {
        private static InstructorAsistencia Mapear(SqlDataReader r)
        {
            return new InstructorAsistencia
            {
                Id = Convert.ToInt64(r["id"]),
                InstructorId = Convert.ToInt64(r["instructor_id"]),
                TurnoId = r["turno_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["turno_id"]) : null,
                Fecha = Convert.ToDateTime(r["fecha"]),
                HoraEntrada = r["hora_entrada"] != DBNull.Value ? (TimeSpan?)r["hora_entrada"] : null,
                HoraSalida = r["hora_salida"] != DBNull.Value ? (TimeSpan?)r["hora_salida"] : null,
                HorasTrabajadas = r["horas_trabajadas"] != DBNull.Value ? Convert.ToDecimal(r["horas_trabajadas"]) : 0m,
                Observaciones = r["observaciones"] as string,
                RegistradoPor = r["registrado_por"] != DBNull.Value ? (long?)Convert.ToInt64(r["registrado_por"]) : null,
                CreadoEn = Convert.ToDateTime(r["creado_en"]),

                InstructorNombre = r["instructor_nombre"] as string,
                InstructorFoto = r["instructor_foto"] != DBNull.Value ? (byte[])r["instructor_foto"] : null,
                ActividadNombre = r["actividad_nombre"] as string,
                DiaSemana = r["dia_semana"] != DBNull.Value ? (byte?)Convert.ToByte(r["dia_semana"]) : null,
                TurnoHoraInicio = r["hora_inicio"] != DBNull.Value ? (TimeSpan?)r["hora_inicio"] : null,
                TurnoHoraFin = r["hora_fin"] != DBNull.Value ? (TimeSpan?)r["hora_fin"] : null,
                RegistradoPorNombre = r["registrado_por_nombre"] as string
            };
        }

        public List<InstructorAsistencia> Obtener(DateTime? desde, DateTime? hasta)
        {
            var lista = new List<InstructorAsistencia>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerInstructorAsistencias", conn))
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

        public List<InstructorAsistencia> Buscar(string texto, long? instructorId,
                                                  DateTime? desde, DateTime? hasta)
        {
            var lista = new List<InstructorAsistencia>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarInstructorAsistencias", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@InstructorId", (object)instructorId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta ?? DBNull.Value);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(Mapear(r));
                }
            }
            return lista;
        }

        public long RegistrarEntrada(long instructorId, long? turnoId, DateTime? fecha,
                                     string observaciones, long registradoPor)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_RegistrarEntradaInstructor", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@InstructorId", instructorId);
                    cmd.Parameters.AddWithValue("@TurnoId", (object)turnoId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Fecha", (object)fecha ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Observaciones", (object)observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RegistradoPor", registradoPor);
                    var res = cmd.ExecuteScalar();
                    return res != null ? Convert.ToInt64(res) : 0;
                }
            }
        }

        public bool RegistrarSalida(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_RegistrarSalidaInstructor", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        public bool Actualizar(long id, long? turnoId, TimeSpan? horaEntrada,
                               TimeSpan? horaSalida, string observaciones)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ActualizarInstructorAsistencia", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@TurnoId", (object)turnoId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@HoraEntrada", (object)horaEntrada ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@HoraSalida", (object)horaSalida ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Observaciones", (object)observaciones ?? DBNull.Value);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        public bool Eliminar(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarInstructorAsistencia", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        public List<TurnoHoy> ObtenerTurnosDeHoy()
        {
            var lista = new List<TurnoHoy>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_TurnosDeHoy", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            lista.Add(new TurnoHoy
                            {
                                TurnoId = Convert.ToInt64(r["turno_id"]),
                                ActividadId = Convert.ToInt64(r["actividad_id"]),
                                InstructorId = r["instructor_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["instructor_id"]) : null,
                                DiaSemana = Convert.ToByte(r["dia_semana"]),
                                HoraInicio = (TimeSpan)r["hora_inicio"],
                                HoraFin = (TimeSpan)r["hora_fin"],
                                CupoMaximo = r["cupo_maximo"] != DBNull.Value ? Convert.ToInt16(r["cupo_maximo"]) : (short)30,
                                ActividadNombre = r["actividad_nombre"] as string,
                                InstructorNombre = r["instructor_nombre"] as string,
                                InstructorFoto = r["instructor_foto"] != DBNull.Value ? (byte[])r["instructor_foto"] : null,
                                AsistenciaId = r["asistencia_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["asistencia_id"]) : null,
                                HoraEntrada = r["hora_entrada"] != DBNull.Value ? (TimeSpan?)r["hora_entrada"] : null,
                                HoraSalida = r["hora_salida"] != DBNull.Value ? (TimeSpan?)r["hora_salida"] : null
                            });
                }
            }
            return lista;
        }

        public EstadisticasInstructorAsistencias ObtenerEstadisticas()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasInstructorAsistencias", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                            return new EstadisticasInstructorAsistencias
                            {
                                AsistenciasHoy = Convert.ToInt32(r["asistencias_hoy"]),
                                AbiertasHoy = Convert.ToInt32(r["abiertas_hoy"]),
                                InstructoresHoy = Convert.ToInt32(r["instructores_hoy"]),
                                AsistenciasMes = Convert.ToInt32(r["asistencias_mes"])
                            };
                }
            }
            return new EstadisticasInstructorAsistencias();
        }

        public FichajeResultado FicharEntrada(long instructorId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_FicharEntradaInstructor", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@InstructorId", instructorId);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                            return new FichajeResultado
                            {
                                Id             = Convert.ToInt64(r["id"]),
                                InstructorId   = Convert.ToInt64(r["instructor_id"]),
                                NombreCompleto = r["nombre_completo"] as string,
                                HoraEntrada    = r["hora_entrada"] != DBNull.Value ? (TimeSpan)r["hora_entrada"] : default(TimeSpan)
                            };
                }
            }
            return null;
        }

        public FichajeResultado FicharSalida(long instructorId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_FicharSalidaInstructor", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@InstructorId", instructorId);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                            return new FichajeResultado
                            {
                                Id              = Convert.ToInt64(r["id"]),
                                InstructorId    = Convert.ToInt64(r["instructor_id"]),
                                NombreCompleto  = r["nombre_completo"] as string,
                                HoraEntrada     = r["hora_entrada"]    != DBNull.Value ? (TimeSpan)r["hora_entrada"]              : default(TimeSpan),
                                HoraSalida      = r["hora_salida"]     != DBNull.Value ? (TimeSpan)r["hora_salida"]               : default(TimeSpan),
                                HorasTrabajadas = r["horas_trabajadas"] != DBNull.Value ? Convert.ToDecimal(r["horas_trabajadas"]) : 0m
                            };
                }
            }
            return null;
        }

        public List<InstructorAsistencia> BuscarPropias(long instructorId,
                                                         DateTime? desde, DateTime? hasta)
        {
            var lista = new List<InstructorAsistencia>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarMisAsistencias", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@InstructorId", instructorId);
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta ?? DBNull.Value);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(Mapear(r));
                }
            }
            return lista;
        }

        public List<ReporteInstructor> ObtenerReporteMensual(int anio, int mes)
        {
            var lista = new List<ReporteInstructor>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ReporteMensualInstructores", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Anio", anio);
                    cmd.Parameters.AddWithValue("@Mes", mes);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            lista.Add(new ReporteInstructor
                            {
                                InstructorId    = Convert.ToInt64(r["instructor_id"]),
                                NombreCompleto  = r["nombre_completo"] as string,
                                TarifaHora      = r["tarifa_hora"]      != DBNull.Value ? Convert.ToDecimal(r["tarifa_hora"])      : 0m,
                                ActividadNombre = r["actividad_nombre"] as string ?? "-",
                                DiasAsistidos   = Convert.ToInt32(r["dias_asistidos"]),
                                TotalHoras      = r["total_horas"]      != DBNull.Value ? Convert.ToDecimal(r["total_horas"])      : 0m,
                                SueldoEstimado  = r["sueldo_estimado"]  != DBNull.Value ? Convert.ToDecimal(r["sueldo_estimado"])  : 0m
                            });
                }
            }
            return lista;
        }

        public List<InstructorAsistencia> ObtenerReporteSemanal(DateTime desde, DateTime hasta)
        {
            var lista = new List<InstructorAsistencia>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ReporteSemanalInstructores", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", desde.Date);
                    cmd.Parameters.AddWithValue("@FechaHasta", hasta.Date);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            if (r["fecha"] == DBNull.Value) continue;
                            lista.Add(new InstructorAsistencia
                            {
                                InstructorId    = Convert.ToInt64(r["instructor_id"]),
                                InstructorNombre = r["nombre_completo"] as string,
                                Fecha           = Convert.ToDateTime(r["fecha"]),
                                HoraEntrada     = r["hora_entrada"]     != DBNull.Value ? (TimeSpan?)r["hora_entrada"]              : null,
                                HoraSalida      = r["hora_salida"]      != DBNull.Value ? (TimeSpan?)r["hora_salida"]               : null,
                                HorasTrabajadas = r["horas_trabajadas"] != DBNull.Value ? Convert.ToDecimal(r["horas_trabajadas"])   : 0m
                            });
                        }
                }
            }
            return lista;
        }
    }
}