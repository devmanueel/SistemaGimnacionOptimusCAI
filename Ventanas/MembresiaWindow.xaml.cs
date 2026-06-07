using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class MembresiaWindow : Window
    {
        private readonly MembresiaController _membresiaController = new MembresiaController();
        private readonly UsuarioController _usuarioController = new UsuarioController();

        public bool MembresiaGuardada { get; private set; } = false;

        private long _membresiaId = 0;
        private long _actividadActualId = 0;
        private string _actividadActualCategoria = null;
        private int? _actividadActualNivel = null;
        private bool _modoRenovacion = false;
        private string _estadoActual = null;
        private string _socioNombre = null;
        private DateTime _vencimientoActual = DateTime.Today;
        private long UsuarioId => SesionManager.HaySesion ? SesionManager.UsuarioId : 1;

        public MembresiaWindow()
        {
            InitializeComponent();
            CargarCombos();
        }

        private void CargarCombos()
        {
            try
            {
                var actividades = _membresiaController.ListarActividadesParaCombo();
                cmbActividad.ItemsSource = actividades;

                var instructores = _usuarioController.ObtenerUsuariosActivosPorRol(2);
                var lista = new List<object> { new { NombreCompleto = "Ninguno", Id = (long?)null } };
                lista.AddRange(instructores);
                cmbInstructor.ItemsSource = lista;
                cmbInstructor.SelectedIndex = 0;
            }
            catch { }
        }

        public void Configurar(long membresiaId)
        {
            _membresiaId = membresiaId;
            try
            {
                var m = _membresiaController.ObtenerPorId(membresiaId);
                if (m == null)
                {
                    NotificacionWindow.MostrarError("No se encontró la membresía.");
                    return;
                }

                _actividadActualId = m.ActividadId;
                _actividadActualCategoria = m.ActividadCategoria;
                _actividadActualNivel = m.ActividadNivel;
                _estadoActual = m.Estado;
                _socioNombre = m.SocioNombre;
                _vencimientoActual = m.FechaVencimiento;

                cmbSocio.ItemsSource = new List<object> { new { NombreCompleto = m.SocioNombre, Id = m.SocioId } };
                cmbSocio.SelectedIndex = 0;

                foreach (var item in cmbActividad.Items)
                {
                    var act = item as ActividadComboItem;
                    if (act != null && act.Id == m.ActividadId)
                    { cmbActividad.SelectedItem = item; break; }
                }

                if (m.InstructorId.HasValue)
                {
                    for (int i = 0; i < cmbInstructor.Items.Count; i++)
                    {
                        var inst = cmbInstructor.Items[i] as Usuario;
                        if (inst != null && inst.Id == m.InstructorId.Value)
                        { cmbInstructor.SelectedIndex = i; break; }
                    }
                }
                else
                {
                    cmbInstructor.SelectedIndex = 0;
                }

                dpInicio.SelectedDate = m.FechaInicio;
                dpVencimiento.SelectedDate = m.FechaVencimiento;
                txtMonto.Text = m.MontoPagado.ToString("F0");

                foreach (ComboBoxItem mp in cmbMetodoPago.Items)
                {
                    if (mp.Tag != null && mp.Tag.ToString() == m.MetodoPago)
                    { cmbMetodoPago.SelectedItem = mp; break; }
                }

                txtObservaciones.Text = m.Observaciones ?? string.Empty;
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al cargar la membresía.\n" + ex.Message);
            }
        }

        public void ConfigurarRenovacion(long membresiaId)
        {
            _modoRenovacion = true;
            Configurar(membresiaId);

            Title = "Renovar Membresia";
            lblTituloFormulario.Text = "RENOVAR MEMBRESIA";
            btnGuardar.Content = "RENOVAR";

            dpInicio.SelectedDate = DateTime.Today;
            dpVencimiento.SelectedDate = CalcularVencimientoRenovacion();
            panelUpgrade.Visibility = Visibility.Collapsed;

            var actividad = cmbActividad.SelectedItem as ActividadComboItem;
            if (actividad != null)
                txtMonto.Text = actividad.Precio.ToString("F0");

            ActualizarPreviewCobro();
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
            if (_membresiaId <= 0) return;

            var actividad = cmbActividad.SelectedItem as ActividadComboItem;
            if (actividad == null)
            {
                NotificacionWindow.MostrarAdvertencia("Seleccioná una actividad.");
                return;
            }

            if (!decimal.TryParse(txtMonto.Text.Trim(), out decimal monto) || monto <= 0)
            {
                errMonto.Text = "El monto debe ser mayor a $0.";
                errMonto.Visibility = Visibility.Visible;
                txtMonto.BorderBrush = System.Windows.Media.Brushes.Red;
                txtMonto.BorderThickness = new Thickness(1.5);
                return;
            }

            long? instructorId = null;
            if (cmbInstructor.SelectedIndex > 0)
            {
                var inst = cmbInstructor.SelectedItem as Usuario;
                if (inst != null) instructorId = inst.Id;
            }

            var metodoItem = cmbMetodoPago.SelectedItem as ComboBoxItem;
            string metodoPago = metodoItem?.Tag?.ToString() ?? "efectivo";

            if (_modoRenovacion)
            {
                var confirmar = NotificacionWindow.MostrarConfirmacion(
                    "Vas a renovar la membresia de:\n\n" +
                    (_socioNombre ?? "Socio") + "\n" +
                    "Actividad: " + actividad.Nombre + "\n" +
                    "Monto: $" + monto.ToString("N0") + "\n" +
                    "Vencimiento: " + (dpVencimiento.SelectedDate ?? CalcularVencimientoRenovacion()).ToString("dd/MM/yyyy") + "\n\n" +
                    "Confirmas la renovacion y el cobro?",
                    "Renovar membresia");

                if (!confirmar) return;

                var r = _membresiaController.Renovar(
                    _membresiaId,
                    monto,
                    metodoPago,
                    UsuarioId,
                    31,
                    actividad.Id,
                    instructorId,
                    txtObservaciones.Text.Trim());

                if (!r.ok)
                {
                    NotificacionWindow.MostrarError(r.mensaje);
                    return;
                }

                NotificacionWindow.MostrarExito(r.mensaje, "Renovacion exitosa");
                MembresiaGuardada = true;
                DialogResult = true;
                Close();
                return;
            }

            bool esUpgrade = actividad.Id != _actividadActualId
                && !string.IsNullOrEmpty(_actividadActualCategoria)
                && actividad.Categoria == _actividadActualCategoria
                && _actividadActualNivel.HasValue
                && actividad.Nivel.HasValue
                && actividad.Nivel.Value > _actividadActualNivel.Value;

            if (esUpgrade)
            {
                decimal diferencia = Math.Abs(actividad.Precio - ObtenerPrecio(_actividadActualId));

                bool confirmo = NotificacionWindow.MostrarConfirmacion(
                    "Vas a mejorar el plan de la membresía:\n\n" +
                    "📋 " + ObtenerNombre(_actividadActualId) + " → " + actividad.Nombre + "\n" +
                    "💰 Diferencia a cobrar: $" + diferencia.ToString("N0") + "\n" +
                    "💳 Método: " + metodoPago + "\n\n" +
                    "⚠️ Solo se permite mejorar el plan una vez por membresía.\n\n" +
                    "¿Confirmás el cambio de plan y el cobro?",
                    "Confirmar cambio de plan");

                if (!confirmo) return;

                try
                {
                    var r = _membresiaController.EjecutarUpgrade(
                        _membresiaId, actividad.Id, metodoPago, UsuarioId);

                    if (r == null)
                    {
                        NotificacionWindow.MostrarError("No se pudo cambiar el plan.");
                        return;
                    }

                    NotificacionWindow.MostrarExito(
                        "El plan se actualizó correctamente.\nMonto cobrado: $" + r.MontoCobrado.ToString("N0"),
                        "¡Plan actualizado!");
                }
                catch (Exception ex)
                {
                    NotificacionWindow.MostrarError(ex.Message);
                    return;
                }
            }
            else
            {
                if (actividad.Id != _actividadActualId)
                {
                    if (!string.IsNullOrEmpty(_actividadActualCategoria) &&
                        !string.IsNullOrEmpty(actividad.Categoria) &&
                        _actividadActualCategoria != actividad.Categoria)
                    {
                        NotificacionWindow.MostrarError(
                            "No se puede cambiar a otra categoría. El cambio de plan solo está permitido dentro de la misma categoría.");
                        return;
                    }

                    if (_actividadActualNivel.HasValue && actividad.Nivel.HasValue &&
                        actividad.Nivel.Value <= _actividadActualNivel.Value)
                    {
                        NotificacionWindow.MostrarError(
                            "Solo podés cambiar a un plan superior. No se puede pasar a un plan inferior.");
                        return;
                    }
                }

                decimal? montoParam = monto > 0 ? (decimal?)monto : null;
                DateTime fechaVenc = dpVencimiento.SelectedDate ?? DateTime.Today.AddDays(31);

                var r = _membresiaController.Modificar(
                    _membresiaId, instructorId, fechaVenc,
                    txtObservaciones.Text.Trim(),
                    UsuarioId,
                    actividad.Id, montoParam, "mensual", metodoPago);

                if (!r.ok)
                {
                    NotificacionWindow.MostrarError(r.mensaje);
                    return;
                }

                NotificacionWindow.MostrarExito(r.mensaje, "¡Actualizado!");
            }

            MembresiaGuardada = true;
            DialogResult = true;
            Close();
        }

        private void cmbActividad_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var act = cmbActividad.SelectedItem as ActividadComboItem;
            if (act == null) return;

            if (_modoRenovacion)
            {
                panelUpgrade.Visibility = Visibility.Collapsed;
                txtMonto.Text = act.Precio.ToString("F0");
                dpInicio.SelectedDate = DateTime.Today;
                dpVencimiento.SelectedDate = CalcularVencimientoRenovacion();
                ActualizarPreviewCobro();
                return;
            }

            if (_membresiaId <= 0)
            {
                panelUpgrade.Visibility = Visibility.Collapsed;
                return;
            }

            if (act.Id != _actividadActualId
                && !string.IsNullOrEmpty(_actividadActualCategoria)
                && act.Categoria == _actividadActualCategoria
                && _actividadActualNivel.HasValue
                && act.Nivel.HasValue
                && act.Nivel.Value > _actividadActualNivel.Value)
            {
                decimal precioActual = ObtenerPrecio(_actividadActualId);
                decimal diferencia = Math.Abs(act.Precio - precioActual);

                lblUpgradeDetalle.Text = "Diferencia a cobrar (" +
                    ObtenerNombre(_actividadActualId) + " → " + act.Nombre + "):";
                lblUpgradeMonto.Text = "$" + diferencia.ToString("N0");
                lblUpgradeNivel.Text = "⬆ Nivel " + _actividadActualNivel.Value + " → " + act.Nivel.Value;
                panelUpgrade.Visibility = Visibility.Visible;
                txtMonto.Text = diferencia.ToString("F0");
            }
            else if (act.Id == _actividadActualId)
            {
                panelUpgrade.Visibility = Visibility.Collapsed;
            }
            else
            {
                panelUpgrade.Visibility = Visibility.Collapsed;
            }
        }

        private void txtMonto_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtMonto.Text.Trim(), out decimal monto) || monto <= 0)
            {
                errMonto.Text = "El monto debe ser mayor a $0.";
                errMonto.Visibility = Visibility.Visible;
                txtMonto.BorderBrush = System.Windows.Media.Brushes.Red;
                txtMonto.BorderThickness = new Thickness(1.5);
            }
            else
            {
                errMonto.Visibility = Visibility.Collapsed;
                txtMonto.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
                txtMonto.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
            }

            ActualizarPreviewCobro();
        }

        private void txtMonto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_modoRenovacion)
                ActualizarPreviewCobro();
        }

        private void txtSoloNumerosYComa_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^[\d,\.]$");

        private string ObtenerNombre(long actividadId)
        {
            foreach (var item in cmbActividad.Items)
            {
                var a = item as ActividadComboItem;
                if (a != null && a.Id == actividadId) return a.Nombre;
            }
            return "actividad actual";
        }

        private decimal ObtenerPrecio(long actividadId)
        {
            foreach (var item in cmbActividad.Items)
            {
                var a = item as ActividadComboItem;
                if (a != null && a.Id == actividadId) return a.Precio;
            }
            return 0;
        }

        private DateTime CalcularVencimientoRenovacion()
        {
            DateTime hoy = DateTime.Today;
            if (_estadoActual == "activa" && _vencimientoActual >= hoy)
                return _vencimientoActual.AddDays(31);

            return hoy.AddDays(31);
        }

        private void ActualizarPreviewCobro()
        {
            if (!_modoRenovacion)
            {
                panelPreviewCobro.Visibility = Visibility.Collapsed;
                return;
            }

            var actividad = cmbActividad.SelectedItem as ActividadComboItem;
            if (actividad == null)
            {
                panelPreviewCobro.Visibility = Visibility.Collapsed;
                return;
            }

            decimal monto;
            if (!decimal.TryParse(txtMonto.Text.Trim(), out monto) || monto <= 0)
            {
                lblPreviewMonto.Text = "$0";
            }
            else
            {
                lblPreviewMonto.Text = "$" + monto.ToString("N0");
            }

            lblPreviewActividad.Text = actividad.Nombre;
            panelPreviewCobro.Visibility = Visibility.Visible;
        }
    }
}
