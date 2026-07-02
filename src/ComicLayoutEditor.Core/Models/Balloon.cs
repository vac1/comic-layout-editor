namespace ComicLayoutEditor.Core.Models;

/// <summary>
/// Bocadillo de texto dentro de una viñeta.
/// </summary>
public sealed class Balloon
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Posición/tamaño del bocadillo como fracciones normalizadas [0,1]
    /// relativas al panel que lo contiene.
    /// </summary>
    public RectD Bounds { get; set; }

    public BalloonShape Shape { get; set; } = BalloonShape.Oval;

    /// <summary>
    /// Texto plano del bocadillo. Se mantiene como proyección/plano de respaldo de
    /// <see cref="RichText"/> (concatenación de sus fragmentos) y para compatibilidad
    /// con archivos que no tienen formato enriquecido.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Texto con formato por fragmentos. <c>null</c> = sin formato (usar <see cref="Text"/>).
    /// Al guardar un bocadillo con formato se escriben ambos: <see cref="Text"/> (plano)
    /// y <see cref="RichText"/>.
    /// </summary>
    public List<TextRun>? RichText { get; set; }

    public string FontFamily { get; set; } = "Segoe UI";

    public double FontSize { get; set; } = 12;

    public TextAlign TextAlign { get; set; } = TextAlign.Center;

    /// <summary>
    /// Punto del "piquito" que apunta hacia el personaje, en fracciones
    /// normalizadas [0,1] relativas al panel. <c>null</c> si el bocadillo no tiene piquito.
    /// </summary>
    public PointD? TailPoint { get; set; }

    /// <summary>Copia profunda del bocadillo con un <see cref="Id"/> nuevo.</summary>
    public Balloon Clone() => new()
    {
        Bounds = Bounds,
        Shape = Shape,
        Text = Text,
        RichText = RichText?.Select(r => r.Clone()).ToList(),
        FontFamily = FontFamily,
        FontSize = FontSize,
        TextAlign = TextAlign,
        TailPoint = TailPoint
    };
}
