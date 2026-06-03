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
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class ProductoWindow : Window
    {
        private readonly ProductoController _controller = new ProductoController();
        private byte[] _fotoBytes = null;

        public bool ProductoGuardado { get; private set; } = false;

        private bool _esNuevo = true;
        private long _idEditar = 0;
        private Producto _productoActual = null;

        public ProductoWindow()
        {
            InitializeComponent();
        }

        public void ModoNuevo()
        {
            _esNuevo = true;
            _idEditar = 0;
            _productoActual = null;
            lblTituloFormulario.Text = "NUEVO PRODUCTO";
            txtStock.IsEnabled = true;
            lblStock.Text = "STOCK INICIAL";
            panelAjusteStock.Visibility = Visibility.Collapsed;
        }

        public void ModoEditar(Producto p)
        {
            _esNuevo = false;
            _idEditar = p.Id;
            _productoActual = p;

            lblTituloFormulario.Text = "EDITAR PRODUCTO";
            txtNombre.Text = p.Nombre;
            txtDescripcion.Text = p.Descripcion ?? string.Empty;
            cmbCategoria.Text = p.Categoria ?? string.Empty;
            txtPrecio.Text = p.Precio.ToString("F0");
            ActualizarPreviewPrecio();

            txtStock.Text = p.Stock.ToString();
            txtStock.IsEnabled = false;
            lblStock.Text = "STOCK ACTUAL";
            txtStockMin.Text = p.StockMin.ToString();

            _fotoBytes = null;
            if (p.Foto != null && p.Foto.Length > 0)
            {
                imgFotoFormulario.Source = BytesABitmapImage(p.Foto);
                lblSinFoto.Visibility = Visibility.Collapsed;
            }
            else
            {
                imgFotoFormulario.Source = null;
                lblSinFoto.Visibility = Visibility.Visible;
            }

            panelAjusteStock.Visibility = Visibility.Visible;
            txtCantidadAjuste.Text = "0";
            ActualizarLabelStock(p.Stock);
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarTodo()) return;

            decimal precio = 0; decimal.TryParse(txtPrecio.Text, out precio);
            int stock = 0; int.TryParse(txtStock.Text, out stock);
            int stockMin = 0; int.TryParse(txtStockMin.Text, out stockMin);
            string categoria = (cmbCategoria.Text ?? string.Empty).Trim();

            if (_esNuevo)
            {
                var r = _controller.Insertar(
                    txtNombre.Text, txtDescripcion.Text, categoria,
                    precio, stock, stockMin, _fotoBytes);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Producto creado!");
            }
            else
            {
                int cantidadAjuste = 0;
                int.TryParse(txtCantidadAjuste.Text, out cantidadAjuste);

                if (cantidadAjuste != 0)
                {
                    string tipo = cantidadAjuste > 0 ? "sumar" : "restar";
                    int cantidadAbs = cantidadAjuste > 0 ? cantidadAjuste : -cantidadAjuste;
                    var rStock = _controller.AjustarStock(_idEditar, tipo, cantidadAbs);
                    if (!rStock.ok)
                    {
                        NotificacionWindow.MostrarError(rStock.mensaje);
                        return;
                    }
                }

                var r = _controller.Modificar(
                    _idEditar, txtNombre.Text, txtDescripcion.Text, categoria,
                    precio, stockMin, _fotoBytes);

                if (!r.ok) { NotificacionWindow.MostrarError(r.mensaje); return; }
                NotificacionWindow.MostrarExito(r.mensaje, "¡Producto actualizado!");
            }

            ProductoGuardado = true;
            DialogResult = true;
            Close();
        }

        private void btnSubirFoto_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar foto del producto",
                Filter = "Imágenes (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                _fotoBytes = File.ReadAllBytes(dialog.FileName);
                imgFotoFormulario.Source = BytesABitmapImage(_fotoBytes);
                lblSinFoto.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo cargar la imagen.\n" + ex.Message);
            }
        }

        private void btnSumarStock_Click(object sender, RoutedEventArgs e)
        {
            int cantidad = 0;
            int.TryParse(txtCantidadAjuste.Text, out cantidad);
            cantidad++;
            txtCantidadAjuste.Text = cantidad.ToString();
        }

        private void btnRestarStock_Click(object sender, RoutedEventArgs e)
        {
            int cantidad = 0;
            int.TryParse(txtCantidadAjuste.Text, out cantidad);
            if (cantidad > 0)
                cantidad--;
            txtCantidadAjuste.Text = cantidad.ToString();
        }

        private void ActualizarLabelStock(int stock)
        {
            if (stock == 0)
            {
                lblStockActual.Text = "Sin stock";
                lblStockActual.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
            }
            else if (stock == 1)
            {
                lblStockActual.Text = "1 unidad";
                lblStockActual.Foreground = new SolidColorBrush(Color.FromRgb(255, 167, 38));
            }
            else
            {
                lblStockActual.Text = stock + " unidades";
                lblStockActual.Foreground = new SolidColorBrush(Color.FromRgb(232, 245, 232));
            }
        }

        private void txtNombre_LostFocus(object sender, RoutedEventArgs e)
        {
            string err = null;
            if (string.IsNullOrWhiteSpace(txtNombre.Text)) err = "El nombre es obligatorio.";
            else if (txtNombre.Text.Trim().Length < 2) err = "Debe tener al menos 2 caracteres.";
            AplicarEstadoCampo(txtNombre, errNombre, err);
        }

        private void txtPrecio_LostFocus(object sender, RoutedEventArgs e)
        {
            decimal precio = 0;
            string err = null;
            if (string.IsNullOrWhiteSpace(txtPrecio.Text))
                err = "El precio es obligatorio.";
            else if (!decimal.TryParse(txtPrecio.Text, out precio) || precio <= 0)
                err = "El precio debe ser mayor a $0.";
            AplicarEstadoCampo(txtPrecio, errPrecio, err);
            ActualizarPreviewPrecio();
        }

        private void ActualizarPreviewPrecio()
        {
            decimal precio = 0;
            if (decimal.TryParse(txtPrecio.Text, out precio) && precio > 0)
            {
                lblPreviewPrecio.Text = "$" + precio.ToString("N0");
                panelPreviewPrecio.Visibility = Visibility.Visible;
            }
            else panelPreviewPrecio.Visibility = Visibility.Collapsed;
        }

        private void txtPrecio_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[\d]$");
        }

        private void txtSoloNumeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[\d]$");
        }

        private bool ValidarTodo()
        {
            bool ok = true;

            string e1 = null;
            if (string.IsNullOrWhiteSpace(txtNombre.Text)) e1 = "El nombre es obligatorio.";
            else if (txtNombre.Text.Trim().Length < 2) e1 = "Debe tener al menos 2 caracteres.";
            AplicarEstadoCampo(txtNombre, errNombre, e1);
            if (e1 != null) ok = false;

            decimal precio = 0;
            string e2 = null;
            if (string.IsNullOrWhiteSpace(txtPrecio.Text)) e2 = "El precio es obligatorio.";
            else if (!decimal.TryParse(txtPrecio.Text, out precio) || precio <= 0)
                e2 = "El precio debe ser mayor a $0.";
            AplicarEstadoCampo(txtPrecio, errPrecio, e2);
            if (e2 != null) ok = false;

            return ok;
        }

        private void AplicarEstadoCampo(TextBox campo, TextBlock labelError, string mensaje)
        {
            if (mensaje != null)
            {
                campo.Style = (Style)Resources["InputErrorEstilo"];
                labelError.Text = mensaje;
                labelError.Visibility = Visibility.Visible;
            }
            else
            {
                campo.Style = (Style)Resources["InputEstilo"];
                labelError.Text = string.Empty;
                labelError.Visibility = Visibility.Collapsed;
            }
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