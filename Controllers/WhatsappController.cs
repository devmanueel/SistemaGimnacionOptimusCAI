// Controllers/WhatsappController.cs — C# 7.3
using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Controllers
{
    public class WhatsappController
    {
        private readonly WhatsappDao _dao = new WhatsappDao();
        private readonly MembresiaController _membreCtrl = new MembresiaController();

        // ──────────────────────────────────────────────────────
        public List<WhatsappMensaje> ObtenerMensajes(string estado = "todos")
        {
            try { return _dao.ObtenerMensajes(estado); }
            catch (Exception ex) { throw new Exception("Error al cargar mensajes.\n" + ex.Message); }
        }

        public List<WhatsappMensaje> Buscar(string texto, string estado, string tipo)
        {
            try { return _dao.Buscar(texto, estado, tipo); }
            catch (Exception ex) { throw new Exception("Error en busqueda.\n" + ex.Message); }
        }

        public WhatsappMensaje ObtenerPorId(long id)
        {
            try { return _dao.ObtenerPorId(id); }
            catch (Exception ex) { throw new Exception("No se encontro el mensaje.\n" + ex.Message); }
        }

        public List<SocioComboItem> ListarSociosParaCombo()
            => _membreCtrl.ListarSociosParaCombo();

        public EstadisticasWhatsapp ObtenerEstadisticas()
        {
            try { return _dao.ObtenerEstadisticas(); }
            catch { return new EstadisticasWhatsapp(); }
        }

        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) Insertar(
            string tipo, long? socioId, string telefono, string mensaje, long? enviadoPor)
        {
            string err = Validar(tipo, telefono, mensaje);
            if (err != null) return (false, err, 0);

            try
            {
                long id = _dao.Insertar(tipo, "manual", socioId,
                    LimpiarTelefono(telefono), mensaje.Trim(), enviadoPor);
                if (id <= 0) return (false, "No se pudo crear el mensaje.", 0);
                return (true, "Mensaje creado.", id);
            }
            catch (Exception ex) { return (false, ex.Message, 0); }
        }

        public (bool ok, string mensaje) MarcarComoEnviado(long id, long? enviadoPor)
        {
            try
            {
                bool ok = _dao.MarcarComoEnviado(id, enviadoPor);
                return ok ? (true, "Mensaje marcado como enviado.")
                          : (false, "No se encontro el mensaje.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool ok, string mensaje) MarcarComoError(long id)
        {
            try
            {
                bool ok = _dao.MarcarComoError(id);
                return ok ? (true, "Mensaje marcado como error.")
                          : (false, "No se encontro el mensaje.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool ok, string mensaje) Eliminar(long id)
        {
            try
            {
                bool ok = _dao.Eliminar(id);
                return ok ? (true, "Mensaje eliminado.")
                          : (false, "No se pudo eliminar.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ──────────────────────────────────────────────────────
        // GENERAR AVISOS DE VENCIMIENTO
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, int generados) GenerarAvisosVencimiento(
            int diasAntes, long? creadoPor)
        {
            if (diasAntes < 0 || diasAntes > 30)
                return (false, "Los dias deben estar entre 0 y 30.", 0);

            try
            {
                int gen = _dao.GenerarAvisosVencimiento(diasAntes, creadoPor);
                if (gen == 0)
                    return (true, "No hay socios con vencimientos en los proximos " +
                                   diasAntes + " dias (o ya tienen aviso reciente).", 0);

                string s = gen == 1 ? "1 aviso generado." : gen + " avisos generados.";
                return (true, s, gen);
            }
            catch (Exception ex) { return (false, ex.Message, 0); }
        }

        // ──────────────────────────────────────────────────────
        // CONSTRUYE LA URL DE WHATSAPP WEB
        //   https://wa.me/{telefono}?text={mensaje_url_encoded}
        // ──────────────────────────────────────────────────────
        public string ConstruirUrlWhatsapp(string telefono, string mensaje)
        {
            string tel = LimpiarTelefono(telefono);
            string msg = Uri.EscapeDataString(mensaje ?? string.Empty);
            return "https://wa.me/" + tel + "?text=" + msg;
        }

        // ──────────────────────────────────────────────────────
        // PLANTILLAS DE MENSAJE PRE-ARMADAS
        // ──────────────────────────────────────────────────────
        public string PlantillaBienvenida(string nombreSocio)
        {
            return "Hola " + (nombreSocio ?? "") + "! 👋\n\n" +
                   "Bienvenido/a a OptimusCAI Gym! Estamos felices de tenerte con nosotros.\n\n" +
                   "Cualquier consulta sobre horarios, rutinas o pagos, podes escribirnos por aqui.\n\n" +
                   "Te esperamos! 💪\n\n" +
                   "_OptimusCAI Gym_";
        }

        public string PlantillaCumpleanios(string nombreSocio)
        {
            return "Feliz cumpleaños " + (nombreSocio ?? "") + "! 🎉🎂\n\n" +
                   "Todo el equipo de OptimusCAI te desea un dia genial.\n\n" +
                   "Te esperamos para celebrarlo entrenando! 💪\n\n" +
                   "_OptimusCAI Gym_";
        }

        public string PlantillaVencimiento(string nombreSocio, string actividad, DateTime fechaVencim)
        {
            return "Hola " + (nombreSocio ?? "") + "! 👋\n\n" +
                   "Te recordamos que tu membresia de " + (actividad ?? "tu actividad") +
                   " vence el " + fechaVencim.ToString("dd/MM/yyyy") + ".\n\n" +
                   "Te esperamos para renovarla y seguir entrenando! 💪\n\n" +
                   "_OptimusCAI Gym_";
        }

        // ──────────────────────────────────────────────────────
        private string Validar(string tipo, string telefono, string mensaje)
        {
            if (string.IsNullOrEmpty(tipo)) return "Tipo invalido.";
            string[] tipos = { "automatico", "masivo", "rutina" };
            bool ok = false;
            foreach (var t in tipos) if (t == tipo) { ok = true; break; }
            if (!ok) return "Tipo invalido. Debe ser: automatico, masivo o rutina.";

            string telLimpio = LimpiarTelefono(telefono);
            if (telLimpio.Length < 8) return "El telefono es invalido.";

            if (string.IsNullOrWhiteSpace(mensaje)) return "El mensaje no puede estar vacio.";
            if (mensaje.Trim().Length < 5) return "El mensaje es muy corto.";

            return null;
        }

        /// <summary>Saca espacios, guiones y parentesis. Mantiene + opcional.</summary>
        public static string LimpiarTelefono(string telefono)
        {
            if (string.IsNullOrEmpty(telefono)) return string.Empty;
            // Permitir solo digitos (opcionalmente con + adelante)
            string limpio = Regex.Replace(telefono, @"[^\d+]", "");
            // Quitar el + si quedo en medio (solo se acepta al inicio)
            if (limpio.IndexOf('+') > 0)
                limpio = limpio.Replace("+", "");
            return limpio;
        }
    }
}