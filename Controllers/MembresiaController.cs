// ============================================================
//  CAPA: Controllers
//  Archivo: MembresiaController.cs
//
//  Validaciones + reglas de negocio para Membresía.
//  Compatible con C# 7.3.
// ============================================================

using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class MembresiaController
    {
        private readonly MembresiaDao _dao = new MembresiaDao();

        // ──────────────────────────────────────────────────────
        // OBTENER / BUSCAR
        // ──────────────────────────────────────────────────────
        public List<Membresia> ObtenerMembresias()
        {
            try { return _dao.ObtenerMembresias(); }
            catch (Exception ex) { throw new Exception("No se pudieron cargar las membresías.\n" + ex.Message); }
        }

        public List<Membresia> BuscarMembresias(string texto, string filtroEstado = "todos")
        {
            try { return _dao.BuscarMembresias(texto, filtroEstado); }
            catch (Exception ex) { throw new Exception("Error en la búsqueda.\n" + ex.Message); }
        }

        public Membresia ObtenerPorId(long id)
        {
            try { return _dao.ObtenerMembresiaPorId(id); }
            catch (Exception ex) { throw new Exception("No se encontró la membresía.\n" + ex.Message); }
        }

        // ──────────────────────────────────────────────────────
        // INSERTAR (= cobrar cuota nueva)
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) Insertar(
            long socioId,
            long actividadId,
            long? instructorId,
            DateTime fechaInicio,
            DateTime fechaVencimiento,
            decimal montoPagado,
            string metodoPago,
            long registradoPor,
            string observaciones,
            string tipoPlan = "mensual")
        {
            string err = ValidarCampos(socioId, actividadId, montoPagado, metodoPago);
            if (err != null) return (false, err, 0);

            fechaInicio = DateTime.Today;
            fechaVencimiento = DateTime.Today.AddDays(31);

            var membresia = new Membresia
            {
                SocioId = socioId,
                ActividadId = actividadId,
                InstructorId = instructorId,
                FechaInicio = fechaInicio.Date,
                FechaVencimiento = fechaVencimiento.Date,
                TipoPlan = tipoPlan ?? "mensual",
                MontoPagado = montoPagado,
                MetodoPago = metodoPago ?? "efectivo",
                RegistradoPor = registradoPor,
                Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim()
            };

            try
            {
                long id = _dao.InsertarMembresia(membresia);
                if (id <= 0) return (false, "No se pudo registrar la membresía.", 0);
                return (true, "Membresía registrada y cuota cobrada correctamente.", id);
            }
            catch (Exception ex)
            {
                return (false, "Error al registrar la membresía.\n" + ex.Message, 0);
            }
        }

        // ──────────────────────────────────────────────────────
        // MODIFICAR (datos secundarios)
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Modificar(
            long id,
            long? instructorId,
            DateTime fechaVencimiento,
            string observaciones,
            long registradoPor = 0,
            long? actividadId = null,
            decimal? montoPagado = null,
            string tipoPlan = null,
            string metodoPago = null)
        {
            if (id <= 0) return (false, "ID de membresía inválido.");
            if (montoPagado.HasValue && montoPagado.Value <= 0)
                return (false, "El monto debe ser mayor a $0.");

            try
            {
                bool ok = _dao.ModificarMembresia(
                    id,
                    instructorId,
                    actividadId,
                    fechaVencimiento.Date,
                    string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim(),
                    registradoPor,
                    montoPagado,
                    string.IsNullOrWhiteSpace(tipoPlan) ? null : tipoPlan,
                    string.IsNullOrWhiteSpace(metodoPago) ? null : metodoPago);

                return ok
                    ? (true, "Membresía actualizada correctamente.")
                    : (false, "No se encontró la membresía para actualizar.");
            }
            catch (Exception ex)
            {
                // El SP lanza error si la fecha retrocede — lo propagamos tal cual
                return (false, ex.Message.Contains("solo puede avanzar")
                    ? "La fecha de vencimiento no puede retroceder. Los días solo pueden aumentar."
                    : "Error al actualizar.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // CAMBIAR ESTADO
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) CambiarEstado(long id, string nuevoEstado)
        {
            string[] valores = { "activa", "vencida", "cancelada", "suspendida" };
            bool valido = false;
            foreach (var v in valores)
                if (v == nuevoEstado) { valido = true; break; }

            if (!valido) return (false, "Estado inválido.");

            try
            {
                bool ok = _dao.CambiarEstadoMembresia(id, nuevoEstado);
                return ok
                    ? (true, "Estado de la membresía actualizado a '" + nuevoEstado + "'.")
                    : (false, "No se encontró la membresía.");
            }
            catch (Exception ex)
            {
                return (false, "Error al cambiar estado.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // RENOVAR (+30 días + cobra)
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, DateTime? nuevaFecha) Renovar(
            long id,
            decimal monto,
            string metodoPago,
            long registradoPor,
            int diasASumar = 30)
        {
            if (monto <= 0)
                return (false, "El monto debe ser mayor a $0.", null);

            if (diasASumar < 1 || diasASumar > 365)
                return (false, "Los días a sumar deben estar entre 1 y 365.", null);

            try
            {
                DateTime? nueva = _dao.RenovarMembresia(id, monto, metodoPago ?? "efectivo",
                                                       registradoPor, diasASumar);
                return nueva.HasValue
                    ? (true, "Membresía renovada hasta " + nueva.Value.ToString("dd/MM/yyyy") + ".", nueva)
                    : (false, "No se pudo renovar la membresía.", null);
            }
            catch (Exception ex)
            {
                return (false, "Error al renovar.\n" + ex.Message, null);
            }
        }

        // ──────────────────────────────────────────────────────
        // CANCELAR (alias de cambiar estado a "cancelada")
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Cancelar(long id, long registradoPor = 0)
        {
            try
            {
                bool ok = _dao.EliminarMembresia(id, registradoPor);
                return ok
                    ? (true, "Membresía cancelada.")
                    : (false, "No se encontró la membresía.");
            }
            catch (Exception ex)
            {
                return (false, "Error al cancelar.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // COMBOS PARA EL FORMULARIO
        // ──────────────────────────────────────────────────────
        public List<SocioComboItem> ListarSociosParaCombo()
        {
            try { return _dao.ListarSociosParaCombo(); }
            catch { return new List<SocioComboItem>(); }
        }

        public List<ActividadComboItem> ListarActividadesParaCombo()
        {
            try { return _dao.ListarActividadesParaCombo(); }
            catch { return new List<ActividadComboItem>(); }
        }

        public List<InstructorComboItem> ListarInstructoresParaCombo()
        {
            try { return _dao.ListarInstructoresParaCombo(); }
            catch { return new List<InstructorComboItem>(); }
        }

        // ──────────────────────────────────────────────────────
        // VALIDACIONES
        // ──────────────────────────────────────────────────────
        private string ValidarCampos(long socioId, long actividadId,
                                     decimal monto, string metodoPago)
        {
            if (socioId <= 0) return "Tenés que seleccionar un socio.";
            if (actividadId <= 0) return "Tenés que seleccionar una actividad.";

            if (monto <= 0)
                return "El monto pagado debe ser mayor a $0.";

            if (monto > 9999999)
                return "El monto no es válido (muy alto).";

            string[] metodos = { "efectivo", "transferencia", "tarjeta", "otro" };
            bool metodoValido = false;
            foreach (var m in metodos)
                if (m == metodoPago) { metodoValido = true; break; }

            if (!metodoValido)
                return "Método de pago inválido.";

            return null;
        }
    }
}