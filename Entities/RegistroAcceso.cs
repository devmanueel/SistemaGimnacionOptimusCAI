// ============================================================
//  CAPA: Entities
//  Archivo: RegistroAcceso.cs
//
//  Representa un intento de ingreso al gimnasio (permitido o no).
//  Compatible con C# 7.3.
// ============================================================

using System;

namespace Entities
{
    public class RegistroAcceso
    {
        // ── Campos directos ───────────────────────────────────
        public long Id { get; set; }
        public long SocioId { get; set; }
        public long? MembresiaId { get; set; }
        public string MetodoAcceso { get; set; }   // huella / dni_pin / manual
        public string Resultado { get; set; }   // permitido / denegado_*
        public DateTime AccedidoEn { get; set; }

        // ── Datos joineados ───────────────────────────────────
        public int NumeroSocio { get; set; }
        public string SocioNombre { get; set; }
        public byte[] SocioFoto { get; set; }
        public string SocioDni { get; set; }
        public string ActividadNombre { get; set; }

        // ── Propiedades calculadas ────────────────────────────

        public bool EsPermitido => Resultado == "permitido";

        public string NumeroSocioFormateado => "#" + NumeroSocio.ToString("D4");

        /// <summary>"14:23" — solo la hora del acceso.</summary>
        public string HoraAcceso => AccedidoEn.ToString("HH:mm");

        /// <summary>"15/12 14:23" — fecha y hora corta.</summary>
        public string FechaCorta => AccedidoEn.ToString("dd/MM HH:mm");

        /// <summary>"Permitido" o motivo del rechazo en humano.</summary>
        public string ResultadoTexto
        {
            get
            {
                if (Resultado == "permitido") return "Permitido";
                if (Resultado == "denegado_huella") return "Huella inválida";
                if (Resultado == "denegado_vencimiento") return "Membresía vencida";
                if (Resultado == "denegado_dia") return "Día no permitido";
                if (Resultado == "denegado_socio_inactivo") return "Socio inactivo";
                if (Resultado != null && Resultado.StartsWith("denegado_")) return "Acceso denegado";
                return Resultado ?? "—";
            }
        }

        /// <summary>"Huella" / "DNI / PIN" / "Manual"</summary>
        public string MetodoTexto
        {
            get
            {
                if (MetodoAcceso == "huella") return "Huella";
                if (MetodoAcceso == "dni_pin") return "DNI / PIN";
                if (MetodoAcceso == "manual") return "Manual";
                return MetodoAcceso ?? "—";
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    //  RESULTADO DE VALIDACIÓN
    //  Lo que devuelve sp_ValidarAccesoPorDni para mostrar la
    //  "tarjeta de acceso" en pantalla con feedback inmediato.
    // ──────────────────────────────────────────────────────────
    public class ResultadoValidacion
    {
        public long? SocioId { get; set; }
        public string Resultado { get; set; }   // permitido / denegado_*
        public string Mensaje { get; set; }
        public string SocioNombre { get; set; }
        public int? NumeroSocio { get; set; }
        public byte[] Foto { get; set; }
        public string ActividadNombre { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public long? RegistroId { get; set; }

        public bool EsPermitido => Resultado == "permitido";

        public string NumeroSocioFormateado
            => NumeroSocio.HasValue ? "#" + NumeroSocio.Value.ToString("D4") : "—";

        public string FechaVencimientoTexto
            => FechaVencimiento.HasValue ? FechaVencimiento.Value.ToString("dd/MM/yyyy") : "—";

        /// <summary>Días que faltan para que venza la membresía.</summary>
        public int? DiasParaVencer
        {
            get
            {
                if (!FechaVencimiento.HasValue) return null;
                return (FechaVencimiento.Value.Date - DateTime.Today).Days;
            }
        }

        public string DiasParaVencerTexto
        {
            get
            {
                if (!DiasParaVencer.HasValue) return "—";
                int d = DiasParaVencer.Value;
                if (d == 0) return "Vence hoy";
                if (d == 1) return "Vence mañana";
                if (d > 1) return "Vence en " + d + " días";
                return "Venció hace " + Math.Abs(d) + " días";
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    //  ESTADÍSTICAS DEL DÍA
    // ──────────────────────────────────────────────────────────
    public class EstadisticasAcceso
    {
        public int PermitidosHoy { get; set; }
        public int DenegadosHoy { get; set; }
        public int SociosUnicosHoy { get; set; }
        public int AccesosSemana { get; set; }
    }
}