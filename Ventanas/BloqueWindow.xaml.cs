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
    public partial class BloqueWindow : Window
    {
        private readonly RutinaController _controller = new RutinaController();

        public bool BloqueGuardado { get; private set; } = false;

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private long _rutinaId = 0;
        private int _ordenSugerido = 1;

        public BloqueWindow()
        {
            InitializeComponent();
        }

        public void ModoNuevo(long rutinaId, int ordenSugerido)
        {
            _esNuevo = true;
            _idEditar = 0;
            _rutinaId = rutinaId;
            _ordenSugerido = ordenSugerido;
            lblTituloFormulario.Text = "NUEVO BLOQUE";
            txtBlqNombre.Text = string.Empty;
            txtBlqOrden.Text = ordenSugerido.ToString();
        }

        public void ModoEditar(RutinaBloque bloque, long rutinaId)
        {
            _esNuevo = false;
            _idEditar = bloque.Id;
            _rutinaId = rutinaId;
            lblTituloFormulario.Text = "EDITAR BLOQUE";
            txtBlqNombre.Text = bloque.Nombre;
            txtBlqOrden.Text = bloque.Orden.ToString();
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
            if (string.IsNullOrWhiteSpace(txtBlqNombre.Text) || txtBlqNombre.Text.Trim().Length < 2)
            {
                NotificacionWindow.MostrarAdvertencia("El nombre debe tener al menos 2 caracteres.");
                return;
            }

            byte orden = 1;
            byte.TryParse(txtBlqOrden.Text, out orden);

            if (_esNuevo)
            {
                var r = _controller.InsertarBloque(_rutinaId, txtBlqNombre.Text, orden);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje);
            }
            else
            {
                var r = _controller.ModificarBloque(_idEditar, txtBlqNombre.Text, orden);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje);
            }

            BloqueGuardado = true;
            DialogResult = true;
            Close();
        }

        private void txtSoloNumeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^\d$");
    }
}