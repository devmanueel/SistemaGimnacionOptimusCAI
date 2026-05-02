// ============================================================
//  CAPA: Entities
//  Archivo: Actividad.cs
//
//  Representa una actividad/plan del gimnasio.
//  Ej: "Gimnasio 3 Veces Por Semana", "Boxeo Todos Los Días".
//  Sin DataAnnotations. Compatible con C# 7.3.
// ============================================================

using System;

namespace Entities
{
    public class Actividad
    {
        // ── Campos de la tabla actividades ────────────────────
        public long Id { get; set; }
        public string Nombre { get; set; }
        public string Tipo { get; set; } = "mensual";  // "mensual" o "mensual_con_clases"
        public int DiasSesiones { get; set; }               // Cantidad de días o clases
        public string DiasSemana { get; set; }               // JSON: "[1,3,5]" o null
        public decimal Precio { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime CreadoEn { get; set; }

        // ── Viene de la subconsulta en el SP ──────────────────
        public int CantSocios { get; set; }   // Socios con membresía activa

        // ── Propiedades calculadas (para la UI) ───────────────

        /// <summary>"Mensual" o "Mensual con clases" — para el DataGrid.</summary>
        public string TipoTexto
        {
            get
            {
                if (Tipo == "mensual") return "Mensual";
                if (Tipo == "mensual_con_clases") return "Mensual con clases";
                return Tipo ?? "—";
            }
        }

        /// <summary>"2 días", "3 días", "1 clase" — para el DataGrid.</summary>
        public string DiasTexto
        {
            get
            {
                if (Tipo == "mensual_con_clases")
                    return DiasSesiones == 1
                        ? DiasSesiones + " clase"
                        : DiasSesiones + " clases";

                return DiasSesiones + " días";
            }
        }

        /// <summary>"$25.000,00" — precio formateado para Argentina.</summary>
        public string PrecioTexto => "$" + Precio.ToString("N0");

        public string EstadoTexto => Activo ? "Activa" : "Inactiva";

        /// <summary>"10 socios" — cantidad de socios activos.</summary>
        public string SociosTexto
        {
            get
            {
                if (CantSocios == 0) return "Sin socios";
                if (CantSocios == 1) return "1 socio";
                return CantSocios + " socios";
            }
        }

        public override string ToString() => Nombre;
    }
}