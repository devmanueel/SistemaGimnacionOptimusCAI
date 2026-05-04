// ============================================================
//  CAPA: Entities
//  Archivo: Producto.cs
//
//  Representa un producto a la venta en el gimnasio
//  (agua, suplementos, ropa, etc.).
//  Compatible con C# 7.3.
// ============================================================

using System;

namespace Entities
{
    public class Producto
    {
        // ── Campos directos ───────────────────────────────────
        public long Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string Categoria { get; set; }
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public int StockMin { get; set; }
        public bool Activo { get; set; } = true;
        public byte[] Foto { get; set; }
        public DateTime CreadoEn { get; set; }

        // ── Calculados desde el SP ────────────────────────────
        public int CantidadVendida { get; set; }

        // ── Propiedades calculadas para la UI ─────────────────

        public string PrecioTexto => "$" + Precio.ToString("N0");

        public string EstadoTexto => Activo ? "Activo" : "Inactivo";

        /// <summary>"Sin stock", "Bajo stock", "Disponible".</summary>
        public string EstadoStock
        {
            get
            {
                if (Stock <= 0) return "Sin stock";
                if (Stock <= StockMin) return "Bajo stock";
                return "Disponible";
            }
        }

        /// <summary>Para los DataTriggers visuales.</summary>
        public bool SinStock => Stock <= 0;
        public bool BajoStock => Stock > 0 && Stock <= StockMin;
        public bool ConStock => Stock > StockMin;

        /// <summary>"5 unidades" / "1 unidad" / "Sin stock".</summary>
        public string StockTexto
        {
            get
            {
                if (Stock <= 0) return "Sin stock";
                if (Stock == 1) return "1 unidad";
                return Stock + " unidades";
            }
        }

        /// <summary>"Valor en stock: $25.000" — para el detalle.</summary>
        public string ValorEnStockTexto
            => "$" + (Stock * Precio).ToString("N0");

        /// <summary>"15 vendidos" / "1 vendido" / "Sin ventas".</summary>
        public string VentasTexto
        {
            get
            {
                if (CantidadVendida <= 0) return "Sin ventas";
                if (CantidadVendida == 1) return "1 vendido";
                return CantidadVendida + " vendidos";
            }
        }

        public string CategoriaTexto
            => string.IsNullOrEmpty(Categoria) ? "Sin categoría" : Categoria;

        public override string ToString() => Nombre;
    }

    // ──────────────────────────────────────────────────────────
    //  ESTADÍSTICAS
    // ──────────────────────────────────────────────────────────
    public class EstadisticasProductos
    {
        public int Total { get; set; }
        public int Activos { get; set; }
        public int SinStock { get; set; }
        public int BajoStock { get; set; }
        public decimal ValorInventario { get; set; }

        public string ValorInventarioTexto => "$" + ValorInventario.ToString("N0");
    }
}