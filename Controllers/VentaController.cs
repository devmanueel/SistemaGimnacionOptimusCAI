// Controllers/VentaController.cs — C# 7.3
using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class VentaController
    {
        private readonly VentaDao _dao = new VentaDao();
        private readonly MembresiaController _membresiaCtrl = new MembresiaController();
        private readonly ProductoController _productoCtrl = new ProductoController();

        public List<Venta> ObtenerVentas(DateTime? desde = null, DateTime? hasta = null)
        {
            try { return _dao.ObtenerVentas(desde, hasta); }
            catch (Exception ex) { throw new Exception("Error al cargar ventas.\n" + ex.Message); }
        }

        public List<Venta> BuscarVentas(string texto, string metodoPago, DateTime? desde, DateTime? hasta)
        {
            try { return _dao.BuscarVentas(texto, metodoPago, desde, hasta); }
            catch (Exception ex) { throw new Exception("Error en la busqueda.\n" + ex.Message); }
        }

        public List<Venta> BuscarVentasPorUsuario(string texto, string metodoPago,
            DateTime? desde, DateTime? hasta, long usuarioId)
        {
            try { return _dao.BuscarVentasPorUsuario(texto, metodoPago, desde, hasta, usuarioId); }
            catch (Exception ex) { throw new Exception("Error en la busqueda.\n" + ex.Message); }
        }

        public Venta ObtenerPorId(long id)
        {
            try { return _dao.ObtenerVentaPorId(id); }
            catch (Exception ex) { throw new Exception("No se encontro la venta.\n" + ex.Message); }
        }

        public List<SocioComboItem> ListarSociosParaCombo()
            => _membresiaCtrl.ListarSociosParaCombo();

        public List<Producto> ListarProductosParaVenta()
        {
            try { return _productoCtrl.BuscarProductos(string.Empty, null, "todos", true); }
            catch { return new List<Producto>(); }
        }

        public (bool ok, string mensaje, long nuevoId, decimal total) RegistrarVenta(
            long usuarioId, long? socioId, string metodoPago,
            string observaciones, List<ItemCarrito> items)
        {
            if (items == null || items.Count == 0)
                return (false, "El carrito esta vacio.", 0, 0);

            string[] metodos = { "efectivo", "transferencia", "tarjeta", "otro" };
            bool metodoOk = false;
            foreach (var m in metodos) if (m == metodoPago) { metodoOk = true; break; }
            if (!metodoOk) return (false, "Metodo de pago invalido.", 0, 0);

            foreach (var item in items)
            {
                if (item.Cantidad <= 0)
                    return (false, "Cantidad invalida en '" + item.Nombre + "'.", 0, 0);
            }

            try
            {
                var res = _dao.RegistrarVenta(usuarioId, socioId, metodoPago,
                    string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim(), items);

                if (res.id <= 0) return (false, "No se pudo registrar la venta.", 0, 0);

                return (true,
                    "Venta #" + res.id.ToString("D5") + " registrada por $" + res.total.ToString("N0") + ".",
                    res.id, res.total);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, 0, 0);
            }
        }

        public (bool ok, string mensaje) AnularVenta(long id)
        {
            if (id <= 0) return (false, "ID invalido.");
            try
            {
                bool ok = _dao.AnularVenta(id);
                return ok
                    ? (true, "Venta anulada. Stock repuesto y caja ajustada.")
                    : (false, "No se pudo anular la venta.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public EstadisticasVentas ObtenerEstadisticas()
        {
            try { return _dao.ObtenerEstadisticas(); }
            catch { return new EstadisticasVentas(); }
        }

        public List<TopProducto> ObtenerTopProductos()
        {
            try { return _dao.ObtenerTopProductos(); }
            catch { return new List<TopProducto>(); }
        }
    }
}