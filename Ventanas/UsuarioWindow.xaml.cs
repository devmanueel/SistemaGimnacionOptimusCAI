using Controllers;
using Entities;
using Microsoft.Win32;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class UsuarioWindow : Window
    {
        private byte[] _fotoBytes = null;

        public bool UsuarioGuardado { get; private set; } = false;

        private bool _esNuevo = true;
        private long _idEditar = 0;

        public UsuarioWindow()
        {
            InitializeComponent();
            cmbRol.SelectedIndex = 0;
        }

        public void ModoNuevo()
        {
            _esNuevo = true;
            _idEditar = 0;
            _fotoBytes = null;
            lblTituloFormulario.Text = "NUEVO USUARIO";
            lblClave.Text = "CONTRASEÑA *";
            lblClaveAclaracion.Visibility = Visibility.Collapsed;
            LimpiarFormulario();
        }

        public void ModoEditar(Usuario usuario)
        {
            _esNuevo = false;
            _idEditar = usuario.Id;

            lblTituloFormulario.Text = "EDITAR USUARIO";
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
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

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

            var controller = new UsuarioController();

            if (_esNuevo)
            {
                var r = controller.Insertar(
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
                var r = controller.Modificar(
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

            UsuarioGuardado = true;
            DialogResult = true;
            Close();
        }

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
        {
            string err = string.IsNullOrWhiteSpace(txtTelefono.Text)
                ? null
                : Controllers.Validador.ValidarTelefono(txtTelefono.Text);
            AplicarEstadoCampo(txtTelefono, errTelefono, err);
        }

        private void txtTelefono_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Controllers.Validador.EsCaracterTelefonoValido(e.Text);
        }

        private void txtTelefono_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string texto = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
                var soloDigitos = new System.Text.StringBuilder();
                foreach (char c in texto)
                    if (char.IsDigit(c)) soloDigitos.Append(c);

                string resultado = soloDigitos.ToString();
                if (resultado.Length > 10)
                    resultado = resultado.Substring(0, 10);

                if (resultado.Length > 0)
                {
                    var tb = sender as TextBox;
                    if (tb != null)
                    {
                        tb.Text = resultado;
                        tb.CaretIndex = tb.Text.Length;
                    }
                }
                e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void txtClave_PasswordChanged(object sender, RoutedEventArgs e)
        {
            string err = Controllers.Validador.ValidarContrasena(txtClave.Password, _esNuevo);
            if (!string.IsNullOrEmpty(txtClave.Password) || _esNuevo)
            {
                errClave.Text = err ?? string.Empty;
                errClave.Visibility = err != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

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

            string errT = string.IsNullOrWhiteSpace(txtTelefono.Text)
                ? null
                : Controllers.Validador.ValidarTelefono(txtTelefono.Text);
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