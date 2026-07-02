using System;
using System.Globalization;
using System.Windows.Data;
using ComicLayoutEditor.App.ViewModels;

namespace ComicLayoutEditor.App.Converters;

/// <summary>
/// Convierte un valor en píxeles de escala base a milímetros, formateado con una
/// cifra decimal (para mostrar posiciones/tamaños en el panel de propiedades).
/// </summary>
public sealed class PixelsToMillimetersConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double px)
        {
            var mm = px / PageViewModel.BasePxPerMm;
            return mm.ToString("0.#", culture);
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
