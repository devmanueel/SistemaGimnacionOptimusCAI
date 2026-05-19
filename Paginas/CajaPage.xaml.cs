// ============================================================
//  Archivo: CajaPage.xaml.cs
//
//  Code-behind con:
//   · Dashboard del balance del día (ingresos / gastos / total)
//   · Gráfico de barras dinámico con los últimos 7 días
//   · Filtros por tipo (Todos / Ingresos / Gastos) y rango fechas
//   · Formulario que cambia entre INGRESO y GASTO según el botón
//   · Conceptos sugeridos según el tipo
//
//  Compatible con C# 7.3.
// ============================================================

using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class CajaPage : Page
    {
        private readonly CajaController _controller = new CajaController();

        private string _filtroTipo = "todos";
        private string _modoForm = "ingreso";   // "ingreso" o "gasto"

        // TODO: reemplazar por el ID del usuario logueado cuando exista el sistema de login
        private const long USUARIO_ACTUAL_ID = 1;

        // Conceptos sugeridos para los autocompletes
        private static readonly string[] ConceptosIngreso = {
            "Clase suelta", "Pase diario", "Inscripción", "Otro ingreso"
        };
        private static readonly string[] ConceptosGasto = {
            "Alquiler", "Sueldos", "Servicios (luz/gas)", "Internet", "Limpieza",
            "Mantenimiento", "Equipamiento", "Insumos", "Marketing", "Impuestos", "Otro"
        };

        public CajaPage()
        {
            InitializeComponent();

            // Default: últimos 30 días
            dpDesde.SelectedDate = DateTime.Today.AddDays(-30);
            dpHasta.SelectedDate = DateTime.Today;

            ResaltarChip(chipTodos);
            CargarTodo();
        }

        // ─────────────────────────────────────────────────────
        // CARGA GENERAL
        // ─────────────────────────────────────────────────────
        private void CargarTodo()
        {
            CargarMovimientos();
            CargarDashboard();
            CargarGrafico7Dias();
        }

        private void CargarMovimientos()
        {
            try
            {
                var lista = _controller.BuscarMovimientos(
                    txtBuscar.Text,
                    _filtroTipo,
                    dpDesde.SelectedDate,
                    dpHasta.SelectedDate);

                gridMovimientos.ItemsSource = lista;
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar movimientos");
            }
        }

        private void CargarDashboard()
        {
            try
            {
                // Resumen del día
                var resumenDia = _controller.ResumenDelDia();
                lblIngresosDia.Text = "$" + resumenDia.TotalIngresos.ToString("N0");
                lblGastosDia.Text = "$" + resumenDia.TotalGastos.ToString("N0");
                lblBalanceDia.Text = resumenDia.BalanceTexto;

                // Color del balance: verde si positivo, rojo si negativo
                lblBalanceDia.Foreground = resumenDia.Balance >= 0
                    ? new SolidColorBrush(Color.FromRgb(0, 230, 118))     // #00E676
                    : new SolidColorBrush(Color.FromRgb(255, 85, 85));    // #FF5555

                // Resumen del mes
                var resumenMes = _controller.ResumenDelMes();
                lblBalanceMes.Text = resumenMes.BalanceTexto;
                lblMovimientosMes.Text = resumenMes.CantidadMovimientos + " movim.";

                lblBalanceMes.Foreground = resumenMes.Balance >= 0
                    ? new SolidColorBrush(Color.FromRgb(0, 207, 255))     // #00CFFF
                    : new SolidColorBrush(Color.FromRgb(255, 85, 85));
            }
            catch
            {
                lblBalanceDia.Text = lblIngresosDia.Text = lblGastosDia.Text = "$0";
                lblBalanceMes.Text = "$0";
            }
        }

        // ─────────────────────────────────────────────────────
        // GRÁFICO DE BARRAS — 7 días
        // ─────────────────────────────────────────────────────
        private void CargarGrafico7Dias()
        {
            try
            {
                gridChart.Children.Clear();
                gridChartLabels.Children.Clear();

                var datos = _controller.ObtenerUltimos7Dias();
                if (datos.Count == 0) return;

                // Encontrar el máximo para normalizar las alturas
                decimal maxValor = 1;
                foreach (var d in datos)
                {
                    decimal m = Math.Max(d.Ingresos, d.Gastos);
                    if (m > maxValor) maxValor = m;
                }

                int columna = 0;
                foreach (var d in datos)
                {
                    // Contenedor de la columna del gráfico
                    var stack = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(2, 0, 2, 0)
                    };

                    // Barra de ingresos (verde)
                    double altIng = d.Ingresos > 0
                        ? (double)(d.Ingresos / maxValor) * 70 + 4
                        : 2;
                    var barraIng = new Border
                    {
                        Width = 10,
                        Height = altIng,
                        Background = new SolidColorBrush(Color.FromRgb(0, 230, 118)),
                        CornerRadius = new CornerRadius(3, 3, 0, 0),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 2, 0),
                        ToolTip = "Ingresos: $" + d.Ingresos.ToString("N0")
                    };

                    // Barra de gastos (rojo)
                    double altGas = d.Gastos > 0
                        ? (double)(d.Gastos / maxValor) * 70 + 4
                        : 2;
                    var barraGas = new Border
                    {
                        Width = 10,
                        Height = altGas,
                        Background = new SolidColorBrush(Color.FromRgb(255, 85, 85)),
                        CornerRadius = new CornerRadius(3, 3, 0, 0),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        ToolTip = "Gastos: $" + d.Gastos.ToString("N0")
                    };

                    stack.Children.Add(barraIng);
                    stack.Children.Add(barraGas);

                    Grid.SetColumn(stack, columna);
                    gridChart.Children.Add(stack);

                    // Label del día
                    var lbl = new TextBlock
                    {
                        Text = d.DiaSemana,
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontFamily = new FontFamily("Segoe UI")
                    };
                    Grid.SetColumn(lbl, columna);
                    gridChartLabels.Children.Add(lbl);

                    columna++;
                }
            }
            catch
            {
                // Silencioso: el gráfico es decorativo, no crítico
            }
        }

        // ─────────────────────────────────────────────────────
        // FILTROS
        // ─────────────────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => CargarMovimientos();

        private void dpFecha_Changed(object sender, SelectionChangedEventArgs e)
        {
            CargarMovimientos();
            CargarDashboard();
        }

        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            _filtroTipo = btn.Tag.ToString();
            ResaltarChip(btn);
            CargarMovimientos();
        }

        private void ResaltarChip(Button seleccionado)
        {
            Button[] chips = { chipTodos, chipIngresos, chipGastos };
            foreach (var c in chips)
            {
                // Al asignar el estilo completo, recuperás el espaciado y los efectos automáticos
                if (c == seleccionado)
                {
                    c.Style = (Style)FindResource("BotonChipActivoEstilo");
                }
                else
                {
                    c.Style = (Style)FindResource("BotonChipEstilo");
                }
            }
        }

        // ─────────────────────────────────────────────────────
        // BOTONES PRINCIPALES
        // ─────────────────────────────────────────────────────
        private void btnIngreso_Click(object sender, RoutedEventArgs e)
        {
            _modoForm = "ingreso";
            ConfigurarFormulario();
            AbrirFormulario();
        }

        private void btnGasto_Click(object sender, RoutedEventArgs e)
        {
            _modoForm = "gasto";
            ConfigurarFormulario();
            AbrirFormulario();
        }

        /// <summary>Configura el formulario según el modo (ingreso o gasto).</summary>
        private void ConfigurarFormulario()
        {
            LimpiarFormulario();
            LimpiarErrores();

            if (_modoForm == "ingreso")
            {
                lblTituloFormulario.Text = "REGISTRAR INGRESO";
                btnGuardar.Content = "REGISTRAR INGRESO";
                lineaSuperior.Background = new SolidColorBrush(Color.FromRgb(0, 230, 118));

                // ← Resetear ícono a verde
                iconoFormulario.Icon = FontAwesome.WPF.FontAwesomeIcon.PlusCircle;
                iconoFormulario.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));

                cmbConcepto.ItemsSource = ConceptosIngreso;
            }
            else
            {
                lblTituloFormulario.Text = "REGISTRAR GASTO";
                btnGuardar.Content = "REGISTRAR GASTO";
                lineaSuperior.Background = new SolidColorBrush(Color.FromRgb(255, 85, 85));

                // ← Ícono rojo (ya lo tenías, sacá el duplicado)
                iconoFormulario.Icon = FontAwesome.WPF.FontAwesomeIcon.MinusCircle;
                iconoFormulario.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));

                cmbConcepto.ItemsSource = ConceptosGasto;
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            var mov = ObtenerMovimientoDeFila(sender);
            if (mov == null) return;

            // Validación rápida en cliente — el SP también lo valida
            if (mov.Tipo == "ingreso_cuota" || mov.Tipo == "ingreso_venta")
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Los ingresos por cuotas o ventas no se pueden eliminar acá. " +
                    "Cancelá la membresía o la venta desde su módulo correspondiente.",
                    "No permitido");
                return;
            }

            string concepto = mov.DescripcionInteligente;
            bool confirmo = NotificacionWindow.MostrarConfirmacion(
                "¿Eliminar este movimiento?\n\n" +
                "💰 " + mov.MontoConSigno + "\n" +
                "📝 " + concepto + "\n" +
                "📅 " + mov.FechaLarga + "\n\n" +
                "Esta acción no se puede deshacer.",
                "Eliminar movimiento");

            if (!confirmo) return;

            try
            {
                var r = _controller.EliminarMovimiento(mov.Id);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarTodo(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string concepto = (cmbConcepto.Text ?? string.Empty).Trim();
            decimal monto = 0;

            // Validar concepto
            if (string.IsNullOrWhiteSpace(concepto) || concepto.Length < 3)
            {
                AplicarEstadoCampo(null, errConcepto, "El concepto debe tener al menos 3 caracteres.");
                NotificacionWindow.MostrarAdvertencia("Faltan datos en el formulario.");
                return;
            }
            else
            {
                AplicarEstadoCampo(null, errConcepto, null);
            }

            // Validar monto
            string errM = null;
            if (string.IsNullOrWhiteSpace(txtMonto.Text))
                errM = "El monto es obligatorio.";
            else if (!decimal.TryParse(txtMonto.Text, out monto) || monto <= 0)
                errM = "El monto debe ser un número mayor a 0.";

            AplicarEstadoCampo(txtMonto, errMonto, errM);
            if (errM != null)
            {
                NotificacionWindow.MostrarAdvertencia(errM);
                return;
            }

            // Método de pago
            var metodoItem = cmbMetodoPago.SelectedItem as ComboBoxItem;
            string metodoPago = metodoItem != null && metodoItem.Tag != null
                ? metodoItem.Tag.ToString() : "efectivo";

            // Llamar al controller según el modo
            if (_modoForm == "ingreso")
            {
                var r = _controller.RegistrarIngresoManual(
                    USUARIO_ACTUAL_ID,
                    "ingreso_clase",   // Por defecto los manuales son clases sueltas
                    concepto,
                    null,
                    txtDetalle.Text,
                    monto,
                    metodoPago);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Ingreso registrado!");
            }
            else
            {
                var r = _controller.RegistrarGasto(
                    USUARIO_ACTUAL_ID,
                    concepto,
                    txtDetalle.Text,
                    monto,
                    metodoPago);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Gasto registrado!");
            }

            CerrarFormulario();
            CargarTodo();
        }

        private void btnCancelarFormulario_Click(object sender, RoutedEventArgs e) => CerrarFormulario();

        // ─────────────────────────────────────────────────────
        // VALIDACIONES INLINE
        // ─────────────────────────────────────────────────────
        private void txtMonto_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[\d]$");
        }

        private void txtMonto_LostFocus(object sender, RoutedEventArgs e)
        {
            decimal monto = 0;
            string err = null;

            if (string.IsNullOrWhiteSpace(txtMonto.Text))
                err = "El monto es obligatorio.";
            else if (!decimal.TryParse(txtMonto.Text, out monto) || monto <= 0)
                err = "El monto debe ser mayor a $0.";

            AplicarEstadoCampo(txtMonto, errMonto, err);
            ActualizarPreviewMonto();
        }

        private void ActualizarPreviewMonto()
        {
            decimal monto = 0;
            if (decimal.TryParse(txtMonto.Text, out monto) && monto > 0)
            {
                if (_modoForm == "ingreso")
                {
                    lblPreviewMonto.Text = "+$" + monto.ToString("N0");
                    lblPreviewMonto.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                    iconoPreviewMonto.Icon = FontAwesome.WPF.FontAwesomeIcon.PlusCircle;
                    iconoPreviewMonto.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                    panelPreviewMonto.Background = new SolidColorBrush(Color.FromArgb(255, 10, 26, 10));
                }
                else
                {
                    lblPreviewMonto.Text = "-$" + monto.ToString("N0");
                    lblPreviewMonto.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                    iconoPreviewMonto.Icon = FontAwesome.WPF.FontAwesomeIcon.MinusCircle;
                    iconoPreviewMonto.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                    panelPreviewMonto.Background = new SolidColorBrush(Color.FromArgb(255, 42, 10, 10));
                }

                panelPreviewMonto.Visibility = Visibility.Visible;
            }
            else
            {
                panelPreviewMonto.Visibility = Visibility.Collapsed;
            }
        }

        private void AplicarEstadoCampo(TextBox campo, TextBlock labelError, string mensajeError)
        {
            if (mensajeError != null)
            {
                if (campo != null) campo.Style = (Style)Resources["InputErrorEstilo"];
                labelError.Text = mensajeError;
                labelError.Visibility = Visibility.Visible;
            }
            else
            {
                if (campo != null) campo.Style = (Style)Resources["InputEstilo"];
                labelError.Text = string.Empty;
                labelError.Visibility = Visibility.Collapsed;
            }
        }

        private void LimpiarErrores()
        {
            errMonto.Text = string.Empty;
            errMonto.Visibility = Visibility.Collapsed;
            errConcepto.Text = string.Empty;
            errConcepto.Visibility = Visibility.Collapsed;
            txtMonto.Style = (Style)Resources["InputEstilo"];
        }

        // ─────────────────────────────────────────────────────
        // ANIMACIONES
        // ─────────────────────────────────────────────────────
        private void AbrirFormulario()
        {
            panelFormulario.Visibility = Visibility.Visible;
            panelFormulario.Opacity = 0;

            var translate = new TranslateTransform { X = 60 };
            panelFormulario.RenderTransform = translate;

            var slide = new DoubleAnimation
            {
                From = 60,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            translate.BeginAnimation(TranslateTransform.XProperty, slide);

            var fade = new DoubleAnimation
            { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(300)) };
            panelFormulario.BeginAnimation(OpacityProperty, fade);
        }

        private void CerrarFormulario()
        {
            var fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (s, ev) =>
            {
                panelFormulario.Visibility = Visibility.Collapsed;
                LimpiarFormulario();
                LimpiarErrores();
            };
            panelFormulario.BeginAnimation(OpacityProperty, fade);
        }

        // ─────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────
        private void LimpiarFormulario()
        {
            cmbConcepto.Text = string.Empty;
            cmbConcepto.SelectedIndex = -1;
            txtMonto.Text = string.Empty;
            cmbMetodoPago.SelectedIndex = 0;
            txtDetalle.Text = string.Empty;
            panelPreviewMonto.Visibility = Visibility.Collapsed;
        }

        private CajaMovimiento ObtenerMovimientoDeFila(object sender)
        {
            var btn = sender as Button;
            if (btn == null) return null;
            return btn.DataContext as CajaMovimiento;
        }

        private void btnRangoHoy_Click(object sender, RoutedEventArgs e)
        {
            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today;
        }
        private void btnRangoSemana_Click(object sender, RoutedEventArgs e)
        {
            dpDesde.SelectedDate = DateTime.Today.AddDays(-6);
            dpHasta.SelectedDate = DateTime.Today;
        }
        private void btnRangoMes_Click(object sender, RoutedEventArgs e)
        {
            dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpHasta.SelectedDate = DateTime.Today;
        }
    }
}