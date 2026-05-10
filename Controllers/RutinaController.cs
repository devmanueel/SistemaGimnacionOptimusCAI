// Controllers/RutinaController.cs — C# 7.3
using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class RutinaController
    {
        private readonly RutinaDao _dao = new RutinaDao();
        private readonly MembresiaController _membreCtrl = new MembresiaController();

        // ── RUTINAS ───────────────────────────────────────────
        public List<Rutina> ObtenerRutinas()
        {
            try { return _dao.ObtenerRutinas(); }
            catch (Exception ex) { throw new Exception("Error al cargar rutinas.\n" + ex.Message); }
        }

        public List<Rutina> BuscarRutinas(string texto, bool soloActivas)
        {
            try { return _dao.BuscarRutinas(texto, soloActivas); }
            catch (Exception ex) { throw new Exception("Error en busqueda.\n" + ex.Message); }
        }

        public Rutina ObtenerConDetalle(long id)
        {
            try { return _dao.ObtenerConDetalle(id); }
            catch (Exception ex) { throw new Exception("No se pudo cargar la rutina.\n" + ex.Message); }
        }

        public EstadisticasRutinas ObtenerEstadisticas()
        {
            try { return _dao.ObtenerEstadisticas(); }
            catch { return new EstadisticasRutinas(); }
        }

        public List<SocioComboItem> ListarSociosParaCombo()
            => _membreCtrl.ListarSociosParaCombo();

        public (bool ok, string mensaje, long nuevoId) InsertarRutina(
            string nombre, string detalles, byte duracionSemanas, long creadoPor)
        {
            string err = ValidarRutina(nombre, duracionSemanas);
            if (err != null) return (false, err, 0);
            if (creadoPor <= 0) return (false, "No hay usuario logueado.", 0);

            try
            {
                long id = _dao.InsertarRutina(
                    nombre.Trim(),
                    string.IsNullOrWhiteSpace(detalles) ? null : detalles.Trim(),
                    duracionSemanas, creadoPor);
                if (id <= 0) return (false, "No se pudo crear la rutina.", 0);
                return (true, "Rutina creada correctamente.", id);
            }
            catch (Exception ex) { return (false, ex.Message, 0); }
        }

        public (bool ok, string mensaje) ModificarRutina(
            long id, string nombre, string detalles, byte duracionSemanas)
        {
            string err = ValidarRutina(nombre, duracionSemanas);
            if (err != null) return (false, err);

            try
            {
                bool ok = _dao.ModificarRutina(id, nombre.Trim(),
                    string.IsNullOrWhiteSpace(detalles) ? null : detalles.Trim(),
                    duracionSemanas);
                return ok ? (true, "Rutina actualizada.") : (false, "No se encontro la rutina.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool ok, string mensaje) EliminarRutina(long id)
        {
            try
            {
                bool ok = _dao.EliminarRutina(id);
                return ok ? (true, "Rutina eliminada con todos sus bloques y ejercicios.")
                          : (false, "No se pudo eliminar.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool ok, string mensaje) CambiarEstado(long id, bool nuevoEstado)
        {
            try
            {
                bool ok = _dao.CambiarEstado(id, nuevoEstado);
                string accion = nuevoEstado ? "activada" : "desactivada";
                return ok ? (true, "Rutina " + accion + ".") : (false, "No se encontro la rutina.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ── BLOQUES ───────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) InsertarBloque(
            long rutinaId, string nombre, byte orden)
        {
            if (rutinaId <= 0) return (false, "Rutina invalida.", 0);
            if (string.IsNullOrWhiteSpace(nombre)) return (false, "El nombre del bloque es obligatorio.", 0);

            try
            {
                long id = _dao.InsertarBloque(rutinaId, nombre.Trim(), orden);
                if (id <= 0) return (false, "No se pudo crear el bloque.", 0);
                return (true, "Bloque agregado.", id);
            }
            catch (Exception ex) { return (false, ex.Message, 0); }
        }

        public (bool ok, string mensaje) ModificarBloque(long id, string nombre, byte orden)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return (false, "Nombre obligatorio.");
            try
            {
                bool ok = _dao.ModificarBloque(id, nombre.Trim(), orden);
                return ok ? (true, "Bloque actualizado.") : (false, "No se encontro el bloque.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool ok, string mensaje) EliminarBloque(long id)
        {
            try
            {
                bool ok = _dao.EliminarBloque(id);
                return ok ? (true, "Bloque eliminado con sus ejercicios.")
                          : (false, "No se pudo eliminar.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ── EJERCICIOS ────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) InsertarEjercicio(RutinaEjercicio ej)
        {
            if (ej.BloqueId <= 0) return (false, "Bloque invalido.", 0);
            if (string.IsNullOrWhiteSpace(ej.Nombre)) return (false, "Nombre obligatorio.", 0);

            ej.Nombre = ej.Nombre.Trim();
            if (!string.IsNullOrEmpty(ej.Repeticiones)) ej.Repeticiones = ej.Repeticiones.Trim();
            if (!string.IsNullOrEmpty(ej.Peso)) ej.Peso = ej.Peso.Trim();
            if (!string.IsNullOrEmpty(ej.Notas)) ej.Notas = ej.Notas.Trim();
            if (!string.IsNullOrEmpty(ej.LinkVideo)) ej.LinkVideo = ej.LinkVideo.Trim();

            try
            {
                long id = _dao.InsertarEjercicio(ej);
                if (id <= 0) return (false, "No se pudo crear el ejercicio.", 0);
                return (true, "Ejercicio agregado.", id);
            }
            catch (Exception ex) { return (false, ex.Message, 0); }
        }

        public (bool ok, string mensaje) ModificarEjercicio(RutinaEjercicio ej)
        {
            if (string.IsNullOrWhiteSpace(ej.Nombre)) return (false, "Nombre obligatorio.");

            ej.Nombre = ej.Nombre.Trim();
            if (!string.IsNullOrEmpty(ej.Repeticiones)) ej.Repeticiones = ej.Repeticiones.Trim();
            if (!string.IsNullOrEmpty(ej.Peso)) ej.Peso = ej.Peso.Trim();
            if (!string.IsNullOrEmpty(ej.Notas)) ej.Notas = ej.Notas.Trim();
            if (!string.IsNullOrEmpty(ej.LinkVideo)) ej.LinkVideo = ej.LinkVideo.Trim();

            try
            {
                bool ok = _dao.ModificarEjercicio(ej);
                return ok ? (true, "Ejercicio actualizado.") : (false, "No se encontro el ejercicio.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool ok, string mensaje) EliminarEjercicio(long id)
        {
            try
            {
                bool ok = _dao.EliminarEjercicio(id);
                return ok ? (true, "Ejercicio eliminado.") : (false, "No se pudo eliminar.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ── ASIGNACIONES ──────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) AsignarRutina(
            long rutinaId, long socioId, long asignadoPor)
        {
            if (rutinaId <= 0) return (false, "Rutina invalida.", 0);
            if (socioId <= 0) return (false, "Tenes que elegir un socio.", 0);
            if (asignadoPor <= 0) return (false, "No hay usuario logueado.", 0);

            try
            {
                long id = _dao.AsignarRutina(rutinaId, socioId, asignadoPor);
                if (id <= 0) return (false, "No se pudo asignar.", 0);
                return (true, "Rutina asignada al socio.", id);
            }
            catch (Exception ex) { return (false, ex.Message, 0); }
        }

        public (bool ok, string mensaje) DesasignarRutina(long id)
        {
            try
            {
                bool ok = _dao.DesasignarRutina(id);
                return ok ? (true, "Asignacion eliminada.") : (false, "No se pudo eliminar.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public List<RutinaAsignacion> AsignacionesDeRutina(long rutinaId)
        {
            try { return _dao.AsignacionesDeRutina(rutinaId); }
            catch { return new List<RutinaAsignacion>(); }
        }

        // ── VALIDACIONES ──────────────────────────────────────
        private string ValidarRutina(string nombre, byte duracionSemanas)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "El nombre es obligatorio.";
            if (nombre.Trim().Length < 2) return "El nombre es muy corto.";
            if (nombre.Trim().Length > 150) return "El nombre es muy largo.";
            if (duracionSemanas < 1 || duracionSemanas > 52) return "La duracion debe estar entre 1 y 52 semanas.";
            return null;
        }
    }
}