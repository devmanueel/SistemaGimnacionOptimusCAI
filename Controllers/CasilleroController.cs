// ============================================================
//  CAPA: Controllers
//  Archivo: CasilleroController.cs
//
//  Validaciones + reglas de negocio para Casilleros.
//  Reusa MembresiaController para listar socios en el combo.
//  Compatible con C# 7.3.
// ============================================================

using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class CasilleroController
    {
        private readonly CasilleroDao _dao = new CasilleroDao();

        // Para el combo de socios al asignar — reusamos el de Membresia
        private readonly MembresiaController _membresiaCtrl = new MembresiaController();

        // ──────────────────────────────────────────────────────
        // OBTENER / BUSCAR
        // ──────────────────────────────────────────────────────
        public List<Casillero> ObtenerCasilleros()
        {
            try { return _dao.ObtenerCasilleros(); }
            catch (Exception ex) { throw new Exception("No se pudieron cargar los casilleros.\n" + ex.Message); }
        }

        public Casillero ObtenerPorId(long id)
        {
            try { return _dao.ObtenerCasilleroPorId(id); }
            catch (Exception ex) { throw new Exception("No se encontró el casillero.\n" + ex.Message); }
        }

        public List<SocioComboItem> ListarSociosParaCombo()
            => _membresiaCtrl.ListarSociosParaCombo();

        // ──────────────────────────────────────────────────────
        // CREAR INDIVIDUAL
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) Crear(
            short numero, decimal? precio, string observaciones)
        {
            string err = ValidarCampos(numero, precio);
            if (err != null) return (false, err, 0);

            try
            {
                long id = _dao.CrearCasillero(numero, precio,
                    string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim());

                if (id == -1) return (false, "Ya existe un casillero con el número " + numero + ".", 0);
                if (id <= 0) return (false, "No se pudo crear el casillero.", 0);

                return (true, "Casillero #" + numero.ToString("D3") + " creado.", id);
            }
            catch (Exception ex)
            {
                return (false, "Error al crear.\n" + ex.Message, 0);
            }
        }

        // ──────────────────────────────────────────────────────
        // CREAR EN MASA (rango de números)
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, int creados) CrearEnMasa(
            short desde, short hasta, decimal? precio)
        {
            if (desde < 1 || desde > 9999)
                return (false, "El número inicial debe estar entre 1 y 9999.", 0);
            if (hasta < 1 || hasta > 9999)
                return (false, "El número final debe estar entre 1 y 9999.", 0);
            if (desde > hasta)
                return (false, "El número inicial debe ser menor al final.", 0);
            if (hasta - desde > 200)
                return (false, "No se pueden crear más de 200 casilleros a la vez.", 0);

            if (precio.HasValue && precio.Value < 0)
                return (false, "El precio no puede ser negativo.", 0);

            try
            {
                int creados = _dao.CrearCasillerosEnMasa(desde, hasta, precio);
                if (creados == 0)
                    return (false, "Todos los casilleros del rango ya existían.", 0);
                return (true, "Se crearon " + creados + " casillero(s).", creados);
            }
            catch (Exception ex)
            {
                return (false, "Error al crear en masa.\n" + ex.Message, 0);
            }
        }

        // ──────────────────────────────────────────────────────
        // ASIGNAR
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Asignar(long id, long socioId, string observaciones)
        {
            if (id <= 0) return (false, "Casillero inválido.");
            if (socioId <= 0) return (false, "Tenés que seleccionar un socio.");

            try
            {
                bool ok = _dao.AsignarCasillero(id, socioId,
                    string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim());
                return ok
                    ? (true, "Casillero asignado correctamente.")
                    : (false, "No se pudo asignar el casillero.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // LIBERAR
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Liberar(long id)
        {
            try
            {
                bool ok = _dao.LiberarCasillero(id);
                return ok
                    ? (true, "Casillero liberado.")
                    : (false, "No se pudo liberar el casillero.");
            }
            catch (Exception ex)
            {
                return (false, "Error al liberar.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // CAMBIAR ESTADO
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) CambiarEstado(long id, string nuevoEstado)
        {
            if (nuevoEstado != "libre" && nuevoEstado != "mantenimiento")
                return (false, "Estado inválido.");

            try
            {
                bool ok = _dao.CambiarEstado(id, nuevoEstado);
                string accion = nuevoEstado == "mantenimiento"
                    ? "puesto en mantenimiento"
                    : "marcado como libre";
                return ok
                    ? (true, "Casillero " + accion + ".")
                    : (false, "No se pudo cambiar el estado.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // ACTUALIZAR PRECIO / OBSERVACIONES
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Actualizar(long id, decimal? precio, string observaciones)
        {
            if (precio.HasValue && precio.Value < 0)
                return (false, "El precio no puede ser negativo.");

            try
            {
                bool ok = _dao.ActualizarCasillero(id, precio,
                    string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim());
                return ok
                    ? (true, "Casillero actualizado.")
                    : (false, "No se pudo actualizar.");
            }
            catch (Exception ex)
            {
                return (false, "Error al actualizar.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // ELIMINAR
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Eliminar(long id)
        {
            try
            {
                bool ok = _dao.EliminarCasillero(id);
                return ok
                    ? (true, "Casillero eliminado.")
                    : (false, "No se pudo eliminar.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // ESTADÍSTICAS
        // ──────────────────────────────────────────────────────
        public EstadisticasCasilleros ObtenerEstadisticas()
        {
            try { return _dao.ObtenerEstadisticas(); }
            catch { return new EstadisticasCasilleros(); }
        }

        // ──────────────────────────────────────────────────────
        // VALIDACIONES
        // ──────────────────────────────────────────────────────
        private string ValidarCampos(short numero, decimal? precio)
        {
            if (numero < 1 || numero > 9999)
                return "El número de casillero debe estar entre 1 y 9999.";

            if (precio.HasValue && precio.Value < 0)
                return "El precio no puede ser negativo.";

            return null;
        }
    }
}