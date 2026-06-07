using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class FichaMedicaController
    {
        private readonly FichaMedicaDao _dao = new FichaMedicaDao();

        public FichaMedica ObtenerPorSocio(long socioId)
        {
            try { return _dao.ObtenerPorSocio(socioId); }
            catch (Exception ex) { throw new Exception("Error al obtener la ficha médica.\n" + ex.Message); }
        }

        public (bool ok, string mensaje) Guardar(
            long socioId,
            decimal? pesoKg,
            short? alturaCm,
            string grupoSanguineo,
            string enfermedades,
            string medicamentos,
            string restriccionesFisicas,
            string contactoEmergencia,
            string telefonoEmergencia,
            bool aptoFisico,
            DateTime? fechaApto,
            string observaciones)
        {
            if (socioId <= 0) return (false, "Socio inválido.");

            var fm = new FichaMedica
            {
                SocioId = socioId,
                PesoKg = pesoKg,
                AlturaCm = alturaCm,
                GrupoSanguineo = string.IsNullOrWhiteSpace(grupoSanguineo) ? null : grupoSanguineo.Trim(),
                Enfermedades = string.IsNullOrWhiteSpace(enfermedades) ? null : enfermedades.Trim(),
                Medicamentos = string.IsNullOrWhiteSpace(medicamentos) ? null : medicamentos.Trim(),
                RestriccionesFisicas = string.IsNullOrWhiteSpace(restriccionesFisicas) ? null : restriccionesFisicas.Trim(),
                ContactoEmergencia = string.IsNullOrWhiteSpace(contactoEmergencia) ? null : contactoEmergencia.Trim(),
                TelefonoEmergencia = string.IsNullOrWhiteSpace(telefonoEmergencia) ? null : telefonoEmergencia.Trim(),
                AptoFisico = aptoFisico,
                FechaApto = fechaApto,
                Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim(),
                ActualizadoPor = SesionManager.HaySesion ? (long?)SesionManager.UsuarioId : null
            };

            try
            {
                long id = _dao.Guardar(fm);
                if (id <= 0) return (false, "No se pudo guardar la ficha médica.");

                Auditor.Registrar("editar", "ficha_medica", id, new Dictionary<string, object> {
                    { "socio_id", socioId }
                });

                return (true, "Ficha médica guardada correctamente.");
            }
            catch (Exception ex)
            {
                return (false, "Error al guardar la ficha médica.\n" + ex.Message);
            }
        }
    }
}
