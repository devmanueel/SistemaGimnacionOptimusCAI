// ============================================================
//  CAPA: Controllers
//  Archivo: UsuarioController.cs
//
//  Actúa como intermediario entre la Vista y el DAO.
//  Responsabilidades:
//    · Validar los datos antes de llamar al DAO
//    · Calcular el hash de la contraseña (SHA-256)
//    · Devolver mensajes de error claros a la Vista
//    · La Vista NUNCA llama al DAO directamente
// ============================================================

using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Controllers
{
    public class UsuarioController
    {
        // El Controller instancia su propio DAO
        private readonly UsuarioDao _dao = new UsuarioDao();

        // ──────────────────────────────────────────────────────────
        // HASH SHA-256
        // La clave NUNCA viaja en claro. Se hashea antes de ir a la BD.
        // ──────────────────────────────────────────────────────────
        public static string HashSHA256(string texto)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(texto));
                var sb = new StringBuilder();
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();  // 64 caracteres hexadecimales
            }
        }

        // ──────────────────────────────────────────────────────────
        // OBTENER TODOS
        // ──────────────────────────────────────────────────────────
        public List<Usuario> ObtenerUsuarios()
        {
            try
            {
                return _dao.ObtenerUsuarios();
            }
            catch (Exception ex)
            {
                throw new Exception("No se pudieron cargar los usuarios.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────────
        // OBTENER USUARIOS ACTIVOS POR ROL
        // ──────────────────────────────────────────────────────────
        public List<Usuario> ObtenerUsuariosActivosPorRol(int rolId)
        {
            try
            {
                return _dao.ObtenerUsuariosActivosPorRol(rolId);
            }
            catch (Exception ex)
            {
                throw new Exception("No se pudieron cargar los usuarios.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────────
        // BUSCAR
        // ──────────────────────────────────────────────────────────
        public List<Usuario> BuscarUsuarios(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return ObtenerUsuarios();

            try
            {
                return _dao.BuscarUsuarios(texto.Trim());
            }
            catch (Exception ex)
            {
                throw new Exception("Error en la búsqueda.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────────
        // OBTENER POR ID
        // ──────────────────────────────────────────────────────────
        public Usuario ObtenerPorId(long id)
        {
            try
            {
                return _dao.ObtenerUsuarioPorId(id);
            }
            catch (Exception ex)
            {
                throw new Exception("No se encontró el usuario.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────────
        // LOGIN
        // ──────────────────────────────────────────────────────────
        public (bool ok, string mensaje, Usuario usuario) Login(string dni, string clave)
        {
            if (string.IsNullOrWhiteSpace(dni))
                return (false, "Ingresá tu DNI.", null);
            if (string.IsNullOrWhiteSpace(clave))
                return (false, "Ingresá tu contraseña.", null);

            try
            {
                var hash = HashSHA256(clave);
                var usuario = _dao.ValidarUsuario(dni.Trim(), hash);

                if (usuario == null)
                    return (false, "DNI o contraseña incorrectos.", null);

                return (true, $"Bienvenido, {usuario.Nombre}!", usuario);
            }
            catch (Exception ex)
            {
                return (false, "Error al iniciar sesión.\n" + ex.Message, null);
            }
        }

        // ──────────────────────────────────────────────────────────
        // INSERTAR
        // Retorna (ok, mensaje, nuevoId)
        // ──────────────────────────────────────────────────────────
        public (bool ok, string mensaje, long nuevoId) Insertar(
            int rolId,
            string nombre,
            string apellido,
            string dni,
            string clave,       // La clave en claro → se hashea aquí
            string domicilio = null,
            string telefono = null,
            string email = null,
            byte[] foto = null,
            decimal tarifaHora = 0)
        {
            // ── Validaciones ──────────────────────────────────────
            if (string.IsNullOrWhiteSpace(nombre))
                return (false, "El nombre es obligatorio.", 0);
            if (string.IsNullOrWhiteSpace(apellido))
                return (false, "El apellido es obligatorio.", 0);
            if (string.IsNullOrWhiteSpace(dni) || dni.Length < 7)
                return (false, "El DNI debe tener entre 7 y 8 dígitos.", 0);
            if (string.IsNullOrWhiteSpace(clave) || clave.Length < 4)
                return (false, "La contraseña debe tener al menos 4 caracteres.", 0);
            if (tarifaHora < 0)
                return (false, "La tarifa por hora no puede ser negativa.", 0);

            var usuario = new Usuario
            {
                RolId = rolId,
                Nombre = nombre.Trim(),
                Apellido = apellido.Trim(),
                Dni = dni.Trim(),
                Domicilio = domicilio?.Trim(),
                Telefono = telefono?.Trim(),
                Email = email?.Trim(),
                PasswordHash = HashSHA256(clave),  // Hash antes de ir a la BD
                Foto = foto,
                Activo = true,
                TarifaHora = tarifaHora
            };

            try
            {
                long id = _dao.InsertarUsuario(usuario);

                if (id == -1)
                    return (false, $"El DNI '{dni}' ya está registrado en el sistema.", 0);
                if (id <= 0)
                    return (false, "No se pudo guardar el usuario. Intentá de nuevo.", 0);

                Auditor.Registrar("crear", "usuario", id, new Dictionary<string, object> {
                    { "nombre", nombre }, { "apellido", apellido }, { "dni", dni }, { "rol_id", rolId }
                });

                return (true, "Usuario registrado correctamente.", id);
            }
            catch (Exception ex)
            {
                return (false, "Error al insertar.\n" + ex.Message, 0);
            }
        }

        // ──────────────────────────────────────────────────────────
        // MODIFICAR
        // Si claveNueva es null o vacía → no se cambia la contraseña.
        // Si foto es null → no se cambia la foto.
        // ──────────────────────────────────────────────────────────
        public (bool ok, string mensaje) Modificar(
            long id,
            int rolId,
            string nombre,
            string apellido,
            string dni,
            string claveNueva = null,   // Opcional: solo si el usuario quiere cambiarla
            string domicilio = null,
            string telefono = null,
            string email = null,
            byte[] foto = null,
            decimal tarifaHora = 0)
        {
            // ── Validaciones ──────────────────────────────────────
            if (string.IsNullOrWhiteSpace(nombre))
                return (false, "El nombre es obligatorio.");
            if (string.IsNullOrWhiteSpace(apellido))
                return (false, "El apellido es obligatorio.");
            if (string.IsNullOrWhiteSpace(dni) || dni.Length < 7)
                return (false, "El DNI debe tener entre 7 y 8 dígitos.");
            if (tarifaHora < 0)
                return (false, "La tarifa por hora no puede ser negativa.");

            bool cambiarPassword = !string.IsNullOrWhiteSpace(claveNueva);
            bool cambiarFoto = foto != null;

            var usuario = new Usuario
            {
                Id = id,
                RolId = rolId,
                Nombre = nombre.Trim(),
                Apellido = apellido.Trim(),
                Dni = dni.Trim(),
                Domicilio = domicilio?.Trim(),
                Telefono = telefono?.Trim(),
                Email = email?.Trim(),
                PasswordHash = cambiarPassword ? HashSHA256(claveNueva) : null,
                Foto = foto,
                TarifaHora = tarifaHora
            };

            try
            {
                bool ok = _dao.ModificarUsuario(usuario, cambiarPassword, cambiarFoto);
                if (ok)
                {
                    Auditor.Registrar("modificar", "usuario", id, new Dictionary<string, object> {
                        { "nombre", nombre }, { "apellido", apellido }, { "dni", dni }
                    });
                }
                return ok
                    ? (true, "Usuario actualizado correctamente.")
                    : (false, "No se encontró el usuario para actualizar.");
            }
            catch (Exception ex)
            {
                // El SP lanza RAISERROR si el DNI está duplicado
                return (false, ex.Message.Contains("DNI")
                    ? ex.Message
                    : "Error al actualizar.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────────
        // MODIFICAR DATOS PROPIOS
        // El usuario solo puede editar sus datos básicos.
        // No puede cambiar rol ni tarifa.
        // ──────────────────────────────────────────────────────────
        public (bool ok, string mensaje) ModificarPropio(
            long id,
            string nombre,
            string apellido,
            string dni,
            string claveNueva = null,
            string domicilio = null,
            string telefono = null,
            string email = null,
            byte[] foto = null)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return (false, "El nombre es obligatorio.");
            if (string.IsNullOrWhiteSpace(apellido))
                return (false, "El apellido es obligatorio.");
            if (string.IsNullOrWhiteSpace(dni) || dni.Length < 7)
                return (false, "El DNI debe tener entre 7 y 8 dígitos.");

            bool cambiarPassword = !string.IsNullOrWhiteSpace(claveNueva);
            bool cambiarFoto = foto != null;

            var usuario = new Usuario
            {
                Id = id,
                Nombre = nombre.Trim(),
                Apellido = apellido.Trim(),
                Dni = dni.Trim(),
                Domicilio = domicilio?.Trim(),
                Telefono = telefono?.Trim(),
                Email = email?.Trim(),
                PasswordHash = cambiarPassword ? HashSHA256(claveNueva) : null,
                Foto = foto
            };

            try
            {
                bool ok = _dao.ModificarUsuarioPropio(usuario, cambiarPassword, cambiarFoto);
                if (ok)
                {
                    Auditor.Registrar("modificar", "usuario_propio", id, new Dictionary<string, object> {
                        { "nombre", nombre }, { "apellido", apellido }, { "dni", dni }
                    });
                }
                return ok
                    ? (true, "Datos actualizados correctamente.")
                    : (false, "No se encontró el usuario para actualizar.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message.Contains("DNI")
                    ? ex.Message
                    : "Error al actualizar.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────────
        // CAMBIAR ESTADO (toggle activo/inactivo)
        // ──────────────────────────────────────────────────────────
        public (bool ok, string mensaje) CambiarEstado(long id, bool nuevoEstado)
        {
            try
            {
                bool ok = _dao.CambiarEstadoUsuario(id, nuevoEstado);
                string accion = nuevoEstado ? "activado" : "desactivado";
                if (ok)
                {
                    Auditor.Registrar(nuevoEstado ? "activar" : "desactivar", "usuario", id);
                }
                return ok
                    ? (true, $"Usuario {accion} correctamente.")
                    : (false, "No se encontró el usuario.");
            }
            catch (Exception ex)
            {
                return (false, "Error al cambiar estado.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────────
        // CAMBIAR CONTRASEÑA
        // ──────────────────────────────────────────────────────────
        public (bool ok, string mensaje) CambiarPassword(long id, string nuevaClave)
        {
            if (string.IsNullOrWhiteSpace(nuevaClave) || nuevaClave.Length < 4)
                return (false, "La contraseña debe tener al menos 4 caracteres.");

            try
            {
                string hash = HashSHA256(nuevaClave);
                bool ok = _dao.CambiarPassword(id, hash);
                if (ok)
                {
                    try { Auditor.Registrar("cambiar_password", "usuario", id); } catch { }
                }
                return ok
                    ? (true, "Contraseña actualizada correctamente.")
                    : (false, "No se encontró el usuario.");
            }
            catch (Exception ex)
            {
                return (false, "Error al cambiar la contraseña.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────────
        // ELIMINAR (soft-delete)
        // ──────────────────────────────────────────────────────────
        public (bool ok, string mensaje) Eliminar(long id)
        {
            try
            {
                bool ok = _dao.EliminarUsuario(id);
                if (ok)
                {
                    Auditor.Registrar("eliminar", "usuario", id);
                }
                return ok
                    ? (true, "Usuario eliminado del sistema.")
                    : (false, "No se encontró el usuario para eliminar.");
            }
            catch (Exception ex)
            {
                return (false, "Error al eliminar.\n" + ex.Message);
            }
        }
    }
}