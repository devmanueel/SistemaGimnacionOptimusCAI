using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class ResultadoAccesoWindow : Window
    {
        private DispatcherTimer _timerOcultar;

        public ResultadoAccesoWindow()
        {
            InitializeComponent();
        }

        public void MostrarResultado(Entities.ResultadoValidacion r)
        {
            if (r.Foto != null && r.Foto.Length > 0)
                imgFoto.ImageSource = BytesABitmapImage(r.Foto);
            else
                imgFoto.ImageSource = null;

            lblSocioNombre.Text = string.IsNullOrEmpty(r.SocioNombre) ? "—" : r.SocioNombre;
            lblNumeroSocio.Text = r.NumeroSocio.HasValue ? r.NumeroSocioFormateado : "";
            lblMensaje.Text = r.Mensaje;

            if (!string.IsNullOrEmpty(r.ActividadNombre))
            {
                lblActividad.Text = r.ActividadNombre;
                lblVencimiento.Text = r.FechaVencimientoTexto;
                lblAsistenciasRestantes.Text = r.AsistenciasRestantesTexto ?? "—";

                if (r.LimitePorSemana.HasValue)
                {
                    int rest = r.AsistenciasRestantesSemana ?? 0;
                    if (rest > 1)
                        lblAsistenciasRestantes.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                    else if (rest == 1)
                        lblAsistenciasRestantes.Foreground = new SolidColorBrush(Color.FromRgb(255, 179, 0));
                    else
                        lblAsistenciasRestantes.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                }
                else
                {
                    lblAsistenciasRestantes.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 204));
                }

                panelInfoMembresia.Visibility = Visibility.Visible;
            }
            else
            {
                panelInfoMembresia.Visibility = Visibility.Collapsed;
            }

            if (r.EsPermitido && r.DescuentoAplicado)
            {
                lblIcono.Text = "✓";
                lblIcono.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                panelResultado.Background = new SolidColorBrush(Color.FromRgb(10, 26, 16));
                panelResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                panelResultado.BorderThickness = new Thickness(2);
                panelDetalles.Background = new SolidColorBrush(Color.FromArgb(60, 0, 230, 118));
                lblMensaje.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                lblBarra.Background = new SolidColorBrush(Color.FromRgb(0, 230, 118));
            }
            else if (r.EsPermitido && !r.DescuentoAplicado)
            {
                lblIcono.Text = "↩";
                lblIcono.Foreground = new SolidColorBrush(Color.FromRgb(255, 179, 0));
                panelResultado.Background = new SolidColorBrush(Color.FromRgb(26, 20, 5));
                panelResultado.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 179, 0));
                panelResultado.BorderThickness = new Thickness(2);
                panelDetalles.Background = new SolidColorBrush(Color.FromArgb(60, 255, 179, 0));
                lblMensaje.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 80));
                lblBarra.Background = new SolidColorBrush(Color.FromRgb(255, 179, 0));
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
                lblBarra.Background = new SolidColorBrush(Color.FromRgb(255, 85, 85));
            }

            if (r.EsPermitido)
                ReproducirSonido("acceso_ok.wav");
            else
                ReproducirSonido("acceso_error.wav");

            Opacity = 0;
            Show();
            Activate();

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(250))
            };
            BeginAnimation(OpacityProperty, fadeIn);

            _timerOcultar = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timerOcultar.Tick += (s, e) =>
            {
                _timerOcultar.Stop();
                Ocultar();
            };
            _timerOcultar.Start();
        }

        private void Ocultar()
        {
            var fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(250))
            };
            fade.Completed += (s, e) =>
            {
                Close();
            };
            BeginAnimation(OpacityProperty, fade);
        }

        private void ReproducirSonido(string nombreArchivo)
        {
            try
            {
                string ruta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds", nombreArchivo);
                if (!File.Exists(ruta)) return;

                Task.Run(() =>
                {
                    try
                    {
                        using (var player = new SoundPlayer(ruta))
                        {
                            player.PlaySync();
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

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
