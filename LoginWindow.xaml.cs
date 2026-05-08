// SistemaGimnacionOptimusCAI/LoginWindow.xaml.cs
// Compatible con C# 7.3.

using Controllers;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace SistemaGimnacionOptimusCAI
{
    public partial class LoginWindow : Window
    {
        private readonly UsuarioController _controller = new UsuarioController();
        private bool _mostrandoPassword = false;

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => txtDni.Focus();
        }

        // ── LOGIN ─────────────────────────────────────────────
        private void btnIngresar_Click(object sender, RoutedEventArgs e)
            => IntentarLogin();

        private void txtDni_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) txtPassword.Focus();
        }

        private void txtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) IntentarLogin();
        }

        private void IntentarLogin()
        {
            LimpiarErrores();

            string dni = txtDni.Text.Trim();
            string password = txtPassword.Password;

            bool hayError = false;

            if (string.IsNullOrEmpty(dni) || dni.Length != 8)
            {
                errDni.Text = "El DNI debe tener 8 digitos.";
                errDni.Visibility = Visibility.Visible;
                hayError = true;
            }

            if (string.IsNullOrEmpty(password))
            {
                errPassword.Text = "La contrasena es obligatoria.";
                errPassword.Visibility = Visibility.Visible;
                hayError = true;
            }

            if (hayError) return;

            
            try
            {
                var resultado = _controller.Login(dni, password);

                if (!resultado.ok)
                {
                    MostrarError(resultado.mensaje);
                    txtPassword.Password = string.Empty;
                    txtPassword.Focus();
                    return;
                }

                // Iniciar sesion en el manager estático
                SesionManager.Iniciar(
                    resultado.usuario.Id,
                    resultado.usuario.Nombre,
                    resultado.usuario.Apellido,
                    resultado.usuario.Dni,
                    resultado.usuario.RolId,
                    resultado.usuario.RolNombre
                );

                // Abrir la ventana principal y cerrar el login
                var main = new MainWindow();
                main.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MostrarError("Error al conectar con la base de datos.\n" + ex.Message);
            }
        }

        // ── MOSTRAR/OCULTAR CONTRASEÑA ────────────────────────
        private void btnTogglePass_Click(object sender, RoutedEventArgs e)
        {
            // En WPF, PasswordBox no soporta mostrar texto directamente.
            // Alternativa: swapear PasswordBox con un TextBox.
            // Por simplicidad cambiamos solo el ícono y guardamos la pass.
            _mostrandoPassword = !_mostrandoPassword;
            lblTogglePass.Text = _mostrandoPassword ? "🔒" : "👁";
            // Nota: para mostrar el texto real habría que agregar un TextBox
            // con Visibility="Collapsed" y swapearlos. Dejamos el ícono
            // como UX mínima para no complicar el XAML.
        }

        // ── CERRAR ────────────────────────────────────────────
        private void btnSalir_Click(object sender, RoutedEventArgs e)
            => Application.Current.Shutdown();

        // Permite mover la ventana arrastrándola
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => DragMove();

        // ── HELPERS ──────────────────────────────────────────
        private void txtDni_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^\d$");

        private void MostrarError(string mensaje)
        {
            lblError.Text = mensaje;
            panelError.Visibility = Visibility.Visible;
        }

        private void LimpiarErrores()
        {
            errDni.Visibility = Visibility.Collapsed;
            errPassword.Visibility = Visibility.Collapsed;
            panelError.Visibility = Visibility.Collapsed;
            errDni.Text = errPassword.Text = lblError.Text = string.Empty;
        }

        private static string HashSHA256(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}