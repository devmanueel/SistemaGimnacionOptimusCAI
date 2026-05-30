using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Linq;
using System.Windows;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class AsignarRutinaWindow : Window
    {
        private readonly RutinaController _controller = new RutinaController();

        public bool AsignacionGuardada { get; private set; } = false;

        private long _rutinaId = 0;
        private string _nombreRutina = string.Empty;
        private long UsuarioId => SesionManager.HaySesion ? SesionManager.UsuarioId : 1;

        public AsignarRutinaWindow()
        {
            InitializeComponent();
            CargarSocios();
            dtpFechaInicio.SelectedDate = DateTime.Today;
        }

        public void Configurar(long rutinaId, string nombreRutina)
        {
            _rutinaId = rutinaId;
            _nombreRutina = nombreRutina;
            lblNombreRutina.Text = nombreRutina;
        }

        private void CargarSocios()
        {
            try
            {
                var socios = _controller.ListarSociosParaCombo();
                cmbSocios.ItemsSource = socios;
                if (socios.Any()) cmbSocios.SelectedIndex = 0;
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

        private void btnAsignar_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSocios.SelectedItem == null)
            {
                NotificacionWindow.MostrarAdvertencia("Seleccione un socio.");
                return;
            }

            var socio = cmbSocios.SelectedItem as SocioComboItem;
            if (socio == null) return;

            var r = _controller.AsignarRutina(_rutinaId, socio.Id, UsuarioId);
            if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }

            NotificacionWindow.MostrarExito(r.mensaje);
            AsignacionGuardada = true;
            DialogResult = true;
            Close();
        }
    }
}