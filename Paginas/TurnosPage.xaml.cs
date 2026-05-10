// SistemaGimnacionOptimusCAI/Paginas/TurnosPage.xaml.cs — C# 7.3
using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class TurnosPage : Page
    {
        private readonly TurnoController _controller = new TurnoController();

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private long? _filtroActividadId = null;

        private List<Turno> _todosLosTurnos = new List<Turno>();
        private List<Actividad> _actividades = new List<Actividad>();

        public TurnosPage()
        {
            InitializeComponent();
            CargarComboActividades();
            CargarComboInstructores();
            CargarTurnos();
        }

        // ── CARGA ─────────────────────────────────────────────
        private void CargarComboActividades()
        {
            try
            {
                _actividades = _controller.ListarActividadesParaCombo();

                cmbActividad.ItemsSource = _actividades;

                // Combo de filtro: agregar opción "Todas"
                var lista = new List<Actividad>
                {
                    new Actividad { Id = 0, Nombre = "Todas las actividades" }
                };
                lista.AddRange(_actividades);
                cmbFiltroActividad.ItemsSource = lista;
                cmbFiltroActividad.SelectedIndex = 0;
            }
            catch { }
        }

        private void CargarComboInstructores()
        {
            try
            {
                var usuarios = _controller.ListarInstructoresParaCombo();
                // Agregar opción "sin asignar" al inicio
                var lista = new List<Usuario>
                {
                    new Usuario { Id = 0, Nombre = "Sin", Apellido = "asignar" }
                };
                lista.AddRange(usuarios);
                cmbInstructor.ItemsSource = lista;
            }
            catch { }
        }

        private void CargarTurnos()
        {
            try
            {
                _todosLosTurnos = _controller.ObtenerTurnos();
                ActualizarStats();
                RenderizarCalendario();
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void ActualizarStats()
        {
            try
            {
                var s = _controller.ObtenerEstadisticas();
                statTotal.Text = s.Total.ToString();
                statActivos.Text = s.Activos.ToString();
                statSinInstructor.Text = s.SinInstructor.ToString();
                statCupoTotal.Text = s.CupoTotal.ToString();
            }
            catch
            {
                statTotal.Text = statActivos.Text =
                    statSinInstructor.Text = statCupoTotal.Text = "—";
            }
        }

        // ── RENDER CALENDARIO ─────────────────────────────────
        private void RenderizarCalendario()
        {
            colLunes.Children.Clear();
            colMartes.Children.Clear();
            colMiercoles.Children.Clear();
            colJueves.Children.Clear();
            colViernes.Children.Clear();
            colSabado.Children.Clear();
            colDomingo.Children.Clear();

            int mostrados = 0;

            foreach (var t in _todosLosTurnos)
            {
                if (_filtroActividadId.HasValue && t.ActividadId != _filtroActividadId.Value)
                    continue;

                StackPanel col = ColumnaParaDia(t.DiaSemana);
                if (col == null) continue;

                col.Children.Add(CrearCardTurno(t));
                mostrados++;
            }

            panelVacio.Visibility = mostrados == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private StackPanel ColumnaParaDia(byte dia)
        {
            switch (dia)
            {
                case 1: return colLunes;
                case 2: return colMartes;
                case 3: return colMiercoles;
                case 4: return colJueves;
                case 5: return colViernes;
                case 6: return colSabado;
                case 7: return colDomingo;
                default: return null;
            }
        }

        private Border CrearCardTurno(Turno t)
        {
            // Color según actividad (hash simple del id)
            Color color = ColorPorActividad(t.ActividadId);
            Color colorBg = Color.FromArgb(40, color.R, color.G, color.B);

            var card = new Border
            {
                Background = new SolidColorBrush(colorBg),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(0, 0, 0, 0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand,
                Tag = t.Id,
                DataContext = t
            };

            // Borde lateral de color
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var barra = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(barra, 0);
            grid.Children.Add(barra);

            var stack = new StackPanel();
            Grid.SetColumn(stack, 1);

            // Hora
            stack.Children.Add(new TextBlock
            {
                Text = t.RangoHorario,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color)
            });

            // Actividad
            stack.Children.Add(new TextBlock
            {
                Text = t.ActividadNombre,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255)),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 32,
                Margin = new Thickness(0, 2, 0, 0)
            });

            // Instructor
            stack.Children.Add(new TextBlock
            {
                Text = "👤 " + t.InstructorNombre,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 192)),
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            // Cupo
            stack.Children.Add(new TextBlock
            {
                Text = "👥 " + t.CupoMaximo + " lugares",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154)),
                Margin = new Thickness(0, 2, 0, 0)
            });

            // Si está inactivo, capa oscura
            if (!t.Activo)
            {
                stack.Opacity = 0.4;
                stack.Children.Add(new TextBlock
                {
                    Text = "INACTIVO",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85)),
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            grid.Children.Add(stack);
            card.Child = grid;

            card.MouseLeftButtonUp += (s, e) => AbrirParaEditar(t);
            return card;
        }

        private Color ColorPorActividad(long actividadId)
        {
            Color[] paleta =
            {
                Color.FromRgb(0, 207, 255),    // cyan
                Color.FromRgb(255, 107, 53),   // naranja
                Color.FromRgb(0, 230, 118),    // verde
                Color.FromRgb(167, 139, 250),  // violeta
                Color.FromRgb(255, 167, 38),   // amarillo
                Color.FromRgb(244, 114, 182),  // rosa
                Color.FromRgb(96, 165, 250)    // azul
            };
            return paleta[Math.Abs((int)(actividadId % paleta.Length))];
        }

        // ── FILTRO ────────────────────────────────────────────
        private void cmbFiltroActividad_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var act = cmbFiltroActividad.SelectedItem as Actividad;
            if (act == null) { _filtroActividadId = null; return; }

            _filtroActividadId = act.Id == 0 ? (long?)null : act.Id;
            RenderizarCalendario();
        }

        // ── BOTONES ───────────────────────────────────────────
        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            _esNuevo = true;
            _idEditar = 0;
            LimpiarFormulario();
            lblTituloFormulario.Text = "NUEVO TURNO";
            panelEliminar.Visibility = Visibility.Collapsed;
            AbrirFormulario();
        }

        private void AbrirParaEditar(Turno t)
        {
            _esNuevo = false;
            _idEditar = t.Id;

            lblTituloFormulario.Text = "EDITAR TURNO";

            // Seleccionar actividad
            foreach (var a in _actividades)
                if (a.Id == t.ActividadId) { cmbActividad.SelectedItem = a; break; }

            // Seleccionar dia
            foreach (var item in cmbDia.Items)
            {
                var ci = item as ComboBoxItem;
                if (ci != null && ci.Tag != null && Convert.ToByte(ci.Tag) == t.DiaSemana)
                {
                    cmbDia.SelectedItem = ci;
                    break;
                }
            }

            // Seleccionar instructor
            if (t.InstructorId.HasValue)
            {
                foreach (var item in cmbInstructor.Items)
                {
                    var u = item as Usuario;
                    if (u != null && u.Id == t.InstructorId.Value)
                    {
                        cmbInstructor.SelectedItem = u;
                        break;
                    }
                }
            }
            else
            {
                cmbInstructor.SelectedIndex = 0;
            }

            txtHoraInicio.Text = t.HoraInicioTexto;
            txtHoraFin.Text = t.HoraFinTexto;
            txtCupo.Text = t.CupoMaximo.ToString();

            panelEliminar.Visibility = Visibility.Visible;
            AbrirFormulario();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            var act = cmbActividad.SelectedItem as Actividad;
            if (act == null || act.Id <= 0)
            { NotificacionWindow.MostrarAdvertencia("Tenes que elegir una actividad."); return; }

            var ciDia = cmbDia.SelectedItem as ComboBoxItem;
            if (ciDia == null || ciDia.Tag == null)
            { NotificacionWindow.MostrarAdvertencia("Tenes que elegir un dia."); return; }
            byte dia = Convert.ToByte(ciDia.Tag);

            TimeSpan horaInicio, horaFin;
            if (!TryParseHora(txtHoraInicio.Text, out horaInicio))
            { NotificacionWindow.MostrarAdvertencia("Hora de inicio invalida. Formato: HH:MM"); return; }
            if (!TryParseHora(txtHoraFin.Text, out horaFin))
            { NotificacionWindow.MostrarAdvertencia("Hora de fin invalida. Formato: HH:MM"); return; }

            short cupo = 30;
            short.TryParse(txtCupo.Text, out cupo);

            // Instructor
            var instr = cmbInstructor.SelectedItem as Usuario;
            long? instructorId = (instr == null || instr.Id == 0) ? (long?)null : instr.Id;

            if (_esNuevo)
            {
                var r = _controller.Insertar(act.Id, instructorId, dia, horaInicio, horaFin, cupo);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje);
            }
            else
            {
                var r = _controller.Modificar(_idEditar, act.Id, instructorId, dia, horaInicio, horaFin, cupo);
                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje);
            }

            CerrarFormulario();
            CargarTurnos();
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (_idEditar <= 0) return;

            bool ok = NotificacionWindow.MostrarConfirmacion(
                "¿Eliminar definitivamente este turno?\n\nEsta accion no se puede deshacer.",
                "Eliminar turno");
            if (!ok) return;

            try
            {
                var r = _controller.Eliminar(_idEditar);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CerrarFormulario(); CargarTurnos(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) => CerrarFormulario();

        // ── HELPERS ───────────────────────────────────────────
        private bool TryParseHora(string texto, out TimeSpan ts)
        {
            ts = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(texto)) return false;

            // Aceptar "HH:MM" o "H:MM"
            if (!Regex.IsMatch(texto, @"^\d{1,2}:\d{2}$")) return false;

            string[] parts = texto.Split(':');
            int h, m;
            if (!int.TryParse(parts[0], out h) || !int.TryParse(parts[1], out m)) return false;
            if (h < 0 || h > 23 || m < 0 || m > 59) return false;

            ts = new TimeSpan(h, m, 0);
            return true;
        }

        private void txtSoloNumeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^\d$");

        private void LimpiarFormulario()
        {
            cmbActividad.SelectedIndex = -1;
            cmbDia.SelectedIndex = -1;
            cmbInstructor.SelectedIndex = 0;
            txtHoraInicio.Text = string.Empty;
            txtHoraFin.Text = string.Empty;
            txtCupo.Text = "30";
            _idEditar = 0;
        }

        // ── ANIMACIONES ───────────────────────────────────────
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
                Duration = new Duration(TimeSpan.FromMilliseconds(180))
            };
            fade.Completed += (s, e) =>
            {
                panelFormulario.Visibility = Visibility.Collapsed;
                LimpiarFormulario();
            };
            panelFormulario.BeginAnimation(OpacityProperty, fade);
        }
    }
}