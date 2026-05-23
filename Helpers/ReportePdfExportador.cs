// Helpers/ReportePdfExportador.cs — C# 7.3
// Requiere: iTextSharp 5.5.13.3
using Entities;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.draw;
using System;
using System.Collections.Generic;
using System.IO;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    public class ReportePdfExportador
    {
        private const string NOMBRE_GIM = "OptimusCAI Gym";
        private const string DIRECCION  = "Jujuy, Argentina";
        private const string TELEFONO   = "+54 388 000-0000";

        private static readonly BaseColor ColorPrimario   = new BaseColor(0,   207, 255);
        private static readonly BaseColor ColorSecundario = new BaseColor(167, 139, 250);
        private static readonly BaseColor ColorFondo      = new BaseColor(18,  18,  30);
        private static readonly BaseColor ColorTexto      = new BaseColor(232, 232, 255);
        private static readonly BaseColor ColorGris       = new BaseColor(106, 106, 154);

        // ── REPORTE DE INGRESOS ───────────────────────────────
        public string ExportarIngresos(
            List<MovimientoReporte> movimientos,
            TotalesReporte totales,
            DateTime desde, DateTime hasta)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "Reporte_Ingresos_" + desde.ToString("yyyyMM") + "_" + DateTime.Now.Ticks + ".pdf");

            using (var doc = new Document(PageSize.A4, 36, 36, 60, 36))
            {
                PdfWriter.GetInstance(doc, new FileStream(path, FileMode.Create));
                doc.Open();

                AgregarEncabezado(doc, "REPORTE DE INGRESOS",
                    "Período: " + desde.ToString("dd/MM/yyyy") + " al " + hasta.ToString("dd/MM/yyyy"));

                AgregarPanelTotales(doc, totales);

                doc.Add(new Paragraph("\n"));
                var tabla = new PdfPTable(6) { WidthPercentage = 100 };
                tabla.SetWidths(new float[] { 2, 4, 2, 3, 2, 2 });

                AgregarFila(tabla, true, "FECHA", "CONCEPTO", "TIPO", "REGISTRADO POR", "MÉTODO", "MONTO");
                int count = 0;
                foreach (var m in movimientos)
                {
                    if (count >= 500) break;
                    AgregarFila(tabla, false,
                        m.FechaTexto, m.Concepto ?? "-",
                        m.Tipo.ToUpper(),
                        m.RegistradoPor ?? "Sistema",
                        m.MetodoPago ?? "-", m.MontoTexto);
                    count++;
                }

                doc.Add(tabla);
                doc.Close();
            }
            return path;
        }

        // ── REPORTE DE SUELDOS ────────────────────────────────
        public string ExportarSueldos(
            List<ResumenDocente> docentes,
            DateTime desde, DateTime hasta)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "Reporte_Sueldos_" + desde.ToString("yyyyMM") + "_" + DateTime.Now.Ticks + ".pdf");

            using (var doc = new Document(PageSize.A4, 36, 36, 60, 36))
            {
                PdfWriter.GetInstance(doc, new FileStream(path, FileMode.Create));
                doc.Open();

                AgregarEncabezado(doc, "LIQUIDACIÓN DE SUELDOS",
                    "Período: " + desde.ToString("dd/MM/yyyy") + " al " + hasta.ToString("dd/MM/yyyy"));

                var tabla = new PdfPTable(6) { WidthPercentage = 100 };
                tabla.SetWidths(new float[] { 3, 2, 2, 2, 2, 2 });

                AgregarFila(tabla, true, "INSTRUCTOR", "ACTIVIDAD", "DÍAS", "HORAS", "TARIFA", "SUELDO");

                decimal totalSueldos = 0;
                foreach (var d in docentes)
                {
                    AgregarFila(tabla, false,
                        d.NombreCompleto, d.ActividadNombre,
                        d.DiasTrabajTexto, d.HorasTexto,
                        d.TarifaTexto, d.SueldoTexto);
                    totalSueldos += d.SueldoEstimado;
                }

                var celdaTotal = new PdfPCell(new Phrase("TOTAL A PAGAR"))
                    { Colspan = 5, HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 8 };
                tabla.AddCell(celdaTotal);
                tabla.AddCell(new PdfPCell(new Phrase("$" + totalSueldos.ToString("N2")))
                    { HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 8 });

                doc.Add(tabla);
                doc.Close();
            }
            return path;
        }

        // ── REPORTE SOCIOS CON DEUDA ──────────────────────────
        public string ExportarDeudas(
            List<SocioConDeuda> vencidas,
            List<SocioConDeuda> proximas)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "Reporte_Deudas_" + DateTime.Now.ToString("yyyyMMdd") + "_" + DateTime.Now.Ticks + ".pdf");

            using (var doc = new Document(PageSize.A4, 36, 36, 60, 36))
            {
                PdfWriter.GetInstance(doc, new FileStream(path, FileMode.Create));
                doc.Open();

                AgregarEncabezado(doc, "SOCIOS CON MEMBRESÍA VENCIDA",
                    "Generado el " + DateTime.Now.ToString("dd/MM/yyyy"));

                if (vencidas.Count > 0)
                {
                    doc.Add(new Paragraph("MEMBRESÍAS VENCIDAS (" + vencidas.Count + ")",
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(220, 60, 60))));
                    doc.Add(new Paragraph(" "));
                    var tv = new PdfPTable(5) { WidthPercentage = 100 };
                    tv.SetWidths(new float[] { 2, 3, 2, 2, 2 });
                    AgregarFila(tv, true, "#", "SOCIO", "ACTIVIDAD", "VENCIMIENTO", "DÍAS");
                    foreach (var s in vencidas)
                        AgregarFila(tv, false,
                            s.NumeroSocioTexto, s.NombreCompleto,
                            s.ActividadNombre, s.VencimientoTexto,
                            s.DiasVencida + " días");
                    doc.Add(tv);
                }

                if (proximas.Count > 0)
                {
                    doc.Add(new Paragraph("\nPRÓXIMAS A VENCER (" + proximas.Count + ")",
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(255, 160, 0))));
                    doc.Add(new Paragraph(" "));
                    var tp = new PdfPTable(5) { WidthPercentage = 100 };
                    tp.SetWidths(new float[] { 2, 3, 2, 2, 2 });
                    AgregarFila(tp, true, "#", "SOCIO", "ACTIVIDAD", "VENCIMIENTO", "DÍAS");
                    foreach (var s in proximas)
                        AgregarFila(tp, false,
                            s.NumeroSocioTexto, s.NombreCompleto,
                            s.ActividadNombre, s.VencimientoTexto,
                            s.DiasParaVencer + " días");
                    doc.Add(tp);
                }

                doc.Close();
            }
            return path;
        }

        // ── HELPERS ───────────────────────────────────────────
        private void AgregarEncabezado(Document doc, string titulo, string subtitulo)
        {
            var fontTitulo = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, ColorPrimario);
            var fontSub    = FontFactory.GetFont(FontFactory.HELVETICA, 10, ColorGris);
            var fontGim    = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, ColorTexto);
            var fontDatos  = FontFactory.GetFont(FontFactory.HELVETICA, 9, ColorGris);

            doc.Add(new Paragraph(NOMBRE_GIM, fontGim));
            doc.Add(new Paragraph(DIRECCION + "  |  " + TELEFONO, fontDatos));
            doc.Add(new Paragraph(" "));
            doc.Add(new Paragraph(titulo, fontTitulo));
            doc.Add(new Paragraph(subtitulo, fontSub));
            doc.Add(new Paragraph("Generado el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"), fontDatos));
            var linea = new LineSeparator(1f, 100f, ColorSecundario, Element.ALIGN_CENTER, -2);
            doc.Add(new Chunk(linea));
            doc.Add(new Paragraph(" "));
        }

        private void AgregarPanelTotales(Document doc, TotalesReporte t)
        {
            var tabla  = new PdfPTable(3) { WidthPercentage = 100 };
            var fontL  = FontFactory.GetFont(FontFactory.HELVETICA, 9, ColorGris);
            var fontV  = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, ColorPrimario);
            AgregarCeldaTotal(tabla, "INGRESOS TOTALES", t.TotalIngresosTexto, fontL, fontV);
            AgregarCeldaTotal(tabla, "EGRESOS TOTALES",  t.TotalEgresosTexto,  fontL, fontV);
            AgregarCeldaTotal(tabla, "BALANCE",          t.BalanceTexto,        fontL, fontV);
            doc.Add(tabla);
        }

        private void AgregarCeldaTotal(PdfPTable t, string label, string valor, Font fontL, Font fontV)
        {
            var cell = new PdfPCell();
            cell.AddElement(new Paragraph(label, fontL));
            cell.AddElement(new Paragraph(valor, fontV));
            cell.Padding = 12;
            t.AddCell(cell);
        }

        private void AgregarFila(PdfPTable tabla, bool esHeader, params string[] valores)
        {
            var font = esHeader
                ? FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BaseColor.WHITE)
                : FontFactory.GetFont(FontFactory.HELVETICA, 9, ColorFondo);

            foreach (var val in valores)
            {
                var cell = new PdfPCell(new Phrase(val ?? "-", font))
                {
                    Padding        = 6,
                    BackgroundColor = esHeader ? ColorFondo : BaseColor.WHITE,
                    BorderColor    = new BaseColor(200, 200, 220)
                };
                tabla.AddCell(cell);
            }
        }
    }
}
