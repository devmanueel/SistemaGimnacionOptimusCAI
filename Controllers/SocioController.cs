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

            e = Validador.ValidarTelefono(telefono);
            if (e != null) return e;

            e = Validador.ValidarEmail(email);
            if (e != null) return e;

            return null;
        }
    }
}