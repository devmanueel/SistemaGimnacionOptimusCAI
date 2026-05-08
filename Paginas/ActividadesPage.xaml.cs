// ============================================================
//  Archivo: ActividadesPage.xaml.cs
//  Módulo Actividades — C# 7.3 compatible
// ============================================================

using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class ActividadesPage : Page
    {
        private readonly ActividadController _controller = new ActividadController();

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private string _filtroEstado = "todos";

        public ActividadesPage()
        {
            InitializeComponent();
            CargarActividades();
            ResaltarChip(chipTodos);
            cmbTipo.SelectedIndex = 0;
        }

        // ─────────────────────────────────────────────────────
        // CARGA + STATS
        // ─────────────────────────────────────────────────────
        private void CargarActividades()
        {
            try
            {
                var lista = _controller.BuscarActividades(txtBuscar.Text, _filtroEstado);
                gridActividades.ItemsSource = lista;
                ActualizarStats();
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar actividades");
            }
        }

        private void ActualizarStats()
        {
            try
            {
                var todos = _controller.ObtenerActividades();
                int total = todos.Count;
                int activos = 0;
                int sociosTotal = 0;

                foreach (var a in todos)
                {
                    if (a.Activo) activos++;
                    sociosTotal += a.CantSocios;
                }

                statTotal.Text = total.ToString();
                statActivos.Text = activos.ToString();
                statSociosTotal.Text = sociosTotal.ToString();
            }
            catch
            {
                statTotal.Text = statActivos.Text = statSociosTotal.Text = "—";
            }
        }

        // ─────────────────────────────────────────────────────
        // BÚSQUEDA / FILTROS
        // ─────────────────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => CargarActividades();

        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            _filtroEstado = btn.Tag.ToString();
            ResaltarChip(btn);
            CargarActividades();
        }

        private void ResaltarChip(Button seleccionado)
        {
            Button[] chips = { chipTodos, chipActivos, chipInactivos };
            foreach (var c in chips)
            {
                if (c == seleccionado)
                {
                    c.Background = new SolidColorBrush(Color.FromRgb(30, 30, 56));
                    c.Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255));
                    c.BorderThickness = new Thickness(0);
                }
                else
                {
                    c.Background = Brushes.Transparent;
                    c.Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154));
                    c.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 37, 64));
                    c.BorderThickness = new Thickness(1);
                }
            }
        }

        // ─────────────────────────────────────────────────────
        // TIPO: cambiar label según selección
        // ─────────────────────────────────────────────────────
        private void cmbTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lblDias == null) return;  // evitar null en init

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

        // ─────────────────────────────────────────────────────
        // BOTONES PRINCIPALES
        // ─────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            _esNuevo = true;
            _idEditar = 0;
            LimpiarFormulario();
            LimpiarErrores();
            AbrirFormulario("NUEVA ACTIVIDAD");
        }

        private void btnEditar_Click(object sender, RoutedEventArgs e)
        {
            var act = ObtenerActividadDeFila(sender);
            if (act == null) return;

            _esNuevo = false;
            _idEditar = act.Id;

            txtNombre.Text = act.Nombre;
            txtDias.Text = act.DiasSesiones.ToString();
            txtPrecio.Text = act.Precio.ToString("F0");

            // Llamamos al método para que el panel aparezca animado
            ActualizarPreviewPrecio();

            // Seleccionar tipo
            foreach (ComboBoxItem item in cmbTipo.Items)
            {
                if (item.Tag.ToString() == act.Tipo)
                { cmbTipo.SelectedItem = item; break; }
            }

            // Marcar checkboxes según DiasSemana JSON
            DesmarcarDias();
            if (!string.IsNullOrEmpty(act.DiasSemana))
            {
                // Parsear "[1,3,5]" → marcar los checks
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

            LimpiarErrores();
            AbrirFormulario("EDITAR ACTIVIDAD");
        }

        private void btnToggleEstado_Click(object sender, RoutedEventArgs e)
        {
            var act = ObtenerActividadDeFila(sender);
            if (act == null) return;

            bool nuevo = !act.Activo;
            string accion = nuevo ? "activar" : "desactivar";

            bool confirmo = NotificacionWindow.MostrarConfirmacion(
                "¿Querés " + accion + " la actividad \"" + act.Nombre + "\"?",
                "Confirmar cambio de estado");

            if (!confirmo) return;

            try
            {
                var r = _controller.CambiarEstado(act.Id, nuevo);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarActividades(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            var act = ObtenerActividadDeFila(sender);
            if (act == null) return;

            bool confirmo = NotificacionWindow.MostrarConfirmacion(
                "¿Querés ELIMINAR DEFINITIVAMENTE la actividad \"" + act.Nombre +
                "\"?\n\nEsta acción no se puede deshacer. Si tiene socios asociados, no se podrá eliminar.",
                "Eliminar actividad");

            if (!confirmo) return;

            try
            {
                var r = _controller.Eliminar(act.Id);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarActividades(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
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

            CerrarFormulario();
            CargarActividades();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) => CerrarFormulario();

        // ─────────────────────────────────────────────────────
        // DÍAS SEMANA → JSON
        // ─────────────────────────────────────────────────────
        /// <summary>Lee los checkboxes y arma "[1,3,5]" para guardar en BD.</summary>
        private string ObtenerDiasSemanaJSON()
        {
            var tipoItem = cmbTipo.SelectedItem as ComboBoxItem;
            if (tipoItem != null && tipoItem.Tag.ToString() == "mensual_con_clases")
                return null;  // Las clases no tienen días fijos

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

        // ─────────────────────────────────────────────────────
        // VALIDACIONES
        // ─────────────────────────────────────────────────────
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
            // Eliminamos cualquier referencia a panelPreviewPrecio.Visibility aquí
            if (decimal.TryParse(txtPrecio.Text, out precio) && precio > 0)
            {
                lblPreviewPrecio.Text = "$" + precio.ToString("N0");
            }
            else
            {
                // Esto es la LLAVE: al poner "$0", el XAML activa la animación de salida
                lblPreviewPrecio.Text = "$0";
            }
        }

        // Solo números en días y precio
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

            // Nombre
            string errN = null;
            if (string.IsNullOrWhiteSpace(txtNombre.Text))
                errN = "El nombre es obligatorio.";
            else if (txtNombre.Text.Trim().Length < 3)
                errN = "Debe tener al menos 3 caracteres.";
            AplicarEstadoCampo(txtNombre, errNombre, errN);
            if (errN != null) ok = false;

            // Tipo
            if (cmbTipo.SelectedItem == null)
            {
                NotificacionWindow.MostrarError("Seleccioná un tipo de actividad.");
                return false;
            }

            // Días
            int dias = 0;
            string errD = null;
            if (string.IsNullOrWhiteSpace(txtDias.Text))
                errD = "La cantidad de días es obligatoria.";
            else if (!int.TryParse(txtDias.Text, out dias) || dias < 1 || dias > 7)
                errD = "Debe ser un número entre 1 y 7.";
            AplicarEstadoCampo(txtDias, errDias, errD);
            if (errD != null) ok = false;

            // Precio
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

        private void LimpiarErrores()
        {
            TextBlock[] labels = { errNombre, errDias, errPrecio };
            TextBox[] campos = { txtNombre, txtDias, txtPrecio };

            foreach (var lbl in labels)
            { lbl.Text = string.Empty; lbl.Visibility = Visibility.Collapsed; }

            foreach (var c in campos)
                c.Style = (Style)Resources["InputEstilo"];
        }

        // ─────────────────────────────────────────────────────
        // ANIMACIONES
        // ─────────────────────────────────────────────────────
        private void AbrirFormulario(string titulo)
        {
            lblTituloFormulario.Text = titulo;
            panelFormulario.Visibility = Visibility.Visible;
            panelFormulario.Opacity = 0;

            var translate = new TranslateTransform { X = 60 };
            panelFormulario.RenderTransform = translate;

            var slide = new DoubleAnimation
            {
                From = 60,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            translate.BeginAnimation(TranslateTransform.XProperty, slide);

            var fade = new DoubleAnimation
            { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(300)) };
            panelFormulario.BeginAnimation(OpacityProperty, fade);
        }

        private void CerrarFormulario()
        {
            var fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (s, ev) =>
            {
                panelFormulario.Visibility = Visibility.Collapsed;
                LimpiarFormulario();
                LimpiarErrores();
            };
            panelFormulario.BeginAnimation(OpacityProperty, fade);
        }

        // ─────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────
        private void LimpiarFormulario()
        {
            txtNombre.Text = string.Empty;
            txtDias.Text = string.Empty;
            txtPrecio.Text = string.Empty;
            cmbTipo.SelectedIndex = 0;
            DesmarcarDias();

            // CORRECCIÓN: No uses Visibility = Collapsed. 
            // Usamos esto para que el trigger de XAML oculte el panel suavemente.
            lblPreviewPrecio.Text = "$0";

            _idEditar = 0;
        }

        private Actividad ObtenerActividadDeFila(object sender)
        {
            var btn = sender as Button;
            if (btn == null) return null;
            return btn.DataContext as Actividad;
        }
    }
}