// SistemaGimnacionOptimusCAI/MainWindow.xaml.cs — C# 7.3
using Controllers;
using FontAwesome.WPF;
using SistemaGimnacionOptimusCAI.Paginas;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SistemaGimnacionOptimusCAI
{
    public partial class MainWindow : Window
    {
        // Definicion de un item del menu
        private class MenuItem
        {
            public FontAwesome.WPF.FontAwesomeIcon Icono { get; set; }
            public string Texto { get; set; }
            public Type TipoPagina { get; set; }
            public bool SoloAdmin { get; set; }
        }

        private List<Border> _botonesMenu = new List<Border>();
        private Border _botonActivo = null;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        // ── INICIO ────────────────────────────────────────────
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Verificar sesion
            if (!SesionManager.HaySesion)
            {
                MessageBox.Show("No hay sesion activa. Volviendo al login.",
                                "Sesion", MessageBoxButton.OK, MessageBoxImage.Warning);
                VolverAlLogin();
                return;
            }

            CargarDatosUsuario();
            ConstruirMenu();
            NavegarPagina(0); // Abrir la primera pagina del menu
        }

        // ── DATOS DEL USUARIO LOGUEADO ────────────────────────
        private void CargarDatosUsuario()
        {
            string nombreCompleto = SesionManager.NombreCompleto;
            lblUserNombre.Text = nombreCompleto;
            lblUserIniciales.Text = ObtenerIniciales(nombreCompleto);

            if (SesionManager.EsAdmin)
            {
                lblUserRol.Text = "ADMIN";
                lblUserRol.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 53));
                badgeRol.Background = new SolidColorBrush(Color.FromRgb(42, 22, 0));
            }
            else
            {
                lblUserRol.Text = "EMPLEADO";
                lblUserRol.Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250));
                badgeRol.Background = new SolidColorBrush(Color.FromRgb(30, 24, 64));
            }
        }

        private string ObtenerIniciales(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return "?";
            string[] partes = nombre.Trim().Split(' ');
            if (partes.Length >= 2)
                return ("" + partes[0][0] + partes[1][0]).ToUpper();
            return nombre[0].ToString().ToUpper();
        }

        // ── CONSTRUCCION DEL MENU SEGUN ROL ───────────────────
        private void ConstruirMenu()
        {
            panelMenu.Children.Clear();
            _botonesMenu.Clear();

            // Lista completa de items
            var items = new List<MenuItem>
            {
                // Todos ven estos
                new MenuItem { Icono = FontAwesomeIcon.Home,                Texto = "Inicio",              TipoPagina = typeof(DashboardPage),             SoloAdmin = false },
                new MenuItem { Icono = FontAwesomeIcon.Users,               Texto = "Socios",               TipoPagina = typeof(SociosPage),                SoloAdmin = false },
                new MenuItem { Icono = FontAwesomeIcon.IdCardOutline,       Texto = "Membresías",           TipoPagina = typeof(MembresiasPage),            SoloAdmin = false },
                new MenuItem { Icono = FontAwesomeIcon.CheckSquareOutline,  Texto = "Asistencias",          TipoPagina = typeof(AsistenciasPage),           SoloAdmin = false },
                new MenuItem { Icono = FontAwesomeIcon.Money,               Texto = "Caja",                 TipoPagina = typeof(CajaPage),                  SoloAdmin = false },
                new MenuItem { Icono = FontAwesomeIcon.ShoppingBasket,      Texto = "Ventas",               TipoPagina = typeof(VentasPage),                SoloAdmin = false },
                new MenuItem { Icono = FontAwesomeIcon.Book,                Texto = "Productos",            TipoPagina = typeof(ProductosPage),             SoloAdmin = false },
                new MenuItem { Icono = FontAwesomeIcon.Calendar,            Texto = "Turnos",               TipoPagina = typeof(TurnosPage),                SoloAdmin = false },
                new MenuItem { Icono = FontAwesomeIcon.ClockOutline,        Texto = "Fichaje Instructores", TipoPagina = typeof(InstructorAsistenciasPage), SoloAdmin = false },
                new MenuItem { Icono = FontAwesomeIcon.Clipboard,           Texto = "Rutinas",              TipoPagina = typeof(RutinasPage),               SoloAdmin = false },
                new MenuItem { Icono = FontAwesomeIcon.Whatsapp,            Texto = "WhatsApp",             TipoPagina = typeof(WhatsappPage),              SoloAdmin = false },

    // Solo admin
    new MenuItem { Icono = FontAwesomeIcon.SoccerBallOutline,Texto = "Actividades",          TipoPagina = typeof(ActividadesPage),          SoloAdmin = true },
    new MenuItem { Icono = FontAwesomeIcon.Lock,            Texto = "Casilleros",           TipoPagina = typeof(CasillerosPage),            SoloAdmin = true },
    new MenuItem { Icono = FontAwesomeIcon.FileTextOutline,  Texto = "Auditoría",            TipoPagina = typeof(AuditoriaPage),            SoloAdmin = true },
    new MenuItem { Icono = FontAwesomeIcon.User,            Texto = "Usuarios",             TipoPagina = typeof(UsuariosPage),              SoloAdmin = true }
            };

            // Filtrar segun rol y crear los botones
            int index = 0;
            foreach (var item in items)
            {
                if (item.SoloAdmin && !SesionManager.EsAdmin) continue;

                var btn = CrearBotonMenu(item, index);
                panelMenu.Children.Add(btn);
                _botonesMenu.Add(btn);
                index++;
            }
        }

        private Border CrearBotonMenu(MenuItem item, int indice)
        {
            var border = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 4),
                Height = 42,
                Cursor = Cursors.Hand,
                Tag = new object[] { indice, item.TipoPagina, item.Texto }
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            // ICONO DE FONT AWESOME
            var imgIcono = new FontAwesome.WPF.ImageAwesome
            {
                Icon = item.Icono,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), // Gris azulado que sugerimos
                Height = 16,
                Width = 16,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // TEXTO
            var txtTexto = new TextBlock
            {
                Text = item.Texto,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 192)),
                VerticalAlignment = VerticalAlignment.Center
            };

            stack.Children.Add(imgIcono);
            stack.Children.Add(txtTexto);
            border.Child = stack;

            // Hover
            border.MouseEnter += (s, e) =>
            {
                if (border != _botonActivo)
                    border.Background = new SolidColorBrush(Color.FromRgb(22, 22, 42));
            };
            border.MouseLeave += (s, e) =>
            {
                if (border != _botonActivo)
                    border.Background = Brushes.Transparent;
            };

            // Click
            border.MouseLeftButtonUp += (s, e) => NavegarPagina(indice);

            return border;
        }

        // ── NAVEGACION ────────────────────────────────────────
        private void NavegarPagina(int indice)
        {
            if (indice < 0 || indice >= _botonesMenu.Count) return;

            var btn = _botonesMenu[indice];
            var tag = btn.Tag as object[];
            if (tag == null || tag.Length < 3) return;

            Type tipoPagina = tag[1] as Type;
            string nombrePagina = tag[2] as string;

            if (tipoPagina == null) return;

            // Marcar boton activo
            ResaltarBotonActivo(btn);

            // Mostrar nombre en barra superior
            lblPaginaActual.Text = nombrePagina;

            // Navegar
            try
            {
                var pagina = Activator.CreateInstance(tipoPagina) as Page;
                if (pagina != null)
                    frameContenido.Navigate(pagina);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir la pagina:\n" + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResaltarBotonActivo(Border btn)
        {
            // Colores de tu paleta
            var colorBordeActivo = new SolidColorBrush(Color.FromRgb(167, 139, 250)); // El lila de tu captura
            var colorFondoActivo = new SolidColorBrush(Color.FromRgb(26, 24, 64));   // Fondo oscuro resaltado
            var colorIconoInactivo = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Gris azulado
            var colorTextoInactivo = new SolidColorBrush(Color.FromRgb(160, 160, 192)); // Gris tenue

            foreach (var b in _botonesMenu)
            {
                // RESETEAR INACTIVOS
                b.Background = Brushes.Transparent;
                b.BorderThickness = new Thickness(0); // Quitar borde

                var stack = b.Child as StackPanel;
                if (stack != null)
                {
                    // Reset color del Icono (FontAwesome) y del Texto
                    ((FontAwesome.WPF.ImageAwesome)stack.Children[0]).Foreground = colorIconoInactivo;
                    ((TextBlock)stack.Children[1]).Foreground = colorTextoInactivo;
                }
            }

            // RESALTAR BOTÓN CLICKEADO (IGUAL A TU FOTO)
            btn.Background = colorFondoActivo;
            btn.BorderBrush = colorBordeActivo;
            btn.BorderThickness = new Thickness(1); // Aplicar el borde lila
            btn.CornerRadius = new CornerRadius(8);

            var stackActivo = btn.Child as StackPanel;
            if (stackActivo != null)
            {
                // Icono y Texto en blanco/lavanda para que resalten
                ((FontAwesome.WPF.ImageAwesome)stackActivo.Children[0]).Foreground = Brushes.White;
                ((TextBlock)stackActivo.Children[1]).Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255));
            }

            _botonActivo = btn;
        }

        // ── CERRAR SESION ─────────────────────────────────────
        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            var resultado = MessageBox.Show(
                "¿Cerrar sesion?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultado != MessageBoxResult.Yes) return;

            SesionManager.Cerrar();
            VolverAlLogin();
        }

        private void VolverAlLogin()
        {
            var login = new LoginWindow();
            login.Show();
            this.Close();
        }

        // ── BOTONES DE VENTANA ────────────────────────────────
        private void btnMinimizar_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void btnMaximizar_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                lblMaxIcon.Text = "🗖";
            }
            else
            {
                WindowState = WindowState.Maximized;
                lblMaxIcon.Text = "🗗";
            }
        }

        private void btnCerrarVentana_Click(object sender, RoutedEventArgs e)
            => Application.Current.Shutdown();

        // ── DRAG WINDOW ───────────────────────────────────────
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (WindowState == WindowState.Maximized) return;
            try { DragMove(); } catch { }
        }
    }
}