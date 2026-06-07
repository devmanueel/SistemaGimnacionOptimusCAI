// Paginas/ReportesPage.xaml.cs — C# 7.3
using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class ReportesPage : Page
    {
        private readonly ReporteController _ctrl = new ReporteController();
        private readonly ConfiguracionController _configCtrl = new ConfiguracionController();
        private readonly VentaController _ventaCtrl = new VentaController();

        private List<MovimientoReporte> _movimientos = new List<MovimientoReporte>();
        private TotalesReporte _totales = new TotalesReporte();
        private List<ResumenDocente> _sueldos = new List<ResumenDocente>();

        private DateTime _desde;
        private DateTime _hasta;

        // Tarifa global de docentes (SDD_Fix_Sueldos_Docentes)
        private decimal _tarifaGlobalActual = 4000m;

        public ReportesPage()
        {
            InitializeComponent();
            Loaded += ReportesPage_Loaded;
        }

        private void ReportesPage_Loaded(object sender, RoutedEventArgs e)
        {
            _desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _hasta = DateTime.Today;

            dpDesde.SelectedDate = _desde;
            dpHasta.SelectedDate = _hasta;
            dpSueldoDesde.SelectedDate = _desde;
            dpSueldoHasta.SelectedDate = _hasta;

            lblFechaHoy.Text = "Hoy: " + DateTime.Today.ToString("dddd dd/MM/yyyy");
            lblAnioGrafico.Text = "INGRESOS POR MES — " + DateTime.Today.Year;

            ConfigurarTabsSegunRol();
            CargarFiltrosComboBox();

            if (SesionManager.EsAdmin)
                CargarIngresos();
            else
                MostrarTabMisVentas();
        }

        // ─── CONFIGURACIÓN DE TABS POR ROL ─────────────────────
        private void ConfigurarTabsSegunRol()
        {
            if (SesionManager.EsAdmin)
            {
                btnTabSueldos.Visibility = Visibility.Visible;
                btnTabDeudas.Visibility = Visibility.Visible;
                btnTabIngresos.Visibility = Visibility.Visible;
            }
            else
            {
                panelIngresos.Visibility = Visibility.Collapsed;
                btnTabIngresos.Visibility = Visibility.Collapsed;
                btnTabSueldos.Visibility = Visibility.Collapsed;
                btnTabDeudas.Visibility = Visibility.Collapsed;
                MostrarTabMisVentas();
            }
        }

        // ─── COMBOS ────────────────────────────────────────────
        private void CargarFiltrosComboBox()
        {
            cmbActividad.Items.Clear();
            cmbActividad.Items.Add(new ComboBoxItem { Content = "Todas", Tag = (long?)null });

            try
            {
                var actividades = _ctrl.ListarActividadesParaFiltro();
                foreach (var a in actividades)
                    cmbActividad.Items.Add(new ComboBoxItem { Content = a.Nombre, Tag = (long?)a.Id });
            }
            catch { }
            cmbActividad.SelectedIndex = 0;
            cmbMetodoPago.SelectedIndex = 0;

            cmbInstructor.Items.Clear();
            cmbInstructor.Items.Add(new ComboBoxItem { Content = "Todos", Tag = (long?)null });
            try
            {
                var instructores = _ctrl.ListarInstructoresParaFiltro();
                foreach (var u in instructores)
                    cmbInstructor.Items.Add(new ComboBoxItem
                    {
                        Content = u.Apellido + ", " + u.Nombre,
                        Tag = (long?)u.Id
                    });
            }
            catch { }
            cmbInstructor.SelectedIndex = 0;
        }

        // ─── NAVEGACIÓN DE TABS ─────────────────────────────────
        private void OcultarTodosLosTabPaneles()
        {
            panelIngresos.Visibility = Visibility.Collapsed;
            panelSueldos.Visibility = Visibility.Collapsed;
            panelDeudas.Visibility = Visibility.Collapsed;
            panelMisVentas.Visibility = Visibility.Collapsed;
        }

        private void ResetTabButtons()
        {
            btnTabIngresos.Style = (Style)FindResource("TabBtnStyle");
            btnTabSueldos.Style = (Style)FindResource("TabBtnStyle");
            btnTabDeudas.Style = (Style)FindResource("TabBtnStyle");
            btnTabMisVentas.Style = (Style)FindResource("TabBtnStyle");
        }

        private void TabIngresos_Click(object sender, RoutedEventArgs e)
        {
            OcultarTodosLosTabPaneles();
            ResetTabButtons();
            panelIngresos.Visibility = Visibility.Visible;
            btnTabIngresos.Style = (Style)FindResource("TabBtnActiveStyle");
            CargarIngresos();
        }

        private void TabSueldos_Click(object sender, RoutedEventArgs e)
        {
            OcultarTodosLosTabPaneles();
            ResetTabButtons();
            panelSueldos.Visibility = Visibility.Visible;
            btnTabSueldos.Style = (Style)FindResource("TabBtnActiveStyle");

            // Cargar tarifa global y mostrar alerta aguinaldo (jun/dic)
            CargarTarifaGlobal();
            bool esJunioODiciembre = DateTime.Today.Month == 6 || DateTime.Today.Month == 12;
            panelAlertaAguinaldo.Visibility = esJunioODiciembre
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TabDeudas_Click(object sender, RoutedEventArgs e)
        {
            OcultarTodosLosTabPaneles();
            ResetTabButtons();
            panelDeudas.Visibility = Visibility.Visible;
            btnTabDeudas.Style = (Style)FindResource("TabBtnActiveStyle");
            CargarDeudas();
        }

        private void TabMisVentas_Click(object sender, RoutedEventArgs e)
        {
            OcultarTodosLosTabPaneles();
            ResetTabButtons();
            MostrarTabMisVentas();
        }

        private void MostrarTabMisVentas()
        {
            panelMisVentas.Visibility = Visibility.Visible;
            btnTabMisVentas.Style = (Style)FindResource("TabBtnActiveStyle");
            CargarMisVentas();
        }

        // ─── TAB 1: INGRESOS ────────────────────────────────────
        private void CargarIngresos()
        {
            if (!SesionManager.EsAdmin) return;

            _desde = dpDesde.SelectedDate ?? _desde;
            _hasta = dpHasta.SelectedDate ?? _hasta;

            long? actividadId = (cmbActividad.SelectedItem as ComboBoxItem)?.Tag as long?;
            string metodoPago = ((cmbMetodoPago.SelectedItem as ComboBoxItem)?.Tag as string);
            if (string.IsNullOrEmpty(metodoPago)) metodoPago = null;
            long? instructorId = (cmbInstructor.SelectedItem as ComboBoxItem)?.Tag as long?;

            try
            {
                _movimientos = _ctrl.ObtenerMovimientos(_desde, _hasta, actividadId, metodoPago, instructorId);
                var result = _ctrl.ObtenerTotales(_desde, _hasta);
                _totales = result.totales;

                // Limitar a 500
                bool hayMas = _movimientos.Count > 500;
                lblAviso500.Visibility = hayMas ? Visibility.Visible : Visibility.Collapsed;
                if (hayMas) _movimientos = _movimientos.GetRange(0, 500);

                dgMovimientos.ItemsSource = _movimientos;

                lblTotalIngresos.Text = _totales.TotalIngresosTexto;
                lblTotalEgresos.Text = _totales.TotalEgresosTexto;
                lblBalance.Text = _totales.BalanceTexto;
                lblBalance.Foreground = _totales.BalancePositivo
                    ? new SolidColorBrush(Color.FromRgb(122, 201, 67))
                    : new SolidColorBrush(Color.FromRgb(255, 85, 85));
                lblCantIngresos.Text = _totales.CantidadIngresos + " movimientos";
                lblCantEgresos.Text = _totales.CantidadEgresos + " movimientos";

                CargarGrafico();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar reportes:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnFiltrar_Click(object sender, RoutedEventArgs e) => CargarIngresos();

        // ─── GRÁFICO DE BARRAS — refleja los movimientos del grid ───────────
        private void CargarGrafico()
        {
            gridGraficoMeses.Children.Clear();
            gridGraficoLabels.Children.Clear();
            gridGraficoMeses.ColumnDefinitions.Clear();
            gridGraficoLabels.ColumnDefinitions.Clear();

            if (_movimientos == null || _movimientos.Count == 0) return;

            // Agrupar por Año-Mes usando los movimientos filtrados (mismo grid)
            var mapa = new Dictionary<string, IngresosPorMes>();
            foreach (var mov in _movimientos)
            {
                string clave = mov.Fecha.Year + "-" + mov.Fecha.Month.ToString("D2");
                IngresosPorMes item;
                if (!mapa.TryGetValue(clave, out item))
                {
                    item = new IngresosPorMes
                    {
                        Mes = mov.Fecha.Month,
                        MesNombre = new DateTime(mov.Fecha.Year, mov.Fecha.Month, 1)
                            .ToString("MMM", new CultureInfo("es-AR")),
                        Ingresos = 0,
                        Egresos = 0
                    };
                    mapa.Add(clave, item);
                }

                if (mov.EsIngreso)
                    item.Ingresos += mov.Monto;
                else
                    item.Egresos += mov.Monto;
            }

            // Generar meses desde enero hasta el mes actual del año filtrado
            int anioGrafico = _hasta.Year;
            int mesLimite = (anioGrafico == DateTime.Today.Year)
                ? DateTime.Today.Month : 12;

            var mesesMostrar = new List<IngresosPorMes>(mesLimite);
            for (int m = 1; m <= mesLimite; m++)
            {
                string clave = anioGrafico + "-" + m.ToString("D2");
                IngresosPorMes item;
                if (mapa.TryGetValue(clave, out item))
                    mesesMostrar.Add(item);
                else
                    mesesMostrar.Add(new IngresosPorMes
                    {
                        Mes = m,
                        MesNombre = new DateTime(anioGrafico, m, 1)
                            .ToString("MMM", new CultureInfo("es-AR")),
                        Ingresos = 0,
                        Egresos = 0
                    });
            }

            // Título según año y período filtrado
            lblAnioGrafico.Text = "INGRESOS Y EGRESOS POR MES — " + anioGrafico;

            decimal maxVal = 0;
            foreach (var d in mesesMostrar)
            {
                decimal m = d.Ingresos > d.Egresos ? d.Ingresos : d.Egresos;
                if (m > maxVal) maxVal = m;
            }
            if (maxVal == 0) maxVal = 1;

            double maxBarH = 160;
            double barW = 18;

            var brushIng = new LinearGradientBrush(
                Color.FromRgb(0, 230, 200), Color.FromRgb(0, 140, 180), 90);
            var brushEgr = new LinearGradientBrush(
                Color.FromRgb(190, 160, 255), Color.FromRgb(130, 100, 220), 90);
            var brushEmpty = new SolidColorBrush(Color.FromRgb(40, 60, 40));

            for (int i = 0; i < mesesMostrar.Count; i++)
            {
                gridGraficoMeses.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridGraficoLabels.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            var lineaBase = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(30, 48, 32)),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetColumnSpan(lineaBase, mesesMostrar.Count);
            gridGraficoMeses.Children.Add(lineaBase);

            for (int col = 0; col < mesesMostrar.Count; col++)
            {
                var d = mesesMostrar[col];
                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 2)
                };

                double altIng = d.Ingresos > 0
                    ? (double)(d.Ingresos / maxVal) * maxBarH
                    : 0;
                if (altIng < 4 && d.Ingresos > 0) altIng = 4;

                if (altIng > 0)
                {
                    stack.Children.Add(new Border
                    {
                        Width = barW,
                        Height = altIng,
                        Background = brushIng,
                        CornerRadius = new CornerRadius(3, 3, 0, 0),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 2, 0),
                        ToolTip = d.MesNombre.ToUpper() + " — Ingresos: " + FormatoARS.Moneda(d.Ingresos)
                    });
                }

                double altEgr = d.Egresos > 0
                    ? (double)(d.Egresos / maxVal) * maxBarH
                    : 0;
                if (altEgr < 4 && d.Egresos > 0) altEgr = 4;

                if (altEgr > 0)
                {
                    stack.Children.Add(new Border
                    {
                        Width = barW,
                        Height = altEgr,
                        Background = brushEgr,
                        CornerRadius = new CornerRadius(3, 3, 0, 0),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        ToolTip = d.MesNombre.ToUpper() + " — Egresos: " + FormatoARS.Moneda(d.Egresos)
                    });
                }

                if (stack.Children.Count == 0)
                {
                    stack.Children.Add(new Border
                    {
                        Width = barW,
                        Height = 4,
                        Background = brushEmpty,
                        CornerRadius = new CornerRadius(2),
                        VerticalAlignment = VerticalAlignment.Bottom
                    });
                }

                Grid.SetColumn(stack, col);
                gridGraficoMeses.Children.Add(stack);

                var lbl = new TextBlock
                {
                    Text = d.MesNombre.ToUpper(),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(130, 150, 130)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
                };
                Grid.SetColumn(lbl, col);
                gridGraficoLabels.Children.Add(lbl);
            }
        }

        // ─── TAB 2: SUELDOS (tarifa global, SDD_Fix_Sueldos_Docentes) ──────────
        private void CargarTarifaGlobal()
        {
            try
            {
                _tarifaGlobalActual = _configCtrl.ObtenerTarifaHoraDocentes();
                txtTarifaGlobal.Text = _tarifaGlobalActual.ToString("N0",
                    new CultureInfo("es-AR"));
            }
            catch
            {
                _tarifaGlobalActual = 4000m;
                txtTarifaGlobal.Text = "4.000";
            }
        }

        private void btnConsultarSueldos_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionManager.EsAdmin) return;

            // El SP ya usa la tarifa global, pero si el usuario tipeó y aún no guardó,
            // recalculamos en memoria con _tarifaGlobalActual.
            try
            {
                // Defensivo: salir de cualquier edición pendiente antes de reasignar ItemsSource
                // (evita el error "No se permite Refresh durante AddNew o EditItem").
                dgSueldos.CommitEdit(DataGridEditingUnit.Cell, true);
                dgSueldos.CommitEdit(DataGridEditingUnit.Row, true);

                var desde = dpSueldoDesde.SelectedDate;
                var hasta = dpSueldoHasta.SelectedDate;

                _sueldos = _ctrl.ObtenerSueldosDocentes(desde, hasta);

                foreach (var d in _sueldos)
                {
                    d.TarifaHora = _tarifaGlobalActual;
                    d.SueldoEstimado = d.HorasTotales * _tarifaGlobalActual;
                }

                dgSueldos.ItemsSource = _sueldos;
                ActualizarTotalSueldos();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActualizarTotalSueldos()
        {
            decimal total = 0;
            if (_sueldos != null)
                foreach (var d in _sueldos) total += d.SueldoEstimado;
            lblTotalSueldos.Text = FormatoARS.Moneda(total);
        }

        // ─── GUARDAR TARIFA GLOBAL ─────────────────────────────────────────────
        private void btnGuardarTarifa_Click(object sender, RoutedEventArgs e)
        {
            decimal nuevaTarifa;
            if (!FormatoARS.TryParsear(txtTarifaGlobal.Text, out nuevaTarifa))
            {
                NotificacionWindow.MostrarError(
                    "Ingresá un número válido. Ejemplo: 4000 o 4.000,00");
                return;
            }

            var resultado = _configCtrl.ActualizarTarifaHoraDocentes(
                nuevaTarifa, SesionManager.UsuarioId);

            if (resultado.ok)
            {
                _tarifaGlobalActual = nuevaTarifa;
                NotificacionWindow.MostrarExito(resultado.mensaje);
                // Si ya había datos cargados, recalcular en memoria sin re-query
                if (_sueldos != null && _sueldos.Count > 0)
                {
                    foreach (var d in _sueldos)
                    {
                        d.TarifaHora = nuevaTarifa;
                        d.SueldoEstimado = d.HorasTotales * nuevaTarifa;
                    }
                    dgSueldos.Items.Refresh();
                    ActualizarTotalSueldos();
                }
                // Reformatear el input con separadores
                txtTarifaGlobal.Text = nuevaTarifa.ToString("N0", new CultureInfo("es-AR"));
            }
            else
            {
                NotificacionWindow.MostrarError(resultado.mensaje);
            }
        }

        private void txtTarifaGlobal_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) btnGuardarTarifa_Click(sender, e);
        }

        // ─── TAB 3: DEUDAS ──────────────────────────────────────
        private void CargarDeudas()
        {
            if (!SesionManager.EsAdmin) return;
            try
            {
                var result = _ctrl.ObtenerSociosDeuda(7);
                dgVencidas.ItemsSource = result.vencidas;
                dgProximas.ItemsSource = result.proximas;
                lblCountVencidas.Text = result.vencidas.Count.ToString();
                lblCountProximas.Text = result.proximas.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnActualizarDeudas_Click(object sender, RoutedEventArgs e) => CargarDeudas();

        private void btnWhatsAppDeuda_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var socio = btn?.Tag as SocioConDeuda;
            if (socio == null) return;

            string tel = (socio.Telefono ?? "").Replace("+", "").Replace(" ", "").Replace("-", "");
            if (string.IsNullOrEmpty(tel))
            {
                MessageBox.Show("El socio no tiene teléfono registrado.", "Sin teléfono",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string msg = Uri.EscapeDataString(
                "Hola " + socio.NombreCompleto.Split(' ')[0] + ", te avisamos desde OptimusCAI Gym " +
                "que tu membresía de " + socio.ActividadNombre + " " + socio.AlertaTexto.ToLower() + ". " +
                "Comunicate para renovarla. ¡Gracias!");

            try { Process.Start("https://wa.me/" + tel + "?text=" + msg); }
            catch { }
        }

        // ─── TAB 4: MIS VENTAS ──────────────────────────────────
        private void CargarMisVentas()
        {
            try
            {
                var result = _ctrl.ObtenerMisVentasDelDia();
                dgMisVentas.ItemsSource = result.ventas;
                lblResumenVentas.Text =
                    "Hoy vendiste " + result.cantidadVentas + " " +
                    (result.cantidadVentas == 1 ? "vez" : "veces") +
                    " por un total de $" + result.totalDia.ToString("N2");
            }
            catch (Exception ex)
            {
                lblResumenVentas.Text = "No se pudieron cargar las ventas.";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnActualizarMisVentas_Click(object sender, RoutedEventArgs e) => CargarMisVentas();

        // ─── DETALLE DE UNA VENTA (popup, igual que la sección Ventas) ───
        private void btnVerDetalleVenta_Click(object sender, RoutedEventArgs e)
        {
            var v = (sender as Button)?.DataContext as VentaEmpleado;
            if (v == null) return;

            try
            {
                var venta = _ventaCtrl.ObtenerPorId(v.Id);
                if (venta == null)
                {
                    NotificacionWindow.MostrarError("No se pudo cargar el detalle de la venta.");
                    return;
                }

                lblDetalleNumero.Text = "Venta " + venta.NumeroVenta;
                lblDetalleTotal.Text = venta.TotalTexto;
                lblDetalleMetodo.Text = "Método: " + venta.MetodoPagoTexto;

                panelDetalleVenta.Children.Clear();
                panelDetalleVenta.Children.Add(FilaDetalle("PRODUCTO", "CANT.", "SUBTOTAL", true));
                foreach (var item in venta.Items)
                    panelDetalleVenta.Children.Add(FilaDetalle(
                        item.ProductoNombre ?? item.Descripcion,
                        item.Cantidad.ToString(),
                        item.SubtotalTexto, false));

                modalDetalleVenta.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message);
            }
        }

        private void btnCerrarDetalleVenta_Click(object sender, RoutedEventArgs e)
        {
            modalDetalleVenta.Visibility = Visibility.Collapsed;
        }

        private Grid FilaDetalle(string c1, string c2, string c3, bool header)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Color color = header ? Color.FromRgb(138, 154, 138) : Color.FromRgb(200, 220, 200);

            TextBlock Tb(string t, int col, TextAlignment align)
            {
                var tb = new TextBlock
                {
                    Text = t,
                    FontSize = header ? 10 : 12,
                    FontWeight = header ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(color),
                    TextAlignment = align,
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetColumn(tb, col);
                return tb;
            }

            g.Children.Add(Tb(c1, 0, TextAlignment.Left));
            g.Children.Add(Tb(c2, 1, TextAlignment.Center));
            g.Children.Add(Tb(c3, 2, TextAlignment.Right));
            return g;
        }

        // ─── EXPORTAR INGRESOS ──────────────────────────────────
        private void btnExportarPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_movimientos == null || _movimientos.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar.", "Sin datos",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                var exp = new ReportePdfExportador();
                string path = exp.ExportarIngresos(_movimientos, _totales, _desde, _hasta);
                Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar PDF:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_movimientos == null || _movimientos.Count == 0)
            {
                MessageBox.Show("No hay datos para exportar.", "Sin datos",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                var exp = new ReporteExcelExportador();
                string path = exp.ExportarIngresos(_movimientos, _totales, _desde, _hasta);
                Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar Excel:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── EXPORTAR SUELDOS ───────────────────────────────────
        private void btnExportarSueldosPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_sueldos == null || _sueldos.Count == 0)
            {
                MessageBox.Show("Primero consulte los sueldos.", "Sin datos",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                var exp = new ReportePdfExportador();
                string path = exp.ExportarSueldos(_sueldos,
                    dpSueldoDesde.SelectedDate ?? _desde,
                    dpSueldoHasta.SelectedDate ?? _hasta);
                Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar PDF:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportarSueldosExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_sueldos == null || _sueldos.Count == 0)
            {
                MessageBox.Show("Primero consulte los sueldos.", "Sin datos",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                var exp = new ReporteExcelExportador();
                string path = exp.ExportarSueldos(_sueldos,
                    dpSueldoDesde.SelectedDate ?? _desde,
                    dpSueldoHasta.SelectedDate ?? _hasta);
                Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar Excel:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── EXPORTAR DEUDAS ────────────────────────────────────
        private void btnExportarDeudasPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _ctrl.ObtenerSociosDeuda(7);
                var exp = new ReportePdfExportador();
                string path = exp.ExportarDeudas(result.vencidas, result.proximas);
                Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar PDF:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}