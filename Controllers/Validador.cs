// ============================================================
//  CAPA: Controllers
//  Archivo: Validador.cs
//
//  Centraliza TODAS las validaciones del sistema.
//  Está en Controllers porque es lógica pura (regex + reglas),
//  NO depende de WPF y puede ser usado por cualquier consumidor:
//  vista WPF, API web, tests, etc.
//  Compatible con C# 7.3.
// ============================================================

using System.Text.RegularExpressions;

namespace Controllers
{
    public static class Validador
    {
        // ── NOMBRE / APELLIDO ─────────────────────────────────────
        // Solo letras (incluyendo acentos, ñ), espacios y guiones.
        // Mínimo 2 caracteres, máximo 100.
        public static string ValidarNombre(string valor, string campo = "Nombre")
        {
            if (string.IsNullOrWhiteSpace(valor))
                return campo + " es obligatorio.";

            valor = valor.Trim();

            if (valor.Length < 2)
                return campo + " debe tener al menos 2 caracteres.";

            if (valor.Length > 100)
                return campo + " no puede superar los 100 caracteres.";

            if (!Regex.IsMatch(valor, @"^[a-zA-ZáéíóúÁÉÍÓÚüÜñÑ\s\-]+$"))
                return campo + " solo puede contener letras, espacios y guiones. " +
                       "No se permiten números ni símbolos.";

            return null; // null = sin error
        }

        // ── DNI ───────────────────────────────────────────────────
        public static string ValidarDni(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return "El DNI es obligatorio.";

            valor = valor.Trim();

            if (!Regex.IsMatch(valor, @"^\d+$"))
                return "El DNI solo puede contener números, sin puntos ni espacios.";

            if (valor.Length < 7 || valor.Length > 8)
                return "El DNI argentino debe tener entre 7 y 8 dígitos.";

            return null;
        }

        // ── TELÉFONO ──────────────────────────────────────────────
        // Exactamente 10 dígitos numéricos, sin 0 inicial, sin prefijo 15.
        public static string ValidarTelefono(string telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono))
                return "El número de celular es obligatorio.";

            foreach (char c in telefono)
            {
                if (!char.IsDigit(c))
                    return "El celular solo puede contener números, sin letras ni símbolos.";
            }

            if (telefono.Length != 10)
                return "El celular debe tener exactamente 10 dígitos (sin el 0 inicial). Ejemplo: 3884123456";

            if (telefono[0] == '0')
                return "No ingreses el 0 inicial. Ejemplo: 3884123456 (no 03884123456)";

            if (telefono.StartsWith("15"))
                return "No ingreses el 15. Ejemplo: 3884123456 (no 1512345678)";

            return null;
        }

        public static bool EsCaracterTelefonoValido(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return false;
            foreach (char c in texto)
                if (!char.IsDigit(c)) return false;
            return true;
        }

        // ── EMAIL ─────────────────────────────────────────────────
        public static string ValidarEmail(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null; // Opcional

            valor = valor.Trim();

            if (!Regex.IsMatch(valor,
                @"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$"))
                return "El correo electrónico no tiene un formato válido. " +
                       "Ej: nombre@dominio.com";

            if (valor.Length > 191)
                return "El correo electrónico es demasiado largo.";

            return null;
        }

        // ── CONTRASEÑA ────────────────────────────────────────────
        public static string ValidarContrasena(string valor, bool esObligatoria)
        {
            if (!esObligatoria && string.IsNullOrWhiteSpace(valor))
                return null; // En edición es opcional

            if (string.IsNullOrWhiteSpace(valor))
                return "La contraseña es obligatoria.";

            if (valor.Length < 4)
                return "La contraseña debe tener al menos 4 caracteres.";

            if (valor.Length > 50)
                return "La contraseña no puede superar los 50 caracteres.";

            return null;
        }

        // ── HELPER ────────────────────────────────────────────────
        public static bool TieneError(string mensajeError)
        {
            return !string.IsNullOrEmpty(mensajeError);
        }
    }
}