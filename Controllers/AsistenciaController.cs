// ============================================================
//  CAPA: Controllers
//  Archivo: AsistenciaController.cs
//
//  Toda la lógica de validación está en el SP. Acá solo
//  validamos el formato del DNI antes de mandarlo y manejamos
//  errores. Compatible con C# 7.3.
// ============================================================

using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class AsistenciaController
    {
        private readonly AsistenciaDao _dao = new AsistenciaDao();
        private readonly SocioController _socioCtrl = new SocioController();

        // ──────────────────────────────────────────────────────
        // VALIDAR ACCESO POR DNI
        //  Valida formato del DNI y delega al SP toda la lógica
        //  de membresía, día permitido, etc.
        // ──────────────────────────────────────────────────────
        public ResultadoValidacion ValidarAcceso(string dni, string metodo = "dni_pin", long? membresiaId = null)
        {
            string err = Validador.ValidarDni(dni);
            if (err != null)
            {
                return new ResultadoValidacion
                {
                    Resultado = "denegado_socio_inactivo",
                    Mensaje = err
                };
            }
            try
            {
                return _dao.ValidarAccesoPorDni(dni.Trim(), metodo, membresiaId);
            }
            catch (Exception ex)
            {
                return new ResultadoValidacion
                {
                    Resultado = "denegado_socio_inactivo",
                    Mensaje = "Error al validar el acceso.\n" + ex.Message
                };
            }
        }

        // ──────────────────────────────────────────────────────
        // VALIDAR ACCESO POR HUELLA
        //  Identifica al socio por su GUID de huella y delega
        //  al flujo estándar de validación por DNI.
        // ──────────────────────────────────────────────────────
        public ResultadoValidacion ValidarAccesoPorHuella(Guid huellaGuid)
        {
            try
            {
                string dni = _socioCtrl.ObtenerDniPorHuellaGuid(huellaGuid);
                if (string.IsNullOrEmpty(dni))
                {
                    return new ResultadoValidacion
                    {
                        Resultado = "denegado_huella",
                        Mensaje   = "Huella no registrada en el sistema."
                    };
                }
                return _dao.ValidarAccesoPorDni(dni, "huella", null);
            }
            catch (Exception ex)
            {
                return new ResultadoValidacion
                {
                    Resultado = "denegado_huella",
                    Mensaje   = "Error al validar por huella.\n" + ex.Message
                };
            }
        }

        // ──────────────────────────────────────────────────────
        // ACCESOS DEL DÍA
        // ──────────────────────────────────────────────────────
        public List<RegistroAcceso> ObtenerAccesosDelDia(int limite = 50)
        {
            try { return _dao.ObtenerAccesosDelDia(limite); }
            catch { return new List<RegistroAcceso>(); }
        }

        // ──────────────────────────────────────────────────────
        // BUSCAR ACCESOS
        // ──────────────────────────────────────────────────────
        public List<RegistroAcceso> BuscarAccesos(string texto, string filtroResultado,
                                                  DateTime? desde, DateTime? hasta)
        {
            try { return _dao.BuscarAccesos(texto, filtroResultado, desde, hasta); }
            catch (Exception ex) { throw new Exception("Error al buscar accesos.\n" + ex.Message); }
        }

        // ──────────────────────────────────────────────────────
        // ESTADÍSTICAS
        // ──────────────────────────────────────────────────────
        public EstadisticasAcceso ObtenerEstadisticas()
        {
            try { return _dao.ObtenerEstadisticas(); }
            catch { return new EstadisticasAcceso(); }
        }

        public List<MembresiaOpcion> ObtenerMembresiasActivasPorDni(string dni)
        {
            return _dao.ObtenerMembresiasActivasPorDni(dni);
        }

        public (string tipo, long id, string nombre, string apellido, byte[] foto) BuscarPersonaPorDni(string dni)
        {
            string err = Validador.ValidarDni(dni);
            if (err != null)
                return ("no_encontrado", 0, null, null, null);
            try
            {
                return _dao.BuscarPersonaPorDni(dni.Trim());
            }
            catch
            {
                return ("no_encontrado", 0, null, null, null);
            }
        }
    }
}