using Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class NotificacionMembresiaDao : ConnectionToDB
    {
        public List<NotificacionMembresia> ObtenerMembresiasPorVencer(int diasAntes)
        {
            var lista = new List<NotificacionMembresia>();

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerNotificacionesMembresiasPorVencer", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@DiasAntes", diasAntes);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new NotificacionMembresia
                            {
                                MembresiaId = Convert.ToInt64(reader["membresia_id"]),
                                SocioId = Convert.ToInt64(reader["socio_id"]),
                                NumeroSocio = Convert.ToInt32(reader["numero_socio"]),
                                SocioNombre = reader["socio_nombre"].ToString(),
                                Telefono = reader["telefono"] != DBNull.Value ? reader["telefono"].ToString() : null,
                                ActividadNombre = reader["actividad_nombre"].ToString(),
                                FechaVencimiento = Convert.ToDateTime(reader["fecha_vencimiento"]),
                                DiasParaVencer = Convert.ToInt32(reader["dias_para_vencer"])
                            });
                        }
                    }
                }
            }

            return lista;
        }
    }
}
