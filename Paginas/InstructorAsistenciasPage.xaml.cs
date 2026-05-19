// SistemaGimnacionOptimusCAI/Paginas/InstructorAsistenciasPage.xaml.cs — C# 7.3
using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
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

            CargarCombosReporteMensual();
            IniciarReloj();
            ConfigurarPanelAdmin();
            SetTab("historial");
            CargarHistorial();
            ActualizarStats();
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

        // ── PANEL ADMIN ───────────────────────────────────────
        private void ConfigurarPanelAdmin()
        {
            if (SesionManager.EsAdmin)
            {
                borderAdmin.Visibility   = Visibility.Visible;
                borderNoAdmin.Visibility = Visibility.Collapsed;
            }
            else
            {
                borderAdmin.Visibility   = Visibility.Collapsed;
                borderNoAdmin.Visibility = Visibility.Visible;
            }
        }

        private void SetTab(string tab)
        {
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
                    statInstructores.Text = statMes.Text = "—";
            }
        }

        // ── FICHAJE RÁPIDO ────────────────────────────────────
        private void btnFicharEntrada_Click(object sender, RoutedEventArgs e)
        {
            string dni  = txtFichajeDni.Text.Trim();
            string hash = HashPassword(pbFichajePassword.Password);

            var (ok, mensaje, resultado) = _controller.FicharEntrada(dni, hash);

            if (ok)
            {
                MostrarResultadoFichaje(resultado, esSalida: false);
                LimpiarFormFichaje();
                ActualizarStats();
                CargarHistorial();
            }
            else
            {
                NotificacionWindow.MostrarError(mensaje);
            }
        }

        private void btnFicharSalida_Click(object sender, RoutedEventArgs e)
        {
            string dni  = txtFichajeDni.Text.Trim();
            string hash = HashPassword(pbFichajePassword.Password);

            var (ok, mensaje, resultado) = _controller.FicharSalida(dni, hash);

            if (ok)
            {
                MostrarResultadoFichaje(resultado, esSalida: true);
                LimpiarFormFichaje();
                ActualizarStats();
                CargarHistorial();
            }
            else
            {
                NotificacionWindow.MostrarError(mensaje);
            }
        }

        private void MostrarResultadoFichaje(FichajeResultado r, bool esSalida)
        {
            panelResultadoFichaje.Visibility = Visibility.Visible;

            lblResultadoNombre.Text = r.NombreCompleto;

            if (esSalida)
            {
                panelResultadoFichaje.BorderBrush = new SolidColorBrush(Color.FromRgb(212, 131, 10));
                panelResultadoFichaje.Background  = new SolidColorBrush(Color.FromRgb(30, 22, 0));
                lblResultadoDetalle.Foreground    = new SolidColorBrush(Color.FromRgb(212, 131, 10));
                lblResultadoDetalle.Text = $"■  Salida: {r.HoraSalidaTexto}";
                lblResultadoHoras.Text   = $"Total trabajado: {r.HorasTrabajadasTexto}";
                lblResultadoHoras.Visibility = Visibility.Visible;
            }
            else
            {
                panelResultadoFichaje.BorderBrush = new SolidColorBrush(Color.FromRgb(74, 222, 128));
                panelResultadoFichaje.Background  = new SolidColorBrush(Color.FromRgb(10, 26, 10));
                lblResultadoDetalle.Foreground    = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                lblResultadoDetalle.Text = $"▶  Entrada: {r.HoraEntradaTexto}";
                lblResultadoHoras.Visibility = Visibility.Collapsed;
            }
        }

        private void LimpiarFormFichaje()
        {
            txtFichajeDni.Text   = string.Empty;
            pbFichajePassword.Clear();
        }

        // ── HISTORIAL ─────────────────────────────────────────
        private void CargarHistorial()
        {
            try
            {
                var lista = _controller.Buscar(
                    txtBuscar.Text, null,
                    dpDesde.SelectedDate, dpHasta.SelectedDate);
                gridHistorial.ItemsSource = lista;
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message);
            }
        }

        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => CargarHistorial();
        private void dpFecha_Changed(object sender, SelectionChangedEventArgs e)  => CargarHistorial();

        private void gridHistorial_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (panelEditarFichaje.Visibility == Visibility.Visible)
                panelEditarFichaje.Visibility = Visibility.Collapsed;
        }

        // ── EDITAR FICHAJE (TAREA F-03) ───────────────────────
        private void btnEditarFichaje_Click(object sender, RoutedEventArgs e)
        {
            _editando = (sender as Button)?.DataContext as InstructorAsistencia;
            if (_editando == null) return;

            txtEditHoraEntrada.Text  = _editando.HoraEntrada?.ToString(@"hh\:mm") ?? string.Empty;
            txtEditHoraSalida.Text   = _editando.HoraSalida?.ToString(@"hh\:mm")  ?? string.Empty;
            lblEditarInstructor.Text = _editando.InstructorNombre + " — " + _editando.FechaTexto;
            panelEditarFichaje.Visibility = Visibility.Visible;

            gridHistorial.ScrollIntoView(_editando);
        }

        private void btnGuardarEdicion_Click(object sender, RoutedEventArgs e)
        {
            if (_editando == null) return;

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
        private static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString().ToUpper();
            }
        }

        private static TimeSpan? ParseHora(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            TimeSpan ts;
            return TimeSpan.TryParse(texto.Trim(), out ts) ? ts : (TimeSpan?)null;
        }
    }
}
