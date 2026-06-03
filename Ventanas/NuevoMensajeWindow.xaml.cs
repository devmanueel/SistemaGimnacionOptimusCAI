using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class NuevoMensajeWindow : Window
    {
        private readonly WhatsappController _controller = new WhatsappController();
        private List<SocioMasivoItem> _sociosMasivo = new List<SocioMasivoItem>();

        public bool MensajeGuardado { get; private set; } = false;

        public NuevoMensajeWindow()
        {
            InitializeComponent();
            CargarComboSocios();
        }

        private long UsuarioId => SesionManager.HaySesion ? SesionManager.UsuarioId : 1;

        private void CargarComboSocios()
        {
            try { cmbSocio.ItemsSource = _controller.ListarSociosParaCombo(); }
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
            var socio = cmbSocio.SelectedItem as SocioComboItem;
            long? socioId = socio != null ? (long?)socio.Id : null;

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
            var socio = cmbSocio.SelectedItem as SocioComboItem;
            if (socio == null) return;
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