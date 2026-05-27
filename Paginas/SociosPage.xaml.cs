// ============================================================
//  Archivo: SociosPage.xaml.cs
//
//  CAMBIO IMPORTANTE:
//  · Validador ahora vive en Controllers (no en Helpers).
//  · NotificacionWindow sigue en Helpers (es WPF puro).
//  · Por eso ahora hay 2 usings: Controllers (para Validador)
//    y Helpers (para NotificacionWindow).
//
//  Compatible con C# 7.3.
// ============================================================

using Controllers;                         // ← Controller + Validador
using Entities;
using Microsoft.Win32;
using SistemaGimnacionOptimusCAI.Helpers;  // ← NotificacionWindow + ByteToImageConverter
using SistemaGimnacionOptimusCAI.Ventanas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class SociosPage : Page
    {
        private readonly SocioController _controller = new SocioController();
        private readonly ActividadController _actividadController = new ActividadController();
        private readonly UsuarioController _usuarioController = new UsuarioController();

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private byte[] _fotoBytes = null;
        private string _filtroEstado = "todos";
        private string _filtroAvanzado = "todos";
        private string _tabActivo = "datos";
        private Visibility _filtrosVisibilidad = Visibility.Collapsed;
        private List<DataGridColumn> _columnasDinamicas = new List<DataGridColumn>();

        // Filtros avanzados (se aplican solo al presionar "Filtrar")
        private long?  _filtroActividadId  = null;
        private bool?  _filtroCuotaVencida = null;
        private long?  _filtroInstructorId = null;
        private string _filtroSexo         = null;
        private int?   _filtroDejaronVenir = null;

        public SociosPage()
        {
            InitializeComponent();
            panelFiltrosAvanzados.Visibility = Visibility.Collapsed;
            ActualizarStats();
            CargarInstructores();
            CargarSocios();
            ResaltarChip(chipTodos);
            CambiarTab("datos");
        }

        // ─────────────────────────────────────────────────────
        // CARGA DE COMBOS (instructores rolId = 2, actividades)
        // ─────────────────────────────────────────────────────
        private void CargarInstructores()
        {
            try
            {
                var actividades = _actividadController.ObtenerActividadesActivas();
                if (cmbFiltroActividad != null) cmbFiltroActividad.ItemsSource = actividades;

                var instructores = _usuarioController.ObtenerUsuariosActivosPorRol(2);
                if (cmbFiltroInstructor != null) cmbFiltroInstructor.ItemsSource = instructores;
            }
            catch { }
        }

        // ─────────────────────────────────────────────────────
        // CARGA + STATS
        // ─────────────────────────────────────────────────────
        private void CargarSocios()
        {
            try
            {
                // 1. Traer TODOS los datos con filtros avanzados aplicados (SIN filtro de chip)
                var listaCompleta = _controller.ListarSociosConMembresias(
                    texto:              txtBuscar != null ? txtBuscar.Text.Trim() : "",
                    filtroEstado:       "todos",  // siempre traer todos para contar correctamente
                    filtroActividadId:  _filtroActividadId,
                    filtroCuotaVencida: _filtroCuotaVencida,
                    filtroInstructorId: _filtroInstructorId,
                    filtroSexo:         _filtroSexo,
                    filtroDejaronVenir: _filtroDejaronVenir);

                // 2. Actualizar chips con la lista completa (sin filtro de chip)
                ActualizarContadoresChips(listaCompleta);

                // 3. Filtrar por estado del chip para mostrar en la tabla
                List<SocioConMembresia> listaFiltrada;
                if (_filtroEstado == "todos")
                    listaFiltrada = listaCompleta;
                else if (_filtroEstado == "activos")
                    listaFiltrada = listaCompleta.FindAll(s => s.MembresiaEstado == "activa");
                else if (_filtroEstado == "inactivos")
                    listaFiltrada = listaCompleta.FindAll(s => s.MembresiaEstado != "activa");
                else
                    listaFiltrada = listaCompleta;

                if (gridSocios != null)
                    gridSocios.ItemsSource = listaFiltrada;

                ActualizarResumenFiltros(listaFiltrada);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar socios");
            }
        }

        private void ActualizarStats()
        {
            try
            {
                var todos = _controller.ObtenerSocios();
                int total = todos.Count;
                int activos = 0;
                int inactivos = 0;
                int nuevosMes = 0;
                var primerDiaMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

                foreach (var s in todos)
                {
                    if (s.Activo) activos++; else inactivos++;
                    if (s.CreadoEn >= primerDiaMes) nuevosMes++;
                }

                statTotal.Text = total.ToString();
                statActivos.Text = activos.ToString();
                statInactivos.Text = inactivos.ToString();
                statNuevosMes.Text = nuevosMes.ToString();
            }
            catch
            {
                statTotal.Text = statActivos.Text = statInactivos.Text = statNuevosMes.Text = "-";
            }
        }

        private void ActualizarContadoresChips(List<SocioConMembresia> lista)
        {
            try
            {
                int total = lista != null ? lista.Count : 0;
                int activos = 0;
                int inactivos = 0;

                if (lista != null)
                {
                    foreach (var s in lista)
                    {
                        if (s.MembresiaEstado == "activa") activos++;
                        else inactivos++;
                    }
                }

                chipTodosNum.Text = $"({total})";
                chipActivosNum.Text = $"({activos})";
                chipInactivosNum.Text = $"({inactivos})";
            }
            catch
            {
                chipTodosNum.Text = "(0)";
                chipActivosNum.Text = "(0)";
                chipInactivosNum.Text = "(0)";
            }
        }

        private void ActualizarResumenFiltros(List<SocioConMembresia> lista)
        {
            if (panelResumenFiltros == null || lblFiltrosActivos == null || lblCantidadSocios == null)
                return;

            bool hayFiltroAvanzado = _filtroActividadId.HasValue
                || _filtroCuotaVencida.HasValue
                || _filtroInstructorId.HasValue
                || !string.IsNullOrEmpty(_filtroSexo)
                || _filtroDejaronVenir.HasValue;

            if (_filtroEstado == "todos" && !hayFiltroAvanzado)
            {
                panelResumenFiltros.Visibility = Visibility.Collapsed;
                return;
            }

            panelResumenFiltros.Visibility = Visibility.Visible;

            var partes = new List<string>();

            // Chip estado
            if (_filtroEstado == "activos")
                partes.Add("los socios activos");
            else if (_filtroEstado == "inactivos")
                partes.Add("los socios inactivos");
            else
                partes.Add("todos los socios");

            // Filtros avanzados
            if (_filtroActividadId.HasValue && cmbFiltroActividad.SelectedItem != null)
            {
                var act = cmbFiltroActividad.SelectedItem as Actividad;
                if (act != null)
                    partes.Add(string.Format("cuya actividad es '{0}'", act.Nombre));
            }

            if (_filtroCuotaVencida.HasValue && _filtroCuotaVencida.Value)
                partes.Add("con cuota vencida");

            if (_filtroInstructorId.HasValue && cmbFiltroInstructor.SelectedItem != null)
            {
                var inst = cmbFiltroInstructor.SelectedItem as Usuario;
                if (inst != null)
                    partes.Add(string.Format("que asisten con el profesor '{0}'", inst.NombreCompleto));
            }

            if (!string.IsNullOrEmpty(_filtroSexo))
            {
                string sexoTexto = _filtroSexo == "M" ? "Masculino" :
                                   _filtroSexo == "F" ? "Femenino" : "Otro";
                partes.Add(string.Format("cuyo sexo es {0}", sexoTexto));
            }

            if (_filtroDejaronVenir.HasValue)
                partes.Add(string.Format("que no asisten hace más de {0} días", _filtroDejaronVenir.Value));

            string textoFiltros = "Se muestran " + string.Join(" ", partes.ToArray());
            lblFiltrosActivos.Text = textoFiltros;

            int cantidad = lista != null ? lista.Count : 0;
            lblCantidadSocios.Text = string.Format("Cantidad de socios: {0}", cantidad);
        }

        // ─────────────────────────────────────────────────────
        // BÚSQUEDA / FILTROS / SELECCIÓN
        // ─────────────────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => CargarSocios();

        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            _filtroEstado = btn.Tag.ToString();
            ResaltarChip(btn);
            CargarSocios();
        }

        private void btnToggleFiltros_Click(object sender, RoutedEventArgs e)
        {
            _filtrosVisibilidad = _filtrosVisibilidad == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
            if (panelFiltrosAvanzados != null)
                panelFiltrosAvanzados.Visibility = _filtrosVisibilidad;
            CargarSocios();
        }

        private void cmbFiltroAvanzado_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ocultar todos los controles secundarios
            if (cmbFiltroActividad  != null) cmbFiltroActividad.Visibility  = Visibility.Collapsed;
            if (cmbFiltroInstructor != null) cmbFiltroInstructor.Visibility = Visibility.Collapsed;
            if (cmbFiltroSexo       != null) cmbFiltroSexo.Visibility       = Visibility.Collapsed;
            if (cmbFiltroDias       != null) cmbFiltroDias.Visibility       = Visibility.Collapsed;

            var item = cmbFiltroAvanzado.SelectedItem as ComboBoxItem;
            if (item == null) return;

            switch (item.Content?.ToString())
            {
                case "Actividad":
                    cmbFiltroActividad.Visibility = Visibility.Visible;
                    break;
                case "Cuota vencida":
                    // No necesita control secundario
                    break;
                case "Profesor":
                    cmbFiltroInstructor.Visibility = Visibility.Visible;
                    break;
                case "Sexo":
                    cmbFiltroSexo.Visibility = Visibility.Visible;
                    break;
                case "Dejaron de venir":
                    cmbFiltroDias.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void btnFiltrar_Click(object sender, RoutedEventArgs e)
        {
            var item = cmbFiltroAvanzado.SelectedItem as ComboBoxItem;
            string filtroActivo = item?.Content?.ToString();

            // Resetear todos los filtros avanzados
            _filtroActividadId  = null;
            _filtroCuotaVencida = null;
            _filtroInstructorId = null;
            _filtroSexo         = null;
            _filtroDejaronVenir = null;

            switch (filtroActivo)
            {
                case "Actividad":
                    if (cmbFiltroActividad.SelectedValue != null)
                        _filtroActividadId = (long?)cmbFiltroActividad.SelectedValue;
                    break;

                case "Cuota vencida":
                    _filtroCuotaVencida = true;
                    break;

                case "Profesor":
                    if (cmbFiltroInstructor.SelectedValue != null)
                        _filtroInstructorId = (long?)cmbFiltroInstructor.SelectedValue;
                    break;

                case "Sexo":
                    if (cmbFiltroSexo.SelectedItem is ComboBoxItem itemSexo)
                        _filtroSexo = itemSexo.Tag?.ToString();
                    break;

                case "Dejaron de venir":
                    if (cmbFiltroDias.SelectedItem is ComboBoxItem itemDias
                        && int.TryParse(itemDias.Tag?.ToString(), out int dias))
                        _filtroDejaronVenir = dias;
                    break;
            }

            CargarSocios();
        }

        private void btnLimpiarFiltros_Click(object sender, RoutedEventArgs e)
        {
            // Resetear variables
            _filtroActividadId  = null;
            _filtroCuotaVencida = null;
            _filtroInstructorId = null;
            _filtroSexo         = null;
            _filtroDejaronVenir = null;

            // Resetear controles
            cmbFiltroAvanzado.SelectedIndex   = -1;
            cmbFiltroActividad.SelectedIndex  = -1;
            cmbFiltroInstructor.SelectedIndex = -1;
            cmbFiltroSexo.SelectedIndex       = -1;
            cmbFiltroDias.SelectedIndex       = -1;

            // Ocultar todos los secundarios
            cmbFiltroActividad.Visibility  = Visibility.Collapsed;
            cmbFiltroInstructor.Visibility = Visibility.Collapsed;
            cmbFiltroSexo.Visibility       = Visibility.Collapsed;
            cmbFiltroDias.Visibility       = Visibility.Collapsed;

            CargarSocios();
        }

        private void AplicarFiltroAvanzado(object sender, SelectionChangedEventArgs e) { }
        private void AplicarFiltroAvanzado(object sender, RoutedEventArgs e) { }

        private void ConfigurarColumnasGrid()
        {
            if (gridSocios == null) return;

            // Remover columnas dinámicas previas
            foreach (var col in _columnasDinamicas)
                gridSocios.Columns.Remove(col);
            _columnasDinamicas.Clear();

            if (_filtroAvanzado == "actividad")
            {
                var col = CrearColumnaTexto("ACTIVIDAD", "ActividadNombre", new DataGridLength(1.2, DataGridLengthUnitType.Star));
                gridSocios.Columns.Add(col);
                _columnasDinamicas.Add(col);
            }

            if (_filtroAvanzado == "instructor")
            {
                var col = CrearColumnaTexto("PROFESOR", "InstructorNombre", new DataGridLength(1.2, DataGridLengthUnitType.Star));
                gridSocios.Columns.Add(col);
                _columnasDinamicas.Add(col);
            }
        }

        private DataGridTextColumn CrearColumnaTexto(string header, string binding, object ancho)
        {
            var col = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(binding),
                FontSize = 12
            };
            if (ancho is DataGridLength) col.Width = (DataGridLength)ancho;
            else if (ancho is double) col.Width = new DataGridLength((double)ancho);
            else if (ancho is int) col.Width = new DataGridLength((int)ancho);
            return col;
        }

        private DataGridTemplateColumn CrearColumnaAvatar()
        {
            var col = new DataGridTemplateColumn { Width = 56, Header = "" };
            var factory = new FrameworkElementFactory(typeof(Grid));
            factory.SetValue(Grid.WidthProperty, 40.0);
            factory.SetValue(Grid.HeightProperty, 40.0);
            factory.SetValue(Grid.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            var imgFactory = new FrameworkElementFactory(typeof(Image));
            var binding = new System.Windows.Data.Binding("Foto") { Converter = new ByteToImageConverter() };
            imgFactory.SetBinding(Image.SourceProperty, binding);
            imgFactory.SetValue(Image.WidthProperty, 40.0);
            imgFactory.SetValue(Image.HeightProperty, 40.0);
            imgFactory.SetValue(Image.StretchProperty, Stretch.UniformToFill);
            imgFactory.SetValue(Image.ClipProperty, new RectangleGeometry { Rect = new Rect(0, 0, 40, 40), RadiusX = 20, RadiusY = 20 });
            factory.AppendChild(imgFactory);

            col.CellTemplate = new DataTemplate { VisualTree = factory };
            return col;
        }

        private DataGridTemplateColumn CrearColumnaEstado()
        {
            var col = new DataGridTemplateColumn { Header = "ESTADO", Width = 100 };
            var spFactory = new FrameworkElementFactory(typeof(StackPanel));
            spFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            spFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            var ellipseFactory = new FrameworkElementFactory(typeof(Ellipse));
            ellipseFactory.SetValue(Ellipse.WidthProperty, 6.0);
            ellipseFactory.SetValue(Ellipse.HeightProperty, 6.0);
            ellipseFactory.SetValue(Ellipse.VerticalAlignmentProperty, VerticalAlignment.Center);
            ellipseFactory.SetValue(Ellipse.MarginProperty, new Thickness(0, 0, 6, 0));

            var estiloEllipse = new Style(typeof(Ellipse));
            estiloEllipse.Setters.Add(new Setter(Ellipse.FillProperty, (Brush)FindResource("TextMuted")));
            estiloEllipse.Triggers.Add(new DataTrigger
            {
                Binding = new System.Windows.Data.Binding("MembresiaEstado"),
                Value = "activa",
                Setters = { new Setter(Ellipse.FillProperty, (Brush)FindResource("GreenMain")) }
            });
            ellipseFactory.SetValue(Ellipse.StyleProperty, estiloEllipse);
            spFactory.AppendChild(ellipseFactory);

            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("EstadoTexto"));
            textFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
            textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);

            var estiloText = new Style(typeof(TextBlock));
            estiloText.Setters.Add(new Setter(TextBlock.ForegroundProperty, (Brush)FindResource("TextMuted")));
            estiloText.Triggers.Add(new DataTrigger
            {
                Binding = new System.Windows.Data.Binding("MembresiaEstado"),
                Value = "activa",
                Setters = { new Setter(TextBlock.ForegroundProperty, (Brush)FindResource("GreenMain")) }
            });
            textFactory.SetValue(TextBlock.StyleProperty, estiloText);
            spFactory.AppendChild(textFactory);

            col.CellTemplate = new DataTemplate { VisualTree = spFactory };
            return col;
        }

        private void ResaltarChip(Button seleccionado)
        {
            Button[] chips = { chipTodos, chipActivos, chipInactivos };
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

        private void gridSocios_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        // ─────────────────────────────────────────────────────
        // TABS DEL FORMULARIO
        // ─────────────────────────────────────────────────────
        private void BtnTab_Click(object sender, RoutedEventArgs e)
        {
            Button btnSeleccionado = sender as Button;
            if (btnSeleccionado == null) return;

            // Resetear todos para que la barra se apague en los otros
            btnTabDatos.IsEnabled = true;
            btnTabContacto.IsEnabled = true;
            btnTabOtros.IsEnabled = true;

            // Activar el que clickeamos (enciende la barra neón)
            btnSeleccionado.IsEnabled = false;

            // Aquí tu lógica de cambio de paneles según el Tag
            string tab = btnSeleccionado.Tag.ToString();
            CambiarTab(tab);
        }

        private void CambiarTab(string tab)
        {
            _tabActivo = tab;

            tabDatos.Visibility = tab == "datos" ? Visibility.Visible : Visibility.Collapsed;
            tabContacto.Visibility = tab == "contacto" ? Visibility.Visible : Visibility.Collapsed;
            tabOtros.Visibility = tab == "otros" ? Visibility.Visible : Visibility.Collapsed;

            ResaltarTab(btnTabDatos, tab == "datos");
            ResaltarTab(btnTabContacto, tab == "contacto");
            ResaltarTab(btnTabOtros, tab == "otros");
        }

        private void ResaltarTab(Button btn, bool activo)
        {
            if (activo)
            {
                btn.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(74, 222, 128));

                // Animar la barra
                var trans = barraTab.RenderTransform as TranslateTransform;

                if (trans != null)
                {
                    double offset = 0;
                    if (btn == btnTabContacto) offset = 118;
                    else if (btn == btnTabOtros) offset = 236;

                    var animation = new DoubleAnimation
                    {
                        To = offset,
                        Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                    };

                    // Usamos la variable 'trans' en lugar del nombre directo
                    trans.BeginAnimation(TranslateTransform.XProperty, animation);
                }
            }
            else
            {
                btn.Foreground = new SolidColorBrush(Color.FromRgb(61, 92, 61));
                btn.BorderBrush = Brushes.Transparent;
            }
        }

        // ─────────────────────────────────────────────────────
        // BOTONES PRINCIPALES
        // ─────────────────────────────────────────────────────
        private void btnBajaInactivos_Click(object sender, RoutedEventArgs e)
        {
            List<SocioInactivo> inactivos;
            try
            {
                inactivos = _controller.ObtenerInactivosParaDarDeBaja(2);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo obtener la lista.\n" + ex.Message);
                return;
            }

            if (inactivos == null || inactivos.Count == 0)
            {
                NotificacionWindow.MostrarExito(
                    "No hay socios con más de 2 meses sin asistir que estén activos.",
                    "Sin inactivos");
                return;
            }

            // Armar resumen para mostrar
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Se encontraron " + inactivos.Count + " socio(s) sin asistir en 2+ meses:\n");
            int mostrar = inactivos.Count > 10 ? 10 : inactivos.Count;
            for (int i = 0; i < mostrar; i++)
            {
                var s = inactivos[i];
                sb.AppendLine("• " + s.NombreCompleto + "  " + s.NumeroSocioFormateado +
                              "  —  " + s.UltimaAsistenciaTexto);
            }
            if (inactivos.Count > 10)
                sb.AppendLine("... y " + (inactivos.Count - 10) + " más.");

            sb.AppendLine("\n¿Dar de baja a todos estos socios?");

            bool confirmo = NotificacionWindow.MostrarConfirmacion(sb.ToString(), "Dar de baja inactivos");
            if (!confirmo) return;

            var ids = new List<long>();
            foreach (var s in inactivos) ids.Add(s.Id);

            try
            {
                var r = _controller.DarDeBajaLote(ids);
                if (r.ok)
                {
                    NotificacionWindow.MostrarExito(r.mensaje, "Baja completada");
                    CargarSocios();
                }
                else
                {
                    NotificacionWindow.MostrarError(r.mensaje);
                }
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al dar de baja.\n" + ex.Message);
            }
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new NuevoSocioWindow
            {
                Owner = Window.GetWindow(this)
            };

            bool? resultado = ventana.ShowDialog();

            // Si el socio (o socio + membresía) fue creado, recargar la tabla
            if (resultado == true)
            {
                CargarSocios();
                ActualizarStats();
            }
        }

        private void btnEditar_Click(object sender, RoutedEventArgs e)
        {
            var socio = ObtenerSocioDeFila(sender);
            if (socio == null) return;

            _esNuevo = false;
            _idEditar = socio.Id;

            txtNombre.Text = socio.Nombre;
            txtApellido.Text = socio.Apellido;
            txtDni.Text = socio.Dni;
            dpNacimiento.SelectedDate = socio.FechaNacimiento;

            foreach (ComboBoxItem item in cmbSexo.Items)
            {
                if (item.Tag != null && item.Tag.ToString() == socio.Sexo)
                { cmbSexo.SelectedItem = item; break; }
            }

            txtTelefono.Text = socio.Telefono ?? string.Empty;
            txtEmail.Text = socio.Email ?? string.Empty;
            txtDomicilio.Text = socio.Domicilio ?? string.Empty;
            txtProfesion.Text = socio.Profesion ?? string.Empty;
            cmbComoConocio.Text = socio.ComoNosConocio ?? string.Empty;
            txtObservaciones.Text = socio.Observaciones ?? string.Empty;
            _fotoBytes = null;

            if (socio.Foto != null && socio.Foto.Length > 0)
                imgFotoFormulario.ImageSource = BytesABitmapImage(socio.Foto);
            else
                imgFotoFormulario.ImageSource = null;

            lblNumeroSocio.Text = socio.NumeroFormateado;
            chkRegenerarPin.Visibility = Visibility.Visible;
            chkRegenerarPin.IsChecked = false;

            LimpiarErrores();
            CambiarTab("datos");
            AbrirFormulario("EDITAR SOCIO");
        }

        private void btnToggleEstado_Click(object sender, RoutedEventArgs e)
        {
            var socio = ObtenerSocioDeFila(sender);
            if (socio == null) return;

            bool nuevoEstado = !socio.Activo;
            string accion = nuevoEstado ? "activar" : "desactivar";

            bool confirmo = NotificacionWindow.MostrarConfirmacion(
                "¿Querés " + accion + " al socio " + socio.NombreCompleto + "?",
                "Confirmar cambio de estado");

            if (!confirmo) return;

            try
            {
                var r = _controller.CambiarEstado(socio.Id, nuevoEstado);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarSocios(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void btnSubirFoto_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar foto del socio",
                Filter = "Imágenes (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                _fotoBytes = File.ReadAllBytes(dialog.FileName);
                imgFotoFormulario.ImageSource = BytesABitmapImage(_fotoBytes);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo cargar la imagen.\n" + ex.Message);
            }
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarTodo())
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Hay campos con errores. Revisá los tabs para encontrarlos.",
                    "Formulario incompleto");
                return;
            }

            string sexo = "Otro";
            var sexoItem = cmbSexo.SelectedItem as ComboBoxItem;
            if (sexoItem != null && sexoItem.Tag != null)
                sexo = sexoItem.Tag.ToString();

            string comoConocio = (cmbComoConocio.Text ?? string.Empty).Trim();
            DateTime? fechaNac = dpNacimiento.SelectedDate;

            if (_esNuevo)
            {
                var r = _controller.Insertar(
                    nombre: txtNombre.Text,
                    apellido: txtApellido.Text,
                    dni: txtDni.Text,
                    fechaNacimiento: fechaNac,
                    sexo: sexo,
                    telefono: txtTelefono.Text,
                    domicilio: txtDomicilio.Text,
                    profesion: txtProfesion.Text,
                    email: txtEmail.Text,
                    comoNosConocio: comoConocio,
                    observaciones: txtObservaciones.Text,
                    foto: _fotoBytes,
                    registradoPor: SesionManager.HaySesion ? (long?)SesionManager.UsuarioId : null);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }

                bool asignar = NotificacionWindow.MostrarConfirmacion(
                    "Socio guardado correctamente.\n\n¿Querés asignarle una membresía ahora?",
                    "¡Socio creado!");

                if (asignar && r.socioCreado != null)
                {
                    var mainWindow = Window.GetWindow(this) as MainWindow;
                    mainWindow?.NavegarAMembresiasConSocio(r.socioCreado);
                    return;
                }

                NotificacionWindow.MostrarExito(r.mensaje, "¡Socio registrado!");
            }
            else
            {
                bool regenerar = chkRegenerarPin.IsChecked == true;

                var r = _controller.Modificar(
                    id: _idEditar,
                    nombre: txtNombre.Text,
                    apellido: txtApellido.Text,
                    dni: txtDni.Text,
                    fechaNacimiento: fechaNac,
                    sexo: sexo,
                    telefono: txtTelefono.Text,
                    domicilio: txtDomicilio.Text,
                    profesion: txtProfesion.Text,
                    email: txtEmail.Text,
                    comoNosConocio: comoConocio,
                    observaciones: txtObservaciones.Text,
                    foto: _fotoBytes,
                    regenerarPin: regenerar);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Socio actualizado!");
            }

            CerrarFormulario();
            CargarSocios();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) => CerrarFormulario();

        // ─────────────────────────────────────────────────────
        // VALIDACIONES INLINE
        // (Validador ahora viene del namespace Controllers)
        // ─────────────────────────────────────────────────────
        private void txtNombre_LostFocus(object sender, RoutedEventArgs e)
            => AplicarEstadoCampo(txtNombre, errNombre,
               Controllers.Validador.ValidarNombre(txtNombre.Text, "El nombre"));

        private void txtApellido_LostFocus(object sender, RoutedEventArgs e)
            => AplicarEstadoCampo(txtApellido, errApellido,
               Controllers.Validador.ValidarNombre(txtApellido.Text, "El apellido"));

        private void txtDni_LostFocus(object sender, RoutedEventArgs e)
            => AplicarEstadoCampo(txtDni, errDni,
               Controllers.Validador.ValidarDni(txtDni.Text));

        private void txtEmail_LostFocus(object sender, RoutedEventArgs e)
            => AplicarEstadoCampo(txtEmail, errEmail,
               Controllers.Validador.ValidarEmail(txtEmail.Text));

        private void txtTelefono_LostFocus(object sender, RoutedEventArgs e)
            => AplicarEstadoCampo(txtTelefono, errTelefono,
               Controllers.Validador.ValidarTelefono(txtTelefono.Text));

        private void txtDni_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d$");
        }

        private void txtDni_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string texto = (string)e.DataObject.GetData(typeof(string));
                if (!Regex.IsMatch(texto, @"^\d+$")) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        private void txtTelefono_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Controllers.Validador.EsCaracterTelefonoValido(e.Text);
        }

        private void txtTelefono_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string texto = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
                var soloDigitos = new System.Text.StringBuilder();
                foreach (char c in texto)
                    if (char.IsDigit(c)) soloDigitos.Append(c);

                string resultado = soloDigitos.ToString();
                if (resultado.Length > 10)
                    resultado = resultado.Substring(0, 10);

                if (resultado.Length > 0)
                {
                    var tb = sender as TextBox;
                    if (tb != null)
                    {
                        tb.Text = resultado;
                        tb.CaretIndex = tb.Text.Length;
                    }
                }
                e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        private bool ValidarTodo()
        {
            bool ok = true;

            string e1 = Controllers.Validador.ValidarNombre(txtNombre.Text, "El nombre");
            AplicarEstadoCampo(txtNombre, errNombre, e1);
            if (e1 != null) { ok = false; CambiarTab("datos"); }

            string e2 = Controllers.Validador.ValidarNombre(txtApellido.Text, "El apellido");
            AplicarEstadoCampo(txtApellido, errApellido, e2);
            if (e2 != null) { ok = false; if (ok || _tabActivo != "datos") CambiarTab("datos"); }

            string e3 = Controllers.Validador.ValidarDni(txtDni.Text);
            AplicarEstadoCampo(txtDni, errDni, e3);
            if (e3 != null) { ok = false; CambiarTab("datos"); }

            if (dpNacimiento.SelectedDate.HasValue)
            {
                if (dpNacimiento.SelectedDate.Value > DateTime.Today)
                {
                    NotificacionWindow.MostrarError("La fecha de nacimiento no puede ser futura.");
                    CambiarTab("datos");
                    return false;
                }
            }

            string e4 = Controllers.Validador.ValidarTelefono(txtTelefono.Text);
            AplicarEstadoCampo(txtTelefono, errTelefono, e4);
            if (e4 != null) { ok = false; if (ok) CambiarTab("contacto"); }

            string e5 = Controllers.Validador.ValidarEmail(txtEmail.Text);
            AplicarEstadoCampo(txtEmail, errEmail, e5);
            if (e5 != null) { ok = false; if (ok) CambiarTab("contacto"); }

            return ok;
        }

        private void AplicarEstadoCampo(TextBox campo, TextBlock labelError, string mensajeError)
        {
            if (mensajeError != null)
            {
                campo.Style = (Style)Resources["InputErrorEstilo"];
                labelError.Text = mensajeError;
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
            TextBlock[] labels = { errNombre, errApellido, errDni, errEmail, errTelefono };
            TextBox[] campos = { txtNombre, txtApellido, txtDni, txtEmail, txtTelefono };

            foreach (var lbl in labels)
            { lbl.Text = string.Empty; lbl.Visibility = Visibility.Collapsed; }

            foreach (var c in campos)
                c.Style = (Style)Resources["InputEstilo"];
        }

        // ─────────────────────────────────────────────────────
        // ANIMACIONES DEL PANEL
        // ─────────────────────────────────────────────────────
        private void AbrirFormulario(string titulo)
        {
            lblTituloFormulario.Text = titulo;
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

            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(300))
            };
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
            txtApellido.Text = string.Empty;
            txtDni.Text = string.Empty;
            dpNacimiento.SelectedDate = null;
            cmbSexo.SelectedIndex = 2;
            txtTelefono.Text = string.Empty;
            txtEmail.Text = string.Empty;
            txtDomicilio.Text = string.Empty;
            txtProfesion.Text = string.Empty;
            cmbComoConocio.Text = string.Empty;
            txtObservaciones.Text = string.Empty;
            imgFotoFormulario.ImageSource = null;
            _fotoBytes = null;
            _idEditar = 0;
        }

        private SocioConMembresia ObtenerSocioDeFila(object sender)
        {
            var btn = sender as Button;
            if (btn == null) return null;
            return btn.DataContext as SocioConMembresia;
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