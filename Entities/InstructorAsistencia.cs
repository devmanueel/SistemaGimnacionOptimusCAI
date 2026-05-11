// Entities/InstructorAsistencia.cs — C# 7.3
using System;

namespace Entities
{
    public class InstructorAsistencia
    {
        public long Id { get; set; }
        public long InstructorId { get; set; }
        public long? TurnoId { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan? HoraEntrada { get; set; }
        public TimeSpan? HoraSalida { get; set; }
        public string Observaciones { get; set; }
        public long? RegistradoPor { get; set; }
        public DateTime CreadoEn { get; set; }

        // Joineados
        public string InstructorNombre { get; set; }
        public byte[] InstructorFoto { get; set; }
        public string ActividadNombre { get; set; }
        public byte? DiaSemana { get; set; }
        public TimeSpan? TurnoHoraInicio { get; set; }
        public TimeSpan? TurnoHoraFin { get; set; }
        public string RegistradoPorNombre { get; set; }

        // Calculadas
        public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
        public string FechaCorta => Fecha.ToString("dd/MM");
        public string HoraEntradaTexto => HoraEntrada.HasValue ? HoraEntrada.Value.ToString(@"hh\:mm") : "—";
        public string HoraSalidaTexto => HoraSalida.HasValue ? HoraSalida.Value.ToString(@"hh\:mm") : "—";

        public bool AsistenciaAbierta => HoraEntrada.HasValue && !HoraSalida.HasValue;
        public bool AsistenciaCerrada => HoraEntrada.HasValue && HoraSalida.HasValue;

        public string EstadoTexto
        {
            get
            {
                if (AsistenciaAbierta) return "Abierta";
                if (AsistenciaCerrada) return "Cerrada";
                return "—";
            }
        }

        /// <summary>Duración trabajada (si está cerrada).</summary>
        public string DuracionTexto
        {
            get
            {
                if (!AsistenciaCerrada) return "—";
                TimeSpan d = HoraSalida.Value - HoraEntrada.Value;
                int h = (int)d.TotalHours;
                int m = d.Minutes;
                if (h == 0) return m + " min";
                if (m == 0) return h + " h";
                return h + "h " + m + "m";
            }
        }

        public string TurnoTexto
        {
            get
            {
                if (!TurnoHoraInicio.HasValue) return "Sin turno asignado";
                return TurnoHoraInicio.Value.ToString(@"hh\:mm") + " — " +
                       TurnoHoraFin.Value.ToString(@"hh\:mm");
            }
        }
    }

    /// <summary>Turno del día con info de fichaje (entrada/salida actual).</summary>
    public class TurnoHoy
    {
        public long TurnoId { get; set; }
        public long ActividadId { get; set; }
        public long? InstructorId { get; set; }
        public byte DiaSemana { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public short CupoMaximo { get; set; }
        public string ActividadNombre { get; set; }
        public string InstructorNombre { get; set; }
        public byte[] InstructorFoto { get; set; }

        public long? AsistenciaId { get; set; }
        public TimeSpan? HoraEntrada { get; set; }
        public TimeSpan? HoraSalida { get; set; }

        // Calculadas
        public string RangoHorario => HoraInicio.ToString(@"hh\:mm") + " — " + HoraFin.ToString(@"hh\:mm");
        public string HoraEntradaTexto => HoraEntrada.HasValue ? HoraEntrada.Value.ToString(@"hh\:mm") : "—";
        public string HoraSalidaTexto => HoraSalida.HasValue ? HoraSalida.Value.ToString(@"hh\:mm") : "—";

        public bool SinFichar => !AsistenciaId.HasValue;
        public bool EntradaRegistrada => AsistenciaId.HasValue && HoraEntrada.HasValue && !HoraSalida.HasValue;
        public bool SalidaRegistrada => AsistenciaId.HasValue && HoraSalida.HasValue;
        public bool TieneInstructor => InstructorId.HasValue;

        public string EstadoTexto
        {
            get
            {
                if (!TieneInstructor) return "Sin instructor";
                if (SinFichar) return "Pendiente";
                if (EntradaRegistrada) return "En curso";
                if (SalidaRegistrada) return "Finalizado";
                return "—";
            }
        }
    }

    public class EstadisticasInstructorAsistencias
    {
        public int AsistenciasHoy { get; set; }
        public int AbiertasHoy { get; set; }
        public int InstructoresHoy { get; set; }
        public int AsistenciasMes { get; set; }
    }
}