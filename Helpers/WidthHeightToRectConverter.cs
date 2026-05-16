using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    public class WidthHeightToRectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is double w && values[1] is double h)
                return new Rect(0, 0, w, h);
            return Rect.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
