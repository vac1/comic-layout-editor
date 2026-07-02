using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using ComicLayoutEditor.App.ViewModels;
using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.App.Controls;

/// <summary>
/// Lienzo de la página: dibuja la página a escala, aloja las viñetas y gestiona
/// el arrastre para crear viñetas, la selección por marco y el zoom con Ctrl+rueda.
/// </summary>
public partial class PageCanvasControl : UserControl
{
    private const double MinNewPanelSize = 4;

    private const double BasePxPerMm = 96.0 / 25.4;

    private bool _dragging;
    private bool _creating;
    private bool _additiveMarquee;
    private Point _startPoint;
    private MainWindowViewModel? _subscribed;
    private bool _initialFitDone;

    public PageCanvasControl()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? Editor => DataContext as MainWindowViewModel;

    private void PageCanvasControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (Editor is { } editor && !ReferenceEquals(_subscribed, editor))
        {
            if (_subscribed is not null)
            {
                _subscribed.FitToWindowRequested -= OnFitToWindowRequested;
            }
            editor.FitToWindowRequested += OnFitToWindowRequested;
            _subscribed = editor;
        }

        UpdateRulers();

        if (!_initialFitDone)
        {
            _initialFitDone = true;
            Dispatcher.BeginInvoke(new Action(FitToWindow),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void OnFitToWindowRequested(object? sender, EventArgs e) => FitToWindow();

    private void Scroller_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateRulers();

    /// <summary>Reajusta el zoom para que la página completa quepa en el lienzo.</summary>
    private void FitToWindow()
    {
        var page = Editor?.CurrentPage;
        if (page is null || Scroller.ViewportWidth <= 0 || Scroller.ViewportHeight <= 0)
        {
            return;
        }

        // El contenido lleva un margen de 40 px por lado dentro del ScrollViewer.
        var zoomX = (Scroller.ViewportWidth - 90) / page.PageWidthPx;
        var zoomY = (Scroller.ViewportHeight - 90) / page.PageHeightPx;
        var zoom = Math.Min(zoomX, zoomY);
        if (zoom > 0)
        {
            Editor!.Zoom = zoom; // el VM lo acota a [0.25, 4]
        }
    }

    /// <summary>Sincroniza las reglas con la posición y escala actuales de la página.</summary>
    private void UpdateRulers()
    {
        var page = Editor?.CurrentPage;
        if (page is null || !PageBorder.IsVisible)
        {
            return;
        }

        try
        {
            var origin = PageBorder.TransformToVisual(Scroller).Transform(new Point(0, 0));
            var pxPerMm = BasePxPerMm * Editor!.Zoom;
            TopRuler.Update(origin.X, pxPerMm, page.SizeMm.Width);
            LeftRuler.Update(origin.Y, pxPerMm, page.SizeMm.Height);
        }
        catch (InvalidOperationException)
        {
            // Los elementos aún no comparten árbol visual; se reintentará al próximo scroll/layout.
        }
    }

    private void PageSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var editor = Editor;
        if (editor?.CurrentPage is null)
        {
            return;
        }

        _startPoint = e.GetPosition(PageRoot);
        _creating = editor.IsCreatePanelMode;
        _additiveMarquee = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        if (!_creating && !_additiveMarquee)
        {
            editor.ClearSelection();
        }

        _dragging = true;
        Marquee.Width = 0;
        Marquee.Height = 0;
        Canvas.SetLeft(Marquee, _startPoint.X);
        Canvas.SetTop(Marquee, _startPoint.Y);
        Marquee.Visibility = Visibility.Visible;
        PageSurface.CaptureMouse();
        e.Handled = true;
    }

    private void PageSurface_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || Editor?.CurrentPage is null)
        {
            return;
        }

        var current = e.GetPosition(PageRoot);
        var rect = MakeRect(_startPoint, current, Editor.CurrentPage);
        Canvas.SetLeft(Marquee, rect.X);
        Canvas.SetTop(Marquee, rect.Y);
        Marquee.Width = rect.Width;
        Marquee.Height = rect.Height;
    }

    private void PageSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        PageSurface.ReleaseMouseCapture();
        Marquee.Visibility = Visibility.Collapsed;

        var editor = Editor;
        if (editor?.CurrentPage is null)
        {
            return;
        }

        var end = e.GetPosition(PageRoot);
        var rect = MakeRect(_startPoint, end, editor.CurrentPage);

        if (_creating)
        {
            if (rect.Width >= MinNewPanelSize && rect.Height >= MinNewPanelSize)
            {
                editor.CreatePanel(rect);
            }
        }
        else
        {
            SelectInRect(editor, rect, _additiveMarquee);
        }
    }

    private static void SelectInRect(MainWindowViewModel editor, RectD rect, bool additive)
    {
        if (!additive)
        {
            editor.ClearSelection();
        }

        foreach (var panel in editor.CurrentPage!.Panels)
        {
            if (Intersects(rect, panel.PixelRect))
            {
                editor.AddToSelection(panel);
            }
        }
    }

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control || Editor is null)
        {
            return;
        }

        var factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
        Editor.Zoom *= factor;
        e.Handled = true;
    }

    private static RectD MakeRect(Point a, Point b, PageViewModel page)
    {
        var x = Math.Max(0, Math.Min(a.X, b.X));
        var y = Math.Max(0, Math.Min(a.Y, b.Y));
        var right = Math.Min(page.PageWidthPx, Math.Max(a.X, b.X));
        var bottom = Math.Min(page.PageHeightPx, Math.Max(a.Y, b.Y));
        return RectD.FromLtrb(x, y, Math.Max(x, right), Math.Max(y, bottom));
    }

    private static bool Intersects(RectD a, RectD b)
        => a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
}
