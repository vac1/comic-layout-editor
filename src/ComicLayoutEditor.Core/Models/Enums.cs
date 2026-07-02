namespace ComicLayoutEditor.Core.Models;

/// <summary>
/// Modo de ajuste de una imagen dentro del marco de su viñeta.
/// </summary>
public enum ImageFit
{
    /// <summary>Cubre todo el marco recortando lo que sobre (mantiene proporción).</summary>
    Cover,

    /// <summary>Encaja completa dentro del marco dejando márgenes (mantiene proporción).</summary>
    Contain,

    /// <summary>Estira para llenar el marco sin mantener proporción.</summary>
    Stretch
}

/// <summary>
/// Forma de un bocadillo de texto.
/// </summary>
public enum BalloonShape
{
    Oval,
    Rounded,
    Rect,

    /// <summary>Cartela/banner de narración: rectángulo sin piquito.</summary>
    Caption,

    Thought,
    Shout
}

/// <summary>
/// Alineación horizontal del texto de un bocadillo.
/// </summary>
public enum TextAlign
{
    Left,
    Center,
    Right,
    Justify
}
