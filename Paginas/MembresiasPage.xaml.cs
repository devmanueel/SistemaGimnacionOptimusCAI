// ============================================================
//  Archivo: MembresiasPage.xaml.cs
//
//  Code-behind del módulo más complejo:
//   · Carga de 3 combos (Socios, Actividades, Instructores)
//   · Autocompletado de precio al elegir actividad
//   · Autocompletado de vencimiento (inicio + 31 días)
//   · Renovación rápida con prompt
//   · Stats incluyendo "recaudado del mes"
//   · 5 chips de filtro (Todos / Activas / Por vencer / Vencidas / Canceladas)
//
//  Compatible con C# 7.3.
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
    public partial class MembresiasPage : Page
    {
        private readonly MembresiaController _controller = new MembresiaController();

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private string _filtroEstado = "todos";
        private DateTime _fechaVencActual = DateTime.Today;
        private Socio _socioPreCargado = null;
        private long? _membresiaIdPreCargada = null;
        private long _actividadActualId = 0;
        private string _actividadActualCategoria = null;
        private int? _actividadActualNivel = null;

        private long USUARIO_ACTUAL_ID => SesionManager.UsuarioId;

        public MembresiasPage() : this(null) { }

        public MembresiasPage(Socio socioPreCargado)
        {
            InitializeComponent();
            _socioPreCargado = socioPreCargado;
            CargarCombos();
            CargarMembresias();
            ResaltarChip(chipTodos);

            // Permisos según rol
            if (!SesionManager.EsAdmin)
            {
                panelStatRecaudado.Visibility = Visibility.Collapsed;
            }

            if (SesionManager.AbrirPanelAlNavegar)
            {
                SesionManager.AbrirPanelAlNavegar = false;
                if (SesionManager.EsAdmin) btnNuevo_Click(null, null);
            }
        }

        public MembresiasPage(long membresiaIdAEditar) : this(null)
        {
            _membresiaIdPreCargada = membresiaIdAEditar;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_membresiaIdPreCargada.HasValue)
            {
                try
                {
                    var m = _controller.ObtenerPorId(_membresiaIdPreCargada.Value);
                    if (m != null)
                    {
                        CargarMembresiaEnPanel(m);
                    }
                }
                catch (Exception ex)
                {
                    NotificacionWindow.MostrarError("No se pudo cargar la membresía.\n" + ex.Message);
                }
                _membresiaIdPreCargada = null;
                return;
            }

            if (_socioPreCargado != null)
            {
                AbrirPanelNuevaMembresia(_socioPreCargado);
                _socioPreCargado = null;
            }
        }

        private void AbrirPanelNuevaMembresia(Socio socio)
        {
            _esNuevo = true;
            _idEditar = 0;

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
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(300))
            };
            panelFormulario.BeginAnimation(OpacityProperty, fade);

            lblTituloFormulario.Text = "COBRAR CUOTA";

            cmbSocio.ItemsSource = _controller.ListarSociosParaCombo();

            bool socioEncontrado = false;
            foreach (var item in cmbSocio.Items)
            {
                var sci = item as SocioComboItem;
                if (sci != null && sci.Id == socio.Id)
                {
                    cmbSocio.SelectedItem = item;
                    socioEncontrado = true;
                    break;
                }
            }

            if (!socioEncontrado)
            {
                var nuevoItem = new SocioComboItem
                {
                    Id = socio.Id,
                    NumeroSocio = socio.NumeroSocio,
                    Nombre = socio.Nombre ?? "Socio",
                    Apellido = socio.Apellido ?? "",
                    Dni = socio.Dni ?? ""
                };
                cmbSocio.Items.Add(nuevoItem);
                cmbSocio.SelectedItem = nuevoItem;
            }

            cmbActividad.SelectedIndex = -1;
            cmbInstructor.SelectedIndex = 0;
            cmbMetodoPago.SelectedIndex = 0;
            txtMonto.Text = string.Empty;
            txtObservaciones.Text = string.Empty;
            panelPreviewMonto.Visibility = Visibility.Collapsed;
            LimpiarErrores();

            dpInicio.SelectedDate = DateTime.Today;
            dpInicio.IsEnabled = false;
            dpVencimiento.SelectedDate = DateTime.Today.AddDays(31);
            dpVencimiento.IsEnabled = false;

            cmbActividad.Focus();
        }

        // ─────────────────────────────────────────────────────
        // CARGA DE COMBOS Y DATOS
        // ─────────────────────────────────────────────────────
        private void CargarCombos()
        {
            try
            {
                cmbSocio.ItemsSource = _controller.ListarSociosParaCombo();
                cmbActividad.ItemsSource = _controller.ListarActividadesParaCombo();

                // Instructor con opción "Sin asignar" al inicio
                var instructores = new List<InstructorComboItem>();
                instructores.Add(new InstructorComboItem
                {
                    Id = 0,
                    Nombre = "Sin",
                    Apellido = "asignar"
                });
                instructores.AddRange(_controller.ListarInstructoresParaCombo());
                cmbInstructor.ItemsSource = instructores;
                cmbInstructor.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al cargar los combos.\n" + ex.Message);
            }
        }

        private void CargarMembresias()
        {
            try
            {
                var lista = _controller.BuscarMembresias(txtBuscar.Text, _filtroEstado);
                gridMembresias.ItemsSource = lista;
                ActualizarStats();
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar membresías");
            }
        }

        public void RefrescarListadoYStats()
        {
            CargarMembresias();
        }

        private void ActualizarStats()
        {
            try
            {
                var todas = _controller.ObtenerMembresias();
                int activas = 0;
                int porVencer = 0;
                int vencidas = 0;
                int canceladas = 0;
                decimal recaudado = 0;
                var primerDiaMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

                foreach (var m in todas)
                {
                    if (m.Estado == "cancelada") canceladas++;
                    else if (m.Estado == "vencida") vencidas++;
                    else if (m.Estado == "activa")
                    {
                        activas++;
                        if (m.EstaPorVencer) porVencer++;
                    }

                    if (m.CreadoEn >= primerDiaMes && m.Estado != "cancelada")
                        recaudado += m.MontoPagado;
                }

                statActivas.Text = activas.ToString();
                statPorVencer.Text = porVencer.ToString();
                statVencidas.Text = vencidas.ToString();
                statRecaudadoMes.Text = "$" + recaudado.ToString("N0");

                chipTodosNum.Text = $"({todas.Count})";
                chipActivasNum.Text = $"({activas})";
                chipPorVencerNum.Text = $"({porVencer})";
                chipVencidasNum.Text = $"({vencidas})";
                chipCanceladasNum.Text = $"({canceladas})";
            }
            catch
            {
                statActivas.Text = statPorVencer.Text = statVencidas.Text = statRecaudadoMes.Text = "-";
            }
        }

        // ─────────────────────────────────────────────────────
        // BÚSQUEDA / FILTROS
        // ─────────────────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => CargarMembresias();

        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            _filtroEstado = btn.Tag.ToString();
            ResaltarChip(btn);
            CargarMembresias();
        }

        private void ResaltarChip(Button seleccionado)
        {
            Button[] chips = { chipTodos, chipActivas, chipPorVencer, chipVencidas, chipCanceladas };

            foreach (var c in chips)
            {
                // Al asignar el estilo completo, recuperás el espaciado y los efectos automáticos
                if (c == seleccionado)
                {
                    c.Style = (Style)FindResource("BotonChipActivoEstilo");
                }
                else
                {
                    c.Style = (Style)FindResource("BotonChipEstilo");
                }
            }
        }

        // ─────────────────────────────────────────────────────
        // AUTOCOMPLETADO al elegir actividad
        // ─────────────────────────────────────────────────────
        private void cmbActividad_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var act = cmbActividad.SelectedItem as ActividadComboItem;
            if (act == null) return;

            if (_esNuevo)
            {
                // Modo nuevo: autocompletar precio normalmente
                txtMonto.Text = act.Precio.ToString("F0");
                ActualizarPreviewMonto();
                panelUpgrade.Visibility = Visibility.Collapsed;
                return;
            }

            // Modo editar: verificar si es upgrade
            if (act.Id != _actividadActualId
                && !string.IsNullOrEmpty(_actividadActualCategoria)
                && act.Categoria == _actividadActualCategoria
                && _actividadActualNivel.HasValue
                && act.Nivel.HasValue
                && act.Nivel.Value > _actividadActualNivel.Value)
            {
                // Es un upgrade — calcular diferencia
                decimal precioActual = 0;
                foreach (var item in cmbActividad.Items)
                {
                    var a = item as ActividadComboItem;
                    if (a != null && a.Id == _actividadActualId)
                    {
                        precioActual = a.Precio;
                        break;
                    }
                }

                decimal diferencia = Math.Abs(act.Precio - precioActual);

                // Mostrar panel upgrade
                lblUpgradeDetalle.Text = "Diferencia a cobrar (" +
                    ObtenerNombreActividad(_actividadActualId) + " → " + act.Nombre + "):";
                lblUpgradeMonto.Text = "$" + diferencia.ToString("N0");
                lblUpgradeNivel.Text = "⬆ Nivel " + _actividadActualNivel.Value +
                                         " → " + act.Nivel.Value;
                panelUpgrade.Visibility = Visibility.Visible;

                // Poner la diferencia en el campo monto
                txtMonto.Text = diferencia.ToString("F0");
                ActualizarPreviewMonto();
            }
            else if (act.Id == _actividadActualId)
            {
                // Volvió a la actividad original — ocultar upgrade
                panelUpgrade.Visibility = Visibility.Collapsed;
                txtMonto.Text = act.Precio.ToString("F0");
                ActualizarPreviewMonto();
            }
            else
            {
                panelUpgrade.Visibility = Visibility.Collapsed;
            }
        }

        // Helper para obtener el nombre de una actividad por id
        private string ObtenerNombreActividad(long actividadId)
        {
            foreach (var item in cmbActividad.Items)
            {
                var a = item as ActividadComboItem;
                if (a != null && a.Id == actividadId) return a.Nombre;
            }
            return "actividad actual";
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

            dpInicio.SelectedDate = DateTime.Today;
            dpVencimiento.SelectedDate = DateTime.Today.AddDays(31);
            cmbMetodoPago.SelectedIndex = 0;
            AbrirFormulario("COBRAR CUOTA");
        }

        private void btnEditar_Click(object sender, RoutedEventArgs e)
        {
            var m = ObtenerMembresiaDeFila(sender);
            if (m == null) return;
            CargarMembresiaEnPanel(m);
        }

        private void CargarMembresiaEnPanel(Membresia m)
        {
            _esNuevo = false;

            LimpiarFormulario();

            _idEditar = m.Id;
            _actividadActualId = m.ActividadId;
            _actividadActualCategoria = m.ActividadCategoria;
            _actividadActualNivel = m.ActividadNivel;

            foreach (var item in cmbSocio.Items)
            {
                var sci = item as SocioComboItem;
                if (sci != null && sci.Id == m.SocioId) { cmbSocio.SelectedItem = item; break; }
            }
            cmbSocio.IsEnabled = false;

            foreach (var item in cmbActividad.Items)
            {
                var aci = item as ActividadComboItem;
                if (aci != null && aci.Id == m.ActividadId) { cmbActividad.SelectedItem = item; break; }
            }

            foreach (var item in cmbInstructor.Items)
            {
                var ici = item as InstructorComboItem;
                if (ici != null)
                {
                    if (m.InstructorId.HasValue && ici.Id == m.InstructorId.Value)
                    { cmbInstructor.SelectedItem = item; break; }
                    if (!m.InstructorId.HasValue && ici.Id == 0)
                    { cmbInstructor.SelectedItem = item; break; }
                }
            }

            dpInicio.SelectedDate = m.FechaInicio;
            dpInicio.IsEnabled = false;
            dpVencimiento.SelectedDate = m.FechaVencimiento;
            dpVencimiento.IsEnabled = false;
            txtMonto.Text = m.MontoPagado.ToString("F0");
            txtMonto.IsEnabled = true;
            ActualizarPreviewMonto();

            foreach (ComboBoxItem mp in cmbMetodoPago.Items)
            {
                if (mp.Tag != null && mp.Tag.ToString() == m.MetodoPago)
                { cmbMetodoPago.SelectedItem = mp; break; }
            }
            cmbMetodoPago.IsEnabled = true;

            txtObservaciones.Text = m.Observaciones ?? string.Empty;

            AbrirFormulario("EDITAR MEMBRESÍA");
        }

        private void btnRenovar_Click(object sender, RoutedEventArgs e)
        {
            var m = ObtenerMembresiaDeFila(sender);
            if (m == null) return;

            // Buscar el precio de la actividad para sugerirlo
            decimal precioSugerido = m.MontoPagado;
            foreach (var item in cmbActividad.Items)
            {
                var a = item as ActividadComboItem;
                if (a != null && a.Id == m.ActividadId)
                { precioSugerido = a.Precio; break; }
            }

            string mensaje = "Vas a renovar la membresía de:\n\n" +
                             "📋 " + m.SocioNombre + "  (" + m.NumeroSocioFormateado + ")\n" +
                             "🏋️ " + m.ActividadNombre + "\n" +
                             "💰 Monto: $" + precioSugerido.ToString("N0") + "\n" +
                             "📅 +31 días al vencimiento actual\n\n" +
                             "¿Confirmás la renovación y el cobro?";

            bool confirmo = NotificacionWindow.MostrarConfirmacion(mensaje, "Renovar membresía");
            if (!confirmo) return;

            try
            {
                var r = _controller.Renovar(m.Id, precioSugerido, m.MetodoPago, USUARIO_ACTUAL_ID, 31);
                if (r.ok)
                {
                    NotificacionWindow.MostrarExito(r.mensaje, "¡Renovación exitosa!");
                    CargarMembresias();
                }
                else NotificacionWindow.MostrarError(r.mensaje);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message);
            }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            var m = ObtenerMembresiaDeFila(sender);
            if (m == null) return;

            if (m.Estado == "cancelada")
            {
                NotificacionWindow.MostrarAdvertencia("Esta membresía ya está cancelada.");
                return;
            }

            bool confirmo = NotificacionWindow.MostrarConfirmacion(
                "¿Querés cancelar la membresía de " + m.SocioNombre +
                " — " + m.ActividadNombre + "?\n\n" +
                "El registro se conserva pero queda en estado 'cancelada'.",
                "Cancelar membresía");

            if (!confirmo) return;

            try
            {
                var r = _controller.Cancelar(m.Id, USUARIO_ACTUAL_ID);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarMembresias(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarTodo()) return;

            var socio = cmbSocio.SelectedItem as SocioComboItem;
            var actividad = cmbActividad.SelectedItem as ActividadComboItem;
            var instructor = cmbInstructor.SelectedItem as InstructorComboItem;
            var metodoItem = cmbMetodoPago.SelectedItem as ComboBoxItem;

            DateTime inicio = DateTime.Today;
            DateTime venc = DateTime.Today.AddDays(31);
            decimal monto = 0;
            decimal.TryParse(txtMonto.Text, out monto);

            string metodoPago = metodoItem != null && metodoItem.Tag != null
                ? metodoItem.Tag.ToString() : "efectivo";

            long? instructorId = null;
            if (instructor != null && instructor.Id > 0)
                instructorId = instructor.Id;

            if (_esNuevo)
            {
                if (socio == null || actividad == null)
                {
                    NotificacionWindow.MostrarAdvertencia("Tenés que elegir socio y actividad.");
                    return;
                }

                var r = _controller.Insertar(
                    socio.Id, actividad.Id, instructorId,
                    inicio, venc, monto, metodoPago,
                    USUARIO_ACTUAL_ID, txtObservaciones.Text, "mensual");

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Cuota cobrada!");
            }
            else
            {
                venc = dpVencimiento.SelectedDate.Value;

                if (venc.Date < _fechaVencActual.Date)
                {
                    NotificacionWindow.MostrarError(
                        "La fecha de vencimiento no puede retroceder. Los días solo pueden aumentar.");
                    return;
                }

                var metodoItemEditar = cmbMetodoPago.SelectedItem as ComboBoxItem;
                string metodoPagoEditar = metodoItemEditar != null && metodoItemEditar.Tag != null
                    ? metodoItemEditar.Tag.ToString() : "efectivo";

                // Verificar si es upgrade
                bool esUpgrade = actividad != null
                    && actividad.Id != _actividadActualId
                    && !string.IsNullOrEmpty(_actividadActualCategoria)
                    && actividad.Categoria == _actividadActualCategoria
                    && _actividadActualNivel.HasValue
                    && actividad.Nivel.HasValue
                    && actividad.Nivel.Value > _actividadActualNivel.Value;

                if (esUpgrade)
                {
                    decimal diferencia = Math.Abs(actividad.Precio - ObtenerPrecioActividad(_actividadActualId));

                    bool confirmo = NotificacionWindow.MostrarConfirmacion(
                        "Vas a mejorar el plan de la membresía:\n\n" +
                        "📋 " + ObtenerNombreActividad(_actividadActualId) + " → " + actividad.Nombre + "\n" +
                        "💰 Diferencia a cobrar: $" + diferencia.ToString("N0") + "\n" +
                        "💳 Método: " + metodoPagoEditar + "\n\n" +
                        "⚠️ Solo se permite mejorar el plan una vez por membresía.\n\n" +
                        "¿Confirmás el cambio de plan y el cobro?",
                        "Confirmar cambio de plan");

                    if (!confirmo) return;

                    try
                    {
                        var r = _controller.EjecutarUpgrade(
                            _idEditar, actividad.Id, metodoPagoEditar, USUARIO_ACTUAL_ID);

                        if (r == null)
                        { NotificacionWindow.MostrarError("No se pudo cambiar el plan."); return; }

                        NotificacionWindow.MostrarExito(
                            "El plan se actualizó correctamente.\nMonto cobrado: $" + r.MontoCobrado.ToString("N0"),
                            "¡Plan actualizado!");
                    }
                    catch (Exception ex)
                    { NotificacionWindow.MostrarError(ex.Message); return; }
                }
                else
                {
                    // Validación de cambio de plan no permitido
                    if (actividad != null && actividad.Id != _actividadActualId)
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

                    // Modificación normal
                    long? actividadEditada = actividad != null ? (long?)actividad.Id : null;
                    decimal montoEditado = 0;
                    decimal.TryParse(txtMonto.Text, out montoEditado);
                    decimal? montoParam = montoEditado > 0 ? (decimal?)montoEditado : null;

                    var r = _controller.Modificar(_idEditar, instructorId, venc,
                        txtObservaciones.Text, USUARIO_ACTUAL_ID,
                        actividadEditada, montoParam, "mensual", metodoPagoEditar);

                    if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                    NotificacionWindow.MostrarExito(r.mensaje, "¡Actualizado!");
                }
            }

            CerrarFormulario();
            CargarMembresias();
        }

        private void btnCancelarFormulario_Click(object sender, RoutedEventArgs e) => CerrarFormulario();

        // ─────────────────────────────────────────────────────
        // MONTO: solo dígitos + preview
        // ─────────────────────────────────────────────────────
        private void txtMonto_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[\d]$");
        }

        private void txtMonto_LostFocus(object sender, RoutedEventArgs e)
        {
            decimal monto = 0;
            string err = null;

            if (string.IsNullOrWhiteSpace(txtMonto.Text))
                err = "El monto es obligatorio.";
            else if (!decimal.TryParse(txtMonto.Text, out monto))
                err = "El monto no es un número válido.";
            else if (monto <= 0)
                err = "El monto debe ser mayor a $0.";

            AplicarEstadoCampo(txtMonto, errMonto, err);
            ActualizarPreviewMonto();
        }

        private void ActualizarPreviewMonto()
        {
            decimal monto = 0;
            if (decimal.TryParse(txtMonto.Text, out monto) && monto > 0)
            {
                lblPreviewMonto.Text = "$" + monto.ToString("N0");
                panelPreviewMonto.Visibility = Visibility.Visible;
            }
            else
            {
                panelPreviewMonto.Visibility = Visibility.Collapsed;
            }
        }

        // ─────────────────────────────────────────────────────
        // VALIDACIÓN
        // ─────────────────────────────────────────────────────
        private bool ValidarTodo()
        {
            if (_esNuevo)
            {
                if (cmbSocio.SelectedItem == null)
                {
                    NotificacionWindow.MostrarAdvertencia("Tenés que elegir un socio.");
                    return false;
                }

                if (cmbActividad.SelectedItem == null)
                {
                    NotificacionWindow.MostrarAdvertencia("Tenés que elegir una actividad.");
                    return false;
                }

                decimal monto = 0;
                string err = null;
                if (string.IsNullOrWhiteSpace(txtMonto.Text))
                    err = "El monto es obligatorio.";
                else if (!decimal.TryParse(txtMonto.Text, out monto) || monto <= 0)
                    err = "El monto debe ser un número mayor a 0.";

                AplicarEstadoCampo(txtMonto, errMonto, err);
                if (err != null)
                {
                    NotificacionWindow.MostrarAdvertencia(err);
                    return false;
                }
            }

            return true;
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
            errMonto.Text = string.Empty;
            errMonto.Visibility = Visibility.Collapsed;
            txtMonto.Style = (Style)Resources["InputEstilo"];
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

            if (_esNuevo)
            {
                dpInicio.SelectedDate = DateTime.Today;
                dpVencimiento.SelectedDate = DateTime.Today.AddDays(31);
                cmbActividad.IsEnabled = true;
            }
            else
            {
                cmbActividad.IsEnabled = true;
            }
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
            cmbSocio.SelectedIndex = -1;
            cmbSocio.IsEnabled = true;
            cmbActividad.SelectedIndex = -1;
            cmbActividad.IsEnabled = true;
            cmbInstructor.SelectedIndex = 0;
            dpInicio.SelectedDate = DateTime.Today;
            dpInicio.IsEnabled = false;
            dpVencimiento.SelectedDate = DateTime.Today.AddDays(31);
            dpVencimiento.IsEnabled = false;
            txtMonto.Text = string.Empty;
            txtMonto.IsEnabled = true;
            cmbMetodoPago.SelectedIndex = 0;
            cmbMetodoPago.IsEnabled = true;
            txtObservaciones.Text = string.Empty;
            panelPreviewMonto.Visibility = Visibility.Collapsed;
            _idEditar = 0;
            // No resetear _actividadActualId, _actividadActualCategoria, _actividadActualNivel aquí
            // porque se asignan después en btnEditar_Click
        }

        private Membresia ObtenerMembresiaDeFila(object sender)
        {
            var btn = sender as Button;
            if (btn == null) return null;
            return btn.DataContext as Membresia;
        }

        // ─────────────────────────────────────────────────────
// HELPERS ACTIVIDAD
// ─────────────────────────────────────────────────────
private decimal ObtenerPrecioActividad(long actividadId)
{
    foreach (var item in cmbActividad.Items)
    {
        var a = item as ActividadComboItem;
        if (a != null && a.Id == actividadId) return a.Precio;
    }
    return 0;
}
    }
}
