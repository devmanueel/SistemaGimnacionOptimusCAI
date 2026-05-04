// ============================================================
//  CAPA: Entities
//  Archivo: Casillero.cs
//
//  Representa un casillero del gimnasio.
//  Estados: libre / ocupado / mantenimiento.
//  Compatible con C# 7.3.
// ============================================================

using System;

namespace Entities
{
    public class Casillero
    {
        // ── Campos directos ───────────────────────────────────
        public long Id { get; set; }
        public short Numero { get; set; }
        public long? SocioId { get; set; }
        public string Estado { get; set; } = "libre";
        public decimal? PrecioMes { get; set; }
        public string Observaciones { get; set; }
        public DateTime? AsignadoEn { get; set; }

        // ── Joineados ─────────────────────────────────────────
        public string SocioNombre { get; set; }
        public int? NumeroSocio { get; set; }
        public string SocioDni { get; set; }
        public byte[] SocioFoto { get; set; }

        // ── Calculadas ────────────────────────────────────────

        /// <summary>"#001" — número formateado.</summary>
        public string NumeroFormateado => "#" + Numero.ToString("D3");

        public bool EsLibre => Estado == "libre";
        public bool EsOcupado => Estado == "ocupado";
        public bool EsMantenimiento => Estado == "mantenimiento";

        /// <summary>"Libre" / "Ocupado" / "Mantenimiento"</summary>
        public string EstadoTexto
        {
            get
            {
                if (Estado == "libre") return "Libre";
                if (Estado == "ocupado") return "Ocupado";
                if (Estado == "mantenimiento") return "Mantenimiento";
                return Estado ?? "—";
            }
        }

        /// <summary>"$3.000" — precio mensual formateado.</summary>
        public string PrecioTexto
            => PrecioMes.HasValue ? "$" + PrecioMes.Value.ToString("N0") : "Sin precio";

        /// <summary>"Pérez, Juan" — nombre del socio asignado o vacío.</summary>
        public string AsignadoA
            => string.IsNullOrEmpty(SocioNombre) ? "—" : SocioNombre;

        /// <summary>"Desde 15/12/2025" — texto de cuándo se asignó.</summary>
        public string AsignadoDesdeTexto
        {
            get
            {
                if (!AsignadoEn.HasValue) return "—";
                int dias = (DateTime.Today - AsignadoEn.Value.Date).Days;
                if (dias == 0) return "Asignado hoy";
                if (dias == 1) return "Asignado ayer";
                return "Hace " + dias + " días";
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    //  ESTADÍSTICAS DE CASILLEROS
    // ──────────────────────────────────────────────────────────
    public class EstadisticasCasilleros
    {
        public int Total { get; set; }
        public int Libres { get; set; }
        public int Ocupados { get; set; }
        public int Mantenimiento { get; set; }
        public decimal IngresoPotencialMes { get; set; }

        /// <summary>Porcentaje de ocupación (0-100).</summary>
        public int PorcentajeOcupacion
        {
            get
            {
                if (Total == 0) return 0;
                return (int)Math.Round((double)Ocupados / Total * 100);
            }
        }

        public string IngresoPotencialTexto => "$" + IngresoPotencialMes.ToString("N0");
    }
}