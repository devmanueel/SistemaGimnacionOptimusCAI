// ============================================================
//  Archivo: ProductosPage.xaml.cs
//
//  Catálogo de productos con cards visuales:
//   · Cada producto es una card de 220×320 con foto, nombre,
//     precio, stock y badge de estado.
//   · Click en una card abre el panel lateral con detalle/edición.
//   · En modo edición aparece el panel de "ajustar stock"
//     con botones + y − para sumar/restar inventario.
//   · Filtros por stock + categoría.
//
//  Compatible con C# 7.3.
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
using System.Windows.Shapes;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class ProductosPage : Page
    {
        private readonly ProductoController _controller = new ProductoController();

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private byte[] _fotoBytes = null;
        private string _filtroStock = "todos";
        private string _filtroCategoria = null;   // null = todas
        private Producto _productoActual = null;

        // Cache para no consultar la BD al filtrar
        private List<Producto> _todosLosProductos = new List<Producto>();

        public ProductosPage()
        {
            InitializeComponent();

            ResaltarChip(chipTodos);
            CargarCategoriasFiltro();
            CargarProductos();
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
                statValorInventario.Text = stats.ValorInventarioTexto;
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
            catch { /* silencioso */ }
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
                // Filtro de búsqueda
                if (buscar.Length > 0)
                {
                    bool coincide = p.Nombre.ToLower().Contains(buscar)
                                 || (p.Descripcion != null && p.Descripcion.ToLower().Contains(buscar));
                    if (!coincide) continue;
                }

                // Filtro de categoría
                if (_filtroCategoria != null && p.Categoria != _filtroCategoria)
                    continue;

                // Filtro de stock
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

        /// <summary>
        /// Construye una card visual para un producto.
        /// </summary>
        private Border CrearCardProducto(Producto p)
        {
            var card = new Border
            {
                Style = (Style)Resources["ProductoCardEstilo"],
                Tag = p.Id,
                DataContext = p,
                RenderTransform = new ScaleTransform(1, 1)
            };

            // Click en la card abre el formulario en modo edición
            card.MouseLeftButtonUp += (s, e) => AbrirParaEditar(p);

            var stack = new StackPanel();

            // ── Imagen del producto ──
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
                var emoji = new TextBlock
                {
                    Text = ObtenerEmojiCategoria(p.Categoria),
                    FontSize = 48,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.4
                };
                gridFoto.Children.Add(emoji);
            }

            // Badge de stock arriba a la derecha
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

            // Badge inactivo (si está desactivado)
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

            // ── Datos abajo ──
            var infoStack = new StackPanel { Margin = new Thickness(14, 12, 14, 12) };

            // Categoría
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

            // Nombre
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

            // Precio + stock
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

            // Botón toggle activo/inactivo
            var btnToggle = new Button
            {
                Content = p.Activo ? "DESACTIVAR" : "REACTIVAR",
                Style = (Style)Resources["ProductoBtnToggleStyle"],
                Margin = new Thickness(0, 10, 0, 0),
                Tag = p.Id
            };
            btnToggle.Click += BtnToggle_Click;

            infoStack.Children.Add(btnToggle);
            stack.Children.Add(infoStack);

            card.Child = stack;
            return card;
        }

        private string ObtenerEmojiCategoria(string categoria)
        {
            if (string.IsNullOrEmpty(categoria)) return "📦";
            string c = categoria.ToLower();
            if (c.Contains("bebida")) return "🥤";
            if (c.Contains("supl")) return "💊";
            if (c.Contains("snack")) return "🍫";
            if (c.Contains("ropa")) return "👕";
            if (c.Contains("acces")) return "🎒";
            if (c.Contains("higien")) return "🧴";
            return "📦";
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

        private void cmbFiltroCategoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = cmbFiltroCategoria.SelectedItem as ComboBoxItem;
            if (item == null) { _filtroCategoria = null; return; }
            _filtroCategoria = item.Tag as string;
            RenderizarGrilla();
        }

        // ─────────────────────────────────────────────────────
        // BOTÓN TOGGLE en cada card
        // ─────────────────────────────────────────────────────
        private void BtnToggle_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;  // Evita que dispare el click de la card
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
        // BOTONES PRINCIPALES
        // ─────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            _esNuevo = true;
            _idEditar = 0;
            _productoActual = null;
            LimpiarFormulario();
            LimpiarErrores();

            lblTituloFormulario.Text = "NUEVO PRODUCTO";
            txtStock.IsEnabled = true;
            lblStock.Text = "STOCK INICIAL";
            panelAjusteStock.Visibility = Visibility.Collapsed;

            AbrirFormulario();
        }

        private void AbrirParaEditar(Producto p)
        {
            _esNuevo = false;
            _idEditar = p.Id;
            _productoActual = p;
            LimpiarErrores();

            lblTituloFormulario.Text = "EDITAR PRODUCTO";
            txtNombre.Text = p.Nombre;
            txtDescripcion.Text = p.Descripcion ?? string.Empty;
            cmbCategoria.Text = p.Categoria ?? string.Empty;
            txtPrecio.Text = p.Precio.ToString("F0");
            ActualizarPreviewPrecio();

            // Stock NO editable acá (se ajusta con los botones + / -)
            txtStock.Text = p.Stock.ToString();
            txtStock.IsEnabled = false;
            lblStock.Text = "STOCK ACTUAL";
            txtStockMin.Text = p.StockMin.ToString();

            _fotoBytes = null;
            if (p.Foto != null && p.Foto.Length > 0)
            {
                imgFotoFormulario.Source = BytesABitmapImage(p.Foto);
                lblSinFoto.Visibility = Visibility.Collapsed;
            }
            else
            {
                imgFotoFormulario.Source = null;
                lblSinFoto.Visibility = Visibility.Visible;
            }

            // Mostrar panel de ajuste de stock
            panelAjusteStock.Visibility = Visibility.Visible;
            ActualizarLabelStock(p.Stock);

            AbrirFormulario();
        }

        private void btnSubirFoto_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar foto del producto",
                Filter = "Imágenes (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                _fotoBytes = File.ReadAllBytes(dialog.FileName);
                imgFotoFormulario.Source = BytesABitmapImage(_fotoBytes);
                lblSinFoto.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo cargar la imagen.\n" + ex.Message);
            }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarTodo()) return;

            decimal precio = 0; decimal.TryParse(txtPrecio.Text, out precio);
            int stock = 0; int.TryParse(txtStock.Text, out stock);
            int stockMin = 0; int.TryParse(txtStockMin.Text, out stockMin);

            string categoria = (cmbCategoria.Text ?? string.Empty).Trim();

            if (_esNuevo)
            {
                var r = _controller.Insertar(
                    txtNombre.Text, txtDescripcion.Text, categoria,
                    precio, stock, stockMin, _fotoBytes);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Producto creado!");
            }
            else
            {
                var r = _controller.Modificar(
                    _idEditar, txtNombre.Text, txtDescripcion.Text, categoria,
                    precio, stockMin, _fotoBytes);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Producto actualizado!");
            }

            CerrarFormulario();
            CargarProductos();
            CargarCategoriasFiltro();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) => CerrarFormulario();

        // ─────────────────────────────────────────────────────
        // AJUSTE DE STOCK
        // ─────────────────────────────────────────────────────
        private void btnSumarStock_Click(object sender, RoutedEventArgs e) => AjustarStock("sumar");
        private void btnRestarStock_Click(object sender, RoutedEventArgs e) => AjustarStock("restar");

        private void AjustarStock(string tipo)
        {
            if (_idEditar <= 0) return;

            int cantidad = 0;
            if (!int.TryParse(txtCantidadAjuste.Text, out cantidad) || cantidad <= 0)
            {
                NotificacionWindow.MostrarAdvertencia("Ingresá una cantidad válida (mayor a 0).");
                return;
            }

            try
            {
                var r = _controller.AjustarStock(_idEditar, tipo, cantidad);
                if (r.ok)
                {
                    NotificacionWindow.MostrarExito(r.mensaje);
                    txtStock.Text = r.stockFinal.ToString();
                    ActualizarLabelStock(r.stockFinal);
                    txtCantidadAjuste.Text = string.Empty;

                    // Refrescar lista en background para que se vea el nuevo stock
                    CargarProductos();
                }
                else NotificacionWindow.MostrarError(r.mensaje);
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void ActualizarLabelStock(int stock)
        {
            if (stock == 0)
            {
                lblStockActual.Text = "Sin stock";
                lblStockActual.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
            }
            else if (stock == 1)
            {
                lblStockActual.Text = "1 unidad";
                lblStockActual.Foreground = new SolidColorBrush(Color.FromRgb(255, 167, 38));
            }
            else
            {
                lblStockActual.Text = stock + " unidades";
                lblStockActual.Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232));
            }
        }

        // ─────────────────────────────────────────────────────
        // VALIDACIONES
        // ─────────────────────────────────────────────────────
        private void txtNombre_LostFocus(object sender, RoutedEventArgs e)
        {
            string err = null;
            if (string.IsNullOrWhiteSpace(txtNombre.Text)) err = "El nombre es obligatorio.";
            else if (txtNombre.Text.Trim().Length < 2) err = "Debe tener al menos 2 caracteres.";
            AplicarEstadoCampo(txtNombre, errNombre, err);
        }

        private void txtPrecio_LostFocus(object sender, RoutedEventArgs e)
        {
            decimal precio = 0;
            string err = null;
            if (string.IsNullOrWhiteSpace(txtPrecio.Text))
                err = "El precio es obligatorio.";
            else if (!decimal.TryParse(txtPrecio.Text, out precio) || precio <= 0)
                err = "El precio debe ser mayor a $0.";
            AplicarEstadoCampo(txtPrecio, errPrecio, err);
            ActualizarPreviewPrecio();
        }

        private void ActualizarPreviewPrecio()
        {
            decimal precio = 0;
            if (decimal.TryParse(txtPrecio.Text, out precio) && precio > 0)
            {
                lblPreviewPrecio.Text = "$" + precio.ToString("N0");
                panelPreviewPrecio.Visibility = Visibility.Visible;
            }
            else panelPreviewPrecio.Visibility = Visibility.Collapsed;
        }

        private void txtPrecio_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[\d]$");
        }

        private void txtSoloNumeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[\d]$");
        }

        private bool ValidarTodo()
        {
            bool ok = true;

            string e1 = null;
            if (string.IsNullOrWhiteSpace(txtNombre.Text)) e1 = "El nombre es obligatorio.";
            else if (txtNombre.Text.Trim().Length < 2) e1 = "Debe tener al menos 2 caracteres.";
            AplicarEstadoCampo(txtNombre, errNombre, e1);
            if (e1 != null) ok = false;

            decimal precio = 0;
            string e2 = null;
            if (string.IsNullOrWhiteSpace(txtPrecio.Text)) e2 = "El precio es obligatorio.";
            else if (!decimal.TryParse(txtPrecio.Text, out precio) || precio <= 0)
                e2 = "El precio debe ser mayor a $0.";
            AplicarEstadoCampo(txtPrecio, errPrecio, e2);
            if (e2 != null) ok = false;

            return ok;
        }

        private void AplicarEstadoCampo(TextBox campo, TextBlock labelError, string mensaje)
        {
            if (mensaje != null)
            {
                campo.Style = (Style)Resources["InputErrorEstilo"];
                labelError.Text = mensaje;
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
            errNombre.Visibility = Visibility.Collapsed;
            errPrecio.Visibility = Visibility.Collapsed;
            txtNombre.Style = (Style)Resources["InputEstilo"];
            txtPrecio.Style = (Style)Resources["InputEstilo"];
        }

        // ─────────────────────────────────────────────────────
        // ANIMACIONES
        // ─────────────────────────────────────────────────────
        private void AbrirFormulario()
        {
            panelFormulario.Visibility = Visibility.Visible;
            panelFormulario.Opacity = 0;

            var translate = new TranslateTransform { X = 60 };
            panelFormulario.RenderTransform = translate;

            var slide = new DoubleAnimation
            {
                From = 60,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            translate.BeginAnimation(TranslateTransform.XProperty, slide);

            var fade = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(300)) };
            panelFormulario.BeginAnimation(OpacityProperty, fade);
        }

        private void CerrarFormulario()
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
                panelFormulario.Visibility = Visibility.Collapsed;
                LimpiarFormulario();
                LimpiarErrores();
            };
            panelFormulario.BeginAnimation(OpacityProperty, fade);
        }

        // ─────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────
        private void LimpiarFormulario()
        {
            txtNombre.Text = string.Empty;
            txtDescripcion.Text = string.Empty;
            cmbCategoria.Text = string.Empty;
            txtPrecio.Text = string.Empty;
            txtStock.Text = "0";
            txtStockMin.Text = "5";
            txtCantidadAjuste.Text = string.Empty;
            imgFotoFormulario.Source = null;
            lblSinFoto.Visibility = Visibility.Visible;
            _fotoBytes = null;
            panelPreviewPrecio.Visibility = Visibility.Collapsed;
            panelAjusteStock.Visibility = Visibility.Collapsed;
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