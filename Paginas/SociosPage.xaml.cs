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
using System.Linq;
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
        private readonly MembresiaController _membresiaController = new MembresiaController();

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private byte[] _fotoBytes = null;
        private string _filtroEstado = "todos";
        private string _filtroAvanzado = "todos";
        private string _tabActivo = "datos";
        private List<DataGridColumn> _columnasDinamicas = new List<DataGridColumn>();

        // Filtros avanzados (se aplican solo al presionar "Filtrar")
        private long?  _filtroActividadId  = null;
        private bool?  _filtroCuotaVencida = null;
        private long?  _filtroInstructorId = null;
        private string _filtroSexo         = null;
        private int?   _filtroDejaronVenir = null;

        // Ordenamiento
        private string _ordenamiento = "nombre_asc";

        // Edición de membresía
        private long _membresiaIdEditar = 0;
        private long _membresiaActividadActualId = 0;
        private string _membresiaActividadActualCategoria = null;
        private int? _membresiaActividadActualNivel = null;

        public SociosPage()
        {
            InitializeComponent();
            ActualizarStats();
            CargarInstructores();
            CargarCombosMembresia();
            CargarSocios();
            ResaltarChip(chipTodos);
            CambiarTab("datos");
            if (SesionManager.AbrirPanelAlNavegar)
            {
                SesionManager.AbrirPanelAlNavegar = false;
                btnNuevo_Click(null, null);
            }
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
                AplicarOrdenamiento();
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

            // Resetear ordenamiento
            _ordenamiento = "nombre_asc";
            if (cmbOrdenarPor != null) cmbOrdenarPor.SelectedIndex = 0;

            CargarSocios();
        }

        private void AplicarFiltroAvanzado(object sender, SelectionChangedEventArgs e) { }
        private void AplicarFiltroAvanzado(object sender, RoutedEventArgs e) { }

        private void cmbOrdenarPor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbOrdenarPor == null) return;

            var item = cmbOrdenarPor.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag != null)
                _ordenamiento = item.Tag.ToString();

            AplicarOrdenamiento();
        }

        private void AplicarOrdenamiento()
        {
            var lista = gridSocios?.ItemsSource as List<SocioConMembresia>;
            if (lista == null || lista.Count == 0) return;

            List<SocioConMembresia> ordenada;

            switch (_ordenamiento)
            {
                case "nombre_desc":
                    ordenada = new List<SocioConMembresia>(
                        ((List<SocioConMembresia>)gridSocios.ItemsSource)
                            .OrderByDescending(x => x.NombreCompleto));
                    break;

                case "vencimiento_desc":
                    ordenada = new List<SocioConMembresia>(
                        ((List<SocioConMembresia>)gridSocios.ItemsSource)
                            .OrderByDescending(x => x.FechaVencimiento.HasValue)
                            .ThenByDescending(x => x.FechaVencimiento));
                    break;

                case "vencimiento_asc":
                    ordenada = new List<SocioConMembresia>(
                        ((List<SocioConMembresia>)gridSocios.ItemsSource)
                            .OrderBy(x => x.FechaVencimiento.HasValue)
                            .ThenBy(x => x.FechaVencimiento));
                    break;

                default: // nombre_asc
                    ordenada = new List<SocioConMembresia>(
                        ((List<SocioConMembresia>)gridSocios.ItemsSource)
                            .OrderBy(x => x.NombreCompleto));
                    break;
            }

            gridSocios.ItemsSource = ordenada;
        }

        // ─────────────────────────────────────────────────────
        // EXPORTAR / IMPRIMIR
        // ─────────────────────────────────────────────────────
        private void btnExportarPdf_Click(object sender, RoutedEventArgs e)
        {
            var socios = gridSocios?.ItemsSource as List<SocioConMembresia>;
            if (socios == null || socios.Count == 0)
            {
                NotificacionWindow.MostrarAdvertencia("No hay socios para exportar.", "Sin datos");
                return;
            }

            try
            {
                var exp = new Helpers.ReportePdfExportador();
                string path = exp.ExportarSocios(socios);
                System.Diagnostics.Process.Start(path);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo exportar a PDF.\n" + ex.Message, "Error");
            }
        }

        private void btnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            var socios = gridSocios?.ItemsSource as List<SocioConMembresia>;
            if (socios == null || socios.Count == 0)
            {
                NotificacionWindow.MostrarAdvertencia("No hay socios para exportar.", "Sin datos");
                return;
            }

            try
            {
                var exp = new Helpers.ReporteExcelExportador();
                string path = exp.ExportarSocios(socios);
                System.Diagnostics.Process.Start(path);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo exportar a Excel.\n" + ex.Message, "Error");
            }
        }

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

            AbrirPanelMembresia(socio.MembresiaId);
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

        // ═════════════════════════════════════════════════════
        //  PANEL LATERAL — EDICIÓN DE MEMBRESÍA
        // ═════════════════════════════════════════════════════

        private void CargarCombosMembresia()
        {
            try
            {
                var actividades = _membresiaController.ListarActividadesParaCombo();
                cmbMembresiaActividad.ItemsSource = actividades;

                var instructores = _usuarioController.ObtenerUsuariosActivosPorRol(2);
                var listaInstructores = new List<object>();
                listaInstructores.Add(new { NombreCompleto = "Ninguno", Id = (long?)null });
                foreach (var inst in instructores)
                    listaInstructores.Add(inst);
                cmbMembresiaInstructor.ItemsSource = listaInstructores;
            }
            catch { /* silencioso */ }
        }

        private void AbrirPanelMembresia(long membresiaId)
        {
            try
            {
                var m = _membresiaController.ObtenerPorId(membresiaId);
                if (m == null)
                {
                    NotificacionWindow.MostrarError("No se encontró la membresía.");
                    return;
                }

                // Precargar controles
                LimpiarFormularioMembresia();
                LimpiarErroresMembresia();

                _membresiaIdEditar = m.Id;
                _membresiaActividadActualId = m.ActividadId;
                _membresiaActividadActualCategoria = m.ActividadCategoria;
                _membresiaActividadActualNivel = m.ActividadNivel;

                // Socio (solo lectura)
                var listaSocio = new List<object>();
                listaSocio.Add(new { NombreCompleto = m.SocioNombre, Id = m.SocioId });
                cmbMembresiaSocio.ItemsSource = listaSocio;
                cmbMembresiaSocio.SelectedIndex = 0;

                // Actividad
                foreach (var item in cmbMembresiaActividad.Items)
                {
                    var act = item as ActividadComboItem;
                    if (act != null && act.Id == m.ActividadId)
                    { cmbMembresiaActividad.SelectedItem = item; break; }
                }

                // Instructor
                if (m.InstructorId.HasValue)
                {
                    for (int i = 0; i < cmbMembresiaInstructor.Items.Count; i++)
                    {
                        var inst = cmbMembresiaInstructor.Items[i] as Usuario;
                        if (inst != null && inst.Id == m.InstructorId.Value)
                        { cmbMembresiaInstructor.SelectedIndex = i; break; }
                    }
                }
                else
                {
                    cmbMembresiaInstructor.SelectedIndex = 0;
                }

                // Fechas (no editables)
                dpMembresiaInicio.SelectedDate = m.FechaInicio;
                dpMembresiaVencimiento.SelectedDate = m.FechaVencimiento;

                // Monto
                txtMembresiaMonto.Text = m.MontoPagado.ToString("F0");

                // Método de pago
                foreach (ComboBoxItem mp in cmbMembresiaMetodoPago.Items)
                {
                    if (mp.Tag != null && mp.Tag.ToString() == m.MetodoPago)
                    { cmbMembresiaMetodoPago.SelectedItem = mp; break; }
                }

                // Observaciones
                txtMembresiaObservaciones.Text = m.Observaciones ?? string.Empty;

                // Abrir panel
                panelFormularioMembresia.Visibility = Visibility.Visible;
                panelFormularioMembresia.Opacity = 0;

                var translate = new TranslateTransform { X = 60 };
                panelFormularioMembresia.RenderTransform = translate;

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
                panelFormularioMembresia.BeginAnimation(OpacityProperty, fade);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al cargar la membresía.\n" + ex.Message);
            }
        }

        private void CerrarPanelMembresia()
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
                panelFormularioMembresia.Visibility = Visibility.Collapsed;
                LimpiarFormularioMembresia();
                LimpiarErroresMembresia();
                _membresiaIdEditar = 0;
            };
            panelFormularioMembresia.BeginAnimation(OpacityProperty, fade);
        }

        private void btnCancelarMembresia_Click(object sender, RoutedEventArgs e)
        {
            CerrarPanelMembresia();
        }

        private void btnGuardarMembresia_Click(object sender, RoutedEventArgs e)
        {
            if (_membresiaIdEditar <= 0) return;

            // Validar actividad
            var actividad = cmbMembresiaActividad.SelectedItem as ActividadComboItem;
            if (actividad == null)
            {
                NotificacionWindow.MostrarAdvertencia("Seleccioná una actividad.");
                return;
            }

            // Validar monto
            if (!decimal.TryParse(txtMembresiaMonto.Text.Trim(), out decimal monto) || monto <= 0)
            {
                errMembresiaMonto.Text = "El monto debe ser mayor a $0.";
                errMembresiaMonto.Visibility = Visibility.Visible;
                txtMembresiaMonto.BorderBrush = System.Windows.Media.Brushes.Red;
                txtMembresiaMonto.BorderThickness = new Thickness(1.5);
                return;
            }

            // Instructor
            long? instructorId = null;
            if (cmbMembresiaInstructor.SelectedIndex > 0)
            {
                var inst = cmbMembresiaInstructor.SelectedItem as Usuario;
                if (inst != null) instructorId = inst.Id;
            }

            // Método de pago
            var metodoItem = cmbMembresiaMetodoPago.SelectedItem as ComboBoxItem;
            string metodoPago = metodoItem?.Tag?.ToString() ?? "efectivo";

            // Verificar si es upgrade
            bool esUpgrade = actividad.Id != _membresiaActividadActualId
                && !string.IsNullOrEmpty(_membresiaActividadActualCategoria)
                && actividad.Categoria == _membresiaActividadActualCategoria
                && _membresiaActividadActualNivel.HasValue
                && actividad.Nivel.HasValue
                && actividad.Nivel.Value > _membresiaActividadActualNivel.Value;

            if (esUpgrade)
            {
                decimal diferencia = Math.Abs(actividad.Precio - ObtenerPrecioActividadCombo(_membresiaActividadActualId));

                bool confirmo = NotificacionWindow.MostrarConfirmacion(
                    "Vas a hacer un upgrade de membresía:\n\n" +
                    "📋 " + ObtenerNombreActividadCombo(_membresiaActividadActualId) + " → " + actividad.Nombre + "\n" +
                    "💰 Diferencia a cobrar: $" + diferencia.ToString("N0") + "\n" +
                    "💳 Método: " + metodoPago + "\n\n" +
                    "⚠️ Solo se permite un upgrade por membresía.\n\n" +
                    "¿Confirmás el upgrade y el cobro?",
                    "Confirmar upgrade");

                if (!confirmo) return;

                try
                {
                    var r = _membresiaController.EjecutarUpgrade(
                        _membresiaIdEditar, actividad.Id, metodoPago,
                        SesionManager.HaySesion ? SesionManager.UsuarioId : 0);

                    if (r == null)
                    {
                        NotificacionWindow.MostrarError("No se pudo ejecutar el upgrade.");
                        return;
                    }

                    NotificacionWindow.MostrarExito(
                        "Upgrade realizado correctamente.\nMonto cobrado: $" + r.MontoCobrado.ToString("N0"),
                        "¡Upgrade exitoso!");
                }
                catch (Exception ex)
                {
                    NotificacionWindow.MostrarError(ex.Message);
                    return;
                }
            }
            else
            {
                // Validación de cambio de plan no permitido
                if (actividad.Id != _membresiaActividadActualId)
                {
                    if (!string.IsNullOrEmpty(_membresiaActividadActualCategoria) &&
                        !string.IsNullOrEmpty(actividad.Categoria) &&
                        _membresiaActividadActualCategoria != actividad.Categoria)
                    {
                        NotificacionWindow.MostrarError(
                            "No se puede cambiar a otra categoría. El cambio de plan solo está permitido dentro de la misma categoría.");
                        return;
                    }

                    if (_membresiaActividadActualNivel.HasValue && actividad.Nivel.HasValue &&
                        actividad.Nivel.Value <= _membresiaActividadActualNivel.Value)
                    {
                        NotificacionWindow.MostrarError(
                            "Solo se permite cambiar a un plan superior (upgrade). El downgrade no está permitido.");
                        return;
                    }
                }

                // Modificación normal
                long? actividadEditada = actividad.Id;
                decimal? montoParam = monto > 0 ? (decimal?)monto : null;
                DateTime fechaVenc = dpMembresiaVencimiento.SelectedDate ?? DateTime.Today.AddDays(31);

                var r = _membresiaController.Modificar(
                    _membresiaIdEditar, instructorId, fechaVenc,
                    txtMembresiaObservaciones.Text.Trim(),
                    SesionManager.HaySesion ? SesionManager.UsuarioId : 0,
                    actividadEditada, montoParam, "mensual", metodoPago);

                if (!r.ok)
                {
                    NotificacionWindow.MostrarError(r.mensaje);
                    return;
                }

                NotificacionWindow.MostrarExito(r.mensaje, "¡Actualizado!");
            }

            CerrarPanelMembresia();
            CargarSocios();
            ActualizarStats();
        }

        private void cmbMembresiaActividad_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var act = cmbMembresiaActividad.SelectedItem as ActividadComboItem;
            if (act == null) return;

            if (_membresiaIdEditar <= 0)
            {
                // Modo nuevo (no debería ocurrir en este panel, pero por seguridad)
                panelUpgrade.Visibility = Visibility.Collapsed;
                return;
            }

            // Modo editar: verificar si es upgrade
            if (act.Id != _membresiaActividadActualId
                && !string.IsNullOrEmpty(_membresiaActividadActualCategoria)
                && act.Categoria == _membresiaActividadActualCategoria
                && _membresiaActividadActualNivel.HasValue
                && act.Nivel.HasValue
                && act.Nivel.Value > _membresiaActividadActualNivel.Value)
            {
                // Es un upgrade — calcular diferencia
                decimal precioActual = 0;
                foreach (var item in cmbMembresiaActividad.Items)
                {
                    var a = item as ActividadComboItem;
                    if (a != null && a.Id == _membresiaActividadActualId)
                    {
                        precioActual = a.Precio;
                        break;
                    }
                }

                decimal diferencia = Math.Abs(act.Precio - precioActual);

                // Mostrar panel upgrade
                lblUpgradeDetalle.Text = "Diferencia a cobrar (" +
                    ObtenerNombreActividadCombo(_membresiaActividadActualId) + " → " + act.Nombre + "):";
                lblUpgradeMonto.Text = "$" + diferencia.ToString("N0");
                lblUpgradeNivel.Text = "⬆ Nivel " + _membresiaActividadActualNivel.Value +
                                         " → " + act.Nivel.Value;
                panelUpgrade.Visibility = Visibility.Visible;

                // Poner la diferencia en el campo monto
                txtMembresiaMonto.Text = diferencia.ToString("F0");
            }
            else if (act.Id == _membresiaActividadActualId)
            {
                // Volvió a la actividad original — ocultar upgrade
                panelUpgrade.Visibility = Visibility.Collapsed;
            }
            else
            {
                panelUpgrade.Visibility = Visibility.Collapsed;
            }
        }

        private string ObtenerNombreActividadCombo(long actividadId)
        {
            foreach (var item in cmbMembresiaActividad.Items)
            {
                var a = item as ActividadComboItem;
                if (a != null && a.Id == actividadId) return a.Nombre;
            }
            return "actividad actual";
        }

        private decimal ObtenerPrecioActividadCombo(long actividadId)
        {
            foreach (var item in cmbMembresiaActividad.Items)
            {
                var a = item as ActividadComboItem;
                if (a != null && a.Id == actividadId) return a.Precio;
            }
            return 0;
        }

        private void txtMembresiaMonto_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[\d,\.]$");
        }

        private void txtMembresiaMonto_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtMembresiaMonto.Text.Trim(), out decimal monto) || monto <= 0)
            {
                errMembresiaMonto.Text = "El monto debe ser mayor a $0.";
                errMembresiaMonto.Visibility = Visibility.Visible;
                txtMembresiaMonto.BorderBrush = System.Windows.Media.Brushes.Red;
                txtMembresiaMonto.BorderThickness = new Thickness(1.5);
            }
            else
            {
                LimpiarErroresMembresia();
            }
        }

        private void LimpiarFormularioMembresia()
        {
            cmbMembresiaSocio.ItemsSource = null;
            cmbMembresiaActividad.SelectedIndex = -1;
            cmbMembresiaInstructor.SelectedIndex = 0;
            dpMembresiaInicio.SelectedDate = null;
            dpMembresiaVencimiento.SelectedDate = null;
            txtMembresiaMonto.Text = string.Empty;
            cmbMembresiaMetodoPago.SelectedIndex = 0;
            txtMembresiaObservaciones.Text = string.Empty;
            panelUpgrade.Visibility = Visibility.Collapsed;
            _membresiaIdEditar = 0;
            _membresiaActividadActualId = 0;
            _membresiaActividadActualCategoria = null;
            _membresiaActividadActualNivel = null;
        }

        private void LimpiarErroresMembresia()
        {
            errMembresiaMonto.Text = string.Empty;
            errMembresiaMonto.Visibility = Visibility.Collapsed;
            txtMembresiaMonto.ClearValue(TextBox.BorderBrushProperty);
            txtMembresiaMonto.ClearValue(TextBox.BorderThicknessProperty);
        }
    }
}