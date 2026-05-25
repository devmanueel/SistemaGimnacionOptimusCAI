// Paginas/EditarUsuarioWindow.xaml.cs — C# 7.3
using Controllers;
using Entities;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using SistemaGimnacionOptimusCAI.Helpers;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class EditarUsuarioWindow : Window
    {
        private readonly UsuarioController _controller = new UsuarioController();
        private readonly long _usuarioId;
        private byte[] _fotoBytes;

        public bool DatosActualizados { get; private set; }

        public EditarUsuarioWindow(long usuarioId)
        {
            InitializeComponent();
            _usuarioId = usuarioId;
            Loaded += EditarUsuarioWindow_Loaded;
        }

        private void EditarUsuarioWindow_Loaded(object sender, RoutedEventArgs e)
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

                txtNombre.Text = u.Nombre ?? string.Empty;
                txtApellido.Text = u.Apellido ?? string.Empty;
                txtDni.Text = u.Dni ?? string.Empty;
                txtEmail.Text = u.Email ?? string.Empty;
                txtTelefono.Text = u.Telefono ?? string.Empty;
                txtDomicilio.Text = u.Domicilio ?? string.Empty;

                _fotoBytes = u.Foto;

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
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al cargar datos.\n" + ex.Message);
                Close();
            }
        }

        private void btnCambiarFoto_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Seleccionar foto de perfil"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _fotoBytes = File.ReadAllBytes(dlg.FileName);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new MemoryStream(_fotoBytes);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    imgFoto.Source = bmp;
                    lblIniciales.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    NotificacionWindow.MostrarError("No se pudo cargar la imagen.\n" + ex.Message);
                }
            }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            lblError.Visibility = Visibility.Collapsed;
            lblError.Text = string.Empty;

            string nombre = txtNombre.Text.Trim();
            string apellido = txtApellido.Text.Trim();
            string dni = txtDni.Text.Trim();
            string email = txtEmail.Text.Trim();
            string telefono = txtTelefono.Text.Trim();
            string domicilio = txtDomicilio.Text.Trim();
            string claveNueva = txtClaveNueva.Password;
            string claveConfirmar = txtClaveConfirmar.Password;

            if (!string.IsNullOrWhiteSpace(claveNueva) || !string.IsNullOrWhiteSpace(claveConfirmar))
            {
                if (claveNueva != claveConfirmar)
                {
                    MostrarError("Las contraseñas no coinciden.");
                    return;
                }
                if (claveNueva.Length < 4)
                {
                    MostrarError("La contraseña debe tener al menos 4 caracteres.");
                    return;
                }
            }

            byte[] foto = _fotoBytes;
            bool cambiarFoto = foto != null && foto.Length > 0;

            var resultado = _controller.ModificarPropio(
                _usuarioId,
                nombre,
                apellido,
                dni,
                claveNueva,
                domicilio,
                telefono,
                email,
                foto
            );

            if (resultado.ok)
            {
                DatosActualizados = true;
                NotificacionWindow.MostrarExito(resultado.mensaje);
                Close();
            }
            else
            {
                MostrarError(resultado.mensaje);
            }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void txtDni_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private string ObtenerIniciales(string nombreCompleto)
        {
            if (string.IsNullOrWhiteSpace(nombreCompleto)) return "?";
            var partes = nombreCompleto.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 1) return partes[0].Substring(0, 1).ToUpper();
            return (partes[0].Substring(0, 1) + partes[partes.Length - 1].Substring(0, 1)).ToUpper();
        }

        private void MostrarError(string mensaje)
        {
            lblError.Text = mensaje;
            lblError.Visibility = Visibility.Visible;
        }
    }
}
