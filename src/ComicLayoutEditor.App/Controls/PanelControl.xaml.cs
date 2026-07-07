using System;
using System.ComponentModel;
using System.Linq;
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
    /// <summary>Si el arrastre actual ajusta la imagen (Alt o modo) en vez de mover la viñeta.</summary>
    private bool _adjustingImage;

    // Posición del ratón (en coordenadas de la viñeta) al empezar a arrastrar la imagen.
    private Point _panStartMouse;

    // Estado del arrastre de un tirador de escala de imagen.
    private double _scaleStartDist;
    private double _scaleStartZoom;

    // ViewModels a los que estamos suscritos para refrescar los adornos del modo.
    private MainWindowViewModel? _editorSub;
    private PanelViewModel? _panelSub;

    public PanelControl()
    {
        InitializeComponent();
    }

    private PanelViewModel? Panel => DataContext as PanelViewModel;

    private MainWindowViewModel? Editor =>
        Window.GetWindow(this)?.DataContext as MainWindowViewModel;

    private static bool AltPressed => (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

    /// <summary>El gesto actual debe ajustar la imagen: con Alt o en modo de ajuste, y con imagen.</summary>
    private bool ImageAdjustGesture =>
        Panel?.HasImage == true && (AltPressed || Editor?.IsAdjustImageMode == true);

    // ---- Adornos del modo de ajuste (borde + tiradores de escala) -------------

    private void PanelControl_Loaded(object sender, RoutedEventArgs e)
    {
        HookViewModels();
        UpdateAdjustVisuals();
    }

    private void PanelControl_Unloaded(object sender, RoutedEventArgs e) => UnhookViewModels();

    private void PanelControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        HookViewModels();
        UpdateAdjustVisuals();
    }

    private void HookViewModels()
    {
        var editor = Editor;
        if (!ReferenceEquals(editor, _editorSub))
        {
            if (_editorSub is not null) _editorSub.PropertyChanged -= OnEditorPropertyChanged;
            _editorSub = editor;
            if (editor is not null) editor.PropertyChanged += OnEditorPropertyChanged;
        }

        var panel = Panel;
        if (!ReferenceEquals(panel, _panelSub))
        {
            if (_panelSub is not null) _panelSub.PropertyChanged -= OnPanelPropertyChanged;
            _panelSub = panel;
            if (panel is not null) panel.PropertyChanged += OnPanelPropertyChanged;
        }
    }

    private void UnhookViewModels()
    {
        if (_editorSub is not null) _editorSub.PropertyChanged -= OnEditorPropertyChanged;
        if (_panelSub is not null) _panelSub.PropertyChanged -= OnPanelPropertyChanged;
        _editorSub = null;
        _panelSub = null;
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsAdjustImageMode))
        {
            UpdateAdjustVisuals();
        }
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PanelViewModel.IsSelected) or nameof(PanelViewModel.HasImage))
        {
            UpdateAdjustVisuals();
        }
    }

    /// <summary>
    /// Muestra los tiradores de redimensionado del marco o los de escala de imagen
    /// según esté activo el modo de ajuste sobre esta viñeta (con imagen y seleccionada).
    /// </summary>
    private void UpdateAdjustVisuals()
    {
        var adjusting = Panel?.HasImage == true && Editor?.IsAdjustImageMode == true;
        var selected = Panel?.IsSelected == true;

        ResizeHandlesGrid.Visibility = selected && !adjusting ? Visibility.Visible : Visibility.Collapsed;
        ImageAdjustGrid.Visibility = selected && adjusting ? Visibility.Visible : Visibility.Collapsed;
        AdjustModeBorder.Visibility = selected && adjusting ? Visibility.Visible : Visibility.Collapsed;
    }

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

        _adjustingImage = ImageAdjustGesture;
        if (_adjustingImage)
        {
            Editor.BeginImageAdjust(Panel);
            // Referencia estable: la viñeta no se mueve durante el ajuste, así que la
            // posición del ratón en sus coordenadas locales da el pan absoluto y exacto.
            _panStartMouse = Mouse.GetPosition(this);
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
            var mouse = Mouse.GetPosition(this);
            Editor.PanImage(Panel, mouse.X - _panStartMouse.X, mouse.Y - _panStartMouse.Y);
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

    private static readonly string[] ImageExtensions =
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    private static bool TryGetImageFile(DragEventArgs e, out string file)
    {
        file = string.Empty;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return false;
        }

        foreach (var candidate in files)
        {
            var ext = System.IO.Path.GetExtension(candidate);
            if (ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                file = candidate;
                return true;
            }
        }
        return false;
    }

    // ---- Zoom de imagen con Alt+rueda -----------------------------------------

    private void PanelControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Editor is null || !ImageAdjustGesture)
        {
            return;
        }

        Editor.ZoomImage(Panel!, e.Delta > 0 ? 1.1 : 1 / 1.1);
        e.Handled = true;
    }

    // ---- Escala de imagen con los tiradores de las esquinas -------------------

    private void ImageScaleThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (Editor is null || Panel is null || !Panel.HasImage)
        {
            return;
        }

        Editor.BeginImageAdjust(Panel);
        // La escala depende de cuánto se aleja/acerca el cursor del centro de la viñeta
        // (pivote del escalado). Se mide con la posición del ratón, estable durante el gesto.
        var mouse = Mouse.GetPosition(this);
        _scaleStartDist = Math.Max(1, Distance(mouse.X - Panel.Width / 2.0, mouse.Y - Panel.Height / 2.0));
        _scaleStartZoom = Panel.Model.ImageZoom;
    }

    private void ImageScaleThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (Editor is null || Panel is null)
        {
            return;
        }

        var mouse = Mouse.GetPosition(this);
        var dist = Distance(mouse.X - Panel.Width / 2.0, mouse.Y - Panel.Height / 2.0);
        Editor.SetImageZoom(Panel, _scaleStartZoom * (dist / _scaleStartDist));
    }

    private void ImageScaleThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        => Editor?.EndImageAdjust();

    private static double Distance(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);
}
