// Controllers/TurnoController.cs — C# 7.3
using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class TurnoController
    {
        private readonly TurnoDao _dao = new TurnoDao();
        private readonly ActividadDao _actDao = new ActividadDao();
        private readonly UsuarioDao _usuDao = new UsuarioDao();

        public List<Turno> ObtenerTurnos()
        {
            try { return _dao.ObtenerTurnos(); }
            catch (Exception ex) { throw new Exception("Error al cargar turnos.\n" + ex.Message); }
        }

        public List<Turno> BuscarTurnos(string texto, long? actividadId, byte? dia, bool soloActivos)
        {
            try { return _dao.BuscarTurnos(texto, actividadId, dia, soloActivos); }
            catch (Exception ex) { throw new Exception("Error en busqueda.\n" + ex.Message); }
        }

        public Turno ObtenerPorId(long id)
        {
            try { return _dao.ObtenerTurnoPorId(id); }
            catch (Exception ex) { throw new Exception("No se encontro el turno.\n" + ex.Message); }
        }

        /// <summary>Lista las actividades activas para el ComboBox.</summary>
        public List<Actividad> ListarActividadesParaCombo()
        {
            try { return _actDao.ObtenerActividades(); }
            catch { return new List<Actividad>(); }
        }

        /// <summary>Lista los usuarios activos (instructores potenciales).</summary>
        public List<Usuario> ListarInstructoresParaCombo()
        {
            try { return _usuDao.ObtenerUsuarios(); }
            catch { return new List<Usuario>(); }
        }

        public EstadisticasTurnos ObtenerEstadisticas()
        {
            try { return _dao.ObtenerEstadisticas(); }
            catch { return new EstadisticasTurnos(); }
        }

        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) Insertar(
            long actividadId, long? instructorId, byte diaSemana,
            TimeSpan horaInicio, TimeSpan horaFin, short cupoMaximo)
        {
            string err = Validar(actividadId, diaSemana, horaInicio, horaFin, cupoMaximo);
            if (err != null) return (false, err, 0);

            var t = new Turno
            {
                ActividadId = actividadId,
                InstructorId = instructorId,
                DiaSemana = diaSemana,
                HoraInicio = horaInicio,
                HoraFin = horaFin,
                CupoMaximo = cupoMaximo
            };

            try
            {
                long id = _dao.InsertarTurno(t);
                if (id <= 0) return (false, "No se pudo guardar el turno.", 0);
                return (true, "Turno creado correctamente.", id);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, 0);
            }
        }

        public (bool ok, string mensaje) Modificar(
            long id, long actividadId, long? instructorId, byte diaSemana,
            TimeSpan horaInicio, TimeSpan horaFin, short cupoMaximo)
        {
            string err = Validar(actividadId, diaSemana, horaInicio, horaFin, cupoMaximo);
            if (err != null) return (false, err);

            var t = new Turno
            {
                Id = id,
                ActividadId = actividadId,
                InstructorId = instructorId,
                DiaSemana = diaSemana,
                HoraInicio = horaInicio,
                HoraFin = horaFin,
                CupoMaximo = cupoMaximo
            };

            try
            {
                bool ok = _dao.ModificarTurno(t);
                return ok
                    ? (true, "Turno actualizado correctamente.")
                    : (false, "No se encontro el turno.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool ok, string mensaje) CambiarEstado(long id, bool nuevoEstado)
        {
            try
            {
                bool ok = _dao.CambiarEstado(id, nuevoEstado);
                string accion = nuevoEstado ? "activado" : "desactivado";
                return ok
                    ? (true, "Turno " + accion + ".")
                    : (false, "No se encontro el turno.");
            }
            catch (Exception ex) { return (false, "Error al cambiar estado.\n" + ex.Message); }
        }

        public (bool ok, string mensaje) Eliminar(long id)
        {
            try
            {
                bool ok = _dao.EliminarTurno(id);
                return ok
                    ? (true, "Turno eliminado.")
                    : (false, "No se pudo eliminar.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private string Validar(long actividadId, byte diaSemana,
                               TimeSpan horaInicio, TimeSpan horaFin, short cupoMaximo)
        {
            if (actividadId <= 0) return "Tenes que elegir una actividad.";
            if (diaSemana < 1 || diaSemana > 7) return "Dia de semana invalido.";
            if (horaInicio >= horaFin) return "La hora de inicio debe ser anterior a la hora de fin.";
            if ((horaFin - horaInicio).TotalMinutes < 15) return "El turno debe durar al menos 15 minutos.";
            if (cupoMaximo < 1) return "El cupo debe ser al menos 1.";
            if (cupoMaximo > 200) return "El cupo no puede superar 200.";
            return null;
        }
    }
}