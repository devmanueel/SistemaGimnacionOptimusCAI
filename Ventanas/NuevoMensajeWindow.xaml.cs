using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class NuevoMensajeWindow : Window
    {
        private readonly WhatsappController _controller = new WhatsappController();

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
            var socio = cmbSocio.SelectedItem as SocioComboItem;
            if (socio != null) nombreSocio = socio.TextoCombo;

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
    }
}