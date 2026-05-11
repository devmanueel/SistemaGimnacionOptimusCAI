// Entities/Turno.cs — C# 7.3
using System;

namespace Entities
{
    public class Turno
    {
        public long Id { get; set; }
        public long ActividadId { get; set; }
        public long? InstructorId { get; set; }
        public byte DiaSemana { get; set; }   // 1=Lun, 7=Dom
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public short CupoMaximo { get; set; } = 30;
        public bool Activo { get; set; } = true;

        // Joineados
        public string ActividadNombre { get; set; }
        public string InstructorNombre { get; set; }

        // Calculadas
        public string DiaTexto
        {
            get
            {
                switch (DiaSemana)
                {
                    case 1: return "Lunes";
                    case 2: return "Martes";
                    case 3: return "Miércoles";
                    case 4: return "Jueves";
                    case 5: return "Viernes";
                    case 6: return "Sábado";
                    case 7: return "Domingo";
                    default: return "—";
                }
            }
        }

        public string DiaCorto
        {
            get
            {
                switch (DiaSemana)
                {
                    case 1: return "LUN";
                    case 2: return "MAR";
                    case 3: return "MIÉ";
                    case 4: return "JUE";
                    case 5: return "VIE";
                    case 6: return "SÁB";
                    case 7: return "DOM";
                    default: return "—";
                }
            }
        }

        public string HoraInicioTexto => HoraInicio.ToString(@"hh\:mm");
        public string HoraFinTexto => HoraFin.ToString(@"hh\:mm");
        public string RangoHorario => HoraInicioTexto + " — " + HoraFinTexto;

        /// <summary>Duración en minutos.</summary>
        public int DuracionMinutos => (int)(HoraFin - HoraInicio).TotalMinutes;

        public string DuracionTexto
        {
            get
            {
                int min = DuracionMinutos;
                if (min < 60) return min + " min";
                if (min % 60 == 0) return (min / 60) + " h";
                return (min / 60) + "h " + (min % 60) + "m";
            }
        }

        public string EstadoTexto => Activo ? "Activo" : "Inactivo";
        public bool SinInstructor => !InstructorId.HasValue;
        public string CupoTexto => CupoMaximo + " lugares";
    }

    public class EstadisticasTurnos
    {
        public int Total { get; set; }
        public int Activos { get; set; }
        public int SinInstructor { get; set; }
        public int CupoTotal { get; set; }
    }
}