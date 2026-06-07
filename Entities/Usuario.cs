// ============================================================
//  CAPA: Entities
//  Archivo: Usuario.cs
//
//  Representa un usuario del sistema (Admin o Instructor).
//  NO se usan DataAnnotations - las validaciones van
//  en el Controller, igual que en el proyecto de referencia.
// ============================================================

namespace Entities
{
    public class Usuario
    {
        // ── Campos de la tabla usuarios ───────────────────────────
        public long Id { get; set; }
        public int RolId { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Dni { get; set; }
        public string Domicilio { get; set; }
        public string Telefono { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }  // SHA-256, nunca en claro
        public byte[] Foto { get; set; }  // Imagen guardada como bytes en la BD
        public bool Activo { get; set; }
        public decimal TarifaHora { get; set; }

        // ── Huella digital (docentes/instructores) ────────────────
        /// <summary>Identidad lógica de la huella. Su template vive en SQL.</summary>
        public System.Guid? HuellaGuid { get; set; }

        // ── Viene del JOIN con la tabla roles ─────────────────────
        public string RolNombre { get; set; }  // "admin" o "empleado"

        // ── Propiedades calculadas (NO se guardan en la BD) ───────

        /// <summary>Apellido, Nombre — usado en el DataGrid</summary>
        public string NombreCompleto => $"{Apellido}, {Nombre}";

        /// <summary>Texto legible del rol para mostrar en la UI</summary>
        public string RolTexto
        {
            get
            {
                if (RolNombre == "admin")
                    return "Administrador";

                if (RolNombre == "empleado")
                    return "Empleado / Instructor";

                return RolNombre ?? "-";
            }
        }
        /// <summary>Texto legible del estado para el DataGrid</summary>
        public string EstadoTexto => Activo ? "Activo" : "Inactivo";

        public string TarifaTexto => TarifaHora > 0 ? "$" + TarifaHora.ToString("N2") + "/h" : "-";

        /// <summary>True si el docente tiene una huella digital registrada.</summary>
        public bool TieneHuella => HuellaGuid.HasValue;

        /// <summary>True si el usuario es docente/instructor (rol_id = 2).</summary>
        public bool EsDocente => RolId == 2;
    }
}