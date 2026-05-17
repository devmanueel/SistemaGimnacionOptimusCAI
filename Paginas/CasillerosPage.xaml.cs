// ============================================================
//  Archivo: CasillerosPage.xaml.cs
//
//  Genera dinámicamente una grilla visual de "lockers" donde
//  cada casillero es un Button cuyo color cambia según estado:
//   · Verde  = libre
//   · Naranja = ocupado (con foto del socio)
//   · Amarillo = mantenimiento
//
//  Click en un casillero abre el panel lateral con sus detalles
//  y acciones disponibles según el estado.
//
//  Compatible con C# 7.3.
// ============================================================

using Controllers;
using Entities;
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
using System.Windows.Shapes;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class CasillerosPage : Page
    {
        private readonly CasilleroController _controller = new CasilleroController();

        private string _filtroEstado = "todos";
        private long _idSeleccionado = 0;
        private Casillero _casilleroActual = null;

        // Cache de la lista actual para no consultar dos veces
        private List<Casillero> _todosLosCasilleros = new List<Casillero>();

        public CasillerosPage()
        {
            InitializeComponent();

            ResaltarChip(chipTodos);
            CargarCasilleros();
            CargarComboSocios();
        }

        // ─────────────────────────────────────────────────────
        // CARGAR CASILLEROS Y RENDERIZAR LA GRILLA
        // ─────────────────────────────────────────────────────
        private void CargarCasilleros()
        {
            try
            {
                _todosLosCasilleros = _controller.ObtenerCasilleros();
                ActualizarStats();
                RenderizarGrilla();
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar casilleros");
            }
        }

        private void ActualizarStats()
        {
            try
            {
                var stats = _controller.ObtenerEstadisticas();
                statTotal.Text = stats.Total.ToString();
                statLibres.Text = stats.Libres.ToString();
                statOcupados.Text = stats.Ocupados.ToString();
                statMantenimiento.Text = stats.Mantenimiento.ToString();
                statIngresoPotencial.Text = stats.IngresoPotencialTexto;
            }
            catch
            {
                statTotal.Text = statLibres.Text = statOcupados.Text =
                    statMantenimiento.Text = statIngresoPotencial.Text = "—";
            }
        }

        private void CargarComboSocios()
        {
            try { cmbSocio.ItemsSource = _controller.ListarSociosParaCombo(); }
            catch { /* silencioso */ }
        }

        /// <summary>
        /// Genera dinámicamente los botones de lockers en el WrapPanel,
        /// aplicando el filtro de estado y de búsqueda.
        /// </summary>
        private void RenderizarGrilla()
        {
            panelCasilleros.Children.Clear();

            string textoBuscar = (txtBuscar.Text ?? string.Empty).Trim().ToLower();

            int mostrados = 0;
            foreach (var c in _todosLosCasilleros)
            {
                // Filtro por estado
                if (_filtroEstado != "todos" && c.Estado != _filtroEstado)
                    continue;

                // Filtro por texto (número o nombre del socio)
                if (textoBuscar.Length > 0)
                {
                    bool coincide =
                        c.Numero.ToString().Contains(textoBuscar) ||
                        c.NumeroFormateado.ToLower().Contains(textoBuscar) ||
                        (c.SocioNombre != null && c.SocioNombre.ToLower().Contains(textoBuscar));

                    if (!coincide) continue;
                }

                panelCasilleros.Children.Add(CrearBotonCasillero(c));
                mostrados++;
            }

            // Mostrar mensaje si no hay nada
            panelVacio.Visibility = mostrados == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Construye un botón "locker" para un casillero específico,
        /// con color según estado y mini foto si tiene socio.
        /// </summary>
        private Button CrearBotonCasillero(Casillero c)
        {
            var btn = new Button
            {
                Style = (Style)Resources["CasilleroBtnEstilo"],
                Tag = c.Id,
                DataContext = c
            };
            btn.Click += BtnCasillero_Click;

            // Colores según estado
            Color colorFondo, colorBorde, colorTexto, colorIcono = Colors.White;
            if (c.EsLibre)
            {
                colorFondo = Color.FromRgb(10, 42, 20);    // #0A2A14
                colorBorde = Color.FromRgb(0, 230, 118);   // #00E676
                colorTexto = Color.FromRgb(0, 230, 118);
                colorIcono = colorTexto;                   // Cambios de JoakoG
            }
            else if (c.EsOcupado)
            {
                colorFondo = Color.FromRgb(42, 22, 0);     // #2A1600
                colorBorde = Color.FromRgb(255, 107, 53);  // #FF6B35
                colorTexto = Color.FromRgb(255, 107, 53);
                colorIcono = colorTexto;                    // Cambios de JoakoG
            }
            else
            {
                colorFondo = Color.FromRgb(42, 31, 0);     // #2A1F00
                colorBorde = Color.FromRgb(255, 167, 38);  // #FFA726
                colorTexto = Color.FromRgb(255, 167, 38);  // Cambios de JoakoG
            }

            btn.Background = new SolidColorBrush(colorFondo);
            btn.BorderBrush = new SolidColorBrush(colorBorde);

            // Contenido del locker
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };




            // Ícono según estado
            // --- CAMBIO AQUÍ: Usamos ImageAwesome en lugar de TextBlock ---
            var icoCasillero = new FontAwesome.WPF.ImageAwesome
            {
                Height = 22, // Mantenemos el tamaño similar al anterior FontSize
                Foreground = new SolidColorBrush(colorIcono),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };

            // Asignación de icono según estado usando el Enum de FontAwesome
            if (c.EsLibre) icoCasillero.Icon = FontAwesome.WPF.FontAwesomeIcon.UnlockAlt;
            else if (c.EsOcupado) icoCasillero.Icon = FontAwesome.WPF.FontAwesomeIcon.Lock;
            else icoCasillero.Icon = FontAwesome.WPF.FontAwesomeIcon.Wrench;

            stack.Children.Add(icoCasillero);
            // -------------------------------------------------------------




            // Número del casillero
            var lblNum = new TextBlock
            {
                Text = c.NumeroFormateado,
                FontFamily = new FontFamily("Bahnschrift SemiBold, Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(colorTexto),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            stack.Children.Add(lblNum);

            // Si está ocupado: mostrar mini foto + nombre corto
            if (c.EsOcupado && !string.IsNullOrEmpty(c.SocioNombre))
            {
                if (c.SocioFoto != null && c.SocioFoto.Length > 0)
                {
                    var ellipse = new Ellipse
                    {
                        Width = 24,
                        Height = 24,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 4),
                        Fill = new ImageBrush(BytesABitmapImage(c.SocioFoto))
                        {
                            Stretch = Stretch.UniformToFill
                        }
                    };
                    stack.Children.Add(ellipse);
                }

                // Nombre corto (apellido)
                string nombreCorto = c.SocioNombre;
                if (nombreCorto.Length > 11) nombreCorto = nombreCorto.Substring(0, 10) + "…";

                var lblNombre = new TextBlock
                {
                    Text = nombreCorto,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                stack.Children.Add(lblNombre);
            }
            else if (c.EsLibre)
            {
                var lblEstado = new TextBlock
                {
                    Text = "LIBRE",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118)),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                stack.Children.Add(lblEstado);
            }
            else
            {
                var lblEstado = new TextBlock
                {
                    Text = "MANTEN.",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 167, 38)),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                stack.Children.Add(lblEstado);
            }

            btn.Content = stack;
            return btn;
        }

        // ─────────────────────────────────────────────────────
        // BÚSQUEDA / FILTROS
        // ─────────────────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => RenderizarGrilla();

        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            _filtroEstado = btn.Tag.ToString();
            ResaltarChip(btn);
            RenderizarGrilla();
        }

        private void ResaltarChip(Button seleccionado)
        {
            Button[] chips = { chipTodos, chipLibres, chipOcupados, chipManten };
            foreach (var c in chips)
            {
                // Al asignar el estilo completo, recuperás el espaciado y los efectos automáticos
                if (c == seleccionado)
                {
                    c.Style = (Style)FindResource("BotonChipActivoEstilo");
                }
                else
                {
                    c.Style = (Style)FindResource("BotonChipEstilo");
                }
            }
        }

        // ─────────────────────────────────────────────────────
        // CLICK EN UN CASILLERO → abrir panel de detalle
        // ─────────────────────────────────────────────────────
        private void BtnCasillero_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            var casillero = btn.DataContext as Casillero;
            if (casillero == null) return;

            MostrarDetalle(casillero);
        }

        private void MostrarDetalle(Casillero c)
        {
            _casilleroActual = c;
            _idSeleccionado = c.Id;

            // Header
            lblNumeroDetalle.Text = c.NumeroFormateado;

            // Badge de estado (color + texto)
            if (c.EsLibre)
            {
                badgeEstado.Background = new SolidColorBrush(Color.FromRgb(10, 42, 20));
                lblEstadoDetalle.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
                lblEstadoDetalle.Text = "LIBRE";
                lineaSuperior.Background = new SolidColorBrush(Color.FromRgb(0, 230, 118));
            }
            else if (c.EsOcupado)
            {
                badgeEstado.Background = new SolidColorBrush(Color.FromRgb(42, 22, 0));
                lblEstadoDetalle.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 53));
                lblEstadoDetalle.Text = "OCUPADO";
                lineaSuperior.Background = new SolidColorBrush(Color.FromRgb(255, 107, 53));
            }
            else
            {
                badgeEstado.Background = new SolidColorBrush(Color.FromRgb(42, 31, 0));
                lblEstadoDetalle.Foreground = new SolidColorBrush(Color.FromRgb(255, 167, 38));
                lblEstadoDetalle.Text = "MANTENIMIENTO";
                lineaSuperior.Background = new SolidColorBrush(Color.FromRgb(255, 167, 38));
            }

            // Datos del socio asignado
            if (c.EsOcupado && !string.IsNullOrEmpty(c.SocioNombre))
            {
                lblSocioNombre.Text = c.SocioNombre;
                lblSocioNumero.Text = c.NumeroSocio.HasValue ? "#" + c.NumeroSocio.Value.ToString("D4") : "";
                lblAsignadoDesde.Text = c.AsignadoDesdeTexto;

                if (c.SocioFoto != null && c.SocioFoto.Length > 0)
                    imgSocio.ImageSource = BytesABitmapImage(c.SocioFoto);
                else
                    imgSocio.ImageSource = null;

                panelSocio.Visibility = Visibility.Visible;
                panelAsignar.Visibility = Visibility.Collapsed;
                btnLiberar.Visibility = Visibility.Visible;
                btnMantenimiento.Visibility = Visibility.Collapsed;
            }
            else if (c.EsLibre)
            {
                panelSocio.Visibility = Visibility.Collapsed;
                panelAsignar.Visibility = Visibility.Visible;
                btnLiberar.Visibility = Visibility.Collapsed;
                btnMantenimiento.Visibility = Visibility.Visible;
                btnMantenimiento.Content = "🔧  PONER EN MANTENIMIENTO";
                cmbSocio.SelectedIndex = -1;
            }
            else // mantenimiento
            {
                panelSocio.Visibility = Visibility.Collapsed;
                panelAsignar.Visibility = Visibility.Collapsed;
                btnLiberar.Visibility = Visibility.Collapsed;
                btnMantenimiento.Visibility = Visibility.Visible;
                btnMantenimiento.Content = "✓  VOLVER A LIBRE";
            }

            // Datos del casillero
            txtPrecio.Text = c.PrecioMes.HasValue ? c.PrecioMes.Value.ToString("F0") : string.Empty;
            txtObservaciones.Text = c.Observaciones ?? string.Empty;

            // Animación de entrada
            AbrirPanelDetalle();
        }

        // ─────────────────────────────────────────────────────
        // BOTONES DEL PANEL DETALLE
        // ─────────────────────────────────────────────────────
        private void btnAsignar_Click(object sender, RoutedEventArgs e)
        {
            if (_idSeleccionado <= 0) return;

            var socio = cmbSocio.SelectedItem as SocioComboItem;
            if (socio == null)
            {
                NotificacionWindow.MostrarAdvertencia("Tenés que seleccionar un socio.");
                return;
            }

            try
            {
                var r = _controller.Asignar(_idSeleccionado, socio.Id, txtObservaciones.Text);
                if (r.ok)
                {
                    NotificacionWindow.MostrarExito(
                        "Casillero " + _casilleroActual.NumeroFormateado +
                        " asignado a " + socio.Apellido + ", " + socio.Nombre + ".",
                        "¡Asignado!");
                    CargarCasilleros();
                    CerrarPanelDetalle();
                }
                else NotificacionWindow.MostrarError(r.mensaje);
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void btnLiberar_Click(object sender, RoutedEventArgs e)
        {
            if (_idSeleccionado <= 0 || _casilleroActual == null) return;

            bool confirmo = NotificacionWindow.MostrarConfirmacion(
                "¿Liberar el casillero " + _casilleroActual.NumeroFormateado + "?\n\n" +
                "Va a dejar de estar asignado a " + _casilleroActual.SocioNombre + ".",
                "Liberar casillero");

            if (!confirmo) return;

            try
            {
                var r = _controller.Liberar(_idSeleccionado);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarCasilleros(); CerrarPanelDetalle(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void btnMantenimiento_Click(object sender, RoutedEventArgs e)
        {
            if (_idSeleccionado <= 0 || _casilleroActual == null) return;

            // Si está libre → poner en mantenimiento
            // Si está en mantenimiento → volver a libre
            string nuevoEstado = _casilleroActual.EsMantenimiento ? "libre" : "mantenimiento";

            try
            {
                var r = _controller.CambiarEstado(_idSeleccionado, nuevoEstado);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarCasilleros(); CerrarPanelDetalle(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void btnGuardarCambios_Click(object sender, RoutedEventArgs e)
        {
            if (_idSeleccionado <= 0) return;

            decimal? precio = null;
            if (!string.IsNullOrWhiteSpace(txtPrecio.Text))
            {
                decimal p;
                if (!decimal.TryParse(txtPrecio.Text, out p) || p < 0)
                {
                    NotificacionWindow.MostrarAdvertencia("El precio debe ser un número válido.");
                    return;
                }
                precio = p;
            }

            try
            {
                var r = _controller.Actualizar(_idSeleccionado, precio, txtObservaciones.Text);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarCasilleros(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_idSeleccionado <= 0 || _casilleroActual == null) return;

            if (_casilleroActual.EsOcupado)
            {
                NotificacionWindow.MostrarAdvertencia(
                    "No se puede eliminar un casillero ocupado. Liberalo primero.");
                return;
            }

            bool confirmo = NotificacionWindow.MostrarConfirmacion(
                "¿Eliminar definitivamente el casillero " + _casilleroActual.NumeroFormateado + "?",
                "Eliminar casillero");

            if (!confirmo) return;

            try
            {
                var r = _controller.Eliminar(_idSeleccionado);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarCasilleros(); CerrarPanelDetalle(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e) => CerrarPanelDetalle();

        // ─────────────────────────────────────────────────────
        // BOTONES SUPERIORES (Agregar / Crear en masa)
        // ─────────────────────────────────────────────────────
        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CasilleroInputDialog("Nuevo casillero", "Número del casillero (1-9999):", "");
            dlg.Owner = Window.GetWindow(this);

            if (dlg.ShowDialog() == true)
            {
                short numero;
                if (!short.TryParse(dlg.ValorIngresado, out numero) || numero < 1 || numero > 9999)
                {
                    NotificacionWindow.MostrarError("El número debe estar entre 1 y 9999.");
                    return;
                }

                var r = _controller.Crear(numero, null, null);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarCasilleros(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
        }

        private void btnCrearMasa_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CasilleroInputDialog(
                "Crear casilleros en masa",
                "Ingresá el rango (ej: 1-50) y el precio mensual.\nFormato: desde-hasta:precio",
                "1-50:3000");
            dlg.Owner = Window.GetWindow(this);

            if (dlg.ShowDialog() == true)
            {
                string entrada = dlg.ValorIngresado.Trim();

                short desde = 0, hasta = 0;
                decimal? precio = null;

                try
                {
                    string[] partes = entrada.Split(':');
                    string rango = partes[0];
                    string[] numeros = rango.Split('-');
                    desde = short.Parse(numeros[0].Trim());
                    hasta = short.Parse(numeros[1].Trim());

                    if (partes.Length > 1 && !string.IsNullOrWhiteSpace(partes[1]))
                    {
                        decimal p;
                        if (decimal.TryParse(partes[1].Trim(), out p)) precio = p;
                    }
                }
                catch
                {
                    NotificacionWindow.MostrarError(
                        "Formato inválido. Ejemplo: \"1-50:3000\" para crear del 1 al 50 con precio 3000.");
                    return;
                }

                var r = _controller.CrearEnMasa(desde, hasta, precio);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarCasilleros(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
        }

        // ─────────────────────────────────────────────────────
        // VALIDACIÓN DE INPUT
        // ─────────────────────────────────────────────────────
        private void txtPrecio_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[\d]$");
        }

        // ─────────────────────────────────────────────────────
        // ANIMACIONES DEL PANEL
        // ─────────────────────────────────────────────────────
        private void AbrirPanelDetalle()
        {
            panelDetalle.Visibility = Visibility.Visible;
            panelDetalle.Opacity = 0;

            var translate = new TranslateTransform { X = 60 };
            panelDetalle.RenderTransform = translate;

            var slide = new DoubleAnimation
            {
                From = 60,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            translate.BeginAnimation(TranslateTransform.XProperty, slide);

            var fade = new DoubleAnimation
            { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(300)) };
            panelDetalle.BeginAnimation(OpacityProperty, fade);
        }

        private void CerrarPanelDetalle()
        {
            var fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (s, e) =>
            {
                panelDetalle.Visibility = Visibility.Collapsed;
                _idSeleccionado = 0;
                _casilleroActual = null;
            };
            panelDetalle.BeginAnimation(OpacityProperty, fade);
        }

        // ─────────────────────────────────────────────────────
        // HELPER
        // ─────────────────────────────────────────────────────
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

    // ──────────────────────────────────────────────────────────
    //  DIÁLOGO INPUT SIMPLE (paleta verde actual)
    // ──────────────────────────────────────────────────────────
    public class CasilleroInputDialog : Window
    {
        public string ValorIngresado { get; private set; }
        private TextBox _txt;

        public CasilleroInputDialog(string titulo, string mensaje, string valorInicial)
        {
            Title = titulo;
            Width = 420;
            Height = 230;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 20, 16)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 52, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0)
            };

            var grid = new Grid { Margin = new Thickness(24) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lblTitulo = new TextBlock
            {
                Text = titulo,
                FontFamily = new FontFamily("Bahnschrift SemiBold, Segoe UI"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(lblTitulo, 0);

            var lblMensaje = new TextBlock
            {
                Text = mensaje,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 122, 90)),
                Margin = new Thickness(0, 0, 0, 14),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(lblMensaje, 1);

            _txt = new TextBox
            {
                Text = valorInicial,
                Background = new SolidColorBrush(Color.FromRgb(18, 26, 20)),
                Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(37, 211, 102)),
                BorderThickness = new Thickness(1.5),
                FontSize = 14,
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(_txt, 2);

            var stackBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(stackBtns, 3);

            var btnCancelar = new Button
            {
                Content = "Cancelar",
                Width = 100,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 0),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 122, 90)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 52, 36)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btnCancelar.Click += (s, e) => { DialogResult = false; Close(); };

            var btnOk = new Button
            {
                Content = "Aceptar",
                Width = 110,
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(37, 211, 102)),
                Foreground = new SolidColorBrush(Color.FromRgb(8, 10, 8)),
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            btnOk.Click += (s, e) =>
            {
                ValorIngresado = _txt.Text;
                DialogResult = true;
                Close();
            };

            stackBtns.Children.Add(btnCancelar);
            stackBtns.Children.Add(btnOk);

            grid.Children.Add(lblTitulo);
            grid.Children.Add(lblMensaje);
            grid.Children.Add(_txt);
            grid.Children.Add(stackBtns);
            border.Child = grid;
            Content = border;

            Loaded += (s, e) => { _txt.Focus(); _txt.SelectAll(); };
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { ValorIngresado = _txt.Text; DialogResult = true; Close(); }
                if (e.Key == Key.Escape) { DialogResult = false; Close(); }
            };
        }
    }
}