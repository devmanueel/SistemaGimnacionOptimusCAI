using System;
using System.Globalization;
using System.Windows.Data;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    public class InicialesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string nombreCompleto && !string.IsNullOrWhiteSpace(nombreCompleto))
            {
                string[] partes = nombreCompleto.Split(',');
                string iniciales = "";
                foreach (var parte in partes)
                {
                    string trimmed = parte.Trim();
                    if (trimmed.Length > 0)
                        iniciales += trimmed[0];
                }
                return iniciales.ToUpper();
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
