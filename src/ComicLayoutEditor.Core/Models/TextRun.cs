namespace ComicLayoutEditor.Core.Models;

/// <summary>
/// Fragmento de texto con formato dentro de un bocadillo. Las propiedades nulas
/// heredan del estilo por defecto del bocadillo (fuente/tamaño) o del color base.
/// El texto puede contener saltos de línea (<c>\n</c>).
/// </summary>
public sealed class TextRun
{
    public string Text { get; set; } = string.Empty;

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    public bool Underline { get; set; }

    /// <summary>Familia tipográfica del fragmento; <c>null</c> = heredar la del bocadillo.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Tamaño del fragmento; <c>null</c> = heredar el del bocadillo.</summary>
    public double? FontSize { get; set; }

    /// <summary>Color en formato <c>#RRGGBB</c> o <c>#AARRGGBB</c>; <c>null</c> = heredar (negro).</summary>
    public string? Color { get; set; }

    public TextRun Clone() => new()
    {
        Text = Text,
        Bold = Bold,
        Italic = Italic,
        Underline = Underline,
        FontFamily = FontFamily,
        FontSize = FontSize,
        Color = Color
    };
}
