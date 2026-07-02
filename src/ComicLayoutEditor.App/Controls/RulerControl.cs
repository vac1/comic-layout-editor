using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ComicLayoutEditor.App.Controls;

/// <summary>
/// Regla graduada en milímetros para el borde del lienzo. No se enlaza por datos:
/// el lienzo la actualiza con <see cref="Update"/> cuando cambian el zoom o el desplazamiento.
/// </summary>
public sealed class RulerControl : FrameworkElement
{
    private static readonly Brush Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));
    private static readonly Pen TickPen = new(new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)), 1);
    private static readonly Pen EdgePen = new(new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), 1);
    private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));
    private static readonly Typeface LabelTypeface = new("Segoe UI");

    private double _originPx;
    private double _pxPerMm = PxPerMmBase;
    private double _pageLengthMm;

    private const double PxPerMmBase = 96.0 / 25.4;

    static RulerControl()
    {
        Background.Freeze();
        TickPen.Freeze();
        EdgePen.Freeze();
        LabelBrush.Freeze();
    }

    /// <summary>Orientación de la regla.</summary>
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    /// <summary>
    /// Actualiza la regla. <paramref name="originPx"/> es la posición en pantalla
    /// del 0 mm de la página a lo largo del eje de la regla; <paramref name="pxPerMm"/>
    /// los píxeles por milímetro ya escalados por el zoom.
    /// </summary>
    public void Update(double originPx, double pxPerMm, double pageLengthMm)
    {
        _originPx = originPx;
        _pxPerMm = pxPerMm <= 0 ? PxPerMmBase : pxPerMm;
        _pageLengthMm = Math.Max(0, pageLengthMm);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var thickness = horizontal ? ActualHeight : ActualWidth;
        dc.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

        // Borde interior (junto al lienzo).
        if (horizontal)
        {
            dc.DrawLine(EdgePen, new Point(0, thickness - 0.5), new Point(ActualWidth, thickness - 0.5));
        }
        else
        {
            dc.DrawLine(EdgePen, new Point(thickness - 0.5, 0), new Point(thickness - 0.5, ActualHeight));
        }

        // Cada cuántos mm se dibuja una marca según el zoom (evita saturar).
        var minorStep = _pxPerMm >= 6 ? 1 : _pxPerMm >= 3 ? 5 : 10;

        for (var mm = 0; mm <= _pageLengthMm + 0.5; mm += minorStep)
        {
            var pos = _originPx + mm * _pxPerMm;
            if (pos < -1 || pos > (horizontal ? ActualWidth : ActualHeight) + 1)
            {
                continue;
            }

            var major = mm % 10 == 0;
            var medium = mm % 5 == 0;
            var len = major ? thickness * 0.55 : medium ? thickness * 0.38 : thickness * 0.22;

            if (horizontal)
            {
                dc.DrawLine(TickPen, new Point(pos, thickness), new Point(pos, thickness - len));
            }
            else
            {
                dc.DrawLine(TickPen, new Point(thickness, pos), new Point(thickness - len, pos));
            }

            if (major)
            {
                var text = new FormattedText(
                    mm.ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    LabelTypeface, 8, LabelBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

                if (horizontal)
                {
                    dc.DrawText(text, new Point(pos + 1, 1));
                }
                else
                {
                    // Etiqueta girada 90° para la regla vertical.
                    dc.PushTransform(new RotateTransform(-90, 2, pos - 1));
                    dc.DrawText(text, new Point(2, pos - 1));
                    dc.Pop();
                }
            }
        }
    }
}
