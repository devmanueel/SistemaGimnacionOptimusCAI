// Controllers/AuditoriaController.cs — C# 7.3
//
// Contiene 2 clases:
//   · AuditoriaController: usado por la pagina de auditoria
//   · Auditor: helper estatico para que cualquier modulo registre logs.
//
// USO desde otros controllers:
//     Auditor.Registrar("crear", "socio", nuevoId, new {
//         nombre = "Juan", dni = "12345678"
//     });

using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;
using System.Text;

namespace Controllers
{
    public class AuditoriaController
    {
        private readonly AuditoriaDao _dao = new AuditoriaDao();

        public List<AuditoriaEntry> Obtener(DateTime? desde = null, DateTime? hasta = null)
        {
            try { return _dao.Obtener(desde, hasta); }
            catch (Exception ex) { throw new Exception("Error al cargar auditoria.\n" + ex.Message); }
        }

        public List<AuditoriaEntry> Buscar(string texto, long? actorId, string entidad,
                                            string accion, DateTime? desde, DateTime? hasta)
        {
            try { return _dao.Buscar(texto, actorId, entidad, accion, desde, hasta); }
            catch (Exception ex) { throw new Exception("Error en busqueda.\n" + ex.Message); }
        }

        public AuditoriaEntry ObtenerPorId(long id)
        {
            try { return _dao.ObtenerPorId(id); }
            catch (Exception ex) { throw new Exception("No se encontro el registro.\n" + ex.Message); }
        }

        public EstadisticasAuditoria ObtenerEstadisticas()
        {
            try { return _dao.ObtenerEstadisticas(); }
            catch { return new EstadisticasAuditoria(); }
        }

        public List<TopUsuarioAuditoria> ObtenerTopUsuarios()
        {
            try { return _dao.ObtenerTopUsuarios(); }
            catch { return new List<TopUsuarioAuditoria>(); }
        }

        // ──────────────────────────────────────────────────────
        // LISTAR ENTIDADES Y ACCIONES PARA LOS FILTROS
        // ──────────────────────────────────────────────────────
        public List<string> ListarEntidades()
        {
            return new List<string>
            {
                "usuario", "socio", "membresia", "actividad",
                "venta", "producto", "caja", "rutina",
                "turno", "asistencia", "casillero", "whatsapp", "sesion"
            };
        }

        public List<string> ListarAcciones()
        {
            return new List<string>
            {
                "crear", "editar", "modificar",
                "eliminar", "anular",
                "activar", "desactivar",
                "login", "logout"
            };
        }

        public List<Usuario> ListarUsuarios()
        {
            try { return new UsuarioDao().ObtenerUsuarios(); }
            catch { return new List<Usuario>(); }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  AUDITOR — helper estatico, llamado desde cualquier modulo
    // ══════════════════════════════════════════════════════════
    public static class Auditor
    {
        private static readonly AuditoriaDao _dao = new AuditoriaDao();

        /// <summary>
        /// Registra un evento en la tabla auditoria.
        /// Usa SesionManager.UsuarioId como actor (o 1 si no hay sesion).
        /// El detalle se serializa a JSON simple desde un Dictionary.
        /// </summary>
        public static void Registrar(string accion, string entidad,
                                      long? entidadId = null,
                                      Dictionary<string, object> detalle = null)
        {
            try
            {
                long actorId = SesionManager.HaySesion ? SesionManager.UsuarioId : 1;
                string json = detalle != null && detalle.Count > 0
                                ? SerializarJson(detalle)
                                : null;

                _dao.Registrar(actorId, accion, entidad, entidadId, json);
            }
            catch
            {
                // El audit nunca debe romper la operacion principal.
                // Falla silenciosamente.
            }
        }

        /// <summary>Variante con texto plano en lugar de Dictionary.</summary>
        public static void Registrar(string accion, string entidad,
                                      long? entidadId, string detalleTexto)
        {
            try
            {
                long actorId = SesionManager.HaySesion ? SesionManager.UsuarioId : 1;
                _dao.Registrar(actorId, accion, entidad, entidadId, detalleTexto);
            }
            catch { }
        }

        // ──────────────────────────────────────────────────────
        // SERIALIZADOR JSON SIMPLE (sin dependencias externas)
        // Maneja: string, numerico, bool, null, DateTime
        // ──────────────────────────────────────────────────────
        private static string SerializarJson(Dictionary<string, object> obj)
        {
            var sb = new StringBuilder();
            sb.Append("{");

            bool primero = true;
            foreach (var kvp in obj)
            {
                if (!primero) sb.Append(",");
                primero = false;

                sb.Append("\"").Append(EscaparJson(kvp.Key)).Append("\":");
                sb.Append(SerializarValor(kvp.Value));
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string SerializarValor(object val)
        {
            if (val == null) return "null";

            if (val is string s) return "\"" + EscaparJson(s) + "\"";
            if (val is bool b) return b ? "true" : "false";
            if (val is DateTime d) return "\"" + d.ToString("yyyy-MM-dd HH:mm:ss") + "\"";
            if (val is decimal dec) return dec.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (val is double dbl) return dbl.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (val is float f) return f.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Numericos enteros y demas
            if (val is int || val is long || val is short || val is byte)
                return val.ToString();

            // Cualquier otra cosa: a string
            return "\"" + EscaparJson(val.ToString()) + "\"";
        }

        private static string EscaparJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}