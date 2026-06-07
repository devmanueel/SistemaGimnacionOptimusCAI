// ============================================================
//  Paginas/EnrolarHuellaWindow.xaml.cs
//
//  Guía al operador a través del proceso de enrolamiento
//  de 4 capturas de huella para un socio.
//  Compatible con C# 7.3.
// ============================================================

using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class EnrolarHuellaWindow : Window
    {
        private readonly Socio _socio;
        private readonly Usuario _usuario;
        private readonly SocioController _socioController = new SocioController();
        private readonly UsuarioController _usuarioController = new UsuarioController();

        // Estrategia de guardado según la entidad (socio o docente).
        private readonly Func<Guid, byte[], (bool ok, string mensaje)> _guardarHuella;

        private CancellationTokenSource _cts;

        public bool Exito { get; private set; }

        public EnrolarHuellaWindow(Socio socio)
        {
            InitializeComponent();
            _socio = socio;
            lblSocioNombre.Text = socio.NombreCompleto + " — " + socio.NumeroFormateado;
            _guardarHuella = (guid, template) =>
                _socioController.GuardarHuella(_socio.Id, guid, template);
        }

        public EnrolarHuellaWindow(Usuario usuario)
        {
            InitializeComponent();
            _usuario = usuario;
            lblSocioNombre.Text = usuario.NombreCompleto + " — Docente";
            _guardarHuella = (guid, template) =>
                _usuarioController.GuardarHuella(_usuario.Id, guid, template);
        }

        // ── Ciclo de vida ───────────────────────────────────────

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IniciarEnrolamiento();
        }

        private void IniciarEnrolamiento()
        {
            var servicio = BiometricManager.Servicio;
            if (servicio == null || !servicio.Disponible)
            {
                MostrarError("El lector de huellas no está disponible.\n" +
                             servicio?.MensajeEstado);
                return;
            }

            // Asignar un GUID nuevo para este enrolamiento (identidad lógica del socio)
            Guid nuevoGuid = Guid.NewGuid();
            _cts = new CancellationTokenSource();

            // Correr en hilo secundario para no bloquear la UI
            var token = _cts.Token;
            System.Threading.Tasks.Task.Run(() =>
            {
                var (exito, mensaje, template) = servicio.Enrollar(
                    (capturas, msg) => Dispatcher.Invoke(() => ActualizarProgreso(capturas, msg)),
                    token);

                Dispatcher.Invoke(() => OnEnrolamientoTerminado(exito, mensaje, nuevoGuid, template));
            });
        }

        // ── Callbacks de progreso ───────────────────────────────

        private void ActualizarProgreso(int capturas, string mensaje)
        {
            // Color del ícono
            bool esperando = capturas == 0 || mensaje.Contains("apoyá") || mensaje.Contains("Apoyá");
            iconHuella.Foreground = esperando
                ? (Brush)FindResource("TextMuted")
                : (Brush)FindResource("GreenMain");

            lblEstado.Text  = mensaje;
            lblDetalle.Text = string.Empty;
            lblCapturas.Text = capturas + " / 4 capturas";

            // Puntos de progreso
            var verde = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
            var gris  = (Brush)FindResource("Border1");
            dot1.Fill = capturas >= 1 ? verde : gris;
            dot2.Fill = capturas >= 2 ? verde : gris;
            dot3.Fill = capturas >= 3 ? verde : gris;
            dot4.Fill = capturas >= 4 ? verde : gris;

            // Si es mensaje de rechazo, mostrarlo en detalle
            if (mensaje.Contains("Dedo") || mensaje.Contains("Captura") ||
                mensaje.Contains("calidad") || mensaje.Contains("inclinado"))
            {
                lblDetalle.Text = mensaje;
                lblEstado.Text  = "Intentá de nuevo";
                iconHuella.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));
            }
        }

        private void OnEnrolamientoTerminado(bool exito, string mensaje, Guid guid, byte[] template)
        {
            if (exito)
            {
                // Guardar el GUID lógico + el template biométrico en la BD
                var res = _guardarHuella(guid, template);
                if (!res.ok)
                {
                    MostrarError("Huella capturada pero no se pudo guardar: " + res.mensaje);
                    return;
                }

                if (_socio != null) _socio.HuellaGuid = guid;
                if (_usuario != null) _usuario.HuellaGuid = guid;
                Exito = true;
                iconHuella.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
                iconHuella.Icon = FontAwesome.WPF.FontAwesomeIcon.CheckCircle;
                lblEstado.Text  = "¡Huella registrada correctamente!";
                lblDetalle.Text = "Ya puede registrar su asistencia con la huella.";
                lblCapturas.Text = "4 / 4 capturas";
                dot1.Fill = dot2.Fill = dot3.Fill = dot4.Fill =
                    new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));

                btnCancelar.Visibility = Visibility.Collapsed;
                btnCerrar.Visibility   = Visibility.Visible;
            }
            else
            {
                MostrarError(mensaje);
            }
        }

        private void MostrarError(string mensaje)
        {
            iconHuella.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));
            iconHuella.Icon = FontAwesome.WPF.FontAwesomeIcon.TimesCircle;
            lblEstado.Text  = "No se pudo registrar la huella";
            lblDetalle.Text = mensaje;

            btnCancelar.Content    = "Cerrar";
            btnCerrar.Visibility   = Visibility.Collapsed;
        }

        // ── Eventos de botones ──────────────────────────────────

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            BiometricManager.Servicio?.Cancelar();
            DialogResult = false;
            Close();
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts?.Cancel();
            base.OnClosed(e);
        }
    }
}
