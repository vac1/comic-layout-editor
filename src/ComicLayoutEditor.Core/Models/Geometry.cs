namespace ComicLayoutEditor.Core.Models;

/// <summary>
/// Punto en coordenadas de doble precisión, independiente de WPF.
/// </summary>
public readonly record struct PointD(double X, double Y);

/// <summary>
/// Tamaño (ancho/alto) en doble precisión, independiente de WPF.
/// </summary>
public readonly record struct SizeD(double Width, double Height);

/// <summary>
/// Rectángulo (posición + tamaño) en doble precisión, independiente de WPF.
/// </summary>
/// <remarks>
/// En el modelo, los <c>Bounds</c> de una <see cref="Panel"/> se expresan como
/// fracciones normalizadas [0,1] relativas a la página, y los de un
/// <see cref="Balloon"/> como fracciones normalizadas [0,1] relativas al panel.
/// Esto mantiene la maqueta independiente de la resolución/escala de dibujo.
/// </remarks>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public static RectD FromLtrb(double left, double top, double right, double bottom)
        => new(left, top, right - left, bottom - top);
}
