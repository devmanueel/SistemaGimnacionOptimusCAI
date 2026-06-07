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
using System.Threading.Tasks;
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
        private string _tabActivo = "datos";
        // Filtros avanzados (se aplican solo al presionar "Filtrar")
        private long?  _filtroActividadId  = null;
        private bool?  _filtroCuotaVencida = null;
        private long?  _filtroInstructorId = null;
        private string _filtroSexo         = null;
        private int?   _filtroDejaronVenir = null;

        // Ordenamiento
        private string _ordenamiento = "nombre_asc";

        // Paginación
        private int  _paginaActual = 1;
        private bool _hayMas       = true;
        private bool _cargando     = false;
        private bool _ignorarScroll = false;
        private bool _primeraCargaCompleta = false;
        private const int TAM_PAGINA = 8;

        public SociosPage()
        {
            InitializeComponent();
            ActualizarStats();
            CargarInstructores();
            CargarSocios();
            ResaltarChip(chipTodos);
            CambiarTab("datos");
            // Abrir el alta de socio diferido al evento Loaded: durante el
            // constructor la página aún no está adjunta a una ventana, por lo que
            // Window.GetWindow(this) devuelve null y el popup no puede centrarse
            // sobre su dueño (WindowStartupLocation="CenterOwner").
            bool abrirNuevoSocio = SesionManager.AbrirPanelAlNavegar;
            if (abrirNuevoSocio) SesionManager.AbrirPanelAlNavegar = false;

            Loaded += (s, e) =>
            {
                SuscribirScrollDataGrid();
                if (abrirNuevoSocio)
                {
                    abrirNuevoSocio = false;
                    btnNuevo_Click(null, null);
                }
            };
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
            _paginaActual = 1;
            _hayMas       = true;
            _primeraCargaCompleta = false;
            _ignorarScroll = true;
            RestablecerScrollSocios();
            ConfigurarColumnasGrid();
            CargarSociosPagina(1, agregar: false);
        }

        public void RefrescarListadoYStats()
        {
            ActualizarStats();
            CargarSocios();
        }

        private async void CargarSociosPagina(int pagina, bool agregar)
        {
            if (_cargando) return;
            _cargando = true;

            if (panelCargando != null)
                panelCargando.Visibility = Visibility.Visible;

            // Si es paginación infinita, mostrar delay antes de cargar
            if (agregar)
            {
                await Task.Delay(1200);
            }

            try
            {
                var resultado = _controller.ListarSociosConMembresias(
                    texto:              txtBuscar != null ? txtBuscar.Text.Trim() : "",
                    filtroEstado:       _filtroEstado,
                    filtroActividadId:  _filtroActividadId,
                    filtroCuotaVencida: _filtroCuotaVencida,
                    filtroInstructorId: _filtroInstructorId,
                    filtroSexo:         _filtroSexo,
                    filtroDejaronVenir: _filtroDejaronVenir,
                    pagina:             pagina,
                    tamPagina:          TAM_PAGINA);

                _hayMas = resultado.HayMas;

                // Ignorar disparos automáticos de ScrollChanged durante el re-renderizado
                _ignorarScroll = true;

                // Guardar posición del scroll antes de agregar items
                double scrollOffset = 0;
                if (agregar)
                {
                    var sv = ObtenerScrollViewer(gridSocios);
                    if (sv != null)
                        scrollOffset = sv.VerticalOffset;
                }

                if (agregar)
                {
                    var listaActual = gridSocios.ItemsSource as List<SocioConMembresia>
                                      ?? new List<SocioConMembresia>();
                    listaActual.AddRange(resultado.Items);

                    gridSocios.ItemsSource = null;
                    gridSocios.ItemsSource = listaActual;

                    // Restaurar posición del scroll después de agregar
                    _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var sv = ObtenerScrollViewer(gridSocios);
                        if (sv != null)
                            sv.ScrollToVerticalOffset(scrollOffset);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                else
                {
                    gridSocios.ItemsSource = resultado.Items;
                    ActualizarContadoresChipsConTotal(resultado.Total, resultado.Items);
                }

                ActualizarResumenFiltros(resultado.Items);
                AplicarOrdenamiento();
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar socios");
            }
            finally
            {
                // Si es la primera carga, marcar como completa
                if (!agregar)
                {
                    _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        RestablecerScrollSocios();
                        _primeraCargaCompleta = true;
                        _ignorarScroll = false;
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                else
                {
                    _ignorarScroll = false;
                }

                _cargando = false;
                if (panelCargando != null)
                    panelCargando.Visibility = Visibility.Collapsed;
            }
        }

        private void RestablecerScrollSocios()
        {
            var sv = ObtenerScrollViewer(gridSocios);
            if (sv != null)
                sv.ScrollToTop();
        }

        private void SuscribirScrollDataGrid()
        {
            // Esperar a que el DataGrid termine de renderizar su template
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                var scrollViewer = ObtenerScrollViewer(gridSocios);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollChanged += OnScrollChanged;
                }
                else
                {
                    // Fallback: intentar nuevamente en el LayoutUpdated
                    EventHandler handler = null;
                    handler = (s, e) =>
                    {
                        scrollViewer = ObtenerScrollViewer(gridSocios);
                        if (scrollViewer != null)
                        {
                            gridSocios.LayoutUpdated -= handler;
                            scrollViewer.ScrollChanged += OnScrollChanged;
                        }
                    };
                    gridSocios.LayoutUpdated += handler;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static ScrollViewer ObtenerScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer) return (ScrollViewer)obj;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                var result = ObtenerScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Ignorar hasta que la primera carga termine completamente
            if (!_primeraCargaCompleta) return;

            // Ignorar si estamos cargando o si el scroll fue automático (re-renderizado)
            if (_ignorarScroll || _cargando) return;

            // Solo responder a scroll manual del usuario (VerticalChange != 0)
            if (e.VerticalChange == 0) return;

            bool llegoAlFinal = e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 50;

            if (llegoAlFinal && _hayMas)
            {
                _paginaActual++;
                CargarSociosPagina(_paginaActual, agregar: true);
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

        private void ActualizarContadoresChipsConTotal(int total, List<SocioConMembresia> itemsPagina)
        {
            try
            {
                var todosParaContar = _controller.ListarSociosConMembresias(
                    texto:              txtBuscar != null ? txtBuscar.Text.Trim() : "",
                    filtroEstado:       "todos",
                    filtroActividadId:  _filtroActividadId,
                    filtroCuotaVencida: _filtroCuotaVencida,
                    filtroInstructorId: _filtroInstructorId,
                    filtroSexo:         _filtroSexo,
                    filtroDejaronVenir: _filtroDejaronVenir,
                    pagina:             1,
                    tamPagina:          99999);

                int totalActivos   = 0;
                int totalInactivos = 0;
                foreach (var s in todosParaContar.Items)
                {
                    if (s.Activo) totalActivos++;
                    else totalInactivos++;
                }

                chipTodosNum.Text     = "(" + todosParaContar.Total + ")";
                chipActivosNum.Text   = "(" + totalActivos + ")";
                chipInactivosNum.Text = "(" + totalInactivos + ")";
            }
            catch
            {
                chipTodosNum.Text = chipActivosNum.Text = chipInactivosNum.Text = "(0)";
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
                partes.Add(string.Format("que no asisten hace {0} días o más, o que nunca asistieron desde una membresía iniciada hace {0} días o más", _filtroDejaronVenir.Value));

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
                    {
                        string tagSexo = itemSexo.Tag?.ToString();
                        _filtroSexo = string.IsNullOrEmpty(tagSexo) ? null : tagSexo;
                    }
                    break;

                case "Dejaron de venir":
                    if (cmbFiltroDias.SelectedItem is ComboBoxItem itemDias
                        && int.TryParse(itemDias.Tag?.ToString(), out int dias)
                        && dias > 0)
                        _filtroDejaronVenir = dias;
                    break;
            }

            // Limpiar filas ANTES de tocar columnas (WPF no permite modificar
            // columnas mientras hay celdas materializadas).
            gridSocios.ItemsSource = null;
            ConfigurarColumnasGrid();
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

            // Limpiar filas ANTES de tocar columnas
            gridSocios.ItemsSource = null;
            ConfigurarColumnasGrid();
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

            bool dejaronVenirActivo = _filtroDejaronVenir.HasValue;

            if (colTelefono != null)
            {
                colTelefono.Header = dejaronVenirActivo ? "ÚLTIMA ASISTENCIA" : "TELÉFONO";
                colTelefono.Binding = new Binding(dejaronVenirActivo ? "UltimaAsistenciaTexto" : "Telefono");
                colTelefono.Width = dejaronVenirActivo ? new DataGridLength(130) : new DataGridLength(100);
            }

            if (colActividad != null)
            {
                colActividad.Header = dejaronVenirActivo ? "DÍAS SIN ASISTIR" : "ACTIVIDAD";
                colActividad.CellTemplate = (DataTemplate)FindResource(dejaronVenirActivo
                    ? "DiasSinAsistirCellTemplate"
                    : "ActividadSocioCellTemplate");
                colActividad.Width = dejaronVenirActivo
                    ? new DataGridLength(130)
                    : new DataGridLength(1.5, DataGridLengthUnitType.Star);
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
                    RefrescarListadoYStats();
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

        private void btnGestionarSocio_Click(object sender, RoutedEventArgs e)
        {
            var fila = ObtenerSocioDeFila(sender);
            if (fila == null) return;

            try
            {
                var socio = _controller.ObtenerPorId(fila.Id);
                if (socio == null)
                {
                    NotificacionWindow.MostrarAdvertencia("No se encontró el socio.");
                    return;
                }

                var ficha = new FichaSocioWindow(socio) { Owner = Window.GetWindow(this) };
                ficha.ShowDialog();

                if (ficha.HuboCambiosSocio)
                {
                    CargarSocios();
                    ActualizarStats();
                }
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al abrir la ficha del socio.\n" + ex.Message);
            }
        }

        private void btnEditarMembresia_Click(object sender, RoutedEventArgs e)
        {
            var socio = ObtenerSocioDeFila(sender);
            if (socio == null) return;
            if (socio.MembresiaId <= 0)
            {
                NotificacionWindow.MostrarAdvertencia("Este socio no tiene una membresia asignada.");
                return;
            }
            if (socio.MembresiaEstado != "activa")
            {
                NotificacionWindow.MostrarAdvertencia("Solo se puede editar/upgrade una membresia activa.");
                return;
            }
            var win = new Ventanas.MembresiaWindow { Owner = Window.GetWindow(this) };
            win.Configurar(socio.MembresiaId);
            if (win.ShowDialog() == true)
            {
                CargarSocios();
                ActualizarStats();
            }
        }

        private void btnRenovarMembresia_Click(object sender, RoutedEventArgs e)
        {
            var socio = ObtenerSocioDeFila(sender);
            if (socio == null) return;
            if (socio.MembresiaId <= 0)
            {
                NotificacionWindow.MostrarAdvertencia("Este socio no tiene una membresia asignada.");
                return;
            }
            if (socio.MembresiaEstado != "activa")
            {
                NotificacionWindow.MostrarAdvertencia("Este boton solo renueva membresias activas. Para vencidas usa Renovar vencida.");
                return;
            }

            var win = new Ventanas.MembresiaWindow { Owner = Window.GetWindow(this) };
            win.ConfigurarRenovacion(socio.MembresiaId, false);
            if (win.ShowDialog() == true)
            {
                CargarSocios();
                ActualizarStats();
            }
        }

        private void btnRenovarMembresiaVencida_Click(object sender, RoutedEventArgs e)
        {
            var socio = ObtenerSocioDeFila(sender);
            if (socio == null) return;
            if (socio.MembresiaId <= 0)
            {
                NotificacionWindow.MostrarAdvertencia("Este socio no tiene una membresia asignada.");
                return;
            }
            if (socio.MembresiaEstado != "vencida")
            {
                NotificacionWindow.MostrarAdvertencia("Esta accion solo aplica a membresias vencidas.");
                return;
            }

            var win = new Ventanas.MembresiaWindow { Owner = Window.GetWindow(this) };
            win.ConfigurarRenovacion(socio.MembresiaId, true);
            if (win.ShowDialog() == true)
            {
                CargarSocios();
                ActualizarStats();
            }
        }

        private void btnAltaDesdeCancelada_Click(object sender, RoutedEventArgs e)
        {
            var socio = ObtenerSocioDeFila(sender);
            if (socio == null) return;
            if (socio.MembresiaId <= 0)
            {
                NotificacionWindow.MostrarAdvertencia("Este socio no tiene una membresia asignada.");
                return;
            }
            if (socio.MembresiaEstado != "cancelada")
            {
                NotificacionWindow.MostrarAdvertencia("Esta accion solo aplica a membresias canceladas.");
                return;
            }

            var win = new Ventanas.MembresiaWindow { Owner = Window.GetWindow(this) };
            win.ConfigurarRenovacion(socio.MembresiaId, true);
            if (win.ShowDialog() == true)
            {
                CargarSocios();
                ActualizarStats();
            }
        }

        private void btnCancelarMembresia_Click(object sender, RoutedEventArgs e)
        {
            var socio = ObtenerSocioDeFila(sender);
            if (socio == null) return;
            if (socio.MembresiaId <= 0)
            {
                NotificacionWindow.MostrarAdvertencia("Este socio no tiene una membresia asignada.");
                return;
            }
            if (socio.MembresiaEstado == "cancelada")
            {
                NotificacionWindow.MostrarAdvertencia("Esta membresia ya esta cancelada.");
                return;
            }

            bool confirmo = NotificacionWindow.MostrarConfirmacion(
                "¿Queres cancelar la membresia de " + socio.NombreCompleto +
                " — " + socio.ActividadNombre + "?\n\n" +
                "El registro se conserva pero queda en estado 'cancelada'.",
                "Cancelar membresia");

            if (!confirmo) return;

            try
            {
                var r = _membresiaController.Cancelar(socio.MembresiaId, SesionManager.UsuarioId);
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

                if (dpNacimiento.SelectedDate.Value.Date > DateTime.Today.AddYears(-6))
                {
                    NotificacionWindow.MostrarError("El socio debe tener al menos 6 años para ser registrado.");
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
