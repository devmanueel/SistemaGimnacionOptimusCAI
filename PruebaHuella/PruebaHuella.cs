using System;
using System.Threading;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;

// Prueba minima de deteccion + captura del lector DigitalPersona U.are.U
// usando el One Touch SDK (.NET). Compilar SIEMPRE en x86.
class PruebaHuella : DPFP.Capture.EventHandler
{
    private Capture capturador;
    private static ManualResetEvent fin = new ManualResetEvent(false);
    private int muestras = 0;

    static void Main()
    {
        Console.WriteLine("=========================================");
        Console.WriteLine(" PRUEBA LECTOR DIGITALPERSONA U.are.U");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        // 1) Enumerar lectores -> prueba que el SDK ve el hardware
        try
        {
            var lectores = new ReadersCollection();
            Console.WriteLine("Lectores detectados por el SDK: " + lectores.Count);
            if (lectores.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("[X] El SDK NO ve ningun lector.");
                Console.WriteLine("    - Verifica el driver 'DigitalPersona ... Non-WBF'");
                Console.WriteLine("    - Verifica el servicio 'DpHost' (Servicio de autenticacion biometrica)");
                Pausa();
                return;
            }
            int i = 1;
            foreach (System.Collections.Generic.KeyValuePair<Guid, ReaderDescription> kv in lectores)
            {
                ReaderDescription rd = kv.Value;
                Console.WriteLine("  Lector #" + i + ":");
                Console.WriteLine("    Serie    : " + rd.SerialNumber);
                Console.WriteLine("    Producto : " + rd.ProductName);
                i++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[X] Error enumerando lectores: " + ex.Message);
            Pausa();
            return;
        }

        Console.WriteLine();
        Console.WriteLine(">> Apoya un dedo en el lector para probar la captura...");
        Console.WriteLine("   (Ctrl+C para salir)");
        Console.WriteLine();

        var prueba = new PruebaHuella();
        try
        {
            prueba.capturador = new Capture();
            prueba.capturador.EventHandler = prueba;
            prueba.capturador.StartCapture();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[X] No se pudo iniciar la captura: " + ex.Message);
            Pausa();
            return;
        }

        // Esperar hasta una captura exitosa (o cierre manual)
        fin.WaitOne();

        try { prueba.capturador.StopCapture(); } catch { }

        Console.WriteLine();
        Console.WriteLine("=========================================");
        Console.WriteLine(" RESULTADO: el lector FUNCIONA con el SDK");
        Console.WriteLine("=========================================");
        Pausa();
    }

    private static void Pausa()
    {
        Console.WriteLine();
        Console.WriteLine("Presiona ENTER para cerrar...");
        Console.ReadLine();
    }

    // ---- Eventos del SDK ----

    public void OnComplete(object Capture, string serie, Sample muestra)
    {
        muestras++;
        Console.WriteLine("[OK] Huella capturada. Muestra #" + muestras);

        // Probar el pipeline completo: extraer caracteristicas
        try
        {
            var extractor = new FeatureExtraction();
            CaptureFeedback fb = CaptureFeedback.None;
            var features = new FeatureSet();
            extractor.CreateFeatureSet(muestra, DataPurpose.Verification, ref fb, ref features);

            if (fb == CaptureFeedback.Good && features != null)
                Console.WriteLine("[OK] Caracteristicas extraidas correctamente (calidad: BUENA).");
            else
                Console.WriteLine("[!] Muestra capturada pero calidad baja. Reintenta apoyando bien el dedo.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[!] Captura OK pero fallo la extraccion: " + ex.Message);
        }

        fin.Set();
    }

    public void OnFingerTouch(object Capture, string serie)
    {
        Console.WriteLine("... dedo detectado");
    }

    public void OnFingerGone(object Capture, string serie)
    {
        Console.WriteLine("... dedo retirado");
    }

    public void OnReaderConnect(object Capture, string serie)
    {
        Console.WriteLine("... lector conectado: " + serie);
    }

    public void OnReaderDisconnect(object Capture, string serie)
    {
        Console.WriteLine("... lector DESCONECTADO");
    }

    public void OnSampleQuality(object Capture, string serie, CaptureFeedback feedback)
    {
        if (feedback == CaptureFeedback.Good)
            Console.WriteLine("... calidad de muestra: BUENA");
        else
            Console.WriteLine("... calidad de muestra: baja (" + feedback + ")");
    }
}
