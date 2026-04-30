// ============================================================
//  CAPA: Views / Paginas
//  Archivo: UsuariosPage.xaml.cs  (Code-behind)
//
//  La Vista SOLO habla con el Controller.
//  Nunca instancia el DAO ni hace operaciones de BD directas.
// ============================================================

using Controllers;
using Entities;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Paginas
{
    public partial class UsuariosPage : Page
    {
        // ── Controller: único punto de entrada a la lógica ────────
        private readonly UsuarioController _controller = new UsuarioController();

        // ── Estado interno del formulario ─────────────────────────
        private bool _esNuevo = true;   // true=Insertar  false=Editar
        private long _idEditar = 0;      // ID del usuario que estamos editando
        private byte[] _fotoBytes = null;   // Foto seleccionada (null = sin cambio)

        // ─────────────────────────────────────────────────────────
        // CONSTRUCTOR
        // ─────────────────────────────────────────────────────────
        public UsuariosPage()
        {
            InitializeComponent();
            CargarUsuarios();   // Carga la grilla al abrir la página
        }

        // ─────────────────────────────────────────────────────────
        // CARGAR / ACTUALIZAR LA GRILLA
        // ─────────────────────────────────────────────────────────
        private void CargarUsuarios()
        {
            try
            {
                gridUsuarios.ItemsSource = _controller.ObtenerUsuarios();
            }
            catch (Exception ex)
            {
                MostrarError(ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────
        // BUSCADOR EN TIEMPO REAL
        // ─────────────────────────────────────────────────────────
        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                gridUsuarios.ItemsSource = _controller.BuscarUsuarios(txtBuscar.Text);
            }
            catch (Exception ex)
            {
                MostrarError(ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────
        // SELECCIÓN EN LA GRILLA (para saber qué usuario está activo)
        // ─────────────────────────────────────────────────────────
        private void gridUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Podemos usar esto para habilitar botones de acción si queremos
        }

        // ─────────────────────────────────────────────────────────
        // BOTÓN: NUEVO USUARIO
        // ─────────────────────────────────────────────────────────
        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            _esNuevo = true;
            _idEditar = 0;
            LimpiarFormulario();
            AbrirFormulario("Nuevo Usuario");

            // En modo Nuevo la contraseña es obligatoria
            lblClave.Text = "Contraseña *";
            lblClaveAclaracion.Visibility = Visibility.Collapsed;
        }

        // ─────────────────────────────────────────────────────────
        // BOTÓN: EDITAR (en columna Acciones del DataGrid)
        // ─────────────────────────────────────────────────────────
        private void btnEditar_Click(object sender, RoutedEventArgs e)
        {
            // Obtenemos el usuario de la fila clickeada
            var usuario = ObtenerUsuarioDeFila(sender);
            if (usuario == null) return;

            _esNuevo = false;
            _idEditar = usuario.Id;

            // Rellenamos el formulario con sus datos actuales
            txtNombre.Text = usuario.Nombre;
            txtApellido.Text = usuario.Apellido;
            txtDni.Text = usuario.Dni;
            txtEmail.Text = usuario.Email;
            txtTelefono.Text = usuario.Telefono;
            txtClave.Password = string.Empty;   // No mostramos la clave actual
            _fotoBytes = null;           // No cambiamos la foto salvo que elija una nueva

            // Seleccionar el rol en el ComboBox
            foreach (ComboBoxItem item in cmbRol.Items)
                if ((int)item.Tag == usuario.RolId) { cmbRol.SelectedItem = item; break; }

            // Mostrar foto actual si tiene
            if (usuario.Foto != null && usuario.Foto.Length > 0)
                imgFotoFormulario.ImageSource = BytesABitmapImage(usuario.Foto);
            else
                imgFotoFormulario.ImageSource = null;

            // En modo Editar la contraseña es opcional
            lblClave.Text = "Nueva contraseña (opcional)";
            lblClaveAclaracion.Visibility = Visibility.Visible;

            AbrirFormulario("Editar Usuario");
        }

        // ─────────────────────────────────────────────────────────
        // BOTÓN: TOGGLE ESTADO (activar/desactivar)
        // ─────────────────────────────────────────────────────────
        private void btnToggleEstado_Click(object sender, RoutedEventArgs e)
        {
            var usuario = ObtenerUsuarioDeFila(sender);
            if (usuario == null) return;

            bool nuevoEstado = !usuario.Activo;
            string accion = nuevoEstado ? "activar" : "desactivar";

            var confirmar = MessageBox.Show(
                $"¿Querés {accion} al usuario {usuario.NombreCompleto}?",
                "Confirmar acción",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmar != MessageBoxResult.Yes) return;

            try
            {
                var (ok, mensaje) = _controller.CambiarEstado(usuario.Id, nuevoEstado);
                if (ok)
                {
                    MostrarExito(mensaje);
                    CargarUsuarios();
                }
                else
                    MostrarError(mensaje);
            }
            catch (Exception ex)
            {
                MostrarError(ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────
        // BOTÓN: SUBIR FOTO (abre el explorador de archivos)
        // ─────────────────────────────────────────────────────────
        private void btnSubirFoto_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar foto de perfil",
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
                MostrarError("No se pudo cargar la imagen.\n" + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────
        // BOTÓN: GUARDAR (Insertar o Modificar según modo)
        // ─────────────────────────────────────────────────────────
        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // Obtenemos el RolId del ComboBox seleccionado
            ComboBoxItem rolItem = cmbRol.SelectedItem as ComboBoxItem;

            // 2. Verificamos si la conversión falló (es decir, si es nulo)
            if (rolItem == null)
            {
                MostrarError("Seleccioná un rol para el usuario.");
                return;
            }

            // Si llegó aquí, rolItem ya existe y podemos sacar el Tag
            int rolId = Convert.ToInt32(rolItem.Tag);


            if (_esNuevo)
            {
                // ── INSERTAR ──────────────────────────────────────
                var (ok, mensaje, nuevoId) = _controller.Insertar(
                    rolId: rolId,
                    nombre: txtNombre.Text,
                    apellido: txtApellido.Text,
                    dni: txtDni.Text,
                    clave: txtClave.Password,
                    telefono: txtTelefono.Text,
                    email: txtEmail.Text,
                    foto: _fotoBytes);

                if (!ok) { MostrarError(mensaje); return; }

                MostrarExito(mensaje);
            }
            else
            {
                // ── MODIFICAR ─────────────────────────────────────
                var (ok, mensaje) = _controller.Modificar(
                    id: _idEditar,
                    rolId: rolId,
                    nombre: txtNombre.Text,
                    apellido: txtApellido.Text,
                    dni: txtDni.Text,
                    claveNueva: txtClave.Password,   // Si está vacío el controller no la cambia
                    telefono: txtTelefono.Text,
                    email: txtEmail.Text,
                    foto: _fotoBytes);          // Si es null el controller no la cambia

                if (!ok) { MostrarError(mensaje); return; }

                MostrarExito(mensaje);
            }

            // Cerrar formulario y recargar grilla
            CerrarFormulario();
            CargarUsuarios();
        }

        // ─────────────────────────────────────────────────────────
        // BOTÓN: CANCELAR
        // ─────────────────────────────────────────────────────────
        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            CerrarFormulario();
        }

        // ─────────────────────────────────────────────────────────
        // HELPERS DE UI
        // ─────────────────────────────────────────────────────────

        /// <summary>Muestra el panel lateral y pone el título correspondiente.</summary>
        private void AbrirFormulario(string titulo)
        {
            lblTituloFormulario.Text = titulo;
            panelFormulario.Visibility = Visibility.Visible;
        }

        /// <summary>Oculta el panel lateral y limpia los campos.</summary>
        private void CerrarFormulario()
        {
            panelFormulario.Visibility = Visibility.Collapsed;
            LimpiarFormulario();
        }

        /// <summary>Vuelve todos los inputs a su estado inicial.</summary>
        private void LimpiarFormulario()
        {
            txtNombre.Text = string.Empty;
            txtApellido.Text = string.Empty;
            txtDni.Text = string.Empty;
            txtEmail.Text = string.Empty;
            txtTelefono.Text = string.Empty;
            txtClave.Password = string.Empty;
            cmbRol.SelectedIndex = 0;
            imgFotoFormulario.ImageSource = null;
            _fotoBytes = null;
            _idEditar = 0;
        }

        /// <summary>
        /// Obtiene el Usuario del contexto de datos de la fila donde se hizo click.
        /// Funciona para cualquier botón dentro de las columnas del DataGrid.
        /// </summary>
        private Usuario ObtenerUsuarioDeFila(object sender)
        {
            if (sender is Button btn && btn.DataContext is Usuario usuario)
                return usuario;
            return null;
        }

        /// <summary>Convierte byte[] a BitmapImage para mostrar en la UI.</summary>
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

        /// <summary>MessageBox de éxito (verde) usando MessageBox estándar de WPF.</summary>
        private void MostrarExito(string mensaje)
        {
            MessageBox.Show(mensaje, "✅ Éxito",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>MessageBox de error.</summary>
        private void MostrarError(string mensaje)
        {
            MessageBox.Show(mensaje, "⚠️ Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}