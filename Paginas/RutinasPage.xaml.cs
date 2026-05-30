// SistemaGimnacionOptimusCAI/Paginas/RutinasPage.xaml.cs — C# 7.3
using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using SistemaGimnacionOptimusCAI.Ventanas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class RutinasPage : Page
    {
        private readonly RutinaController _controller = new RutinaController();

        private List<Rutina> _todasLasRutinas = new List<Rutina>();
        private Rutina _rutinaActual = null;

        public RutinasPage()
        {
            InitializeComponent();
            CargarRutinas();
        }

        private long UsuarioId => SesionManager.HaySesion ? SesionManager.UsuarioId : 1;

        // ── CARGA ─────────────────────────────────────────────
        private void CargarRutinas()
        {
            try
            {
                _todasLasRutinas = _controller.ObtenerRutinas();
                ActualizarStats();
                RenderizarLista();

                if (_rutinaActual != null)
                {
                    long id = _rutinaActual.Id;
                    _rutinaActual = _controller.ObtenerConDetalle(id);
                    if (_rutinaActual != null) RenderizarDetalle();
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
                statActivas.Text = s.Activas.ToString();
                statEjercicios.Text = s.TotalEjercicios.ToString();
                statSocios.Text = s.SociosAsignados.ToString();
            }
            catch
            {
                statTotal.Text = statActivas.Text =
                    statEjercicios.Text = statSocios.Text = "-";
            }
        }

        // ── LISTA ─────────────────────────────────────────────
        private void RenderizarLista()
        {
            panelRutinas.Children.Clear();
            string buscar = (txtBuscar.Text ?? string.Empty).Trim().ToLower();

            int mostrados = 0;
            foreach (var r in _todasLasRutinas)
            {
                if (buscar.Length > 0)
                {
                    bool match = r.Nombre.ToLower().Contains(buscar) ||
                                 (r.Detalles != null && r.Detalles.ToLower().Contains(buscar));
                    if (!match) continue;
                }

                panelRutinas.Children.Add(CrearCardRutina(r));
                mostrados++;
            }

            panelSinRutinas.Visibility = mostrados == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private Border CrearCardRutina(Rutina r)
        {
            bool seleccionada = _rutinaActual != null && _rutinaActual.Id == r.Id;

            var card = new Border
            {
                Background = new SolidColorBrush(seleccionada
                                    ? Color.FromRgb(22, 32, 22)
                                    : Color.FromRgb(17, 24, 17)),
                BorderBrush = new SolidColorBrush(seleccionada
                                    ? Color.FromRgb(74, 222, 128)
                                    : Color.FromRgb(30, 40, 30)),
                BorderThickness = new Thickness(seleccionada ? 1.5 : 1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand,
                Tag = r.Id
            };
            card.MouseLeftButtonUp += (s, e) => SeleccionarRutina(r.Id);

            if (!seleccionada)
            {
                card.MouseEnter += (s, e) =>
                {
                    if (!(s is Border b) || b.Tag == null) return;
                    b.Background = new SolidColorBrush(Color.FromRgb(26, 36, 26));
                };
                card.MouseLeave += (s, e) =>
                {
                    if (!(s is Border b) || b.Tag == null) return;
                    b.Background = new SolidColorBrush(Color.FromRgb(17, 24, 17));
                };
            }

            var stack = new StackPanel();

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lblNombre = new TextBlock
            {
                Text = r.Nombre,
                FontFamily = new FontFamily("Bahnschrift SemiBold, Segoe UI"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232)),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 36
            };
            Grid.SetColumn(lblNombre, 0);
            grid.Children.Add(lblNombre);

            if (!r.Activo)
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(42, 10, 10)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    VerticalAlignment = VerticalAlignment.Top,
                    Child = new TextBlock
                    {
                        Text = "INACTIVA",
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 102, 102))
                    }
                };
                Grid.SetColumn(badge, 1);
                grid.Children.Add(badge);
            }
            stack.Children.Add(grid);

            stack.Children.Add(new TextBlock
            {
                Text = r.ResumenTexto,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(122, 173, 122)),
                Margin = new Thickness(0, 6, 0, 0)
            });

            stack.Children.Add(new TextBlock
            {
                Text = r.DuracionTexto + "  ·  " + r.AsignacionesTexto,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(61, 92, 61)),
                Margin = new Thickness(0, 2, 0, 0)
            });

            card.Child = stack;
            return card;
        }

        // ── SELECCION ─────────────────────────────────────────
        private void SeleccionarRutina(long id)
        {
            try
            {
                _rutinaActual = _controller.ObtenerConDetalle(id);
                if (_rutinaActual == null) return;
                RenderizarLista();
                RenderizarDetalle();
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void MostrarSinSeleccion()
        {
            _rutinaActual = null;
            panelSinSeleccion.Visibility = Visibility.Visible;
            panelDetalle.Visibility = Visibility.Collapsed;
        }

        // ── DETALLE ───────────────────────────────────────────
        private void RenderizarDetalle()
        {
            if (_rutinaActual == null) { MostrarSinSeleccion(); return; }

            panelSinSeleccion.Visibility = Visibility.Collapsed;
            panelDetalle.Visibility = Visibility.Visible;

            lblDetalleNombre.Text = _rutinaActual.Nombre;
            lblDetalleDuracion.Text = _rutinaActual.DuracionTexto;

            int totalEj = 0;
            foreach (var b in _rutinaActual.Bloques) totalEj += b.Ejercicios.Count;

            string b1 = _rutinaActual.Bloques.Count == 1 ? "1 bloque" : _rutinaActual.Bloques.Count + " bloques";
            string e1 = totalEj == 1 ? "1 ejercicio" : totalEj + " ejercicios";
            lblDetalleResumen.Text = b1 + " · " + e1;

            int asignaciones = _rutinaActual.TotalAsignaciones;
            try
            {
                var asigs = _controller.AsignacionesDeRutina(_rutinaActual.Id);
                asignaciones = asigs.Count;
            }
            catch { }
            lblDetalleAsignaciones.Text = asignaciones == 1
                ? "1 socio asignado"
                : asignaciones + " socios asignados";

            if (string.IsNullOrEmpty(_rutinaActual.Detalles))
            {
                lblDetalleDetalles.Text = string.Empty;
                lblDetalleDetalles.Visibility = Visibility.Collapsed;
            }
            else
            {
                lblDetalleDetalles.Text = _rutinaActual.Detalles;
                lblDetalleDetalles.Visibility = Visibility.Visible;
            }

            panelBloques.Children.Clear();
            if (_rutinaActual.Bloques.Count == 0)
            {
                panelBloques.Children.Add(new TextBlock
                {
                    Text = "Esta rutina no tiene bloques. Agrega el primero abajo.",
                    FontSize = 12,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(61, 92, 61)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 20)
                });
            }
            else
            {
                foreach (var b in _rutinaActual.Bloques)
                    panelBloques.Children.Add(CrearCardBloque(b));
            }
        }

        private Border CrearCardBloque(RutinaBloque b)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(17, 24, 17)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 40, 30)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var stack = new StackPanel();

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(17, 24, 17)),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(14, 10, 10, 10)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerInfo = new StackPanel();
            headerInfo.Children.Add(new TextBlock
            {
                Text = b.Nombre,
                FontFamily = new FontFamily("Bahnschrift SemiBold, Segoe UI"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232))
            });
            headerInfo.Children.Add(new TextBlock
            {
                Text = b.CantidadEjerciciosTexto,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(122, 173, 122)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(headerInfo, 0);
            headerGrid.Children.Add(headerInfo);

            var btnsHeader = new StackPanel { Orientation = Orientation.Horizontal };
            var btnEditBloque = CrearBotonAccion("✏", "ButtonStyleEditar", () => AbrirVentanaEditarBloque(b));
            btnEditBloque.ToolTip = "Editar bloque";
            btnsHeader.Children.Add(btnEditBloque);
            var btnDelBloque = CrearBotonAccion("🗑", "ButtonStyleCancelar", Color.FromRgb(255, 85, 85), () => EliminarBloque(b));
            btnDelBloque.ToolTip = "Eliminar bloque";
            btnsHeader.Children.Add(btnDelBloque);
            Grid.SetColumn(btnsHeader, 1);
            headerGrid.Children.Add(btnsHeader);

            headerBorder.Child = headerGrid;
            stack.Children.Add(headerBorder);

            var ejercStack = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            foreach (var e in b.Ejercicios)
                ejercStack.Children.Add(CrearFilaEjercicio(e));

            var btnAgregar = new Button
            {
                Content = "＋ Agregar ejercicio",
                Style = (Style)FindResource("BotonAgregarEstilo"),
                ToolTip = "Agregar ejercicio"
            };
            btnAgregar.Click += (s, ev) => AbrirVentanaEjercicio(b.Id, null);
            ejercStack.Children.Add(btnAgregar);

            stack.Children.Add(ejercStack);
            card.Child = stack;
            return card;
        }

        private Border CrearFilaEjercicio(RutinaEjercicio e)
        {
            var fila = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 40, 30)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 10, 10, 10),
                Margin = new Thickness(0, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            fila.MouseLeftButtonUp += (s, ev) => AbrirVentanaEjercicio(e.BloqueId, e);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();

            var lineaSuperior = new StackPanel { Orientation = Orientation.Horizontal };
            lineaSuperior.Children.Add(new TextBlock
            {
                Text = e.Nombre,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232))
            });
            if (e.TieneVideo)
            {
                lineaSuperior.Children.Add(new TextBlock
                {
                    Text = "  📹",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 53))
                });
            }
            info.Children.Add(lineaSuperior);

            info.Children.Add(new TextBlock
            {
                Text = e.ResumenCompacto,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118)),
                Margin = new Thickness(0, 3, 0, 0)
            });

            if (!string.IsNullOrEmpty(e.Notas))
            {
                info.Children.Add(new TextBlock
                {
                    Text = e.Notas,
                    FontSize = 10,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(61, 92, 61)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }

            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            if (e.TieneVideo)
            {
                var btnVideo = CrearBotonAccion("▶", "ButtonStyleAccionBase", Color.FromRgb(255, 107, 53), () =>
                {
                    try { Process.Start(e.LinkVideo); }
                    catch { NotificacionWindow.MostrarError("No se pudo abrir el link."); }
                });
                btnVideo.ToolTip = "Ver video";
                Grid.SetColumn(btnVideo, 1);
                grid.Children.Add(btnVideo);
            }

            var btnDel = CrearBotonAccion("✕", "ButtonStyleCancelar", Color.FromRgb(255, 85, 85), () => EliminarEjercicio(e));
            btnDel.ToolTip = "Eliminar ejercicio";
            Grid.SetColumn(btnDel, 2);
            grid.Children.Add(btnDel);

            fila.Child = grid;
            return fila;
        }

        private Button CrearBotonAccion(string texto, string estilo, Action onClick)
        {
            var btn = new Button
            {
                Content = texto,
                Width = 34,
                Height = 34,
                FontSize = 13,
                Cursor = Cursors.Hand,
                Margin = new Thickness(2, 0, 2, 0)
            };
            btn.SetResourceReference(FrameworkElement.StyleProperty, estilo);
            btn.Click += (s, e) => { e.Handled = true; onClick(); };
            return btn;
        }

        private Button CrearBotonAccion(string texto, string estilo, Color foreground, Action onClick)
        {
            var btn = CrearBotonAccion(texto, estilo, onClick);
            btn.Foreground = new SolidColorBrush(foreground);
            return btn;
        }

        // ── BUSQUEDA ──────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => RenderizarLista();

        // ── RUTINA ─────────────────────────────────────────────
        private void btnNuevaRutina_Click(object sender, RoutedEventArgs e)
        {
            var win = new RutinaWindow { Owner = Window.GetWindow(this) };
            win.ModoNuevo();
            if (win.ShowDialog() == true)
            {
                CargarRutinas();
                if (win.NuevoId > 0) SeleccionarRutina(win.NuevoId);
            }
        }

        private void btnEditarRutina_Click(object sender, RoutedEventArgs e)
        {
            if (_rutinaActual == null) return;
            var win = new RutinaWindow { Owner = Window.GetWindow(this) };
            win.ModoEditar(_rutinaActual);
            if (win.ShowDialog() == true)
            {
                CargarRutinas();
                SeleccionarRutina(_rutinaActual.Id);
            }
        }

        private void btnEliminarRutina_Click(object sender, RoutedEventArgs e)
        {
            if (_rutinaActual == null) return;

            bool ok = NotificacionWindow.MostrarConfirmacion(
                "¿Eliminar la rutina \"" + _rutinaActual.Nombre + "\"?\n\n" +
                "Se eliminaran todos sus bloques, ejercicios y asignaciones.",
                "Eliminar rutina");
            if (!ok) return;

            try
            {
                var r = _controller.EliminarRutina(_rutinaActual.Id);
                if (r.ok)
                {
                    NotificacionWindow.MostrarExito(r.mensaje);
                    _rutinaActual = null;
                    CargarRutinas();
                    MostrarSinSeleccion();
                }
                else NotificacionWindow.MostrarError(r.mensaje);
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        // ── BLOQUES ───────────────────────────────────────────
        private void btnAgregarBloque_Click(object sender, RoutedEventArgs e)
        {
            if (_rutinaActual == null) return;
            int ordenSug = _rutinaActual.Bloques.Count + 1;
            var win = new BloqueWindow { Owner = Window.GetWindow(this) };
            win.ModoNuevo(_rutinaActual.Id, ordenSug);
            if (win.ShowDialog() == true)
            {
                SeleccionarRutina(_rutinaActual.Id);
            }
        }

        private void AbrirVentanaEditarBloque(RutinaBloque bloque)
        {
            var win = new BloqueWindow { Owner = Window.GetWindow(this) };
            win.ModoEditar(bloque, _rutinaActual.Id);
            if (win.ShowDialog() == true)
            {
                SeleccionarRutina(_rutinaActual.Id);
            }
        }

        private void EliminarBloque(RutinaBloque b)
        {
            bool ok = NotificacionWindow.MostrarConfirmacion(
                "¿Eliminar el bloque \"" + b.Nombre + "\" y todos sus ejercicios?",
                "Eliminar bloque");
            if (!ok) return;

            var r = _controller.EliminarBloque(b.Id);
            if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); SeleccionarRutina(_rutinaActual.Id); }
            else { NotificacionWindow.MostrarError(r.mensaje); }
        }

        // ── EJERCICIOS ────────────────────────────────────────
        private void AbrirVentanaEjercicio(long bloqueId, RutinaEjercicio ejercicio)
        {
            var win = new EjercicioWindow { Owner = Window.GetWindow(this) };
            if (ejercicio != null)
                win.ModoEditar(ejercicio, bloqueId);
            else
                win.ModoNuevo(bloqueId);

            if (win.ShowDialog() == true)
            {
                SeleccionarRutina(_rutinaActual.Id);
            }
        }

        private void EliminarEjercicio(RutinaEjercicio e)
        {
            bool ok = NotificacionWindow.MostrarConfirmacion(
                "¿Eliminar el ejercicio \"" + e.Nombre + "\"?",
                "Eliminar ejercicio");
            if (!ok) return;

            var r = _controller.EliminarEjercicio(e.Id);
            if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); SeleccionarRutina(_rutinaActual.Id); }
            else { NotificacionWindow.MostrarError(r.mensaje); }
        }

        // ── ASIGNAR ───────────────────────────────────────────
        private void btnAsignar_Click(object sender, RoutedEventArgs e)
        {
            if (_rutinaActual == null) return;
            var win = new AsignarRutinaWindow { Owner = Window.GetWindow(this) };
            win.Configurar(_rutinaActual.Id, _rutinaActual.Nombre);
            if (win.ShowDialog() == true)
            {
                CargarRutinas();
                SeleccionarRutina(_rutinaActual.Id);
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