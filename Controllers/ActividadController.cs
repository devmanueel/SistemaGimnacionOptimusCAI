// ============================================================
//  CAPA: Controllers
//  Archivo: ActividadController.cs
//
//  Validaciones + reglas de negocio para Actividad.
//  Validador está en este mismo namespace (Controllers).
//  Compatible con C# 7.3.
// ============================================================

using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class ActividadController
    {
        private readonly ActividadDao _dao = new ActividadDao();

        // ──────────────────────────────────────────────────────
        // OBTENER / BUSCAR
        // ──────────────────────────────────────────────────────
        public List<Actividad> ObtenerActividades()
        {
            try { return _dao.ObtenerActividades(); }
            catch (Exception ex) { throw new Exception("No se pudieron cargar las actividades.\n" + ex.Message); }
        }

        public List<Actividad> BuscarActividades(string texto, string filtroEstado = "todos")
        {
            try { return _dao.BuscarActividades(texto, filtroEstado); }
            catch (Exception ex) { throw new Exception("Error en la búsqueda.\n" + ex.Message); }
        }

        public Actividad ObtenerPorId(long id)
        {
            try { return _dao.ObtenerActividadPorId(id); }
            catch (Exception ex) { throw new Exception("No se encontró la actividad.\n" + ex.Message); }
        }

        // ──────────────────────────────────────────────────────
        // INSERTAR
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) Insertar(
            string nombre,
            string tipo,
            int diasSesiones,
            string diasSemana,
            decimal precio)
        {
            string err = ValidarCampos(nombre, tipo, diasSesiones, precio);
            if (err != null) return (false, err, 0);

            var actividad = new Actividad
            {
                Nombre = nombre.Trim(),
                Tipo = tipo,
                DiasSesiones = diasSesiones,
                DiasSemana = string.IsNullOrWhiteSpace(diasSemana) ? null : diasSemana.Trim(),
                Precio = precio
            };

            try
            {
                long id = _dao.InsertarActividad(actividad);
                if (id == -1) return (false, "Ya existe una actividad con ese nombre.", 0);
                if (id <= 0) return (false, "No se pudo guardar la actividad.", 0);

                Auditor.Registrar("crear", "actividad", id, new Dictionary<string, object> {
                    { "nombre", nombre }, { "tipo", tipo }, { "precio", precio }
                });

                return (true, "Actividad creada correctamente.", id);
            }
            catch (Exception ex)
            {
                return (false, "Error al insertar.\n" + ex.Message, 0);
            }
        }

        // ──────────────────────────────────────────────────────
        // MODIFICAR
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Modificar(
            long id,
            string nombre,
            string tipo,
            int diasSesiones,
            string diasSemana,
            decimal precio)
        {
            string err = ValidarCampos(nombre, tipo, diasSesiones, precio);
            if (err != null) return (false, err);

            var actividad = new Actividad
            {
                Id = id,
                Nombre = nombre.Trim(),
                Tipo = tipo,
                DiasSesiones = diasSesiones,
                DiasSemana = string.IsNullOrWhiteSpace(diasSemana) ? null : diasSemana.Trim(),
                Precio = precio
            };

            try
            {
                bool ok = _dao.ModificarActividad(actividad);
                if (ok)
                {
                    Auditor.Registrar("modificar", "actividad", id, new Dictionary<string, object> {
                        { "nombre", nombre }, { "tipo", tipo }, { "precio", precio }
                    });
                }
                return ok
                    ? (true, "Actividad actualizada correctamente.")
                    : (false, "No se encontró la actividad.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message.Contains("nombre")
                    ? ex.Message
                    : "Error al actualizar.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // CAMBIAR ESTADO
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) CambiarEstado(long id, bool nuevoEstado)
        {
            try
            {
                bool ok = _dao.CambiarEstadoActividad(id, nuevoEstado);
                string accion = nuevoEstado ? "activada" : "desactivada";
                if (ok)
                {
                    Auditor.Registrar(nuevoEstado ? "activar" : "desactivar", "actividad", id);
                }
                return ok
                    ? (true, $"Actividad {accion} correctamente.")
                    : (false, "No se encontró la actividad.");
            }
            catch (Exception ex)
            {
                return (false, "Error al cambiar estado.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // ELIMINAR
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Eliminar(long id)
        {
            try
            {
                bool ok = _dao.EliminarActividad(id);
                if (ok)
                {
                    Auditor.Registrar("eliminar", "actividad", id);
                }
                return ok
                    ? (true, "Actividad eliminada.")
                    : (false, "No se pudo eliminar la actividad.");
            }
            catch (Exception ex)
            {
                // El SP lanza RAISERROR si tiene membresías
                return (false, ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // VALIDACIONES
        // ──────────────────────────────────────────────────────
        private string ValidarCampos(string nombre, string tipo,
                                     int diasSesiones, decimal precio)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return "El nombre de la actividad es obligatorio.";

            if (nombre.Trim().Length < 3)
                return "El nombre debe tener al menos 3 caracteres.";

            if (nombre.Trim().Length > 150)
                return "El nombre no puede superar los 150 caracteres.";

            if (tipo != "mensual" && tipo != "mensual_con_clases")
                return "El tipo debe ser 'Mensual' o 'Mensual con clases'.";

            if (diasSesiones < 1 || diasSesiones > 7)
                return "La cantidad de días/sesiones debe ser entre 1 y 7.";

            if (precio <= 0)
                return "El precio debe ser mayor a $0.";

            if (precio > 9999999)
                return "El precio no es válido (muy alto).";

            return null;
        }
    }
}