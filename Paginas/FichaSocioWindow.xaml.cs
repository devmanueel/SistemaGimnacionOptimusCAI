using Controllers;
using Entities;
using FontAwesome.WPF;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class FichaSocioWindow : Window
    {
        private readonly Socio _socio;
        private Membresia _membresiaActiva;
        private readonly MembresiaController _membresiaController = new MembresiaController();
        private readonly AsistenciaController _asistenciaController = new AsistenciaController();
        private readonly FichaMedicaController _fichaController = new FichaMedicaController();

        public FichaSocioWindow(Socio socio)
        {
            InitializeComponent();
            _socio = socio;
            CargarDatosSocio();
            CargarMembresias();
            CargarAsistencias();
            CargarFichaMedica();
            ActualizarBtnHuella();
        }

        private void ActualizarBtnHuella()
        {
            bool tieneHuella = _socio.TieneHuella;
            bool lectorDisponible = BiometricManager.Servicio?.Disponible == true;

            if (!lectorDisponible)
            {
                btnHuella.IsEnabled = false;
                btnHuella.ToolTip = "Lector de huellas no detectado";
                return;
            }

            lblBtnHuella.Text = tieneHuella ? "ACTUALIZAR HUELLA" : "REGISTRAR HUELLA";
            iconBtnHuella.Foreground = tieneHuella
                ? new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80))
                : (Brush)FindResource("TextPrimary");
        }

        private void CargarDatosSocio()
        {
            lblNombre.Text = _socio.NombreCompleto;
            lblNumeroSocio.Text = _socio.NumeroFormateado;
            lblEstado.Text = _socio.Activo ? "ACTIVO" : "INACTIVO";
            lblCelular.Text = _socio.Telefono ?? "";

            if (_socio.Foto != null && _socio.Foto.Length > 0)
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new MemoryStream(_socio.Foto);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    imgFoto.Source = bmp;
                }
                catch { }
            }
        }

        private void CargarMembresias()
        {
            try
            {
                var todas = _membresiaController.BuscarMembresias(_socio.Dni, "todos");
                var delSocio = new List<Membresia>();
                foreach (var m in todas)
                {
                    if (m.SocioId == _socio.Id)
                    {
                        delSocio.Add(m);
                        if (m.Estado == "activa" && _membresiaActiva == null)
                            _membresiaActiva = m;
                    }
                }
                gridMembresias.ItemsSource = delSocio;
            }
            catch { }
        }

        private void CargarAsistencias()
        {
            try
            {
                var accesos = _asistenciaController.BuscarAccesos(_socio.Dni, "todos", null, null);
                var delSocio = new List<RegistroAcceso>();
                foreach (var a in accesos)
                {
                    if (a.SocioId == _socio.Id) delSocio.Add(a);
                }
                gridAsistencias.ItemsSource = delSocio;
            }
            catch { }
        }

        private void CargarFichaMedica()
        {
            try
            {
                var ficha = _fichaController.ObtenerPorSocio(_socio.Id);
                if (ficha != null)
                {
                    txtPeso.Text = ficha.PesoKg.HasValue ? ficha.PesoKg.Value.ToString("F1") : "";
                    txtAltura.Text = ficha.AlturaCm.HasValue ? ficha.AlturaCm.Value.ToString() : "";
                    txtGrupo.Text = ficha.GrupoSanguineo ?? "";
                    txtEnfermedades.Text = ficha.Enfermedades ?? "";
                    txtMedicamentos.Text = ficha.Medicamentos ?? "";
                    txtRestricciones.Text = ficha.RestriccionesFisicas ?? "";
                    txtContactoEmergencia.Text = ficha.ContactoEmergencia ?? "";
                    txtTelEmergencia.Text = ficha.TelefonoEmergencia ?? "";
                    chkApto.IsChecked = ficha.AptoFisico;
                    dpFechaApto.SelectedDate = ficha.FechaApto;
                    txtObsMedicas.Text = ficha.Observaciones ?? "";
                }
            }
            catch { }
        }

        private void btnGuardarFicha_Click(object sender, RoutedEventArgs e)
        {
            decimal? peso = null;
            if (!string.IsNullOrWhiteSpace(txtPeso.Text))
            {
                decimal p;
                if (decimal.TryParse(txtPeso.Text, out p)) peso = p;
            }

            short? altura = null;
            if (!string.IsNullOrWhiteSpace(txtAltura.Text))
            {
                short a;
                if (short.TryParse(txtAltura.Text, out a)) altura = a;
            }

            var r = _fichaController.Guardar(
                _socio.Id,
                peso,
                altura,
                txtGrupo.Text,
                txtEnfermedades.Text,
                txtMedicamentos.Text,
                txtRestricciones.Text,
                txtContactoEmergencia.Text,
                txtTelEmergencia.Text,
                chkApto.IsChecked == true,
                dpFechaApto.SelectedDate,
                txtObsMedicas.Text);

            if (r.ok)
                NotificacionWindow.MostrarExito(r.mensaje);
            else
                NotificacionWindow.MostrarError(r.mensaje);
        }

        private void btnGenerarCarnet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var gen = new CarnetGenerator();
                string path = gen.GenerarCarnet(_socio, _membresiaActiva);
                if (path != null)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                        { UseShellExecute = true });
                    NotificacionWindow.MostrarExito("Carnet generado correctamente.");
                }
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al generar el carnet.\n" + ex.Message);
            }
        }

        private void btnHuella_Click(object sender, RoutedEventArgs e)
        {
            var win = new EnrolarHuellaWindow(_socio) { Owner = this };
            bool? result = win.ShowDialog();
            if (result == true)
            {
                // Actualizar estado del botón reflejando que ahora tiene huella
                _socio.HuellaGuid = Guid.NewGuid(); // cualquier guid para marcar TieneHuella=true
                ActualizarBtnHuella();
                NotificacionWindow.MostrarExito(
                    "Huella registrada. El socio ya puede acceder con su huella.",
                    "Huella digital");
            }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
