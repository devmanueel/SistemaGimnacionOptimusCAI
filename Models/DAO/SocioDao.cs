// ============================================================
//  CAPA: Models / DAO
//  Archivo: SocioDao.cs
//
//  Acceso a datos para Socio. TODO va por Stored Procedures.
//  Hereda ConnectionToDB.
// ============================================================

using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class SocioDao : ConnectionToDB
    {
        // Helper: convierte una fila DataReader a Socio
        private static Socio MapearSocio(SqlDataReader r)
        {
            return new Socio
            {
                Id = Convert.ToInt64(r["id"]),
                NumeroSocio = Convert.ToInt32(r["numero_socio"]),
                Nombre = r["nombre"].ToString(),
                Apellido = r["apellido"].ToString(),
                Dni = r["dni"].ToString(),
                DniPin = r["dni_pin"].ToString(),
                Foto = r["foto"] != DBNull.Value ? (byte[])r["foto"] : null,
                FechaNacimiento = r["fecha_nacimiento"] != DBNull.Value ? (DateTime?)r["fecha_nacimiento"] : null,
                Sexo = r["sexo"] as string ?? "Otro",
                Telefono = r["telefono"] as string,
                Domicilio = r["domicilio"] as string,
                Profesion = r["profesion"] as string,
                Email = r["email"] as string,
                ComoNosConocio = r["como_nos_conocio"] as string,
                Observaciones = r["observaciones"] as string,
                Activo = Convert.ToBoolean(r["activo"]),
                RegistradoPor = r["registrado_por"] != DBNull.Value ? (long?)Convert.ToInt64(r["registrado_por"]) : null,
                RegistradoPorNombre = r["registrado_por_nombre"] as string,
                CreadoEn = Convert.ToDateTime(r["creado_en"]),
                ActualizadoEn = Convert.ToDateTime(r["actualizado_en"])
            };
        }

        // ──────────────────────────────────────────────────────
        // OBTENER TODOS
        // ──────────────────────────────────────────────────────
        public List<Socio> ObtenerSocios()
        {
            var lista = new List<Socio>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerSocios", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearSocio(reader));
                }
            }
            return lista;
        }

        // ──────────────────────────────────────────────────────
        // OBTENER POR ID
        // ──────────────────────────────────────────────────────
        public Socio ObtenerSocioPorId(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerSocioPorId", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                        if (reader.Read()) return MapearSocio(reader);
                }
            }
            return null;
        }

        // ──────────────────────────────────────────────────────
        // BUSCAR (con filtro de estado)
        // ──────────────────────────────────────────────────────
        public List<Socio> BuscarSocios(string texto, string filtroEstado = "todos")
        {
            var lista = new List<Socio>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarSocios", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@FiltroEstado", filtroEstado);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearSocio(reader));
                }
            }
            return lista;
        }

        // ──────────────────────────────────────────────────────
        // LISTAR SOCIOS CON MEMBRESÍAS (una fila por membresía)
        // ──────────────────────────────────────────────────────
        public List<SocioConMembresia> ListarSociosConMembresias(
            string texto, string filtroEstado, string filtroAvanzado,
            long? actividadId, DateTime? fechaDesde, DateTime? fechaHasta,
            int? diasSinAsistencia, long? instructorId, string sexo)
        {
            var lista = new List<SocioConMembresia>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ListarSociosConMembresias", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@Texto", SqlDbType.NVarChar, 100).Value = texto ?? string.Empty;
                    cmd.Parameters.Add("@FiltroEstado", SqlDbType.VarChar, 20).Value = filtroEstado ?? "todos";
                    cmd.Parameters.Add("@FiltroAvanzado", SqlDbType.VarChar, 30).Value = filtroAvanzado ?? "todos";
                    cmd.Parameters.Add("@ActividadId", SqlDbType.BigInt).Value = (object)actividadId ?? DBNull.Value;
                    cmd.Parameters.Add("@FechaDesde", SqlDbType.Date).Value = (object)fechaDesde ?? DBNull.Value;
                    cmd.Parameters.Add("@FechaHasta", SqlDbType.Date).Value = (object)fechaHasta ?? DBNull.Value;
                    cmd.Parameters.Add("@DiasSinAsistencia", SqlDbType.Int).Value = (object)diasSinAsistencia ?? DBNull.Value;
                    cmd.Parameters.Add("@InstructorId", SqlDbType.BigInt).Value = (object)instructorId ?? DBNull.Value;
                    cmd.Parameters.Add("@Sexo", SqlDbType.VarChar, 10).Value = (object)sexo ?? DBNull.Value;

                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearSocioConMembresia(reader));
                }
            }
            return lista;
        }

        private static SocioConMembresia MapearSocioConMembresia(SqlDataReader r)
        {
            return new SocioConMembresia
            {
                Id = Convert.ToInt64(r["id"]),
                NumeroSocio = Convert.ToInt32(r["numero_socio"]),
                Nombre = r["nombre"].ToString(),
                Apellido = r["apellido"].ToString(),
                Dni = r["dni"].ToString(),
                DniPin = r["dni_pin"].ToString(),
                Foto = r["foto"] != DBNull.Value ? (byte[])r["foto"] : null,
                FechaNacimiento = r["fecha_nacimiento"] != DBNull.Value ? (DateTime?)r["fecha_nacimiento"] : null,
                Sexo = r["sexo"] as string ?? "Otro",
                Telefono = r["telefono"] as string,
                Domicilio = r["domicilio"] as string,
                Profesion = r["profesion"] as string,
                Email = r["email"] as string,
                ComoNosConocio = r["como_nos_conocio"] as string,
                Observaciones = r["observaciones"] as string,
                Activo = Convert.ToBoolean(r["activo"]),
                RegistradoPor = r["registrado_por"] != DBNull.Value ? (long?)Convert.ToInt64(r["registrado_por"]) : null,
                RegistradoPorNombre = r["registrado_por_nombre"] as string,
                CreadoEn = r["creado_en"] != DBNull.Value ? Convert.ToDateTime(r["creado_en"]) : DateTime.MinValue,
                ActualizadoEn = r["actualizado_en"] != DBNull.Value ? Convert.ToDateTime(r["actualizado_en"]) : DateTime.MinValue,
                ActividadNombre = r["actividad_nombre"] as string ?? "—",
                FechaVencimiento = r["fecha_vencimiento"] != DBNull.Value ? (DateTime?)r["fecha_vencimiento"] : null,
                MembresiaEstado = r["membresia_estado"] != DBNull.Value ? r["membresia_estado"].ToString() : null,
                UltimaAsistencia = r["ultima_asistencia"] != DBNull.Value ? (DateTime?)r["ultima_asistencia"] : null,
                InstructorNombre = r["instructor_nombre"] as string ?? "Sin asignar"
            };
        }

        // ──────────────────────────────────────────────────────
        // SIGUIENTE NÚMERO DE SOCIO (preview en pantalla)
        // ──────────────────────────────────────────────────────
        public int ObtenerSiguienteNumeroSocio()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerSiguienteNumeroSocio", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 1;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // INSERTAR
        // Retorna: Socio con Id y NumeroSocio completados / null = error
        // ──────────────────────────────────────────────────────
        public Socio InsertarSocio(Socio s)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_InsertarSocio", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Nombre", s.Nombre);
                    cmd.Parameters.AddWithValue("@Apellido", s.Apellido);
                    cmd.Parameters.AddWithValue("@Dni", s.Dni);
                    cmd.Parameters.AddWithValue("@DniPin", s.DniPin);
                    cmd.Parameters.AddWithValue("@FechaNacimiento", (object)s.FechaNacimiento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Sexo", s.Sexo ?? "Otro");
                    cmd.Parameters.AddWithValue("@Telefono", (object)s.Telefono ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Domicilio", (object)s.Domicilio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Profesion", (object)s.Profesion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email", (object)s.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ComoNosConocio", (object)s.ComoNosConocio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Observaciones", (object)s.Observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RegistradoPor", (object)s.RegistradoPor ?? DBNull.Value);

                    var fotoParam = new SqlParameter("@Foto", SqlDbType.VarBinary);
                    fotoParam.Value = s.Foto != null ? (object)s.Foto : DBNull.Value;
                    cmd.Parameters.Add(fotoParam);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var id = Convert.ToInt64(reader["id"]);
                            if (id == -1) return null; // DNI duplicado

                            return new Socio
                            {
                                Id = id,
                                NumeroSocio = Convert.ToInt32(reader["numero_socio"])
                            };
                        }
                    }
                    return null;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // MODIFICAR
        // Si cambiarPin = false → no se manda PIN (mantiene el actual).
        // Si cambiarFoto = false → no se manda foto (mantiene la actual).
        // ──────────────────────────────────────────────────────
        public bool ModificarSocio(Socio s, bool cambiarPin = false, bool cambiarFoto = false)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ModificarSocio", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", s.Id);
                    cmd.Parameters.AddWithValue("@Nombre", s.Nombre);
                    cmd.Parameters.AddWithValue("@Apellido", s.Apellido);
                    cmd.Parameters.AddWithValue("@Dni", s.Dni);

                    cmd.Parameters.AddWithValue("@DniPin",
                        cambiarPin ? (object)s.DniPin : DBNull.Value);

                    cmd.Parameters.AddWithValue("@FechaNacimiento", (object)s.FechaNacimiento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Sexo", s.Sexo ?? "Otro");
                    cmd.Parameters.AddWithValue("@Telefono", (object)s.Telefono ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Domicilio", (object)s.Domicilio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Profesion", (object)s.Profesion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email", (object)s.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ComoNosConocio", (object)s.ComoNosConocio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Observaciones", (object)s.Observaciones ?? DBNull.Value);

                    var fotoParam = new SqlParameter("@Foto", SqlDbType.VarBinary);
                    fotoParam.Value = cambiarFoto && s.Foto != null ? (object)s.Foto : DBNull.Value;
                    cmd.Parameters.Add(fotoParam);

                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // CAMBIAR ESTADO
        // ──────────────────────────────────────────────────────
        public bool CambiarEstadoSocio(long id, bool nuevoEstado)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CambiarEstadoSocio", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Activo", nuevoEstado);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // ELIMINAR (soft-delete)
        // ──────────────────────────────────────────────────────
        public bool EliminarSocio(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarSocio", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // SOCIOS INACTIVOS (candidatos a dar de baja)
        // ──────────────────────────────────────────────────────
        public List<SocioInactivo> ObtenerInactivosParaDarDeBaja(int mesesSinActividad = 2)
        {
            var lista = new List<SocioInactivo>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_SociosParaDarDeBaja", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@MesesSinActividad", mesesSinActividad);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(new SocioInactivo
                            {
                                Id = Convert.ToInt64(reader["id"]),
                                Nombre = reader["nombre"].ToString(),
                                Apellido = reader["apellido"].ToString(),
                                Dni = reader["dni"].ToString(),
                                NumeroSocio = Convert.ToInt32(reader["numero_socio"]),
                                Foto = reader["foto"] != DBNull.Value ? (byte[])reader["foto"] : null,
                                UltimaAsistencia = reader["ultima_asistencia"] != DBNull.Value
                                    ? (DateTime?)Convert.ToDateTime(reader["ultima_asistencia"])
                                    : null,
                                DiasInactivo = Convert.ToInt32(reader["dias_inactivo"])
                            });
                }
            }
            return lista;
        }

        public int DarDeBajaLote(string ids)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_DarDeBajaSocios", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Ids", ids);
                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? Convert.ToInt32(resultado) : 0;
                }
            }
        }
    }
}