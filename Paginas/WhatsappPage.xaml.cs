// SistemaGimnacionOptimusCAI/Paginas/WhatsappPage.xaml.cs — C# 7.3
using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using SistemaGimnacionOptimusCAI.Ventanas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class WhatsappPage : Page
    {
        private readonly WhatsappController _controller = new WhatsappController();

        private List<WhatsappMensaje> _todos = new List<WhatsappMensaje>();
        private WhatsappMensaje _mensajeActual = null;
        private string _filtroEstado = "todos";

        public WhatsappPage()
        {
            InitializeComponent();
            ResaltarChip(chipTodos);
            CargarMensajes();
        }

        private long UsuarioId => SesionManager.HaySesion ? SesionManager.UsuarioId : 1;

        // ── CARGA ─────────────────────────────────────────────
        private void CargarMensajes()
        {
            try
            {
                _todos = _controller.Buscar(txtBuscar.Text, _filtroEstado, "todos");
                ActualizarStats();
                RenderizarLista();

                if (_mensajeActual != null)
                {
                    _mensajeActual = _controller.ObtenerPorId(_mensajeActual.Id);
                    if (_mensajeActual != null) RenderizarDetalle();
                    else MostrarSinSeleccion();
                }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void ActualizarStats()
        {
            try
            {
                var s = _controller.ObtenerEstadisticas();
                statTotal.Text = s.Total.ToString();
                statPendientes.Text = s.Pendientes.ToString();
                statEnviados.Text = s.Enviados.ToString();
                statEnviadosHoy.Text = s.EnviadosHoy.ToString();
            }
            catch
            {
                statTotal.Text = statPendientes.Text =
                    statEnviados.Text = statEnviadosHoy.Text = "-";
            }
        }

        // ── LISTA ─────────────────────────────────────────────
        private void RenderizarLista()
        {
            panelMensajes.Children.Clear();

            if (_todos.Count == 0)
            {
                panelSinMensajes.Visibility = Visibility.Visible;
                return;
            }

            panelSinMensajes.Visibility = Visibility.Collapsed;
            foreach (var m in _todos) panelMensajes.Children.Add(CrearCardMensaje(m));
        }

        private Border CrearCardMensaje(WhatsappMensaje m)
        {
            bool seleccionado = _mensajeActual != null && _mensajeActual.Id == m.Id;

            Color colorEstado = m.EsEnviado ? Color.FromRgb(37, 211, 102)
                              : m.EsError ? Color.FromRgb(255, 85, 85)
                              : Color.FromRgb(255, 167, 38);

            var card = new Border
            {
                Background = new SolidColorBrush(seleccionado
                                    ? Color.FromRgb(26, 32, 28)
                                    : Color.FromRgb(18, 26, 20)),
                BorderBrush = new SolidColorBrush(seleccionado
                                    ? Color.FromRgb(37, 211, 102)
                                    : Color.FromRgb(30, 52, 36)),
                BorderThickness = new Thickness(seleccionado ? 1.5 : 1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand,
                Tag = m.Id
            };
            card.MouseLeftButtonUp += (s, e) => Seleccionar(m.Id);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var bar = new Border
            {
                Background = new SolidColorBrush(colorEstado),
                CornerRadius = new CornerRadius(10, 0, 0, 10)
            };
            Grid.SetColumn(bar, 0);
            grid.Children.Add(bar);

            var contenido = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };

            var linea1 = new Grid();
            linea1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linea1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nombreEstado = new StackPanel { Orientation = Orientation.Horizontal };
            nombreEstado.Children.Add(new TextBlock
            {
                Text = m.SocioNombre,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 230
            });
            Grid.SetColumn(nombreEstado, 0);
            linea1.Children.Add(nombreEstado);

            var lblHora = new TextBlock
            {
                Text = m.FechaCreado,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(90, 122, 90)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lblHora, 1);
            linea1.Children.Add(lblHora);
            contenido.Children.Add(linea1);

            contenido.Children.Add(new TextBlock
            {
                Text = "📞 " + m.Telefono,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(colorEstado),
                Margin = new Thickness(0, 3, 0, 0)
            });

            contenido.Children.Add(new TextBlock
            {
                Text = m.MensajePreview,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(138, 170, 138)),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 32,
                Margin = new Thickness(0, 5, 0, 0)
            });

            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(
                                        (byte)(colorEstado.R * 0.2),
                                        (byte)(colorEstado.G * 0.2),
                                        (byte)(colorEstado.B * 0.2))),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 6, 0, 0),
                Child = new TextBlock
                {
                    Text = m.EstadoTexto.ToUpper(),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(colorEstado)
                }
            };
            contenido.Children.Add(badge);

            Grid.SetColumn(contenido, 1);
            grid.Children.Add(contenido);
            card.Child = grid;
            return card;
        }

        // ── SELECCION ─────────────────────────────────────────
        private void Seleccionar(long id)
        {
            try
            {
                _mensajeActual = _controller.ObtenerPorId(id);
                if (_mensajeActual == null) return;
                RenderizarLista();
                RenderizarDetalle();
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void MostrarSinSeleccion()
        {
            _mensajeActual = null;
            panelSinSeleccion.Visibility = Visibility.Visible;
            panelDetalle.Visibility = Visibility.Collapsed;
        }

        // ── DETALLE ───────────────────────────────────────────
        private void RenderizarDetalle()
        {
            if (_mensajeActual == null) { MostrarSinSeleccion(); return; }

            panelSinSeleccion.Visibility = Visibility.Collapsed;
            panelDetalle.Visibility = Visibility.Visible;

            lblDetalleNombre.Text = _mensajeActual.SocioNombre;
            lblDetalleTelefono.Text = "📞 " + _mensajeActual.Telefono;
            lblDetalleNumeroSocio.Text = _mensajeActual.NumeroSocioTexto;
            lblDetalleDisparador.Text = "Tipo: " + _mensajeActual.TipoTexto +
                                          "  ·  " + _mensajeActual.DisparadorTexto;
            lblDetalleMensaje.Text = _mensajeActual.Mensaje;
            lblDetalleFechaCreado.Text = "Creado: " + _mensajeActual.FechaCreado;
            if (_mensajeActual.EsEnviado)
                lblDetalleFechaCreado.Text += "  ·  Enviado: " + _mensajeActual.FechaEnviado;

            if (_mensajeActual.SocioFoto != null && _mensajeActual.SocioFoto.Length > 0)
                imgDetalleFoto.ImageSource = BytesABitmapImage(_mensajeActual.SocioFoto);
            else
                imgDetalleFoto.ImageSource = null;

            Color colorBadge;
            Color colorBadgeBg;
            if (_mensajeActual.EsEnviado)
            {
                colorBadge = Color.FromRgb(37, 211, 102);
                colorBadgeBg = Color.FromRgb(10, 42, 20);
            }
            else if (_mensajeActual.EsError)
            {
                colorBadge = Color.FromRgb(255, 85, 85);
                colorBadgeBg = Color.FromRgb(42, 10, 10);
            }
            else
            {
                colorBadge = Color.FromRgb(255, 167, 38);
                colorBadgeBg = Color.FromRgb(42, 31, 0);
            }

            badgeEstado.Background = new SolidColorBrush(colorBadgeBg);
            lblBadgeEstado.Text = _mensajeActual.EstadoTexto.ToUpper();
            lblBadgeEstado.Foreground = new SolidColorBrush(colorBadge);

            btnMarcarEnviado.IsEnabled = !_mensajeActual.EsEnviado;
            btnMarcarEnviado.Opacity = _mensajeActual.EsEnviado ? 0.4 : 1.0;
        }

        // ── BUSQUEDA + FILTROS ────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => CargarMensajes();

        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            _filtroEstado = btn.Tag.ToString();
            ResaltarChip(btn);
            CargarMensajes();
        }

        private void ResaltarChip(Button sel)
        {
            Button[] chips = { chipTodos, chipPendientes, chipEnviados, chipErrores };
            foreach (var c in chips)
            {
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

        // ── ACCIONES SOBRE MENSAJE ────────────────────────────
        private void btnMarcarEnviado_Click(object sender, RoutedEventArgs e)
        {
            if (_mensajeActual == null) return;
            try
            {
                var r = _controller.MarcarComoEnviado(_mensajeActual.Id, UsuarioId);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarMensajes(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void btnAbrirWhatsapp_Click(object sender, RoutedEventArgs e)
        {
            if (_mensajeActual == null) return;
            try
            {
                string url = _controller.ConstruirUrlWhatsapp(
                    _mensajeActual.Telefono, _mensajeActual.Mensaje);
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo abrir WhatsApp Web.\n" + ex.Message);
            }
        }

        private void btnEliminarMensaje_Click(object sender, RoutedEventArgs e)
        {
            if (_mensajeActual == null) return;

            bool ok = NotificacionWindow.MostrarConfirmacion(
                "¿Eliminar este mensaje?\n\nEsta accion no se puede deshacer.",
                "Eliminar mensaje");
            if (!ok) return;

            try
            {
                var r = _controller.Eliminar(_mensajeActual.Id);
                if (r.ok)
                {
                    NotificacionWindow.MostrarExito(r.mensaje);
                    _mensajeActual = null;
                    CargarMensajes();
                    MostrarSinSeleccion();
                }
                else NotificacionWindow.MostrarError(r.mensaje);
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        // ── VENTANAS EMERGENTES ──────────────────────────────
        private void btnNuevoMensaje_Click(object sender, RoutedEventArgs e)
        {
            var win = new NuevoMensajeWindow();
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true)
            {
                CargarMensajes();
            }
        }

        private void btnGenerarAvisos_Click(object sender, RoutedEventArgs e)
        {
            var win = new GenerarAvisosWindow();
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true && win.MensajesGenerados > 0)
            {
                _filtroEstado = "pendiente";
                ResaltarChip(chipPendientes);
                CargarMensajes();
            }
        }

        // ── HELPERS ───────────────────────────────────────────
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