// Helpers/ReporteExcelExportador.cs — C# 7.3
// Requiere: ClosedXML 0.95.4
using ClosedXML.Excel;
using Entities;
using System;
using System.Collections.Generic;
using System.IO;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    public class ReporteExcelExportador
    {
        public string ExportarIngresos(
            List<MovimientoReporte> movimientos,
            TotalesReporte totales,
            DateTime desde, DateTime hasta)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "Reporte_Ingresos_" + desde.ToString("yyyyMM") + "_" + DateTime.Now.Ticks + ".xlsx");

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Ingresos");

                ws.Cell("A1").Value = "OptimusCAI Gym — Reporte de Ingresos";
                ws.Cell("A1").Style.Font.Bold = true;
                ws.Cell("A1").Style.Font.FontSize = 14;
                ws.Cell("A2").Value = "Período: " + desde.ToString("dd/MM/yyyy") +
                                      " al " + hasta.ToString("dd/MM/yyyy");
                ws.Cell("A3").Value = "Generado: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");

                ws.Cell("A5").Value = "TOTAL INGRESOS"; ws.Cell("B5").Value = (double)totales.TotalIngresos;
                ws.Cell("A6").Value = "TOTAL EGRESOS";  ws.Cell("B6").Value = (double)totales.TotalEgresos;
                ws.Cell("A7").Value = "BALANCE";        ws.Cell("B7").Value = (double)totales.Balance;
                ws.Range("B5:B7").Style.NumberFormat.Format = "$#,##0.00";
                ws.Range("A5:A7").Style.Font.Bold = true;

                int fila = 9;
                ws.Cell(fila, 1).Value = "FECHA";
                ws.Cell(fila, 2).Value = "CONCEPTO";
                ws.Cell(fila, 3).Value = "TIPO";
                ws.Cell(fila, 4).Value = "ACTIVIDAD";
                ws.Cell(fila, 5).Value = "INSTRUCTOR";
                ws.Cell(fila, 6).Value = "MÉTODO";
                ws.Cell(fila, 7).Value = "MONTO";
                ws.Range(fila, 1, fila, 7).Style.Font.Bold = true;

                foreach (var m in movimientos)
                {
                    fila++;
                    ws.Cell(fila, 1).Value = m.Fecha.ToString("dd/MM/yyyy");
                    ws.Cell(fila, 2).Value = m.Concepto ?? "-";
                    ws.Cell(fila, 3).Value = m.Tipo;
                    ws.Cell(fila, 4).Value = m.ActividadNombre ?? "-";
                    ws.Cell(fila, 5).Value = m.InstructorNombre ?? "-";
                    ws.Cell(fila, 6).Value = m.MetodoPago ?? "-";
                    ws.Cell(fila, 7).Value = (double)m.Monto;
                    ws.Cell(fila, 7).Style.NumberFormat.Format = "$#,##0.00";
                }

                ws.Columns().AdjustToContents();
                wb.SaveAs(path);
            }
            return path;
        }

        // ── REPORTE DE SOCIOS ──────────────────────────────
        public string ExportarSocios(List<SocioConMembresia> socios)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "Reporte_Socios_" + DateTime.Now.ToString("yyyyMMdd") + "_" + DateTime.Now.Ticks + ".xlsx");

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Socios");

                ws.Cell("A1").Value = "OptimusCAI Gym — Listado de Socios";
                ws.Cell("A1").Style.Font.Bold = true;
                ws.Cell("A1").Style.Font.FontSize = 14;
                ws.Cell("A2").Value = "Total: " + socios.Count + " socio(s)";
                ws.Cell("A3").Value = "Generado: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");

                int fila = 5;
                ws.Cell(fila, 1).Value = "N°";
                ws.Cell(fila, 2).Value = "SOCIO";
                ws.Cell(fila, 3).Value = "DNI";
                ws.Cell(fila, 4).Value = "TELÉFONO";
                ws.Cell(fila, 5).Value = "EDAD";
                ws.Cell(fila, 6).Value = "ACTIVIDAD";
                ws.Cell(fila, 7).Value = "VENCIMIENTO";
                ws.Cell(fila, 8).Value = "ESTADO";
                ws.Range(fila, 1, fila, 8).Style.Font.Bold = true;

                foreach (var s in socios)
                {
                    fila++;
                    ws.Cell(fila, 1).Value = s.NumeroFormateado;
                    ws.Cell(fila, 2).Value = s.NombreCompleto;
                    ws.Cell(fila, 3).Value = s.Dni ?? "-";
                    ws.Cell(fila, 4).Value = s.Telefono ?? "-";
                    ws.Cell(fila, 5).Value = s.EdadTexto;
                    ws.Cell(fila, 6).Value = s.ActividadNombre ?? "-";
                    ws.Cell(fila, 7).Value = s.FechaVencimientoTexto;
                    ws.Cell(fila, 8).Value = s.EstadoTexto;
                }

                ws.Columns().AdjustToContents();
                wb.SaveAs(path);
            }
            return path;
        }

        public string ExportarSueldos(List<ResumenDocente> docentes, DateTime desde, DateTime hasta)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "Sueldos_" + desde.ToString("yyyyMM") + "_" + DateTime.Now.Ticks + ".xlsx");

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Sueldos");
                ws.Cell("A1").Value = "OptimusCAI Gym — Liquidación de Sueldos";
                ws.Cell("A1").Style.Font.Bold = true;
                ws.Cell("A1").Style.Font.FontSize = 14;
                ws.Cell("A2").Value = "Período: " + desde.ToString("dd/MM/yyyy") +
                                      " al " + hasta.ToString("dd/MM/yyyy");

                int fila = 4;
                ws.Cell(fila, 1).Value = "INSTRUCTOR";
                ws.Cell(fila, 2).Value = "ACTIVIDAD";
                ws.Cell(fila, 3).Value = "DÍAS";
                ws.Cell(fila, 4).Value = "HORAS TOTALES";
                ws.Cell(fila, 5).Value = "TARIFA/HORA";
                ws.Cell(fila, 6).Value = "SUELDO ESTIMADO";
                ws.Cell(fila, 7).Value = "INGRESOS GENERADOS";
                ws.Range(fila, 1, fila, 7).Style.Font.Bold = true;

                decimal totalSueldos = 0;
                foreach (var d in docentes)
                {
                    fila++;
                    ws.Cell(fila, 1).Value = d.NombreCompleto;
                    ws.Cell(fila, 2).Value = d.ActividadNombre;
                    ws.Cell(fila, 3).Value = d.DiasTrabajados;
                    ws.Cell(fila, 4).Value = (double)d.HorasTotales;
                    ws.Cell(fila, 5).Value = (double)d.TarifaHora;
                    ws.Cell(fila, 6).Value = (double)d.SueldoEstimado;
                    ws.Cell(fila, 7).Value = (double)d.IngresosGenerados;
                    ws.Range(fila, 5, fila, 7).Style.NumberFormat.Format = "$#,##0.00";
                    totalSueldos += d.SueldoEstimado;
                }

                fila++;
                ws.Cell(fila, 5).Value = "TOTAL";
                ws.Cell(fila, 6).Value = (double)totalSueldos;
                ws.Cell(fila, 6).Style.NumberFormat.Format = "$#,##0.00";
                ws.Cell(fila, 5).Style.Font.Bold = true;
                ws.Cell(fila, 6).Style.Font.Bold = true;

                ws.Columns().AdjustToContents();
                wb.SaveAs(path);
            }
            return path;
        }
    }
}
