// ============================================================
//  Archivo: NotificacionWindow.xaml.cs
//
//  Ventana de notificación personalizada.
//  Reemplaza los MessageBox del sistema por una UI propia.
//
//  Uso desde cualquier página:
//
//    // Éxito:
//    NotificacionWindow.MostrarExito("Usuario guardado correctamente.");
//
//    // Error:
//    NotificacionWindow.MostrarError("El DNI ya está registrado.");
//
//    // Advertencia:
//    NotificacionWindow.MostrarAdvertencia("Completá los campos obligatorios.");
//
//    // Confirmación (retorna true si el usuario apretó Sí):
//    bool confirmo = NotificacionWindow.MostrarConfirmacion(
//        "¿Querés desactivar este usuario?");
// ============================================================

using System.Windows;
using System.Windows.Media;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    // Tipos disponibles de notificación
    public enum TipoNotificacion
    {
        Exito,
        Error,
        Advertencia,
        Confirmacion
    }

    public partial class NotificacionWindow : Window
    {
        // Resultado cuando es Confirmación (true = Sí, false = No/Cerró)
        public bool Confirmado { get; private set; } = false;

        // ─────────────────────────────────────────────────────────
        // CONSTRUCTOR PRIVADO
        // Solo se instancia desde los métodos estáticos de abajo.
        // ─────────────────────────────────────────────────────────
        private NotificacionWindow(TipoNotificacion tipo, string titulo, string mensaje)
        {
            InitializeComponent();
            AplicarEstilo(tipo, titulo, mensaje);
        }

        // ─────────────────────────────────────────────────────────
        // APLICAR ESTILO SEGÚN EL TIPO
        // ─────────────────────────────────────────────────────────
        private void AplicarEstilo(TipoNotificacion tipo, string titulo, string mensaje)
        {
            txtTitulo.Text = titulo;
            txtMensaje.Text = mensaje;

            switch (tipo)
            {
                case TipoNotificacion.Exito:
                    bandaColor.Background = new SolidColorBrush(Color.FromRgb(46, 125, 50));   // Verde
                    circuloIcono.Background = new SolidColorBrush(Color.FromRgb(27, 60, 30));
                    btnAceptar.Background = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                    txtIcono.Text = "✅";
                    break;

                case TipoNotificacion.Error:
                    bandaColor.Background = new SolidColorBrush(Color.FromRgb(198, 40, 40));   // Rojo
                    circuloIcono.Background = new SolidColorBrush(Color.FromRgb(70, 20, 20));
                    btnAceptar.Background = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                    txtIcono.Text = "❌";
                    break;

                case TipoNotificacion.Advertencia:
                    bandaColor.Background = new SolidColorBrush(Color.FromRgb(230, 130, 0));   // Naranja
                    circuloIcono.Background = new SolidColorBrush(Color.FromRgb(70, 45, 0));
                    btnAceptar.Background = new SolidColorBrush(Color.FromRgb(230, 130, 0));
                    txtIcono.Text = "⚠️";
                    break;

                case TipoNotificacion.Confirmacion:
                    bandaColor.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));   // Azul
                    circuloIcono.Background = new SolidColorBrush(Color.FromRgb(0, 40, 80));
                    btnAceptar.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    txtIcono.Text = "❓";
                    // En confirmación agregamos botón Cancelar
                    MostrarBotonesDosOpciones();
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────
        // AGREGAR BOTÓN "NO" PARA CONFIRMACIONES
        // ─────────────────────────────────────────────────────────
        private void MostrarBotonesDosOpciones()
        {
            // Cambiar el texto del botón principal a "Sí"
            btnAceptar.Content = "Sí, confirmar";

            // Crear botón "No" dinámicamente
            var btnNo = new System.Windows.Controls.Button
            {
                Content = "No, cancelar",
                Height = 42,
                MinWidth = 120,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(50, 50, 70)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 12, 0),
                Style = (System.Windows.Style)Resources["BtnNotifStyle"]
            };
            btnNo.Click += (s, e) => { Confirmado = false; Close(); };

            // Reemplazar el botón único por un panel con los dos
            var panel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Mover el btnAceptar (Sí) al panel
            var grid = (System.Windows.Controls.Grid)contenedorPrincipal.Child;
            grid.Children.Remove(btnAceptar);
            panel.Children.Add(btnNo);
            panel.Children.Add(btnAceptar);
            System.Windows.Controls.Grid.SetRow(panel, 3);
            grid.Children.Add(panel);
        }

        // ─────────────────────────────────────────────────────────
        // EVENTOS DE BOTONES
        // ─────────────────────────────────────────────────────────
        private void btnAceptar_Click(object sender, RoutedEventArgs e)
        {
            Confirmado = true;
            Close();
        }

        // ─────────────────────────────────────────────────────────
        // MÉTODOS ESTÁTICOS DE ACCESO RÁPIDO
        // Estos son los que vas a llamar desde las páginas.
        // ─────────────────────────────────────────────────────────

        /// <summary>Muestra un mensaje de ÉXITO (verde).</summary>
        public static void MostrarExito(string mensaje, string titulo = "¡Listo!")
        {
            var ventana = new NotificacionWindow(TipoNotificacion.Exito, titulo, mensaje);
            ventana.ShowDialog();
        }

        /// <summary>Muestra un mensaje de ERROR (rojo).</summary>
        public static void MostrarError(string mensaje, string titulo = "Se encontró un problema")
        {
            var ventana = new NotificacionWindow(TipoNotificacion.Error, titulo, mensaje);
            ventana.ShowDialog();
        }

        /// <summary>Muestra un mensaje de ADVERTENCIA (naranja).</summary>
        public static void MostrarAdvertencia(string mensaje, string titulo = "Atención")
        {
            var ventana = new NotificacionWindow(TipoNotificacion.Advertencia, titulo, mensaje);
            ventana.ShowDialog();
        }

        /// <summary>
        /// Muestra una CONFIRMACIÓN (azul) con Sí/No.
        /// Retorna true si el usuario eligió "Sí".
        /// </summary>
        public static bool MostrarConfirmacion(string mensaje, string titulo = "Confirmar acción")
        {
            var ventana = new NotificacionWindow(TipoNotificacion.Confirmacion, titulo, mensaje);
            ventana.ShowDialog();
            return ventana.Confirmado;
        }
    }
}