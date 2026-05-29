// ============================================================
//  Archivo: AsistenciasPage.xaml.cs
//
//  Panel de control de acceso al gimnasio:
//   · Reloj en vivo arriba a la derecha
//   · Input gigante de DNI con foco automático (tipo torniquete)
//   · Validación al presionar ENTER o el botón
//   · Tarjeta de resultado animada (verde/rojo según resultado)
//   · Auto-cierre de la tarjeta a los 5 segundos
//   · Lista de accesos del día con auto-refresh cada 10 segundos
//   · Filtros por resultado (Todos / Permitidos / Denegados)
//   · Selector de actividad cuando el socio tiene más de una membresía
//
//  Compatible con C# 7.3.
// ============================================================

using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class AsistenciasPage : Page
    {
        private readonly AsistenciaController _controller = new AsistenciaController();

        private string _filtroResultado = "todos";
        private string _dniPendiente = null;
        private DispatcherTimer _timerReloj;
        private DispatcherTimer _timerRefresh;
        private DispatcherTimer _timerOcultarResultado;

        // Modo huella
        private bool _modoHuellaActivo = false;
        private CancellationTokenSource _huellaIdCts;
        private Task _huellaIdTask;

        public AsistenciasPage()
        {
            InitializeComponent();

            ResaltarChip(chipTodos);
            CargarAccesos();
            ActualizarStats();
            IniciarReloj();
            IniciarAutoRefresh();

            Loaded += (s, e) =>
            {
                txtDni.Focus();
                ConfigurarBtnHuella();
            };

            Unloaded += (s, e) => DetenerModoHuella();
        }

        // ─────────────────────────────────────────────────────
        // BOTÓN MODO HUELLA — configuración inicial
        // ─────────────────────────────────────────────────────
        private void ConfigurarBtnHuella()
        {
            // Esperar a que el servicio termine de inicializar (puede tardar ~1s)
            Task.Run(async () =>
            {
                // Reintentar hasta 8s que el servicio se inicialice
                for (int i = 0; i < 16; i++)
                {
                    var svc = BiometricManager.Servicio;
                    if (svc != null) break;
                    await Task.Delay(500);
                }

                Dispatcher.Invoke(() =>
                {
                    var svc = BiometricManager.Servicio;
                    if (svc != null && svc.Disponible)
                    {
                        btnModoHuella.Visibility = Visibility.Visible;
                        lblEstadoLector.Visibility = Visibility.Visible;
                        lblEstadoLector.Text = "Lector de huellas listo";
                    }
                    else
                    {
                        btnModoHuella.Visibility = Visibility.Collapsed;
                        lblEstadoLector.Visibility = Visibility.Visible;
                        lblEstadoLector.Text = svc?.MensajeEstado ?? "Lector no disponible";
                    }
                });
            });
        }

        // ─────────────────────────────────────────────────────
        // MODO HUELLA — activar / desactivar
        // ─────────────────────────────────────────────────────
        private void btnModoHuella_Click(object sender, RoutedEventArgs e)
        {
            if (_modoHuellaActivo)
                DetenerModoHuella();
            else
                ActivarModoHuella();
        }

        private void ActivarModoHuella()
        {
            var svc = BiometricManager.Servicio;
            if (svc == null || !svc.Disponible)
            {
                NotificacionWindow.MostrarAdvertencia(
                    "El lector de huellas no está disponible.\n" + svc?.MensajeEstado);
                return;
            }

            _modoHuellaActivo = true;

            // UI: modo huella ON
            lblModoHuella.Text    = "DESACTIVAR MODO HUELLA";
            iconModoHuella.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));
            panelHuellaEsperando.Visibility = Visibility.Visible;
            lblHuellaEstado.Text  = "APOYÁ TU DEDO EN EL LECTOR";
            txtDni.IsEnabled      = false;
            btnValidar.IsEnabled  = false;

            // Empezar loop de identificación en hilo secundario
            _huellaIdCts  = new CancellationTokenSource();
            var token     = _huellaIdCts.Token;
            _huellaIdTask = Task.Run(() =>
                svc.IniciarIdentificacion(token, OnHuellaIdentificada));
        }

        private void DetenerModoHuella()
        {
            if (!_modoHuellaActivo) return;
            _modoHuellaActivo = false;

            _huellaIdCts?.Cancel();
            BiometricManager.Servicio?.Cancelar();

            // UI: modo huella OFF
            Dispatcher.Invoke(() =>
            {
                lblModoHuella.Text    = "ACTIVAR MODO HUELLA";
                iconModoHuella.Foreground = (Brush)FindResource("GreenMain");
                panelHuellaEsperando.Visibility = Visibility.Collapsed;
                panelEsperando.Visibility       = Visibility.Visible;
                txtDni.IsEnabled   = true;
                btnValidar.IsEnabled = true;
                txtDni.Focus();
            });
        }

        private void OnHuellaIdentificada(Guid? guid)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_modoHuellaActivo) return;

                if (!guid.HasValue)
                {
                    // Huella no reconocida
                    lblHuellaEstado.Text  = "HUELLA NO RECONOCIDA";
                    lblHuellaDetalle.Text = "Este dedo no está registrado en el sistema";
                    iconHuellaEsperando.Foreground =
                        new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));

                    ReproducirSonido("acceso_error.wav");

                    // Restaurar después de 2 segundos
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        if (_modoHuellaActivo)
                        {
                            lblHuellaEstado.Text  = "APOYÁ TU DEDO EN EL LECTOR";
                            lblHuellaDetalle.Text = "Modo huella activo";
                            iconHuellaEsperando.Foreground =
                                (Brush)FindResource("GreenMain");
                        }
                    };
                    timer.Start();
                    return;
                }

                // Validar acceso por huella
                try
                {
                    var resultado = _controller.ValidarAccesoPorHuella(guid.Value);
                    panelHuellaEsperando.Visibility = Visibility.Collapsed;
                    FinalizarValidacion(resultado);

                    // Después de mostrar el resultado, volver al modo huella
                    var timer = new DispatcherTimer
                    { Interval = TimeSpan.FromSeconds(5.5) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        if (_modoHuellaActivo)
                        {
                            panelHuellaEsperando.Visibility = Visibility.Visible;
                            lblHuellaEstado.Text  = "APOYÁ TU DEDO EN EL LECTOR";
                            lblHuellaDetalle.Text = "Modo huella activo";
                            iconHuellaEsperando.Foreground = (Brush)FindResource("GreenMain");
                        }
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    NotificacionWindow.MostrarError("Error al validar huella.\n" + ex.Message);
                }
            });
        }

        // ─────────────────────────────────────────────────────
        // RELOJ EN VIVO
        // ─────────────────────────────────────────────────────
        private void IniciarReloj()
        {
            ActualizarReloj();
            _timerReloj = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timerReloj.Tick += (s, e) => ActualizarReloj();
            _timerReloj.Start();
        }

        private void ActualizarReloj()
        {
            DateTime ahora = DateTime.Now;
            lblReloj.Text = ahora.ToString("HH:mm:ss");

            string[] dias = { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };
            string[] meses = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                               "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

            lblFecha.Text = dias[(int)ahora.DayOfWeek] + ", " +
                            ahora.Day + " de " + meses[ahora.Month - 1] + " " + ahora.Year;
        }

        // ─────────────────────────────────────────────────────
        // AUTO-REFRESH cada 10 segundos
        // ─────────────────────────────────────────────────────
        private void IniciarAutoRefresh()
        {
            _timerRefresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _timerRefresh.Tick += (s, e) => { CargarAccesos(); ActualizarStats(); };
            _timerRefresh.Start();
        }

        // ─────────────────────────────────────────────────────
        // CARGAR ACCESOS DEL DÍA + ESTADÍSTICAS
        // ─────────────────────────────────────────────────────
        private void CargarAccesos()
        {
            try
            {
                var hoy = DateTime.Today;
                var lista = _controller.BuscarAccesos(string.Empty, _filtroResultado, hoy, hoy);
                gridAccesos.ItemsSource = lista;
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError(ex.Message, "Error al cargar accesos");
            }
        }

        private void ActualizarStats()
        {
            try
            {
                var stats = _controller.ObtenerEstadisticas();
                statPermitidos.Text = stats.PermitidosHoy.ToString();
                statDenegados.Text = stats.DenegadosHoy.ToString();
                statSociosUnicos.Text = stats.SociosUnicosHoy.ToString();
                statSemana.Text = stats.AccesosSemana.ToString();
            }
            catch
            {
                statPermitidos.Text = statDenegados.Text =
                statSociosUnicos.Text = statSemana.Text = "—";
            }
        }

        // ─────────────────────────────────────────────────────
        // FILTROS
        // ─────────────────────────────────────────────────────
        private void chipFiltro_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            _filtroResultado = btn.Tag.ToString();
            ResaltarChip(btn);
            CargarAccesos();
        }

        private void ResaltarChip(Button seleccionado)
        {
            Button[] chips = { chipTodos, chipPermitidos, chipDenegados };
            foreach (var c in chips)
                c.Style = (Style)FindResource(c == seleccionado
                    ? "BotonChipActivoEstilo"
                    : "BotonChipEstilo");
        }

        // ─────────────────────────────────────────────────────
        // VALIDAR ACCESO — flujo principal
        // ─────────────────────────────────────────────────────
        private void btnValidar_Click(object sender, RoutedEventArgs e)
        {
            // Si el selector está visible y hay membresía elegida → validar con membresiaId
            if (panelSelectorActividad.Visibility == Visibility.Visible
                && cmbActividades.SelectedItem is MembresiaOpcion opcion
                && _dniPendiente != null)
            {
                try
                {
                    var resultado = _controller.ValidarAcceso(_dniPendiente, "manual", opcion.MembresiaId);
                    FinalizarValidacion(resultado);
                }
                catch (Exception ex)
                {
                    NotificacionWindow.MostrarError("Error al validar.\n" + ex.Message);
                }
                return;
            }

            ValidarAcceso();
        }

        private void txtDni_KeyDown(object sender, KeyEventArgs e)
        {
            // ENTER solo dispara si el selector NO está visible
            if (e.Key == Key.Enter && panelSelectorActividad.Visibility == Visibility.Collapsed)
                ValidarAcceso();
        }

        private void ValidarAcceso()
        {
            string dni = txtDni.Text.Trim();
            if (string.IsNullOrEmpty(dni))
            {
                NotificacionWindow.MostrarAdvertencia("Ingresá un DNI para validar.");
                txtDni.Focus();
                return;
            }

            try
            {
                var resultado = _controller.ValidarAcceso(dni, "manual");

                // Socio con más de una membresía activa → mostrar selector
                if (resultado.Resultado == "seleccionar_membresia")
                {
                    _dniPendiente = dni;
                    MostrarSelectorActividad(dni);
                    return;
                }

                FinalizarValidacion(resultado);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al validar.\n" + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────
        // SELECTOR DE ACTIVIDAD
        // ─────────────────────────────────────────────────────
        private void MostrarSelectorActividad(string dni)
        {
            try
            {
                var membresias = _controller.ObtenerMembresiasActivasPorDni(dni);
                cmbActividades.ItemsSource = membresias;
                cmbActividades.SelectedIndex = -1;
                panelSelectorActividad.Visibility = Visibility.Visible;
                btnValidar.IsEnabled = false;
                cmbActividades.Focus();
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("Error al cargar actividades.\n" + ex.Message);
            }
        }

        private void cmbActividades_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Habilitar botón solo cuando hay selección
            btnValidar.IsEnabled = cmbActividades.SelectedItem != null;
        }

        // ─────────────────────────────────────────────────────
        // FINALIZAR VALIDACIÓN — limpia estado y muestra resultado
        // ─────────────────────────────────────────────────────
        private void FinalizarValidacion(ResultadoValidacion resultado)
        {
            panelSelectorActividad.Visibility = Visibility.Collapsed;
            cmbActividades.ItemsSource = null;
            btnValidar.IsEnabled = true;
            _dniPendiente = null;

            MostrarResultado(resultado);
            CargarAccesos();
            ActualizarStats();

            txtDni.Text = string.Empty;
            txtDni.Focus();
        }

        // ─────────────────────────────────────────────────────
        // MOSTRAR RESULTADO en ventana emergente
        // ─────────────────────────────────────────────────────
        private void MostrarResultado(ResultadoValidacion r)
        {
            if (_timerOcultarResultado != null) _timerOcultarResultado.Stop();

            var ventana = new ResultadoAccesoWindow();
            ventana.MostrarResultado(r);

            _timerOcultarResultado = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timerOcultarResultado.Tick += (s, e) =>
            {
                _timerOcultarResultado.Stop();
            };
            _timerOcultarResultado.Start();
        }

        // ─────────────────────────────────────────────────────
        // BLOQUEAR LETRAS EN EL DNI
        // ─────────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────────
        // HELPER
        // ─────────────────────────────────────────────────────
        private void ReproducirSonido(string nombreArchivo)
        {
            try
            {
                string ruta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds", nombreArchivo);
                if (!File.Exists(ruta)) return;

                Task.Run(() =>
                {
                    try
                    {
                        using (var player = new SoundPlayer(ruta))
                        {
                            player.PlaySync();
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }
    }
}