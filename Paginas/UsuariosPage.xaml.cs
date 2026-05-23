// ============================================================
//  Archivo: UsuariosPage.xaml.cs
//  Versión: dark theme con animaciones y stats
// ============================================================

using Controllers;
using Entities;
using Microsoft.Win32;
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

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class UsuariosPage : Page
    {
        private readonly UsuarioController _controller = new UsuarioController();

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private byte[] _fotoBytes = null;

        public UsuariosPage()
        {
            InitializeComponent();
            CargarUsuarios();
        }

        // ─────────────────────────────────────────────────────────
        // CARGA Y STATS
        // ─────────────────────────────────────────────────────────
        private void CargarUsuarios()
        {
            try
            {
                var lista = _controller.ObtenerUsuarios();
                gridUsuarios.ItemsSource = lista;
                ActualizarStats(lista);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar usuarios");
            }
        }

        /// <summary>Actualiza los 4 contadores de la barra de stats.</summary>
        private void ActualizarStats(List<Usuario> lista)
        {
            int total = lista.Count;
            int activos = 0;
            int admins = 0;
            int instructores = 0;

            foreach (var u in lista)
            {
                if (u.Activo) activos++;
                if (u.RolNombre == "admin") admins++;
                if (u.RolNombre == "empleado") instructores++;
            }

            statTotal.Text = total.ToString();
            statActivos.Text = activos.ToString();
            statAdmins.Text = admins.ToString();
            statInstructores.Text = instructores.ToString();
        }

        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                gridUsuarios.ItemsSource = _controller.BuscarUsuarios(txtBuscar.Text);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message);
            }
        }

        private void gridUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        // ─────────────────────────────────────────────────────────
        // NUEVO
        // ─────────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            _esNuevo = true;
            _idEditar = 0;
            LimpiarFormulario();
            LimpiarErrores();
            AbrirFormulario("NUEVO USUARIO");
            lblClave.Text = "CONTRASEÑA *";
            lblClaveAclaracion.Visibility = Visibility.Collapsed;
        }

        // ─────────────────────────────────────────────────────────
        // EDITAR
        // ─────────────────────────────────────────────────────────
        private void btnEditar_Click(object sender, RoutedEventArgs e)
        {
            var usuario = ObtenerUsuarioDeFila(sender);
            if (usuario == null) return;

            _esNuevo = false;
            _idEditar = usuario.Id;

            txtNombre.Text = usuario.Nombre;
            txtApellido.Text = usuario.Apellido;
            txtDni.Text = usuario.Dni;
            txtEmail.Text = usuario.Email ?? string.Empty;
            txtTelefono.Text = usuario.Telefono ?? string.Empty;
            txtDomicilio.Text = usuario.Domicilio ?? string.Empty;
            txtTarifaHora.Text = usuario.TarifaHora > 0 ? usuario.TarifaHora.ToString("F0") : "0";
            txtClave.Password = string.Empty;
            _fotoBytes = null;

            foreach (ComboBoxItem item in cmbRol.Items)
            {
                if (Convert.ToInt32(item.Tag) == usuario.RolId)
                {
                    cmbRol.SelectedItem = item;
                    break;
                }
            }

            if (usuario.Foto != null && usuario.Foto.Length > 0)
                imgFotoFormulario.ImageSource = BytesABitmapImage(usuario.Foto);
            else
                imgFotoFormulario.ImageSource = null;

            lblClave.Text = "NUEVA CONTRASEÑA  (opcional)";
            lblClaveAclaracion.Visibility = Visibility.Visible;

            LimpiarErrores();
            AbrirFormulario("EDITAR USUARIO");
        }

        // ─────────────────────────────────────────────────────────
        // TOGGLE ESTADO
        // ─────────────────────────────────────────────────────────
        private void btnToggleEstado_Click(object sender, RoutedEventArgs e)
        {
            var usuario = ObtenerUsuarioDeFila(sender);
            if (usuario == null) return;

            bool nuevoEstado = !usuario.Activo;
            string accion = nuevoEstado ? "activar" : "desactivar";

            bool confirmo = NotificacionWindow.MostrarConfirmacion(
                "¿Querés " + accion + " al usuario " + usuario.NombreCompleto + "?",
                "Confirmar cambio de estado");

            if (!confirmo) return;

            try
            {
                var resultado = _controller.CambiarEstado(usuario.Id, nuevoEstado);
                if (resultado.ok)
                {
                    NotificacionWindow.MostrarExito(resultado.mensaje);
                    CargarUsuarios();
                }
                else
                    NotificacionWindow.MostrarError(resultado.mensaje);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────
        // CAMBIAR CONTRASEÑA
        // ─────────────────────────────────────────────────────────
        private void btnCambiarClave_Click(object sender, RoutedEventArgs e)
        {
            var usuario = ObtenerUsuarioDeFila(sender);
            if (usuario == null) return;

            string nuevaClave = MostrarDialogoClave(usuario.NombreCompleto);
            if (string.IsNullOrWhiteSpace(nuevaClave)) return;

            var resultado = _controller.CambiarPassword(usuario.Id, nuevaClave);
            if (resultado.ok)
                NotificacionWindow.MostrarExito(resultado.mensaje);
            else
                NotificacionWindow.MostrarError(resultado.mensaje);
        }

        private string MostrarDialogoClave(string nombreUsuario)
        {
            var win = new Window
            {
                Title = "Cambiar contraseña",
                Width = 380,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = new SolidColorBrush(Color.FromRgb(16, 22, 16))
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            var lbl = new TextBlock
            {
                Text = "Nueva contraseña para " + nombreUsuario + ":",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 200)),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var pwd = new PasswordBox
            {
                FontSize = 14,
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(24, 32, 24)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 80, 60)),
                Padding = new Thickness(8, 6, 8, 6)
            };

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            string resultado = null;

            var btnAceptar = new Button
            {
                Content = "GUARDAR",
                Width = 100,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(122, 201, 67)),
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            btnAceptar.Click += (s, ev) => { resultado = pwd.Password; win.Close(); };

            var btnCancelar = new Button
            {
                Content = "Cancelar",
                Width = 80,
                Height = 34,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 160)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 80, 60)),
                Cursor = Cursors.Hand
            };
            btnCancelar.Click += (s, ev) => win.Close();

            btnPanel.Children.Add(btnAceptar);
            btnPanel.Children.Add(btnCancelar);

            panel.Children.Add(lbl);
            panel.Children.Add(pwd);
            panel.Children.Add(btnPanel);
            win.Content = panel;

            win.ShowDialog();
            return resultado;
        }

        // ─────────────────────────────────────────────────────────
        // FOTO
        // ─────────────────────────────────────────────────────────
        private void btnSubirFoto_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar foto de perfil",
                Filter = "Imágenes (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                _fotoBytes = File.ReadAllBytes(dialog.FileName);
                imgFotoFormulario.ImageSource = BytesABitmapImage(_fotoBytes);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo cargar la imagen.\n" + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────
        // GUARDAR
        // ─────────────────────────────────────────────────────────
        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarTodo())
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Hay campos con errores. Corregílos antes de guardar.",
                    "Formulario incompleto");
                return;
            }

            var rolItem = cmbRol.SelectedItem as ComboBoxItem;
            if (rolItem == null)
            {
                errRol.Text = "Seleccioná un rol.";
                errRol.Visibility = Visibility.Visible;
                return;
            }
            int rolId = Convert.ToInt32(rolItem.Tag);

            decimal tarifaHora = 0;
            if (!string.IsNullOrWhiteSpace(txtTarifaHora.Text))
                decimal.TryParse(txtTarifaHora.Text, out tarifaHora);

            if (_esNuevo)
            {
                var r = _controller.Insertar(
                    rolId: rolId,
                    nombre: txtNombre.Text,
                    apellido: txtApellido.Text,
                    dni: txtDni.Text,
                    clave: txtClave.Password,
                    domicilio: txtDomicilio.Text,
                    telefono: txtTelefono.Text,
                    email: txtEmail.Text,
                    foto: _fotoBytes,
                    tarifaHora: tarifaHora);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Usuario registrado!");
            }
            else
            {
                var r = _controller.Modificar(
                    id: _idEditar,
                    rolId: rolId,
                    nombre: txtNombre.Text,
                    apellido: txtApellido.Text,
                    dni: txtDni.Text,
                    claveNueva: txtClave.Password,
                    domicilio: txtDomicilio.Text,
                    telefono: txtTelefono.Text,
                    email: txtEmail.Text,
                    foto: _fotoBytes,
                    tarifaHora: tarifaHora);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Usuario actualizado!");
            }

            CerrarFormulario();
            CargarUsuarios();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            CerrarFormulario();
        }

        // ─────────────────────────────────────────────────────────
        // VALIDACIONES EN LOSTFOCUS
        // ─────────────────────────────────────────────────────────
        private void txtNombre_LostFocus(object sender, RoutedEventArgs e)
            => AplicarEstadoCampo(txtNombre, errNombre,
               Controllers.Validador.ValidarNombre(txtNombre.Text, "El nombre"));

        private void txtApellido_LostFocus(object sender, RoutedEventArgs e)
            => AplicarEstadoCampo(txtApellido, errApellido,
               Controllers.Validador.ValidarNombre(txtApellido.Text, "El apellido"));

        private void txtDni_LostFocus(object sender, RoutedEventArgs e)
            => AplicarEstadoCampo(txtDni, errDni,
               Controllers.Validador.ValidarDni(txtDni.Text));

        private void txtEmail_LostFocus(object sender, RoutedEventArgs e)
            => AplicarEstadoCampo(txtEmail, errEmail,
               Controllers.Validador.ValidarEmail(txtEmail.Text));

        private void txtTelefono_LostFocus(object sender, RoutedEventArgs e)
            => AplicarEstadoCampo(txtTelefono, errTelefono,
               Controllers.Validador.ValidarTelefono(txtTelefono.Text));

        private void txtClave_PasswordChanged(object sender, RoutedEventArgs e)
        {
            string err = Controllers.Validador.ValidarContrasena(txtClave.Password, _esNuevo);
            if (!string.IsNullOrEmpty(txtClave.Password) || _esNuevo)
            {
                errClave.Text = err ?? string.Empty;
                errClave.Visibility = err != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ─────────────────────────────────────────────────────────
        // BLOQUEO DNI: solo números en tiempo real
        // ─────────────────────────────────────────────────────────
        private void txtDni_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d$");
        }

        private void txtTarifaHora_PreviewTextInput(object sender, TextCompositionEventArgs e)
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

        // ─────────────────────────────────────────────────────────
        // VALIDAR TODO
        // ─────────────────────────────────────────────────────────
        private bool ValidarTodo()
        {
            bool ok = true;

            string errN = Controllers.Validador.ValidarNombre(txtNombre.Text, "El nombre");
            AplicarEstadoCampo(txtNombre, errNombre, errN);
            if (errN != null) ok = false;

            string errA = Controllers.Validador.ValidarNombre(txtApellido.Text, "El apellido");
            AplicarEstadoCampo(txtApellido, errApellido, errA);
            if (errA != null) ok = false;

            string errD = Controllers.Validador.ValidarDni(txtDni.Text);
            AplicarEstadoCampo(txtDni, errDni, errD);
            if (errD != null) ok = false;

            string errE = Controllers.Validador.ValidarEmail(txtEmail.Text);
            AplicarEstadoCampo(txtEmail, errEmail, errE);
            if (errE != null) ok = false;

            string errT = Controllers.Validador.ValidarTelefono(txtTelefono.Text);
            AplicarEstadoCampo(txtTelefono, errTelefono, errT);
            if (errT != null) ok = false;

            string errC = Controllers.Validador.ValidarContrasena(txtClave.Password, _esNuevo);
            errClave.Text = errC ?? string.Empty;
            errClave.Visibility = errC != null ? Visibility.Visible : Visibility.Collapsed;
            if (errC != null) ok = false;

            if (cmbRol.SelectedItem == null)
            {
                errRol.Text = "Seleccioná un rol.";
                errRol.Visibility = Visibility.Visible;
                ok = false;
            }
            else
                errRol.Visibility = Visibility.Collapsed;

            return ok;
        }

        // ─────────────────────────────────────────────────────────
        // FEEDBACK VISUAL POR CAMPO
        // ─────────────────────────────────────────────────────────
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
            TextBlock[] labels = { errNombre, errApellido, errDni,
                                   errEmail, errTelefono, errClave, errRol };
            TextBox[] campos = { txtNombre, txtApellido, txtDni,
                                   txtEmail, txtTelefono, txtDomicilio };

            foreach (var lbl in labels)
            { lbl.Text = string.Empty; lbl.Visibility = Visibility.Collapsed; }

            foreach (var campo in campos)
                campo.Style = (Style)Resources["InputEstilo"];
        }

        // ─────────────────────────────────────────────────────────
        // ANIMACIÓN DEL PANEL (slide + fade)
        // ─────────────────────────────────────────────────────────
        private void AbrirFormulario(string titulo)
        {
            lblTituloFormulario.Text = titulo;
            panelFormulario.Visibility = Visibility.Visible;
            panelFormulario.Opacity = 0;

            // Slide desde la derecha
            var translate = new TranslateTransform { X = 60 };
            panelFormulario.RenderTransform = translate;

            var slideAnim = new DoubleAnimation
            {
                From = 60,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            translate.BeginAnimation(TranslateTransform.XProperty, slideAnim);

            // Fade in
            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(300))
            };
            panelFormulario.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private void CerrarFormulario()
        {
            // Fade out rápido
            var fadeAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fadeAnim.Completed += (s, e) =>
            {
                panelFormulario.Visibility = Visibility.Collapsed;
                LimpiarFormulario();
                LimpiarErrores();
            };
            panelFormulario.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private void LimpiarFormulario()
        {
            txtNombre.Text = string.Empty;
            txtApellido.Text = string.Empty;
            txtDni.Text = string.Empty;
            txtEmail.Text = string.Empty;
            txtTelefono.Text = string.Empty;
            txtDomicilio.Text = string.Empty;
            txtTarifaHora.Text = "0";
            txtClave.Password = string.Empty;
            cmbRol.SelectedIndex = 0;
            imgFotoFormulario.ImageSource = null;
            _fotoBytes = null;
            _idEditar = 0;
        }

        private Usuario ObtenerUsuarioDeFila(object sender)
        {
            var btn = sender as Button;
            if (btn == null) return null;
            return btn.DataContext as Usuario;
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