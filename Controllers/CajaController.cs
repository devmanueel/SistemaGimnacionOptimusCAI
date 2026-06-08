// ============================================================
//  CAPA: Controllers
//  Archivo: CajaController.cs
//
//  Validaciones + reglas de negocio para Caja.
//  Compatible con C# 7.3.
// ============================================================

using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class CajaController
    {
        private readonly CajaDao _dao = new CajaDao();

        // ──────────────────────────────────────────────────────
        // OBTENER / BUSCAR
        // ──────────────────────────────────────────────────────
        public List<CajaMovimiento> ObtenerMovimientos(DateTime? desde = null, DateTime? hasta = null)
        {
            try { return _dao.ObtenerMovimientos(desde, hasta); }
            catch (Exception ex) { throw new Exception("No se pudieron cargar los movimientos.\n" + ex.Message); }
        }

        public List<CajaMovimiento> BuscarMovimientos(string texto, string filtroTipo,
                                                     DateTime? desde, DateTime? hasta)
        {
            try { return _dao.BuscarMovimientos(texto, filtroTipo, desde, hasta); }
            catch (Exception ex) { throw new Exception("Error en la búsqueda.\n" + ex.Message); }
        }

        public List<CajaMovimiento> BuscarMovimientosPorUsuario(string texto, string filtroTipo,
                                                     DateTime? desde, DateTime? hasta, long usuarioId)
        {
            try { return _dao.BuscarMovimientosPorUsuario(texto, filtroTipo, desde, hasta, usuarioId); }
            catch (Exception ex) { throw new Exception("Error en la búsqueda.\n" + ex.Message); }
        }

        // ──────────────────────────────────────────────────────
        // RESUMEN
        // ──────────────────────────────────────────────────────
        public ResumenCaja ObtenerResumen(DateTime? desde = null, DateTime? hasta = null)
        {
            try { return _dao.ObtenerResumen(desde, hasta); }
            catch { return new ResumenCaja(); }
        }

        public ResumenCaja ResumenDelDia() => ObtenerResumen(DateTime.Today, DateTime.Today);

        public ResumenCaja ResumenDelMes()
        {
            var primerDia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            return ObtenerResumen(primerDia, DateTime.Today);
        }

        // ──────────────────────────────────────────────────────
        // GRÁFICO 7 DÍAS
        // ──────────────────────────────────────────────────────
        public List<IngresoDiario> ObtenerUltimos7Dias()
        {
            try { return _dao.ObtenerIngresosUltimos7Dias(); }
            catch { return new List<IngresoDiario>(); }
        }

        // ──────────────────────────────────────────────────────
        // REGISTRAR GASTO
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) RegistrarGasto(
            long usuarioId,
            string subtipo,
            string detalle,
            decimal monto,
            string metodoPago)
        {
            string err = ValidarGasto(subtipo, monto, metodoPago);
            if (err != null) return (false, err, 0);

            try
            {
                long id = _dao.RegistrarGasto(usuarioId, subtipo.Trim(),
                    string.IsNullOrWhiteSpace(detalle) ? null : detalle.Trim(),
                    monto, metodoPago);

                if (id <= 0) return (false, "No se pudo registrar el gasto.", 0);
                return (true, "Gasto de $" + monto.ToString("N0") + " registrado.", id);
            }
            catch (Exception ex)
            {
                return (false, "Error al registrar el gasto.\n" + ex.Message, 0);
            }
        }

        // ──────────────────────────────────────────────────────
        // REGISTRAR INGRESO MANUAL
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) RegistrarIngresoManual(
            long usuarioId,
            string tipo,
            string subtipo,
            long? socioId,
            string detalle,
            decimal monto,
            string metodoPago)
        {
            // Validar tipo
            if (tipo != "ingreso_clase" && tipo != "movimiento_interno")
                return (false, "Tipo de ingreso manual inválido.", 0);

            string err = ValidarMontoYMetodo(monto, metodoPago);
            if (err != null) return (false, err, 0);

            if (string.IsNullOrWhiteSpace(subtipo))
                return (false, "El concepto es obligatorio.", 0);

            try
            {
                long id = _dao.RegistrarIngresoManual(usuarioId, tipo, subtipo.Trim(),
                    socioId,
                    string.IsNullOrWhiteSpace(detalle) ? null : detalle.Trim(),
                    monto, metodoPago);

                if (id <= 0) return (false, "No se pudo registrar el ingreso.", 0);
                return (true, "Ingreso de $" + monto.ToString("N0") + " registrado.", id);
            }
            catch (Exception ex)
            {
                return (false, "Error al registrar el ingreso.\n" + ex.Message, 0);
            }
        }

        // ──────────────────────────────────────────────────────
        // ELIMINAR MOVIMIENTO
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) EliminarMovimiento(long id)
        {
            try
            {
                bool ok = _dao.EliminarMovimiento(id);
                return ok
                    ? (true, "Movimiento eliminado.")
                    : (false, "No se pudo eliminar el movimiento.");
            }
            catch (Exception ex)
            {
                // El SP devuelve RAISERROR si es un ingreso de cuota o venta
                return (false, ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // VALIDACIONES
        // ──────────────────────────────────────────────────────
        private string ValidarGasto(string subtipo, decimal monto, string metodoPago)
        {
            if (string.IsNullOrWhiteSpace(subtipo))
                return "El concepto del gasto es obligatorio (ej: Alquiler, Sueldos, Limpieza).";

            if (subtipo.Trim().Length < 3)
                return "El concepto debe tener al menos 3 caracteres.";

            return ValidarMontoYMetodo(monto, metodoPago);
        }

        private string ValidarMontoYMetodo(decimal monto, string metodoPago)
        {
            if (monto <= 0)
                return "El monto debe ser mayor a $0.";

            if (monto > 9999999)
                return "El monto es demasiado alto.";

            string[] metodos = { "efectivo", "transferencia", "tarjeta", "otro" };
            bool ok = false;
            foreach (var m in metodos)
                if (m == metodoPago) { ok = true; break; }

            if (!ok) return "Método de pago inválido.";

            return null;
        }
    }
}