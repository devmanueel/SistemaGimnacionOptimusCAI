// Entities/FormatoARS.cs — C# 7.3
// Centraliza el formato de moneda en pesos argentinos (ARS).
// Vive en Entities para evitar dependencias circulares: cualquier capa
// (Entities, Models, Controllers, WPF) puede usarlo.
//
// Cultura es-AR: punto en miles, coma en decimales.  Ej: 4000 → "$ 4.000,00"
using System.Globalization;

namespace Entities
{
    public static class FormatoARS
    {
        private static readonly CultureInfo Cultura = new CultureInfo("es-AR");

        /// <summary>Moneda con símbolo: "$ 4.000,00"</summary>
        public static string Moneda(decimal valor)
        {
            return valor.ToString("C2", Cultura);
        }

        /// <summary>Número con separadores ARS sin símbolo: "4.000,00"</summary>
        public static string Numero(decimal valor)
        {
            return valor.ToString("N2", Cultura);
        }

        /// <summary>Moneda corta sin decimales: "$ 4.000"</summary>
        public static string MonedaCorta(decimal valor)
        {
            return "$ " + valor.ToString("N0", Cultura);
        }

        /// <summary>
        /// Parsea entrada del usuario a decimal.
        /// Acepta: "4000", "4.000", "4.000,00", "$ 4.000,00", "4000.50".
        /// </summary>
        public static bool TryParsear(string texto, out decimal resultado)
        {
            resultado = 0;
            if (string.IsNullOrWhiteSpace(texto)) return false;

            string limpio = texto.Replace("$", "").Replace(" ", "").Trim();

            if (decimal.TryParse(limpio, NumberStyles.Any, Cultura, out resultado))
                return true;

            if (decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.InvariantCulture, out resultado))
                return true;

            return false;
        }
    }
}
