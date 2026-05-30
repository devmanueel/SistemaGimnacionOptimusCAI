using Controllers;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class MovimientoCajaWindow : Window
    {
        private readonly CajaController _controller = new CajaController();
        private readonly string _modo;

        private static readonly string[] ConceptosIngreso = {
            "Clase suelta", "Pase diario", "Inscripción", "Otro ingreso"
        };
        private static readonly string[] ConceptosGasto = {
            "Alquiler", "Sueldos", "Servicios (luz/gas)", "Internet", "Limpieza",
            "Mantenimiento", "Equipamiento", "Insumos", "Marketing", "Impuestos", "Otro"
        };

        public MovimientoCajaWindow(string modo)
        {
            InitializeComponent();
            _modo = modo;
            ConfigurarSegunModo();
        }

        private void ConfigurarSegunModo()
        {
            if (_modo == "ingreso")
            {
                lblTituloFormulario.Text = "REGISTRAR INGRESO";
                btnGuardar.Content = "REGISTRAR INGRESO";
                lineaSuperior.Background = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                iconoFormulario.Icon = FontAwesome.WPF.FontAwesomeIcon.PlusCircle;
                iconoFormulario.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                cmbConcepto.ItemsSource = ConceptosIngreso;
            }
            else
            {
                lblTituloFormulario.Text = "REGISTRAR GASTO";
                btnGuardar.Content = "REGISTRAR GASTO";
                lineaSuperior.Background = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                iconoFormulario.Icon = FontAwesome.WPF.FontAwesomeIcon.MinusCircle;
                iconoFormulario.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                cmbConcepto.ItemsSource = ConceptosGasto;
            }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string concepto = (cmbConcepto.Text ?? string.Empty).Trim();
            decimal monto = 0;

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

            var metodoItem = cmbMetodoPago.SelectedItem as ComboBoxItem;
            string metodoPago = metodoItem != null && metodoItem.Tag != null
                ? metodoItem.Tag.ToString() : "efectivo";

            long usuarioId = SesionManager.UsuarioId;

            if (_modo == "ingreso")
            {
                var r = _controller.RegistrarIngresoManual(
                    usuarioId,
                    "ingreso_clase",
                    concepto,
                    null,
                    txtDetalle.Text,
                    monto,
                    metodoPago);

                if (!r.ok)
                {
                    NotificacionWindow.MostrarError(r.mensaje);
                    return;
                }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Ingreso registrado!");
            }
            else
            {
                var r = _controller.RegistrarGasto(
                    usuarioId,
                    concepto,
                    txtDetalle.Text,
                    monto,
                    metodoPago);

                if (!r.ok)
                {
                    NotificacionWindow.MostrarError(r.mensaje);
                    return;
                }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Gasto registrado!");
            }

            DialogResult = true;
            Close();
        }

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
                if (_modo == "ingreso")
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
    }
}