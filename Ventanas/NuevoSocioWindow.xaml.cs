// ============================================================
//  Archivo: NuevoSocioWindow.xaml.cs
//
//  Ventana emergente de 2 pasos para crear socio + membresía.
//  Paso 1: datos del socio → validación
//  Paso 2: membresía → guarda socio + membresía juntos
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
        private int    _pasoActual   = 1;
        private long   _socioId      = 0;
        private int    _numeroSocio  = 0;
        private byte[] _fotoBytes    = null;
        private Socio  _socioCreado  = null;

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

        // ── PASO 1: validar datos y pasar al paso 2 ───────────
        private void EjecutarPaso1()
        {
            if (!ValidarPaso1()) return;

            // Mostrar preview del socio en el paso 2 (aún no guardado)
            string nombreCompleto = txtApellido.Text.Trim() + ", " + txtNombre.Text.Trim();
            lblSocioCreado.Text = nombreCompleto;
            lblNumeroSocio.Text = "Socio pendiente de registro";

            IrAPaso2();
        }

        // ── PASO 2: guardar socio + membresía juntos ──────────
        private void EjecutarPaso2()
        {
            if (cmbActividad.SelectedItem == null)
            {
                NotificacionWindow.MostrarAdvertencia("Seleccioná una actividad para continuar.");
                return;
            }

            // ── 1. Guardar el socio ───────────────────────────
            string sexo = "Otro";
            var sexoItem = cmbSexo.SelectedItem as ComboBoxItem;
            if (sexoItem?.Tag != null) sexo = sexoItem.Tag.ToString();

            string comoConocio = (cmbComoConocio.SelectedItem as ComboBoxItem)?.Content?.ToString()
                                 ?? string.Empty;

            var resultadoSocio = _socioCtrl.Insertar(
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

            if (!resultadoSocio.ok)
            {
                NotificacionWindow.MostrarError(resultadoSocio.mensaje);
                return;
            }

            _socioCreado = resultadoSocio.socioCreado;
            _socioId     = resultadoSocio.socioCreado.Id;
            _numeroSocio = resultadoSocio.socioCreado.NumeroSocio;

            // ── 2. Guardar la membresía ───────────────────────
            var actividad = cmbActividad.SelectedItem as Actividad;
            var metodoPagoItem = cmbMetodoPago.SelectedItem as ComboBoxItem;
            string metodoPago = metodoPagoItem?.Tag?.ToString() ?? "efectivo";

            long? instructorId = null;
            if (cmbInstructor.SelectedIndex > 0)
            {
                var inst = cmbInstructor.SelectedItem as Usuario;
                if (inst != null) instructorId = inst.Id;
            }

            var resultadoMem = _membresiaCtrl.Insertar(
                socioId:          _socioId,
                actividadId:      actividad.Id,
                instructorId:     instructorId,
                fechaInicio:      DateTime.Today,
                fechaVencimiento: DateTime.Today.AddDays(31),
                montoPagado:      actividad.Precio,
                metodoPago:       metodoPago,
                registradoPor:    SesionManager.HaySesion ? SesionManager.UsuarioId : 0L,
                observaciones:    null);

            if (!resultadoMem.ok)
            {
                NotificacionWindow.MostrarError(resultadoMem.mensaje);
                return;
            }

            // ── 3. Preguntar por huella digital ───────────────
            bool registrarHuella = NotificacionWindow.MostrarConfirmacion(
                "Membresía creada correctamente.\n\n¿Querés registrar la huella digital del socio ahora?",
                "¡Todo listo!");

            if (registrarHuella)
            {
                var svc = BiometricManager.Servicio;
                if (svc != null && svc.Disponible && _socioCreado != null)
                {
                    var win = new Paginas.EnrolarHuellaWindow(_socioCreado) { Owner = this };
                    win.ShowDialog();
                }
                else
                {
                    NotificacionWindow.MostrarAdvertencia(
                        "El lector de huellas no está disponible en este momento.\n" +
                        (svc?.MensajeEstado ?? "Podés registrar la huella más tarde desde la ficha del socio."),
                        "Lector no disponible");
                }
            }

            DialogResult = true;
            Close();
        }

        // ── Navegación: Paso 1 → Paso 2 ──────────────────────
        private void IrAPaso2()
        {
            _pasoActual = 2;

            panelPaso1.Visibility = Visibility.Collapsed;
            panelPaso2.Visibility = Visibility.Visible;

            circuloPaso2.Background = (System.Windows.Media.Brush)FindResource("GreenMain");
            lblPaso2.Foreground     = (System.Windows.Media.Brush)FindResource("GreenMain");

            lblTitulo.Text      = "NUEVA MEMBRESÍA";
            lblSubtitulo.Text   = "Asigná una actividad al nuevo socio";
            btnAccion.Content   = "COBRAR ✓";
            btnCancelar.Content = "← Volver";
        }

        // ── Navegación: Paso 2 → Paso 1 ──────────────────────
        private void VolverAPaso1()
        {
            _pasoActual = 1;

            panelPaso2.Visibility = Visibility.Collapsed;
            panelPaso1.Visibility = Visibility.Visible;

            circuloPaso2.Background = (System.Windows.Media.Brush)FindResource("Bg3");
            lblPaso2.Foreground     = (System.Windows.Media.Brush)FindResource("TextMuted");

            lblTitulo.Text      = "NUEVO SOCIO";
            lblSubtitulo.Text   = "Completá los datos del nuevo socio";
            btnAccion.Content   = "SIGUIENTE →";
            btnCancelar.Content = "Cancelar";
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

        // ── Botón inferior izquierdo (Cancelar / ← Volver) ────
        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            if (_pasoActual == 2)
            {
                // Volver al paso 1 para modificar datos del socio
                VolverAPaso1();
            }
            else
            {
                // Paso 1: cancelar todo
                DialogResult = false;
                Close();
            }
        }

        // ── Botón X del header ────────────────────────────────
        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            if (_pasoActual == 2)
            {
                bool cerrar = NotificacionWindow.MostrarConfirmacion(
                    "¿Cancelar el registro del socio y cerrar?",
                    "¿Cerrar?");
                if (!cerrar) return;
            }
            DialogResult = false;
            Close();
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

            if (dpNacimiento.SelectedDate.HasValue && dpNacimiento.SelectedDate.Value.Date > DateTime.Today.AddYears(-6))
            {
                NotificacionWindow.MostrarError("El socio debe tener al menos 6 años para ser registrado.");
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
