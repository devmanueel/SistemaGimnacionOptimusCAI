// SistemaGimnacionOptimusCAI/Paginas/VentasPage.xaml.cs — C# 7.3
using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class VentasPage : Page
    {
        private readonly VentaController _controller = new VentaController();

        private List<ItemCarrito> _carrito = new List<ItemCarrito>();
        private List<Producto> _productos = new List<Producto>();
        private string _filtroMetodo = "todos";

        private long USUARIO_ACTUAL_ID => SesionManager.UsuarioId;

        public VentasPage()
        {
            InitializeComponent();
            ResaltarChip(chipTodos);
            CargarVentas();
            CargarComboSocios();
            if (SesionManager.AbrirPanelAlNavegar)
            {
                SesionManager.AbrirPanelAlNavegar = false;
                btnNuevaVenta_Click(null, null);
            }
        }

        // ── CARGA ─────────────────────────────────────────────
        private void CargarVentas()
        {
            try
            {
                var lista = _controller.BuscarVentas(
                    txtBuscar.Text, _filtroMetodo,
                    DateTime.Today.AddDays(-30), DateTime.Today);
                gridVentas.ItemsSource = lista;
                ActualizarStats();
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void ActualizarStats()
        {
            try
            {
                var s = _controller.ObtenerEstadisticas();
                statVentasHoy.Text = s.VentasHoy.ToString();
                statTotalHoy.Text = s.TotalHoyTexto;
                statVentasMes.Text = s.VentasMes.ToString();
                statTotalMes.Text = s.TotalMesTexto;
            }
            catch
            {
                statVentasHoy.Text = statTotalHoy.Text =
                    statVentasMes.Text = statTotalMes.Text = "-";
            }
        }

        private void CargarComboSocios()
        {
            try { cmbSocio.ItemsSource = _controller.ListarSociosParaCombo(); }
            catch { }
        }

        private void CargarCatalogo()
        {
            try
            {
                _productos = _controller.ListarProductosParaVenta();
                RenderizarCatalogo();
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void RenderizarCatalogo()
        {
            panelCatalogo.Children.Clear();
            string buscar = (txtBuscarProducto.Text ?? string.Empty).Trim().ToLower();
            int mostrados = 0;

            foreach (var p in _productos)
            {
                if (p.Stock <= 0) continue;
                if (buscar.Length > 0 && !p.Nombre.ToLower().Contains(buscar)) continue;
                panelCatalogo.Children.Add(CrearCardCatalogo(p));
                mostrados++;
            }

            panelSinProductos.Visibility = mostrados == 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private Border CrearCardCatalogo(Producto p)
        {
            var card = new Border
            {
                Width = 140,
                Height = 180,
                Margin = new Thickness(6),
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(37, 37, 64)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Cursor = System.Windows.Input.Cursors.Hand,
                ClipToBounds = true,
                Tag = p.Id
            };
            card.MouseLeftButtonUp += (s, e) => AgregarAlCarrito(p);

            var stack = new StackPanel();

            var cont = new Border
            {
                Height = 90,
                Background = new SolidColorBrush(Color.FromRgb(10, 10, 25))
            };
            var g = new Grid();
            if (p.Foto != null && p.Foto.Length > 0)
                g.Children.Add(new Image { Source = BytesABitmapImage(p.Foto), Stretch = Stretch.UniformToFill });
            else
                g.Children.Add(new TextBlock
                {
                    Text = ObtenerEmoji(p.Categoria),
                    FontSize = 34,
                    Opacity = 0.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            cont.Child = g;
            stack.Children.Add(cont);

            var info = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };
            info.Children.Add(new TextBlock
            {
                Text = p.Nombre,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255)),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 34,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var gf = new Grid();
            gf.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gf.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lp = new TextBlock
            {
                Text = p.PrecioTexto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118))
            };
            Grid.SetColumn(lp, 0);

            var ls = new TextBlock
            {
                Text = p.Stock + "u",
                FontSize = 10,
                Foreground = p.BajoStock
                    ? new SolidColorBrush(Color.FromRgb(255, 167, 38))
                    : new SolidColorBrush(Color.FromRgb(106, 106, 154)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(ls, 1);

            gf.Children.Add(lp); gf.Children.Add(ls);
            info.Children.Add(gf);
            stack.Children.Add(info);
            card.Child = stack;
            return card;
        }

        // ── CARRITO ───────────────────────────────────────────
        private void AgregarAlCarrito(Producto p)
        {
            ItemCarrito existente = null;
            foreach (var i in _carrito)
                if (i.ProductoId == p.Id) { existente = i; break; }

            if (existente != null)
            {
                if (existente.StockAlLimite)
                {
                    NotificacionWindow.MostrarAdvertencia("Stock maximo para " + p.Nombre + ".");
                    return;
                }
                existente.Cantidad++;
            }
            else
            {
                _carrito.Add(new ItemCarrito
                {
                    ProductoId = p.Id,
                    Nombre = p.Nombre,
                    Foto = p.Foto,
                    PrecioUnitario = p.Precio,
                    Cantidad = 1,
                    StockDisponible = p.Stock
                });
            }
            RenderizarCarrito();
        }

        private void RenderizarCarrito()
        {
            panelCarrito.Children.Clear();
            panelCarritoVacio.Visibility = _carrito.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            decimal total = 0;
            int cantItems = 0;

            foreach (var item in _carrito)
            {
                total += item.Subtotal;
                cantItems += item.Cantidad;
                panelCarrito.Children.Add(CrearFilaCarrito(item));
            }

            lblCantidadItems.Text = cantItems + " item" + (cantItems != 1 ? "s" : "");
            lblTotal.Text = "$" + total.ToString("N0");
        }

        private Border CrearFilaCarrito(ItemCarrito item)
        {
            var fila = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(37, 37, 64)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 10, 0, 10)
            };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            info.Children.Add(new TextBlock
            {
                Text = item.Nombre,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255)),
                TextWrapping = TextWrapping.Wrap
            });
            info.Children.Add(new TextBlock
            {
                Text = item.PrecioTexto + " x " + item.Cantidad + " = " + item.SubtotalTexto,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118))
            });
            Grid.SetColumn(info, 0);

            var btns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            btns.Children.Add(CrearBtnCantidad("－", item, false));
            btns.Children.Add(new TextBlock
            {
                Text = item.Cantidad.ToString(),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255)),
                Width = 30,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            btns.Children.Add(CrearBtnCantidad("＋", item, true));
            Grid.SetColumn(btns, 1);

            g.Children.Add(info); g.Children.Add(btns);
            fila.Child = g;
            return fila;
        }

        private Button CrearBtnCantidad(string texto, ItemCarrito item, bool sumar)
        {
            var btn = new Button
            {
                Content = texto,
                Width = 28,
                Height = 28,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 56)),
                Foreground = sumar
                    ? new SolidColorBrush(Color.FromRgb(0, 230, 118))
                    : new SolidColorBrush(Color.FromRgb(255, 107, 53)),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Click += (s, e) =>
            {
                if (sumar)
                {
                    if (item.StockAlLimite)
                    { NotificacionWindow.MostrarAdvertencia("Stock maximo."); return; }
                    item.Cantidad++;
                }
                else
                {
                    item.Cantidad--;
                    if (item.Cantidad <= 0) _carrito.Remove(item);
                }
                RenderizarCarrito();
            };
            return btn;
        }

        // ── FILTROS ───────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => CargarVentas();
        private void txtBuscarProducto_TextChanged(object sender, TextChangedEventArgs e) => RenderizarCatalogo();

        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            _filtroMetodo = btn.Tag.ToString();
            ResaltarChip(btn);
            CargarVentas();
        }

        private void ResaltarChip(Button sel)
        {
            Button[] chips = { chipTodos, chipEfectivo, chipTransfer, chipTarjeta };
            foreach (var c in chips)
            {
                // Al asignar el estilo completo, recuperás el espaciado y los efectos automáticos
                if (c == sel)
                {
                    c.Style = (Style)FindResource("BotonChipActivoEstilo");
                }
                else
                {
                    c.Style = (Style)FindResource("BotonChipEstilo");
                }
            }
        }

        // ── NAVEGACIÓN ────────────────────────────────────────
        private void btnNuevaVenta_Click(object sender, RoutedEventArgs e)
        {
            _carrito.Clear();
            cmbSocio.SelectedIndex = -1;
            cmbMetodoPago.SelectedIndex = 0;
            txtBuscarProducto.Text = string.Empty;
            RenderizarCarrito();
            CargarCatalogo();
            Fade(vistaListado, vistaPOS);
        }

        private void btnVolver_Click(object sender, RoutedEventArgs e)
            => Fade(vistaPOS, vistaListado);

        private void Fade(UIElement ocultar, UIElement mostrar)
        {
            var out_ = new DoubleAnimation { From = 1, To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(160)) };
            out_.Completed += (s, ev) =>
            {
                ocultar.Visibility = Visibility.Collapsed;
                mostrar.Visibility = Visibility.Visible;
                mostrar.Opacity = 0;
                var in_ = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(200)) };
                mostrar.BeginAnimation(OpacityProperty, in_);
            };
            ocultar.BeginAnimation(OpacityProperty, out_);
        }

        // ── COBRAR ────────────────────────────────────────────
        private void btnCobrar_Click(object sender, RoutedEventArgs e)
        {
            if (_carrito.Count == 0)
            { NotificacionWindow.MostrarAdvertencia("El carrito esta vacio."); return; }

            var socio = cmbSocio.SelectedItem as SocioComboItem;
            var metodoItem = cmbMetodoPago.SelectedItem as ComboBoxItem;
            string metodo = metodoItem?.Tag?.ToString() ?? "efectivo";

            var r = _controller.RegistrarVenta(
                USUARIO_ACTUAL_ID, socio?.Id, metodo, null, _carrito);

            if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }

            MostrarTicket(r.nuevoId);
            _carrito.Clear();
            RenderizarCarrito();
            CargarCatalogo();
        }

        // ── TICKET ────────────────────────────────────────────
        private void MostrarTicket(long ventaId)
        {
            var venta = _controller.ObtenerPorId(ventaId);
            if (venta == null) return;

            lblTicketNumero.Text = "Venta " + venta.NumeroVenta;
            lblTicketTotal.Text = venta.TotalTexto;
            panelTicket.Children.Clear();

            panelTicket.Children.Add(FilaTicket("PRODUCTO", "CANT.", "SUBTOTAL", true));
            foreach (var item in venta.Items)
                panelTicket.Children.Add(FilaTicket(
                    item.ProductoNombre ?? item.Descripcion,
                    item.Cantidad.ToString(),
                    item.SubtotalTexto, false));

            panelTicket.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 64)),
                Margin = new Thickness(0, 8, 0, 8)
            });
            panelTicket.Children.Add(new TextBlock
            {
                Text = "Metodo: " + venta.MetodoPagoTexto,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154))
            });

            modalTicket.Visibility = Visibility.Visible;
            modalTicket.Opacity = 0;
            var fade = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(200)) };
            modalTicket.BeginAnimation(OpacityProperty, fade);
        }

        private Grid FilaTicket(string c1, string c2, string c3, bool header)
        {
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Color color = header ? Color.FromRgb(106, 106, 154) : Color.FromRgb(200, 200, 220);

            TextBlock Tb(string t, int col, TextAlignment align = TextAlignment.Left)
            {
                var tb = new TextBlock
                {
                    Text = t,
                    FontSize = header ? 10 : 12,
                    FontWeight = header ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(color),
                    TextAlignment = align
                };
                Grid.SetColumn(tb, col);
                return tb;
            }

            g.Children.Add(Tb(c1, 0));
            g.Children.Add(Tb(c2, 1, TextAlignment.Center));
            g.Children.Add(Tb(c3, 2, TextAlignment.Right));
            return g;
        }

        private void btnCerrarTicket_Click(object sender, RoutedEventArgs e)
        {
            var fade = new DoubleAnimation { From = 1, To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(180)) };
            fade.Completed += (s, ev) =>
            {
                modalTicket.Visibility = Visibility.Collapsed;
                Fade(vistaPOS, vistaListado);
                CargarVentas();
            };
            modalTicket.BeginAnimation(OpacityProperty, fade);
        }

        // ── DATAGRID ACCIONES ─────────────────────────────────
        private void btnVerDetalle_Click(object sender, RoutedEventArgs e)
        {
            var v = (sender as Button)?.DataContext as Venta;
            if (v == null) return;
            MostrarTicket(v.Id);
        }

        private void btnAnular_Click(object sender, RoutedEventArgs e)
        {
            var v = (sender as Button)?.DataContext as Venta;
            if (v == null) return;

            bool ok = NotificacionWindow.MostrarConfirmacion(
                "Anular venta " + v.NumeroVenta + " de " + v.TotalTexto + "?\n\n" +
                "Se repondra el stock y se eliminara el movimiento de caja.",
                "Anular venta");
            if (!ok) return;

            try
            {
                var r = _controller.AnularVenta(v.Id);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarVentas(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        // ── HELPERS ───────────────────────────────────────────
        private string ObtenerEmoji(string cat)
        {
            if (string.IsNullOrEmpty(cat)) return "📦";
            string c = cat.ToLower();
            if (c.Contains("bebida")) return "🥤";
            if (c.Contains("supl")) return "💊";
            if (c.Contains("snack")) return "🍫";
            if (c.Contains("ropa")) return "👕";
            if (c.Contains("acces")) return "🎒";
            if (c.Contains("higien")) return "🧴";
            return "📦";
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