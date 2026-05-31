// ============================================================
//  CAPA: Controllers
//  Archivo: SocioController.cs
//
//  Validaciones + reglas de negocio para Socio.
//  La Vista llama acá, nunca al DAO directamente.
//  Validador ahora está en este mismo namespace (Controllers),
//  por eso NO necesita using extra.
//  Compatible con C# 7.3.
// ============================================================

using Entities;
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class SocioController
    {
        private readonly SocioDao _dao = new SocioDao();

        // ──────────────────────────────────────────────────────
        // OBTENER TODOS / BUSCAR / POR ID
        // ──────────────────────────────────────────────────────
        public List<Socio> ObtenerSocios()
        {
            try { return _dao.ObtenerSocios(); }
            catch (Exception ex) { throw new Exception("No se pudieron cargar los socios.\n" + ex.Message); }
        }

        public List<Socio> BuscarSocios(string texto, string filtroEstado = "todos")
        {
            try { return _dao.BuscarSocios(texto, filtroEstado); }
            catch (Exception ex) { throw new Exception("Error en la búsqueda.\n" + ex.Message); }
        }

        public List<SocioConMembresia> ListarSociosConMembresias(
            string texto              = "",
            string filtroEstado       = "todos",
            long?  filtroActividadId  = null,
            bool?  filtroCuotaVencida = null,
            long?  filtroInstructorId = null,
            string filtroSexo         = null,
            int?   filtroDejaronVenir = null)
        {
            try
            {
                return _dao.ListarSociosConMembresias(
                    texto, filtroEstado, filtroActividadId,
                    filtroCuotaVencida, filtroInstructorId,
                    filtroSexo, filtroDejaronVenir);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al listar socios.\n" + ex.Message);
            }
        }

        public Socio ObtenerPorId(long id)
        {
            try { return _dao.ObtenerSocioPorId(id); }
            catch (Exception ex) { throw new Exception("No se encontró el socio.\n" + ex.Message); }
        }

        public int ObtenerSiguienteNumeroSocio()
        {
            try { return _dao.ObtenerSiguienteNumeroSocio(); }
            catch { return 1; }
        }

        // ──────────────────────────────────────────────────────
        // INSERTAR
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje, Socio socioCreado) Insertar(
            string nombre,
            string apellido,
            string dni,
            DateTime? fechaNacimiento,
            string sexo,
            string telefono,
            string domicilio,
            string profesion,
            string email,
            string comoNosConocio,
            string observaciones,
            byte[] foto,
            long? registradoPor)
        {
            // Validaciones (Validador está en este mismo namespace)
            string err = ValidarCampos(nombre, apellido, dni, fechaNacimiento,
                                       telefono, email);
            if (err != null) return (false, err, null);

            var socio = new Socio
            {
                Nombre = nombre.Trim(),
                Apellido = apellido.Trim(),
                Dni = dni.Trim(),
                DniPin = UsuarioController.HashSHA256(dni.Trim()),
                FechaNacimiento = fechaNacimiento,
                Sexo = sexo ?? "Otro",
                Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim(),
                Domicilio = string.IsNullOrWhiteSpace(domicilio) ? null : domicilio.Trim(),
                Profesion = string.IsNullOrWhiteSpace(profesion) ? null : profesion.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                ComoNosConocio = string.IsNullOrWhiteSpace(comoNosConocio) ? null : comoNosConocio.Trim(),
                Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim(),
                Foto = foto,
                RegistradoPor = registradoPor
            };

            try
            {
                Socio socioCreado = _dao.InsertarSocio(socio);
                if (socioCreado == null) return (false, "No se pudo guardar el socio. Intentá de nuevo.", null);
                if (socioCreado.Id <= 0) return (false, $"El DNI '{dni}' ya está registrado en el sistema.", null);

                // Completar datos que el DAO no retorna
                socioCreado.Nombre = socio.Nombre;
                socioCreado.Apellido = socio.Apellido;
                socioCreado.Dni = socio.Dni;

                Auditor.Registrar("crear", "socio", socioCreado.Id, new Dictionary<string, object> {
                    { "nombre", nombre }, { "apellido", apellido }, { "dni", dni }
                });

                return (true, "Socio registrado correctamente.", socioCreado);
            }
            catch (Exception ex)
            {
                return (false, "Error al insertar.\n" + ex.Message, null);
            }
        }

        // ──────────────────────────────────────────────────────
        // MODIFICAR
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Modificar(
            long id,
            string nombre,
            string apellido,
            string dni,
            DateTime? fechaNacimiento,
            string sexo,
            string telefono,
            string domicilio,
            string profesion,
            string email,
            string comoNosConocio,
            string observaciones,
            byte[] foto,
            bool regenerarPin = false)
        {
            string err = ValidarCampos(nombre, apellido, dni, fechaNacimiento,
                                       telefono, email);
            if (err != null) return (false, err);

            var socio = new Socio
            {
                Id = id,
                Nombre = nombre.Trim(),
                Apellido = apellido.Trim(),
                Dni = dni.Trim(),
                DniPin = regenerarPin ? UsuarioController.HashSHA256(dni.Trim()) : null,
                FechaNacimiento = fechaNacimiento,
                Sexo = sexo ?? "Otro",
                Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim(),
                Domicilio = string.IsNullOrWhiteSpace(domicilio) ? null : domicilio.Trim(),
                Profesion = string.IsNullOrWhiteSpace(profesion) ? null : profesion.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                ComoNosConocio = string.IsNullOrWhiteSpace(comoNosConocio) ? null : comoNosConocio.Trim(),
                Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim(),
                Foto = foto
            };

            try
            {
                bool ok = _dao.ModificarSocio(socio, regenerarPin, foto != null);
                if (ok)
                {
                    Auditor.Registrar("modificar", "socio", id, new Dictionary<string, object> {
                        { "nombre", nombre }, { "apellido", apellido }, { "dni", dni }
                    });
                }
                return ok
                    ? (true, "Socio actualizado correctamente.")
                    : (false, "No se encontró el socio para actualizar.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message.Contains("DNI")
                    ? ex.Message
                    : "Error al actualizar.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // CAMBIAR ESTADO
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) CambiarEstado(long id, bool nuevoEstado)
        {
            try
            {
                bool ok = _dao.CambiarEstadoSocio(id, nuevoEstado);
                string accion = nuevoEstado ? "activado" : "desactivado";
                if (ok)
                {
                    Auditor.Registrar(nuevoEstado ? "activar" : "desactivar", "socio", id);
                }
                return ok
                    ? (true, $"Socio {accion} correctamente.")
                    : (false, "No se encontró el socio.");
            }
            catch (Exception ex)
            {
                return (false, "Error al cambiar estado.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // ELIMINAR
        // ──────────────────────────────────────────────────────
        public (bool ok, string mensaje) Eliminar(long id)
        {
            try
            {
                bool ok = _dao.EliminarSocio(id);
                if (ok)
                {
                    Auditor.Registrar("eliminar", "socio", id);
                }
                return ok
                    ? (true, "Socio eliminado del sistema.")
                    : (false, "No se encontró el socio.");
            }
            catch (Exception ex)
            {
                return (false, "Error al eliminar.\n" + ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────
        // SOCIOS INACTIVOS — dar de baja en lote
        // ──────────────────────────────────────────────────────
        public List<SocioInactivo> ObtenerInactivosParaDarDeBaja(int mesesSinActividad = 2)
        {
            try { return _dao.ObtenerInactivosParaDarDeBaja(mesesSinActividad); }
            catch (Exception ex) { throw new Exception("Error al obtener socios inactivos.\n" + ex.Message); }
        }

        public (bool ok, string mensaje, int afectados) DarDeBajaLote(List<long> ids)
        {
            if (ids == null || ids.Count == 0)
                return (false, "No se seleccionó ningún socio.", 0);

            string listaIds = string.Join(",", ids);
            try
            {
                int afectados = _dao.DarDeBajaLote(listaIds);
                return (true, afectados + " socio(s) dados de baja correctamente.", afectados);
            }
            catch (Exception ex)
            {
                return (false, "Error al dar de baja.\n" + ex.Message, 0);
            }
        }

        // ──────────────────────────────────────────────────────
        // HUELLAS DIGITALES
        // ──────────────────────────────────────────────────────

        public (bool ok, string mensaje) ActualizarHuellaGuid(long socioId, Guid? guid)
        {
            try
            {
                _dao.ActualizarHuellaGuid(socioId, guid);
                string accion = guid.HasValue ? "registrada" : "eliminada";
                return (true, "Huella " + accion + " correctamente.");
            }
            catch (Exception ex)
            {
                return (false, "Error al actualizar la huella.\n" + ex.Message);
            }
        }

        /// <summary>Retorna el DNI del socio con ese GUID de huella, o null si no existe.</summary>
        public string ObtenerDniPorHuellaGuid(Guid guid)
        {
            try
            {
                var resultado = _dao.ObtenerDniPorHuellaGuid(guid);
                return resultado?.dni;
            }
            catch { return null; }
        }

        /// <summary>Guarda el GUID lógico + el template biométrico del socio.</summary>
        public (bool ok, string mensaje) GuardarHuella(long socioId, Guid guid, byte[] template)
        {
            try
            {
                if (template == null || template.Length == 0)
                    return (false, "El template de la huella está vacío.");

                _dao.GuardarHuella(socioId, guid, template);
                return (true, "Huella registrada correctamente.");
            }
            catch (Exception ex)
            {
                return (false, "Error al guardar la huella.\n" + ex.Message);
            }
        }

        /// <summary>Devuelve (guid, template) de todos los socios activos con huella, para identificar 1:N.</summary>
        public List<(Guid guid, byte[] template)> ObtenerHuellas()
        {
            try { return _dao.ObtenerHuellas(); }
            catch { return new List<(Guid guid, byte[] template)>(); }
        }

        // ──────────────────────────────────────────────────────
        // VALIDACIONES CENTRALIZADAS
        // ──────────────────────────────────────────────────────
        private string ValidarCampos(string nombre, string apellido, string dni,
                                     DateTime? fechaNac, string telefono, string email)
        {
            string e = Validador.ValidarNombre(nombre, "El nombre");
            if (e != null) return e;

            e = Validador.ValidarNombre(apellido, "El apellido");
            if (e != null) return e;

            e = Validador.ValidarDni(dni);
            if (e != null) return e;

            if (fechaNac.HasValue)
            {
                if (fechaNac.Value > DateTime.Today)
                    return "La fecha de nacimiento no puede ser futura.";
                if (fechaNac.Value < DateTime.Today.AddYears(-120))
                    return "La fecha de nacimiento no es válida.";
            }

            if (string.IsNullOrWhiteSpace(telefono))
                return "El número de celular es obligatorio.";
            if (telefono.Trim().Length < 8)
                return "El número de celular es inválido.";

            e = Validador.ValidarTelefono(telefono);
            if (e != null) return e;

            e = Validador.ValidarEmail(email);
            if (e != null) return e;

            return null;
        }
    }
}