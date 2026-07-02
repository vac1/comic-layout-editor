using System;
using System.Windows;
using System.Windows.Media;
using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.App.Infrastructure;

/// <summary>
/// Construye las geometrías de WPF de las formas de bocadillo (cuerpo y piquito)
/// a partir del tamaño en píxeles y del punto del piquito en coordenadas locales.
/// </summary>
public static class BalloonGeometry
{
    /// <summary>Geometría del cuerpo del bocadillo según su forma.</summary>
    public static Geometry BuildBody(BalloonShape shape, double width, double height)
    {
        var w = Math.Max(1, width);
        var h = Math.Max(1, height);
        var rect = new Rect(0, 0, w, h);

        Geometry geometry = shape switch
        {
            BalloonShape.Rect => new RectangleGeometry(rect),
            BalloonShape.Caption => new RectangleGeometry(rect),
            BalloonShape.Rounded => new RectangleGeometry(rect, Math.Min(w, h) * 0.22, Math.Min(w, h) * 0.22),
            BalloonShape.Shout => BuildStar(w, h),
            _ => new EllipseGeometry(rect) // Oval y Thought comparten cuerpo elíptico
        };
        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// Silueta completa del bocadillo: cuerpo con el piquito fundido en un único
    /// contorno (unión booleana) para las formas de cuerpo sólido. Para las formas
    /// sin piquito de cuerpo sólido, o el bocadillo de pensamiento, devuelve el cuerpo
    /// tal cual (los globos del pensamiento van aparte, ver <see cref="BuildThoughtBubbles"/>).
    /// </summary>
    public static Geometry BuildOutline(BalloonShape shape, double width, double height, Point? tailLocal)
    {
        var body = BuildBody(shape, width, height);

        // Solo las formas de cuerpo sólido funden el piquito en la silueta.
        if (tailLocal is { } tail && shape is BalloonShape.Oval or BalloonShape.Rounded or BalloonShape.Rect
            && BuildTailTriangle(width, height, tail) is { } triangle)
        {
            var combined = Geometry.Combine(body, triangle, GeometryCombineMode.Union, null);
            combined.Freeze();
            return combined;
        }

        return body;
    }

    /// <summary>
    /// Globos del bocadillo de pensamiento hacia <paramref name="tailLocal"/>, o
    /// <c>null</c> si la forma no es de pensamiento o no hay punto de piquito. Van
    /// como figuras separadas (esa es la convención del bocadillo de pensamiento).
    /// </summary>
    public static Geometry? BuildThoughtBubbles(BalloonShape shape, double width, double height, Point? tailLocal)
    {
        if (shape != BalloonShape.Thought || tailLocal is not { } tail)
        {
            return null;
        }

        var w = Math.Max(1, width);
        var h = Math.Max(1, height);
        var center = new Point(w / 2, h / 2);
        var dx = tail.X - center.X;
        var dy = tail.Y - center.Y;
        if (Math.Sqrt(dx * dx + dy * dy) < 1)
        {
            return null;
        }

        return BuildBubbles(center, tail, w, h);
    }

    /// <summary>
    /// Triángulo del piquito, con la base anclada dentro del cuerpo para que la unión
    /// con la silueta sea limpia. <c>null</c> si el piquito es degenerado.
    /// </summary>
    private static Geometry? BuildTailTriangle(double width, double height, Point tail)
    {
        var w = Math.Max(1, width);
        var h = Math.Max(1, height);
        var center = new Point(w / 2, h / 2);

        var dx = tail.X - center.X;
        var dy = tail.Y - center.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1)
        {
            return null;
        }

        var ux = dx / len;
        var uy = dy / len;

        // La base se ancla dentro del cuerpo (factor < 1) para que solape con él y la
        // unión no deje una costura; el vértice es la punta del piquito.
        var baseAnchor = new Point(center.X + ux * (w / 2) * 0.7, center.Y + uy * (h / 2) * 0.7);
        var baseHalf = Math.Max(6, Math.Min(w, h) * 0.16);
        var perpX = -uy;
        var perpY = ux;
        var b1 = new Point(baseAnchor.X + perpX * baseHalf, baseAnchor.Y + perpY * baseHalf);
        var b2 = new Point(baseAnchor.X - perpX * baseHalf, baseAnchor.Y - perpY * baseHalf);

        var figure = new PathFigure { StartPoint = b1, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment(tail, true));
        figure.Segments.Add(new LineSegment(b2, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    private static Geometry BuildStar(double w, double h)
    {
        var cx = w / 2;
        var cy = h / 2;
        var rx = w / 2;
        var ry = h / 2;
        const int spikes = 12;

        var figure = new PathFigure { IsClosed = true, IsFilled = true };
        for (var i = 0; i < spikes * 2; i++)
        {
            var angle = Math.PI * i / spikes - Math.PI / 2;
            var outer = i % 2 == 0;
            var scaleX = outer ? rx : rx * 0.72;
            var scaleY = outer ? ry : ry * 0.72;
            var point = new Point(cx + Math.Cos(angle) * scaleX, cy + Math.Sin(angle) * scaleY);
            if (i == 0)
            {
                figure.StartPoint = point;
            }
            else
            {
                figure.Segments.Add(new LineSegment(point, true));
            }
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static Geometry BuildBubbles(Point center, Point tail, double w, double h)
    {
        var group = new GeometryGroup();
        for (var i = 1; i <= 3; i++)
        {
            var f = i / 4.0;
            var p = new Point(center.X + (tail.X - center.X) * f, center.Y + (tail.Y - center.Y) * f);
            var r = Math.Max(2, Math.Min(w, h) * 0.12 * (1 - f * 0.6));
            group.Children.Add(new EllipseGeometry(p, r, r));
        }
        group.Freeze();
        return group;
    }
}
