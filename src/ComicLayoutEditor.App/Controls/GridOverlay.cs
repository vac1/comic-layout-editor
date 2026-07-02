using System.Windows;
using System.Windows.Media;

namespace ComicLayoutEditor.App.Controls;

/// <summary>
/// Dibuja una rejilla sobre la página (en coordenadas de la página, por lo que
/// escala con el zoom). El paso se controla con <see cref="GridSizePx"/>.
/// </summary>
public sealed class GridOverlay : FrameworkElement
{
    private static readonly Pen GridPen = CreatePen();

    private static Pen CreatePen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x60, 0xB0)), 0.5);
        pen.Freeze();
        return pen;
    }

    public static readonly DependencyProperty GridSizePxProperty =
        DependencyProperty.Register(
            nameof(GridSizePx), typeof(double), typeof(GridOverlay),
            new FrameworkPropertyMetadata(18.9, FrameworkPropertyMetadataOptions.AffectsRender));

    public double GridSizePx
    {
        get => (double)GetValue(GridSizePxProperty);
        set => SetValue(GridSizePxProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var step = GridSizePx;
        if (step < 2)
        {
            return;
        }

        for (var x = step; x < ActualWidth; x += step)
        {
            dc.DrawLine(GridPen, new Point(x, 0), new Point(x, ActualHeight));
        }
        for (var y = step; y < ActualHeight; y += step)
        {
            dc.DrawLine(GridPen, new Point(0, y), new Point(ActualWidth, y));
        }
    }
}
