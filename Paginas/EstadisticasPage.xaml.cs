using Controllers;
using Entities;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class EstadisticasPage : Page
    {
        private readonly EstadisticasController _controller = new EstadisticasController();
        private DateTime _fechaDesde;
        private DateTime _fechaHasta;
        private bool _cargado;

        private static readonly CultureInfo _cultAR = new CultureInfo("es-AR");

        private static readonly OxyColor ColorFondo = OxyColor.FromRgb(18, 24, 18);
        private static readonly OxyColor ColorBorde = OxyColor.FromRgb(42, 58, 42);
        private static readonly OxyColor ColorTexto = OxyColor.FromRgb(150, 170, 150);
        private static readonly OxyColor ColorTextoClaro = OxyColor.FromRgb(200, 220, 200);
        private static readonly OxyColor ColorTextoEje = OxyColor.FromRgb(120, 140, 120);
        private static readonly OxyColor ColorGrid = OxyColor.FromRgb(30, 45, 30);
        private static readonly OxyColor ColorLabelSerie = OxyColor.FromRgb(180, 200, 180);

        private static readonly OxyColor ColorVerde = OxyColor.FromRgb(102, 187, 106);
        private static readonly OxyColor ColorRojo = OxyColor.FromRgb(239, 83, 80);
        private static readonly OxyColor ColorTeal = OxyColor.FromRgb(0, 150, 136);

        public EstadisticasPage()
        {
            InitializeComponent();
            CalcularPeriodo("Esta semana");
            Loaded += EstadisticasPage_Loaded;
        }

        private void EstadisticasPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!SesionManager.EsAdmin)
            {
                MessageBox.Show("Solo el administrador puede ver las estadisticas.",
                    "Acceso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            dpDesde.SelectedDate = _fechaDesde;
            dpHasta.SelectedDate = _fechaHasta;
            CargarActividades();
            _cargado = true;
            CargarTodo();
        }

        // ══════════════════════════════════════════════════════════
        //  PERIODO
        // ══════════════════════════════════════════════════════════

        private void cmbPeriodo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cargado) return;
            var item = cmbPeriodo.SelectedItem as ComboBoxItem;
            if (item == null) return;

            string texto = item.Content.ToString();

            if (texto == "Definir periodo")
            {
                panelFechasCustom.Visibility = Visibility.Visible;
                return;
            }

            panelFechasCustom.Visibility = Visibility.Collapsed;
            CalcularPeriodo(texto);
            CargarTodo();
        }

        private void btnMostrar_Click(object sender, MouseButtonEventArgs e)
        {
            if (dpDesde.SelectedDate == null || dpHasta.SelectedDate == null)
            {
                MessageBox.Show("Selecciona ambas fechas.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _fechaDesde = dpDesde.SelectedDate.Value;
            _fechaHasta = dpHasta.SelectedDate.Value;

            if (_fechaDesde > _fechaHasta)
            {
                DateTime tmp = _fechaDesde;
                _fechaDesde = _fechaHasta;
                _fechaHasta = tmp;
                dpDesde.SelectedDate = _fechaDesde;
                dpHasta.SelectedDate = _fechaHasta;
            }

            CargarTodo();
        }

        private void CalcularPeriodo(string texto)
        {
            DateTime hoy = DateTime.Today;

            if (texto == "Esta semana")
            {
                int offset = (int)hoy.DayOfWeek;
                if (offset == 0) offset = 7;
                _fechaDesde = hoy.AddDays(-(offset - 1));
                _fechaHasta = hoy;
            }
            else if (texto == "Este mes")
            {
                _fechaDesde = new DateTime(hoy.Year, hoy.Month, 1);
                _fechaHasta = hoy;
            }
            else if (texto == "Este ano")
            {
                _fechaDesde = new DateTime(hoy.Year, 1, 1);
                _fechaHasta = hoy;
            }
            else if (texto == "Ultima semana")
            {
                int offset = (int)hoy.DayOfWeek;
                if (offset == 0) offset = 7;
                DateTime lunesActual = hoy.AddDays(-(offset - 1));
                _fechaDesde = lunesActual.AddDays(-7);
                _fechaHasta = lunesActual.AddDays(-1);
            }
            else if (texto == "Ultimo mes")
            {
                var primerDiaMes = new DateTime(hoy.Year, hoy.Month, 1);
                _fechaHasta = primerDiaMes.AddDays(-1);
                _fechaDesde = new DateTime(_fechaHasta.Year, _fechaHasta.Month, 1);
            }
            else if (texto == "Ultimo ano")
            {
                _fechaDesde = new DateTime(hoy.Year - 1, 1, 1);
                _fechaHasta = new DateTime(hoy.Year - 1, 12, 31);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CARGA GENERAL
        // ══════════════════════════════════════════════════════════

        private void CargarTodo()
        {
            lblPeriodoCaja.Text = "Periodo elegido: del " +
                _fechaDesde.ToString("dd/MM/yyyy") + " al " +
                _fechaHasta.ToString("dd/MM/yyyy");

            CargarKPIs();
            CargarCaja();
            CargarAsistencias();
            CargarSocios();
        }

        // ══════════════════════════════════════════════════════════
        //  KPIs GLOBALES
        // ══════════════════════════════════════════════════════════

        private void CargarKPIs()
        {
            try
            {
                var kpi = _controller.ObtenerKPIs();
                lblSociosActivos.Text = kpi.SociosActivos.ToString();
                lblMembresiasVencer.Text = kpi.MembresiasPorVencerTexto;
                lblProductoTop.Text = kpi.ProductoTexto;
                lblIngresosMes.Text = FormatoARS.MonedaCorta(kpi.IngresosMes);
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════
        //  CAJA
        // ══════════════════════════════════════════════════════════

        private void CargarCaja()
        {
            try
            {
                var (totalI, totalE, puntosI, puntosE) =
                    _controller.ObtenerCajaConEgresos(_fechaDesde, _fechaHasta);

                lblIngresos.Text = FormatoARS.MonedaCorta(totalI);
                lblGastos.Text = FormatoARS.MonedaCorta(totalE);
                lblGanancias.Text = FormatoARS.MonedaCorta(totalI - totalE);

                plotIngresos.Model = ConstruirGraficoLinea(
                    puntosI, "Ingresos por dia", ColorVerde);

                plotGastos.Model = ConstruirGraficoLinea(
                    puntosE, "Gastos por dia", ColorRojo);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar caja:\n" + ex.Message);
            }
        }

        private PlotModel ConstruirGraficoLinea(
            List<PuntoDiario> puntos, string titulo, OxyColor color)
        {
            var modelo = CrearModeloBase(titulo);
            modelo.Padding = new OxyThickness(10, 10, 20, 10);

            var ejeX = new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                TextColor = ColorTextoEje,
                TicklineColor = ColorBorde,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = ColorGrid,
                FontSize = 10
            };
            foreach (var p in puntos)
                ejeX.Labels.Add(p.FechaTexto);
            modelo.Axes.Add(ejeX);

            var ejeY = new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = ColorGrid,
                TicklineColor = OxyColors.Transparent,
                TextColor = ColorTextoEje,
                FontSize = 10,
                StringFormat = "$ #,0"
            };
            modelo.Axes.Add(ejeY);

            if (puntos.Count == 0)
            {
                AgregarTextoSinDatos(modelo);
                return modelo;
            }

            var serieArea = new AreaSeries
            {
                Color = color,
                Fill = OxyColor.FromAColor(40, color),
                StrokeThickness = 0
            };

            var serieLinea = new LineSeries
            {
                Color = color,
                StrokeThickness = 2.5,
                MarkerType = MarkerType.Circle,
                MarkerSize = 5,
                MarkerFill = color,
                MarkerStroke = OxyColor.FromRgb(8, 10, 8),
                MarkerStrokeThickness = 1.5,
                LabelFormatString = "$ {1:N0}",
                FontSize = 9,
                TextColor = ColorLabelSerie
            };

            for (int i = 0; i < puntos.Count; i++)
            {
                double val = (double)puntos[i].Monto;
                serieLinea.Points.Add(new DataPoint(i, val));
                serieArea.Points.Add(new DataPoint(i, val));
                serieArea.Points2.Add(new DataPoint(i, 0));
            }

            modelo.Series.Add(serieArea);
            modelo.Series.Add(serieLinea);
            return modelo;
        }

        // ══════════════════════════════════════════════════════════
        //  ASISTENCIAS
        // ══════════════════════════════════════════════════════════

        private void CargarActividades()
        {
            cmbActividad.Items.Clear();
            cmbActividad.Items.Add(new ComboBoxItem { Content = "Todas", Tag = null });

            try
            {
                var actividades = _controller.ObtenerActividades();
                foreach (var a in actividades)
                {
                    cmbActividad.Items.Add(new ComboBoxItem
                    {
                        Content = a.Nombre,
                        Tag = a.Id
                    });
                }
            }
            catch { }

            cmbActividad.SelectedIndex = 0;
        }

        private void cmbActividad_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cargado) return;
            CargarAsistencias();
        }

        private int? ObtenerActividadSeleccionada()
        {
            var item = cmbActividad.SelectedItem as ComboBoxItem;
            if (item == null || item.Tag == null) return null;
            return (int)item.Tag;
        }

        private void CargarAsistencias()
        {
            try
            {
                int? actividadId = ObtenerActividadSeleccionada();
                var porHora = _controller.ObtenerAsistenciasPorHora(_fechaDesde, _fechaHasta, actividadId);
                var porActividad = _controller.ObtenerAsistenciasPorActividad(_fechaDesde, _fechaHasta);

                // Promedio por hora
                var labelsProm = new List<string>();
                var valoresProm = new List<double>();
                foreach (var p in porHora)
                {
                    labelsProm.Add(p.Hora.ToString("D2") + "h");
                    valoresProm.Add((double)p.Promedio);
                }
                plotPromedioHora.Model = ConstruirGraficoBarrasVertical(
                    "Promedio de asistencias por hora", labelsProm, valoresProm,
                    ColorTeal, null, null);

                // Porcentaje por hora
                var labelsPct = new List<string>();
                var valoresPct = new List<double>();
                foreach (var p in porHora)
                {
                    labelsPct.Add(p.Hora.ToString("D2") + "h");
                    valoresPct.Add((double)p.Porcentaje);
                }
                plotPorcentajeHora.Model = ConstruirGraficoBarrasVertical(
                    "Porcentaje de asistencias por hora", labelsPct, valoresPct,
                    ColorTeal, "{0}%", 100);

                // Pie chart por actividad
                var modeloPie = CrearModeloBase("Asistencias por actividad");
                if (porActividad.Count == 0)
                {
                    AgregarTextoSinDatos(modeloPie);
                }
                else
                {
                    OxyColor[] coloresPie = new OxyColor[]
                    {
                        OxyColor.FromRgb(0, 150, 136),
                        OxyColor.FromRgb(33, 150, 243),
                        OxyColor.FromRgb(255, 152, 0),
                        OxyColor.FromRgb(156, 39, 176),
                        OxyColor.FromRgb(244, 67, 54),
                        OxyColor.FromRgb(76, 175, 80),
                        OxyColor.FromRgb(121, 85, 72),
                        OxyColor.FromRgb(255, 193, 7),
                        OxyColor.FromRgb(63, 81, 181)
                    };

                    var seriePie = new PieSeries
                    {
                        StrokeThickness = 1.5,
                        Stroke = OxyColor.FromRgb(8, 10, 8),
                        InsideLabelFormat = "",
                        OutsideLabelFormat = "{1}: {0}",
                        FontSize = 10,
                        TextColor = ColorLabelSerie,
                        Diameter = 0.85
                    };

                    int ci = 0;
                    foreach (var a in porActividad)
                    {
                        seriePie.Slices.Add(new PieSlice(a.Actividad, a.Cantidad)
                        {
                            Fill = coloresPie[ci % coloresPie.Length]
                        });
                        ci++;
                    }

                    modeloPie.Series.Add(seriePie);
                }
                plotPieActividades.Model = modeloPie;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar asistencias:\n" + ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SOCIOS
        // ══════════════════════════════════════════════════════════

        private void txtDiasInactivo_LostFocus(object sender, RoutedEventArgs e)
        {
            ActualizarInactivos();
        }

        private void txtDiasInactivo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ActualizarInactivos();
        }

        private void ActualizarInactivos()
        {
            if (!_cargado) return;
            int dias = ObtenerDiasInactivo();
            try
            {
                int cant = _controller.ObtenerSociosInactivos(dias);
                lblInactivos.Text = cant.ToString();
            }
            catch
            {
                lblInactivos.Text = "-";
            }
        }

        private int ObtenerDiasInactivo()
        {
            int dias;
            if (int.TryParse(txtDiasInactivo.Text.Trim(), out dias) && dias > 0)
                return dias;
            return 20;
        }

        private void CargarSocios()
        {
            try
            {
                var nuevosPorDia = _controller.ObtenerNuevosSocios(_fechaDesde, _fechaHasta);
                var top5 = _controller.ObtenerTop5Socios(_fechaDesde, _fechaHasta);
                int diasInactivo = ObtenerDiasInactivo();
                int cantInactivos = _controller.ObtenerSociosInactivos(diasInactivo);

                lblInactivos.Text = cantInactivos.ToString();

                // Nuevos socios por dia
                var labelsN = new List<string>();
                var valoresN = new List<double>();
                foreach (var n in nuevosPorDia)
                {
                    labelsN.Add(n.FechaTexto);
                    valoresN.Add(n.Cantidad);
                }
                plotNuevosSocios.Model = ConstruirGraficoBarrasVertical(
                    "Nuevos socios", labelsN, valoresN,
                    ColorTeal, null, null);

                // Top 5 socios (barras horizontales)
                var modeloTop = CrearModeloBase("Los 5 socios que mas asistieron");
                if (top5.Count == 0)
                {
                    AgregarTextoSinDatos(modeloTop);
                }
                else
                {
                    var ejeXTop = new LinearAxis
                    {
                        Position = AxisPosition.Bottom,
                        Minimum = 0,
                        MajorGridlineStyle = LineStyle.Dot,
                        MajorGridlineColor = ColorGrid,
                        TextColor = ColorTextoEje, FontSize = 9
                    };
                    var ejeYTop = new CategoryAxis
                    {
                        Position = AxisPosition.Left,
                        TextColor = ColorLabelSerie, FontSize = 9
                    };
                    var serieTop = new BarSeries
                    {
                        FillColor = OxyColor.FromRgb(211, 47, 47),
                        StrokeThickness = 0,
                        LabelPlacement = LabelPlacement.Outside,
                        LabelFormatString = "{0}",
                        FontSize = 10,
                        TextColor = OxyColor.FromRgb(200, 200, 200)
                    };

                    var top5Reversed = new List<SocioTop>(top5);
                    top5Reversed.Reverse();
                    foreach (var s in top5Reversed)
                    {
                        ejeYTop.Labels.Add(s.NombreCompleto.ToUpper());
                        serieTop.Items.Add(new BarItem(s.TotalAsistencias));
                    }

                    modeloTop.Axes.Add(ejeXTop);
                    modeloTop.Axes.Add(ejeYTop);
                    modeloTop.Series.Add(serieTop);
                }
                plotTop5.Model = modeloTop;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar socios:\n" + ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS OXYPLOT
        // ══════════════════════════════════════════════════════════

        private PlotModel CrearModeloBase(string titulo)
        {
            return new PlotModel
            {
                Title = titulo,
                TitleFontSize = 11,
                TitleColor = ColorTextoClaro,
                Background = OxyColors.Transparent,
                PlotAreaBorderColor = ColorBorde,
                TextColor = ColorTexto
            };
        }

        private PlotModel ConstruirGraficoBarrasVertical(
            string titulo, List<string> etiquetas, List<double> valores,
            OxyColor color, string formatoEjeY, double? maximoY)
        {
            var modelo = CrearModeloBase(titulo);

            if (etiquetas.Count == 0)
            {
                AgregarTextoSinDatos(modelo);
                return modelo;
            }

            var labels = etiquetas;
            var ejeX = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = -0.5,
                Maximum = labels.Count - 0.5,
                MajorStep = 1,
                MinorStep = 1,
                TextColor = ColorTextoEje,
                FontSize = 9,
                MajorGridlineStyle = LineStyle.None,
                TicklineColor = ColorBorde,
                LabelFormatter = v =>
                {
                    int i = (int)Math.Round(v);
                    return (i >= 0 && i < labels.Count) ? labels[i] : "";
                }
            };

            var ejeY = new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = ColorGrid,
                TextColor = ColorTextoEje,
                FontSize = 9
            };
            bool esPorcentaje = formatoEjeY != null && formatoEjeY.Contains("%");
            if (esPorcentaje)
                ejeY.LabelFormatter = v => v.ToString("N0", _cultAR) + "%";
            else if (formatoEjeY != null)
                ejeY.StringFormat = formatoEjeY;

            double maxValor = 0;
            foreach (var v in valores)
                if (v > maxValor) maxValor = v;
            if (maximoY.HasValue) ejeY.Maximum = maximoY.Value;
            else ejeY.Maximum = maxValor > 0 ? maxValor * 1.15 : 1;
            var serie = new RectangleBarSeries
            {
                FillColor = color,
                StrokeColor = OxyColors.Transparent,
                StrokeThickness = 0,
                LabelColor = ColorLabelSerie,
                FontSize = 9,
                LabelFormatString = "{0}"
            };

            const double mitadAncho = 0.35;
            for (int i = 0; i < valores.Count; i++)
            {
                var item = new RectangleBarItem(i - mitadAncho, 0, i + mitadAncho, valores[i]);
                item.Title = esPorcentaje
                    ? valores[i].ToString("N1", _cultAR) + "%"
                    : valores[i].ToString("N1", _cultAR);
                serie.Items.Add(item);
            }

            modelo.Axes.Add(ejeX);
            modelo.Axes.Add(ejeY);
            modelo.Series.Add(serie);
            return modelo;
        }

        private void AgregarTextoSinDatos(PlotModel modelo)
        {
            modelo.Annotations.Add(new TextAnnotation
            {
                Text = "Sin datos en el periodo",
                TextPosition = new DataPoint(0.5, 0.5),
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                FontSize = 11,
                TextColor = OxyColor.FromRgb(100, 120, 100),
                StrokeThickness = 0
            });
        }
    }
}
