// Controllers/ReporteController.cs — C# 7.3
using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class ReporteController
    {
        private readonly ReporteDao   _dao    = new ReporteDao();
        private readonly ActividadDao _actDao = new ActividadDao();
        private readonly UsuarioDao   _usuDao = new UsuarioDao();

        public List<MovimientoReporte> ObtenerMovimientos(
            DateTime? desde, DateTime? hasta,
            long? actividadId = null, string metodoPago = null, long? instructorId = null)
        {
            if (!SesionManager.EsAdmin)
                return new List<MovimientoReporte>();
            try { return _dao.ObtenerMovimientos(desde, hasta, actividadId, metodoPago, instructorId); }
            catch (Exception ex) { throw new Exception("Error al cargar movimientos.\n" + ex.Message); }
        }

        public (TotalesReporte totales,
                List<(string actividad, decimal total, int cantidad)> porActividad,
                List<(string metodo, decimal total, int cantidad)> porMetodo)
            ObtenerTotales(DateTime? desde, DateTime? hasta)
        {
            if (!SesionManager.EsAdmin)
                return (new TotalesReporte(), new List<(string, decimal, int)>(), new List<(string, decimal, int)>());
            try { return _dao.ObtenerTotales(desde, hasta); }
            catch { return (new TotalesReporte(), new List<(string, decimal, int)>(), new List<(string, decimal, int)>()); }
        }

        public List<IngresosPorMes> ObtenerGraficoPorMes(int anio)
        {
            if (!SesionManager.EsAdmin)
                return new List<IngresosPorMes>();
            try { return _dao.ObtenerGraficoPorMes(anio); }
            catch { return new List<IngresosPorMes>(); }
        }

        public List<ResumenDocente> ObtenerSueldosDocentes(DateTime? desde, DateTime? hasta)
        {
            if (!SesionManager.EsAdmin)
                return new List<ResumenDocente>();
            try { return _dao.ObtenerSueldosDocentes(desde, hasta); }
            catch (Exception ex) { throw new Exception("Error al cargar sueldos.\n" + ex.Message); }
        }

        public (List<SocioConDeuda> vencidas, List<SocioConDeuda> proximas)
            ObtenerSociosDeuda(int diasProximos = 7)
        {
            if (!SesionManager.EsAdmin)
                return (new List<SocioConDeuda>(), new List<SocioConDeuda>());
            try { return _dao.ObtenerSociosDeuda(diasProximos); }
            catch { return (new List<SocioConDeuda>(), new List<SocioConDeuda>()); }
        }

        public (List<VentaEmpleado> ventas, decimal totalDia, int cantidadVentas)
            ObtenerMisVentasDelDia()
        {
            try { return _dao.ObtenerMisVentasDelDia(SesionManager.UsuarioId); }
            catch { return (new List<VentaEmpleado>(), 0m, 0); }
        }

        public List<Actividad> ListarActividadesParaFiltro()
        {
            try { return _actDao.ObtenerActividades(); }
            catch { return new List<Actividad>(); }
        }

        // ── Editar tarifa/hora de un instructor desde el reporte (SDD_Fix_Reportes) ──
        public (bool ok, string mensaje) ActualizarTarifaInstructor(
            long instructorId, decimal tarifaHora)
        {
            if (!SesionManager.EsAdmin)
                return (false, "Solo los administradores pueden editar tarifas.");
            if (instructorId <= 0)
                return (false, "Instructor inválido.");
            if (tarifaHora < 0)
                return (false, "La tarifa no puede ser negativa.");

            try
            {
                bool ok = _dao.ActualizarTarifaInstructor(instructorId, tarifaHora);
                if (ok)
                {
                    Auditor.Registrar("editar", "usuario", instructorId,
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "campo",       "tarifa_hora" },
                            { "valor_nuevo", tarifaHora }
                        });
                    return (true, "Tarifa actualizada correctamente.");
                }
                return (false, "No se encontró el instructor.");
            }
            catch (Exception ex)
            {
                return (false, "Error al actualizar tarifa.\n" + ex.Message);
            }
        }

        public List<Usuario> ListarInstructoresParaFiltro()
        {
            try
            {
                var todos = _usuDao.ObtenerUsuarios();
                return todos.FindAll(u => u.RolId == 2 && u.Activo);
            }
            catch { return new List<Usuario>(); }
        }
    }
}
