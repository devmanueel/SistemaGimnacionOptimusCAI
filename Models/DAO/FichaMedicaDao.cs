using Entities;
using System;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class FichaMedicaDao : ConnectionToDB
    {
        private static FichaMedica Mapear(SqlDataReader r)
        {
            return new FichaMedica
            {
                Id = Convert.ToInt64(r["id"]),
                SocioId = Convert.ToInt64(r["socio_id"]),
                PesoKg = r["peso_kg"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["peso_kg"]) : null,
                AlturaCm = r["altura_cm"] != DBNull.Value ? (short?)Convert.ToInt16(r["altura_cm"]) : null,
                GrupoSanguineo = r["grupo_sanguineo"]?.ToString(),
                Enfermedades = r["enfermedades"]?.ToString(),
                Medicamentos = r["medicamentos"]?.ToString(),
                RestriccionesFisicas = r["restricciones_fisicas"]?.ToString(),
                ContactoEmergencia = r["contacto_emergencia"]?.ToString(),
                TelefonoEmergencia = r["telefono_emergencia"]?.ToString(),
                AptoFisico = Convert.ToBoolean(r["apto_fisico"]),
                FechaApto = r["fecha_apto"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["fecha_apto"]) : null,
                Observaciones = r["observaciones"]?.ToString(),
                ActualizadoEn = Convert.ToDateTime(r["actualizado_en"]),
                ActualizadoPor = r["actualizado_por"] != DBNull.Value ? (long?)Convert.ToInt64(r["actualizado_por"]) : null,
                ActualizadoPorNombre = r["actualizado_por_nombre"]?.ToString()
            };
        }

        public FichaMedica ObtenerPorSocio(long socioId)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerFichaMedica", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SocioId", socioId);

                    using (var reader = cmd.ExecuteReader())
                        if (reader.Read()) return Mapear(reader);
                }
            }
            return null;
        }

        public long Guardar(FichaMedica fm)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_GuardarFichaMedica", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@SocioId", fm.SocioId);
                    cmd.Parameters.AddWithValue("@PesoKg", (object)fm.PesoKg ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@AlturaCm", (object)fm.AlturaCm ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@GrupoSanguineo", (object)fm.GrupoSanguineo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Enfermedades", (object)fm.Enfermedades ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Medicamentos", (object)fm.Medicamentos ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RestriccionesFisicas", (object)fm.RestriccionesFisicas ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ContactoEmergencia", (object)fm.ContactoEmergencia ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TelefonoEmergencia", (object)fm.TelefonoEmergencia ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@AptoFisico", fm.AptoFisico);
                    cmd.Parameters.AddWithValue("@FechaApto", (object)fm.FechaApto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Observaciones", (object)fm.Observaciones ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ActualizadoPor", (object)fm.ActualizadoPor ?? DBNull.Value);

                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt64(result) : 0;
                }
            }
        }
    }
}
