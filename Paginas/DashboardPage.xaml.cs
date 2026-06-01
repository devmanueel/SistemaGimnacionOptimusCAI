using Controllers;
using Entities;
using System;
using System.Globalization;
using System.IO;
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

        private DispatcherTimer _timerOcultar;
        private int _versionResultado;

        public DashboardPage()
        {
            InitializeComponent();
            Loaded += DashboardPage_Loaded;
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDatosUsuario();
            CargarFecha();
            txtDni.Focus();
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

        private void ProcesarAsistencia()
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
                ProcesarSocio(dni);
            }
            else if (tipo == "instructor")
            {
                ProcesarInstructor(dni, nombre, apellido, foto);
            }
        }

        private void ProcesarSocio(string dni)
        {
            ResultadoValidacion resultado = _asistenciaCtrl.ValidarAcceso(dni);
            MostrarResultadoSocio(resultado);
        }

        private void ProcesarInstructor(string dni, string nombre, string apellido, byte[] foto)
        {
            var (ok, mensaje, resultado) = _instructorCtrl.FicharInstructorDashboard(dni);

            if (!ok || resultado == null)
            {
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
    }
}
