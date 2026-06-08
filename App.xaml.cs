using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SistemaGimnacionOptimusCAI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ConfigurarLogErrores();

            AppDomain.CurrentDomain.SetData(
                "DataDirectory",
                AppDomain.CurrentDomain.BaseDirectory
            );

            RegistrarLog("Inicio de aplicacion. BaseDirectory: " +
                         AppDomain.CurrentDomain.BaseDirectory);

            base.OnStartup(e);

            // Detectar el lector de huellas en segundo plano para no demorar el inicio.
            BiometricManager.InicializarAsync();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            BiometricManager.Liberar();
            base.OnExit(e);
        }

        private static void ConfigurarLogErrores()
        {
            Dispatcher.CurrentDispatcher.UnhandledException += Dispatcher_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private static void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            RegistrarLog("DispatcherUnhandledException", e.Exception);
            MessageBox.Show(
                "La aplicacion no pudo iniciar correctamente.\n\n" +
                "Se genero el archivo error_inicio.txt en la carpeta del ejecutable.",
                "Error de inicio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            RegistrarLog("UnhandledException", e.ExceptionObject as Exception);
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            RegistrarLog("UnobservedTaskException", e.Exception);
            e.SetObserved();
        }

        private static void RegistrarLog(string mensaje)
        {
            RegistrarLog(mensaje, null);
        }

        private static void RegistrarLog(string mensaje, Exception ex)
        {
            try
            {
                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "error_inicio.txt");

                File.AppendAllText(path,
                    "==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====\r\n" +
                    mensaje + "\r\n" +
                    (ex != null ? ex.ToString() + "\r\n" : string.Empty) +
                    "\r\n");
            }
            catch { }
        }
    }
}
