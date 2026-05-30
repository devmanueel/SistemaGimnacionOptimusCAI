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
    public partial class EjercicioWindow : Window
    {
        private readonly RutinaController _controller = new RutinaController();

        public bool EjercicioGuardado { get; private set; } = false;

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private long _bloqueId = 0;

        public EjercicioWindow()
        {
            InitializeComponent();
        }

        public void ModoNuevo(long bloqueId)
        {
            _esNuevo = true;
            _idEditar = 0;
            _bloqueId = bloqueId;
            lblTituloFormulario.Text = "NUEVO EJERCICIO";
            txtEjjNombre.Text = string.Empty;
            txtEjjSeries.Text = "3";
            txtEjjRepeticiones.Text = "10";
            txtEjjPeso.Text = "0";
            txtEjjDescanso.Text = "60";
            txtEjjNotas.Text = string.Empty;
        }

        public void ModoEditar(RutinaEjercicio ejercicio, long bloqueId)
        {
            _esNuevo = false;
            _idEditar = ejercicio.Id;
            _bloqueId = bloqueId;
            lblTituloFormulario.Text = "EDITAR EJERCICIO";
            txtEjjNombre.Text = ejercicio.Nombre;
            txtEjjSeries.Text = ejercicio.Series.HasValue ? ejercicio.Series.Value.ToString() : "3";
            txtEjjRepeticiones.Text = ejercicio.Repeticiones ?? "10";
            txtEjjPeso.Text = ejercicio.Peso ?? "0";
            txtEjjDescanso.Text = ejercicio.DescansoSeg.HasValue ? ejercicio.DescansoSeg.Value.ToString() : "60";
            txtEjjNotas.Text = ejercicio.Notas ?? string.Empty;
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
            if (string.IsNullOrWhiteSpace(txtEjjNombre.Text) || txtEjjNombre.Text.Trim().Length < 2)
            {
                NotificacionWindow.MostrarAdvertencia("El nombre debe tener al menos 2 caracteres.");
                return;
            }

            byte series = 3;
            byte.TryParse(txtEjjSeries.Text, out series);

            short? descanso = null;
            short dTmp;
            if (short.TryParse(txtEjjDescanso.Text, out dTmp)) descanso = dTmp;

            var ejercicio = new RutinaEjercicio
            {
                Id = _idEditar,
                BloqueId = _bloqueId,
                Nombre = txtEjjNombre.Text.Trim(),
                Series = series,
                Repeticiones = txtEjjRepeticiones.Text.Trim(),
                Peso = txtEjjPeso.Text.Trim(),
                DescansoSeg = descanso,
                Notas = string.IsNullOrWhiteSpace(txtEjjNotas.Text) ? null : txtEjjNotas.Text.Trim()
            };

            if (_esNuevo)
            {
                var r = _controller.InsertarEjercicio(ejercicio);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje);
            }
            else
            {
                var r = _controller.ModificarEjercicio(ejercicio);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje);
            }

            EjercicioGuardado = true;
            DialogResult = true;
            Close();
        }

        private void txtSoloNumeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^\d$");

        private void txtDecimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^[\d,]$");
    }
}