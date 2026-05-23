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

            dpDesde.SelectedDate  = _desde;
            dpHasta.SelectedDate  = _hasta;
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
                btnTabSueldos.Visibility   = Visibility.Visible;
                btnTabDeudas.Visibility    = Visibility.Visible;
                btnTabIngresos.Visibility  = Visibility.Visible;
            }
            else
            {
                panelIngresos.Visibility  = Visibility.Collapsed;
                btnTabIngresos.Visibility = Visibility.Collapsed;
                btnTabSueldos.Visibility  = Visibility.Collapsed;
                btnTabDeudas.Visibility   = Visibility.Collapsed;
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
                        Tag     = (long?)u.Id
                    });
            }
            catch { }
            cmbInstructor.SelectedIndex = 0;
        }

        // ─── NAVEGACIÓN DE TABS ─────────────────────────────────
        private void OcultarTodosLosTabPaneles()
        {
            panelIngresos.Visibility  = Visibility.Collapsed;
            panelSueldos.Visibility   = Visibility.Collapsed;
            panelDeudas.Visibility    = Visibility.Collapsed;
            panelMisVentas.Visibility = Visibility.Collapsed;
        }

        private void ResetTabButtons()
        {
            btnTabIngresos.Style  = (Style)FindResource("TabBtnStyle");
            btnTabSueldos.Style   = (Style)FindResource("TabBtnStyle");
            btnTabDeudas.Style    = (Style)FindResource("TabBtnStyle");
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

            long? actividadId  = (cmbActividad.SelectedItem as ComboBoxItem)?.Tag as long?;
            string metodoPago  = ((cmbMetodoPago.SelectedItem as ComboBoxItem)?.Tag as string);
            if (string.IsNullOrEmpty(metodoPago)) metodoPago = null;
            long? instructorId = (cmbInstructor.SelectedItem as ComboBoxItem)?.Tag as long?;

            try
            {
                _movimientos = _ctrl.ObtenerMovimientos(_desde, _hasta, actividadId, metodoPago, instructorId);
                var result   = _ctrl.ObtenerTotales(_desde, _hasta);
                _totales     = result.totales;

                // Limitar a 500
                bool hayMas = _movimientos.Count > 500;
                lblAviso500.Visibility = hayMas ? Visibility.Visible : Visibility.Collapsed;
                if (hayMas) _movimientos = _movimientos.GetRange(0, 500);

                dgMovimientos.ItemsSource = _movimientos;

                lblTotalIngresos.Text = _totales.TotalIngresosTexto;
                lblTotalEgresos.Text  = _totales.TotalEgresosTexto;
                lblBalance.Text       = _totales.BalanceTexto;
                lblBalance.Foreground = _totales.BalancePositivo
                    ? new SolidColorBrush(Color.FromRgb(122, 201, 67))
                    : new SolidColorBrush(Color.FromRgb(255, 85, 85));
                lblCantIngresos.Text = _totales.CantidadIngresos + " movimientos";
                lblCantEgresos.Text  = _totales.CantidadEgresos  + " movimientos";

                CargarGrafico();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar reportes:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnFiltrar_Click(object sender, RoutedEventArgs e) => CargarIngresos();

        // ─── GRÁFICO DE BARRAS WPF CANVAS ───────────────────────
        private void CargarGrafico()
        {
            canvasGrafico.Children.Clear();
            listaMeses.ItemsSource = null;

            var datos = _ctrl.ObtenerGraficoPorMes(DateTime.Today.Year);
            if (datos == null || datos.Count == 0) return;

            listaMeses.ItemsSource = datos;

            decimal maxVal = 0;
            foreach (var d in datos)
                if (d.Ingresos > maxVal) maxVal = d.Ingresos;
            if (maxVal == 0) maxVal = 1;

            double canvasH = canvasGrafico.ActualHeight;
            if (canvasH < 1) canvasH = 180;
            double barWidth  = canvasGrafico.ActualWidth > 0
                ? (canvasGrafico.ActualWidth - 20) / datos.Count : 60;
            double groupW    = barWidth * 0.8;

            for (int i = 0; i < datos.Count; i++)
            {
                var d = datos[i];
                double x = 10 + i * barWidth;

                // Barra ingresos (cyan)
                double hIngreso = (double)(d.Ingresos / maxVal) * (canvasH - 10);
                var barI = new Rectangle
                {
                    Width  = groupW * 0.55,
                    Height = Math.Max(hIngreso, 2),
                    Fill   = new LinearGradientBrush(
                        Color.FromRgb(0, 207, 255), Color.FromRgb(0, 140, 200), 90)
                };
                Canvas.SetLeft(barI, x);
                Canvas.SetTop(barI, canvasH - barI.Height);
                canvasGrafico.Children.Add(barI);

                // Barra egresos (violeta)
                double hEgreso = (double)(d.Egresos / maxVal) * (canvasH - 10);
                var barE = new Rectangle
                {
                    Width  = groupW * 0.40,
                    Height = Math.Max(hEgreso, 2),
                    Fill   = new SolidColorBrush(Color.FromRgb(167, 139, 250))
                };
                Canvas.SetLeft(barE, x + groupW * 0.58);
                Canvas.SetTop(barE, canvasH - barE.Height);
                canvasGrafico.Children.Add(barE);
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
                dgSueldos.CommitEdit(DataGridEditingUnit.Row,  true);

                var desde = dpSueldoDesde.SelectedDate;
                var hasta = dpSueldoHasta.SelectedDate;

                _sueldos = _ctrl.ObtenerSueldosDocentes(desde, hasta);

                foreach (var d in _sueldos)
                {
                    d.TarifaHora     = _tarifaGlobalActual;
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
                        d.TarifaHora     = nuevaTarifa;
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
                dgVencidas.ItemsSource   = result.vencidas;
                dgProximas.ItemsSource   = result.proximas;
                lblCountVencidas.Text    = result.vencidas.Count.ToString();
                lblCountProximas.Text    = result.proximas.Count.ToString();
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
            var btn   = sender as Button;
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
                lblResumenVentas.Text   =
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
                var exp  = new ReportePdfExportador();
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
                var exp  = new ReporteExcelExportador();
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
                var exp  = new ReportePdfExportador();
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
                var exp  = new ReporteExcelExportador();
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
                var exp    = new ReportePdfExportador();
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
