namespace ComicLayoutEditor.Core.Models;

/// <summary>
/// Viñeta dentro de una página. Puede contener una imagen y varios bocadillos.
/// </summary>
public sealed class Panel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Posición/tamaño de la viñeta como fracciones normalizadas [0,1]
    /// relativas a la página que la contiene.
    /// </summary>
    public RectD Bounds { get; set; }

    /// <summary>Rotación en grados (en sentido horario).</summary>
    public double Rotation { get; set; }

    /// <summary>Orden de apilado; mayor valor se dibuja por encima.</summary>
    public int ZIndex { get; set; }

    /// <summary>Imagen de la viñeta, o <c>null</c> si no tiene.</summary>
    public ImageRef? Image { get; set; }

    public ImageFit ImageFit { get; set; } = ImageFit.Cover;

    /// <summary>
    /// Rotación de la imagen dentro del marco, en grados horarios y en pasos de 90
    /// (0, 90, 180, 270). Útil para enderezar fotos tomadas con el móvil.
    /// </summary>
    public int ImageRotation { get; set; }

    /// <summary>Zoom adicional de la imagen dentro del marco (1 = ajuste base).</summary>
    public double ImageZoom { get; set; } = 1.0;

    /// <summary>
    /// Desplazamiento (pan) de la imagen dentro del marco, como fracción
    /// normalizada del tamaño de la viñeta. (0,0) = sin desplazamiento.
    /// </summary>
    public PointD ImageOffset { get; set; }

    public List<Balloon> Balloons { get; set; } = new();

    /// <summary>Copia profunda de la viñeta (imagen y bocadillos) con un <see cref="Id"/> nuevo.</summary>
    public Panel Clone() => new()
    {
        Bounds = Bounds,
        Rotation = Rotation,
        ZIndex = ZIndex,
        Image = Image?.Clone(),
        ImageFit = ImageFit,
        ImageRotation = ImageRotation,
        ImageZoom = ImageZoom,
        ImageOffset = ImageOffset,
        Balloons = Balloons.Select(b => b.Clone()).ToList()
    };
}
