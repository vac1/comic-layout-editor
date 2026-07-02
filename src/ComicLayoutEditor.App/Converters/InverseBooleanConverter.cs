using System;
using System.Globalization;
using System.Windows.Data;

namespace ComicLayoutEditor.App.Converters;

/// <summary>Devuelve la negación de un valor booleano.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}
