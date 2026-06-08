// ============================================================
//  Archivo: CajaPage.xaml.cs
//
//  Code-behind con:
//   · Dashboard del balance del día (ingresos / gastos / total)
//   · Gráfico de barras dinámico con los últimos 7 días
//   · Filtros por tipo (Todos / Ingresos / Gastos) y rango fechas
//   · Apertura de ventana emergente para Ingreso/Egreso
//
//  Compatible con C# 7.3.
// ============================================================

using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using SistemaGimnacionOptimusCAI.Ventanas;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class CajaPage : Page
    {
        private readonly CajaController _controller = new CajaController();

        private string _filtroTipo = "todos";

        private long USUARIO_ACTUAL_ID => SesionManager.UsuarioId;

        public CajaPage()
        {
            InitializeComponent();

            dpDesde.SelectedDate = DateTime.Today.AddDays(-30);
            dpHasta.SelectedDate = DateTime.Today;

            ConfigurarPermisosPorRol();
            ResaltarChip(chipTodos);
            CargarTodo();
        }

        // ─────────────────────────────────────────────────────
        // CARGA GENERAL
        // ─────────────────────────────────────────────────────
        private void CargarTodo()
        {
            CargarMovimientos();
            if (SesionManager.EsAdmin)
            {
                CargarDashboard();
                CargarGrafico7Dias();
            }
        }

        private void CargarMovimientos()
        {
            try
            {
                List<CajaMovimiento> lista;
                if (SesionManager.EsAdmin)
                {
                    lista = _controller.BuscarMovimientos(
                        txtBuscar.Text,
                        _filtroTipo,
                        dpDesde.SelectedDate,
                        dpHasta.SelectedDate);
                }
                else
                {
                    lista = _controller.BuscarMovimientosPorUsuario(
                        txtBuscar.Text,
                        "todos",
                        DateTime.Today,
                        DateTime.Today,
                        SesionManager.UsuarioId);
                }

                gridMovimientos.ItemsSource = lista;
                if (SesionManager.EsAdmin)
                    ActualizarFooterMovimientos(lista);
            }
            catch (Exception ex)
            {
                if (SesionManager.EsAdmin)
                    ActualizarFooterMovimientos(new List<CajaMovimiento>());
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar movimientos");
            }
        }

        private void ConfigurarPermisosPorRol()
        {
            if (SesionManager.EsAdmin) return;

            panelEstadisticasCaja.Visibility = Visibility.Collapsed;
            panelFiltrosCaja.Visibility = Visibility.Collapsed;
            footerTotalesCaja.Visibility = Visibility.Collapsed;
            colAccion.Visibility = Visibility.Collapsed;

            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today;
            _filtroTipo = "ingreso";

            lblSubtituloCaja.Text = "Ventas registradas hoy y carga de gastos";
            txtBuscar.Tag = "Buscar venta del dia...";
        }

        private void ActualizarFooterMovimientos(List<CajaMovimiento> movimientos)
        {
            int cantidad = movimientos != null ? movimientos.Count : 0;
            decimal ingresos = 0;
            decimal gastos = 0;

            if (movimientos != null)
            {
                foreach (var mov in movimientos)
                {
                    if (mov == null) continue;

                    if (mov.EsIngreso)
                        ingresos += mov.Monto;
                    else if (mov.EsGasto)
                        gastos += mov.Monto;
                }
            }

            decimal neto = ingresos - gastos;

            lblTotalRegistros.Text = cantidad == 1
                ? "1 movimiento"
                : cantidad + " movimientos";

            lblTotalIngresos.Text = "$" + ingresos.ToString("N0");
            lblTotalGastos.Text = "$" + gastos.ToString("N0");
            lblTotalNeto.Text = "$" + neto.ToString("N0");
            lblTotalNeto.Tag = neto >= 0 ? "positivo" : "negativo";
        }

        private void CargarDashboard()
        {
            if (!SesionManager.EsAdmin) return;

            try
            {
                var resumenDia = _controller.ResumenDelDia();
                lblIngresosDia.Text = "$" + resumenDia.TotalIngresos.ToString("N0");
                lblGastosDia.Text = "$" + resumenDia.TotalGastos.ToString("N0");
                lblBalanceDia.Text = resumenDia.BalanceTexto;

                lblBalanceDia.Foreground = resumenDia.Balance >= 0
                    ? new SolidColorBrush(Color.FromRgb(0, 230, 118))
                    : new SolidColorBrush(Color.FromRgb(255, 85, 85));

                var resumenMes = _controller.ResumenDelMes();
                lblBalanceMes.Text = resumenMes.BalanceTexto;
                lblMovimientosMes.Text = resumenMes.CantidadMovimientos + " movim.";

                lblBalanceMes.Foreground = resumenMes.Balance >= 0
                    ? new SolidColorBrush(Color.FromRgb(0, 207, 255))
                    : new SolidColorBrush(Color.FromRgb(255, 85, 85));
            }
            catch
            {
                lblBalanceDia.Text = lblIngresosDia.Text = lblGastosDia.Text = "$0";
                lblBalanceMes.Text = "$0";
            }
        }

        // ─────────────────────────────────────────────────────
        // GRÁFICO DE BARRAS — 7 días (solo admin)
        // ─────────────────────────────────────────────────────
        private void CargarGrafico7Dias()
        {
            if (!SesionManager.EsAdmin) return;

            try
            {
                gridChart.Children.Clear();
                gridChartLabels.Children.Clear();

                var datos = _controller.ObtenerUltimos7Dias();
                if (datos.Count == 0) return;

                decimal maxValor = 1;
                foreach (var d in datos)
                {
                    decimal m = Math.Max(d.Ingresos, d.Gastos);
                    if (m > maxValor) maxValor = m;
                }

                int columna = 0;
                foreach (var d in datos)
                {
                    var stack = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(2, 0, 2, 0)
                    };

                    double altIng = d.Ingresos > 0
                        ? (double)(d.Ingresos / maxValor) * 70 + 4
                        : 2;
                    var barraIng = new Border
                    {
                        Width = 10,
                        Height = altIng,
                        Background = new SolidColorBrush(Color.FromRgb(0, 230, 118)),
                        CornerRadius = new CornerRadius(3, 3, 0, 0),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 2, 0),
                        ToolTip = "Ingresos: $" + d.Ingresos.ToString("N0")
                    };

                    double altGas = d.Gastos > 0
                        ? (double)(d.Gastos / maxValor) * 70 + 4
                        : 2;
                    var barraGas = new Border
                    {
                        Width = 10,
                        Height = altGas,
                        Background = new SolidColorBrush(Color.FromRgb(255, 85, 85)),
                        CornerRadius = new CornerRadius(3, 3, 0, 0),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        ToolTip = "Gastos: $" + d.Gastos.ToString("N0")
                    };

                    stack.Children.Add(barraIng);
                    stack.Children.Add(barraGas);

                    Grid.SetColumn(stack, columna);
                    gridChart.Children.Add(stack);

                    var lbl = new TextBlock
                    {
                        Text = d.DiaSemana,
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontFamily = new FontFamily("Segoe UI")
                    };
                    Grid.SetColumn(lbl, columna);
                    gridChartLabels.Children.Add(lbl);

                    columna++;
                }
            }
            catch
            {
            }
        }

        // ─────────────────────────────────────────────────────
        // FILTROS
        // ─────────────────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => CargarMovimientos();

        private void dpFecha_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!SesionManager.EsAdmin) return;
            CargarMovimientos();
            CargarDashboard();
        }

        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionManager.EsAdmin) return;
            var btn = sender as Button;
            if (btn == null) return;
            _filtroTipo = btn.Tag.ToString();
            ResaltarChip(btn);
            CargarMovimientos();
        }

        private void ResaltarChip(Button seleccionado)
        {
            Button[] chips = { chipTodos, chipIngresos, chipGastos };
            foreach (var c in chips)
            {
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

        // ─────────────────────────────────────────────────────
        // BOTONES PRINCIPALES — ABREN VENTANA EMERGENTE
        // ─────────────────────────────────────────────────────
        private void btnIngreso_Click(object sender, RoutedEventArgs e)
        {
            var win = new MovimientoCajaWindow("ingreso");
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true)
            {
                CargarTodo();
            }
        }

        private void btnGasto_Click(object sender, RoutedEventArgs e)
        {
            var win = new MovimientoCajaWindow("gasto");
            win.Owner = Window.GetWindow(this);
            if (win.ShowDialog() == true)
            {
                CargarTodo();
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (!SesionManager.EsAdmin)
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Solo administradores pueden eliminar movimientos.",
                    "Acceso denegado");
                return;
            }

            var mov = ObtenerMovimientoDeFila(sender);
            if (mov == null) return;

            if (mov.Tipo == "ingreso_cuota" || mov.Tipo == "ingreso_venta")
            {
                NotificacionWindow.MostrarAdvertencia(
                    "Los ingresos por cuotas o ventas no se pueden eliminar acá. " +
                    "Cancelá la membresía o la venta desde su módulo correspondiente.",
                    "No permitido");
                return;
            }

            string concepto = mov.DescripcionInteligente;
            bool confirmo = NotificacionWindow.MostrarConfirmacion(
                "¿Eliminar este movimiento?\n\n" +
                "💰 " + mov.MontoConSigno + "\n" +
                "📝 " + concepto + "\n" +
                "📅 " + mov.FechaLarga + "\n\n" +
                "Esta acción no se puede deshacer.",
                "Eliminar movimiento");

            if (!confirmo) return;

            try
            {
                var r = _controller.EliminarMovimiento(mov.Id);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarTodo(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        // ─────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────
        private CajaMovimiento ObtenerMovimientoDeFila(object sender)
        {
            var btn = sender as Button;
            if (btn == null) return null;
            return btn.DataContext as CajaMovimiento;
        }

        private void btnRangoHoy_Click(object sender, RoutedEventArgs e)
        {
            dpDesde.SelectedDate = DateTime.Today;
            dpHasta.SelectedDate = DateTime.Today;
        }

        private void btnRangoSemana_Click(object sender, RoutedEventArgs e)
        {
            dpDesde.SelectedDate = DateTime.Today.AddDays(-6);
            dpHasta.SelectedDate = DateTime.Today;
        }

        private void btnRangoMes_Click(object sender, RoutedEventArgs e)
        {
            dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpHasta.SelectedDate = DateTime.Today;
        }
    }
}
