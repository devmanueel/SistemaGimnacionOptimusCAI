using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class DashboardPage : Page
    {
        private readonly AsistenciaController _asistenciaCtrl = new AsistenciaController();
        private readonly InstructorAsistenciaController _instructorCtrl = new InstructorAsistenciaController();
        private readonly NotificacionMembresiaController _notificacionCtrl = new NotificacionMembresiaController();
        private readonly SocioController _socioCtrl = new SocioController();
        private readonly UsuarioController _usuarioCtrl = new UsuarioController();

        private DispatcherTimer _timerOcultar;
        private int _versionResultado;

        // ── Reconocimiento de huella ───────────────────────────────
        private CancellationTokenSource _huellaCts;
        private DispatcherTimer _huellaInitTimer;
        private int _huellaInitIntentos;

        public DashboardPage()
        {
            InitializeComponent();
            Loaded += DashboardPage_Loaded;
            Unloaded += DashboardPage_Unloaded;
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDatosUsuario();
            CargarFecha();
            CargarAlertasMembresias();
            IniciarReconocimientoHuella();
            txtDni.Focus();
        }

        private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            DetenerReconocimientoHuella();
        }

        private void CargarDatosUsuario()
        {
            if (!SesionManager.HaySesion) return;

            string nombre = SesionManager.NombreCompleto ?? "";
            string[] partes = nombre.Trim().Split(' ');
            string iniciales = partes.Length >= 2
                ? ("" + partes[0][0] + partes[1][0]).ToUpper()
                : (nombre.Length > 0 ? nombre[0].ToString().ToUpper() : "?");

            lblIniciales.Text = iniciales;

            int hora = DateTime.Now.Hour;
            string saludo = hora < 12 ? "Buenos dias" : hora < 19 ? "Buenas tardes" : "Buenas noches";
            lblSaludo.Text = saludo + ", " + (partes.Length > 0 ? partes[0] : nombre);
        }

        private void CargarFecha()
        {
            CultureInfo cultura = new CultureInfo("es-AR");
            string fecha = DateTime.Today.ToString("dddd, d 'de' MMMM 'de' yyyy", cultura);
            lblFechaHoy.Text = char.ToUpper(fecha[0]) + fecha.Substring(1);
        }

        private void CargarAlertasMembresias()
        {
            panelVencimientos.Children.Clear();

            try
            {
                List<NotificacionMembresia> alertas = _notificacionCtrl.ObtenerMembresiasPorVencer(7);

                if (alertas.Count == 0)
                {
                    panelVencimientos.Children.Add(CrearTextoPanel("No hay membresias por vencer.", 12, true));
                    return;
                }

                foreach (NotificacionMembresia alerta in alertas)
                {
                    panelVencimientos.Children.Add(CrearCardAlerta(alerta));
                }
            }
            catch (Exception ex)
            {
                panelVencimientos.Children.Add(CrearTextoPanel("No se pudieron cargar alertas.", 12, true));
                panelVencimientos.Children.Add(CrearTextoPanel(ex.Message, 10, false));
            }
        }

        private TextBlock CrearTextoPanel(string texto, double fontSize, bool centrado)
        {
            return new TextBlock
            {
                Text = texto,
                FontSize = fontSize,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 204)),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = centrado ? TextAlignment.Center : TextAlignment.Left,
                HorizontalAlignment = centrado ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
                Margin = new Thickness(4, 8, 4, 4)
            };
        }

        private Border CrearCardAlerta(NotificacionMembresia alerta)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 24, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(52, 64, 58)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var stack = new StackPanel();
            var header = new DockPanel { LastChildFill = true };
            var estado = new TextBlock
            {
                Text = alerta.EstadoVencimientoTexto,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = alerta.DiasParaVencer <= 1
                    ? new SolidColorBrush(Color.FromRgb(255, 179, 0))
                    : new SolidColorBrush(Color.FromRgb(212, 131, 10)),
                Margin = new Thickness(0, 0, 0, 2)
            };

            DockPanel.SetDock(estado, Dock.Right);
            header.Children.Add(estado);
            header.Children.Add(new TextBlock
            {
                Text = alerta.SocioNombre,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 250)),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            stack.Children.Add(header);

            stack.Children.Add(new TextBlock
            {
                Text = alerta.NumeroSocioTexto + " - " + alerta.ActividadNombre,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 204)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Vencimiento: " + alerta.FechaVencimientoTexto,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 204)),
                Margin = new Thickness(0, 2, 0, 8)
            });

            var boton = new Button
            {
                Content = "Abrir en WhatsApp",
                Style = TryFindResource("BotonVerdeStyle") as Style,
                Height = 28,
                FontSize = 11,
                Tag = alerta,
                IsEnabled = !string.IsNullOrWhiteSpace(alerta.Telefono),
                ToolTip = string.IsNullOrWhiteSpace(alerta.Telefono)
                    ? "El socio no tiene telefono cargado"
                    : "Abrir WhatsApp Web con mensaje personalizado"
            };
            boton.Click += btnWhatsappAlerta_Click;
            stack.Children.Add(boton);

            card.Child = stack;
            return card;
        }

        private void btnWhatsappAlerta_Click(object sender, RoutedEventArgs e)
        {
            var boton = sender as Button;
            var alerta = boton != null ? boton.Tag as NotificacionMembresia : null;
            if (alerta == null) return;

            try
            {
                string url = _notificacionCtrl.ConstruirUrlWhatsapp(alerta);
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo abrir WhatsApp Web.\n" + ex.Message);
            }
        }

        private void txtDni_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ProcesarAsistencia();
            }
        }

        private void btnValidar_Click(object sender, RoutedEventArgs e)
        {
            ProcesarAsistencia();
        }

        // ─────────────────────────────────────────────────────
        // RECONOCIMIENTO DE HUELLA (automático en Inicio)
        //  · Combina las huellas de socios y docentes.
        //  · Al reconocer una, registra asistencia automáticamente
        //    (socio = acceso con su membresía; docente = fichaje
        //    entrada/salida con espera de 10 minutos).
        // ─────────────────────────────────────────────────────
        private void IniciarReconocimientoHuella()
        {
            DetenerReconocimientoHuella();

            var servicio = BiometricManager.Servicio;

            // El servicio se inicializa en segundo plano al arrancar la app.
            // Si todavía no está listo, reintentamos unas cuantas veces.
            if (servicio == null || !servicio.Disponible)
            {
                if (servicio != null && !servicio.Disponible &&
                    !string.IsNullOrEmpty(servicio.MensajeEstado) &&
                    _huellaInitIntentos >= 8)
                {
                    // Ya esperamos suficiente: el lector no está disponible.
                    ActualizarEstadoHuella(false, servicio.MensajeEstado);
                    return;
                }

                ActualizarEstadoHuella(false, "Iniciando lector de huellas...");
                ProgramarReintentoHuella();
                return;
            }

            // Cargar las huellas de socios + docentes (identificación 1:N)
            var plantillas = new List<(Guid guid, byte[] template)>();
            try { plantillas.AddRange(_socioCtrl.ObtenerHuellas()); } catch { }
            try { plantillas.AddRange(_usuarioCtrl.ObtenerHuellas()); } catch { }

            if (plantillas.Count == 0)
            {
                ActualizarEstadoHuella(true,
                    "Lector listo. Aún no hay huellas registradas — usá el DNI.");
                return;
            }

            ActualizarEstadoHuella(true, "Apoyá tu huella para registrar tu asistencia.");

            _huellaCts = new CancellationTokenSource();
            var token = _huellaCts.Token;
            System.Threading.Tasks.Task.Run(() =>
                servicio.IniciarIdentificacion(plantillas, token, OnHuellaIdentificada));
        }

        private void ProgramarReintentoHuella()
        {
            if (_huellaInitTimer == null)
            {
                _huellaInitTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _huellaInitTimer.Tick += (s, e) =>
                {
                    _huellaInitTimer.Stop();
                    _huellaInitIntentos++;
                    IniciarReconocimientoHuella();
                };
            }
            _huellaInitTimer.Start();
        }

        private void DetenerReconocimientoHuella()
        {
            if (_huellaInitTimer != null) _huellaInitTimer.Stop();

            if (_huellaCts != null)
            {
                try { _huellaCts.Cancel(); } catch { }
                _huellaCts = null;
            }

            try { BiometricManager.Servicio?.Cancelar(); } catch { }
        }

        // Corre en hilo secundario (bucle de identificación del SDK).
        private void OnHuellaIdentificada(Guid? match)
        {
            if (!match.HasValue)
            {
                Dispatcher.Invoke(() =>
                    MostrarError("Huella no reconocida. Probá de nuevo o ingresá tu DNI."));
                return;
            }

            // Resolver el DNI. Un docente tiene precedencia sobre un socio
            // (un docente no puede ser socio).
            string dni = null;
            try { dni = _usuarioCtrl.ObtenerDniPorHuellaGuid(match.Value); } catch { }
            if (string.IsNullOrEmpty(dni))
            {
                try { dni = _socioCtrl.ObtenerDniPorHuellaGuid(match.Value); } catch { }
            }

            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(dni))
                {
                    MostrarError("Huella reconocida, pero la persona no está activa.");
                    return;
                }

                txtDni.Text = dni;
                ProcesarAsistencia("huella");
            });
        }

        private void ActualizarEstadoHuella(bool disponible, string mensaje)
        {
            if (lblHuellaEstado == null) return;

            lblHuellaEstado.Text = mensaje;

            Color color = disponible
                ? Color.FromRgb(0x4A, 0xDE, 0x80)   // verde
                : Color.FromRgb(0xAA, 0xAA, 0xCC);  // gris/muted
            var brush = new SolidColorBrush(color);
            lblHuellaEstado.Foreground = brush;
            if (iconHuellaDash != null) iconHuellaDash.Foreground = brush;
        }

        private void ProcesarAsistencia(string metodo = "dni_pin")
        {
            string dni = txtDni.Text.Trim();
            if (string.IsNullOrEmpty(dni)) return;

            var (tipo, id, nombre, apellido, foto) = _asistenciaCtrl.BuscarPersonaPorDni(dni);

            if (tipo == "no_encontrado" || id == 0)
            {
                MostrarError("No se encontro ninguna persona con DNI " + dni);
                return;
            }

            if (tipo == "socio")
            {
                ProcesarSocio(dni, metodo);
            }
            else if (tipo == "instructor")
            {
                ProcesarInstructor(dni, nombre, apellido, foto);
            }
        }

        private void ProcesarSocio(string dni, string metodo = "dni_pin")
        {
            ResultadoValidacion resultado = _asistenciaCtrl.ValidarAcceso(dni, metodo);
            MostrarResultadoSocio(resultado);
        }

        private void ProcesarInstructor(string dni, string nombre, string apellido, byte[] foto)
        {
            var (ok, mensaje, resultado) = _instructorCtrl.FicharInstructorDashboard(dni);

            if (!ok)
            {
                if (resultado != null && resultado.Operacion == "espera_minima")
                {
                    MostrarResultadoInstructor(resultado);
                    return;
                }
                MostrarError(mensaje);
                return;
            }

            MostrarResultadoInstructor(resultado);
        }

        private void MostrarResultadoSocio(ResultadoValidacion r)
        {
            _versionResultado++;
            panelResultado.BeginAnimation(UIElement.OpacityProperty, null);
            panelResultado.Opacity = 1;
            panelResultado.Visibility = Visibility.Visible;
            lblPlaceholder.Visibility = Visibility.Collapsed;

            imgFotoResultado.ImageSource = BytesAImagen(r.Foto);

            lblResultadoNombre.Text = string.IsNullOrEmpty(r.SocioNombre) ? "—" : r.SocioNombre;
            lblResultadoTipo.Text = "SOCIO";

            if (r.EsPermitido)
            {
                lblResultadoMensaje.Text = r.Mensaje;
                lblResultadoMensaje.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                borderResultado.Background = new SolidColorBrush(Color.FromRgb(10, 26, 16));
                borderResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 230, 118));
            }
            else
            {
                lblResultadoMensaje.Text = r.Mensaje;
                lblResultadoMensaje.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                borderResultado.Background = new SolidColorBrush(Color.FromRgb(26, 10, 10));
                borderResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 85, 85));
            }

            if (!string.IsNullOrEmpty(r.ActividadNombre))
            {
                lblResultadoActividad.Text = r.ActividadNombre;
                lblResultadoVencimiento.Text = r.FechaVencimientoTexto;
                lblResultadoAsistencias.Text = r.AsistenciasRestantesTexto ?? "—";

                if (r.LimitePorSemana.HasValue)
                {
                    int rest = r.AsistenciasRestantesSemana ?? 0;
                    if (rest > 1)
                        lblResultadoAsistencias.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                    else if (rest == 1)
                        lblResultadoAsistencias.Foreground = new SolidColorBrush(Color.FromRgb(255, 179, 0));
                    else
                        lblResultadoAsistencias.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                }
                else
                {
                    lblResultadoAsistencias.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 204));
                }

                panelInfoMembresia.Visibility = Visibility.Visible;
                panelInfoFichaje.Visibility = Visibility.Collapsed;
            }
            else
            {
                panelInfoMembresia.Visibility = Visibility.Collapsed;
                panelInfoFichaje.Visibility = Visibility.Collapsed;
            }

            IniciarTimerOcultar();
        }

        private void MostrarResultadoInstructor(FichajeResultado r)
        {
            _versionResultado++;
            panelResultado.BeginAnimation(UIElement.OpacityProperty, null);
            panelResultado.Opacity = 1;
            panelResultado.Visibility = Visibility.Visible;
            lblPlaceholder.Visibility = Visibility.Collapsed;

            imgFotoResultado.ImageSource = BytesAImagen(r.Foto);

            lblResultadoNombre.Text = string.IsNullOrEmpty(r.NombreCompleto) ? "—" : r.NombreCompleto;
            lblResultadoTipo.Text = "INSTRUCTOR";

            if (r.Operacion == "espera_minima")
            {
                lblResultadoMensaje.Text = r.Mensaje;
                lblResultadoMensaje.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                borderResultado.Background = new SolidColorBrush(Color.FromRgb(26, 10, 10));
                borderResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 85, 85));

                panelInfoMembresia.Visibility = Visibility.Collapsed;
                panelInfoFichaje.Visibility = Visibility.Visible;

                lblResultadoOperacion.Text = "Espera";
                lblResultadoOperacion.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                lblResultadoHora.Text = "—";
                lblResultadoHoras.Text = "—";

                IniciarTimerOcultar();
                return;
            }

            bool esEntrada = r.Operacion == "entrada";

            if (esEntrada)
            {
                lblResultadoMensaje.Text = r.Mensaje;
                lblResultadoMensaje.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                borderResultado.Background = new SolidColorBrush(Color.FromRgb(10, 26, 16));
                borderResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 230, 118));
            }
            else
            {
                lblResultadoMensaje.Text = r.Mensaje;
                lblResultadoMensaje.Foreground = new SolidColorBrush(Color.FromRgb(212, 131, 10));
                borderResultado.Background = new SolidColorBrush(Color.FromRgb(30, 22, 0));
                borderResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(212, 131, 10));
            }

            panelInfoMembresia.Visibility = Visibility.Collapsed;
            panelInfoFichaje.Visibility = Visibility.Visible;

            lblResultadoOperacion.Text = esEntrada ? "Entrada" : "Salida";
            lblResultadoOperacion.Foreground = esEntrada
                ? new SolidColorBrush(Color.FromRgb(0, 230, 118))
                : new SolidColorBrush(Color.FromRgb(212, 131, 10));

            lblResultadoHora.Text = esEntrada ? r.HoraEntradaTexto : r.HoraSalidaTexto;
            lblResultadoHoras.Text = r.HorasTrabajadasTexto;

            IniciarTimerOcultar();
        }

        private void MostrarError(string mensaje)
        {
            _versionResultado++;
            panelResultado.BeginAnimation(UIElement.OpacityProperty, null);
            panelResultado.Opacity = 1;
            panelResultado.Visibility = Visibility.Visible;
            lblPlaceholder.Visibility = Visibility.Collapsed;

            imgFotoResultado.ImageSource = null;
            lblResultadoNombre.Text = "Error";
            lblResultadoTipo.Text = "";
            lblResultadoMensaje.Text = mensaje;
            lblResultadoMensaje.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
            borderResultado.Background = new SolidColorBrush(Color.FromRgb(26, 10, 10));
            borderResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 85, 85));

            panelInfoMembresia.Visibility = Visibility.Collapsed;
            panelInfoFichaje.Visibility = Visibility.Collapsed;

            IniciarTimerOcultar();
        }

        private void IniciarTimerOcultar()
        {
            if (_timerOcultar != null)
            {
                _timerOcultar.Stop();
                _timerOcultar = null;
            }

            _timerOcultar = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timerOcultar.Tick += (s, e) =>
            {
                _timerOcultar.Stop();
                _timerOcultar = null;
                OcultarResultado();
            };
            _timerOcultar.Start();
        }

        private void OcultarResultado()
        {
            int versionActual = _versionResultado;
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300))
            };
            fadeOut.Completed += (s, e) =>
            {
                if (_versionResultado != versionActual) return;
                panelResultado.Visibility = Visibility.Collapsed;
                lblPlaceholder.Visibility = Visibility.Visible;
                panelResultado.Opacity = 1;
                txtDni.Text = string.Empty;
                txtDni.Focus();
            };
            panelResultado.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private System.Windows.Media.ImageSource BytesAImagen(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }

        private void NavegarConSidebar(Type tipo, bool abrirPanel = false)
        {
            if (abrirPanel) SesionManager.AbrirPanelAlNavegar = true;
            var main = Window.GetWindow(this) as MainWindow;
            main?.NavegarA(tipo);
        }

        private void btnRapidoSocio_Click(object sender, RoutedEventArgs e)
            => NavegarConSidebar(typeof(SociosPage), true);

        private void btnRapidoCuota_Click(object sender, RoutedEventArgs e)
            => NavegarConSidebar(typeof(MembresiasPage), true);

        private void btnRapidoAsistencia_Click(object sender, RoutedEventArgs e)
            => NavegarConSidebar(typeof(AsistenciasPage));

        private void btnRapidoVenta_Click(object sender, RoutedEventArgs e)
        {
            SesionManager.AbrirPanelAlNavegar = true;
            NavegarConSidebar(typeof(VentasPage));
        }

        private void btnRapidoTurno_Click(object sender, RoutedEventArgs e)
            => NavegarConSidebar(typeof(TurnosPage), true);

        private void btnRapidoReporte_Click(object sender, RoutedEventArgs e)
            => NavegarConSidebar(typeof(ReportesPage));
    }
}
