using System;
using System.Globalization;
using System.Windows.Data;

namespace ComicLayoutEditor.App.Converters;

/// <summary>Convierte un índice base 0 en su número base 1 (para numerar páginas).</summary>
public sealed class AddOneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i ? i + 1 : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
