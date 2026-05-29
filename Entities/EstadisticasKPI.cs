// Entities/EstadisticasKPI.cs — C# 7.3
using System;

namespace Entities
{
    public class EstadisticasKPI
    {
        public int SociosActivos { get; set; }
        public int MembresiasPorVencer { get; set; }
        public string ProductoMasVendido { get; set; }
        public int ProductoMasVendidoQty { get; set; }
        public int NuevosSociosMes { get; set; }
        public decimal IngresosMes { get; set; }
        public decimal EgresosMes { get; set; }

        public decimal GananciaMes
        {
            get { return IngresosMes - EgresosMes; }
        }

        public string ProductoTexto
        {
            get
            {
                if (string.IsNullOrEmpty(ProductoMasVendido)) return "Sin ventas este mes";
                return ProductoMasVendido + " (" + ProductoMasVendidoQty + " unid.)";
            }
        }

        public string MembresiasPorVencerTexto
        {
            get
            {
                if (MembresiasPorVencer == 0) return "Ninguna";
                if (MembresiasPorVencer == 1) return "1 vence esta semana";
                return MembresiasPorVencer + " vencen esta semana";
            }
        }
    }

    public class PuntoDiario
    {
        public DateTime Fecha { get; set; }
        public decimal Monto { get; set; }

        public string FechaTexto
        {
            get { return Fecha.ToString("dd/MM"); }
        }
    }

    public class AsistenciaPorHora
    {
        public int Hora { get; set; }
        public decimal Promedio { get; set; }
        public decimal Porcentaje { get; set; }
    }

    public class AsistenciaPorActividad
    {
        public string Actividad { get; set; }
        public int Cantidad { get; set; }
    }

    public class NuevoSocioPorDia
    {
        public DateTime Fecha { get; set; }
        public int Cantidad { get; set; }

        public string FechaTexto
        {
            get { return Fecha.ToString("dd/MM"); }
        }
    }

    public class SocioTop
    {
        public string NombreCompleto { get; set; }
        public int TotalAsistencias { get; set; }
    }
}
