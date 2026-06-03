// ============================================================
//  Archivo: ActividadesPage.xaml.cs
//  Módulo Actividades — C# 7.3 compatible
// ============================================================

using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using SistemaGimnacionOptimusCAI.Ventanas;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class ActividadesPage : Page
    {
        private readonly ActividadController _controller = new ActividadController();
        private string _filtroEstado = "todos";

        public ActividadesPage()
        {
            InitializeComponent();
            CargarActividades();
            ResaltarChip(chipTodos);
        }

        // ─────────────────────────────────────────────────────
        // CARGA + STATS
        // ─────────────────────────────────────────────────────
        private void CargarActividades()
        {
            try
            {
                var lista = _controller.BuscarActividades(txtBuscar.Text, _filtroEstado);
                gridActividades.ItemsSource = lista;
                ActualizarStats();
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar actividades");
            }
        }

        private void ActualizarStats()
        {
            try
            {
                var todos = _controller.ObtenerActividades();
                int total = todos.Count;
                int activos = 0;
                int sociosTotal = 0;

                foreach (var a in todos)
                {
                    if (a.Activo) activos++;
                    sociosTotal += a.CantSocios;
                }

                statTotal.Text = total.ToString();
                statActivos.Text = activos.ToString();
                statSociosTotal.Text = sociosTotal.ToString();
            }
            catch
            {
                statTotal.Text = statActivos.Text = statSociosTotal.Text = "-";
            }
        }

        // ─────────────────────────────────────────────────────
        // BÚSQUEDA / FILTROS
        // ─────────────────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => CargarActividades();

        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            _filtroEstado = btn.Tag.ToString();
            ResaltarChip(btn);
            CargarActividades();
        }

        private void ResaltarChip(Button seleccionado)
        {
            Button[] chips = { chipTodos, chipActivos, chipInactivos };
            foreach (var c in chips)
            {
                if (c == seleccionado)
                    c.Style = (Style)FindResource("BotonChipActivoEstilo");
                else
                    c.Style = (Style)FindResource("BotonChipEstilo");
            }
        }

        // ─────────────────────────────────────────────────────
        // BOTONES PRINCIPALES
        // ─────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var win = new ActividadWindow();
            win.Owner = Window.GetWindow(this);
            win.ModoNuevo();
            if (win.ShowDialog() == true)
            {
                CargarActividades();
            }
        }

        private void btnEditar_Click(object sender, RoutedEventArgs e)
        {
            var act = ObtenerActividadDeFila(sender);
            if (act == null) return;

            var win = new ActividadWindow();
            win.Owner = Window.GetWindow(this);
            win.ModoEditar(act);
            if (win.ShowDialog() == true)
            {
                CargarActividades();
            }
        }

        private void btnToggleEstado_Click(object sender, RoutedEventArgs e)
        {
            var act = ObtenerActividadDeFila(sender);
            if (act == null) return;

            bool nuevo = !act.Activo;
            string accion = nuevo ? "activar" : "desactivar";

            bool confirmo = NotificacionWindow.MostrarConfirmacion(
                "¿Querés " + accion + " la actividad \"" + act.Nombre + "\"?",
                "Confirmar cambio de estado");

            if (!confirmo) return;

            try
            {
                var r = _controller.CambiarEstado(act.Id, nuevo);
                if (r.ok) { NotificacionWindow.MostrarExito(r.mensaje); CargarActividades(); }
                else { NotificacionWindow.MostrarError(r.mensaje); }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        // ─────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────
        private Actividad ObtenerActividadDeFila(object sender)
        {
            var btn = sender as Button;
            if (btn == null) return null;
            return btn.DataContext as Actividad;
        }
    }
}