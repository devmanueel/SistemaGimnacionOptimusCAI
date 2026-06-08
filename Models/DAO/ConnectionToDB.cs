// ============================================================
//  CAPA: Models / DAO
//  Archivo: ConnectionToDB.cs
//
//  Clase base de conexión a SQL Server LocalDB.
//  Todos los DAO del proyecto heredan de esta clase
//  y usan GetConnection() para abrir la conexión.
// ============================================================

using System.Data.SqlClient;

namespace Models.Dao
{
    public class ConnectionToDB
    {
        // ── IMPORTANTE: cambiá esta ruta a la de tu archivo .mdf ──
        private readonly string _connectionString =
            @"Data Source=(LocalDB)\MSSQLLocalDB;" +
            @"AttachDbFilename=|DataDirectory|\DataBase\DB_CAI_Optimus.mdf;" +
            @"Integrated Security=True;Connect Timeout=30";

        /// <summary>
        /// Retorna una SqlConnection configurada y lista para .Open().
        /// Usala siempre dentro de un bloque using para que se cierre sola.
        /// </summary>
        protected SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}