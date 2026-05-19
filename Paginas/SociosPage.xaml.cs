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
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class SociosPage : Page
    {
        private readonly SocioController _controller = new SocioController();

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private byte[] _fotoBytes = null;
        private string _filtroEstado = "todos";
        private string _tabActivo = "datos";

        public SociosPage()
        {
            InitializeComponent();
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
        // CARGA + STATS
        // ─────────────────────────────────────────────────────
        private void CargarSocios()
        {
            try
            {
                var lista = _controller.BuscarSocios(txtBuscar.Text, _filtroEstado);
                gridSocios.ItemsSource = lista;
                ActualizarStats();
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
                chipTodosNum.Text = $"({total})";
                chipActivosNum.Text = $"({activos})";
                chipInactivosNum.Text = $"({inactivos})";
            }
            catch
            {
                statTotal.Text = statActivos.Text = statInactivos.Text = statNuevosMes.Text = "—";
            }
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
            _esNuevo = true;
            _idEditar = 0;
            LimpiarFormulario();
            LimpiarErrores();

            int siguiente = _controller.ObtenerSiguienteNumeroSocio();
            lblNumeroSocio.Text = "#" + siguiente.ToString("D4");

            chkRegenerarPin.Visibility = Visibility.Collapsed;
            CambiarTab("datos");
            AbrirFormulario("NUEVO SOCIO");
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
                    registradoPor: null);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
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

        private Socio ObtenerSocioDeFila(object sender)
        {
            var btn = sender as Button;
            if (btn == null) return null;
            return btn.DataContext as Socio;
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