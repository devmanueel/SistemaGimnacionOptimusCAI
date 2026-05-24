// ============================================================
//  CAPA: Models / DAO
//  Archivo: MembresiaDao.cs
//  Acceso a datos para Membresía. Todo vía Stored Procedures.
// ============================================================

using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class MembresiaDao : ConnectionToDB
    {
        private static string LeerColumnaSegura(SqlDataReader r, string columna)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (r.GetName(i).Equals(columna, StringComparison.OrdinalIgnoreCase))
                    return r[columna] as string;
            return null;
        }

        private static Membresia MapearMembresia(SqlDataReader r)
        {
            return new Membresia
            {
                Id = Convert.ToInt64(r["id"]),
                SocioId = Convert.ToInt64(r["socio_id"]),
                ActividadId = Convert.ToInt64(r["actividad_id"]),
                InstructorId = r["instructor_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["instructor_id"]) : null,
                FechaInicio = Convert.ToDateTime(r["fecha_inicio"]),
                FechaVencimiento = Convert.ToDateTime(r["fecha_vencimiento"]),
                MontoPagado = Convert.ToDecimal(r["monto_pagado"]),
                MetodoPago = r["metodo_pago"].ToString(),
                Estado = r["estado"].ToString(),
                TipoPlan = LeerColumnaSegura(r, "tipo_plan") ?? "mensual",
                RegistradoPor = Convert.ToInt64(r["registrado_por"]),
                Observaciones = r["observaciones"] as string,
                CreadoEn = Convert.ToDateTime(r["creado_en"]),
                ActualizadoEn = Convert.ToDateTime(r["actualizado_en"]),

                NumeroSocio = Convert.ToInt32(r["numero_socio"]),
                SocioNombre = r["socio_nombre"].ToString(),
                SocioDni = r["socio_dni"].ToString(),
                SocioFoto = r["socio_foto"] != DBNull.Value ? (byte[])r["socio_foto"] : null,
                ActividadNombre = r["actividad_nombre"].ToString(),
                ActividadTipo = r["actividad_tipo"].ToString(),
                ActividadCategoria = LeerColumnaSegura(r, "actividad_categoria"),
                ActividadNivel = r["actividad_nivel"] != DBNull.Value ? (int?)Convert.ToInt32(r["actividad_nivel"]) : null,
                InstructorNombre = r["instructor_nombre"].ToString(),
                RegistradoPorNombre = r["registrado_por_nombre"].ToString(),
                DiasParaVencer = Convert.ToInt32(r["dias_para_vencer"])
            };
        }

        // ──────────────────────────────────────────────────────
        // OBTENER TODAS
        // ──────────────────────────────────────────────────────
        public List<Membresia> ObtenerMembresias()
        {
            var lista = new List<Membresia>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerMembresias", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearMembresia(reader));
                }
            }
            return lista;
        }

        // ──────────────────────────────────────────────────────
        // OBTENER POR ID
        // ──────────────────────────────────────────────────────
        public Membresia ObtenerMembresiaPorId(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerMembresiaPorId", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                        if (reader.Read()) return MapearMembresia(reader);
                }
            }
            return null;
        }

        // ──────────────────────────────────────────────────────
        // BUSCAR (texto + filtro estado)
        // ──────────────────────────────────────────────────────
        public List<Membresia> BuscarMembresias(string texto, string filtroEstado = "todos")
        {
            var lista = new List<Membresia>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarMembresias", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@FiltroEstado", filtroEstado);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearMembresia(reader));
                }
            }
            return lista;
        }

        // ──────────────────────────────────────────────────────
        // OBTENER CATEGORÍA DE MEMBRESÍA ACTIVA POR SOCIO
        // ──────────────────────────────────────────────────────
        public string ObtenerCategoriaMembresiaActiva(long socioId, long actividadId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerCategoriaMembresiaActiva", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SocioId", socioId);
                    cmd.Parameters.AddWithValue("@ActividadId", actividadId);
                    var resultado = cmd.ExecuteScalar();
                    return resultado != null && resultado != DBNull.Value 
                        ? resultado.ToString() 
                        : null;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // INSERTAR
        // ──────────────────────────────────────────────────────
        public long InsertarMembresia(Membresia m)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_InsertarMembresia", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SocioId", m.SocioId);
                    cmd.Parameters.AddWithValue("@ActividadId", m.ActividadId);
                    cmd.Parameters.AddWithValue("@InstructorId", (object)m.InstructorId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaInicio", m.FechaInicio);
                    cmd.Parameters.AddWithValue("@FechaVencimiento", m.FechaVencimiento);
                    cmd.Parameters.AddWithValue("@TipoPlan", m.TipoPlan ?? "mensual");
                    cmd.Parameters.AddWithValue("@MontoPagado", m.MontoPagado);
                    cmd.Parameters.AddWithValue("@MetodoPago", m.MetodoPago ?? "efectivo");
                    cmd.Parameters.AddWithValue("@RegistradoPor", m.RegistradoPor);
                    cmd.Parameters.AddWithValue("@Observaciones", (object)m.Observaciones ?? DBNull.Value);

                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? Convert.ToInt64(resultado) : 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // MODIFICAR (solo datos secundarios)
        // ──────────────────────────────────────────────────────
        public bool ModificarMembresia(long id, long? instructorId, long? actividadId,
                                       DateTime fechaVencimiento, string observaciones,
                                       long registradoPor = 0,
                                       decimal? montoPagado = null,
                                       string tipoPlan = null,
                                       string metodoPago = null)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ModificarMembresia", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@InstructorId", (object)instructorId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ActividadId", (object)actividadId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaVencimiento", fechaVencimiento);
                    cmd.Parameters.AddWithValue("@MontoPagado", (object)montoPagado ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TipoPlan", (object)tipoPlan ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@MetodoPago", (object)metodoPago ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Observaciones", (object)observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RegistradoPor", registradoPor > 0 ? (object)registradoPor : DBNull.Value);

                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // CAMBIAR ESTADO (cancelar / suspender / activar)
        // ──────────────────────────────────────────────────────
        public bool CambiarEstadoMembresia(long id, string nuevoEstado)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CambiarEstadoMembresia", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Estado", nuevoEstado);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // RENOVAR (suma 30 días + cobra)
        // Retorna la nueva fecha de vencimiento.
        // ──────────────────────────────────────────────────────
        public DateTime? RenovarMembresia(long id, decimal monto, string metodoPago,
                                          long registradoPor, int diasASumar = 30)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_RenovarMembresia", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@MontoPagado", monto);
                    cmd.Parameters.AddWithValue("@MetodoPago", metodoPago ?? "efectivo");
                    cmd.Parameters.AddWithValue("@RegistradoPor", registradoPor);
                    cmd.Parameters.AddWithValue("@DiasASumar", diasASumar);

                    var resultado = cmd.ExecuteScalar();
                    return resultado != null && resultado != DBNull.Value
                        ? (DateTime?)Convert.ToDateTime(resultado)
                        : null;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // ELIMINAR (cancela, no borra)
        // ──────────────────────────────────────────────────────
        public bool EliminarMembresia(long id, long registradoPor = 0)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarMembresia", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@RegistradoPor", registradoPor > 0 ? (object)registradoPor : DBNull.Value);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // COMBOS LIVIANOS
        // ──────────────────────────────────────────────────────
        public List<SocioComboItem> ListarSociosParaCombo()
        {
            var lista = new List<SocioComboItem>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ListarSociosParaCombo", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(new SocioComboItem
                            {
                                Id = Convert.ToInt64(reader["id"]),
                                NumeroSocio = Convert.ToInt32(reader["numero_socio"]),
                                Nombre = reader["nombre"].ToString(),
                                Apellido = reader["apellido"].ToString(),
                                Dni = reader["dni"].ToString()
                            });
                }
            }
            return lista;
        }

        public List<ActividadComboItem> ListarActividadesParaCombo()
        {
            var lista = new List<ActividadComboItem>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ListarActividadesParaCombo", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(new ActividadComboItem
                            {
                                Id = Convert.ToInt64(reader["id"]),
                                Nombre = reader["nombre"].ToString(),
                                Tipo = reader["tipo"].ToString(),
                                DiasSesiones = Convert.ToInt32(reader["dias_sesiones"]),
                                Precio = Convert.ToDecimal(reader["precio"]),
                                Categoria = reader["categoria"] as string,
                                Nivel = reader["nivel"] != DBNull.Value ? (int?)Convert.ToInt32(reader["nivel"]) : null
                            });
                }
            }
            return lista;
        }

        public List<InstructorComboItem> ListarInstructoresParaCombo()
        {
            var lista = new List<InstructorComboItem>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ListarInstructoresParaCombo", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(new InstructorComboItem
                            {
                                Id = Convert.ToInt64(reader["id"]),
                                Nombre = reader["nombre"].ToString(),
                                Apellido = reader["apellido"].ToString()
                            });
                }
            }
            return lista;
        }
    }
}