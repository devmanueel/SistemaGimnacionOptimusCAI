// Models/DAO/ConfiguracionDao.cs — C# 7.3
// Acceso a la tabla configuracion_sistema (clave-valor).
using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace Models.Dao
{
    public class ConfiguracionDao : ConnectionToDB
    {
        public string ObtenerValor(string clave)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerConfiguracion", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Clave", clave);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read()) return r["valor"].ToString();
                }
            }
            return null;
        }

        public decimal ObtenerDecimal(string clave, decimal valorDefault = 0)
        {
            string val = ObtenerValor(clave);
            if (string.IsNullOrEmpty(val)) return valorDefault;

            decimal resultado;
            return decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out resultado)
                ? resultado
                : valorDefault;
        }

        public bool ActualizarValor(string clave, string valor, long? actualizadoPor)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ActualizarConfiguracion", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Clave",          clave);
                    cmd.Parameters.AddWithValue("@Valor",          valor);
                    cmd.Parameters.AddWithValue("@ActualizadoPor",
                        (object)actualizadoPor ?? DBNull.Value);

                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }
    }
}
