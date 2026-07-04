// ============================================================
//  Helpers/BiometricService.cs
//
//  Servicio de alto nivel para el lector de huellas digitales.
//  Usa el SDK DigitalPersona One Touch for Windows (.NET):
//  DPFPDevNET / DPFPEngNET / DPFPShrNET / DPFPVerNET.
//
//  El lector se instala con el driver DigitalPersona (servicio
//  DpHost), NO con Windows Biometric Framework. Cada socio tiene
//  un GUID lógico (huella_guid) y su template serializado se
//  guarda en SQL (huella_template). La identificación es 1:N:
//  se compara la muestra contra cada template con DPFP.Verification.
//
//  IMPORTANTE: las DLLs son x86 mixed-mode → la app debe
//  compilarse con PlatformTarget x86.
//
//  Compatible con C# 7.3 / .NET 4.7.2.
// ============================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    public sealed class BiometricService : IDisposable
    {
        // Número de capturas que pide el enrolamiento DigitalPersona (por defecto 4).
        private const int CAPTURAS_OBJETIVO = 4;

        // FAR objetivo de la verificación: 1 en 100.000 (valor recomendado por DigitalPersona).
        private const int FAR_REQUERIDO = int.MaxValue / 100000;

        private bool _disponible;
        private LectorCaptura _activo;   // captura en curso (para poder cancelarla)
        private readonly object _capturaLock = new object();

        public bool   Disponible    => _disponible;
        public string MensajeEstado { get; private set; } = "No inicializado";

        // ── Inicialización ──────────────────────────────────────

        /// <summary>
        /// Detecta el lector vía el SDK DigitalPersona. Llamar en hilo secundario.
        /// </summary>
        public bool Inicializar()
        {
            try
            {
                var lectores = new DPFP.Capture.ReadersCollection();
                if (lectores.Count == 0)
                {
                    _disponible = false;
                    MensajeEstado = "No se detectó ningún lector de huellas.\n" +
                                    "Verificá que esté conectado y que el servicio " +
                                    "DigitalPersona (DpHost) esté en ejecución.";
                    return false;
                }

                string producto = null;
                foreach (var kv in lectores)
                {
                    producto = kv.Value.ProductName;
                    break;
                }

                _disponible = true;
                MensajeEstado = "Lector de huellas listo" +
                                (producto != null ? " (" + producto + ")." : ".");
                return true;
            }
            catch (Exception ex)
            {
                _disponible = false;
                MensajeEstado = "No se pudo inicializar el lector DigitalPersona.\n" +
                                "¿Está corriendo el servicio DpHost?\n" + ex.Message;
                return false;
            }
        }

        // ── Enrolamiento ────────────────────────────────────────

        /// <summary>
        /// Captura huellas hasta formar un template y lo devuelve serializado.
        /// <paramref name="onProgreso"/> recibe (capturasHechas, mensaje).
        /// Retorna (true, mensaje, template) si exitoso, (false, motivo, null) si falla.
        /// </summary>
        public (bool exito, string mensaje, byte[] template) Enrollar(
            Action<int, string> onProgreso,
            CancellationToken token)
        {
            if (!_disponible)
                return (false, "El lector no está disponible.", null);

            lock (_capturaLock)
            {
                if (token.IsCancellationRequested)
                    return (false, "Enrolamiento cancelado.", null);

                var extractor  = new DPFP.Processing.FeatureExtraction();
                var enrollment = new DPFP.Processing.Enrollment();

                using (var lector = new LectorCaptura())
                {
                    _activo = lector;
                    try
                    {
                        lector.Iniciar();

                        while (!token.IsCancellationRequested)
                        {
                            var estado = enrollment.TemplateStatus;
                            if (estado == DPFP.Processing.Enrollment.Status.Ready ||
                                estado == DPFP.Processing.Enrollment.Status.Failed)
                                break;

                            int hechas = Math.Max(0,
                                CAPTURAS_OBJETIVO - (int)enrollment.FeaturesNeeded);

                            onProgreso(hechas, string.Format(
                                "Apoyá tu dedo en el lector... ({0}/{1})",
                                Math.Min(hechas + 1, CAPTURAS_OBJETIVO), CAPTURAS_OBJETIVO));

                            var muestra = lector.EsperarMuestra(token);
                            if (muestra == null) break; // cancelado

                            var features = ExtraerFeatures(
                                extractor, muestra, DPFP.Processing.DataPurpose.Enrollment);

                            if (features == null)
                            {
                                onProgreso(hechas, "Captura de baja calidad. Levantá el dedo y reintentá.");
                                continue;
                            }

                            try { enrollment.AddFeatures(features); }
                            catch
                            {
                                onProgreso(hechas, "Captura no válida. Reintentá apoyando bien el dedo.");
                                continue;
                            }

                            if (enrollment.TemplateStatus ==
                                DPFP.Processing.Enrollment.Status.Failed)
                            {
                                enrollment.Clear();
                                onProgreso(0, "No se logró formar la huella. Empezá de nuevo.");
                                continue;
                            }

                            int hechasAhora = Math.Max(0,
                                CAPTURAS_OBJETIVO - (int)enrollment.FeaturesNeeded);
                            if (enrollment.TemplateStatus !=
                                DPFP.Processing.Enrollment.Status.Ready)
                                onProgreso(hechasAhora, "Levantá el dedo y volvé a apoyar");
                        }

                        if (token.IsCancellationRequested)
                            return (false, "Enrolamiento cancelado.", null);

                        if (enrollment.TemplateStatus == DPFP.Processing.Enrollment.Status.Ready)
                        {
                            onProgreso(CAPTURAS_OBJETIVO, "Procesando template...");
                            byte[] bytes = enrollment.Template.Bytes;
                            if (bytes == null || bytes.Length == 0)
                                return (false, "No se pudo serializar la huella. Intentá de nuevo.", null);

                            return (true, "Huella registrada correctamente.", bytes);
                        }

                        return (false, "No se pudo completar el enrolamiento. Intentá de nuevo.", null);
                    }
                    catch (Exception ex)
                    {
                        return (false, "Error inesperado durante el enrolamiento: " + ex.Message, null);
                    }
                    finally
                    {
                        lector.Detener();
                        _activo = null;
                    }
                }
            }
        }

        // ── Identificación continua ─────────────────────────────

        /// <summary>
        /// Bucle bloqueante: espera huellas, las compara 1:N contra
        /// <paramref name="plantillas"/> y llama a <paramref name="callback"/>
        /// con el GUID del socio identificado (null si no se reconoce).
        /// Diseñado para correr en Task.Run; se detiene con el CancellationToken.
        /// </summary>
        public void IniciarIdentificacion(
            IReadOnlyList<(Guid guid, byte[] template)> plantillas,
            CancellationToken token,
            Action<Guid?> callback)
        {
            if (!_disponible) return;

            var extractor   = new DPFP.Processing.FeatureExtraction();
            var verificador = new DPFP.Verification.Verification(FAR_REQUERIDO);

            // Reconstruir los templates DPFP una sola vez
            var lista = new List<(Guid guid, DPFP.Template tpl)>();
            if (plantillas != null)
            {
                foreach (var p in plantillas)
                {
                    if (p.template == null || p.template.Length == 0) continue;
                    try
                    {
                        var t = new DPFP.Template();
                        t.DeSerialize(p.template);
                        lista.Add((p.guid, t));
                    }
                    catch { /* template corrupto: ignorar */ }
                }
            }

            lock (_capturaLock)
            {
                if (token.IsCancellationRequested) return;

                using (var lector = new LectorCaptura())
                {
                    _activo = lector;
                    try
                    {
                        lector.Iniciar();

                        while (!token.IsCancellationRequested)
                        {
                            var muestra = lector.EsperarMuestra(token);
                            if (muestra == null) break; // cancelado

                            var features = ExtraerFeatures(
                                extractor, muestra, DPFP.Processing.DataPurpose.Verification);
                            if (features == null) continue; // mala captura: reintentar

                            Guid? match = null;
                            foreach (var item in lista)
                            {
                                var result = new DPFP.Verification.Verification.Result();
                                try { verificador.Verify(features, item.tpl, ref result); }
                                catch { continue; }

                                if (result.Verified)
                                {
                                    match = item.guid;
                                    break;
                                }
                            }

                            callback(match); // null = huella no registrada
                            Thread.Sleep(match.HasValue ? 1500 : 800);
                        }
                    }
                    finally
                    {
                        lector.Detener();
                        _activo = null;
                    }
                }
            }
        }

        // ── Eliminación de huella ───────────────────────────────

        /// <summary>
        /// Con DigitalPersona el template vive en SQL, no en el lector,
        /// así que el borrado real lo hace el SP. Se mantiene por
        /// compatibilidad con el código que la llamaba.
        /// </summary>
        public (bool exito, string mensaje) EliminarHuella(Guid huellaGuid)
        {
            return (true, "Huella eliminada.");
        }

        // ── Cancelación ─────────────────────────────────────────

        /// <summary>Interrumpe la captura en curso (enrolamiento o identificación).</summary>
        public void Cancelar()
        {
            try { _activo?.Detener(); } catch { }
        }

        // ── Helpers ─────────────────────────────────────────────

        private static DPFP.FeatureSet ExtraerFeatures(
            DPFP.Processing.FeatureExtraction extractor,
            DPFP.Sample muestra,
            DPFP.Processing.DataPurpose proposito)
        {
            var feedback = DPFP.Capture.CaptureFeedback.None;
            var features = new DPFP.FeatureSet();
            try
            {
                extractor.CreateFeatureSet(muestra, proposito, ref feedback, ref features);
            }
            catch
            {
                return null;
            }
            return feedback == DPFP.Capture.CaptureFeedback.Good ? features : null;
        }

        // ── IDisposable ─────────────────────────────────────────

        public void Dispose()
        {
            try { _activo?.Detener(); } catch { }
            _activo = null;
        }

        // ════════════════════════════════════════════════════════
        //  Captura del lector — adapta el modelo por eventos del SDK
        //  a una espera bloqueante con cancelación.
        // ════════════════════════════════════════════════════════
        private sealed class LectorCaptura : DPFP.Capture.EventHandler, IDisposable
        {
            private readonly DPFP.Capture.Capture _capture;
            private readonly BlockingCollection<DPFP.Sample> _muestras =
                new BlockingCollection<DPFP.Sample>(new ConcurrentQueue<DPFP.Sample>());
            private volatile bool _activo;

            public LectorCaptura()
            {
                _capture = new DPFP.Capture.Capture();
                _capture.EventHandler = this;
            }

            public void Iniciar()
            {
                _activo = true;
                Exception ultimoError = null;
                for (int intento = 1; intento <= 3; intento++)
                {
                    try
                    {
                        _capture.StartCapture();
                        return;
                    }
                    catch (Exception ex)
                    {
                        ultimoError = ex;
                        Thread.Sleep(350);
                    }
                }

                _activo = false;
                throw ultimoError ?? new InvalidOperationException("No se pudo iniciar la captura.");
            }

            public void Detener()
            {
                _activo = false;
                try { _capture.StopCapture(); } catch { }
            }

            /// <summary>Bloquea hasta la próxima muestra. Devuelve null si se cancela.</summary>
            public DPFP.Sample EsperarMuestra(CancellationToken token)
            {
                try { return _muestras.Take(token); }
                catch (OperationCanceledException) { return null; }
                catch (ObjectDisposedException)    { return null; }
                catch (InvalidOperationException)  { return null; }
            }

            // ── Eventos del SDK ──────────────────────────────────
            public void OnComplete(object capture, string serie, DPFP.Sample muestra)
            {
                if (_activo) { try { _muestras.Add(muestra); } catch { } }
            }

            public void OnFingerTouch(object capture, string serie) { }
            public void OnFingerGone(object capture, string serie) { }
            public void OnReaderConnect(object capture, string serie) { }
            public void OnReaderDisconnect(object capture, string serie) { }
            public void OnSampleQuality(object capture, string serie,
                                        DPFP.Capture.CaptureFeedback feedback) { }

            public void Dispose()
            {
                Detener();
                _muestras.Dispose();
            }
        }
    }

    // ── Singleton estático (igual a SesionManager) ──────────────

    public static class BiometricManager
    {
        private static BiometricService _servicio;

        public static BiometricService Servicio => _servicio;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        /// <summary>
        /// Inicializa el servicio en hilo secundario y retorna inmediatamente.
        /// El resultado se lee desde Servicio.Disponible / Servicio.MensajeEstado.
        /// </summary>
        public static void InicializarAsync()
        {
            if (!EstaDisponibleSdkDigitalPersona())
            {
                _servicio = null;
                return;
            }

            _servicio = new BiometricService();
            System.Threading.Tasks.Task.Run(() =>
            {
                try { _servicio.Inicializar(); }
                catch { _servicio = null; }
            });
        }

        public static void Liberar()
        {
            _servicio?.Dispose();
            _servicio = null;
        }

        private static bool EstaDisponibleSdkDigitalPersona()
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = LoadLibrary("DPFPApi.dll");
                return handle != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (handle != IntPtr.Zero)
                    FreeLibrary(handle);
            }
        }
    }
}
