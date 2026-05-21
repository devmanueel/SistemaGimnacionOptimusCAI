// ============================================================
//  CAPA: Entities
//  Archivo: Membresia.cs
//
//  Representa una cuota cobrada al socio para una actividad.
//  Incluye datos joineados de socio, actividad e instructor
//  para mostrar todo en el DataGrid sin queries extra.
//  Compatible con C# 7.3.
// ============================================================

using System;

namespace Entities
{
    public class Membresia
    {
        // ── Campos directos de la tabla membresias ───────────
        public long Id { get; set; }
        public long SocioId { get; set; }
        public long ActividadId { get; set; }
        public long? InstructorId { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public decimal MontoPagado { get; set; }
        public string MetodoPago { get; set; } = "efectivo";
        public string Estado { get; set; } = "activa";
        public string TipoPlan { get; set; } = "mensual";
        public long RegistradoPor { get; set; }
        public string Observaciones { get; set; }
        public DateTime CreadoEn { get; set; }
        public DateTime ActualizadoEn { get; set; }

        // ── Datos joineados (vienen del SP) ───────────────────
        public int NumeroSocio { get; set; }
        public string SocioNombre { get; set; }
        public string SocioDni { get; set; }
        public byte[] SocioFoto { get; set; }
        public string ActividadNombre { get; set; }
        public string ActividadTipo { get; set; }
        public string ActividadCategoria { get; set; }
        public int? ActividadNivel { get; set; }
        public string InstructorNombre { get; set; }
        public string RegistradoPorNombre { get; set; }
        public int DiasParaVencer { get; set; }   // negativo si ya venció

        // ── Propiedades calculadas para la UI ─────────────────

        /// <summary>"#0042" — número de socio formateado.</summary>
        public string NumeroSocioFormateado => "#" + NumeroSocio.ToString("D4");

        /// <summary>"$25.000" — monto formateado.</summary>
        public string MontoTexto => "$" + MontoPagado.ToString("N0");

        /// <summary>"15/12/2025" — fecha de vencimiento.</summary>
        public string FechaVencimientoTexto => FechaVencimiento.ToString("dd/MM/yyyy");

        /// <summary>"01/12/2025" — fecha de inicio.</summary>
        public string FechaInicioTexto => FechaInicio.ToString("dd/MM/yyyy");

        /// <summary>
        /// Texto descriptivo de cuántos días quedan o pasaron desde el vencimiento.
        /// "Vence hoy", "Vence en 5 días", "Venció hace 3 días", etc.
        /// </summary>
        public string DiasParaVencerTexto
        {
            get
            {
                if (DiasParaVencer == 0) return "Vence hoy";
                if (DiasParaVencer == 1) return "Vence mañana";
                if (DiasParaVencer > 1) return "Vence en " + DiasParaVencer + " días";
                if (DiasParaVencer == -1) return "Venció ayer";
                return "Venció hace " + Math.Abs(DiasParaVencer) + " días";
            }
        }

        /// <summary>"Activa" / "Vencida" / "Cancelada" / "Suspendida" — capitalizado.</summary>
        public string EstadoTexto
        {
            get
            {
                if (string.IsNullOrEmpty(Estado)) return "—";
                return char.ToUpper(Estado[0]) + Estado.Substring(1);
            }
        }

        /// <summary>"Por vencer" si está activa y vence en ≤ 7 días.</summary>
        public bool EstaPorVencer
        {
            get
            {
                return Estado == "activa"
                    && DiasParaVencer >= 0
                    && DiasParaVencer <= 7;
            }
        }

        /// <summary>Estado considerando "por vencer" como su propia categoría visual.</summary>
        public string EstadoVisual
        {
            get
            {
                if (Estado == "activa" && EstaPorVencer) return "por_vencer";
                return Estado;
            }
        }

        /// <summary>Texto del método de pago capitalizado.</summary>
        public string MetodoPagoTexto
        {
            get
            {
                if (string.IsNullOrEmpty(MetodoPago)) return "—";
                return char.ToUpper(MetodoPago[0]) + MetodoPago.Substring(1);
            }
        }

        public string TipoPlanTexto
        {
            get
            {
                if (TipoPlan == "clase") return "Clase suelta";
                if (TipoPlan == "semanal") return "Semanal";
                return "Mensual";
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    //  ITEMS LIVIANOS PARA LOS COMBOBOX
    //  No queremos cargar el Socio entero solo para mostrarlo.
    // ──────────────────────────────────────────────────────────

    public class SocioComboItem
    {
        public long Id { get; set; }
        public int NumeroSocio { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Dni { get; set; }

        /// <summary>"#0042 — Pérez, Juan (DNI 12345678)" — para mostrar en el combo.</summary>
        public string TextoCombo
            => "#" + NumeroSocio.ToString("D4") + " — " + Apellido + ", " + Nombre +
               " (DNI " + Dni + ")";

        public override string ToString() => TextoCombo;
    }

    public class ActividadComboItem
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public string Tipo { get; set; }
        public int DiasSesiones { get; set; }
        public decimal Precio { get; set; }
        public string Categoria { get; set; }
        public int? Nivel { get; set; }

        /// <summary>"Boxeo 3 Vxs — $22.000" — para el combo.</summary>
        public string TextoCombo
            => Nombre + " — $" + Precio.ToString("N0");

        public override string ToString() => TextoCombo;
    }

    public class InstructorComboItem
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }

        public string TextoCombo => Apellido + ", " + Nombre;
        public override string ToString() => TextoCombo;
    }
}