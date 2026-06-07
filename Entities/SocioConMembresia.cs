// ============================================================
//  CAPA: Entities
//  Archivo: SocioConMembresia.cs
//
//  Representa un socio con datos de su membresía.
//  Una fila por cada membresía del socio.
// ============================================================

using System;

namespace Entities
{
    public class SocioConMembresia
    {
        // Campos del socio
        public long Id { get; set; }
        public int NumeroSocio { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Dni { get; set; }
        public string DniPin { get; set; }
        public byte[] Foto { get; set; }
        public DateTime? FechaNacimiento { get; set; }
        public string Sexo { get; set; }
        public string Telefono { get; set; }
        public string Domicilio { get; set; }
        public string Profesion { get; set; }
        public string Email { get; set; }
        public string ComoNosConocio { get; set; }
        public string Observaciones { get; set; }
        public bool Activo { get; set; }
        public long? RegistradoPor { get; set; }
        public string RegistradoPorNombre { get; set; }
        public DateTime CreadoEn { get; set; }
        public DateTime ActualizadoEn { get; set; }

        // Campos de la membresía
        public long MembresiaId { get; set; }
        public string ActividadNombre { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public string MembresiaEstado { get; set; }
        public DateTime? UltimaAsistencia { get; set; }
        public int? DiasSinAsistir { get; set; }
        public string InstructorNombre { get; set; }

        // Propiedades calculadas
        public string NombreCompleto => $"{Apellido}, {Nombre}";

        public string SexoTexto =>
            Sexo == "M" ? "Masculino" :
            Sexo == "F" ? "Femenino"  : "Otro";

        public string DiasSinAsistirTexto =>
            DiasSinAsistir.HasValue
                ? DiasSinAsistir.Value + " días"
                : "—";

        public string EstadoTexto
        {
            get
            {
                switch (MembresiaEstado)
                {
                    case "activa": return "Activo";
                    case "vencida": return "Vencida";
                    case "cancelada": return "Cancelada";
                    case "suspendida": return "Suspendida";
                    default: return MembresiaEstado ?? "Desconocido";
                }
            }
        }

        public string FechaVencimientoTexto
            => FechaVencimiento.HasValue ? FechaVencimiento.Value.ToString("dd/MM/yyyy") : "—";

        public string UltimaAsistenciaTexto
            => UltimaAsistencia.HasValue ? UltimaAsistencia.Value.ToString("dd/MM/yyyy") : "Nunca";

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

        public int? Edad
        {
            get
            {
                if (!FechaNacimiento.HasValue) return null;
                var hoy = DateTime.Today;
                int edad = hoy.Year - FechaNacimiento.Value.Year;
                if (FechaNacimiento.Value.Date > hoy.AddYears(-edad)) edad--;
                return edad;
            }
        }

        public string EdadTexto => Edad.HasValue ? Edad.Value.ToString() : "—";

        public string NumeroFormateado => NumeroSocio.ToString("D4");

        public bool PuedeRenovarActiva
        {
            get { return MembresiaEstado == "activa"; }
        }

        public bool PuedeEditarMembresia
        {
            get { return MembresiaEstado == "activa"; }
        }

        public bool PuedeRenovarVencida
        {
            get { return MembresiaEstado == "vencida"; }
        }

        public bool PuedeCancelarMembresia
        {
            get { return MembresiaEstado == "activa" || MembresiaEstado == "vencida" || MembresiaEstado == "suspendida"; }
        }

        public bool PuedeAltaDesdeCancelada
        {
            get { return MembresiaEstado == "cancelada"; }
        }
    }
}
