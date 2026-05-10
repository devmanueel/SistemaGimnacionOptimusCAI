// SistemaGimnacionOptimusCAI/Paginas/InstructorAsistenciasPage.xaml.cs — C# 7.3
using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class InstructorAsistenciasPage : Page
    {
        private readonly InstructorAsistenciaController _controller = new InstructorAsistenciaController();

        private DispatcherTimer _timerReloj;

        public InstructorAsistenciasPage()
        {
            InitializeComponent();

            dpDesde.SelectedDate = DateTime.Today.AddDays(-7);
            dpHasta.SelectedDate = DateTime.Today;

            IniciarReloj();
            CargarTurnosDeHoy();
            CargarHistorial();
            ActualizarStats();
        }

        // ── RELOJ ─────────────────────────────────────────────
        private void IniciarReloj()
        {
            ActualizarReloj();
            _timerReloj = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timerReloj.Tick += (s, e) => ActualizarReloj();
            _timerReloj.Start();
        }

        private void ActualizarReloj()
        {
            DateTime ahora = DateTime.Now;
            lblReloj.Text = ahora.ToString("HH:mm:ss");

            string[] dias = { "Domingo", "Lunes", "Martes", "Miercoles", "Jueves", "Viernes", "Sabado" };
            string[] meses = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                               "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

            lblFecha.Text = dias[(int)ahora.DayOfWeek] + ", " +
                            ahora.Day + " de " + meses[ahora.Month - 1] + " " + ahora.Year;
        }

        // ── STATS ─────────────────────────────────────────────
        private void ActualizarStats()
        {
            try
            {
                var s = _controller.ObtenerEstadisticas();
                statHoy.Text = s.AsistenciasHoy.ToString();
                statAbiertas.Text = s.AbiertasHoy.ToString();
                statInstructores.Text = s.InstructoresHoy.ToString();
                statMes.Text = s.AsistenciasMes.ToString();
            }
            catch
            {
                statHoy.Text = statAbiertas.Text =
                    statInstructores.Text = statMes.Text = "—";
            }
        }

        // ── TURNOS DE HOY (fichaje rapido) ────────────────────
        private void CargarTurnosDeHoy()
        {
            try
            {
                var turnos = _controller.ObtenerTurnosDeHoy();
                panelTurnosHoy.Children.Clear();

                if (turnos.Count == 0)
                {
                    panelSinTurnos.Visibility = Visibility.Visible;
                    return;
                }

                panelSinTurnos.Visibility = Visibility.Collapsed;

                foreach (var t in turnos)
                    panelTurnosHoy.Children.Add(CrearCardTurnoHoy(t));
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message);
            }
        }

        private Border CrearCardTurnoHoy(TurnoHoy t)
        {
            // Color de fondo segun estado
            Color colorBorde;
            Color colorBg;
            if (t.SalidaRegistrada)
            { colorBorde = Color.FromRgb(0, 230, 118); colorBg = Color.FromRgb(10, 26, 16); }
            else if (t.EntradaRegistrada)
            { colorBorde = Color.FromRgb(255, 167, 38); colorBg = Color.FromRgb(42, 31, 0); }
            else
            { colorBorde = Color.FromRgb(106, 106, 154); colorBg = Color.FromRgb(18, 18, 30); }

            var card = new Border
            {
                Background = new SolidColorBrush(colorBg),
                BorderBrush = new SolidColorBrush(colorBorde),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Info izquierda
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            // Horario en grande
            infoStack.Children.Add(new TextBlock
            {
                Text = t.RangoHorario,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(colorBorde)
            });

            // Actividad
            infoStack.Children.Add(new TextBlock
            {
                Text = t.ActividadNombre,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255)),
                Margin = new Thickness(0, 4, 0, 0)
            });

            // Instructor + foto mini
            var instrStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0)
            };

            if (t.InstructorFoto != null && t.InstructorFoto.Length > 0)
            {
                var ellipse = new System.Windows.Shapes.Ellipse
                {
                    Width = 22,
                    Height = 22,
                    Margin = new Thickness(0, 0, 6, 0),
                    Fill = new ImageBrush(BytesABitmapImage(t.InstructorFoto))
                    {
                        Stretch = Stretch.UniformToFill
                    }
                };
                instrStack.Children.Add(ellipse);
            }

            instrStack.Children.Add(new TextBlock
            {
                Text = t.InstructorNombre,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
                VerticalAlignment = VerticalAlignment.Center
            });
            infoStack.Children.Add(instrStack);

            // Estado + horas fichadas
            if (t.EntradaRegistrada || t.SalidaRegistrada)
            {
                string fichaje = "Entrada: " + t.HoraEntradaTexto;
                if (t.SalidaRegistrada) fichaje += "  |  Salida: " + t.HoraSalidaTexto;

                infoStack.Children.Add(new TextBlock
                {
                    Text = fichaje,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154)),
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);

            // Boton de accion a la derecha
            if (t.TieneInstructor)
            {
                var btnStack = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center
                };

                if (t.SinFichar)
                {
                    var btn = CrearBotonFichaje("▶ ENTRADA", "#0A2A14", "#00E676", t);
                    btn.Click += (s, e) => FicharEntrada(t);
                    btnStack.Children.Add(btn);
                }
                else if (t.EntradaRegistrada)
                {
                    var btn = CrearBotonFichaje("■ SALIDA", "#2A1F00", "#FFA726", t);
                    btn.Click += (s, e) => FicharSalida(t);
                    btnStack.Children.Add(btn);
                }
                else
                {
                    btnStack.Children.Add(new TextBlock
                    {
                        Text = "✓",
                        FontSize = 22,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                }

                Grid.SetColumn(btnStack, 1);
                grid.Children.Add(btnStack);
            }

            card.Child = grid;
            return card;
        }

        private Button CrearBotonFichaje(string texto, string bgHex, string fgHex, TurnoHoy t)
        {
            var bgColor = (Color)ColorConverter.ConvertFromString(bgHex);
            var fgColor = (Color)ColorConverter.ConvertFromString(fgHex);

            var btn = new Button
            {
                Width = 110,
                Height = 36,
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush(bgColor),
                Foreground = new SolidColorBrush(fgColor),
                BorderBrush = new SolidColorBrush(fgColor),
                BorderThickness = new Thickness(1.5),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Tag = t
            };

            btn.Template = CrearBotonTemplate();

            var tb = new TextBlock
            {
                Text = texto,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = new SolidColorBrush(fgColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btn.Content = tb;
            return btn;
        }

        private ControlTemplate CrearBotonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;
            return template;
        }

        // ── FICHAJE ───────────────────────────────────────────
        private void FicharEntrada(TurnoHoy t)
        {
            if (!t.TieneInstructor || t.InstructorId == null) return;

            long registradoPor = SesionManager.HaySesion ? SesionManager.UsuarioId : 1;

            try
            {
                var r = _controller.RegistrarEntrada(
                    t.InstructorId.Value, t.TurnoId, null, registradoPor);

                if (r.ok)
                {
                    NotificacionWindow.MostrarExito("Entrada registrada para " + t.InstructorNombre + ".");
                    RefrescarTodo();
                }
                else NotificacionWindow.MostrarError(r.mensaje);
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void FicharSalida(TurnoHoy t)
        {
            if (!t.AsistenciaId.HasValue) return;

            try
            {
                var r = _controller.RegistrarSalida(t.AsistenciaId.Value);

                if (r.ok)
                {
                    NotificacionWindow.MostrarExito("Salida registrada para " + t.InstructorNombre + ".");
                    RefrescarTodo();
                }
                else NotificacionWindow.MostrarError(r.mensaje);
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        // ── HISTORIAL ─────────────────────────────────────────
        private void CargarHistorial()
        {
            try
            {
                var lista = _controller.Buscar(
                    txtBuscar.Text, null,
                    dpDesde.SelectedDate, dpHasta.SelectedDate);
                gridHistorial.ItemsSource = lista;
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message);
            }
        }

        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e)
            => CargarHistorial();

        private void dpFecha_Changed(object sender, SelectionChangedEventArgs e)
            => CargarHistorial();

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            var asist = (sender as Button)?.DataContext as InstructorAsistencia;
            if (asist == null) return;

            bool ok = NotificacionWindow.MostrarConfirmacion(
                "Eliminar asistencia de " + asist.InstructorNombre +
                " del " + asist.FechaTexto + "?",
                "Eliminar asistencia");
            if (!ok) return;

            try
            {
                var r = _controller.Eliminar(asist.Id);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); RefrescarTodo(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        // ── REFRESH ───────────────────────────────────────────
        private void RefrescarTodo()
        {
            CargarTurnosDeHoy();
            CargarHistorial();
            ActualizarStats();
        }

        // ── HELPER ────────────────────────────────────────────
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