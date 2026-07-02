namespace ComicLayoutEditor.Core.Models;

/// <summary>
/// Página del documento. Por defecto A4 en milímetros.
/// </summary>
public sealed class Page
{
    /// <summary>Ancho A4 en milímetros.</summary>
    public const double A4WidthMm = 210;

    /// <summary>Alto A4 en milímetros.</summary>
    public const double A4HeightMm = 297;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tamaño de la página en milímetros (por defecto A4 vertical).</summary>
    public SizeD SizeMm { get; set; } = new(A4WidthMm, A4HeightMm);

    public List<Panel> Panels { get; set; } = new();

    /// <summary>Crea una página A4 vacía. <paramref name="landscape"/> la pone horizontal.</summary>
    public static Page CreateA4(bool landscape = false) => new()
    {
        SizeMm = landscape ? new SizeD(A4HeightMm, A4WidthMm) : new SizeD(A4WidthMm, A4HeightMm)
    };

    /// <summary>Copia profunda de la página (viñetas incluidas) con un <see cref="Id"/> nuevo.</summary>
    public Page Clone() => new()
    {
        SizeMm = SizeMm,
        Panels = Panels.Select(p => p.Clone()).ToList()
    };
}
