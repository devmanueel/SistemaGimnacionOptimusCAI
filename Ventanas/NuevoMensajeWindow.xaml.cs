using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class NuevoMensajeWindow : Window
    {
        private const int TelefonoWhatsappMinLength = 8;
        private const int TelefonoWhatsappMaxLength = 15;

        private readonly WhatsappController _controller = new WhatsappController();
        private List<SocioMasivoItem> _sociosMasivo = new List<SocioMasivoItem>();
        private List<SocioComboItem> _sociosCombo = new List<SocioComboItem>();

        public bool MensajeGuardado { get; private set; } = false;

        public NuevoMensajeWindow()
        {
            InitializeComponent();
            CargarComboSocios();
        }

        private long UsuarioId => SesionManager.HaySesion ? SesionManager.UsuarioId : 1;

        private void CargarComboSocios()
        {
            try
            {
                _sociosCombo = _controller.ListarSociosParaCombo();
                cmbSocio.ItemsSource = _sociosCombo;
            }
            catch { }
        }

        private void CargarSociosParaMasivo()
        {
            try
            {
                _sociosMasivo = _controller.ListarSociosParaMasivo();
                ListViewSociosMasivo.ItemsSource = _sociosMasivo;
            }
            catch { }
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
            if (RbIndividual.IsChecked == true)
            {
                GuardarIndividual();
            }
            else if (RbMasivo.IsChecked == true)
            {
                GuardarMasivo();
            }
        }

        private void GuardarIndividual()
        {
            AutocompletarTelefonoSocioSeleccionado();

            var socio = cmbSocio.SelectedItem as SocioComboItem;
            long? socioId = socio != null ? (long?)socio.Id : null;

            string errorTelefono = ValidarTelefonoWhatsapp(txtTelefono.Text);
            if (errorTelefono != null)
            {
                NotificacionWindow.MostrarError(errorTelefono);
                return;
            }

            var r = _controller.Insertar("masivo", socioId, txtTelefono.Text, txtMensaje.Text, UsuarioId);
            if (!r.ok)
            {
                NotificacionWindow.MostrarError(r.mensaje);
                return;
            }

            NotificacionWindow.MostrarExito(r.mensaje);
            MensajeGuardado = true;
            DialogResult = true;
            Close();
        }

        private void GuardarMasivo()
        {
            var seleccionados = _sociosMasivo.Where(s => s.IsSelected).ToList();

            var r = _controller.InsertarMasivo(seleccionados, txtMensaje.Text, UsuarioId);
            if (!r.ok)
            {
                NotificacionWindow.MostrarError(r.mensaje);
                return;
            }

            NotificacionWindow.MostrarExito(r.mensaje);
            MensajeGuardado = true;
            DialogResult = true;
            Close();
        }

        private void cmbSocio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AutocompletarTelefonoSocioSeleccionado();
        }

        // Filtra el combo en vivo por DNI, nombre o apellido mientras se tipea.
        private void cmbSocio_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab ||
                e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right)
                return;

            var view = CollectionViewSource.GetDefaultView(cmbSocio.ItemsSource);
            if (view == null) return;

            string filtro = (cmbSocio.Text ?? string.Empty).Trim().ToLower();

            if (string.IsNullOrEmpty(filtro))
            {
                view.Filter = null;
            }
            else
            {
                view.Filter = o =>
                {
                    var s = o as SocioComboItem;
                    if (s == null) return false;

                    string nombreCompleto = ((s.Nombre ?? "") + " " + (s.Apellido ?? "")).ToLower();
                    string apellidoNombre = ((s.Apellido ?? "") + " " + (s.Nombre ?? "")).ToLower();

                    return nombreCompleto.Contains(filtro)
                        || apellidoNombre.Contains(filtro)
                        || (s.Dni ?? "").ToLower().Contains(filtro)
                        || s.TextoCombo.ToLower().Contains(filtro);
                };
            }

            cmbSocio.IsDropDownOpen = true;
        }

        // Al cerrar el desplegable limpiamos el filtro para que el proximo abra completo.
        private void cmbSocio_DropDownClosed(object sender, EventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(cmbSocio.ItemsSource);
            if (view != null) view.Filter = null;

            AutocompletarTelefonoSocioSeleccionado();
        }

        private void AutocompletarTelefonoSocioSeleccionado()
        {
            if (RbIndividual == null || RbIndividual.IsChecked != true) return;

            var socio = cmbSocio.SelectedItem as SocioComboItem;
            if (socio == null) return;

            txtTelefono.Text = NormalizarTelefonoWhatsapp(socio.Telefono);
        }

        private void txtTelefono_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!EsTextoTelefonoValido(e.Text))
            {
                e.Handled = true;
                return;
            }

            e.Handled = SuperaLargoMaximo(txtTelefono, e.Text);
        }

        private void txtTelefono_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(typeof(string)))
            {
                e.CancelCommand();
                return;
            }

            string texto = e.DataObject.GetData(typeof(string)) as string;
            texto = NormalizarTelefonoWhatsapp(texto);

            if (string.IsNullOrEmpty(texto) || SuperaLargoMaximo(txtTelefono, texto))
            {
                e.CancelCommand();
                return;
            }

            e.DataObject.SetData(typeof(string), texto);
        }

        private static bool EsTextoTelefonoValido(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return false;

            foreach (char c in texto)
                if (!char.IsDigit(c)) return false;

            return true;
        }

        private static bool SuperaLargoMaximo(TextBox textBox, string textoNuevo)
        {
            int largoActual = textBox.Text != null ? textBox.Text.Length : 0;
            int largoFinal = largoActual - textBox.SelectionLength + (textoNuevo != null ? textoNuevo.Length : 0);
            return largoFinal > TelefonoWhatsappMaxLength;
        }

        private static string NormalizarTelefonoWhatsapp(string telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono)) return string.Empty;

            var caracteres = new List<char>();
            foreach (char c in telefono)
            {
                if (char.IsDigit(c))
                {
                    caracteres.Add(c);
                    if (caracteres.Count == TelefonoWhatsappMaxLength) break;
                }
            }

            return new string(caracteres.ToArray());
        }

        private static string ValidarTelefonoWhatsapp(string telefono)
        {
            telefono = telefono != null ? telefono.Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(telefono))
                return "El telefono es obligatorio.";

            if (!EsTextoTelefonoValido(telefono))
                return "El telefono solo puede contener numeros.";

            if (telefono.Length < TelefonoWhatsappMinLength)
                return "El telefono debe tener al menos 8 digitos.";

            if (telefono.Length > TelefonoWhatsappMaxLength)
                return "El telefono no puede superar los 15 digitos.";

            return null;
        }

        private void btnPlantilla_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            string tag = btn.Tag.ToString();

            string nombreSocio = "";
            if (RbIndividual.IsChecked == true)
            {
                var socio = cmbSocio.SelectedItem as SocioComboItem;
                if (socio != null) nombreSocio = socio.TextoCombo;
            }

            switch (tag)
            {
                case "bienvenida":
                    txtMensaje.Text = _controller.PlantillaBienvenida(nombreSocio);
                    break;
                case "cumple":
                    txtMensaje.Text = _controller.PlantillaCumpleanios(nombreSocio);
                    break;
                case "limpiar":
                    txtMensaje.Text = string.Empty;
                    break;
            }
        }

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (GridIndividual == null || GridMasivo == null) return;

            if (RbIndividual.IsChecked == true)
            {
                GridIndividual.Visibility = Visibility.Visible;
                GridMasivo.Visibility = Visibility.Collapsed;
            }
            else if (RbMasivo.IsChecked == true)
            {
                GridIndividual.Visibility = Visibility.Collapsed;
                GridMasivo.Visibility = Visibility.Visible;

                if (ListViewSociosMasivo.ItemsSource == null || _sociosMasivo.Count == 0)
                {
                    CargarSociosParaMasivo();
                }
            }
        }

        private void BtnMarcarActivos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var socio in _sociosMasivo)
            {
                if (socio.EstadoMembresia == "activa")
                    socio.IsSelected = true;
            }
        }

        private void BtnDesmarcarTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var socio in _sociosMasivo)
                socio.IsSelected = false;
        }

        private void TxtBuscarSocioMasivo_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = TxtBuscarSocioMasivo.Text.ToLower();

            if (string.IsNullOrEmpty(filtro))
            {
                ListViewSociosMasivo.ItemsSource = _sociosMasivo;
            }
            else
            {
                var filtrados = _sociosMasivo.Where(s =>
                    s.NombreCompleto.ToLower().Contains(filtro) ||
                    s.Telefono.Contains(filtro)).ToList();
                ListViewSociosMasivo.ItemsSource = filtrados;
            }
        }

        private void ScrollSociosMasivo_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var sv = sender as ScrollViewer;
            if (sv != null)
            {
                double offset = sv.VerticalOffset - e.Delta;
                offset = Math.Max(0, Math.Min(offset, sv.ScrollableHeight));
                sv.ScrollToVerticalOffset(offset);
                e.Handled = true;
            }
        }
    }
}
