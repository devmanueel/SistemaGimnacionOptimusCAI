// Controllers/ConfiguracionController.cs — C# 7.3
// Reglas de negocio + auditoría para configuracion_sistema.
using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Controllers
{
    public class ConfiguracionController
    {
        private readonly ConfiguracionDao _dao = new ConfiguracionDao();

        // ── Tarifa por hora de docentes (global) ─────────────────
        public decimal ObtenerTarifaHoraDocentes()
        {
            try { return _dao.ObtenerDecimal("tarifa_hora_docentes", 4000); }
            catch { return 4000; }
        }

        public (bool ok, string mensaje) ActualizarTarifaHoraDocentes(
            decimal nuevaTarifa, long actualizadoPor)
        {
            if (nuevaTarifa <= 0)
                return (false, "La tarifa debe ser mayor a $0.");
            if (nuevaTarifa > 999999)
                return (false, "La tarifa parece demasiado alta. Verificá el valor.");

            try
            {
                string valorStr = nuevaTarifa.ToString("F2", CultureInfo.InvariantCulture);

                bool ok = _dao.ActualizarValor(
                    "tarifa_hora_docentes", valorStr, actualizadoPor);

                if (ok)
                {
                    Auditor.Registrar("editar", "configuracion", null,
                        new Dictionary<string, object>
                        {
                            { "clave",       "tarifa_hora_docentes" },
                            { "valor_nuevo", nuevaTarifa }
                        });
                    return (true, "Tarifa actualizada a " +
                        FormatoARS.MonedaCorta(nuevaTarifa) +
                        "/h. Se aplica a todos los instructores.");
                }
                return (false, "No se pudo actualizar la tarifa.");
            }
            catch (Exception ex)
            {
                return (false, "Error al guardar: " + ex.Message);
            }
        }

        // ── Datos del gimnasio (para encabezado PDF) ─────────────
        public string ObtenerNombreGimnasio()
        {
            try { return _dao.ObtenerValor("nombre_gimnasio") ?? "OptimusCAI Gym"; }
            catch { return "OptimusCAI Gym"; }
        }

        public string ObtenerDireccion()
        {
            try { return _dao.ObtenerValor("direccion_gimnasio") ?? ""; }
            catch { return ""; }
        }

        public string ObtenerTelefono()
        {
            try { return _dao.ObtenerValor("telefono_gimnasio") ?? ""; }
            catch { return ""; }
        }
    }
}
