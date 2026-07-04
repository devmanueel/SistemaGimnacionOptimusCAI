// Paginas/FichaUsuarioWindow.xaml.cs — C# 7.3
using Controllers;
using Entities;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using SistemaGimnacionOptimusCAI.Helpers;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class FichaUsuarioWindow : Window
    {
        private readonly UsuarioController _controller = new UsuarioController();
        private readonly long _usuarioId;
        private Usuario _usuarioActual;

        public FichaUsuarioWindow(long usuarioId)
        {
            InitializeComponent();
            _usuarioId = usuarioId;
            Loaded += FichaUsuarioWindow_Loaded;
        }

        private void FichaUsuarioWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDatos();
        }

        private void CargarDatos()
        {
            try
            {
                Usuario u = _controller.ObtenerPorId(_usuarioId);
                if (u == null)
                {
                    NotificacionWindow.MostrarError("No se encontró el usuario.");
                    Close();
                    return;
                }

                _usuarioActual = u;

                lblNombre.Text = u.NombreCompleto;
                lblRol.Text = u.RolTexto.ToUpper();
                lblDni.Text = u.Dni ?? "—";
                lblEmail.Text = u.Email ?? "—";
                lblTelefono.Text = u.Telefono ?? "—";
                lblDomicilio.Text = u.Domicilio ?? "—";
                lblTarifa.Text = u.TarifaTexto;
                lblId.Text = "#" + u.Id;
                lblEstado.Text = u.EstadoTexto;
                lblEstado.Foreground = u.Activo
                    ? new SolidColorBrush(Color.FromRgb(0, 207, 255))
                    : new SolidColorBrush(Color.FromRgb(255, 85, 85));

                // Foto o iniciales
                if (u.Foto != null && u.Foto.Length > 0)
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = new MemoryStream(u.Foto);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        imgFoto.Source = bmp;
                        lblIniciales.Visibility = Visibility.Collapsed;
                    }
                    catch
                    {
                        lblIniciales.Text = ObtenerIniciales(u.Nombre + " " + u.Apellido);
                    }
                }
                else
                {
                    lblIniciales.Text = ObtenerIniciales(u.Nombre + " " + u.Apellido);
                }

                // Color del badge según rol
                if (u.RolNombre == "admin")
                {
                    badgeRol.Background = new SolidColorBrush(Color.FromRgb(42, 22, 0));
                    lblRol.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 53));
                }
                else
                {
                    badgeRol.Background = new SolidColorBrush(Color.FromRgb(30, 24, 64));
                    lblRol.Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250));
                }

                ActualizarPanelHuella();
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al cargar datos del usuario.\n" + ex.Message);
                Close();
            }
        }

        private void ActualizarPanelHuella()
        {
            if (panelHuella == null) return;

            if (_usuarioActual == null || _usuarioActual.RolId != 2)
            {
                panelHuella.Visibility = Visibility.Collapsed;
                return;
            }

            panelHuella.Visibility = Visibility.Visible;

            var servicio = BiometricManager.Servicio;
            bool lectorDisponible = servicio != null && servicio.Disponible;

            if (!lectorDisponible)
            {
                btnHuella.IsEnabled = false;
                lblBtnHuella.Text = "Huella no disponible";
                lblHuellaEstado.Text = servicio != null
                    ? servicio.MensajeEstado
                    : "Servicio biométrico no inicializado.";
                iconBtnHuella.Foreground = new SolidColorBrush(Color.FromRgb(7, 18, 7));
                return;
            }

            btnHuella.IsEnabled = true;
            bool tieneHuella = _usuarioActual.TieneHuella;
            lblBtnHuella.Text = tieneHuella ? "Actualizar Huella" : "Registrar Huella";
            iconBtnHuella.Foreground = new SolidColorBrush(Color.FromRgb(7, 18, 7));
            lblHuellaEstado.Text = tieneHuella
                ? "Ya tenés una huella registrada. Podés volver a capturarla."
                : "Registrá tu huella para fichar asistencia desde Inicio.";
        }

        private string ObtenerIniciales(string nombreCompleto)
        {
            if (string.IsNullOrWhiteSpace(nombreCompleto)) return "?";
            var partes = nombreCompleto.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 1) return partes[0].Substring(0, 1).ToUpper();
            return (partes[0].Substring(0, 1) + partes[partes.Length - 1].Substring(0, 1)).ToUpper();
        }

        private void btnEditar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new EditarUsuarioWindow(_usuarioId);
                win.Owner = this;
                win.ShowDialog();

                if (win.DatosActualizados)
                {
                    CargarDatos();
                }
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al abrir editor.\n" + ex.Message);
            }
        }

        private void btnHuella_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioActual == null) return;

            var servicio = BiometricManager.Servicio;
            if (servicio == null || !servicio.Disponible)
            {
                NotificacionWindow.MostrarAdvertencia(
                    "El lector de huellas no está disponible en este momento.\n" +
                    (servicio != null ? servicio.MensajeEstado : "Servicio biométrico no inicializado."),
                    "Lector no disponible");
                ActualizarPanelHuella();
                return;
            }

            var win = new EnrolarHuellaWindow(_usuarioActual)
            {
                Owner = this
            };

            if (win.ShowDialog() == true)
            {
                CargarDatos();
                NotificacionWindow.MostrarExito(
                    "Huella registrada. Ya podés fichar asistencia con tu huella.",
                    "Huella digital");
            }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
