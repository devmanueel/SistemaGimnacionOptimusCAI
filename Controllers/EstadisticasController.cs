// Controllers/EstadisticasController.cs — C# 7.3
using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class EstadisticasController
    {
        private readonly EstadisticasDao _dao = new EstadisticasDao();

        public EstadisticasKPI ObtenerKPIs()
        {
            try { return _dao.ObtenerKPIs(); }
            catch { return new EstadisticasKPI(); }
        }

        public (decimal totalIngresos, decimal totalEgresos,
                List<PuntoDiario> ingresos, List<PuntoDiario> egresos)
            ObtenerCajaConEgresos(DateTime desde, DateTime hasta)
        {
            try { return _dao.ObtenerCajaConEgresos(desde, hasta); }
            catch { return (0, 0, new List<PuntoDiario>(), new List<PuntoDiario>()); }
        }

        public List<AsistenciaPorHora> ObtenerAsistenciasPorHora(DateTime desde, DateTime hasta, int? actividadId)
        {
            try { return _dao.ObtenerAsistenciasPorHora(desde, hasta, actividadId); }
            catch { return new List<AsistenciaPorHora>(); }
        }

        public List<(int Id, string Nombre)> ObtenerActividades()
        {
            try { return _dao.ObtenerActividades(); }
            catch { return new List<(int, string)>(); }
        }

        public List<AsistenciaPorActividad> ObtenerAsistenciasPorActividad(DateTime desde, DateTime hasta)
        {
            try { return _dao.ObtenerAsistenciasPorActividad(desde, hasta); }
            catch { return new List<AsistenciaPorActividad>(); }
        }

        public List<NuevoSocioPorDia> ObtenerNuevosSocios(DateTime desde, DateTime hasta)
        {
            try { return _dao.ObtenerNuevosSocios(desde, hasta); }
            catch { return new List<NuevoSocioPorDia>(); }
        }

        public List<SocioTop> ObtenerTop5Socios(DateTime desde, DateTime hasta)
        {
            try { return _dao.ObtenerTop5Socios(desde, hasta); }
            catch { return new List<SocioTop>(); }
        }

        public int ObtenerSociosInactivos(int diasInactivo)
        {
            try { return _dao.ObtenerSociosInactivos(diasInactivo); }
            catch { return 0; }
        }
    }
}
