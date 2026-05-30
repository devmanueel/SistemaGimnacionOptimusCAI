// ============================================================
//  Archivo: UsuariosPage.xaml.cs
//  Versión: dark theme
// ============================================================

using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using SistemaGimnacionOptimusCAI.Ventanas;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class UsuariosPage : Page
    {
        private readonly UsuarioController _controller = new UsuarioController();

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

        private void ActualizarStats(System.Collections.Generic.List<Usuario> lista)
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
            var win = new UsuarioWindow();
            win.Owner = Window.GetWindow(this);
            win.ModoNuevo();
            if (win.ShowDialog() == true)
            {
                CargarUsuarios();
            }
        }

        // ─────────────────────────────────────────────────────────
        // EDITAR
        // ─────────────────────────────────────────────────────────
        private void btnEditar_Click(object sender, RoutedEventArgs e)
        {
            var usuario = ObtenerUsuarioDeFila(sender);
            if (usuario == null) return;

            var win = new UsuarioWindow();
            win.Owner = Window.GetWindow(this);
            win.ModoEditar(usuario);
            if (win.ShowDialog() == true)
            {
                CargarUsuarios();
            }
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
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 22, 16))
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            var lbl = new TextBlock
            {
                Text = "Nueva contraseña para " + nombreUsuario + ":",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 210, 200)),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var pwd = new PasswordBox
            {
                FontSize = 14,
                Height = 36,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 32, 24)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 80, 60)),
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
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(122, 201, 67)),
                Foreground = System.Windows.Media.Brushes.Black,
                FontWeight = FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnAceptar.Click += (s, ev) => { resultado = pwd.Password; win.Close(); };

            var btnCancelar = new Button
            {
                Content = "Cancelar",
                Width = 80,
                Height = 34,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 170, 160)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 80, 60)),
                Cursor = System.Windows.Input.Cursors.Hand
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
        // HELPERS
        // ─────────────────────────────────────────────────────────
        private Usuario ObtenerUsuarioDeFila(object sender)
        {
            var btn = sender as Button;
            if (btn == null) return null;
            return btn.DataContext as Usuario;
        }
    }
}