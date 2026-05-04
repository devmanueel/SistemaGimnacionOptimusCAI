// ============================================================
//  CAPA: Entities
//  Archivo: CajaMovimiento.cs
//
//  Representa un movimiento de caja: ingreso o gasto.
//  Sin DataAnnotations. Compatible con C# 7.3.
// ============================================================

using System;

namespace Entities
{
    public class CajaMovimiento
    {
        // ── Campos directos de caja_movimientos ──────────────
        public long Id { get; set; }
        public string Tipo { get; set; }   // ingreso_cuota / ingreso_clase / ingreso_venta / gasto / movimiento_interno
        public string Subtipo { get; set; }
        public long UsuarioId { get; set; }
        public long? SocioId { get; set; }
        public long? MembresiaId { get; set; }
        public long? ActividadId { get; set; }
        public long? VentaId { get; set; }
        public string Detalle { get; set; }
        public string MetodoPago { get; set; } = "efectivo";
        public decimal Monto { get; set; }
        public DateTime CreadoEn { get; set; }

        // ── Datos joineados ──────────────────────────────────
        public string UsuarioNombre { get; set; }
        public string SocioNombre { get; set; }
        public string ActividadNombre { get; set; }
        public int? NumeroSocio { get; set; }

        // ── Propiedades calculadas ───────────────────────────

        /// <summary>True si es cualquier tipo de ingreso.</summary>
        public bool EsIngreso => Tipo != null && Tipo.StartsWith("ingreso");

        /// <summary>True si es un gasto.</summary>
        public bool EsGasto => Tipo == "gasto";

        /// <summary>"+$25.000" o "-$5.000" según tipo, formateado.</summary>
        public string MontoConSigno
        {
            get
            {
                string signo = EsIngreso ? "+" : "-";
                return signo + "$" + Monto.ToString("N0");
            }
        }

        /// <summary>"$25.000" sin signo.</summary>
        public string MontoTexto => "$" + Monto.ToString("N0");

        /// <summary>"15/12 14:30" — formato corto para listado.</summary>
        public string FechaCorta => CreadoEn.ToString("dd/MM HH:mm");

        /// <summary>"15/12/2025" — formato largo.</summary>
        public string FechaLarga => CreadoEn.ToString("dd/MM/yyyy");

        /// <summary>"Cuota" / "Venta" / "Clase" / "Gasto" / "Movim."</summary>
        public string TipoTexto
        {
            get
            {
                if (Tipo == "ingreso_cuota") return "Cuota";
                if (Tipo == "ingreso_venta") return "Venta";
                if (Tipo == "ingreso_clase") return "Clase";
                if (Tipo == "gasto") return "Gasto";
                if (Tipo == "movimiento_interno") return "Movim.";
                return Tipo ?? "—";
            }
        }

        /// <summary>Capitalizado: "Efectivo", "Transferencia", etc.</summary>
        public string MetodoPagoTexto
        {
            get
            {
                if (string.IsNullOrEmpty(MetodoPago)) return "—";
                return char.ToUpper(MetodoPago[0]) + MetodoPago.Substring(1);
            }
        }

        /// <summary>
        /// Descripción inteligente del movimiento — lo que se muestra
        /// como detalle principal en el grid.
        /// </summary>
        public string DescripcionInteligente
        {
            get
            {
                if (!string.IsNullOrEmpty(Detalle)) return Detalle;
                if (EsGasto && !string.IsNullOrEmpty(Subtipo)) return "Gasto: " + Subtipo;
                if (!string.IsNullOrEmpty(Subtipo)) return Subtipo;
                return TipoTexto;
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    //  RESUMEN DE CAJA (totales por rango)
    // ──────────────────────────────────────────────────────────
    public class ResumenCaja
    {
        public decimal TotalIngresos { get; set; }
        public decimal TotalGastos { get; set; }
        public decimal IngresosEfectivo { get; set; }
        public decimal IngresosTransferencia { get; set; }
        public decimal IngresosTarjeta { get; set; }
        public decimal IngresosCuotas { get; set; }
        public decimal IngresosVentas { get; set; }
        public decimal IngresosClases { get; set; }
        public int CantidadMovimientos { get; set; }

        public decimal Balance => TotalIngresos - TotalGastos;

        public string TotalIngresosTexto => "$" + TotalIngresos.ToString("N0");
        public string TotalGastosTexto => "$" + TotalGastos.ToString("N0");
        public string BalanceTexto
        {
            get
            {
                string signo = Balance >= 0 ? "+" : "";
                return signo + "$" + Balance.ToString("N0");
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    //  PUNTO DEL GRÁFICO DE 7 DÍAS
    // ──────────────────────────────────────────────────────────
    public class IngresoDiario
    {
        public DateTime Fecha { get; set; }
        public decimal Ingresos { get; set; }
        public decimal Gastos { get; set; }

        public string FechaCorta => Fecha.ToString("dd/MM");
        public string DiaSemana
        {
            get
            {
                string[] dias = { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" };
                return dias[(int)Fecha.DayOfWeek];
            }
        }
    }
}