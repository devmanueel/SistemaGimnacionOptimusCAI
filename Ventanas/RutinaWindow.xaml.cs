using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class RutinaWindow : Window
    {
        private readonly RutinaController _controller = new RutinaController();

        public bool RutinaGuardada { get; private set; } = false;
        public long NuevoId { get; private set; } = 0;

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private long UsuarioId => SesionManager.HaySesion ? SesionManager.UsuarioId : 1;

        public RutinaWindow()
        {
            InitializeComponent();
        }

        public void ModoNuevo()
        {
            _esNuevo = true;
            _idEditar = 0;
            lblTituloFormulario.Text = "NUEVA RUTINA";
            txtRutNombre.Text = string.Empty;
            txtRutDetalles.Text = string.Empty;
            txtRutSemanas.Text = "4";
        }

        public void ModoEditar(Rutina rutina)
        {
            _esNuevo = false;
            _idEditar = rutina.Id;
            lblTituloFormulario.Text = "EDITAR RUTINA";
            txtRutNombre.Text = rutina.Nombre;
            txtRutDetalles.Text = rutina.Detalles ?? string.Empty;
            txtRutSemanas.Text = rutina.DuracionSemanas.ToString();
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
            if (string.IsNullOrWhiteSpace(txtRutNombre.Text) || txtRutNombre.Text.Trim().Length < 2)
            {
                NotificacionWindow.MostrarAdvertencia("El nombre debe tener al menos 2 caracteres.");
                return;
            }

            byte sem = 4;
            byte.TryParse(txtRutSemanas.Text, out sem);

            if (_esNuevo)
            {
                var r = _controller.InsertarRutina(txtRutNombre.Text, txtRutDetalles.Text, sem, UsuarioId);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NuevoId = r.nuevoId;
                NotificacionWindow.MostrarExito(r.mensaje);
            }
            else
            {
                var r = _controller.ModificarRutina(_idEditar, txtRutNombre.Text, txtRutDetalles.Text, sem);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje);
            }

            RutinaGuardada = true;
            DialogResult = true;
            Close();
        }

        private void txtSoloNumeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^\d$");
    }
}