// ============================================================
//  CAPA: Controllers
//  Archivo: ProductoController.cs
//
//  Validaciones + reglas de negocio para Productos.
//  Compatible con C# 7.3.
// ============================================================

using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class ProductoController
    {
        private readonly ProductoDao _dao = new ProductoDao();

        // ──────────────────────────────────────────────────────
        // OBTENER / BUSCAR
        // ──────────────────────────────────────────────────────
        public List<Producto> ObtenerProductos()
        {
            try { return _dao.ObtenerProductos(); }
            catch (Exception ex) { throw new Exception("No se pudieron cargar los productos.\n" + ex.Message); }
        }

        public List<Producto> BuscarProductos(string texto, string categoria,
                                              string filtroStock, bool soloActivos = false)
        {
            try { return _dao.BuscarProductos(texto, categoria, filtroStock, soloActivos); }
            catch (Exception ex) { throw new Exception("Error en la búsqueda.\n" + ex.Message); }
        }

        public Producto ObtenerPorId(long id)
        {
            try { return _dao.ObtenerProductoPorId(id); }
            catch (Exception ex) { throw new Exception("No se encontró el producto.\n" + ex.Message); }
        }

        public List<string> ListarCategorias()
        {
            try { return _dao.ListarCategorias(); }
            catch { return new List<string>(); }
        }

        public EstadisticasProductos ObtenerEstadisticas()
        {
            try { return _dao.ObtenerEstadisticas(); }
            catch { return new EstadisticasProductos(); }
        }

        // ──────────────────────────────────────────────────────
        // INSERTAR
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) Insertar(
            string nombre,
            string descripcion,
            string categoria,
            decimal precio,
            int stock,
            int stockMin,
            byte[] foto)
        {
            string err = ValidarCampos(nombre, precio, stock, stockMin);
            if (err != null) return (false, err, 0);

            var p = new Producto
            {
                Nombre = nombre.Trim(),
                Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
                Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim(),
                Precio = precio,
                Stock = stock,
                StockMin = stockMin,
                Foto = foto
            };

            try
            {
                long id = _dao.InsertarProducto(p);
                if (id == -1) return (false, "Ya existe un producto con ese nombre.", 0);
                if (id <= 0) return (false, "No se pudo guardar el producto.", 0);

                Auditor.Registrar("crear", "producto", id, new Dictionary<string, object> {
                    { "nombre", nombre }, { "precio", precio }
                });

                return (true, "Producto creado correctamente.", id);
            }
            catch (Exception ex)
            {
                return (false, "Error al insertar.\n" + ex.Message, 0);
            }
        }

        // ──────────────────────────────────────────────────────
        // MODIFICAR (no toca stock)
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Modificar(
            long id,
            string nombre,
            string descripcion,
            string categoria,
            decimal precio,
            int stockMin,
            byte[] foto)
        {
            string err = ValidarCampos(nombre, precio, 0, stockMin);
            if (err != null) return (false, err);

            var p = new Producto
            {
                Id = id,
                Nombre = nombre.Trim(),
                Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
                Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim(),
                Precio = precio,
                StockMin = stockMin,
                Foto = foto
            };

            try
            {
                bool ok = _dao.ModificarProducto(p, foto != null);
                if (ok)
                {
                    Auditor.Registrar("modificar", "producto", id, new Dictionary<string, object> {
                        { "nombre", nombre }, { "precio", precio }
                    });
                }
                return ok
                    ? (true, "Producto actualizado correctamente.")
                    : (false, "No se encontró el producto.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message.Contains("nombre")
                    ? ex.Message
                    : "Error al actualizar.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // AJUSTAR STOCK (sumar / restar / ajustar)
        //   sumar    = entrada de mercadería
        //   restar   = baja manual (rotura, regalo, etc.)
        //   ajustar  = setea el valor exacto (inventario físico)
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, int stockFinal) AjustarStock(
            long id, string tipo, int cantidad)
        {
            if (id <= 0) return (false, "Producto inválido.", 0);
            if (cantidad < 0) return (false, "La cantidad no puede ser negativa.", 0);

            string[] tipos = { "sumar", "restar", "ajustar" };
            bool tipoOk = false;
            foreach (var t in tipos) if (t == tipo) { tipoOk = true; break; }
            if (!tipoOk) return (false, "Tipo de ajuste inválido.", 0);

            try
            {
                int stockFinal = _dao.AjustarStock(id, tipo, cantidad);
                string accion = tipo == "sumar" ? "agregadas"
                              : tipo == "restar" ? "descontadas"
                              : "ajustado";

                string msg = tipo == "ajustar"
                    ? "Stock " + accion + " a " + stockFinal + " unidades."
                    : cantidad + " unidades " + accion + ". Stock actual: " + stockFinal + ".";

                return (true, msg, stockFinal);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, 0);
            }
        }

        // ──────────────────────────────────────────────────────
        // CAMBIAR ESTADO
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) CambiarEstado(long id, bool nuevoEstado)
        {
            try
            {
                bool ok = _dao.CambiarEstado(id, nuevoEstado);
                string accion = nuevoEstado ? "activado" : "desactivado";
                if (ok)
                {
                    Auditor.Registrar(nuevoEstado ? "activar" : "desactivar", "producto", id);
                }
                return ok
                    ? (true, "Producto " + accion + ".")
                    : (false, "No se encontró el producto.");
            }
            catch (Exception ex)
            {
                return (false, "Error al cambiar estado.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // ELIMINAR
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Eliminar(long id)
        {
            try
            {
                bool ok = _dao.EliminarProducto(id);
                if (ok)
                {
                    Auditor.Registrar("eliminar", "producto", id);
                }
                return ok
                    ? (true, "Producto eliminado.")
                    : (false, "No se pudo eliminar el producto.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // VALIDACIONES
        // ──────────────────────────────────────────────────────
        private string ValidarCampos(string nombre, decimal precio, int stock, int stockMin)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return "El nombre del producto es obligatorio.";

            if (nombre.Trim().Length < 2)
                return "El nombre debe tener al menos 2 caracteres.";

            if (nombre.Trim().Length > 150)
                return "El nombre no puede superar los 150 caracteres.";

            if (precio <= 0)
                return "El precio debe ser mayor a $0.";

            if (precio > 9999999)
                return "El precio es demasiado alto.";

            if (stock < 0)
                return "El stock no puede ser negativo.";

            if (stockMin < 0)
                return "El stock mínimo no puede ser negativo.";

            return null;
        }
    }
}