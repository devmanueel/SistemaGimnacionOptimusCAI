// Entities/ReporteFinanciero.cs — C# 7.3
using System;

namespace Entities
{
    public class MovimientoReporte
    {
        public long     Id               { get; set; }
        public string   Tipo             { get; set; }
        public string   Subtipo          { get; set; }
        public string   Concepto         { get; set; }
        public decimal  Monto            { get; set; }
        public string   MetodoPago       { get; set; }
        public string   ReferenciaKipo   { get; set; }
        public DateTime Fecha            { get; set; }
        public string   RegistradoPor    { get; set; }
        public string   ActividadNombre  { get; set; }
        public string   InstructorNombre { get; set; }

        public string FechaTexto   => Fecha.ToString("dd/MM/yyyy");
        public string MontoTexto   => FormatoARS.Moneda(Monto);
        public bool   EsIngreso    => (Tipo ?? "").StartsWith("ingreso");
    }

    public class TotalesReporte
    {
        public decimal TotalIngresos    { get; set; }
        public decimal TotalEgresos     { get; set; }
        public decimal Balance          { get; set; }
        public int     CantidadIngresos { get; set; }
        public int     CantidadEgresos  { get; set; }

        public string TotalIngresosTexto => FormatoARS.Moneda(TotalIngresos);
        public string TotalEgresosTexto  => FormatoARS.Moneda(TotalEgresos);
        public string BalanceTexto       => FormatoARS.Moneda(Balance);
        public bool   BalancePositivo    => Balance >= 0;
    }

    public class IngresosPorMes
    {
        public int     Mes       { get; set; }
        public string  MesNombre { get; set; }
        public decimal Ingresos  { get; set; }
        public decimal Egresos   { get; set; }
    }

    public class ResumenDocente
    {
        public long    InstructorId      { get; set; }
        public string  NombreCompleto    { get; set; }
        public byte[]  Foto              { get; set; }
        public decimal TarifaHora        { get; set; }
        public string  ActividadNombre   { get; set; }
        public int     DiasTrabajados    { get; set; }
        public decimal HorasTotales      { get; set; }
        public decimal SueldoEstimado    { get; set; }
        public decimal IngresosGenerados { get; set; }

        public string HorasTexto
        {
            get
            {
                int h = (int)HorasTotales;
                int m = (int)((HorasTotales - h) * 60);
                return h > 0 ? h + "h " + m.ToString("D2") + "m" : m + " min";
            }
        }
        public string SueldoTexto        => FormatoARS.Moneda(SueldoEstimado);
        public string TarifaTexto        => FormatoARS.MonedaCorta(TarifaHora) + "/h";
        public string IngresosGenerTexto => FormatoARS.Moneda(IngresosGenerados);
        public string DiasTrabajTexto    => DiasTrabajados + (DiasTrabajados == 1 ? " día" : " días");
    }

    public class SocioConDeuda
    {
        public long    SocioId          { get; set; }
        public string  NombreCompleto   { get; set; }
        public int?    NumeroSocio      { get; set; }
        public string  Telefono         { get; set; }
        public byte[]  Foto             { get; set; }
        public long    MembresiaId      { get; set; }
        public string  TipoPlan         { get; set; }
        public string  ActividadNombre  { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public int     DiasVencida      { get; set; }
        public int     DiasParaVencer   { get; set; }
        public string  EstadoDeuda      { get; set; }

        public bool   EsVencida         => EstadoDeuda == "vencida";
        public string NumeroSocioTexto  => NumeroSocio.HasValue ? "#" + NumeroSocio.Value.ToString("D4") : "-";
        public string VencimientoTexto  => FechaVencimiento.ToString("dd/MM/yyyy");

        public string AlertaTexto
        {
            get
            {
                if (EsVencida)
                    return "Vencida hace " + DiasVencida + (DiasVencida == 1 ? " día" : " días");
                return "Vence en " + DiasParaVencer + (DiasParaVencer == 1 ? " día" : " días");
            }
        }
    }

    public class VentaEmpleado
    {
        public long     Id            { get; set; }
        public decimal  Total         { get; set; }
        public string   MetodoPago    { get; set; }
        public DateTime CreadoEn      { get; set; }
        public int      CantidadItems { get; set; }

        public string HoraTexto  => CreadoEn.ToString("HH:mm");
        public string TotalTexto => FormatoARS.Moneda(Total);
    }
}
