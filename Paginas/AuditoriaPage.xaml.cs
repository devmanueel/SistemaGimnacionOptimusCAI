// SistemaGimnacionOptimusCAI/Paginas/AuditoriaPage.xaml.cs — C# 7.3
using Controllers;
using Entities;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class AuditoriaPage : Page
    {
        private readonly AuditoriaController _controller = new AuditoriaController();

        private List<AuditoriaEntry> _todos = new List<AuditoriaEntry>();
        private AuditoriaEntry _entryActual = null;

        public AuditoriaPage()
        {
            InitializeComponent();

            dpDesde.SelectedDate = DateTime.Today.AddDays(-7);
            dpHasta.SelectedDate = DateTime.Today;

            CargarFiltros();
            Cargar();
        }

        // ── FILTROS ───────────────────────────────────────────
        private void CargarFiltros()
        {
            // Usuarios
            var listaUsr = new List<dynamic_>();
            // Como no podemos usar dynamic real en este nivel, usamos ComboBoxItem
            cmbUsuario.Items.Clear();
            cmbUsuario.Items.Add(new ComboBoxItem
            {
                Content = "Todos los usuarios",
                Tag = (long?)null,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232)),
                IsSelected = true
            });
            try
            {
                var usuarios = _controller.ListarUsuarios();
                foreach (var u in usuarios)
                {
                    cmbUsuario.Items.Add(new ComboBoxItem
                    {
                        Content = u.Nombre + " " + u.Apellido,
                        Tag = (long?)u.Id,
                        Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232))
                    });
                }
            }
            catch { }

            // Entidades
            cmbEntidad.Items.Clear();
            cmbEntidad.Items.Add(new ComboBoxItem
            {
                Content = "Todas las entidades",
                Tag = "",
                Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232)),
                IsSelected = true
            });
            foreach (var ent in _controller.ListarEntidades())
            {
                string display = char.ToUpper(ent[0]) + ent.Substring(1);
                cmbEntidad.Items.Add(new ComboBoxItem
                {
                    Content = display,
                    Tag = ent,
                    Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232))
                });
            }

            // Acciones
            cmbAccion.Items.Clear();
            cmbAccion.Items.Add(new ComboBoxItem
            {
                Content = "Todas las acciones",
                Tag = "",
                Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232)),
                IsSelected = true
            });
            foreach (var ac in _controller.ListarAcciones())
            {
                string display = char.ToUpper(ac[0]) + ac.Substring(1);
                cmbAccion.Items.Add(new ComboBoxItem
                {
                    Content = display,
                    Tag = ac,
                    Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232))
                });
            }
        }

        // Tipo dummy para evitar warnings (no se usa realmente)
        private class dynamic_ { }

        // ── CARGA ─────────────────────────────────────────────
        private void Cargar()
        {
            try
            {
                long? actorId = LeerActorIdSeleccionado();
                string ent = LeerTagComboBox(cmbEntidad);
                string acc = LeerTagComboBox(cmbAccion);

                _todos = _controller.Buscar(
                    txtBuscar.Text, actorId,
                    string.IsNullOrEmpty(ent) ? null : ent,
                    string.IsNullOrEmpty(acc) ? null : acc,
                    dpDesde.SelectedDate, dpHasta.SelectedDate);

                ActualizarStats();
                RenderizarTimeline();

                // Refrescar detalle si hay seleccion
                if (_entryActual != null)
                {
                    _entryActual = _controller.ObtenerPorId(_entryActual.Id);
                    if (_entryActual != null) RenderizarDetalle();
                    else MostrarSinSeleccion();
                }
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private long? LeerActorIdSeleccionado()
        {
            var item = cmbUsuario.SelectedItem as ComboBoxItem;
            if (item == null) return null;
            return item.Tag as long?;
        }

        private string LeerTagComboBox(ComboBox cb)
        {
            var item = cb.SelectedItem as ComboBoxItem;
            if (item == null) return string.Empty;
            return item.Tag as string ?? string.Empty;
        }

        private void ActualizarStats()
        {
            try
            {
                var s = _controller.ObtenerEstadisticas();
                statTotal.Text = s.Total.ToString();
                statHoy.Text = s.Hoy.ToString();
                statMes.Text = s.Mes.ToString();
                statUsuarios.Text = s.UsuariosActivosMes.ToString();
            }
            catch
            {
                statTotal.Text = statHoy.Text = statMes.Text = statUsuarios.Text = "-";
            }
        }

        // ── TIMELINE ──────────────────────────────────────────
        private void RenderizarTimeline()
        {
            panelTimeline.Children.Clear();

            if (_todos.Count == 0)
            {
                panelVacio.Visibility = Visibility.Visible;
                return;
            }
            panelVacio.Visibility = Visibility.Collapsed;

            // Agrupar visualmente por dia
            string ultimoDia = null;
            foreach (var entry in _todos)
            {
                string dia = entry.CreadoEn.ToString("dd 'de' MMMM, yyyy",
                    new System.Globalization.CultureInfo("es-AR"));

                if (dia != ultimoDia)
                {
                    panelTimeline.Children.Add(CrearSeparadorDia(dia));
                    ultimoDia = dia;
                }
                panelTimeline.Children.Add(CrearItemTimeline(entry));
            }
        }

        private Border CrearSeparadorDia(string textoDia)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 17, 13)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 40, 30)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 4, 0, 0),
                Child = new TextBlock
                {
                    Text = textoDia.ToUpper(),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(61, 92, 61))
                }
            };
        }

        private Border CrearItemTimeline(AuditoriaEntry e)
        {
            bool seleccionado = _entryActual != null && _entryActual.Id == e.Id;
            Color colorAccion = ColorPorAccion(e.Accion);

            var card = new Border
            {
                Background = new SolidColorBrush(seleccionado
                                    ? Color.FromRgb(22, 32, 22)
                                    : Color.FromRgb(13, 17, 13)),
                BorderBrush = new SolidColorBrush(seleccionado
                                    ? Color.FromRgb(74, 222, 128)
                                    : Color.FromRgb(30, 40, 30)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 12, 20, 12),
                Cursor = Cursors.Hand,
                Tag = e.Id
            };
            card.MouseLeftButtonUp += (s, ev) => Seleccionar(e.Id);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Hora
            var lblHora = new TextBlock
            {
                Text = e.CreadoEn.ToString("HH:mm"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(61, 92, 61)),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 50
            };
            Grid.SetColumn(lblHora, 0);
            grid.Children.Add(lblHora);

            // Punto colored del timeline
            var dot = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(colorAccion),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            };
            Grid.SetColumn(dot, 1);
            grid.Children.Add(dot);

            // Contenido
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var linea1 = new StackPanel { Orientation = Orientation.Horizontal };
            linea1.Children.Add(new TextBlock
            {
                Text = e.IconoEntidad + "  ",
                FontSize = 13
            });
            linea1.Children.Add(new TextBlock
            {
                Text = e.ResumenAccion,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232))
            });
            stack.Children.Add(linea1);

            stack.Children.Add(new TextBlock
            {
                Text = "por " + e.ActorNombre,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(122, 173, 122)),
                Margin = new Thickness(0, 2, 0, 0)
            });

            Grid.SetColumn(stack, 2);
            grid.Children.Add(stack);

            // Badge accion
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, colorAccion.R, colorAccion.G, colorAccion.B)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = e.Accion.ToUpper(),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(colorAccion)
                }
            };
            Grid.SetColumn(badge, 3);
            grid.Children.Add(badge);

            card.Child = grid;
            return card;
        }

        private Color ColorPorAccion(string accion)
        {
            switch ((accion ?? "").ToLower())
            {
                case "crear":
                case "activar":
                    return Color.FromRgb(0, 230, 118);     // verde
                case "editar":
                case "modificar":
                    return Color.FromRgb(0, 207, 255);     // cyan
                case "eliminar":
                case "desactivar":
                case "anular":
                    return Color.FromRgb(255, 85, 85);     // rojo
                case "login":
                case "logout":
                    return Color.FromRgb(167, 139, 250);   // violeta
                default:
                    return Color.FromRgb(160, 160, 192);   // gris
            }
        }

        // ── SELECCION ─────────────────────────────────────────
        private void Seleccionar(long id)
        {
            try
            {
                _entryActual = _controller.ObtenerPorId(id);
                if (_entryActual == null) return;
                RenderizarTimeline();
                RenderizarDetalle();
            }
            catch (Exception ex) { NotificacionWindow.MostrarError(ex.Message); }
        }

        private void MostrarSinSeleccion()
        {
            _entryActual = null;
            panelSinSeleccion.Visibility = Visibility.Visible;
            scrollDetalle.Visibility = Visibility.Collapsed;
        }

        // ── DETALLE ───────────────────────────────────────────
        private void RenderizarDetalle()
        {
            if (_entryActual == null) { MostrarSinSeleccion(); return; }

            panelSinSeleccion.Visibility = Visibility.Collapsed;
            scrollDetalle.Visibility = Visibility.Visible;

            lblDetalleActor.Text = _entryActual.ActorNombre;
            lblDetalleRol.Text = _entryActual.ActorRol == "admin" ? "Administrador" : "Empleado";

            if (_entryActual.ActorFoto != null && _entryActual.ActorFoto.Length > 0)
                imgDetalleFoto.ImageSource = BytesABitmapImage(_entryActual.ActorFoto);
            else
                imgDetalleFoto.ImageSource = null;

            lblIconoEntidad.Text = _entryActual.IconoEntidad;
            lblDetalleResumen.Text = _entryActual.ResumenAccion;
            lblDetalleFechaHora.Text = _entryActual.FechaLarga;

            lblEventoDescripcion.Text = ConstruirDescripcionEvento(_entryActual);
            lblDatoAccion.Text = AccionAmigable(_entryActual.Accion);
            lblDatoEntidad.Text = EntidadAmigable(_entryActual.Entidad, false);
            lblDatoCuando.Text = _entryActual.FechaLarga;
            lblDatoEntidadId.Text = _entryActual.EntidadId.HasValue
                                        ? "#" + _entryActual.EntidadId.Value
                                        : "—";

            // Datos adicionales en lenguaje claro (antes era JSON crudo)
            lblDetalleJson.Text = string.IsNullOrEmpty(_entryActual.Detalle)
                                    ? "Sin datos adicionales para este evento."
                                    : FormatearDetalleHumano(_entryActual.Detalle);
        }

        // ── DESCRIPCIÓN EN LENGUAJE NATURAL ───────────────────
        /// <summary>"Manuel Mendoza editó un socio (registro N° 42) el 06/06/2026 a las 14:30."</summary>
        private string ConstruirDescripcionEvento(AuditoriaEntry e)
        {
            string quien = string.IsNullOrWhiteSpace(e.ActorNombre) ? "Un usuario" : e.ActorNombre;
            string verbo = VerboPasado(e.Accion);
            string entidad = EntidadAmigable(e.Entidad, true);

            string cuando = e.CreadoEn.ToString("dd/MM/yyyy 'a las' HH:mm",
                new System.Globalization.CultureInfo("es-AR"));

            string accion = (e.Accion ?? "").ToLower();
            // Login / logout no tienen entidad
            if (accion == "login" || accion == "logout")
                return quien + " " + verbo + " el " + cuando + ".";

            string idTxt = e.EntidadId.HasValue
                ? " (registro N° " + e.EntidadId.Value + ")"
                : "";

            return quien + " " + verbo + " " + entidad + idTxt + " el " + cuando + ".";
        }

        private static string VerboPasado(string accion)
        {
            switch ((accion ?? "").ToLower())
            {
                case "crear":            return "creó";
                case "editar":           return "editó";
                case "modificar":        return "modificó";
                case "eliminar":         return "eliminó";
                case "activar":          return "activó";
                case "desactivar":       return "dio de baja";
                case "anular":           return "anuló";
                case "login":            return "inició sesión";
                case "logout":           return "cerró sesión";
                case "cambiar_password": return "cambió la contraseña de";
                case "registrar_huella": return "registró la huella de";
                default:                 return (accion ?? "realizó una acción sobre");
            }
        }

        private static string AccionAmigable(string accion)
        {
            switch ((accion ?? "").ToLower())
            {
                case "crear":            return "Registró un alta";
                case "editar":
                case "modificar":        return "Modificó datos";
                case "eliminar":         return "Eliminó un registro";
                case "activar":          return "Activó";
                case "desactivar":       return "Dio de baja";
                case "anular":           return "Anuló";
                case "login":            return "Inició sesión";
                case "logout":           return "Cerró sesión";
                case "cambiar_password": return "Cambió la contraseña";
                case "registrar_huella": return "Registró una huella";
                default:
                    if (string.IsNullOrEmpty(accion)) return "—";
                    return char.ToUpper(accion[0]) + accion.Substring(1);
            }
        }

        /// <summary>Nombre amigable de la entidad. conArticulo=true → "un socio".</summary>
        private static string EntidadAmigable(string entidad, bool conArticulo)
        {
            switch ((entidad ?? "").ToLower())
            {
                case "socio":                 return conArticulo ? "un socio" : "Socio";
                case "usuario":
                case "usuario_propio":        return conArticulo ? "un usuario" : "Usuario";
                case "membresia":             return conArticulo ? "una membresía" : "Membresía";
                case "actividad":             return conArticulo ? "una actividad" : "Actividad";
                case "venta":                 return conArticulo ? "una venta" : "Venta";
                case "producto":              return conArticulo ? "un producto" : "Producto";
                case "caja":                  return conArticulo ? "un movimiento de caja" : "Caja";
                case "rutina":                return conArticulo ? "una rutina" : "Rutina";
                case "turno":                 return conArticulo ? "un turno" : "Turno";
                case "asistencia":
                case "asistencia_dashboard":  return conArticulo ? "una asistencia" : "Asistencia";
                case "casillero":             return conArticulo ? "un casillero" : "Casillero";
                case "whatsapp":              return conArticulo ? "un mensaje de WhatsApp" : "WhatsApp";
                case "sesion":                return conArticulo ? "una sesión" : "Sesión";
                default:
                    if (string.IsNullOrEmpty(entidad)) return conArticulo ? "un registro" : "—";
                    string txt = char.ToUpper(entidad[0]) + entidad.Substring(1);
                    return conArticulo ? "un " + entidad : txt;
            }
        }

        // ── DATOS ADICIONALES: JSON → texto legible ────────────
        private string FormatearDetalleHumano(string json)
        {
            var pares = ParsearJsonPlano(json);
            if (pares.Count == 0)
                return "Sin datos adicionales para este evento.";

            var sb = new StringBuilder();
            foreach (var kv in pares)
            {
                sb.Append("• ");
                sb.Append(EtiquetaCampo(kv.Key));
                sb.Append(": ");
                sb.AppendLine(ValorAmigable(kv.Key, kv.Value));
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Parser sencillo para JSON plano {"clave":"valor",...} producido por Auditor.</summary>
        private static List<KeyValuePair<string, string>> ParsearJsonPlano(string json)
        {
            var lista = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(json)) return lista;

            string s = json.Trim();
            if (s.StartsWith("{")) s = s.Substring(1);
            if (s.EndsWith("}")) s = s.Substring(0, s.Length - 1);

            int i = 0;
            while (i < s.Length)
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == ',' ||
                       s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++;
                if (i >= s.Length || s[i] != '"') break;

                i++; // abrir comilla de la clave
                var key = new StringBuilder();
                while (i < s.Length && s[i] != '"')
                {
                    if (s[i] == '\\' && i + 1 < s.Length) { i++; key.Append(s[i]); }
                    else key.Append(s[i]);
                    i++;
                }
                i++; // cerrar comilla

                while (i < s.Length && (s[i] == ' ' || s[i] == ':')) i++;

                var val = new StringBuilder();
                if (i < s.Length && s[i] == '"')
                {
                    i++;
                    while (i < s.Length && s[i] != '"')
                    {
                        if (s[i] == '\\' && i + 1 < s.Length) { i++; val.Append(s[i]); }
                        else val.Append(s[i]);
                        i++;
                    }
                    i++;
                }
                else
                {
                    while (i < s.Length && s[i] != ',' && s[i] != '}') { val.Append(s[i]); i++; }
                }

                lista.Add(new KeyValuePair<string, string>(key.ToString().Trim(), val.ToString().Trim()));
            }
            return lista;
        }

        private static string EtiquetaCampo(string clave)
        {
            switch ((clave ?? "").ToLower())
            {
                case "nombre":               return "Nombre";
                case "apellido":             return "Apellido";
                case "dni":                  return "DNI";
                case "rol_id":               return "Rol";
                case "email":                return "Email";
                case "telefono":             return "Teléfono";
                case "domicilio":            return "Domicilio";
                case "precio":               return "Precio";
                case "tipo":                 return "Tipo";
                case "motivo":               return "Motivo";
                case "socio_id":             return "Socio";
                case "instructor_id":        return "Instructor";
                case "membresia_id":         return "Membresía";
                case "actividad_id":         return "Actividad";
                case "operacion":            return "Operación";
                case "hora_entrada":         return "Hora de entrada";
                case "hora_salida":          return "Hora de salida";
                case "hora_entrada_nueva":   return "Nueva hora de entrada";
                case "hora_salida_nueva":    return "Nueva hora de salida";
                case "horas_trabajadas":     return "Horas trabajadas";
                case "monto":                return "Monto";
                case "monto_pagado":         return "Monto pagado";
                case "metodo_pago":          return "Método de pago";
                case "registrado_por":       return "Registrado por (usuario)";
                case "corregido_por_admin":  return "Corregido por un admin";
                case "tarifa_hora":          return "Tarifa por hora";
                default:
                    if (string.IsNullOrEmpty(clave)) return "Dato";
                    string limpio = clave.Replace("_", " ");
                    return char.ToUpper(limpio[0]) + limpio.Substring(1);
            }
        }

        private static string ValorAmigable(string clave, string valor)
        {
            if (string.IsNullOrEmpty(valor) || valor == "null") return "—";

            string k = (clave ?? "").ToLower();
            if (k == "rol_id")
            {
                if (valor == "1") return "Administrador";
                if (valor == "2") return "Empleado / Instructor";
            }
            if (valor == "true") return "Sí";
            if (valor == "false") return "No";
            return valor;
        }

        // ── EVENTOS ───────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => Cargar();
        private void cmbFiltro_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Evita que se ejecute antes de inicializar todo
            if (!IsLoaded && !IsInitialized) return;
            Cargar();
        }
        private void dpFecha_Changed(object sender, SelectionChangedEventArgs e) => Cargar();

        // ── HELPERS ───────────────────────────────────────────
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