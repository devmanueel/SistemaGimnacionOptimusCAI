using SistemaGimnacionOptimusCAI.Helpers;
using System.Windows;

namespace SistemaGimnacionOptimusCAI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Detectar el lector de huellas en segundo plano para no demorar el inicio
            BiometricManager.InicializarAsync();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            BiometricManager.Liberar();
            base.OnExit(e);
        }
    }
}
