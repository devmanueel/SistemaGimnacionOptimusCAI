using System;

namespace Entities
{
    public class NotificacionMembresia
    {
        public long MembresiaId { get; set; }
        public long SocioId { get; set; }
        public int NumeroSocio { get; set; }
        public string SocioNombre { get; set; }
        public string Telefono { get; set; }
        public string ActividadNombre { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public int DiasParaVencer { get; set; }

        public string NumeroSocioTexto
        {
            get { return "#" + NumeroSocio.ToString("D4"); }
        }

        public string FechaVencimientoTexto
        {
            get { return FechaVencimiento.ToString("dd/MM/yyyy"); }
        }

        public string EstadoVencimientoTexto
        {
            get
            {
                if (DiasParaVencer == 0) return "Vence hoy";
                if (DiasParaVencer == 1) return "Vence mañana";
                return "Vence en " + DiasParaVencer + " dias";
            }
        }

        public string TelefonoTexto
        {
            get { return string.IsNullOrWhiteSpace(Telefono) ? "Sin telefono" : Telefono; }
        }
    }
}
