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
//
//  Compatible con C# 7.3.
// ============================================================

using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
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

            // Foco automático en el DNI al cargar
            Loaded += (s, e) => txtDni.Focus();
        }

        // ─────────────────────────────────────────────────────
        // RELOJ EN VIVO
        // ─────────────────────────────────────────────────────
        private void IniciarReloj()
        {
            ActualizarReloj();

            _timerReloj = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
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
            _timerRefresh = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
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
                statPermitidos.Text = statDenegados.Text = statSociosUnicos.Text = statSemana.Text = "—";
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
        // VALIDAR ACCESO
        // ─────────────────────────────────────────────────────
        private void btnValidar_Click(object sender, RoutedEventArgs e) => ValidarAcceso();

        private void txtDni_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ValidarAcceso();
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
                MostrarResultado(resultado);

                // Refrescar lista y stats
                CargarAccesos();
                ActualizarStats();

                // Limpiar input y volver a poner foco
                txtDni.Text = string.Empty;
                txtDni.Focus();
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al validar.\n" + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────
        // MOSTRAR RESULTADO en la tarjeta
        // ─────────────────────────────────────────────────────
        private void MostrarResultado(ResultadoValidacion r)
        {
            // Cancelar timer anterior si estaba corriendo
            if (_timerOcultarResultado != null) _timerOcultarResultado.Stop();

            panelEsperando.Visibility = Visibility.Collapsed;

            // Datos del socio
            lblSocioNombre.Text = string.IsNullOrEmpty(r.SocioNombre) ? "—" : r.SocioNombre;
            lblNumeroSocio.Text = r.NumeroSocio.HasValue ? r.NumeroSocioFormateado : "";
            lblMensaje.Text = r.Mensaje;

            // Foto
            if (r.Foto != null && r.Foto.Length > 0)
                imgFoto.ImageSource = BytesABitmapImage(r.Foto);
            else
                imgFoto.ImageSource = null;

            // Info de la membresía (solo si hay datos)
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

            // Configurar colores según resultado
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
                lblIcono.Text = "✕";
                lblIcono.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));

                panelResultado.Background = new SolidColorBrush(Color.FromRgb(26, 10, 10));
                panelResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                panelResultado.BorderThickness = new Thickness(2);

                panelDetalles.Background = new SolidColorBrush(Color.FromArgb(60, 255, 85, 85));
                lblMensaje.Foreground = new SolidColorBrush(Color.FromRgb(255, 130, 130));
            }

            // Mostrar con animación de "pulse" (escala 0.8 → 1.0 + fade)
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
                EasingFunction = new BackEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Amplitude = 0.4
                }
            };
            scaleResultado.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scaleResultado.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

            // Auto-ocultar a los 5 segundos
            _timerOcultarResultado = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
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