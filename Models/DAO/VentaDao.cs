// Models/Dao/VentaDao.cs — C# 7.3
using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class VentaDao : ConnectionToDB
    {
        private static Venta MapearVenta(SqlDataReader r)
        {
            return new Venta
            {
                Id = Convert.ToInt64(r["id"]),
                UsuarioId = Convert.ToInt64(r["usuario_id"]),
                SocioId = r["socio_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["socio_id"]) : null,
                Total = Convert.ToDecimal(r["total"]),
                MetodoPago = r["metodo_pago"].ToString(),
                Observaciones = r["observaciones"] as string,
                CreadoEn = Convert.ToDateTime(r["creado_en"]),
                UsuarioNombre = r["usuario_nombre"] as string,
                SocioNombre = r["socio_nombre"] as string,
                NumeroSocio = r["numero_socio"] != DBNull.Value ? (int?)Convert.ToInt32(r["numero_socio"]) : null,
                SocioFoto = r["socio_foto"] != DBNull.Value ? (byte[])r["socio_foto"] : null,
                CantidadItems = HasColumn(r, "cantidad_items") ? Convert.ToInt32(r["cantidad_items"]) : 0
            };
        }

        private static bool HasColumn(SqlDataReader r, string col)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (r.GetName(i).Equals(col, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public List<Venta> ObtenerVentas(DateTime? desde = null, DateTime? hasta = null)
        {
            var lista = new List<Venta>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerVentas", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta ?? DBNull.Value);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(MapearVenta(r));
                }
            }
            return lista;
        }

        public Venta ObtenerVentaPorId(long id)
        {
            Venta venta = null;
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerVentaPorId", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read()) venta = MapearVenta(r);
                        if (venta != null && r.NextResult())
                        {
                            while (r.Read())
                            {
                                venta.Items.Add(new VentaItem
                                {
                                    Id = Convert.ToInt64(r["id"]),
                                    VentaId = Convert.ToInt64(r["venta_id"]),
                                    ProductoId = r["producto_id"] != DBNull.Value ? (long?)Convert.ToInt64(r["producto_id"]) : null,
                                    Descripcion = r["descripcion"].ToString(),
                                    Cantidad = Convert.ToInt32(r["cantidad"]),
                                    PrecioUnitario = Convert.ToDecimal(r["precio_unitario"]),
                                    Subtotal = Convert.ToDecimal(r["subtotal"]),
                                    ProductoNombre = r["producto_nombre"] as string,
                                    ProductoFoto = r["producto_foto"] != DBNull.Value ? (byte[])r["producto_foto"] : null
                                });
                            }
                        }
                    }
                }
            }
            return venta;
        }

        public List<Venta> BuscarVentas(string texto, string metodoPago, DateTime? desde, DateTime? hasta)
        {
            var lista = new List<Venta>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarVentas", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@MetodoPago", metodoPago ?? "todos");
                    cmd.Parameters.AddWithValue("@FechaDesde", (object)desde ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaHasta", (object)hasta ?? DBNull.Value);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) lista.Add(MapearVenta(r));
                }
            }
            return lista;
        }

        public (long id, decimal total) RegistrarVenta(
            long usuarioId, long? socioId, string metodoPago,
            string observaciones, List<ItemCarrito> items)
        {
            var dt = new DataTable();
            dt.Columns.Add("producto_id", typeof(long));
            dt.Columns.Add("descripcion", typeof(string));
            dt.Columns.Add("cantidad", typeof(int));
            dt.Columns.Add("precio_unitario", typeof(decimal));

            foreach (var item in items)
                dt.Rows.Add((object)item.ProductoId, item.Nombre, item.Cantidad, item.PrecioUnitario);

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_RegistrarVenta", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    cmd.Parameters.AddWithValue("@SocioId", (object)socioId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@MetodoPago", metodoPago ?? "efectivo");
                    cmd.Parameters.AddWithValue("@Observaciones", (object)observaciones ?? DBNull.Value);

                    var p = cmd.Parameters.AddWithValue("@Items", dt);
                    p.SqlDbType = SqlDbType.Structured;
                    p.TypeName = "dbo.TipoVentaItem";

                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                            return (Convert.ToInt64(r["id"]), Convert.ToDecimal(r["total"]));
                }
            }
            return (0, 0);
        }

        public bool AnularVenta(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_AnularVenta", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var res = cmd.ExecuteScalar();
                    return res != null && Convert.ToInt32(res) == 1;
                }
            }
        }

        public EstadisticasVentas ObtenerEstadisticas()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasVentas", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                            return new EstadisticasVentas
                            {
                                VentasHoy = Convert.ToInt32(r["ventas_hoy"]),
                                TotalHoy = Convert.ToDecimal(r["total_hoy"]),
                                VentasMes = Convert.ToInt32(r["ventas_mes"]),
                                TotalMes = Convert.ToDecimal(r["total_mes"])
                            };
                }
            }
            return new EstadisticasVentas();
        }

        public List<TopProducto> ObtenerTopProductos()
        {
            var lista = new List<TopProducto>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_TopProductosVendidos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            lista.Add(new TopProducto
                            {
                                Id = Convert.ToInt64(r["id"]),
                                Nombre = r["nombre"].ToString(),
                                Foto = r["foto"] != DBNull.Value ? (byte[])r["foto"] : null,
                                UnidadesVendidas = Convert.ToInt32(r["unidades_vendidas"]),
                                TotalFacturado = Convert.ToDecimal(r["total_facturado"])
                            });
                }
            }
            return lista;
        }
    }
}