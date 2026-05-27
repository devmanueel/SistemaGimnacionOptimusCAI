// ============================================================
//  Helpers/BiometricService.cs
//
//  Servicio de alto nivel para el lector de huellas digitales.
//  Usa la Windows Biometric Framework API (winbio.dll) con un
//  pool privado y base de datos propia para identificar socios
//  sin depender de cuentas de Windows.
//
//  Compatible con C# 7.3 / .NET 4.7.2.
// ============================================================

using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    public sealed class BiometricService : IDisposable
    {
        // GUID fijo de la base de datos privada del sistema gimnasio.
        // No cambiar: es el identificador que usa WBF para nuestra DB.
        private static readonly Guid DB_GUID = new Guid("A9F3B2C1-D4E5-4F78-9ABC-1234567890AB");

        private uint[] _unitIds = new uint[0];
        private uint   _session;     // handle activo, 0 = cerrado
        private bool   _disponible;

        public bool   Disponible     => _disponible;
        public string MensajeEstado  { get; private set; } = "No inicializado";

        // ── Inicialización ──────────────────────────────────────

        /// <summary>
        /// Detecta el lector, crea la base de datos privada si no existe,
        /// y abre la sesión de identificación.  Llamar en hilo secundario.
        /// </summary>
        public bool Inicializar()
        {
            try
            {
                // Asegurar que el servicio Windows Biometric esté corriendo
                if (!AsegurarServicioWbio())
                    return false;

                // 1. Detectar unidades biométricas
                _unitIds = EnumerarUnidades();
                if (_unitIds.Length == 0)
                {
                    MensajeEstado = "No se detectó ningún lector de huellas conectado.";
                    return false;
                }

                // 2. Verificar / crear la base de datos privada
                if (!AsegurarBaseDeDatos())
                    return false;   // MensajeEstado ya seteado

                // 3. Abrir sesión avanzada (permite identificar y enrolar)
                if (!AbrirSesion())
                    return false;

                _disponible  = true;
                MensajeEstado = "Lector de huellas listo (" + _unitIds.Length + " unidad(es)).";
                return true;
            }
            catch (Exception ex)
            {
                MensajeEstado = "Error al inicializar el lector: " + ex.Message;
                return false;
            }
        }

        // ── Servicio Windows Biometric ──────────────────────────

        private bool AsegurarServicioWbio()
        {
            try
            {
                using (var sc = new ServiceController("WbioSrvc"))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                        return true;

                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    }

                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch (InvalidOperationException)
            {
                MensajeEstado = "El servicio Windows Biometric Framework no está instalado.";
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MensajeEstado = "No se pudo iniciar el servicio biométrico.\n" +
                                "Ejecutá el programa como Administrador o iniciá el servicio manualmente.";
                return false;
            }
            catch (System.TimeoutException)
            {
                MensajeEstado = "El servicio biométrico tardó demasiado en iniciar.";
                return false;
            }
        }

        // ── Enumeración de unidades ─────────────────────────────

        private uint[] EnumerarUnidades()
        {
            IntPtr ptr;
            IntPtr countPtr;
            int hr = WinBioNative.WinBioEnumBiometricUnits(
                WinBioNative.WINBIO_TYPE_FINGERPRINT, out ptr, out countPtr);

            if (hr != WinBioNative.S_OK || ptr == IntPtr.Zero) return new uint[0];

            int count = countPtr.ToInt32();
            if (count <= 0)
            {
                WinBioNative.WinBioFree(ptr);
                return new uint[0];
            }

            int sz = Marshal.SizeOf(typeof(WinBioNative.WINBIO_UNIT_SCHEMA));
            var ids = new uint[count];
            for (int i = 0; i < count; i++)
            {
                var schema = (WinBioNative.WINBIO_UNIT_SCHEMA)
                    Marshal.PtrToStructure(IntPtr.Add(ptr, i * sz),
                                           typeof(WinBioNative.WINBIO_UNIT_SCHEMA));
                ids[i] = schema.UnitId;
            }
            WinBioNative.WinBioFree(ptr);
            return ids;
        }

        // ── Gestión de la base de datos privada ─────────────────

        private bool AsegurarBaseDeDatos()
        {
            // Verificar si ya existe nuestra DB privada
            if (BaseDeDatosExiste()) return true;

            // Necesita ejecutarse como administrador para crear la DB
            return CrearBaseDeDatos();
        }

        private bool BaseDeDatosExiste()
        {
            IntPtr ptr;
            IntPtr countPtr;
            int hr = WinBioNative.WinBioEnumDatabases(
                WinBioNative.WINBIO_TYPE_FINGERPRINT, out ptr, out countPtr);
            if (hr != WinBioNative.S_OK || ptr == IntPtr.Zero) return false;

            int count = countPtr.ToInt32();
            int sz = Marshal.SizeOf(typeof(WinBioNative.WINBIO_STORAGE_SCHEMA));
            bool encontrada = false;

            for (int i = 0; i < count && !encontrada; i++)
            {
                var schema = (WinBioNative.WINBIO_STORAGE_SCHEMA)
                    Marshal.PtrToStructure(IntPtr.Add(ptr, i * sz),
                                           typeof(WinBioNative.WINBIO_STORAGE_SCHEMA));
                if (schema.DatabaseId == DB_GUID) encontrada = true;
            }
            WinBioNative.WinBioFree(ptr);
            return encontrada;
        }

        private bool CrearBaseDeDatos()
        {
            // Obtener el DataFormat del sistema (de una DB existente del sensor)
            Guid dataFormat = ObtenerFormatoDato();

            // Obtener el schema de la primera unidad para asociarla
            IntPtr ptr;
            IntPtr countPtr;
            int hr = WinBioNative.WinBioEnumBiometricUnits(
                WinBioNative.WINBIO_TYPE_FINGERPRINT, out ptr, out countPtr);
            if (hr != WinBioNative.S_OK || ptr == IntPtr.Zero)
            {
                MensajeEstado = "No se pudo obtener información del lector.";
                return false;
            }

            var unitSchema = (WinBioNative.WINBIO_UNIT_SCHEMA)
                Marshal.PtrToStructure(ptr, typeof(WinBioNative.WINBIO_UNIT_SCHEMA));
            WinBioNative.WinBioFree(ptr);

            Guid dbGuid = DB_GUID;
            hr = WinBioNative.WinBioCreateDatabase(
                ref unitSchema, ref dbGuid, ref dataFormat, null, null);

            if (hr == WinBioNative.S_OK || hr == WinBioNative.WINBIO_E_DATABASE_ALREADY_EXISTS)
                return true;

            MensajeEstado = string.Format(
                "No se pudo crear la base de datos de huellas (0x{0:X8}).\n" +
                "Ejecutá el programa como Administrador la primera vez.", hr);
            return false;
        }

        private Guid ObtenerFormatoDato()
        {
            // Reutiliza el DataFormat de una DB existente del sensor
            IntPtr ptr;
            IntPtr countPtr;
            int hr = WinBioNative.WinBioEnumDatabases(
                WinBioNative.WINBIO_TYPE_FINGERPRINT, out ptr, out countPtr);
            if (hr != WinBioNative.S_OK || ptr == IntPtr.Zero || countPtr.ToInt32() == 0)
                return Guid.Empty;

            var schema = (WinBioNative.WINBIO_STORAGE_SCHEMA)
                Marshal.PtrToStructure(ptr, typeof(WinBioNative.WINBIO_STORAGE_SCHEMA));
            WinBioNative.WinBioFree(ptr);
            return schema.DataFormat;
        }

        // ── Sesión ──────────────────────────────────────────────

        private bool AbrirSesion()
        {
            Guid dbGuid = DB_GUID;
            var dbHandle = GCHandle.Alloc(dbGuid, GCHandleType.Pinned);
            try
            {
                int hr = WinBioNative.WinBioOpenSession(
                    WinBioNative.WINBIO_TYPE_FINGERPRINT,
                    WinBioNative.WINBIO_POOL_PRIVATE,
                    WinBioNative.WINBIO_FLAG_ADVANCED,
                    _unitIds,
                    new IntPtr(_unitIds.Length),
                    dbHandle.AddrOfPinnedObject(),
                    out _session);

                if (hr != WinBioNative.S_OK)
                {
                    MensajeEstado = string.Format(
                        "No se pudo abrir la sesión biométrica (0x{0:X8}).", hr);
                    return false;
                }
                return true;
            }
            finally
            {
                dbHandle.Free();
            }
        }

        private void CerrarSesion()
        {
            if (_session != 0)
            {
                WinBioNative.WinBioCancel(_session);
                WinBioNative.WinBioCloseSession(_session);
                _session = 0;
            }
        }

        // Interrumpe cualquier operación bloqueante (Identify, EnrollCapture, etc.)
        public void Cancelar()
        {
            if (_session != 0) WinBioNative.WinBioCancel(_session);
        }

        // ── Identificación continua ─────────────────────────────

        /// <summary>
        /// Bucle bloqueante que espera huellas y llama a <paramref name="callback"/>
        /// con el GUID del socio identificado (null si no se reconoce).
        /// Diseñado para correr en Task.Run; se detiene con el CancellationToken.
        /// </summary>
        public void IniciarIdentificacion(CancellationToken token, Action<Guid?> callback)
        {
            if (!_disponible || _session == 0) return;

            // Al cancelar: interrumpir la llamada bloqueante a WinBioIdentify
            using (token.Register(() => WinBioNative.WinBioCancel(_session)))
            {
                while (!token.IsCancellationRequested)
                {
                    uint unitId;
                    WinBioNative.WINBIO_IDENTITY identity;
                    byte subFactor;
                    uint rejectDetail;

                    int hr = WinBioNative.WinBioIdentify(
                        _session, out unitId, out identity, out subFactor, out rejectDetail);

                    if (token.IsCancellationRequested) break;

                    if (hr == WinBioNative.S_OK && identity.Type == WinBioNative.WINBIO_ID_TYPE_GUID)
                    {
                        callback(identity.Guid);
                        Thread.Sleep(1500); // pausa para no procesar el mismo dedo dos veces
                    }
                    else if (hr == WinBioNative.WINBIO_E_CANCELED)
                    {
                        break;
                    }
                    else if (hr == WinBioNative.WINBIO_E_UNKNOWN_ID ||
                             hr == WinBioNative.WINBIO_E_NO_MATCH)
                    {
                        callback(null); // huella no registrada en el sistema
                        Thread.Sleep(800);
                    }
                    // WINBIO_E_BAD_CAPTURE y otros: simplemente reintentar
                }
            }
        }

        // ── Enrolamiento ────────────────────────────────────────

        /// <summary>
        /// Enrola la huella de un socio con su GUID.  Requiere 4 capturas correctas.
        /// <paramref name="onProgreso"/> recibe (capturaActual, mensaje).
        /// Retorna (true, "") si exitoso, (false, motivo) si falla.
        /// </summary>
        public (bool exito, string mensaje) Enrollar(
            Guid huellaGuid,
            Action<int, string> onProgreso,
            CancellationToken token)
        {
            if (!_disponible || _session == 0)
                return (false, "El lector no está disponible.");

            const int CAPTURAS_REQUERIDAS = 4;

            // Iniciar enrolamiento para el primer lector disponible
            int hrBegin = WinBioNative.WinBioEnrollBegin(
                _session, WinBioNative.WINBIO_SUBTYPE_ANY, _unitIds[0]);

            if (!WinBioNative.Exitoso(hrBegin))
                return (false, string.Format("No se pudo iniciar el enrolamiento (0x{0:X8}).", hrBegin));

            int capturas = 0;
            try
            {
                while (capturas < CAPTURAS_REQUERIDAS && !token.IsCancellationRequested)
                {
                    onProgreso(capturas, string.Format(
                        "Apoyá tu dedo en el lector... ({0}/{1})", capturas + 1, CAPTURAS_REQUERIDAS));

                    uint rejectDetail;
                    int hr = WinBioNative.WinBioEnrollCaptureSample(_session, out rejectDetail);

                    if (token.IsCancellationRequested)
                    {
                        WinBioNative.WinBioEnrollDiscard(_session);
                        return (false, "Enrolamiento cancelado.");
                    }

                    if (hr == WinBioNative.WINBIO_E_CANCELED)
                    {
                        WinBioNative.WinBioEnrollDiscard(_session);
                        return (false, "Enrolamiento cancelado.");
                    }
                    else if (hr == WinBioNative.WINBIO_E_BAD_CAPTURE)
                    {
                        onProgreso(capturas, WinBioNative.DescribirRechazo(rejectDetail));
                        Thread.Sleep(600);
                    }
                    else if (WinBioNative.Exitoso(hr))
                    {
                        capturas++;
                        if (capturas < CAPTURAS_REQUERIDAS)
                            onProgreso(capturas, "Levantá el dedo y volvé a apoyar");
                        else
                            onProgreso(capturas, "Procesando template...");

                        Thread.Sleep(400);
                    }
                    else
                    {
                        WinBioNative.WinBioEnrollDiscard(_session);
                        return (false, string.Format("Error en la captura (0x{0:X8}).", hr));
                    }
                }

                if (token.IsCancellationRequested)
                {
                    WinBioNative.WinBioEnrollDiscard(_session);
                    return (false, "Enrolamiento cancelado.");
                }

                // Intentar completar el enrolamiento
                var identity = new WinBioNative.WINBIO_IDENTITY
                {
                    Type = WinBioNative.WINBIO_ID_TYPE_GUID,
                    Guid = huellaGuid
                };
                bool isNew;
                int hrCommit = WinBioNative.WinBioEnrollCommit(_session, ref identity, out isNew);

                if (hrCommit == WinBioNative.WINBIO_E_ENROLLMENT_INCOMPLETE)
                {
                    // Necesita más capturas — pedir una más
                    WinBioNative.WinBioEnrollDiscard(_session);
                    return (false, "Calidad insuficiente. Intentá el enrolamiento de nuevo.");
                }

                if (!WinBioNative.Exitoso(hrCommit))
                {
                    WinBioNative.WinBioEnrollDiscard(_session);
                    return (false, string.Format("No se pudo guardar la huella (0x{0:X8}).", hrCommit));
                }

                return (true, isNew ? "Huella registrada correctamente." : "Huella actualizada correctamente.");
            }
            catch (Exception ex)
            {
                try { WinBioNative.WinBioEnrollDiscard(_session); } catch { }
                return (false, "Error inesperado: " + ex.Message);
            }
        }

        // ── Eliminación de huella ───────────────────────────────

        /// <summary>Elimina el template del socio de la base de datos WBF.</summary>
        public (bool exito, string mensaje) EliminarHuella(Guid huellaGuid)
        {
            if (!_disponible || _session == 0)
                return (false, "El lector no está disponible.");

            var identity = new WinBioNative.WINBIO_IDENTITY
            {
                Type = WinBioNative.WINBIO_ID_TYPE_GUID,
                Guid = huellaGuid
            };

            int hr = WinBioNative.WinBioDeleteTemplate(
                _session, _unitIds[0], ref identity, WinBioNative.WINBIO_SUBTYPE_ANY);

            if (WinBioNative.Exitoso(hr))
                return (true, "Huella eliminada del lector.");

            if (hr == WinBioNative.WINBIO_E_UNKNOWN_ID)
                return (true, "La huella ya no estaba registrada en el lector.");

            return (false, string.Format("Error al eliminar la huella (0x{0:X8}).", hr));
        }

        // ── IDisposable ─────────────────────────────────────────

        public void Dispose() => CerrarSesion();
    }

    // ── Singleton estático (igual a SesionManager) ──────────────

    public static class BiometricManager
    {
        private static BiometricService _servicio;

        public static BiometricService Servicio => _servicio;

        /// <summary>
        /// Inicializa el servicio en hilo secundario y retorna inmediatamente.
        /// El resultado se puede leer desde Servicio.Disponible / Servicio.MensajeEstado.
        /// </summary>
        public static void InicializarAsync()
        {
            _servicio = new BiometricService();
            System.Threading.Tasks.Task.Run(() => _servicio.Inicializar());
        }

        public static void Liberar()
        {
            _servicio?.Dispose();
            _servicio = null;
        }
    }
}
