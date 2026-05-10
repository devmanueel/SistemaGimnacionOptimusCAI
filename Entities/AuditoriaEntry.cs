// Entities/Auditoria.cs — C# 7.3
using System;

namespace Entities
{
    public class AuditoriaEntry
    {
        public long Id { get; set; }
        public long ActorId { get; set; }
        public string Accion { get; set; }
        public string Entidad { get; set; }
        public long? EntidadId { get; set; }
        public string Detalle { get; set; }   // JSON crudo
        public DateTime CreadoEn { get; set; }

        // Joineados
        public string ActorNombre { get; set; }
        public byte[] ActorFoto { get; set; }
        public string ActorRol { get; set; }

        // Calculadas
        public string FechaCorta => CreadoEn.ToString("dd/MM HH:mm");
        public string FechaLarga => CreadoEn.ToString("dd/MM/yyyy HH:mm:ss");

        public string AccionTexto
        {
            get
            {
                if (string.IsNullOrEmpty(Accion)) return "—";
                return char.ToUpper(Accion[0]) + Accion.Substring(1);
            }
        }

        public string EntidadTexto
        {
            get
            {
                if (string.IsNullOrEmpty(Entidad)) return "—";
                return char.ToUpper(Entidad[0]) + Entidad.Substring(1);
            }
        }

        /// <summary>"Editó socio #42" / "Creó venta #15".</summary>
        public string ResumenAccion
        {
            get
            {
                string verbo;
                switch ((Accion ?? "").ToLower())
                {
                    case "crear": verbo = "Creó"; break;
                    case "editar": verbo = "Editó"; break;
                    case "modificar": verbo = "Modificó"; break;
                    case "eliminar": verbo = "Eliminó"; break;
                    case "login": verbo = "Inició sesión"; break;
                    case "logout": verbo = "Cerró sesión"; break;
                    case "activar": verbo = "Activó"; break;
                    case "desactivar": verbo = "Desactivó"; break;
                    case "anular": verbo = "Anuló"; break;
                    default: verbo = AccionTexto; break;
                }

                string ent = string.IsNullOrEmpty(Entidad) ? "" : " " + Entidad;
                string id = EntidadId.HasValue ? " #" + EntidadId.Value : "";
                return verbo + ent + id;
            }
        }

        /// <summary>Emoji segun la entidad.</summary>
        public string IconoEntidad
        {
            get
            {
                switch ((Entidad ?? "").ToLower())
                {
                    case "usuario": return "👤";
                    case "socio": return "🏋";
                    case "membresia": return "🎫";
                    case "actividad": return "🥊";
                    case "venta": return "💰";
                    case "producto": return "📦";
                    case "caja": return "💵";
                    case "rutina": return "📋";
                    case "turno": return "📅";
                    case "asistencia": return "✓";
                    case "casillero": return "🔒";
                    case "whatsapp": return "💬";
                    case "sesion": return "🔑";
                    default: return "📝";
                }
            }
        }
    }

    public class EstadisticasAuditoria
    {
        public int Total { get; set; }
        public int Hoy { get; set; }
        public int Mes { get; set; }
        public int UsuariosActivosMes { get; set; }
    }

    public class TopUsuarioAuditoria
    {
        public long? ActorId { get; set; }
        public string Nombre { get; set; }
        public byte[] Foto { get; set; }
        public int Acciones { get; set; }

        public string AccionesTexto
            => Acciones == 1 ? "1 acción" : Acciones + " acciones";
    }
}