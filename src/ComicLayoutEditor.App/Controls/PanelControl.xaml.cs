using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ComicLayoutEditor.App.ViewModels;

namespace ComicLayoutEditor.App.Controls;

/// <summary>
/// Control visual de una viñeta: se mueve arrastrando el cuerpo y se redimensiona
/// con ocho tiradores. Coordina la selección y el deshacer/rehacer con el editor.
/// </summary>
public partial class PanelControl : UserControl
{
    /// <summary>Si el arrastre actual ajusta la imagen (Alt) en vez de mover la viñeta.</summary>
    private bool _adjustingImage;

    public PanelControl()
    {
        InitializeComponent();
    }

    private PanelViewModel? Panel => DataContext as PanelViewModel;

    private MainWindowViewModel? Editor =>
        Window.GetWindow(this)?.DataContext as MainWindowViewModel;

    private static bool AltPressed => (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

    // ---- Selección ------------------------------------------------------------

    private void MoveThumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Panel is null || Editor is null)
        {
            return;
        }

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (ctrl)
        {
            Editor.ToggleSelection(Panel);
        }
        else if (!Panel.IsSelected)
        {
            Editor.SelectOnly(Panel);
        }
        // Si ya está seleccionada y no hay Ctrl, se mantiene la selección (arrastre en grupo).
    }

    // ---- Movimiento (o pan de imagen con Alt) --------------------------------

    private void MoveThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (Editor is null || Panel is null)
        {
            return;
        }

        _adjustingImage = AltPressed && Panel.HasImage;
        if (_adjustingImage)
        {
            Editor.BeginImageAdjust(Panel);
        }
        else
        {
            Editor.BeginInteractiveChange(Editor.SelectedPanels);
        }
    }

    private void MoveThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (Editor is null)
        {
            return;
        }

        if (_adjustingImage && Panel is not null)
        {
            Editor.PanImage(Panel, e.HorizontalChange, e.VerticalChange);
        }
        else
        {
            Editor.MoveSelectedBy(e.HorizontalChange, e.VerticalChange);
        }
    }

    private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_adjustingImage)
        {
            Editor?.EndImageAdjust();
            _adjustingImage = false;
        }
        else
        {
            Editor?.EndInteractiveChange();
        }
    }

    // ---- Redimensionado -------------------------------------------------------

    private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (Panel is not null)
        {
            Editor?.BeginInteractiveChange(new[] { Panel });
        }
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (Panel is null || Editor is null || sender is not Thumb { Tag: string handle })
        {
            return;
        }

        double left = Panel.Left;
        double top = Panel.Top;
        double right = Panel.Left + Panel.Width;
        double bottom = Panel.Top + Panel.Height;

        if (handle.Contains('W')) left += e.HorizontalChange;
        if (handle.Contains('E')) right += e.HorizontalChange;
        if (handle.Contains('N')) top += e.VerticalChange;
        if (handle.Contains('S')) bottom += e.VerticalChange;

        if (Editor.SnapToGrid)
        {
            if (handle.Contains('W')) left = Editor.Snap(left);
            if (handle.Contains('E')) right = Editor.Snap(right);
            if (handle.Contains('N')) top = Editor.Snap(top);
            if (handle.Contains('S')) bottom = Editor.Snap(bottom);
        }

        var min = Editor.MinPanelSize;
        var pageW = Panel.PageWidthPx;
        var pageH = Panel.PageHeightPx;

        // Mantener tamaño mínimo según el lado que se mueve.
        if (right - left < min)
        {
            if (handle.Contains('W')) left = right - min;
            else right = left + min;
        }
        if (bottom - top < min)
        {
            if (handle.Contains('N')) top = bottom - min;
            else bottom = top + min;
        }

        // No salirse de la página.
        left = Math.Max(0, left);
        top = Math.Max(0, top);
        right = Math.Min(pageW, right);
        bottom = Math.Min(pageH, bottom);

        Panel.Left = left;
        Panel.Top = top;
        Panel.Width = Math.Max(min, right - left);
        Panel.Height = Math.Max(min, bottom - top);
    }

    // ---- Importar imagen por arrastre -----------------------------------------

    private void PanelControl_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetImageFile(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void PanelControl_Drop(object sender, DragEventArgs e)
    {
        if (Panel is null || Editor is null || !TryGetImageFile(e, out var file))
        {
            return;
        }

        Editor.SelectOnly(Panel);
        Editor.ImportImageToPanel(Panel, file);
        e.Handled = true;
    }

    private static bool TryGetImageFile(DragEventArgs e, out string file)
    {
        file = string.Empty;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            file = files[0];
            return true;
        }
        return false;
    }

    // ---- Zoom de imagen con Alt+rueda -----------------------------------------

    private void PanelControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!AltPressed || Panel is null || Editor is null || !Panel.HasImage)
        {
            return;
        }

        Editor.ZoomImage(Panel, e.Delta > 0 ? 1.1 : 1 / 1.1);
        e.Handled = true;
    }
}
