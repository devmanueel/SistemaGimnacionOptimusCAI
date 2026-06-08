// ============================================================
//  CAPA: Models / DAO
//  Archivo: CajaDao.cs
//  Acceso a datos para movimientos de caja, vía SP.
// ============================================================

using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class CajaDao : ConnectionToDB
    {
        private static CajaMovimiento MapearMovimiento(SqlDataReader r)
        {
            return new CajaMovimiento
            {
                Id = Convert.ToInt64(r["id"]),
                Tipo = r["tipo"].ToString(),
                Subtipo = r["subtipo"] as string,
                UsuarioId = Convert.ToInt64(r["usuario_id"]),
                SocioId = r["socio_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["socio_id"]) : null,
                MembresiaId = r["membresia_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["membresia_id"]) : null,
                ActividadId = r["actividad_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["actividad_id"]) : null,
                VentaId = r["venta_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["venta_id"]) : null,
                Detalle = r["detalle"] as string,
                MetodoPago = r["metodo_pago"].ToString(),
                Monto = Convert.ToDecimal(r["monto"]),
                CreadoEn = Convert.ToDateTime(r["creado_en"]),

                UsuarioNombre = r["usuario_nombre"] as string,
                SocioNombre = r["socio_nombre"] as string,
                ActividadNombre = r["actividad_nombre"] as string,
                NumeroSocio = r["numero_socio"] != DBNull.Value ? (int?)Convert.ToInt32(r["numero_socio"]) : null
            };
        }

        // ──────────────────────────────────────────────────────
        // OBTENER MOVIMIENTOS (con rango de fechas)
        // ──────────────────────────────────────────────────────
        public List<CajaMovimiento> ObtenerMovimientos(DateTime? desde = null, DateTime? hasta = null)
        {
            var lista = new List<CajaMovimiento>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerMovimientos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta ?? DBNull.Value);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearMovimiento(reader));
                }
            }
            return lista;
        }

        // ──────────────────────────────────────────────────────
        // BUSCAR (texto + filtro tipo + rango)
        // ──────────────────────────────────────────────────────
        public List<CajaMovimiento> BuscarMovimientos(
            string texto, string filtroTipo = "todos",
            DateTime? desde = null, DateTime? hasta = null)
        {
            var lista = new List<CajaMovimiento>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarMovimientos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@FiltroTipo", filtroTipo);
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta ?? DBNull.Value);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearMovimiento(reader));
                }
            }
            return lista;
        }

        public List<CajaMovimiento> BuscarMovimientosPorUsuario(
            string texto, string filtroTipo = "todos",
            DateTime? desde = null, DateTime? hasta = null, long usuarioId = 0)
        {
            var lista = new List<CajaMovimiento>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarMovimientosPorUsuario", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@FiltroTipo", filtroTipo);
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearMovimiento(reader));
                }
            }
            return lista;
        }

        // ──────────────────────────────────────────────────────
        // RESUMEN
        // ──────────────────────────────────────────────────────
        public ResumenCaja ObtenerResumen(DateTime? desde = null, DateTime? hasta = null)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ResumenCaja", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta ?? DBNull.Value);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ResumenCaja
                            {
                                TotalIngresos = Convert.ToDecimal(reader["total_ingresos"]),
                                TotalGastos = Convert.ToDecimal(reader["total_gastos"]),
                                IngresosEfectivo = Convert.ToDecimal(reader["ingresos_efectivo"]),
                                IngresosTransferencia = Convert.ToDecimal(reader["ingresos_transferencia"]),
                                IngresosTarjeta = Convert.ToDecimal(reader["ingresos_tarjeta"]),
                                IngresosCuotas = Convert.ToDecimal(reader["ingresos_cuotas"]),
                                IngresosVentas = Convert.ToDecimal(reader["ingresos_ventas"]),
                                IngresosClases = Convert.ToDecimal(reader["ingresos_clases"]),
                                CantidadMovimientos = Convert.ToInt32(reader["cantidad_movimientos"])
                            };
                        }
                    }
                }
            }
            return new ResumenCaja();
        }

        // ──────────────────────────────────────────────────────
        // REGISTRAR GASTO MANUAL
        // ──────────────────────────────────────────────────────
        public long RegistrarGasto(long usuarioId, string subtipo, string detalle,
                                   decimal monto, string metodoPago)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_RegistrarGasto", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    cmd.Parameters.AddWithValue("@Subtipo", subtipo);
                    cmd.Parameters.AddWithValue("@Detalle", (object)detalle ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Monto", monto);
                    cmd.Parameters.AddWithValue("@MetodoPago", metodoPago ?? "efectivo");

                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? Convert.ToInt64(resultado) : 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // REGISTRAR INGRESO MANUAL (clases sueltas, otros)
        // ──────────────────────────────────────────────────────
        public long RegistrarIngresoManual(long usuarioId, string tipo, string subtipo,
                                           long? socioId, string detalle,
                                           decimal monto, string metodoPago)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_RegistrarIngresoManual", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    cmd.Parameters.AddWithValue("@Tipo", tipo);
                    cmd.Parameters.AddWithValue("@Subtipo", subtipo);
                    cmd.Parameters.AddWithValue("@SocioId", (object)socioId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Detalle", (object)detalle ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Monto", monto);
                    cmd.Parameters.AddWithValue("@MetodoPago", metodoPago ?? "efectivo");

                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? Convert.ToInt64(resultado) : 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // ELIMINAR (solo gastos / movimientos manuales)
        // ──────────────────────────────────────────────────────
        public bool EliminarMovimiento(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarMovimiento", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // GRÁFICO DE LOS ÚLTIMOS 7 DÍAS
        // ──────────────────────────────────────────────────────
        public List<IngresoDiario> ObtenerIngresosUltimos7Dias()
        {
            var lista = new List<IngresoDiario>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_IngresosUltimos7Dias", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(new IngresoDiario
                            {
                                Fecha = Convert.ToDateTime(reader["fecha"]),
                                Ingresos = Convert.ToDecimal(reader["ingresos"]),
                                Gastos = Convert.ToDecimal(reader["gastos"])
                            });
                }
            }
            return lista;
        }
    }
}