using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace ComicLayoutEditor.App.Infrastructure;

/// <summary>
/// Lee la orientación EXIF de una imagen para enderezar automáticamente las fotos
/// (típicamente tomadas con el móvil) al importarlas. WPF no aplica la orientación
/// EXIF por su cuenta, así que la traducimos a una rotación en pasos de 90°.
/// </summary>
public static class ExifOrientation
{
    private const string OrientationQuery = "System.Photo.Orientation";

    /// <summary>
    /// Devuelve los grados horarios (0, 90, 180 o 270) que hay que rotar la imagen
    /// de <paramref name="path"/> para mostrarla derecha según su etiqueta EXIF.
    /// Devuelve 0 si no hay metadatos, el formato no los admite o hay algún error.
    /// </summary>
    public static int GetRotationDegrees(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);

            if (frame.Metadata is not BitmapMetadata metadata
                || !metadata.ContainsQuery(OrientationQuery)
                || metadata.GetQuery(OrientationQuery) is not { } value)
            {
                return 0;
            }

            // Los valores EXIF con espejo (2, 4, 5, 7) no se pueden representar solo
            // con rotación; se ignoran (rarísimos en fotos de móvil).
            return Convert.ToInt32(value) switch
            {
                3 => 180,
                6 => 90,
                8 => 270,
                _ => 0
            };
        }
        catch (Exception ex) when (ex is IOException
                                      or NotSupportedException
                                      or ArgumentException
                                      or FormatException
                                      or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
