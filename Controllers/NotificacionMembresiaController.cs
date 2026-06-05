using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class NotificacionMembresiaController
    {
        private readonly NotificacionMembresiaDao _dao = new NotificacionMembresiaDao();
        private readonly WhatsappController _whatsappCtrl = new WhatsappController();

        public List<NotificacionMembresia> ObtenerMembresiasPorVencer(int diasAntes = 7)
        {
            if (diasAntes < 0) diasAntes = 0;
            if (diasAntes > 30) diasAntes = 30;

            try
            {
                return _dao.ObtenerMembresiasPorVencer(diasAntes);
            }
            catch (Exception ex)
            {
                throw new Exception("No se pudieron cargar las alertas de membresias por vencer.\n" + ex.Message);
            }
        }

        public string ConstruirMensajeWhatsapp(NotificacionMembresia alerta)
        {
            if (alerta == null) return string.Empty;

            return "Hola " + (alerta.SocioNombre ?? "") + "!\n\n" +
                   "Te recordamos que tu membresia de " + (alerta.ActividadNombre ?? "tu actividad") +
                   " (" + alerta.NumeroSocioTexto + ") vence el " +
                   alerta.FechaVencimiento.ToString("dd/MM/yyyy") + ".\n\n" +
                   "Te esperamos para renovarla y seguir entrenando.\n\n" +
                   "_OptimusCAI Gym_";
        }

        public string ConstruirUrlWhatsapp(NotificacionMembresia alerta)
        {
            if (alerta == null) return string.Empty;
            return _whatsappCtrl.ConstruirUrlWhatsapp(alerta.Telefono, ConstruirMensajeWhatsapp(alerta));
        }
    }
}
