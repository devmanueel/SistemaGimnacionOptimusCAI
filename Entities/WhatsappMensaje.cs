// Entities/WhatsappMensaje.cs — C# 7.3
using System;

namespace Entities
{
    public class WhatsappMensaje
    {
        public long Id { get; set; }
        public string Tipo { get; set; } = "automatico";
        public string Disparador { get; set; }
        public long? SocioId { get; set; }
        public string Telefono { get; set; }
        public string Mensaje { get; set; }
        public string Estado { get; set; } = "pendiente";
        public long? EnviadoPor { get; set; }
        public DateTime CreadoEn { get; set; }
        public DateTime? EnviadoEn { get; set; }

        // Joineados
        public string SocioNombre { get; set; }
        public int? NumeroSocio { get; set; }
        public byte[] SocioFoto { get; set; }
        public string EnviadoPorNombre { get; set; }

        // Calculadas
        public string TipoTexto
        {
            get
            {
                switch (Tipo)
                {
                    case "automatico": return "Automatico";
                    case "masivo": return "Masivo";
                    case "rutina": return "Rutina";
                    default: return Tipo;
                }
            }
        }

        public string EstadoTexto
        {
            get
            {
                switch (Estado)
                {
                    case "pendiente": return "Pendiente";
                    case "enviado": return "Enviado";
                    case "error": return "Error";
                    default: return Estado;
                }
            }
        }

        public bool EsPendiente => Estado == "pendiente";
        public bool EsEnviado => Estado == "enviado";
        public bool EsError => Estado == "error";

        public string FechaCreado => CreadoEn.ToString("dd/MM HH:mm");
        public string FechaEnviado => EnviadoEn.HasValue ? EnviadoEn.Value.ToString("dd/MM HH:mm") : "—";

        public string DisparadorTexto
        {
            get
            {
                if (string.IsNullOrEmpty(Disparador)) return "Manual";
                if (Disparador == "vencimiento_membresia") return "Vencimiento de membresía";
                return Disparador;
            }
        }

        public string NumeroSocioTexto
            => NumeroSocio.HasValue ? "#" + NumeroSocio.Value.ToString("D4") : string.Empty;

        /// <summary>Primeras 80 caracteres del mensaje para preview.</summary>
        public string MensajePreview
        {
            get
            {
                if (string.IsNullOrEmpty(Mensaje)) return string.Empty;
                string limpio = Mensaje.Replace("\n", " ").Replace("\r", " ").Trim();
                if (limpio.Length <= 80) return limpio;
                return limpio.Substring(0, 80) + "...";
            }
        }
    }

    public class EstadisticasWhatsapp
    {
        public int Total { get; set; }
        public int Pendientes { get; set; }
        public int Enviados { get; set; }
        public int Errores { get; set; }
        public int EnviadosHoy { get; set; }
        public int EnviadosMes { get; set; }
    }
}