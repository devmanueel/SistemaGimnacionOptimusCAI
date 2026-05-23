// Entities/Venta.cs — C# 7.3
using System;
using System.Collections.Generic;

namespace Entities
{
    public class Venta
    {
        public long Id { get; set; }
        public long UsuarioId { get; set; }
        public long? SocioId { get; set; }
        public decimal Total { get; set; }
        public string MetodoPago { get; set; } = "efectivo";
        public string Observaciones { get; set; }
        public DateTime CreadoEn { get; set; }

        // Joineados
        public string UsuarioNombre { get; set; }
        public string SocioNombre { get; set; }
        public int? NumeroSocio { get; set; }
        public byte[] SocioFoto { get; set; }
        public int CantidadItems { get; set; }

        public List<VentaItem> Items { get; set; } = new List<VentaItem>();

        // Calculadas
        public string TotalTexto => "$" + Total.ToString("N0");
        public string FechaCorta => CreadoEn.ToString("dd/MM HH:mm");
        public string NumeroVenta => "#" + Id.ToString("D5");
        public bool EsPublicoGeneral => !SocioId.HasValue;
        public string NumeroSocioTexto => NumeroSocio.HasValue ? "#" + NumeroSocio.Value.ToString("D4") : string.Empty;

        public string MetodoPagoTexto
        {
            get
            {
                if (string.IsNullOrEmpty(MetodoPago)) return "-";
                return char.ToUpper(MetodoPago[0]) + MetodoPago.Substring(1);
            }
        }

        public string CantidadItemsTexto
        {
            get
            {
                if (CantidadItems == 0) return "Sin items";
                if (CantidadItems == 1) return "1 producto";
                return CantidadItems + " productos";
            }
        }
    }

    public class VentaItem
    {
        public long Id { get; set; }
        public long VentaId { get; set; }
        public long? ProductoId { get; set; }
        public string Descripcion { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public string ProductoNombre { get; set; }
        public byte[] ProductoFoto { get; set; }

        public string PrecioUnitarioTexto => "$" + PrecioUnitario.ToString("N0");
        public string SubtotalTexto => "$" + Subtotal.ToString("N0");
    }

    public class ItemCarrito
    {
        public long ProductoId { get; set; }
        public string Nombre { get; set; }
        public byte[] Foto { get; set; }
        public decimal PrecioUnitario { get; set; }
        public int Cantidad { get; set; }
        public int StockDisponible { get; set; }

        public decimal Subtotal => Cantidad * PrecioUnitario;
        public string PrecioTexto => "$" + PrecioUnitario.ToString("N0");
        public string SubtotalTexto => "$" + Subtotal.ToString("N0");
        public bool StockAlLimite => Cantidad >= StockDisponible;
    }

    public class EstadisticasVentas
    {
        public int VentasHoy { get; set; }
        public decimal TotalHoy { get; set; }
        public int VentasMes { get; set; }
        public decimal TotalMes { get; set; }

        public string TotalHoyTexto => "$" + TotalHoy.ToString("N0");
        public string TotalMesTexto => "$" + TotalMes.ToString("N0");
    }

    public class TopProducto
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public byte[] Foto { get; set; }
        public int UnidadesVendidas { get; set; }
        public decimal TotalFacturado { get; set; }

        public string UnidadesTexto => UnidadesVendidas + " u.";
        public string TotalFacturadoTexto => "$" + TotalFacturado.ToString("N0");
    }
}