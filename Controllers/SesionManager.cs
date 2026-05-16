// ============================================================
//  Controllers/SesionManager.cs
//
//  Singleton estatico que guarda el usuario logueado.
//  Reemplaza USUARIO_ACTUAL_ID = 1 en todos los modulos.
//
//  USO:
//    // Al hacer login exitoso:
//    SesionManager.Iniciar(usuario);
//
//    // En cualquier pagina, donde antes habia = 1:
//    long id = SesionManager.UsuarioId;
//    bool esAdmin = SesionManager.EsAdmin;
//
//    // Al cerrar sesion:
//    SesionManager.Cerrar();
//
//  Compatible con C# 7.3.
// ============================================================

namespace Controllers
{
    public static class SesionManager
    {
        // ── Datos del usuario activo ──────────────────────────
        public static long UsuarioId { get; private set; }
        public static string Nombre { get; private set; }
        public static string Apellido { get; private set; }
        public static string Dni { get; private set; }
        public static int RolId { get; private set; }
        public static string RolNombre { get; private set; }

        // ── Flags de navegacion ────────────────────────────────
        public static bool AbrirPanelAlNavegar { get; set; }

        // ── Estado ───────────────────────────────────────────
        public static bool HaySesion => UsuarioId > 0;
        public static bool EsAdmin => RolNombre == "admin";

        /// <summary>Nombre completo: "Juan Perez"</summary>
        public static string NombreCompleto
            => Nombre + " " + Apellido;

        /// <summary>Texto para mostrar en la UI: "Juan Perez (Admin)"</summary>
        public static string NombreConRol
        {
            get
            {
                string rol = EsAdmin ? "Admin" : "Empleado";
                return NombreCompleto + "  ·  " + rol;
            }
        }

        // ── Métodos ───────────────────────────────────────────
        public static void Iniciar(long id, string nombre, string apellido,
                                   string dni, int rolId, string rolNombre)
        {
            UsuarioId = id;
            Nombre = nombre;
            Apellido = apellido;
            Dni = dni;
            RolId = rolId;
            RolNombre = rolNombre;
        }

        public static void Cerrar()
        {
            UsuarioId = 0;
            Nombre = null;
            Apellido = null;
            Dni = null;
            RolId = 0;
            RolNombre = null;
        }
    }
}