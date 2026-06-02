// Controllers/InstructorAsistenciaController.cs — C# 7.3
using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class InstructorAsistenciaController
    {
        private readonly InstructorAsistenciaDao _dao = new InstructorAsistenciaDao();
        private readonly UsuarioDao _usuDao = new UsuarioDao();

        public List<InstructorAsistencia> Obtener(DateTime? desde = null, DateTime? hasta = null)
        {
            try { return _dao.Obtener(desde, hasta); }
            catch (Exception ex) { throw new Exception("Error al cargar asistencias.\n" + ex.Message); }
        }

        public List<InstructorAsistencia> Buscar(string texto, long? instructorId,
                                                  DateTime? desde, DateTime? hasta)
        {
            try { return _dao.Buscar(texto, instructorId, desde, hasta); }
            catch (Exception ex) { throw new Exception("Error en busqueda.\n" + ex.Message); }
        }

        public List<TurnoHoy> ObtenerTurnosDeHoy()
        {
            try { return _dao.ObtenerTurnosDeHoy(); }
            catch { return new List<TurnoHoy>(); }
        }

        public List<Usuario> ListarInstructoresParaCombo()
        {
            try { return _usuDao.ObtenerUsuarios(); }
            catch { return new List<Usuario>(); }
        }

        public EstadisticasInstructorAsistencias ObtenerEstadisticas()
        {
            try { return _dao.ObtenerEstadisticas(); }
            catch { return new EstadisticasInstructorAsistencias(); }
        }

        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) RegistrarEntrada(
            long instructorId, long? turnoId, string observaciones, long registradoPor)
        {
            if (instructorId <= 0) return (false, "Instructor invalido.", 0);
            if (registradoPor <= 0) return (false, "No hay usuario logueado.", 0);

            try
            {
                long id = _dao.RegistrarEntrada(instructorId, turnoId, null,
                    string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim(),
                    registradoPor);
                if (id <= 0) return (false, "No se pudo registrar la entrada.", 0);

                Auditor.Registrar("crear", "asistencia", id, new Dictionary<string, object> {
                    { "instructor_id", instructorId },
                    { "tipo", "entrada" }
                });

                return (true, "Entrada registrada.", id);
            }
            catch (Exception ex) { return (false, ex.Message, 0); }
        }

        public (bool ok, string mensaje) RegistrarSalida(long id)
        {
            if (id <= 0) return (false, "ID invalido.");
            try
            {
                bool ok = _dao.RegistrarSalida(id);
                if (!ok) return (false, "No se pudo registrar la salida.");

                Auditor.Registrar("editar", "asistencia", id, new Dictionary<string, object> {
                    { "tipo", "salida" }
                });

                return (true, "Salida registrada.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool ok, string mensaje) Actualizar(long id, long? turnoId,
            TimeSpan? horaEntrada, TimeSpan? horaSalida, string observaciones)
        {
            if (id <= 0) return (false, "ID invalido.");
            try
            {
                bool ok = _dao.Actualizar(id, turnoId, horaEntrada, horaSalida,
                    string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim());
                if (!ok) return (false, "No se encontro la asistencia.");

                Auditor.Registrar("editar", "asistencia", id, new Dictionary<string, object> {
                    { "corregido_por_admin", true },
                    { "hora_entrada_nueva", horaEntrada?.ToString() ?? "-" },
                    { "hora_salida_nueva",  horaSalida?.ToString()  ?? "-" }
                });

                return (true, "Asistencia actualizada.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool ok, string mensaje) Eliminar(long id)
        {
            if (id <= 0) return (false, "ID invalido.");
            try
            {
                bool ok = _dao.Eliminar(id);
                return ok
                    ? (true, "Asistencia eliminada.")
                    : (false, "No se pudo eliminar.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ──────────────────────────────────────────────────────
        // FICHAJE — validado con DNI del usuario logueado
        // El DNI ingresado debe coincidir con el de la sesión.
        // No se requiere contraseña separada.
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, FichajeResultado resultado) FicharEntrada(string dniIngresado)
        {
            if (string.IsNullOrWhiteSpace(dniIngresado))
                return (false, "Ingresá tu DNI para confirmar tu identidad.", null);

            if (!SesionManager.HaySesion)
                return (false, "No hay sesión activa.", null);

            if (!string.Equals(dniIngresado.Trim(), SesionManager.Dni,
                                StringComparison.OrdinalIgnoreCase))
                return (false, "El DNI ingresado no coincide con tu cuenta.", null);

            try
            {
                var r = _dao.FicharEntrada(SesionManager.UsuarioId);
                if (r == null) return (false, "No se pudo registrar la entrada.", null);

                Auditor.Registrar("crear", "asistencia", r.Id, new Dictionary<string, object> {
                    { "instructor_id", r.InstructorId },
                    { "hora_entrada",  r.HoraEntradaTexto },
                    { "tipo",          "entrada_fichaje" }
                });

                return (true, $"Entrada registrada — {r.HoraEntradaTexto}", r);
            }
            catch (Exception ex) { return (false, ex.Message, null); }
        }

        public (bool ok, string mensaje, FichajeResultado resultado) FicharSalida(string dniIngresado)
        {
            if (string.IsNullOrWhiteSpace(dniIngresado))
                return (false, "Ingresá tu DNI para confirmar tu identidad.", null);

            if (!SesionManager.HaySesion)
                return (false, "No hay sesión activa.", null);

            if (!string.Equals(dniIngresado.Trim(), SesionManager.Dni,
                                StringComparison.OrdinalIgnoreCase))
                return (false, "El DNI ingresado no coincide con tu cuenta.", null);

            try
            {
                var r = _dao.FicharSalida(SesionManager.UsuarioId);
                if (r == null) return (false, "No se pudo registrar la salida.", null);

                Auditor.Registrar("editar", "asistencia", r.Id, new Dictionary<string, object> {
                    { "hora_salida",      r.HoraSalidaTexto },
                    { "horas_trabajadas", r.HorasTrabajadas },
                    { "tipo",             "salida_fichaje" }
                });

                return (true, $"Salida registrada — {r.HorasTrabajadasTexto} trabajados", r);
            }
            catch (Exception ex) { return (false, ex.Message, null); }
        }

        public List<InstructorAsistencia> BuscarPropias(DateTime? desde, DateTime? hasta)
        {
            try { return _dao.BuscarPropias(SesionManager.UsuarioId, desde, hasta); }
            catch (Exception ex) { throw new Exception("Error al cargar tus asistencias.\n" + ex.Message); }
        }

        // ──────────────────────────────────────────────────────
        // TAREA 4 & 5 — Reportes
        // ──────────────────────────────────────────────────────
        public List<ReporteInstructor> ObtenerReporteMensual(int anio, int mes)
        {
            try { return _dao.ObtenerReporteMensual(anio, mes); }
            catch (Exception ex) { throw new Exception("Error al generar reporte mensual.\n" + ex.Message); }
        }

        public List<InstructorAsistencia> ObtenerReporteSemanal(DateTime desde, DateTime hasta)
        {
            try { return _dao.ObtenerReporteSemanal(desde, hasta); }
            catch (Exception ex) { throw new Exception("Error al generar reporte semanal.\n" + ex.Message); }
        }

        public (bool ok, string mensaje, FichajeResultado resultado) FicharInstructorDashboard(string dni)
        {
            if (string.IsNullOrWhiteSpace(dni))
                return (false, "DNI invalido.", null);

            if (!SesionManager.HaySesion)
                return (false, "No hay sesion activa.", null);

            try
            {
                var r = _dao.FicharInstructorDashboard(dni.Trim(), SesionManager.UsuarioId);
                if (r == null || r.Operacion == "no_encontrado")
                    return (false, r != null ? r.Mensaje : "Instructor no encontrado.", null);

                if (r.Operacion == "espera_minima")
                    return (false, r.Mensaje, r);

                Auditor.Registrar(
                    r.Operacion == "entrada" ? "crear" : "editar",
                    "asistencia_dashboard",
                    r.Id,
                    new Dictionary<string, object> {
                        { "instructor_id", r.InstructorId },
                        { "operacion", r.Operacion },
                        { "registrado_por", SesionManager.UsuarioId }
                    }
                );

                string msg = r.Operacion == "entrada"
                    ? $"Entrada registrada — {r.HoraEntradaTexto}"
                    : $"Salida registrada — {r.HorasTrabajadasTexto} trabajados";

                return (true, msg, r);
            }
            catch (Exception ex) { return (false, ex.Message, null); }
        }
    }
}
