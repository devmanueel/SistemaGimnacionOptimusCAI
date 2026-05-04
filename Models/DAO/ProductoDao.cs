// ============================================================
//  CAPA: Models / DAO
//  Archivo: ProductoDao.cs
// ============================================================

using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class ProductoDao : ConnectionToDB
    {
        private static Producto MapearProducto(SqlDataReader r)
        {
            return new Producto
            {
                Id = Convert.ToInt64(r["id"]),
                Nombre = r["nombre"].ToString(),
                Descripcion = r["descripcion"] as string,
                Categoria = r["categoria"] as string,
                Precio = Convert.ToDecimal(r["precio"]),
                Stock = Convert.ToInt32(r["stock"]),
                StockMin = Convert.ToInt32(r["stock_min"]),
                Activo = Convert.ToBoolean(r["activo"]),
                CreadoEn = Convert.ToDateTime(r["creado_en"]),
                Foto = r["foto"] != DBNull.Value ? (byte[])r["foto"] : null,
                CantidadVendida = Convert.ToInt32(r["cantidad_vendida"])
            };
        }

        // ──────────────────────────────────────────────────────
        // OBTENER TODOS
        // ──────────────────────────────────────────────────────
        public List<Producto> ObtenerProductos()
        {
            var lista = new List<Producto>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerProductos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearProducto(reader));
                }
            }
            return lista;
        }

        public Producto ObtenerProductoPorId(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerProductoPorId", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                        if (reader.Read()) return MapearProducto(reader);
                }
            }
            return null;
        }

        // ──────────────────────────────────────────────────────
        // BUSCAR
        // ──────────────────────────────────────────────────────
        public List<Producto> BuscarProductos(string texto, string categoria,
                                              string filtroStock, bool soloActivos)
        {
            var lista = new List<Producto>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_BuscarProductos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Categoria", string.IsNullOrEmpty(categoria) ? (object)DBNull.Value : categoria);
                    cmd.Parameters.AddWithValue("@FiltroStock", filtroStock ?? "todos");
                    cmd.Parameters.AddWithValue("@SoloActivos", soloActivos);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearProducto(reader));
                }
            }
            return lista;
        }

        // ──────────────────────────────────────────────────────
        // INSERTAR
        // ──────────────────────────────────────────────────────
        public long InsertarProducto(Producto p)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_InsertarProducto", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Nombre", p.Nombre);
                    cmd.Parameters.AddWithValue("@Descripcion", (object)p.Descripcion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Categoria", (object)p.Categoria ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Precio", p.Precio);
                    cmd.Parameters.AddWithValue("@Stock", p.Stock);
                    cmd.Parameters.AddWithValue("@StockMin", p.StockMin);

                    var fotoParam = new SqlParameter("@Foto", SqlDbType.VarBinary);
                    fotoParam.Value = p.Foto != null ? (object)p.Foto : DBNull.Value;
                    cmd.Parameters.Add(fotoParam);

                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? Convert.ToInt64(resultado) : 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // MODIFICAR (NO toca stock — para eso usar AjustarStock)
        // ──────────────────────────────────────────────────────
        public bool ModificarProducto(Producto p, bool cambiarFoto)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ModificarProducto", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", p.Id);
                    cmd.Parameters.AddWithValue("@Nombre", p.Nombre);
                    cmd.Parameters.AddWithValue("@Descripcion", (object)p.Descripcion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Categoria", (object)p.Categoria ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Precio", p.Precio);
                    cmd.Parameters.AddWithValue("@StockMin", p.StockMin);

                    var fotoParam = new SqlParameter("@Foto", SqlDbType.VarBinary);
                    fotoParam.Value = cambiarFoto && p.Foto != null ? (object)p.Foto : DBNull.Value;
                    cmd.Parameters.Add(fotoParam);

                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // AJUSTAR STOCK — retorna el stock final
        // ──────────────────────────────────────────────────────
        public int AjustarStock(long id, string tipo, int cantidad)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_AjustarStock", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Tipo", tipo);
                    cmd.Parameters.AddWithValue("@Cantidad", cantidad);
                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? Convert.ToInt32(resultado) : 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // CAMBIAR ESTADO
        // ──────────────────────────────────────────────────────
        public bool CambiarEstado(long id, bool activo)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_CambiarEstadoProducto", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Activo", activo);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // ELIMINAR
        // ──────────────────────────────────────────────────────
        public bool EliminarProducto(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EliminarProducto", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────
        // CATEGORÍAS
        // ──────────────────────────────────────────────────────
        public List<string> ListarCategorias()
        {
            var lista = new List<string>();
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ListarCategorias", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                        {
                            string cat = reader["categoria"] as string;
                            if (!string.IsNullOrEmpty(cat)) lista.Add(cat);
                        }
                }
            }
            return lista;
        }

        // ──────────────────────────────────────────────────────
        // ESTADÍSTICAS
        // ──────────────────────────────────────────────────────
        public EstadisticasProductos ObtenerEstadisticas()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_EstadisticasProductos", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    using (var reader = cmd.ExecuteReader())
                        if (reader.Read())
                            return new EstadisticasProductos
                            {
                                Total = Convert.ToInt32(reader["total"]),
                                Activos = Convert.ToInt32(reader["activos"]),
                                SinStock = Convert.ToInt32(reader["sin_stock"]),
                                BajoStock = Convert.ToInt32(reader["bajo_stock"]),
                                ValorInventario = Convert.ToDecimal(reader["valor_inventario"])
                            };
                }
            }
            return new EstadisticasProductos();
        }
    }
}