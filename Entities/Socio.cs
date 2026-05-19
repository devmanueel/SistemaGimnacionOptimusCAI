// ============================================================
//  CAPA: Entities
//  Archivo: Socio.cs
//
//  Representa a un socio del gimnasio.
//  Campos de la tabla `socios` + propiedades calculadas para UI.
// ============================================================

using System;

namespace Entities
{
    public class Socio
    {
        // ── Campos de la tabla socios ───────────────────────────
        public long Id { get; set; }
        public int NumeroSocio { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Dni { get; set; }
        public string DniPin { get; set; }   // SHA-256 del DNI (acceso rápido)
        public byte[] Foto { get; set; }
        public DateTime? FechaNacimiento { get; set; }
        public string Sexo { get; set; } = "Otro";
        public string Telefono { get; set; }
        public string Domicilio { get; set; }
        public string Profesion { get; set; }
        public string Email { get; set; }
        public string ComoNosConocio { get; set; }
        public string Observaciones { get; set; }
        public bool Activo { get; set; } = true;
        public long? RegistradoPor { get; set; }
        public string RegistradoPorNombre { get; set; }   // viene del JOIN
        public DateTime CreadoEn { get; set; }
        public DateTime ActualizadoEn { get; set; }

        // ── Propiedades calculadas (NO se guardan en la BD) ─────

        /// <summary>"Apellido, Nombre" — usado en el DataGrid.</summary>
        public string NombreCompleto => $"{Apellido}, {Nombre}";

        /// <summary>"#0042" — número con padding para mostrar.</summary>
        public string NumeroFormateado => "#" + NumeroSocio.ToString("D4");

        /// <summary>Edad calculada a partir de la fecha de nacimiento.</summary>
        public int? Edad
        {
            get
            {
                if (FechaNacimiento == null) return null;
                var hoy = DateTime.Today;
                int edad = hoy.Year - FechaNacimiento.Value.Year;
                if (FechaNacimiento.Value.Date > hoy.AddYears(-edad)) edad--;
                return edad;
            }
        }

        public string EdadTexto => Edad.HasValue ? Edad.Value + " años" : "—";

        public string SexoTexto
        {
            get
            {
                if (Sexo == "M") return "Masculino";
                if (Sexo == "F") return "Femenino";
                return "Otro";
            }
        }

        public string EstadoTexto => Activo ? "Activo" : "Inactivo";

        /// <summary>Indica si tiene huella registrada (PIN distinto del default).</summary>
        public bool TienePin => !string.IsNullOrEmpty(DniPin) && DniPin.Length == 64;

        public override string ToString() => NombreCompleto;
    }

    public class SocioInactivo
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Dni { get; set; }
        public int NumeroSocio { get; set; }
        public byte[] Foto { get; set; }
        public DateTime? UltimaAsistencia { get; set; }
        public int DiasInactivo { get; set; }

        public string NombreCompleto => Nombre + " " + Apellido;
        public string NumeroSocioFormateado => "#" + NumeroSocio.ToString("D4");
        public string UltimaAsistenciaTexto =>
            UltimaAsistencia.HasValue
                ? UltimaAsistencia.Value.ToString("dd/MM/yyyy")
                : "Nunca asistió";
        public string DiasInactivoTexto =>
            DiasInactivo >= 999 ? "Sin registros" : DiasInactivo + " días sin asistir";
    }
}