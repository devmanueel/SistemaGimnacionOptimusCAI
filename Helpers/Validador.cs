// ============================================================
//  Archivo: Validador.cs
//  Proyecto: SistemaGimnacionOptimusCAI (o Helpers)
//
//  Clase estática que centraliza TODAS las validaciones.
//  Así si mañana cambia una regla, la cambiás en un solo lugar.
//  Compatible con C# 7.3 — sin sintaxis moderna.
// ============================================================

using System.Text.RegularExpressions;

namespace SistemaGimnacionOptimusCAI.Helpers
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

            // Solo letras, tildes, ñ, espacios y guiones
            if (!Regex.IsMatch(valor, @"^[a-zA-ZáéíóúÁÉÍÓÚüÜñÑ\s\-]+$"))
                return campo + " solo puede contener letras, espacios y guiones. No se permiten números ni símbolos.";

            return null; // null = sin error
        }

        // ── DNI ───────────────────────────────────────────────────
        // Solo dígitos, entre 7 y 8 caracteres.
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
        // Opcional, pero si se ingresa: solo números, +, espacios y guiones.
        // Entre 8 y 15 caracteres.
        public static string ValidarTelefono(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null; // Campo opcional → sin error si está vacío

            valor = valor.Trim();

            if (!Regex.IsMatch(valor, @"^[\d\+\-\s\(\)]+$"))
                return "El teléfono solo puede contener números, +, -, paréntesis y espacios.";

            // Contar solo los dígitos para la validación de longitud
            string soloDigitos = Regex.Replace(valor, @"\D", "");
            if (soloDigitos.Length < 6)
                return "El teléfono debe tener al menos 6 dígitos.";
            if (soloDigitos.Length > 15)
                return "El teléfono no puede superar los 15 dígitos.";

            return null;
        }

        // ── EMAIL ─────────────────────────────────────────────────
        // Opcional, pero si se ingresa debe tener formato válido.
        public static string ValidarEmail(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null; // Opcional → sin error si está vacío

            valor = valor.Trim();

            // Regex RFC básica para email — funciona en C# 7.3
            if (!Regex.IsMatch(valor,
                @"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$"))
                return "El correo electrónico no tiene un formato válido. Ej: nombre@dominio.com";

            if (valor.Length > 191)
                return "El correo electrónico es demasiado largo.";

            return null;
        }

        // ── CONTRASEÑA ────────────────────────────────────────────
        // Solo obligatoria al crear. Al editar puede estar vacía.
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

        // ── HELPER: ¿Tiene error? ─────────────────────────────────
        // Retorna true si el mensaje de error NO es null ni vacío.
        public static bool TieneError(string mensajeError)
        {
            return !string.IsNullOrEmpty(mensajeError);
        }
    }
}