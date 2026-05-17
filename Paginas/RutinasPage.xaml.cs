// SistemaGimnacionOptimusCAI/Paginas/RutinasPage.xaml.cs — C# 7.3
using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class RutinasPage : Page
    {
        private readonly RutinaController _controller = new RutinaController();

        private List<Rutina> _todasLasRutinas = new List<Rutina>();
        private Rutina _rutinaActual = null;

        // Estado de los modales
        private bool _editandoRutina = false;
        private bool _editandoBloque = false;
        private long _bloqueIdEditar = 0;
        private long _ejercicioIdEditar = 0;
        private long _bloqueIdParaEjercicio = 0;
        private bool _editandoEjercicio = false;

        public RutinasPage()
        {
            InitializeComponent();
            CargarRutinas();
            CargarComboSocios();
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

                // Si había una rutina seleccionada, recargarla
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
                    statEjercicios.Text = statSocios.Text = "—";
            }
        }

        private void CargarComboSocios()
        {
            try
            {
                cmbAsignarSocio.ItemsSource = _controller.ListarSociosParaCombo();
            }
            catch { }
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
                                    ? Color.FromRgb(26, 24, 64)
                                    : Color.FromRgb(22, 22, 42)),
                BorderBrush = new SolidColorBrush(seleccionada
                                    ? Color.FromRgb(167, 139, 250)
                                    : Color.FromRgb(37, 37, 64)),
                BorderThickness = new Thickness(seleccionada ? 1.5 : 1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand,
                Tag = r.Id
            };
            card.MouseLeftButtonUp += (s, e) => SeleccionarRutina(r.Id);

            var stack = new StackPanel();

            // Header con nombre + estado
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lblNombre = new TextBlock
            {
                Text = r.Nombre,
                FontFamily = new FontFamily("Bahnschrift SemiBold, Segoe UI"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255)),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 36
            };
            Grid.SetColumn(lblNombre, 0);
            grid.Children.Add(lblNombre);

            // Badge inactivo si corresponde
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

            // Resumen
            stack.Children.Add(new TextBlock
            {
                Text = r.ResumenTexto,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
                Margin = new Thickness(0, 6, 0, 0)
            });

            // Asignaciones + duracion
            stack.Children.Add(new TextBlock
            {
                Text = r.DuracionTexto + "  ·  " + r.AsignacionesTexto,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154)),
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
                RenderizarLista();   // refresca highlight
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
            // Si vino del SP de detalle, no trae el contador, recalcular
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

            // Renderizar bloques
            panelBloques.Children.Clear();
            if (_rutinaActual.Bloques.Count == 0)
            {
                panelBloques.Children.Add(new TextBlock
                {
                    Text = "Esta rutina no tiene bloques. Agrega el primero abajo.",
                    FontSize = 12,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154)),
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
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(37, 37, 64)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var stack = new StackPanel();

            // Header bloque
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 24, 64)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255))
            });
            headerInfo.Children.Add(new TextBlock
            {
                Text = b.CantidadEjerciciosTexto,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(headerInfo, 0);
            headerGrid.Children.Add(headerInfo);

            var btnsHeader = new StackPanel { Orientation = Orientation.Horizontal };
            btnsHeader.Children.Add(CrearBotonMini("✏", Color.FromRgb(167, 139, 250), () => AbrirPanelBloque(b)));
            btnsHeader.Children.Add(CrearBotonMini("🗑", Color.FromRgb(255, 85, 85), () => EliminarBloque(b)));
            Grid.SetColumn(btnsHeader, 1);
            headerGrid.Children.Add(btnsHeader);

            headerBorder.Child = headerGrid;
            stack.Children.Add(headerBorder);

            // Lista de ejercicios
            var ejercStack = new StackPanel { Margin = new Thickness(0) };
            foreach (var e in b.Ejercicios)
                ejercStack.Children.Add(CrearFilaEjercicio(e));

            // Boton agregar ejercicio
            var btnAgregar = new Button
            {
                Content = "＋ Agregar ejercicio",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 53)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(14, 10, 14, 12),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            btnAgregar.Click += (s, ev) => AbrirPanelEjercicio(b.Id, null);
            ejercStack.Children.Add(btnAgregar);

            stack.Children.Add(ejercStack);
            card.Child = stack;
            return card;
        }

        private Border CrearFilaEjercicio(RutinaEjercicio e)
        {
            var fila = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(26, 26, 46)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 10, 10, 10),
                Cursor = Cursors.Hand
            };
            fila.MouseLeftButtonUp += (s, ev) => AbrirPanelEjercicio(e.BloqueId, e);

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
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255))
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
                    Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }

            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            // Boton ver video (si tiene)
            if (e.TieneVideo)
            {
                var btnVideo = CrearBotonMini("▶", Color.FromRgb(255, 107, 53), () =>
                {
                    try { Process.Start(e.LinkVideo); }
                    catch { NotificacionWindow.MostrarError("No se pudo abrir el link."); }
                });
                Grid.SetColumn(btnVideo, 1);
                grid.Children.Add(btnVideo);
            }

            // Boton eliminar
            var btnDel = CrearBotonMini("✕", Color.FromRgb(255, 85, 85), () => EliminarEjercicio(e));
            Grid.SetColumn(btnDel, 2);
            grid.Children.Add(btnDel);

            fila.Child = grid;
            return fila;
        }

        private Button CrearBotonMini(string texto, Color color, Action onClick)
        {
            var btn = new Button
            {
                Content = texto,
                Width = 30,
                Height = 30,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(color),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 13,
                Margin = new Thickness(2, 0, 2, 0)
            };
            btn.Click += (s, e) => { e.Handled = true; onClick(); };
            return btn;
        }

        // ── BUSQUEDA ──────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => RenderizarLista();

        // ── BOTONES PRINCIPALES ───────────────────────────────
        private void btnNuevaRutina_Click(object sender, RoutedEventArgs e)
        {
            _editandoRutina = false;
            lblTituloPanelRutina.Text = "NUEVA RUTINA";
            txtRutNombre.Text = string.Empty;
            txtRutDetalles.Text = string.Empty;
            txtRutSemanas.Text = "4";
            AbrirPanel(panelRutina);
        }

        private void btnEditarRutina_Click(object sender, RoutedEventArgs e)
        {
            if (_rutinaActual == null) return;
            _editandoRutina = true;
            lblTituloPanelRutina.Text = "EDITAR RUTINA";
            txtRutNombre.Text = _rutinaActual.Nombre;
            txtRutDetalles.Text = _rutinaActual.Detalles ?? string.Empty;
            txtRutSemanas.Text = _rutinaActual.DuracionSemanas.ToString();
            AbrirPanel(panelRutina);
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

        // ── MODAL RUTINA ──────────────────────────────────────
        private void btnRutGuardar_Click(object sender, RoutedEventArgs e)
        {
            byte sem = 4;
            byte.TryParse(txtRutSemanas.Text, out sem);

            if (_editandoRutina)
            {
                var r = _controller.ModificarRutina(_rutinaActual.Id,
                    txtRutNombre.Text, txtRutDetalles.Text, sem);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje);
                CerrarPanel(panelRutina);
                CargarRutinas();
            }
            else
            {
                var r = _controller.InsertarRutina(
                    txtRutNombre.Text, txtRutDetalles.Text, sem, UsuarioId);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje);
                CerrarPanel(panelRutina);

                _todasLasRutinas = _controller.ObtenerRutinas();
                ActualizarStats();
                SeleccionarRutina(r.nuevoId);
            }
        }

        private void btnRutCancelar_Click(object sender, RoutedEventArgs e) => CerrarPanel(panelRutina);

        // ── BLOQUES ───────────────────────────────────────────
        private void btnAgregarBloque_Click(object sender, RoutedEventArgs e)
        {
            if (_rutinaActual == null) return;
            AbrirPanelBloque(null);
        }

        private void AbrirPanelBloque(RutinaBloque bloque)
        {
            _editandoBloque = bloque != null;
            _bloqueIdEditar = bloque != null ? bloque.Id : 0;

            lblTituloPanelBloque.Text = _editandoBloque ? "EDITAR BLOQUE" : "NUEVO BLOQUE";
            txtBlqNombre.Text = bloque != null ? bloque.Nombre : string.Empty;
            txtBlqOrden.Text = bloque != null ? bloque.Orden.ToString()
                                               : (_rutinaActual.Bloques.Count + 1).ToString();
            AbrirPanel(panelBloque);
        }

        private void btnBlqGuardar_Click(object sender, RoutedEventArgs e)
        {
            byte orden = 1;
            byte.TryParse(txtBlqOrden.Text, out orden);

            if (_editandoBloque)
            {
                var r = _controller.ModificarBloque(_bloqueIdEditar, txtBlqNombre.Text, orden);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
            }
            else
            {
                var r = _controller.InsertarBloque(_rutinaActual.Id, txtBlqNombre.Text, orden);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
            }

            CerrarPanel(panelBloque);
            SeleccionarRutina(_rutinaActual.Id);
        }

        private void btnBlqCancelar_Click(object sender, RoutedEventArgs e) => CerrarPanel(panelBloque);

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
        private void AbrirPanelEjercicio(long bloqueId, RutinaEjercicio ej)
        {
            _editandoEjercicio = ej != null;
            _ejercicioIdEditar = ej != null ? ej.Id : 0;
            _bloqueIdParaEjercicio = bloqueId;

            lblTituloPanelEj.Text = _editandoEjercicio ? "EDITAR EJERCICIO" : "NUEVO EJERCICIO";

            txtEjNombre.Text = ej != null ? ej.Nombre : string.Empty;
            txtEjSeries.Text = ej != null && ej.Series.HasValue ? ej.Series.Value.ToString() : string.Empty;
            txtEjReps.Text = ej != null ? (ej.Repeticiones ?? string.Empty) : string.Empty;
            txtEjPeso.Text = ej != null ? (ej.Peso ?? string.Empty) : string.Empty;
            txtEjDescanso.Text = ej != null && ej.DescansoSeg.HasValue ? ej.DescansoSeg.Value.ToString() : string.Empty;
            txtEjVideo.Text = ej != null ? (ej.LinkVideo ?? string.Empty) : string.Empty;
            txtEjNotas.Text = ej != null ? (ej.Notas ?? string.Empty) : string.Empty;
            txtEjOrden.Text = ej != null ? ej.Orden.ToString() : "1";

            AbrirPanel(panelEjercicio);
        }

        private void btnEjGuardar_Click(object sender, RoutedEventArgs e)
        {
            byte? series = null;
            byte sTmp;
            if (byte.TryParse(txtEjSeries.Text, out sTmp)) series = sTmp;

            short? descanso = null;
            short dTmp;
            if (short.TryParse(txtEjDescanso.Text, out dTmp)) descanso = dTmp;

            byte orden = 1;
            byte.TryParse(txtEjOrden.Text, out orden);

            var ej = new RutinaEjercicio
            {
                Id = _ejercicioIdEditar,
                BloqueId = _bloqueIdParaEjercicio,
                Nombre = txtEjNombre.Text,
                Series = series,
                Repeticiones = txtEjReps.Text,
                Peso = txtEjPeso.Text,
                DescansoSeg = descanso,
                LinkVideo = txtEjVideo.Text,
                Notas = txtEjNotas.Text,
                Orden = orden
            };

            if (_editandoEjercicio)
            {
                var r = _controller.ModificarEjercicio(ej);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
            }
            else
            {
                var r = _controller.InsertarEjercicio(ej);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
            }

            CerrarPanel(panelEjercicio);
            SeleccionarRutina(_rutinaActual.Id);
        }

        private void btnEjCancelar_Click(object sender, RoutedEventArgs e) => CerrarPanel(panelEjercicio);

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
            lblAsignarRutina.Text = _rutinaActual.Nombre;
            cmbAsignarSocio.SelectedIndex = -1;
            CargarAsignacionesActuales();
            AbrirModal(modalAsignar);
        }

        private void CargarAsignacionesActuales()
        {
            panelAsignaciones.Children.Clear();
            try
            {
                var lista = _controller.AsignacionesDeRutina(_rutinaActual.Id);
                if (lista.Count == 0)
                {
                    panelAsignaciones.Children.Add(new TextBlock
                    {
                        Text = "Esta rutina no tiene socios asignados.",
                        FontSize = 11,
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 12, 0, 12)
                    });
                    return;
                }

                foreach (var a in lista)
                    panelAsignaciones.Children.Add(CrearFilaAsignacion(a));
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private Border CrearFilaAsignacion(RutinaAsignacion a)
        {
            var fila = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(37, 37, 64)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 8, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Foto
            var fotoCont = new Grid { Margin = new Thickness(0, 0, 10, 0) };
            var ringEll = new System.Windows.Shapes.Ellipse
            {
                Width = 36,
                Height = 36,
                Fill = new LinearGradientBrush(
                    Color.FromRgb(0, 207, 255),
                    Color.FromRgb(167, 139, 250),
                    new Point(0, 0), new Point(1, 1))
            };
            fotoCont.Children.Add(ringEll);

            var innerEll = new System.Windows.Shapes.Ellipse { Width = 32, Height = 32 };
            if (a.SocioFoto != null && a.SocioFoto.Length > 0)
                innerEll.Fill = new ImageBrush(BytesABitmapImage(a.SocioFoto)) { Stretch = Stretch.UniformToFill };
            else
                innerEll.Fill = new SolidColorBrush(Color.FromRgb(40, 40, 60));
            fotoCont.Children.Add(innerEll);
            Grid.SetColumn(fotoCont, 0);
            grid.Children.Add(fotoCont);

            // Nombre + nro
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = a.SocioNombre,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255))
            });
            info.Children.Add(new TextBlock
            {
                Text = a.NumeroSocioTexto + "  ·  asignada el " + a.FechaTexto,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154))
            });
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            var btnDel = CrearBotonMini("✕", Color.FromRgb(255, 85, 85), () =>
            {
                bool ok = NotificacionWindow.MostrarConfirmacion(
                    "¿Quitar la rutina al socio " + a.SocioNombre + "?",
                    "Desasignar");
                if (!ok) return;

                var r = _controller.DesasignarRutina(a.Id);
                if (r.ok)
                {
                    NotificacionWindow.MostrarExito(r.mensaje);
                    CargarAsignacionesActuales();
                    CargarRutinas();
                }
                else NotificacionWindow.MostrarError(r.mensaje);
            });
            Grid.SetColumn(btnDel, 2);
            grid.Children.Add(btnDel);

            fila.Child = grid;
            return fila;
        }

        private void btnConfirmarAsignar_Click(object sender, RoutedEventArgs e)
        {
            var socio = cmbAsignarSocio.SelectedItem as SocioComboItem;
            if (socio == null)
            { NotificacionWindow.MostrarAdvertencia("Eleg un socio."); return; }

            var r = _controller.AsignarRutina(_rutinaActual.Id, socio.Id, UsuarioId);
            if (r.ok)
            {
                NotificacionWindow.MostrarExito(r.mensaje);
                cmbAsignarSocio.SelectedIndex = -1;
                CargarAsignacionesActuales();
                CargarRutinas();
            }
            else NotificacionWindow.MostrarError(r.mensaje);
        }

        private void btnCerrarAsignar_Click(object sender, RoutedEventArgs e)
            => CerrarModal(modalAsignar);

        // ── HELPERS ───────────────────────────────────────────
        private void txtSoloNumeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^\d$");

        private void AbrirPanel(Border panel)
        {
            panel.Visibility = Visibility.Visible;
            panel.Opacity = 0;

            var translate = new TranslateTransform { X = 60 };
            panel.RenderTransform = translate;

            var slide = new DoubleAnimation
            {
                From = 60,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            translate.BeginAnimation(TranslateTransform.XProperty, slide);

            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(300))
            };
            panel.BeginAnimation(OpacityProperty, fade);
        }

        private void CerrarPanel(Border panel)
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
                panel.Visibility = Visibility.Collapsed;
                panel.RenderTransform = null;
            };
            panel.BeginAnimation(OpacityProperty, fade);
        }

        private void AbrirModal(Border modal)
        {
            modal.Visibility = Visibility.Visible;
            modal.Opacity = 0;
            var fade = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(180)) };
            modal.BeginAnimation(OpacityProperty, fade);
        }

        private void CerrarModal(Border modal)
        {
            var fade = new DoubleAnimation { From = 1, To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(150)) };
            fade.Completed += (s, e) => modal.Visibility = Visibility.Collapsed;
            modal.BeginAnimation(OpacityProperty, fade);
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