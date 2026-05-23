// SistemaGimnacionOptimusCAI/Paginas/AuditoriaPage.xaml.cs — C# 7.3
using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class AuditoriaPage : Page
    {
        private readonly AuditoriaController _controller = new AuditoriaController();

        private List<AuditoriaEntry> _todos = new List<AuditoriaEntry>();
        private AuditoriaEntry _entryActual = null;

        public AuditoriaPage()
        {
            InitializeComponent();

            dpDesde.SelectedDate = DateTime.Today.AddDays(-7);
            dpHasta.SelectedDate = DateTime.Today;

            CargarFiltros();
            Cargar();
        }

        // ── FILTROS ───────────────────────────────────────────
        private void CargarFiltros()
        {
            // Usuarios
            var listaUsr = new List<dynamic_>();
            // Como no podemos usar dynamic real en este nivel, usamos ComboBoxItem
            cmbUsuario.Items.Clear();
            cmbUsuario.Items.Add(new ComboBoxItem
            {
                Content = "Todos los usuarios",
                Tag = (long?)null,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255)),
                IsSelected = true
            });
            try
            {
                var usuarios = _controller.ListarUsuarios();
                foreach (var u in usuarios)
                {
                    cmbUsuario.Items.Add(new ComboBoxItem
                    {
                        Content = u.Nombre + " " + u.Apellido,
                        Tag = (long?)u.Id,
                        Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255))
                    });
                }
            }
            catch { }

            // Entidades
            cmbEntidad.Items.Clear();
            cmbEntidad.Items.Add(new ComboBoxItem
            {
                Content = "Todas las entidades",
                Tag = "",
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255)),
                IsSelected = true
            });
            foreach (var ent in _controller.ListarEntidades())
            {
                string display = char.ToUpper(ent[0]) + ent.Substring(1);
                cmbEntidad.Items.Add(new ComboBoxItem
                {
                    Content = display,
                    Tag = ent,
                    Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255))
                });
            }

            // Acciones
            cmbAccion.Items.Clear();
            cmbAccion.Items.Add(new ComboBoxItem
            {
                Content = "Todas las acciones",
                Tag = "",
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255)),
                IsSelected = true
            });
            foreach (var ac in _controller.ListarAcciones())
            {
                string display = char.ToUpper(ac[0]) + ac.Substring(1);
                cmbAccion.Items.Add(new ComboBoxItem
                {
                    Content = display,
                    Tag = ac,
                    Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255))
                });
            }
        }

        // Tipo dummy para evitar warnings (no se usa realmente)
        private class dynamic_ { }

        // ── CARGA ─────────────────────────────────────────────
        private void Cargar()
        {
            try
            {
                long? actorId = LeerActorIdSeleccionado();
                string ent = LeerTagComboBox(cmbEntidad);
                string acc = LeerTagComboBox(cmbAccion);

                _todos = _controller.Buscar(
                    txtBuscar.Text, actorId,
                    string.IsNullOrEmpty(ent) ? null : ent,
                    string.IsNullOrEmpty(acc) ? null : acc,
                    dpDesde.SelectedDate, dpHasta.SelectedDate);

                ActualizarStats();
                RenderizarTimeline();

                // Refrescar detalle si hay seleccion
                if (_entryActual != null)
                {
                    _entryActual = _controller.ObtenerPorId(_entryActual.Id);
                    if (_entryActual != null) RenderizarDetalle();
                    else MostrarSinSeleccion();
                }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private long? LeerActorIdSeleccionado()
        {
            var item = cmbUsuario.SelectedItem as ComboBoxItem;
            if (item == null) return null;
            return item.Tag as long?;
        }

        private string LeerTagComboBox(ComboBox cb)
        {
            var item = cb.SelectedItem as ComboBoxItem;
            if (item == null) return string.Empty;
            return item.Tag as string ?? string.Empty;
        }

        private void ActualizarStats()
        {
            try
            {
                var s = _controller.ObtenerEstadisticas();
                statTotal.Text = s.Total.ToString();
                statHoy.Text = s.Hoy.ToString();
                statMes.Text = s.Mes.ToString();
                statUsuarios.Text = s.UsuariosActivosMes.ToString();
            }
            catch
            {
                statTotal.Text = statHoy.Text = statMes.Text = statUsuarios.Text = "-";
            }
        }

        // ── TIMELINE ──────────────────────────────────────────
        private void RenderizarTimeline()
        {
            panelTimeline.Children.Clear();

            if (_todos.Count == 0)
            {
                panelVacio.Visibility = Visibility.Visible;
                return;
            }
            panelVacio.Visibility = Visibility.Collapsed;

            // Agrupar visualmente por dia
            string ultimoDia = null;
            foreach (var entry in _todos)
            {
                string dia = entry.CreadoEn.ToString("dd 'de' MMMM, yyyy",
                    new System.Globalization.CultureInfo("es-AR"));

                if (dia != ultimoDia)
                {
                    panelTimeline.Children.Add(CrearSeparadorDia(dia));
                    ultimoDia = dia;
                }
                panelTimeline.Children.Add(CrearItemTimeline(entry));
            }
        }

        private Border CrearSeparadorDia(string textoDia)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 15, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(37, 37, 64)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 4, 0, 0),
                Child = new TextBlock
                {
                    Text = textoDia.ToUpper(),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154))
                }
            };
        }

        private Border CrearItemTimeline(AuditoriaEntry e)
        {
            bool seleccionado = _entryActual != null && _entryActual.Id == e.Id;
            Color colorAccion = ColorPorAccion(e.Accion);

            var card = new Border
            {
                Background = new SolidColorBrush(seleccionado
                                    ? Color.FromRgb(22, 24, 50)
                                    : Color.FromRgb(15, 15, 30)),
                BorderBrush = new SolidColorBrush(seleccionado
                                    ? Color.FromRgb(167, 139, 250)
                                    : Color.FromRgb(26, 26, 46)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 12, 20, 12),
                Cursor = Cursors.Hand,
                Tag = e.Id
            };
            card.MouseLeftButtonUp += (s, ev) => Seleccionar(e.Id);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Hora
            var lblHora = new TextBlock
            {
                Text = e.CreadoEn.ToString("HH:mm"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154)),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 50
            };
            Grid.SetColumn(lblHora, 0);
            grid.Children.Add(lblHora);

            // Punto colored del timeline
            var dot = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(colorAccion),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            };
            Grid.SetColumn(dot, 1);
            grid.Children.Add(dot);

            // Contenido
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var linea1 = new StackPanel { Orientation = Orientation.Horizontal };
            linea1.Children.Add(new TextBlock
            {
                Text = e.IconoEntidad + "  ",
                FontSize = 13
            });
            linea1.Children.Add(new TextBlock
            {
                Text = e.ResumenAccion,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255))
            });
            stack.Children.Add(linea1);

            stack.Children.Add(new TextBlock
            {
                Text = "por " + e.ActorNombre,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 192)),
                Margin = new Thickness(0, 2, 0, 0)
            });

            Grid.SetColumn(stack, 2);
            grid.Children.Add(stack);

            // Badge accion
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, colorAccion.R, colorAccion.G, colorAccion.B)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = e.Accion.ToUpper(),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(colorAccion)
                }
            };
            Grid.SetColumn(badge, 3);
            grid.Children.Add(badge);

            card.Child = grid;
            return card;
        }

        private Color ColorPorAccion(string accion)
        {
            switch ((accion ?? "").ToLower())
            {
                case "crear":
                case "activar":
                    return Color.FromRgb(0, 230, 118);     // verde
                case "editar":
                case "modificar":
                    return Color.FromRgb(0, 207, 255);     // cyan
                case "eliminar":
                case "desactivar":
                case "anular":
                    return Color.FromRgb(255, 85, 85);     // rojo
                case "login":
                case "logout":
                    return Color.FromRgb(167, 139, 250);   // violeta
                default:
                    return Color.FromRgb(160, 160, 192);   // gris
            }
        }

        // ── SELECCION ─────────────────────────────────────────
        private void Seleccionar(long id)
        {
            try
            {
                _entryActual = _controller.ObtenerPorId(id);
                if (_entryActual == null) return;
                RenderizarTimeline();
                RenderizarDetalle();
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void MostrarSinSeleccion()
        {
            _entryActual = null;
            panelSinSeleccion.Visibility = Visibility.Visible;
            scrollDetalle.Visibility = Visibility.Collapsed;
        }

        // ── DETALLE ───────────────────────────────────────────
        private void RenderizarDetalle()
        {
            if (_entryActual == null) { MostrarSinSeleccion(); return; }

            panelSinSeleccion.Visibility = Visibility.Collapsed;
            scrollDetalle.Visibility = Visibility.Visible;

            lblDetalleActor.Text = _entryActual.ActorNombre;
            lblDetalleRol.Text = _entryActual.ActorRol == "admin" ? "Administrador" : "Empleado";

            if (_entryActual.ActorFoto != null && _entryActual.ActorFoto.Length > 0)
                imgDetalleFoto.ImageSource = BytesABitmapImage(_entryActual.ActorFoto);
            else
                imgDetalleFoto.ImageSource = null;

            lblIconoEntidad.Text = _entryActual.IconoEntidad;
            lblDetalleResumen.Text = _entryActual.ResumenAccion;
            lblDetalleFechaHora.Text = _entryActual.FechaLarga;

            lblDatoAccion.Text = _entryActual.AccionTexto;
            lblDatoEntidad.Text = _entryActual.EntidadTexto;
            lblDatoEntidadId.Text = _entryActual.EntidadId.HasValue
                                        ? "#" + _entryActual.EntidadId.Value
                                        : "-";
            lblDatoRegId.Text = "#" + _entryActual.Id.ToString("D6");

            // JSON formateado
            lblDetalleJson.Text = string.IsNullOrEmpty(_entryActual.Detalle)
                                    ? "(sin datos adicionales)"
                                    : FormatearJson(_entryActual.Detalle);
        }

        /// <summary>Formato simple del JSON con indentación.</summary>
        private string FormatearJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return "";
            var sb = new StringBuilder();
            int indent = 0;
            bool inString = false;

            foreach (char c in json)
            {
                if (c == '"' && !inString) { inString = true; sb.Append(c); continue; }
                if (c == '"' && inString) { inString = false; sb.Append(c); continue; }

                if (inString) { sb.Append(c); continue; }

                if (c == '{' || c == '[')
                {
                    sb.Append(c);
                    indent++;
                    sb.AppendLine();
                    sb.Append(new string(' ', indent * 2));
                }
                else if (c == '}' || c == ']')
                {
                    indent--;
                    sb.AppendLine();
                    sb.Append(new string(' ', indent * 2));
                    sb.Append(c);
                }
                else if (c == ',')
                {
                    sb.Append(c);
                    sb.AppendLine();
                    sb.Append(new string(' ', indent * 2));
                }
                else if (c == ':')
                {
                    sb.Append(": ");
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        // ── EVENTOS ───────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => Cargar();
        private void cmbFiltro_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Evita que se ejecute antes de inicializar todo
            if (!IsLoaded && !IsInitialized) return;
            Cargar();
        }
        private void dpFecha_Changed(object sender, SelectionChangedEventArgs e) => Cargar();

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