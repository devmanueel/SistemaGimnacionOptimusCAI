using Controllers;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class GenerarAvisosWindow : Window
    {
        private readonly WhatsappController _controller = new WhatsappController();

        public int MensajesGenerados { get; private set; } = 0;

        public GenerarAvisosWindow()
        {
            InitializeComponent();
        }

        private long UsuarioId => SesionManager.HaySesion ? SesionManager.UsuarioId : 1;

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

        private void btnGenerar_Click(object sender, RoutedEventArgs e)
        {
            int dias = 3;
            int.TryParse(txtDiasAntes.Text, out dias);

            var r = _controller.GenerarAvisosVencimiento(dias, UsuarioId);
            if (!r.ok)
            {
                NotificacionWindow.MostrarError(r.mensaje);
                return;
            }

            MensajesGenerados = r.generados;

            if (r.generados > 0)
                NotificacionWindow.MostrarExito(r.mensaje);
            else
                NotificacionWindow.MostrarAdvertencia(r.mensaje);

            DialogResult = true;
            Close();
        }

        private void txtSoloNumeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^\d$");
    }
}