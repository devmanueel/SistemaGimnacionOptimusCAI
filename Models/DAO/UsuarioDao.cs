// ============================================================
//  CAPA: Models / DAO
//  Archivo: UsuarioDao.cs
//
//  Acceso a datos para la entidad Usuario.
//  TODAS las operaciones van por Stored Procedures (no SQL inline).
//  Hereda ConnectionToDB para obtener la conexión.
// ============================================================

using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class UsuarioDao : ConnectionToDB
    {
        // ── HELPER PRIVADO: convierte un DataReader fila → Usuario ──
        // Lo usamos en cada método para no repetir el mapeo.
        private static Usuario MapearUsuario(SqlDataReader r)
        {
            return new Usuario
            {
                Id = Convert.ToInt64(r["id"]),
                RolId = Convert.ToInt32(r["rol_id"]),
                RolNombre = r["rol_nombre"].ToString(),
                Nombre = r["nombre"].ToString(),
                Apellido = r["apellido"].ToString(),
                Dni = r["dni"].ToString(),
                Domicilio = LeerColumnaSegura(r, "domicilio"),
                Telefono = LeerColumnaSegura(r, "telefono"),
                Email = LeerColumnaSegura(r, "email"),
                PasswordHash = LeerColumnaSegura(r, "password_hash") ?? string.Empty,
                Foto = r["foto"] != DBNull.Value ? (byte[])r["foto"] : null,
                Activo = Convert.ToBoolean(r["activo"]),
                TarifaHora = LeerDecimalSegura(r, "tarifa_hora")
            };
        }

        private static decimal LeerDecimalSegura(SqlDataReader r, string columna)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (r.GetName(i).Equals(columna, StringComparison.OrdinalIgnoreCase))
                    return r[columna] != DBNull.Value ? Convert.ToDecimal(r[columna]) : 0m;
            return 0m;
        }

        private static string LeerColumnaSegura(SqlDataReader r, string columna)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (r.GetName(i).Equals(columna, StringComparison.OrdinalIgnoreCase))
                    return r[columna] as string;
            return null;
        }

        // ──────────────────────────────────────────────────────────
        // OBTENER TODOS
        // ──────────────────────────────────────────────────────────
        public List<Usuario> ObtenerUsuarios()
        {
            var lista = new List<Usuario>();

            using (var conn = GetConnection())
            {
                conn.Open();

                // Llamamos al SP sin parámetros
                using (var cmd = new SqlCommand("sp_ObtenerUsuarios", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearUsuario(reader));
                }
            }

            return lista;
        }

        // ──────────────────────────────────────────────────────────
        // OBTENER POR ID
        // ──────────────────────────────────────────────────────────
        public Usuario ObtenerUsuarioPorId(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                using (var cmd = new SqlCommand("sp_ObtenerUsuarioPorId", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);

                    using (var reader = cmd.ExecuteReader())
                        if (reader.Read()) return MapearUsuario(reader);
                }
            }
            return null;
        }

        // ──────────────────────────────────────────────────────────
        // BUSCAR (barra de búsqueda en tiempo real)
        // ──────────────────────────────────────────────────────────
        public List<Usuario> BuscarUsuarios(string texto)
        {
            var lista = new List<Usuario>();

            using (var conn = GetConnection())
            {
                conn.Open();

                using (var cmd = new SqlCommand("sp_BuscarUsuarios", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Texto", texto);

                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            lista.Add(MapearUsuario(reader));
                }
            }

            return lista;
        }

        // ──────────────────────────────────────────────────────────
        // LOGIN
        // Retorna el usuario si las credenciales son correctas,
        // o NULL si el DNI/hash no coinciden o está inactivo.
        // ──────────────────────────────────────────────────────────
        public Usuario ValidarUsuario(string dni, string passwordHash)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                using (var cmd = new SqlCommand("sp_ValidarUsuario", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Dni", dni);
                    cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);

                    using (var reader = cmd.ExecuteReader())
                        if (reader.Read()) return MapearUsuario(reader);
                }
            }
            return null;
        }

        // ──────────────────────────────────────────────────────────
        // INSERTAR
        // Retorna:  > 0  → ID generado (éxito)
        //           = -1 → DNI duplicado
        //           = 0  → otro error
        // ──────────────────────────────────────────────────────────
        public long InsertarUsuario(Usuario u)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                using (var cmd = new SqlCommand("sp_InsertarUsuario", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@RolId", u.RolId);
                    cmd.Parameters.AddWithValue("@Nombre", u.Nombre);
                    cmd.Parameters.AddWithValue("@Apellido", u.Apellido);
                    cmd.Parameters.AddWithValue("@Dni", u.Dni);
                    cmd.Parameters.AddWithValue("@Domicilio", (object)u.Domicilio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Telefono", (object)u.Telefono ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email", (object)u.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PasswordHash", u.PasswordHash);
                    cmd.Parameters.AddWithValue("@TarifaHora", u.TarifaHora);

                    // Para VARBINARY usamos un SqlParameter tipado
                    var fotoParam = new SqlParameter("@Foto", SqlDbType.VarBinary);
                    fotoParam.Value = u.Foto != null ? (object)u.Foto : DBNull.Value;
                    cmd.Parameters.Add(fotoParam);

                    // El SP retorna una fila con la columna "id"
                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? Convert.ToInt64(resultado) : 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────────
        // MODIFICAR
        // Retorna true si se actualizó al menos 1 fila.
        // Lanza excepción si el DNI ya lo usa otro usuario.
        // ──────────────────────────────────────────────────────────
        public bool ModificarUsuario(Usuario u, bool cambiarPassword = false, bool cambiarFoto = false)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                using (var cmd = new SqlCommand("sp_ModificarUsuario", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@Id", u.Id);
                    cmd.Parameters.AddWithValue("@RolId", u.RolId);
                    cmd.Parameters.AddWithValue("@Nombre", u.Nombre);
                    cmd.Parameters.AddWithValue("@Apellido", u.Apellido);
                    cmd.Parameters.AddWithValue("@Dni", u.Dni);
                    cmd.Parameters.AddWithValue("@Domicilio", (object)u.Domicilio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Telefono", (object)u.Telefono ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email", (object)u.Email ?? DBNull.Value);

                    // Solo manda password si se quiere cambiar
                    cmd.Parameters.AddWithValue("@PasswordHash",
                        cambiarPassword ? (object)u.PasswordHash : DBNull.Value);

                    cmd.Parameters.AddWithValue("@TarifaHora", u.TarifaHora);

                    // Solo manda foto si se quiere cambiar
                    var fotoParam = new SqlParameter("@Foto", SqlDbType.VarBinary);
                    fotoParam.Value = cambiarFoto && u.Foto != null ? (object)u.Foto : DBNull.Value;
                    cmd.Parameters.Add(fotoParam);

                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────────
        // MODIFICAR DATOS PROPIOS (sin cambiar rol ni tarifa)
        // ──────────────────────────────────────────────────────────
        public bool ModificarUsuarioPropio(Usuario u, bool cambiarPassword = false, bool cambiarFoto = false)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                using (var cmd = new SqlCommand("sp_ModificarUsuarioPropio", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@Id", u.Id);
                    cmd.Parameters.AddWithValue("@Nombre", u.Nombre);
                    cmd.Parameters.AddWithValue("@Apellido", u.Apellido);
                    cmd.Parameters.AddWithValue("@Dni", u.Dni);
                    cmd.Parameters.AddWithValue("@Domicilio", (object)u.Domicilio ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Telefono", (object)u.Telefono ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email", (object)u.Email ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@PasswordHash",
                        cambiarPassword ? (object)u.PasswordHash : DBNull.Value);

                    var fotoParam = new SqlParameter("@Foto", SqlDbType.VarBinary);
                    fotoParam.Value = cambiarFoto && u.Foto != null ? (object)u.Foto : DBNull.Value;
                    cmd.Parameters.Add(fotoParam);

                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────────
        // CAMBIAR ESTADO (activar / desactivar)
        // ──────────────────────────────────────────────────────────
        public bool CambiarEstadoUsuario(long id, bool nuevoEstado)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                using (var cmd = new SqlCommand("sp_CambiarEstadoUsuario", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Activo", nuevoEstado);

                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────────
        // CAMBIAR CONTRASEÑA
        // ──────────────────────────────────────────────────────────
        public bool CambiarPassword(long id, string nuevoHash)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                using (var cmd = new SqlCommand("sp_CambiarPasswordUsuario", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@NuevoHashSHA256", nuevoHash);

                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────────────────
        // ELIMINAR (soft-delete)
        // ──────────────────────────────────────────────────────────
        public bool EliminarUsuario(long id)
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                using (var cmd = new SqlCommand("sp_EliminarUsuario", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", id);

                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }
    }
}