// ============================================================
//  Archivo: NuevoSocioWindow.xaml.cs
//
//  Ventana emergente de 2 pasos para crear socio + membresía.
//  Paso 1: datos del socio → guarda con sp_InsertarSocio
//  Paso 2: membresía → guarda con sp_InsertarMembresia
//  Compatible con C# 7.3
// ============================================================

using Controllers;
using Entities;
using Microsoft.Win32;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class NuevoSocioWindow : Window
    {
        // ── Controllers ───────────────────────────────────────
        private readonly SocioController      _socioCtrl      = new SocioController();
        private readonly ActividadController  _actividadCtrl  = new ActividadController();
        private readonly UsuarioController    _usuarioCtrl    = new UsuarioController();
        private readonly MembresiaController  _membresiaCtrl  = new MembresiaController();

        // ── Estado interno ────────────────────────────────────
        private int    _pasoActual  = 1;
        private long   _socioId     = 0;
        private int    _numeroSocio = 0;
        private byte[] _fotoBytes   = null;

        public NuevoSocioWindow()
        {
            InitializeComponent();
            CargarCombos();
            dpVencimiento.SelectedDate = DateTime.Today.AddDays(31);
        }

        // ── Carga de combos ───────────────────────────────────
        private void CargarCombos()
        {
            try
            {
                // Actividades activas
                var actividades = _actividadCtrl.ObtenerActividadesActivas();
                cmbActividad.ItemsSource = actividades;

                // Instructores (rolId = 2)
                var instructores = _usuarioCtrl.ObtenerUsuariosActivosPorRol(2);
                // Agregar opción "Ninguno" al inicio
                var listaInstructores = new List<object>();
                listaInstructores.Add(new { NombreCompleto = "Ninguno", Id = (long?)null });
                foreach (var inst in instructores)
                    listaInstructores.Add(inst);
                cmbInstructor.ItemsSource  = listaInstructores;
                cmbInstructor.SelectedIndex = 0;
            }
            catch { /* silencioso */ }
        }

        // ── Botón principal (Siguiente / Cobrar) ──────────────
        private void btnAccion_Click(object sender, RoutedEventArgs e)
        {
            if (_pasoActual == 1)
                EjecutarPaso1();
            else
                EjecutarPaso2();
        }

        // ── PASO 1: validar y guardar socio ───────────────────
        private void EjecutarPaso1()
        {
            if (!ValidarPaso1()) return;

            string sexo = "Otro";
            var sexoItem = cmbSexo.SelectedItem as ComboBoxItem;
            if (sexoItem?.Tag != null) sexo = sexoItem.Tag.ToString();

            string comoConocio = (cmbComoConocio.SelectedItem as ComboBoxItem)?.Content?.ToString()
                                 ?? string.Empty;

            var resultado = _socioCtrl.Insertar(
                nombre:          txtNombre.Text.Trim(),
                apellido:        txtApellido.Text.Trim(),
                dni:             txtDni.Text.Trim(),
                fechaNacimiento: dpNacimiento.SelectedDate,
                sexo:            sexo,
                telefono:        txtTelefono.Text.Trim(),
                domicilio:       txtDomicilio.Text.Trim(),
                profesion:       txtProfesion.Text.Trim(),
                email:           txtEmail.Text.Trim(),
                comoNosConocio:  comoConocio,
                observaciones:   txtObservaciones.Text.Trim(),
                foto:            _fotoBytes,
                registradoPor:   SesionManager.HaySesion ? (long?)SesionManager.UsuarioId : null);

            if (!resultado.ok)
            {
                NotificacionWindow.MostrarError(resultado.mensaje);
                return;
            }

            // Guardar datos del socio creado para el paso 2
            _socioId     = resultado.socioCreado.Id;
            _numeroSocio = resultado.socioCreado.NumeroSocio;

            // Actualizar UI del paso 2
            lblSocioCreado.Text  = resultado.socioCreado.Apellido + ", " + resultado.socioCreado.Nombre;
            lblNumeroSocio.Text  = "#" + _numeroSocio.ToString("D4") + " — Socio registrado correctamente";

            // Pasar al paso 2
            IrAPaso2();
        }

        // ── PASO 2: validar y guardar membresía ───────────────
        private void EjecutarPaso2()
        {
            if (cmbActividad.SelectedItem == null)
            {
                NotificacionWindow.MostrarAdvertencia("Seleccioná una actividad para continuar.");
                return;
            }

            var actividad    = cmbActividad.SelectedItem as Actividad;
            var metodoPagoItem = cmbMetodoPago.SelectedItem as ComboBoxItem;
            string metodoPago  = metodoPagoItem?.Tag?.ToString() ?? "efectivo";

            // Instructor (puede ser null)
            long? instructorId = null;
            if (cmbInstructor.SelectedIndex > 0)
            {
                var inst = cmbInstructor.SelectedItem as Usuario;
                if (inst != null) instructorId = inst.Id;
            }

            DateTime fechaInicio      = DateTime.Today;
            DateTime fechaVencimiento = DateTime.Today.AddDays(31);

            var resultado = _membresiaCtrl.Insertar(
                socioId:          _socioId,
                actividadId:      actividad.Id,
                instructorId:     instructorId,
                fechaInicio:      fechaInicio,
                fechaVencimiento: fechaVencimiento,
                montoPagado:      actividad.Precio,
                metodoPago:       metodoPago,
                registradoPor:    SesionManager.HaySesion ? SesionManager.UsuarioId : 0L,
                observaciones:    null);

            if (!resultado.ok)
            {
                NotificacionWindow.MostrarError(resultado.mensaje);
                return;
            }

            // Preguntar por huella digital (no implementado todavía)
            bool registrarHuella = NotificacionWindow.MostrarConfirmacion(
                "Membresía creada correctamente.\n\n¿Querés registrar la huella digital del socio ahora?",
                "¡Todo listo!");

            if (registrarHuella)
            {
                // TODO: implementar registro de huella en una versión futura
                NotificacionWindow.MostrarAdvertencia(
                    "El registro de huella digital estará disponible próximamente.",
                    "Próximamente");
            }

            // Cerrar la ventana con resultado exitoso
            DialogResult = true;
            Close();
        }

        // ── Navegación entre pasos ────────────────────────────
        private void IrAPaso2()
        {
            _pasoActual = 2;

            // Mostrar paso 2, ocultar paso 1
            panelPaso1.Visibility = Visibility.Collapsed;
            panelPaso2.Visibility = Visibility.Visible;

            // Actualizar indicador visual
            circuloPaso2.Background    = (System.Windows.Media.Brush)FindResource("GreenMain");
            lblPaso2.Foreground        = (System.Windows.Media.Brush)FindResource("GreenMain");

            // Actualizar textos
            lblTitulo.Text      = "NUEVA MEMBRESÍA";
            lblSubtitulo.Text   = "Asigná una actividad al nuevo socio";
            btnAccion.Content   = "COBRAR ✓";
            btnCancelar.Content = "← Volver";
        }

        // ── Cambio de actividad → mostrar precio ──────────────
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

        // ── Foto ──────────────────────────────────────────────
        private void btnSubirFoto_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title  = "Seleccionar foto del socio",
                Filter = "Imágenes (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                _fotoBytes = File.ReadAllBytes(dialog.FileName);
                imgFoto.ImageSource = BytesABitmapImage(_fotoBytes);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo cargar la imagen.\n" + ex.Message);
            }
        }

        // ── Botones Cancelar / Cerrar ─────────────────────────
        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            if (_pasoActual == 2 && _socioId > 0)
            {
                // El socio ya fue guardado — preguntar si cerrar de todas formas
                bool cerrar = NotificacionWindow.MostrarConfirmacion(
                    "El socio ya fue registrado. ¿Cerrar sin asignar membresía?",
                    "¿Cerrar?");
                if (!cerrar) return;

                DialogResult = true; // igual recargamos la tabla
            }
            else
            {
                DialogResult = false;
            }
            Close();
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            btnCancelar_Click(sender, e);
        }

        // ── Validaciones Paso 1 ───────────────────────────────
        private bool ValidarPaso1()
        {
            bool ok = true;

            var e1 = Validador.ValidarNombre(txtNombre.Text, "El nombre");
            AplicarError(txtNombre, errNombre, e1);
            if (e1 != null) ok = false;

            var e2 = Validador.ValidarNombre(txtApellido.Text, "El apellido");
            AplicarError(txtApellido, errApellido, e2);
            if (e2 != null) ok = false;

            var e3 = Validador.ValidarDni(txtDni.Text);
            AplicarError(txtDni, errDni, e3);
            if (e3 != null) ok = false;

            if (dpNacimiento.SelectedDate.HasValue && dpNacimiento.SelectedDate.Value > DateTime.Today)
            {
                NotificacionWindow.MostrarError("La fecha de nacimiento no puede ser futura.");
                return false;
            }

            var e4 = Validador.ValidarTelefono(txtTelefono.Text);
            AplicarError(txtTelefono, errTelefono, e4);
            if (e4 != null) ok = false;

            var e5 = Validador.ValidarEmail(txtEmail.Text);
            AplicarError(txtEmail, errEmail, e5);
            if (e5 != null) ok = false;

            if (!ok)
                NotificacionWindow.MostrarAdvertencia("Hay campos con errores. Revisalos antes de continuar.");

            return ok;
        }

        // ── Validaciones inline al perder foco ────────────────
        private void txtNombre_LostFocus(object sender, RoutedEventArgs e)
            => AplicarError(txtNombre, errNombre, Validador.ValidarNombre(txtNombre.Text, "El nombre"));

        private void txtApellido_LostFocus(object sender, RoutedEventArgs e)
            => AplicarError(txtApellido, errApellido, Validador.ValidarNombre(txtApellido.Text, "El apellido"));

        private void txtDni_LostFocus(object sender, RoutedEventArgs e)
            => AplicarError(txtDni, errDni, Validador.ValidarDni(txtDni.Text));

        private void txtTelefono_LostFocus(object sender, RoutedEventArgs e)
            => AplicarError(txtTelefono, errTelefono, Validador.ValidarTelefono(txtTelefono.Text));

        private void txtEmail_LostFocus(object sender, RoutedEventArgs e)
            => AplicarError(txtEmail, errEmail, Validador.ValidarEmail(txtEmail.Text));

        private void txtDni_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^\d$");

        private void txtDni_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string texto = (string)e.DataObject.GetData(typeof(string));
                if (!Regex.IsMatch(texto, @"^\d+$")) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        private void txtTelefono_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
            => e.Handled = !Validador.EsCaracterTelefonoValido(e.Text);

        private void txtTelefono_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string texto = (string)e.DataObject.GetData(typeof(string)) ?? string.Empty;
                var sb = new System.Text.StringBuilder();
                foreach (char c in texto) if (char.IsDigit(c)) sb.Append(c);
                string resultado = sb.Length > 10 ? sb.ToString().Substring(0, 10) : sb.ToString();
                if (resultado.Length > 0)
                {
                    var tb = sender as TextBox;
                    if (tb != null) { tb.Text = resultado; tb.CaretIndex = tb.Text.Length; }
                }
                e.CancelCommand();
            }
            else e.CancelCommand();
        }

        // ── Helper: aplicar/limpiar error en campo ────────────
        private void AplicarError(TextBox campo, TextBlock label, string mensaje)
        {
            if (mensaje != null)
            {
                campo.BorderBrush     = System.Windows.Media.Brushes.Red;
                campo.BorderThickness = new Thickness(1.5);
                label.Text            = mensaje;
                label.Visibility      = Visibility.Visible;
            }
            else
            {
                campo.ClearValue(TextBox.BorderBrushProperty);
                campo.ClearValue(TextBox.BorderThicknessProperty);
                label.Text       = string.Empty;
                label.Visibility = Visibility.Collapsed;
            }
        }

        // ── Helper: bytes → imagen ────────────────────────────
        private static BitmapImage BytesABitmapImage(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption   = BitmapCacheOption.OnLoad;
                bmp.StreamSource  = ms;
                bmp.EndInit();
                return bmp;
            }
        }
    }
}
