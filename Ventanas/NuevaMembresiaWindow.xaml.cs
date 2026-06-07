// ============================================================
//  Archivo: NuevaMembresiaWindow.xaml.cs
//
//  Ventana emergente de cambio/alta rápida de membresía para un
//  socio ya existente, directamente desde la ficha del socio.
//  Compatible con C# 7.3.
// ============================================================

using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class NuevaMembresiaWindow : Window
    {
        private readonly ActividadController _actividadCtrl = new ActividadController();
        private readonly UsuarioController   _usuarioCtrl   = new UsuarioController();
        private readonly MembresiaController _membresiaCtrl = new MembresiaController();

        private readonly Socio _socio;

        public bool MembresiaCreada { get; private set; }

        public NuevaMembresiaWindow(Socio socio)
        {
            InitializeComponent();
            _socio = socio;
            Loaded += (s, e) =>
            {
                MostrarSocio();
                CargarCombos();
                dpVencimiento.SelectedDate = DateTime.Today.AddDays(31);
            };
        }

        private void MostrarSocio()
        {
            if (_socio == null) return;
            lblSocio.Text       = _socio.NombreCompleto;
            lblNumeroSocio.Text = _socio.NumeroFormateado;
        }

        private void CargarCombos()
        {
            try
            {
                cmbActividad.ItemsSource = _actividadCtrl.ObtenerActividadesActivas();

                var instructores = _usuarioCtrl.ObtenerUsuariosActivosPorRol(2);
                var lista = new List<object>();
                lista.Add(new { NombreCompleto = "Ninguno", Id = (long?)null });
                foreach (var inst in instructores) lista.Add(inst);
                cmbInstructor.ItemsSource  = lista;
                cmbInstructor.SelectedIndex = 0;
            }
            catch { /* silencioso */ }
        }

        // ── Mostrar precio de la actividad seleccionada ───────
        private void cmbActividad_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var actividad = cmbActividad.SelectedItem as Actividad;
            if (actividad == null)
            {
                panelPrecio.Visibility = Visibility.Collapsed;
                return;
            }

            lblActividad.Text      = actividad.Nombre;
            lblPrecio.Text         = "$" + actividad.Precio.ToString("N0");
            panelPrecio.Visibility = Visibility.Visible;
        }

        // ── Confirmar (cobrar) ────────────────────────────────
        private void btnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            lblError.Visibility = Visibility.Collapsed;

            var actividad = cmbActividad.SelectedItem as Actividad;
            if (actividad == null)
            {
                MostrarError("Seleccioná una actividad para continuar.");
                return;
            }

            string metodoPago = "efectivo";
            var metodoItem = cmbMetodoPago.SelectedItem as ComboBoxItem;
            if (metodoItem != null && metodoItem.Tag != null)
                metodoPago = metodoItem.Tag.ToString();

            long? instructorId = null;
            if (cmbInstructor.SelectedIndex > 0)
            {
                var inst = cmbInstructor.SelectedItem as Usuario;
                if (inst != null) instructorId = inst.Id;
            }

            var r = _membresiaCtrl.Insertar(
                socioId:          _socio.Id,
                actividadId:      actividad.Id,
                instructorId:     instructorId,
                fechaInicio:      DateTime.Today,
                fechaVencimiento: DateTime.Today.AddDays(31),
                montoPagado:      actividad.Precio,
                metodoPago:       metodoPago,
                registradoPor:    SesionManager.HaySesion ? SesionManager.UsuarioId : 0L,
                observaciones:    null);

            if (!r.ok)
            {
                MostrarError(r.mensaje);
                return;
            }

            MembresiaCreada = true;
            NotificacionWindow.MostrarExito(r.mensaje, "¡Membresía registrada!");
            DialogResult = true;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) => Close();
        private void btnCerrar_Click(object sender, RoutedEventArgs e) => Close();

        private void MostrarError(string mensaje)
        {
            // Mostrar el error en ventana emergente, igual que el resto de los mensajes.
            NotificacionWindow.MostrarError(mensaje);
        }
    }
}
