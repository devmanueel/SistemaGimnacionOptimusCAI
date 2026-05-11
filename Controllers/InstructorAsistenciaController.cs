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
                return ok
                    ? (true, "Salida registrada.")
                    : (false, "No se pudo registrar la salida.");
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
                return ok
                    ? (true, "Asistencia actualizada.")
                    : (false, "No se encontro la asistencia.");
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
    }
}