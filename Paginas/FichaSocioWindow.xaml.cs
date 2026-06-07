using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using SistemaGimnacionOptimusCAI.Ventanas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class FichaSocioWindow : Window
    {
        private readonly Socio _socio;
        private Membresia _membresiaActiva;
        private readonly SocioController _socioController = new SocioController();
        private readonly MembresiaController _membresiaController = new MembresiaController();
        private readonly AsistenciaController _asistenciaController = new AsistenciaController();
        private readonly FichaMedicaController _fichaController = new FichaMedicaController();

        public bool HuboCambiosSocio { get; private set; }

        public FichaSocioWindow(Socio socio)
        {
            InitializeComponent();
            _socio = socio;
            CargarDatosSocio();
            CargarMembresias();
            CargarAsistencias();
            CargarFichaMedica();
            ActualizarBtnHuella();
            ConfigurarModo();          // muestra/oculta botones según activo/inactivo
        }

        // ──────────────────────────────────────────────────────
        //  CARGA DE DATOS
        // ──────────────────────────────────────────────────────
        private void CargarDatosSocio()
        {
            lblNombre.Text       = _socio.NombreCompleto;
            lblNumeroSocio.Text  = _socio.NumeroFormateado;
            lblCelular.Text      = string.IsNullOrWhiteSpace(_socio.Telefono) ? "Sin dato" : _socio.Telefono;
            lblDni.Text          = _socio.Dni;
            lblFechaNac.Text     = _socio.FechaNacimiento.HasValue
                                    ? _socio.FechaNacimiento.Value.ToString("dd/MM/yyyy") : "Sin dato";
            lblSexo.Text         = _socio.SexoTexto;
            lblEdad.Text         = _socio.EdadTexto;
            lblDomicilio.Text    = string.IsNullOrWhiteSpace(_socio.Domicilio) ? "Sin dato" : _socio.Domicilio;
            lblMail.Text         = string.IsNullOrWhiteSpace(_socio.Email) ? "Sin dato" : _socio.Email;
            lblObservaciones.Text= string.IsNullOrWhiteSpace(_socio.Observaciones) ? "Sin dato" : _socio.Observaciones;

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
                _membresiaActiva = null;
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
                listaActividades.ItemsSource = delSocio;
                lblSinActividades.Visibility = delSocio.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                lblSinActividades.Visibility = Visibility.Visible;
            }
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

                    badgeApto.Visibility = ficha.AptoFisico ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch { }
        }

        private void ActualizarBtnHuella()
        {
            bool tieneHuella = _socio.TieneHuella;
            var servicio = BiometricManager.Servicio;
            bool lectorDisponible = servicio?.Disponible == true;

            if (!lectorDisponible)
            {
                btnHuella.IsEnabled = false;
                string motivo = servicio?.MensajeEstado ?? "Servicio biométrico no inicializado";
                btnHuella.ToolTip = motivo;
                lblBtnHuella.Text = "Huella (no disponible)";
                return;
            }

            btnHuella.IsEnabled = true;
            lblBtnHuella.Text = tieneHuella ? "Actualizar Huella" : "Registrar Huella";
            iconBtnHuella.Foreground = tieneHuella
                ? new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80))
                : Brushes.White;
        }

        // ──────────────────────────────────────────────────────
        //  MODO ACTIVO / INACTIVO
        // ──────────────────────────────────────────────────────
        private void ConfigurarModo()
        {
            bool activo = _socio.Activo;

            // Badge de estado
            lblEstado.Text = activo ? "ACTIVO" : "INACTIVO";
            lblEstado.Foreground = activo
                ? new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80))   // verde
                : new SolidColorBrush(Color.FromRgb(0xFF, 0x77, 0x77));  // rojo
            badgeEstado.Background = activo
                ? new SolidColorBrush(Color.FromRgb(0x0A, 0x2A, 0x14))
                : new SolidColorBrush(Color.FromRgb(0x2A, 0x0A, 0x0A));

            // Botones del panel derecho que solo aplican a socios activos
            var visActivo = activo ? Visibility.Visible : Visibility.Collapsed;
            btnHuella.Visibility         = visActivo;
            btnNuevaMembresia.Visibility = visActivo;
            btnRutinas.Visibility        = visActivo;
            btnGenerarCarnet.Visibility  = visActivo;

            // Tab de actividades solo si está activo
            tabActividades.Visibility = visActivo;

            // Footer: dar de baja (activo) / restaurar (inactivo)
            btnDarDeBaja.Visibility      = activo ? Visibility.Visible : Visibility.Collapsed;
            btnRestaurarSocio.Visibility = activo ? Visibility.Collapsed : Visibility.Visible;
        }

        // ──────────────────────────────────────────────────────
        //  BOTONES DEL PANEL
        // ──────────────────────────────────────────────────────
        private void btnEditarDatos_Click(object sender, RoutedEventArgs e)
        {
            // Popup de edición de datos personales (no redirige a Socios).
            var win = new EditarSocioWindow(_socio.Id) { Owner = this };
            if (win.ShowDialog() == true)
                RefrescarSocio();
        }

        private void btnNuevaMembresia_Click(object sender, RoutedEventArgs e)
        {
            // Popup rápido para cambiar / asignar membresía sin salir de la ficha.
            var win = new NuevaMembresiaWindow(_socio) { Owner = this };
            if (win.ShowDialog() == true)
                CargarMembresias();
        }

        /// <summary>Recarga los datos del socio desde la BD y refresca la ficha.</summary>
        private void RefrescarSocio()
        {
            try
            {
                var fresco = _socioController.ObtenerPorId(_socio.Id);
                if (fresco == null) return;

                _socio.Nombre          = fresco.Nombre;
                _socio.Apellido        = fresco.Apellido;
                _socio.Dni             = fresco.Dni;
                _socio.FechaNacimiento = fresco.FechaNacimiento;
                _socio.Sexo            = fresco.Sexo;
                _socio.Telefono        = fresco.Telefono;
                _socio.Email           = fresco.Email;
                _socio.Domicilio       = fresco.Domicilio;
                _socio.Profesion       = fresco.Profesion;
                _socio.Observaciones   = fresco.Observaciones;
                _socio.Foto            = fresco.Foto;

                CargarDatosSocio();
            }
            catch { }
        }

        private void btnRutinas_Click(object sender, RoutedEventArgs e)
        {
            var main = Owner as MainWindow;
            if (main != null)
            {
                main.NavegarA(typeof(RutinasPage));
                Close();
            }
            else
            {
                NotificacionWindow.MostrarAdvertencia("Abrí el módulo Rutinas.");
            }
        }

        private void btnHuella_Click(object sender, RoutedEventArgs e)
        {
            var win = new EnrolarHuellaWindow(_socio) { Owner = this };
            bool? result = win.ShowDialog();
            if (result == true)
            {
                _socio.HuellaGuid = Guid.NewGuid(); // marcar TieneHuella = true
                ActualizarBtnHuella();
                NotificacionWindow.MostrarExito(
                    "Huella registrada. El socio ya puede acceder con su huella.",
                    "Huella digital");
            }
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

        // ──────────────────────────────────────────────────────
        //  DAR DE BAJA / RESTAURAR
        // ──────────────────────────────────────────────────────
        private void btnDarDeBaja_Click(object sender, RoutedEventArgs e)
        {
            var confirmar = MessageBox.Show(
                "¿Dar de baja al socio " + _socio.NombreCompleto + "?\n" +
                "El socio quedará inactivo. Sus datos se conservan.",
                "Dar de baja", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmar != MessageBoxResult.Yes) return;

            var r = _socioController.CambiarEstado(_socio.Id, false);
            if (r.ok)
            {
                _socio.Activo = false;
                HuboCambiosSocio = true;
                CargarMembresias();
                ConfigurarModo();
                NotificacionWindow.MostrarExito(r.mensaje);
            }
            else
            {
                NotificacionWindow.MostrarError(r.mensaje);
            }
        }

        private void btnRestaurarSocio_Click(object sender, RoutedEventArgs e)
        {
            var confirmar = MessageBox.Show(
                "¿Restaurar al socio " + _socio.NombreCompleto + "?\n" +
                "El socio volverá a estar activo en el sistema.",
                "Restaurar socio", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmar != MessageBoxResult.Yes) return;

            var r = _socioController.CambiarEstado(_socio.Id, true);
            if (r.ok)
            {
                _socio.Activo = true;
                HuboCambiosSocio = true;
                CargarMembresias();
                ConfigurarModo();
                NotificacionWindow.MostrarExito(r.mensaje);
            }
            else
            {
                NotificacionWindow.MostrarError(r.mensaje);
            }
        }

        // ──────────────────────────────────────────────────────
        //  FICHA MÉDICA
        // ──────────────────────────────────────────────────────
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
            {
                badgeApto.Visibility = chkApto.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                NotificacionWindow.MostrarExito(r.mensaje);
            }
            else
            {
                NotificacionWindow.MostrarError(r.mensaje);
            }
        }

        // ──────────────────────────────────────────────────────
        //  VENTANA
        // ──────────────────────────────────────────────────────
        private void Barra_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
