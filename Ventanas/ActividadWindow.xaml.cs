using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class ActividadWindow : Window
    {
        private readonly ActividadController _controller = new ActividadController();

        public bool ActividadGuardada { get; private set; } = false;

        private bool _esNuevo = true;
        private long _idEditar = 0;

        public ActividadWindow()
        {
            InitializeComponent();
            cmbTipo.SelectedIndex = 0;
        }

        public void ModoNuevo()
        {
            _esNuevo = true;
            _idEditar = 0;
            lblTituloFormulario.Text = "NUEVA ACTIVIDAD";
            LimpiarFormulario();
        }

        public void ModoEditar(Actividad act)
        {
            _esNuevo = false;
            _idEditar = act.Id;

            lblTituloFormulario.Text = "EDITAR ACTIVIDAD";
            txtNombre.Text = act.Nombre;
            txtDias.Text = act.DiasSesiones.ToString();
            txtPrecio.Text = act.Precio.ToString("F0");
            ActualizarPreviewPrecio();

            foreach (ComboBoxItem item in cmbTipo.Items)
            {
                if (item.Tag.ToString() == act.Tipo)
                { cmbTipo.SelectedItem = item; break; }
            }

            DesmarcarDias();
            if (!string.IsNullOrEmpty(act.DiasSemana))
            {
                string limpio = act.DiasSemana.Replace("[", "").Replace("]", "");
                string[] numeros = limpio.Split(',');
                foreach (string n in numeros)
                {
                    string num = n.Trim();
                    if (num == "1") chkLun.IsChecked = true;
                    if (num == "2") chkMar.IsChecked = true;
                    if (num == "3") chkMie.IsChecked = true;
                    if (num == "4") chkJue.IsChecked = true;
                    if (num == "5") chkVie.IsChecked = true;
                    if (num == "6") chkSab.IsChecked = true;
                }
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
            if (!ValidarTodo())
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Revisá los campos marcados en rojo.",
                    "Formulario incompleto");
                return;
            }

            var tipoItem = cmbTipo.SelectedItem as ComboBoxItem;
            string tipo = tipoItem != null ? tipoItem.Tag.ToString() : "mensual";

            int dias = 0;
            int.TryParse(txtDias.Text, out dias);

            decimal precio = 0;
            decimal.TryParse(txtPrecio.Text, out precio);

            string diasSemana = ObtenerDiasSemanaJSON();

            if (_esNuevo)
            {
                var r = _controller.Insertar(
                    txtNombre.Text, tipo, dias, diasSemana, precio);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Actividad creada!");
            }
            else
            {
                var r = _controller.Modificar(
                    _idEditar, txtNombre.Text, tipo, dias, diasSemana, precio);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Actividad actualizada!");
            }

            ActividadGuardada = true;
            DialogResult = true;
            Close();
        }

        private void cmbTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lblDias == null) return;

            var item = cmbTipo.SelectedItem as ComboBoxItem;
            if (item == null) return;

            string tipo = item.Tag.ToString();
            if (tipo == "mensual_con_clases")
            {
                lblDias.Text = "CLASES POR MES *";
                lblSelectorDias.Visibility = Visibility.Collapsed;
                panelDias.Visibility = Visibility.Collapsed;
            }
            else
            {
                lblDias.Text = "DÍAS POR SEMANA *";
                lblSelectorDias.Visibility = Visibility.Visible;
                panelDias.Visibility = Visibility.Visible;
            }
        }

        private string ObtenerDiasSemanaJSON()
        {
            var tipoItem = cmbTipo.SelectedItem as ComboBoxItem;
            if (tipoItem != null && tipoItem.Tag.ToString() == "mensual_con_clases")
                return null;

            var dias = new List<string>();
            if (chkLun.IsChecked == true) dias.Add("1");
            if (chkMar.IsChecked == true) dias.Add("2");
            if (chkMie.IsChecked == true) dias.Add("3");
            if (chkJue.IsChecked == true) dias.Add("4");
            if (chkVie.IsChecked == true) dias.Add("5");
            if (chkSab.IsChecked == true) dias.Add("6");

            if (dias.Count == 0) return null;
            return "[" + string.Join(",", dias) + "]";
        }

        private void DesmarcarDias()
        {
            chkLun.IsChecked = false;
            chkMar.IsChecked = false;
            chkMie.IsChecked = false;
            chkJue.IsChecked = false;
            chkVie.IsChecked = false;
            chkSab.IsChecked = false;
        }

        private void LimpiarFormulario()
        {
            txtNombre.Text = string.Empty;
            txtDias.Text = string.Empty;
            txtPrecio.Text = string.Empty;
            cmbTipo.SelectedIndex = 0;
            DesmarcarDias();
            lblPreviewPrecio.Text = "$0";
            _idEditar = 0;
        }

        private void txtNombre_LostFocus(object sender, RoutedEventArgs e)
        {
            string err = null;
            if (string.IsNullOrWhiteSpace(txtNombre.Text))
                err = "El nombre es obligatorio.";
            else if (txtNombre.Text.Trim().Length < 3)
                err = "Debe tener al menos 3 caracteres.";
            AplicarEstadoCampo(txtNombre, errNombre, err);
        }

        private void txtPrecio_LostFocus(object sender, RoutedEventArgs e)
        {
            decimal precio = 0;
            string err = null;

            if (string.IsNullOrWhiteSpace(txtPrecio.Text))
                err = "El precio es obligatorio.";
            else if (!decimal.TryParse(txtPrecio.Text, out precio))
                err = "El precio no es un número válido.";
            else if (precio <= 0)
                err = "El precio debe ser mayor a $0.";

            AplicarEstadoCampo(txtPrecio, errPrecio, err);
            ActualizarPreviewPrecio();
        }

        private void ActualizarPreviewPrecio()
        {
            decimal precio = 0;
            if (decimal.TryParse(txtPrecio.Text, out precio) && precio > 0)
            {
                lblPreviewPrecio.Text = "$" + precio.ToString("N0");
            }
            else
            {
                lblPreviewPrecio.Text = "$0";
            }
        }

        private void txtDias_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[1-7]$");
        }

        private void txtPrecio_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[\d]$");
        }

        private bool ValidarTodo()
        {
            bool ok = true;

            string errN = null;
            if (string.IsNullOrWhiteSpace(txtNombre.Text))
                errN = "El nombre es obligatorio.";
            else if (txtNombre.Text.Trim().Length < 3)
                errN = "Debe tener al menos 3 caracteres.";
            AplicarEstadoCampo(txtNombre, errNombre, errN);
            if (errN != null) ok = false;

            if (cmbTipo.SelectedItem == null)
            {
                NotificacionWindow.MostrarError("Seleccioná un tipo de actividad.");
                return false;
            }

            int dias = 0;
            string errD = null;
            if (string.IsNullOrWhiteSpace(txtDias.Text))
                errD = "La cantidad de días es obligatoria.";
            else if (!int.TryParse(txtDias.Text, out dias) || dias < 1 || dias > 7)
                errD = "Debe ser un número entre 1 y 7.";
            AplicarEstadoCampo(txtDias, errDias, errD);
            if (errD != null) ok = false;

            decimal precio = 0;
            string errP = null;
            if (string.IsNullOrWhiteSpace(txtPrecio.Text))
                errP = "El precio es obligatorio.";
            else if (!decimal.TryParse(txtPrecio.Text, out precio) || precio <= 0)
                errP = "El precio debe ser un número mayor a 0.";
            AplicarEstadoCampo(txtPrecio, errPrecio, errP);
            if (errP != null) ok = false;

            return ok;
        }

        private void AplicarEstadoCampo(TextBox campo, TextBlock labelError, string mensajeError)
        {
            if (mensajeError != null)
            {
                campo.Style = (Style)Resources["InputErrorEstilo"];
                labelError.Text = mensajeError;
                labelError.Visibility = Visibility.Visible;
            }
            else
            {
                campo.Style = (Style)Resources["InputEstilo"];
                labelError.Text = string.Empty;
                labelError.Visibility = Visibility.Collapsed;
            }
        }
    }
}