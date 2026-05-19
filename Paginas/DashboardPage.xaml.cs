using Controllers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    /// <summary>
    /// Lógica de interacción para DashboardPage.xaml
    /// </summary>
    public partial class DashboardPage : Page
    {
        private readonly SocioController _controller = new SocioController();
        private readonly MembresiaController _controllerM = new MembresiaController();
        private readonly CajaController _controllerC = new CajaController();
        private readonly AsistenciaController _controllerA = new AsistenciaController();
        private DateTime _mesActual;

        public DashboardPage()
        {
            InitializeComponent();
            _mesActual = DateTime.Today;
            Loaded += DashboardPage_Loaded;
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDatosUsuario();
            CargarFecha();
            GenerarCalendario(_mesActual);
            CargarStats();
            CargarVencimientos();
        }

        // ── DATOS DEL USUARIO ──────────────────────────────────
        private void CargarDatosUsuario()
        {
            if (!SesionManager.HaySesion) return;

            string nombre = SesionManager.NombreCompleto ?? "";
            string[] partes = nombre.Trim().Split(' ');
            string iniciales = partes.Length >= 2
                ? ("" + partes[0][0] + partes[1][0]).ToUpper()
                : (nombre.Length > 0 ? nombre[0].ToString().ToUpper() : "?");

            lblIniciales.Text = iniciales;

            int hora = DateTime.Now.Hour;
            string saludo = hora < 12 ? "Buenos dias" : hora < 19 ? "Buenas tardes" : "Buenas noches";
            lblSaludo.Text = saludo + ", " + (partes.Length > 0 ? partes[0] : nombre);
        }

        // ── FECHA ──────────────────────────────────────────────
        private void CargarFecha()
        {
            CultureInfo cultura = new CultureInfo("es-AR");
            string fecha = DateTime.Today.ToString("dddd, d 'de' MMMM 'de' yyyy", cultura);
            lblFechaHoy.Text = char.ToUpper(fecha[0]) + fecha.Substring(1);
        }

        // ── CALENDARIO ────────────────────────────────────────
        private void GenerarCalendario(DateTime mes)
        {
            CultureInfo cultura = new CultureInfo("es-AR");
            lblMesAnio.Text = mes.ToString("MMMM yyyy", cultura).ToUpper();

            gridCalendario.Children.Clear();

            // Primer dia del mes y dia de la semana (lunes=0)
            DateTime primerDia = new DateTime(mes.Year, mes.Month, 1);
            int diasOffset = ((int)primerDia.DayOfWeek + 6) % 7; // Lunes como primer dia
            int diasEnMes = DateTime.DaysInMonth(mes.Year, mes.Month);

            DateTime hoy = DateTime.Today;

            // Celdas vacias antes del primer dia
            for (int i = 0; i < diasOffset; i++)
                gridCalendario.Children.Add(CrearCeldaVacia());

            // Dias del mes
            for (int dia = 1; dia <= diasEnMes; dia++)
            {
                bool esHoy = (mes.Year == hoy.Year && mes.Month == hoy.Month && dia == hoy.Day);
                bool esDomingo = ((diasOffset + dia - 1) % 7 == 6);
                gridCalendario.Children.Add(CrearCeldaDia(dia, esHoy, esDomingo));
            }

            // Celdas vacias al final para completar la grilla
            int totalCeldas = diasOffset + diasEnMes;
            int celdasRestantes = (6 * 7) - totalCeldas;
            for (int i = 0; i < celdasRestantes; i++)
                gridCalendario.Children.Add(CrearCeldaVacia());
        }

        private Border CrearCeldaDia(int dia, bool esHoy, bool esDomingo)
        {
            var borde = new Border
            {
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand
            };

            if (esHoy)
            {
                borde.Background = new SolidColorBrush(Color.FromArgb(30, 45, 212, 191));
                borde.BorderBrush = new SolidColorBrush(Color.FromRgb(45, 212, 191));
                borde.BorderThickness = new Thickness(1.5);
            }
            else
            {
                borde.Background = Brushes.Transparent;
                borde.BorderBrush = Brushes.Transparent;
                borde.BorderThickness = new Thickness(1);

                borde.MouseEnter += (s, e) =>
                {
                    borde.Background = new SolidColorBrush(Color.FromRgb(30, 58, 47));
                    borde.BorderBrush = new SolidColorBrush(Color.FromRgb(74, 222, 128));
                };
                borde.MouseLeave += (s, e) =>
                {
                    borde.Background = Brushes.Transparent;
                    borde.BorderBrush = Brushes.Transparent;
                };
            }

            var txt = new TextBlock
            {
                Text = dia.ToString(),
                FontSize = 13,
                FontWeight = esHoy ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 6)
            };

            if (esHoy)
                txt.Foreground = new SolidColorBrush(Color.FromRgb(0, 207, 255));
            else if (esDomingo)
                txt.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
            else
                txt.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 210));

            borde.Child = txt;
            return borde;
        }

        private Border CrearCeldaVacia()
        {
            return new Border
            {
                Margin = new Thickness(2),
                Background = Brushes.Transparent
            };
        }

        // ── STATS ─────────────────────────────────────────────
        private void CargarStats()
        {
            var listaSocios = _controller.ObtenerSocios();
            var listaMembresias = _controllerM.ObtenerMembresias();
            var resumenMes = _controllerC.ResumenDelMes();
            var stats = _controllerA.ObtenerAccesosDelDia();
            int membresiasActivas = 0;
            int activos = 0;

            foreach (var s in listaSocios)
            {
                if (s.Activo) activos++;
            }

            foreach (var m in listaMembresias)
            {
                if (m.Estado == "activa") membresiasActivas++; 
            }

            statSociosActivos.Text = activos.ToString();
            statMembresiasActivas.Text = membresiasActivas.ToString();
            statIngresosMes.Text = resumenMes.BalanceTexto;
            statAsistenciasHoy.Text = stats.Count().ToString();
            // TODO: reemplazar con llamadas reales a tus DAOs
            // Ejemplo:
            // statSociosActivos.Text = SocioDao.ContarActivos().ToString();
            // statMembresiasActivas.Text = MembresiaDao.ContarActivas().ToString();
            // statIngresosMes.Text = "$" + CajaDao.IngresosMes().ToString("N0");
            // statAsistenciasHoy.Text = AsistenciaDao.ContarHoy().ToString();
        }

        // ── PROXIMOS VENCIMIENTOS ─────────────────────────────
        private void CargarVencimientos()
        {
            panelVencimientos.Children.Clear();

            // TODO: reemplazar con llamada real a tu DAO
            // var vencimientos = MembresiaDao.GetProximosVencimientos(7);

            // Por ahora muestra placeholder
            var placeholder = new TextBlock
            {
                Text = "No hay vencimientos proximos",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 12)
            };
            panelVencimientos.Children.Add(placeholder);

            // Cuando tengas datos, usa este metodo para agregar cada fila:
            // AgregarFilaVencimiento("Juan Perez", "Boxeo", 3);
        }

        // Metodo auxiliar para agregar una fila de vencimiento
        private void AgregarFilaVencimiento(string nombre, string actividad, int diasRestantes)
        {
            string colorHex = diasRestantes <= 3 ? "#FF5555" :
                              diasRestantes <= 7 ? "#FFA726" : "#6A6A9A";
            var color = (Color)ColorConverter.ConvertFromString(colorHex);

            var fila = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 42)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 3, 0, 3),
                Padding = new Thickness(10, 8, 10, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = nombre,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 232, 255))
            });
            stack.Children.Add(new TextBlock
            {
                Text = actividad,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 154)),
                Margin = new Thickness(0, 1, 0, 0)
            });

            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = diasRestantes == 0 ? "Hoy" : diasRestantes + "d",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color)
            };

            Grid.SetColumn(stack, 0);
            Grid.SetColumn(badge, 1);
            grid.Children.Add(stack);
            grid.Children.Add(badge);

            fila.Child = grid;
            panelVencimientos.Children.Add(fila);
        }

        // ── NAVEGACION ACCESOS RAPIDOS ─────────────────────────
        private void NavegarConSidebar(Type tipo, bool abrirPanel = false)
        {
            if (abrirPanel) SesionManager.AbrirPanelAlNavegar = true;
            var main = Window.GetWindow(this) as MainWindow;
            main?.NavegarA(tipo);
        }

        private void btnRapidoSocio_Click(object sender, RoutedEventArgs e)
            => NavegarConSidebar(typeof(SociosPage), true);

        private void btnRapidoCuota_Click(object sender, RoutedEventArgs e)
            => NavegarConSidebar(typeof(MembresiasPage), true);

        private void btnRapidoAsistencia_Click(object sender, RoutedEventArgs e)
            => NavegarConSidebar(typeof(AsistenciasPage));

        private void btnRapidoVenta_Click(object sender, RoutedEventArgs e)
        {
            SesionManager.AbrirPanelAlNavegar = true;
            NavegarConSidebar(typeof(VentasPage));
        }

        private void btnRapidoTurno_Click(object sender, RoutedEventArgs e)
            => NavegarConSidebar(typeof(TurnosPage), true);
    }
}
