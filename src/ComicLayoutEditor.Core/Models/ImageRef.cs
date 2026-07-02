namespace ComicLayoutEditor.Core.Models;

/// <summary>
/// Referencia a un archivo de imagen almacenado dentro del paquete del proyecto,
/// en la carpeta <c>assets/</c>.
/// </summary>
public sealed class ImageRef
{
    /// <summary>
    /// Ruta relativa a la carpeta <c>assets/</c> del proyecto (p. ej. <c>img_0001.png</c>).
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Nombre original del archivo tal como lo importó el usuario (solo informativo).
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Copia superficial (comparte el mismo archivo en <c>assets/</c>).</summary>
    public ImageRef Clone() => new()
    {
        RelativePath = RelativePath,
        OriginalFileName = OriginalFileName
    };
}
