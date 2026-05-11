// Entities/Rutina.cs — C# 7.3
using System;
using System.Collections.Generic;

namespace Entities
{
    public class Rutina
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public string Detalles { get; set; }
        public byte DuracionSemanas { get; set; } = 4;
        public long CreadoPor { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime CreadoEn { get; set; }
        public DateTime ActualizadoEn { get; set; }

        // Joineados
        public string CreadorNombre { get; set; }
        public int TotalBloques { get; set; }
        public int TotalEjercicios { get; set; }
        public int TotalAsignaciones { get; set; }

        // Detalle (cargado a demanda)
        public List<RutinaBloque> Bloques { get; set; } = new List<RutinaBloque>();

        // Calculadas
        public string DuracionTexto
        {
            get
            {
                if (DuracionSemanas == 0) return "Sin duración";
                if (DuracionSemanas == 1) return "1 semana";
                return DuracionSemanas + " semanas";
            }
        }

        public string ResumenTexto
        {
            get
            {
                string b = TotalBloques == 1 ? "1 bloque" : TotalBloques + " bloques";
                string e = TotalEjercicios == 1 ? "1 ejercicio" : TotalEjercicios + " ejercicios";
                return b + " · " + e;
            }
        }

        public string AsignacionesTexto
        {
            get
            {
                if (TotalAsignaciones == 0) return "Sin asignar";
                if (TotalAsignaciones == 1) return "1 socio";
                return TotalAsignaciones + " socios";
            }
        }

        public string EstadoTexto => Activo ? "Activa" : "Inactiva";
        public string ActualizadoTexto => "Editada " + ActualizadoEn.ToString("dd/MM/yyyy");
    }

    // ──────────────────────────────────────────────────────────
    public class RutinaBloque
    {
        public long Id { get; set; }
        public long RutinaId { get; set; }
        public string Nombre { get; set; }
        public byte Orden { get; set; }

        public List<RutinaEjercicio> Ejercicios { get; set; } = new List<RutinaEjercicio>();

        public string CantidadEjerciciosTexto
        {
            get
            {
                int c = Ejercicios != null ? Ejercicios.Count : 0;
                if (c == 0) return "Sin ejercicios";
                if (c == 1) return "1 ejercicio";
                return c + " ejercicios";
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    public class RutinaEjercicio
    {
        public long Id { get; set; }
        public long BloqueId { get; set; }
        public string Nombre { get; set; }
        public byte? Series { get; set; }
        public string Repeticiones { get; set; }
        public string Peso { get; set; }
        public short? DescansoSeg { get; set; }
        public string Notas { get; set; }
        public string LinkVideo { get; set; }
        public byte Orden { get; set; }

        // Calculadas
        public string SeriesTexto
            => Series.HasValue ? Series.Value + "x" : "—";

        public string DescansoTexto
        {
            get
            {
                if (!DescansoSeg.HasValue) return "—";
                int s = DescansoSeg.Value;
                if (s < 60) return s + "s";
                int m = s / 60;
                int rest = s % 60;
                return rest == 0 ? m + " min" : m + "m " + rest + "s";
            }
        }

        /// <summary>Texto compacto: "4x10 · 50kg · 90s"</summary>
        public string ResumenCompacto
        {
            get
            {
                var parts = new List<string>();
                if (Series.HasValue && !string.IsNullOrEmpty(Repeticiones))
                    parts.Add(Series.Value + "x" + Repeticiones);
                else if (Series.HasValue)
                    parts.Add(Series.Value + " series");
                else if (!string.IsNullOrEmpty(Repeticiones))
                    parts.Add(Repeticiones + " reps");

                if (!string.IsNullOrEmpty(Peso)) parts.Add(Peso);
                if (DescansoSeg.HasValue && DescansoSeg.Value > 0) parts.Add(DescansoTexto);

                return parts.Count == 0 ? "—" : string.Join(" · ", parts);
            }
        }

        public bool TieneVideo => !string.IsNullOrEmpty(LinkVideo);
    }

    // ──────────────────────────────────────────────────────────
    public class RutinaAsignacion
    {
        public long Id { get; set; }
        public long RutinaId { get; set; }
        public long SocioId { get; set; }
        public long AsignadoPor { get; set; }
        public bool EnviadoWp { get; set; }
        public DateTime AsignadoEn { get; set; }

        // Joineados
        public string SocioNombre { get; set; }
        public int? NumeroSocio { get; set; }
        public byte[] SocioFoto { get; set; }
        public string AsignadoPorNombre { get; set; }

        public string FechaTexto => AsignadoEn.ToString("dd/MM/yyyy");
        public string NumeroSocioTexto => NumeroSocio.HasValue ? "#" + NumeroSocio.Value.ToString("D4") : "—";
    }

    // ──────────────────────────────────────────────────────────
    public class EstadisticasRutinas
    {
        public int Total { get; set; }
        public int Activas { get; set; }
        public int TotalEjercicios { get; set; }
        public int TotalAsignaciones { get; set; }
        public int SociosAsignados { get; set; }
    }
}