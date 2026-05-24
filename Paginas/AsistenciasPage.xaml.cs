// ============================================================
//  Archivo: AsistenciasPage.xaml.cs
//
//  Panel de control de acceso al gimnasio:
//   · Reloj en vivo arriba a la derecha
//   · Input gigante de DNI con foco automático (tipo torniquete)
//   · Validación al presionar ENTER o el botón
//   · Tarjeta de resultado animada (verde/rojo según resultado)
//   · Auto-cierre de la tarjeta a los 5 segundos
//   · Lista de accesos del día con auto-refresh cada 10 segundos
//   · Filtros por resultado (Todos / Permitidos / Denegados)
//   · Selector de actividad cuando el socio tiene más de una membresía
//
//  Compatible con C# 7.3.
// ============================================================

using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class AsistenciasPage : Page
    {
        private readonly AsistenciaController _controller = new AsistenciaController();

        private string _filtroResultado = "todos";
        private string _dniPendiente = null;   // DNI guardado mientras se elige actividad
        private DispatcherTimer _timerReloj;
        private DispatcherTimer _timerRefresh;
        private DispatcherTimer _timerOcultarResultado;

        public AsistenciasPage()
        {
            InitializeComponent();

            ResaltarChip(chipTodos);
            CargarAccesos();
            ActualizarStats();
            IniciarReloj();
            IniciarAutoRefresh();

            Loaded += (s, e) => txtDni.Focus();
        }

        // ─────────────────────────────────────────────────────
        // RELOJ EN VIVO
        // ─────────────────────────────────────────────────────
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

            string[] dias = { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };
            string[] meses = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                               "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

            lblFecha.Text = dias[(int)ahora.DayOfWeek] + ", " +
                            ahora.Day + " de " + meses[ahora.Month - 1] + " " + ahora.Year;
        }

        // ─────────────────────────────────────────────────────
        // AUTO-REFRESH cada 10 segundos
        // ─────────────────────────────────────────────────────
        private void IniciarAutoRefresh()
        {
            _timerRefresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _timerRefresh.Tick += (s, e) => { CargarAccesos(); ActualizarStats(); };
            _timerRefresh.Start();
        }

        // ─────────────────────────────────────────────────────
        // CARGAR ACCESOS DEL DÍA + ESTADÍSTICAS
        // ─────────────────────────────────────────────────────
        private void CargarAccesos()
        {
            try
            {
                var hoy = DateTime.Today;
                var lista = _controller.BuscarAccesos(string.Empty, _filtroResultado, hoy, hoy);
                gridAccesos.ItemsSource = lista;
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar accesos");
            }
        }

        private void ActualizarStats()
        {
            try
            {
                var stats = _controller.ObtenerEstadisticas();
                statPermitidos.Text = stats.PermitidosHoy.ToString();
                statDenegados.Text = stats.DenegadosHoy.ToString();
                statSociosUnicos.Text = stats.SociosUnicosHoy.ToString();
                statSemana.Text = stats.AccesosSemana.ToString();
            }
            catch
            {
                statPermitidos.Text = statDenegados.Text =
                statSociosUnicos.Text = statSemana.Text = "—";
            }
        }

        // ─────────────────────────────────────────────────────
        // FILTROS
        // ─────────────────────────────────────────────────────
        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            _filtroResultado = btn.Tag.ToString();
            ResaltarChip(btn);
            CargarAccesos();
        }

        private void ResaltarChip(Button seleccionado)
        {
            Button[] chips = { chipTodos, chipPermitidos, chipDenegados };
            foreach (var c in chips)
                c.Style = (Style)FindResource(c == seleccionado
                    ? "BotonChipActivoEstilo"
                    : "BotonChipEstilo");
        }

        // ─────────────────────────────────────────────────────
        // VALIDAR ACCESO — flujo principal
        // ─────────────────────────────────────────────────────
        private void btnValidar_Click(object sender, RoutedEventArgs e)
        {
            // Si el selector está visible y hay membresía elegida → validar con membresiaId
            if (panelSelectorActividad.Visibility == Visibility.Visible
                && cmbActividades.SelectedItem is MembresiaOpcion opcion
                && _dniPendiente != null)
            {
                try
                {
                    var resultado = _controller.ValidarAcceso(_dniPendiente, "manual", opcion.MembresiaId);
                    FinalizarValidacion(resultado);
                }
                catch (Exception ex)
                {
                    NotificacionWindow.MostrarError("Error al validar.\n" + ex.Message);
                }
                return;
            }

            ValidarAcceso();
        }

        private void txtDni_KeyDown(object sender, KeyEventArgs e)
        {
            // ENTER solo dispara si el selector NO está visible
            if (e.Key == Key.Enter && panelSelectorActividad.Visibility == Visibility.Collapsed)
                ValidarAcceso();
        }

        private void ValidarAcceso()
        {
            string dni = txtDni.Text.Trim();
            if (string.IsNullOrEmpty(dni))
            {
                NotificacionWindow.MostrarAdvertencia("Ingresá un DNI para validar.");
                txtDni.Focus();
                return;
            }

            try
            {
                var resultado = _controller.ValidarAcceso(dni, "manual");

                // Socio con más de una membresía activa → mostrar selector
                if (resultado.Resultado == "seleccionar_membresia")
                {
                    _dniPendiente = dni;
                    MostrarSelectorActividad(dni);
                    return;
                }

                FinalizarValidacion(resultado);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al validar.\n" + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────
        // SELECTOR DE ACTIVIDAD
        // ─────────────────────────────────────────────────────
        private void MostrarSelectorActividad(string dni)
        {
            try
            {
                var membresias = _controller.ObtenerMembresiasActivasPorDni(dni);
                cmbActividades.ItemsSource = membresias;
                cmbActividades.SelectedIndex = -1;
                panelSelectorActividad.Visibility = Visibility.Visible;
                btnValidar.IsEnabled = false;
                cmbActividades.Focus();
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al cargar actividades.\n" + ex.Message);
            }
        }

        private void cmbActividades_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Habilitar botón solo cuando hay selección
            btnValidar.IsEnabled = cmbActividades.SelectedItem != null;
        }

        // ─────────────────────────────────────────────────────
        // FINALIZAR VALIDACIÓN — limpia estado y muestra resultado
        // ─────────────────────────────────────────────────────
        private void FinalizarValidacion(ResultadoValidacion resultado)
        {
            panelSelectorActividad.Visibility = Visibility.Collapsed;
            cmbActividades.ItemsSource = null;
            btnValidar.IsEnabled = true;
            _dniPendiente = null;

            MostrarResultado(resultado);
            CargarAccesos();
            ActualizarStats();

            txtDni.Text = string.Empty;
            txtDni.Focus();
        }

        // ─────────────────────────────────────────────────────
        // MOSTRAR RESULTADO en la tarjeta
        // ─────────────────────────────────────────────────────
        private void MostrarResultado(ResultadoValidacion r)
        {
            if (_timerOcultarResultado != null) _timerOcultarResultado.Stop();

            panelEsperando.Visibility = Visibility.Collapsed;

            // Datos del socio
            lblSocioNombre.Text = string.IsNullOrEmpty(r.SocioNombre) ? "—" : r.SocioNombre;
            lblNumeroSocio.Text = r.NumeroSocio.HasValue ? r.NumeroSocioFormateado : "";
            lblMensaje.Text = r.Mensaje;

            if (r.Foto != null && r.Foto.Length > 0)
                imgFoto.ImageSource = BytesABitmapImage(r.Foto);
            else
                imgFoto.ImageSource = null;

            if (!string.IsNullOrEmpty(r.ActividadNombre))
            {
                lblActividad.Text = r.ActividadNombre;
                lblVencimiento.Text = r.FechaVencimientoTexto + "  (" + r.DiasParaVencerTexto + ")";
                panelInfoMembresia.Visibility = Visibility.Visible;
            }
            else
            {
                panelInfoMembresia.Visibility = Visibility.Collapsed;
            }

            if (r.EsPermitido && r.DescuentoAplicado)
            {
                // Primera entrada del día — verde
                lblIcono.Text = "✓";
                lblIcono.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                panelResultado.Background = new SolidColorBrush(Color.FromRgb(10, 26, 16));
                panelResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                panelResultado.BorderThickness = new Thickness(2);
                panelDetalles.Background = new SolidColorBrush(Color.FromArgb(60, 0, 230, 118));
                lblMensaje.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
            }
            else if (r.EsPermitido && !r.DescuentoAplicado)
            {
                // Ya registró entrada hoy — ámbar
                lblIcono.Text = "↩";
                lblIcono.Foreground = new SolidColorBrush(Color.FromRgb(255, 179, 0));
                panelResultado.Background = new SolidColorBrush(Color.FromRgb(26, 20, 5));
                panelResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 179, 0));
                panelResultado.BorderThickness = new Thickness(2);
                panelDetalles.Background = new SolidColorBrush(Color.FromArgb(60, 255, 179, 0));
                lblMensaje.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 80));
            }
            else
            {
                // Denegado — rojo
                lblIcono.Text = "✕";
                lblIcono.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                panelResultado.Background = new SolidColorBrush(Color.FromRgb(26, 10, 10));
                panelResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                panelResultado.BorderThickness = new Thickness(2);
                panelDetalles.Background = new SolidColorBrush(Color.FromArgb(60, 255, 85, 85));
                lblMensaje.Foreground = new SolidColorBrush(Color.FromRgb(255, 130, 130));
            }

            panelResultado.Visibility = Visibility.Visible;
            panelResultado.Opacity = 0;
            scaleResultado.ScaleX = 0.8;
            scaleResultado.ScaleY = 0.8;

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(250))
            };
            panelResultado.BeginAnimation(OpacityProperty, fadeIn);

            var scaleAnim = new DoubleAnimation
            {
                From = 0.8,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
            };
            scaleResultado.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scaleResultado.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

            _timerOcultarResultado = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timerOcultarResultado.Tick += (s, e) =>
            {
                _timerOcultarResultado.Stop();
                OcultarResultado();
            };
            _timerOcultarResultado.Start();
        }

        private void OcultarResultado()
        {
            var fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(250))
            };
            fade.Completed += (s, e) =>
            {
                panelResultado.Visibility = Visibility.Collapsed;
                panelEsperando.Visibility = Visibility.Visible;
            };
            panelResultado.BeginAnimation(OpacityProperty, fade);
        }

        // ─────────────────────────────────────────────────────
        // BLOQUEAR LETRAS EN EL DNI
        // ─────────────────────────────────────────────────────
        private void txtDni_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d$");
        }

        private void txtDni_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string texto = (string)e.DataObject.GetData(typeof(string));
                if (!Regex.IsMatch(texto, @"^\d+$")) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        // ─────────────────────────────────────────────────────
        // HELPER
        // ─────────────────────────────────────────────────────
        private static BitmapImage BytesABitmapImage(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                return bmp;
            }
        }
    }
}