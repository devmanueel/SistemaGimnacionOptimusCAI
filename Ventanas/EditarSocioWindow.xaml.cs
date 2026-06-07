// ============================================================
//  Archivo: EditarSocioWindow.xaml.cs
//
//  Ventana emergente para editar SOLO los datos personales de
//  un socio existente (nombre, DNI, contacto, etc.).
//  NO toca el plan / membresía / instructor (eso va en la página
//  Socios / Membresías). Compatible con C# 7.3.
// ============================================================

using Controllers;
using Entities;
using Microsoft.Win32;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class EditarSocioWindow : Window
    {
        private readonly SocioController _socioCtrl = new SocioController();
        private readonly long _socioId;
        private byte[] _fotoBytes;

        /// <summary>True si los datos se guardaron correctamente.</summary>
        public bool DatosActualizados { get; private set; }

        public EditarSocioWindow(long socioId)
        {
            InitializeComponent();
            _socioId = socioId;
            Loaded += (s, e) => CargarDatos();
        }

        // ── Carga inicial de los datos del socio ──────────────
        private void CargarDatos()
        {
            try
            {
                Socio socio = _socioCtrl.ObtenerPorId(_socioId);
                if (socio == null)
                {
                    NotificacionWindow.MostrarError("No se encontró el socio.");
                    Close();
                    return;
                }

                lblSubtitulo.Text   = "Modificá los datos de " + socio.NombreCompleto;
                txtNombre.Text       = socio.Nombre ?? string.Empty;
                txtApellido.Text     = socio.Apellido ?? string.Empty;
                txtDni.Text          = socio.Dni ?? string.Empty;
                txtTelefono.Text     = socio.Telefono ?? string.Empty;
                txtEmail.Text        = socio.Email ?? string.Empty;
                txtDomicilio.Text    = socio.Domicilio ?? string.Empty;
                txtProfesion.Text    = socio.Profesion ?? string.Empty;
                txtObservaciones.Text = socio.Observaciones ?? string.Empty;
                dpNacimiento.SelectedDate = socio.FechaNacimiento;

                SeleccionarSexo(socio.Sexo);

                _fotoBytes = socio.Foto;
                if (socio.Foto != null && socio.Foto.Length > 0)
                {
                    try { imgFoto.ImageSource = BytesABitmapImage(socio.Foto); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al cargar datos.\n" + ex.Message);
                Close();
            }
        }

        private void SeleccionarSexo(string sexo)
        {
            foreach (var item in cmbSexo.Items)
            {
                var cbi = item as ComboBoxItem;
                if (cbi != null && cbi.Tag != null && cbi.Tag.ToString() == sexo)
                {
                    cmbSexo.SelectedItem = cbi;
                    return;
                }
            }
        }

        // ── Cambiar foto ──────────────────────────────────────
        private void btnCambiarFoto_Click(object sender, RoutedEventArgs e)
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
                imgFoto.ImageSource = BytesABitmapImage(_fotoBytes);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo cargar la imagen.\n" + ex.Message);
            }
        }

        // ── Guardar cambios ───────────────────────────────────
        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            lblError.Visibility = Visibility.Collapsed;

            string sexo = "Otro";
            var sexoItem = cmbSexo.SelectedItem as ComboBoxItem;
            if (sexoItem != null && sexoItem.Tag != null)
                sexo = sexoItem.Tag.ToString();

            var r = _socioCtrl.Modificar(
                id:              _socioId,
                nombre:          txtNombre.Text,
                apellido:        txtApellido.Text,
                dni:             txtDni.Text,
                fechaNacimiento: dpNacimiento.SelectedDate,
                sexo:            sexo,
                telefono:        txtTelefono.Text,
                domicilio:       txtDomicilio.Text,
                profesion:       txtProfesion.Text,
                email:           txtEmail.Text,
                comoNosConocio:  null,
                observaciones:   txtObservaciones.Text,
                foto:            _fotoBytes,
                regenerarPin:    false);

            if (!r.ok)
            {
                MostrarError(r.mensaje);
                return;
            }

            DatosActualizados = true;
            NotificacionWindow.MostrarExito(r.mensaje, "¡Socio actualizado!");
            DialogResult = true;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) => Close();
        private void btnCerrar_Click(object sender, RoutedEventArgs e) => Close();

        // ── Solo dígitos en el DNI ────────────────────────────
        private void txtDni_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^\d$");

        private void MostrarError(string mensaje)
        {
            lblError.Text = mensaje;
            lblError.Visibility = Visibility.Visible;
        }

        private static BitmapImage BytesABitmapImage(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption  = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                return bmp;
            }
        }
    }
}
