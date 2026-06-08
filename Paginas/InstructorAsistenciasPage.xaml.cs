// SistemaGimnacionOptimusCAI/Paginas/InstructorAsistenciasPage.xaml.cs — C# 7.3
using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class InstructorAsistenciasPage : Page
    {
        private readonly InstructorAsistenciaController _controller = new InstructorAsistenciaController();

        private DispatcherTimer _timerReloj;
        private InstructorAsistencia _editando;

        public InstructorAsistenciasPage()
        {
            InitializeComponent();

            dpDesde.SelectedDate        = DateTime.Today.AddDays(-7);
            dpHasta.SelectedDate        = DateTime.Today;
            dpSemanalDesde.SelectedDate = DateTime.Today.AddDays(-6);
            dpSemanalHasta.SelectedDate = DateTime.Today;

            ConfigurarPermisosPorRol();
            CargarCombosReporteMensual();
            IniciarReloj();
            SetTab("historial");
            CargarHistorial();
            ActualizarStats();
        }

        private void ConfigurarPermisosPorRol()
        {
            if (SesionManager.EsAdmin) return;

            panelStatsInstructores.Visibility = Visibility.Collapsed;
            panelTabsInstructores.Visibility = Visibility.Collapsed;
            btnTabSemanal.Visibility = Visibility.Collapsed;
            btnTabMensual.Visibility = Visibility.Collapsed;
            panelFiltrosHistorial.Visibility = Visibility.Collapsed;
            colAccionesHistorial.Visibility = Visibility.Collapsed;
            panelEditarFichaje.Visibility = Visibility.Collapsed;

            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today;
            txtBuscar.Text = string.Empty;
        }

        // ── RELOJ ─────────────────────────────────────────────
        private void IniciarReloj()
        {
            ActualizarReloj();
            _timerReloj = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timerReloj.Tick += (s, e) => ActualizarReloj();
            _timerReloj.Start();
        }

        private void ActualizarReloj()
        {
            DateTime ahora = DateTime.Now;
            lblReloj.Text = ahora.ToString("HH:mm:ss");
            string[] dias  = { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };
            string[] meses = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                               "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            lblFecha.Text = dias[(int)ahora.DayOfWeek] + ", " +
                            ahora.Day + " de " + meses[ahora.Month - 1] + " " + ahora.Year;
        }

        private void SetTab(string tab)
        {
            if (!SesionManager.EsAdmin && (tab == "semanal" || tab == "mensual"))
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Solo administradores pueden ver reportes semanales y mensuales.",
                    "Acceso denegado");
                return;
            }

            btnTabHistorial.IsEnabled = (tab != "historial");
            btnTabSemanal.IsEnabled   = (tab != "semanal");
            btnTabMensual.IsEnabled   = (tab != "mensual");

            panelHistorial.Visibility = tab == "historial" ? Visibility.Visible : Visibility.Collapsed;
            panelSemanal.Visibility   = tab == "semanal"   ? Visibility.Visible : Visibility.Collapsed;
            panelMensual.Visibility   = tab == "mensual"   ? Visibility.Visible : Visibility.Collapsed;

            panelEditarFichaje.Visibility = Visibility.Collapsed;
        }

        private void btnTabHistorial_Click(object sender, RoutedEventArgs e) { SetTab("historial"); CargarHistorial(); }
        private void btnTabSemanal_Click(object sender, RoutedEventArgs e)   { SetTab("semanal"); }
        private void btnTabMensual_Click(object sender, RoutedEventArgs e)   { SetTab("mensual"); }

        // ── STATS ─────────────────────────────────────────────
        private void ActualizarStats()
        {
            if (!SesionManager.EsAdmin)
            {
                statHoy.Text = statAbiertas.Text =
                    statInstructores.Text = statMes.Text = "-";
                return;
            }

            try
            {
                var s = _controller.ObtenerEstadisticas();
                statHoy.Text          = s.AsistenciasHoy.ToString();
                statAbiertas.Text     = s.AbiertasHoy.ToString();
                statInstructores.Text = s.InstructoresHoy.ToString();
                statMes.Text          = s.AsistenciasMes.ToString();
            }
            catch
            {
                statHoy.Text = statAbiertas.Text =
                    statInstructores.Text = statMes.Text = "-";
            }
        }

        // ── HISTORIAL ─────────────────────────────────────────
        private void CargarHistorial()
        {
            try
            {
                List<InstructorAsistencia> lista;
                if (SesionManager.EsAdmin)
                {
                    lista = _controller.Buscar(
                        txtBuscar.Text, null,
                        dpDesde.SelectedDate, dpHasta.SelectedDate);
                }
                else
                {
                    lista = _controller.BuscarPropias(
                        DateTime.Today, DateTime.Today);
                }
                gridHistorial.ItemsSource = lista;
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message);
            }
        }

        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => CargarHistorial();
        private void dpFecha_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!SesionManager.EsAdmin) return;
            CargarHistorial();
        }

        private void gridHistorial_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelEditarFichaje.Visibility == Visibility.Visible)
                panelEditarFichaje.Visibility = Visibility.Collapsed;
        }

        // ── EDITAR FICHAJE ────────────────────────────────────
        private void btnEditarFichaje_Click(object sender, RoutedEventArgs e)
        {
            _editando = (sender as Button)?.DataContext as InstructorAsistencia;
            if (_editando == null) return;

            if (!SesionManager.EsAdmin)
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Solo administradores pueden editar fichajes.",
                    "Acceso denegado");
                return;
            }

            txtEditHoraEntrada.Text  = _editando.HoraEntrada?.ToString(@"hh\:mm") ?? string.Empty;
            txtEditHoraSalida.Text   = _editando.HoraSalida?.ToString(@"hh\:mm")  ?? string.Empty;
            lblEditarInstructor.Text = _editando.InstructorNombre + " — " + _editando.FechaTexto;
            panelEditarFichaje.Visibility = Visibility.Visible;

            gridHistorial.ScrollIntoView(_editando);
        }

        private void btnGuardarEdicion_Click(object sender, RoutedEventArgs e)
        {
            if (_editando == null) return;

            if (!SesionManager.EsAdmin)
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Solo administradores pueden editar fichajes.",
                    "Acceso denegado");
                panelEditarFichaje.Visibility = Visibility.Collapsed;
                return;
            }

            TimeSpan? entrada = ParseHora(txtEditHoraEntrada.Text);
            TimeSpan? salida  = ParseHora(txtEditHoraSalida.Text);

            if (!string.IsNullOrWhiteSpace(txtEditHoraEntrada.Text) && entrada == null)
            {
                NotificacionWindow.MostrarError("Hora de entrada inválida. Formato: HH:MM");
                return;
            }
            if (!string.IsNullOrWhiteSpace(txtEditHoraSalida.Text) && salida == null)
            {
                NotificacionWindow.MostrarError("Hora de salida inválida. Formato: HH:MM");
                return;
            }

            var r = _controller.Actualizar(_editando.Id, _editando.TurnoId, entrada, salida,
                _editando.Observaciones);

            if (r.ok)
            {
                NotificacionWindow.MostrarExito(r.mensaje);
                panelEditarFichaje.Visibility = Visibility.Collapsed;
                _editando = null;
                CargarHistorial();
                ActualizarStats();
            }
            else
            {
                NotificacionWindow.MostrarError(r.mensaje);
            }
        }

        private void btnCancelarEdicion_Click(object sender, RoutedEventArgs e)
        {
            panelEditarFichaje.Visibility = Visibility.Collapsed;
            _editando = null;
        }

        // ── ELIMINAR ──────────────────────────────────────────
        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            var asist = (sender as Button)?.DataContext as InstructorAsistencia;
            if (asist == null) return;

            if (!SesionManager.EsAdmin)
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Solo administradores pueden eliminar fichajes.",
                    "Acceso denegado");
                return;
            }

            bool ok = NotificacionWindow.MostrarConfirmacion(
                "¿Eliminar asistencia de " + asist.InstructorNombre + " del " + asist.FechaTexto + "?",
                "Eliminar asistencia");
            if (!ok) return;

            try
            {
                var r = _controller.Eliminar(asist.Id);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarHistorial(); ActualizarStats(); }
                else      { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        // ── REPORTE SEMANAL ───────────────────────────────────
        private void btnCargarSemanal_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionManager.EsAdmin)
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Solo administradores pueden ver reportes semanales.",
                    "Acceso denegado");
                return;
            }

            if (dpSemanalDesde.SelectedDate == null || dpSemanalHasta.SelectedDate == null)
            {
                NotificacionWindow.MostrarError("Seleccioná el rango de fechas.");
                return;
            }
            try
            {
                var lista = _controller.ObtenerReporteSemanal(
                    dpSemanalDesde.SelectedDate.Value,
                    dpSemanalHasta.SelectedDate.Value);
                gridSemanal.ItemsSource = lista;
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        // ── REPORTE MENSUAL ───────────────────────────────────
        private void CargarCombosReporteMensual()
        {
            string[] nombres = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                                 "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            for (int i = 0; i < 12; i++)
                cmbMes.Items.Add(new ComboBoxItem { Content = nombres[i], Tag = i + 1 });
            cmbMes.SelectedIndex = DateTime.Today.Month - 1;

            int anioActual = DateTime.Today.Year;
            for (int a = anioActual - 2; a <= anioActual + 1; a++)
                cmbAnio.Items.Add(new ComboBoxItem { Content = a.ToString(), Tag = a });
            cmbAnio.SelectedIndex = 2;
        }

        private void btnCargarMensual_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionManager.EsAdmin)
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Solo administradores pueden ver reportes mensuales.",
                    "Acceso denegado");
                return;
            }

            if (cmbMes.SelectedItem == null || cmbAnio.SelectedItem == null)
            {
                NotificacionWindow.MostrarError("Seleccioná mes y año.");
                return;
            }

            int mes  = (int)((ComboBoxItem)cmbMes.SelectedItem).Tag;
            int anio = (int)((ComboBoxItem)cmbAnio.SelectedItem).Tag;

            try
            {
                var lista = _controller.ObtenerReporteMensual(anio, mes);
                gridMensual.ItemsSource = lista;
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        // ── HELPERS ───────────────────────────────────────────
        private static TimeSpan? ParseHora(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            TimeSpan ts;
            return TimeSpan.TryParse(texto.Trim(), out ts) ? ts : (TimeSpan?)null;
        }
    }
}
