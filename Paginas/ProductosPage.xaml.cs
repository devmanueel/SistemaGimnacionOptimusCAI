// ============================================================
//  Archivo: ProductosPage.xaml.cs
//
//  Catálogo de productos con cards visuales:
//   · Cada producto es una card de 220×320 con foto, nombre,
//     precio, stock y badge de estado.
//   · Click en una card abre ventana emergente para edición.
//   · Filtros por stock + categoría.
//
//  Compatible con C# 7.3.
// ============================================================

using Controllers;
using Entities;
using FontAwesome.WPF;
using SistemaGimnacionOptimusCAI.Helpers;
using SistemaGimnacionOptimusCAI.Ventanas;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class ProductosPage : Page
    {
        private readonly ProductoController _controller = new ProductoController();

        private string _filtroStock = "todos";
        private string _filtroCategoria = null;
        private List<Producto> _todosLosProductos = new List<Producto>();

        public ProductosPage()
        {
            InitializeComponent();
            ConfigurarPermisosPorRol();
            ResaltarChip(chipTodos);
            CargarCategoriasFiltro();
            CargarProductos();
        }

        private void ConfigurarPermisosPorRol()
        {
            if (SesionManager.EsAdmin) return;

            cardValorInventario.Visibility = Visibility.Collapsed;
            colSepValorInventario.Width = new GridLength(0);
            colValorInventario.Width = new GridLength(0);
        }

        // ─────────────────────────────────────────────────────
        // CARGA + ESTADÍSTICAS
        // ─────────────────────────────────────────────────────
        private void CargarProductos()
        {
            try
            {
                _todosLosProductos = _controller.ObtenerProductos();
                ActualizarStats();
                RenderizarGrilla();
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar productos");
            }
        }

        private void ActualizarStats()
        {
            try
            {
                var stats = _controller.ObtenerEstadisticas();
                statTotal.Text = stats.Total.ToString();
                statSinStock.Text = stats.SinStock.ToString();
                statBajoStock.Text = stats.BajoStock.ToString();
                if (SesionManager.EsAdmin)
                    statValorInventario.Text = stats.ValorInventarioTexto;
                else
                    statValorInventario.Text = "-";
            }
            catch
            {
                statTotal.Text = statSinStock.Text =
                    statBajoStock.Text = statValorInventario.Text = "-";
            }
        }

        private void CargarCategoriasFiltro()
        {
            try
            {
                cmbFiltroCategoria.Items.Clear();
                cmbFiltroCategoria.Items.Add(new ComboBoxItem
                {
                    Content = "Todas las categorías",
                    Tag = null,
                    Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232)),
                    IsSelected = true
                });

                foreach (var cat in _controller.ListarCategorias())
                {
                    cmbFiltroCategoria.Items.Add(new ComboBoxItem
                    {
                        Content = cat,
                        Tag = cat,
                        Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255))
                    });
                }
            }
            catch { }
        }

        // ─────────────────────────────────────────────────────
        // RENDERIZAR GRILLA DE CARDS
        // ─────────────────────────────────────────────────────
        private void RenderizarGrilla()
        {
            panelProductos.Children.Clear();

            string buscar = (txtBuscar.Text ?? string.Empty).Trim().ToLower();

            int mostrados = 0;
            foreach (var p in _todosLosProductos)
            {
                if (buscar.Length > 0)
                {
                    bool coincide = p.Nombre.ToLower().Contains(buscar)
                                 || (p.Descripcion != null && p.Descripcion.ToLower().Contains(buscar));
                    if (!coincide) continue;
                }

                if (_filtroCategoria != null && p.Categoria != _filtroCategoria)
                    continue;

                if (_filtroStock == "sin_stock" && !p.SinStock) continue;
                if (_filtroStock == "bajo_stock" && !p.BajoStock) continue;
                if (_filtroStock == "con_stock" && !p.ConStock) continue;

                panelProductos.Children.Add(CrearCardProducto(p));
                mostrados++;
            }

            panelVacio.Visibility = mostrados == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private Border CrearCardProducto(Producto p)
        {
            var card = new Border
            {
                Style = (Style)Resources["ProductoCardEstilo"],
                Tag = p.Id,
                DataContext = p,
                RenderTransform = new ScaleTransform(1, 1)
            };

            card.MouseLeftButtonUp += (s, e) => AbrirVentanaEditar(p);

            var stack = new StackPanel();

            var contenedorFoto = new Border
            {
                Height = 150,
                Background = new SolidColorBrush(Color.FromRgb(17, 24, 17)),
                CornerRadius = new CornerRadius(11, 11, 0, 0),
                ClipToBounds = true
            };

            var gridFoto = new Grid();
            if (p.Foto != null && p.Foto.Length > 0)
            {
                var img = new Image
                {
                    Source = BytesABitmapImage(p.Foto),
                    Stretch = Stretch.UniformToFill
                };
                gridFoto.Children.Add(img);
            }
            else
            {
                var iconoCategoria = new ImageAwesome
                {
                    Icon = ObtenerIconoCategoria(p.Categoria),
                    Width = 54,
                    Height = 54,
                    Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.4
                };
                gridFoto.Children.Add(iconoCategoria);
            }

            var badgeEstado = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 8, 0)
            };

            Color colorBadgeBg, colorBadgeFg;
            string textoBadge;
            if (p.SinStock)
            { colorBadgeBg = Color.FromRgb(42, 10, 10); colorBadgeFg = Color.FromRgb(255, 102, 102); textoBadge = "SIN STOCK"; }
            else if (p.BajoStock)
            { colorBadgeBg = Color.FromRgb(42, 31, 0); colorBadgeFg = Color.FromRgb(255, 167, 38); textoBadge = "BAJO STOCK"; }
            else
            { colorBadgeBg = Color.FromRgb(10, 42, 20); colorBadgeFg = Color.FromRgb(0, 230, 118); textoBadge = "DISPONIBLE"; }

            badgeEstado.Background = new SolidColorBrush(colorBadgeBg);
            var lblBadge = new TextBlock
            {
                Text = textoBadge,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(colorBadgeFg)
            };
            badgeEstado.Child = lblBadge;
            gridFoto.Children.Add(badgeEstado);

            if (!p.Activo)
            {
                var capaInactivo = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(180, 10, 10, 20))
                };
                var lblInactivo = new TextBlock
                {
                    Text = "INACTIVO",
                    FontFamily = new FontFamily("Bahnschrift SemiBold"),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                capaInactivo.Child = lblInactivo;
                gridFoto.Children.Add(capaInactivo);
            }

            contenedorFoto.Child = gridFoto;
            stack.Children.Add(contenedorFoto);
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(30, 40, 30))
            });

            var infoStack = new StackPanel { Margin = new Thickness(14, 12, 14, 12) };

            if (!string.IsNullOrEmpty(p.Categoria))
            {
                var lblCat = new TextBlock
                {
                    Text = p.Categoria.ToUpper(),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(122, 173, 122)),
                    Margin = new Thickness(0, 0, 0, 4)
                };
                infoStack.Children.Add(lblCat);
            }

            var lblNombre = new TextBlock
            {
                Text = p.Nombre,
                FontFamily = new FontFamily("Bahnschrift SemiBold, Segoe UI"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255)),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 38,
                Margin = new Thickness(0, 0, 0, 6)
            };
            infoStack.Children.Add(lblNombre);

            var gridFooter = new Grid();
            gridFooter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridFooter.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lblPrecio = new TextBlock
            {
                Text = p.PrecioTexto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lblPrecio, 0);
            gridFooter.Children.Add(lblPrecio);

            var lblStock = new TextBlock
            {
                Text = p.Stock + " u.",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = p.SinStock
                    ? new SolidColorBrush(Color.FromRgb(255, 85, 85))
                    : p.BajoStock
                        ? new SolidColorBrush(Color.FromRgb(255, 167, 38))
                        : new SolidColorBrush(Color.FromRgb(122, 173, 122)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lblStock, 1);
            gridFooter.Children.Add(lblStock);

            infoStack.Children.Add(gridFooter);

            if (SesionManager.EsAdmin)
            {
                var btnToggle = new Button
                {
                    Content = p.Activo ? "DESACTIVAR" : "REACTIVAR",
                    Style = (Style)Resources["ProductoBtnToggleStyle"],
                    Margin = new Thickness(0, 10, 0, 0),
                    Tag = p.Id
                };
                btnToggle.Click += BtnToggle_Click;
                infoStack.Children.Add(btnToggle);
            }

            stack.Children.Add(infoStack);

            card.Child = stack;
            return card;
        }

        private FontAwesomeIcon ObtenerIconoCategoria(string categoria)
        {
            if (string.IsNullOrEmpty(categoria)) return FontAwesomeIcon.Cube;
            string c = categoria.ToLower();
            if (c.Contains("bebida")) return FontAwesomeIcon.Coffee;
            if (c.Contains("supl")) return FontAwesomeIcon.Medkit;
            if (c.Contains("snack")) return FontAwesomeIcon.Cutlery;
            if (c.Contains("ropa")) return FontAwesomeIcon.ShoppingBag;
            if (c.Contains("acces")) return FontAwesomeIcon.Tags;
            if (c.Contains("higien")) return FontAwesomeIcon.Flask;
            return FontAwesomeIcon.Cube;
        }

        // ─────────────────────────────────────────────────────
        // BÚSQUEDA / FILTROS
        // ─────────────────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => RenderizarGrilla();

        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            _filtroStock = btn.Tag.ToString();
            ResaltarChip(btn);
            RenderizarGrilla();
        }

        private void ResaltarChip(Button seleccionado)
        {
            Button[] chips = { chipTodos, chipConStock, chipBajoStock, chipSinStock };
            foreach (var c in chips)
            {
                if (c == seleccionado)
                    c.Style = (Style)FindResource("BotonChipActivoEstilo");
                else
                    c.Style = (Style)FindResource("BotonChipEstilo");
            }
        }

        private void cmbFiltroCategoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = cmbFiltroCategoria.SelectedItem as ComboBoxItem;
            if (item == null) { _filtroCategoria = null; return; }
            _filtroCategoria = item.Tag as string;
            RenderizarGrilla();
        }

        // ─────────────────────────────────────────────────────
        // TOGGLE en cada card (solo admin)
        // ─────────────────────────────────────────────────────
        private void BtnToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionManager.EsAdmin)
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Solo administradores pueden cambiar el estado de productos.",
                    "Acceso denegado");
                return;
            }

            e.Handled = true;
            var btn = sender as Button;
            if (btn == null || btn.Tag == null) return;

            long id = Convert.ToInt64(btn.Tag);
            var prod = _todosLosProductos.Find(x => x.Id == id);
            if (prod == null) return;

            try
            {
                var r = _controller.CambiarEstado(id, !prod.Activo);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarProductos(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        // ─────────────────────────────────────────────────────
        // VENTANAS EMERGENTES
        // ─────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var win = new ProductoWindow();
            win.Owner = Window.GetWindow(this);
            win.ModoNuevo();
            if (win.ShowDialog() == true)
            {
                CargarProductos();
                CargarCategoriasFiltro();
            }
        }

        private void AbrirVentanaEditar(Producto p)
        {
            var win = new ProductoWindow();
            win.Owner = Window.GetWindow(this);
            win.ModoEditar(p);
            if (win.ShowDialog() == true)
            {
                CargarProductos();
                CargarCategoriasFiltro();
            }
        }

        // ─────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────
        private static System.Windows.Media.Imaging.BitmapImage BytesABitmapImage(byte[] bytes)
        {
            using (var ms = new System.IO.MemoryStream(bytes))
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                return bmp;
            }
        }
    }
}
