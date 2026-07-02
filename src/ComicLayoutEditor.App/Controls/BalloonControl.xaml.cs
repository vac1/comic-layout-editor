using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ComicLayoutEditor.App.Infrastructure;
using ComicLayoutEditor.App.ViewModels;
using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.App.Controls;

/// <summary>
/// Control visual de un bocadillo: forma con piquito, edición de texto con formato
/// (negrita/cursiva/subrayado, fuente, tamaño y color por fragmento) y tiradores
/// para mover, redimensionar y ajustar el piquito.
/// </summary>
public partial class BalloonControl : UserControl
{
    private const double MinBalloonSize = 20;

    private static readonly double[] FontSizes =
        { 8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72 };

    private static readonly SolidColorBrush[] ColorSwatches =
    {
        Brushes.Black,
        (SolidColorBrush)new BrushConverter().ConvertFromString("#E53935")!, // rojo
        (SolidColorBrush)new BrushConverter().ConvertFromString("#1E88E5")!, // azul
        (SolidColorBrush)new BrushConverter().ConvertFromString("#43A047")!, // verde
        (SolidColorBrush)new BrushConverter().ConvertFromString("#FB8C00")!, // naranja
        (SolidColorBrush)new BrushConverter().ConvertFromString("#8E24AA")!, // morado
        Brushes.White
    };

    private List<TextRun> _editStartRuns = new();
    private BalloonViewModel? _hooked;
    private bool _syncingToolbar;

    public BalloonControl()
    {
        InitializeComponent();
        SizeCombo.ItemsSource = FontSizes;
        ColorCombo.ItemsSource = ColorSwatches;

        Loaded += (_, _) => { HookViewModel(); RebuildDocument(); };
        DataContextChanged += (_, _) => { HookViewModel(); RebuildDocument(); };
        Unloaded += (_, _) => UnhookViewModel();
    }

    private BalloonViewModel? Balloon => DataContext as BalloonViewModel;

    private MainWindowViewModel? Editor =>
        Window.GetWindow(this)?.DataContext as MainWindowViewModel;

    // ---- Sincronización del documento con el modelo ---------------------------

    private void HookViewModel()
    {
        if (ReferenceEquals(_hooked, Balloon))
        {
            return;
        }
        UnhookViewModel();
        _hooked = Balloon;
        if (_hooked is not null)
        {
            _hooked.PropertyChanged += OnBalloonPropertyChanged;
        }
    }

    private void UnhookViewModel()
    {
        if (_hooked is not null)
        {
            _hooked.PropertyChanged -= OnBalloonPropertyChanged;
            _hooked = null;
        }
    }

    private void OnBalloonPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BalloonViewModel.Runs)
            or nameof(BalloonViewModel.FontFamily)
            or nameof(BalloonViewModel.FontFamilyValue)
            or nameof(BalloonViewModel.FontSize)
            or nameof(BalloonViewModel.TextAlign)
            or nameof(BalloonViewModel.TextAlignmentValue))
        {
            RebuildDocument();
        }
    }

    /// <summary>Reconstruye el documento del editor desde los fragmentos del bocadillo.</summary>
    private void RebuildDocument()
    {
        if (Balloon is null || TextEditor is null || Balloon.IsEditing)
        {
            return; // durante la edición manda el documento en vivo
        }
        TextEditor.Document = RichTextIo.ToDocument(
            Balloon.Runs, Balloon.Model.FontFamily, Balloon.Model.FontSize, Balloon.TextAlignmentValue);
    }

    // ---- Selección y edición --------------------------------------------------

    private void BalloonControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Balloon is null || Editor is null || Balloon.IsEditing)
        {
            return;
        }

        Editor.SelectBalloon(Balloon);

        if (e.ClickCount == 2)
        {
            BeginEdit();
            e.Handled = true;
        }
    }

    private void BeginEdit()
    {
        if (Balloon is null)
        {
            return;
        }

        _editStartRuns = Balloon.Runs.Select(r => r.Clone()).ToList();
        Balloon.IsEditing = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            TextEditor.Focus();
            TextEditor.SelectAll();
        }));
    }

    private void EndEdit()
    {
        if (Balloon is null || !Balloon.IsEditing)
        {
            return;
        }

        var runs = RichTextIo.FromDocument(TextEditor.Document, Balloon.Model.FontFamily, Balloon.Model.FontSize);
        Balloon.SetRichText(runs);
        Balloon.IsEditing = false;
        Editor?.CommitBalloonRichText(Balloon, _editStartRuns);
        RebuildDocument();
    }

    private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || (e.Key == Key.Enter && (Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) == 0))
        {
            EndEdit();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void TextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // No terminar la edición si el foco pasa a la barra de formato de este bocadillo.
        if (e.NewFocus is DependencyObject next && IsInThisControl(next))
        {
            return;
        }
        EndEdit();
    }

    private bool IsInThisControl(DependencyObject node)
    {
        for (DependencyObject? cur = node; cur is not null; cur = VisualTreeHelper.GetParent(cur))
        {
            if (ReferenceEquals(cur, this))
            {
                return true;
            }
        }
        return false;
    }

    // ---- Barra de formato -----------------------------------------------------

    private void TextEditor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (Balloon is null || !Balloon.IsEditing || _syncingToolbar)
        {
            return;
        }

        _syncingToolbar = true;
        try
        {
            var sel = TextEditor.Selection;

            BoldBtn.IsChecked = sel.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight fw
                                && fw.ToOpenTypeWeight() >= FontWeights.Bold.ToOpenTypeWeight();
            ItalicBtn.IsChecked = sel.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle fs
                                  && fs == FontStyles.Italic;
            UnderlineBtn.IsChecked = sel.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection dec
                                     && dec.Any(d => d.Location == TextDecorationLocation.Underline);

            FontCombo.SelectedItem = sel.GetPropertyValue(TextElement.FontFamilyProperty) is FontFamily ff
                ? ff.Source
                : null;
            SizeCombo.SelectedItem = sel.GetPropertyValue(TextElement.FontSizeProperty) is double sz
                ? FontSizes.FirstOrDefault(s => Math.Abs(s - sz) < 0.01)
                : null;
            ColorCombo.SelectedItem = sel.GetPropertyValue(TextElement.ForegroundProperty) is SolidColorBrush b
                ? ColorSwatches.FirstOrDefault(sw => sw.Color == b.Color)
                : null;
        }
        finally
        {
            _syncingToolbar = false;
        }
    }

    private void FontCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingToolbar || Balloon is null || !Balloon.IsEditing)
        {
            return;
        }
        if (FontCombo.SelectedItem is string family)
        {
            ApplyToSelection(TextElement.FontFamilyProperty, new FontFamily(family));
        }
    }

    private void SizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingToolbar || Balloon is null || !Balloon.IsEditing)
        {
            return;
        }
        if (SizeCombo.SelectedItem is double size)
        {
            ApplyToSelection(TextElement.FontSizeProperty, size);
        }
    }

    private void ColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingToolbar || Balloon is null || !Balloon.IsEditing)
        {
            return;
        }
        if (ColorCombo.SelectedItem is SolidColorBrush brush)
        {
            ApplyToSelection(TextElement.ForegroundProperty, brush);
        }
    }

    private void ApplyToSelection(DependencyProperty property, object value)
    {
        TextEditor.Selection.ApplyPropertyValue(property, value);
        TextEditor.Focus();
    }

    // ---- Mover ----------------------------------------------------------------

    private void MoveThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (Balloon is not null)
        {
            Editor?.SelectBalloon(Balloon);
            Editor?.BeginBalloonChange(Balloon);
        }
    }

    private void MoveThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (Balloon is not null)
        {
            Editor?.MoveBalloonBy(Balloon, e.HorizontalChange, e.VerticalChange);
        }
    }

    private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
        => Editor?.EndBalloonChange();

    // ---- Redimensionar --------------------------------------------------------

    private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (Balloon is not null)
        {
            Editor?.BeginBalloonChange(Balloon);
        }
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (Balloon is null || sender is not Thumb { Tag: string handle })
        {
            return;
        }

        double left = Balloon.Left;
        double top = Balloon.Top;
        double right = Balloon.Left + Balloon.Width;
        double bottom = Balloon.Top + Balloon.Height;

        if (handle.Contains('W')) left += e.HorizontalChange;
        if (handle.Contains('E')) right += e.HorizontalChange;
        if (handle.Contains('N')) top += e.VerticalChange;
        if (handle.Contains('S')) bottom += e.VerticalChange;

        // Límites de la página en coordenadas locales de la viñeta: el bocadillo
        // puede sobresalir del marco pero no salir de la página.
        var (minX, minY, maxX, maxY) = MainWindowViewModel.PageBoundsInPanelSpace(Balloon.Panel);

        if (right - left < MinBalloonSize)
        {
            if (handle.Contains('W')) left = right - MinBalloonSize;
            else right = left + MinBalloonSize;
        }
        if (bottom - top < MinBalloonSize)
        {
            if (handle.Contains('N')) top = bottom - MinBalloonSize;
            else bottom = top + MinBalloonSize;
        }

        left = Math.Max(minX, left);
        top = Math.Max(minY, top);
        right = Math.Min(maxX, right);
        bottom = Math.Min(maxY, bottom);

        Balloon.Left = left;
        Balloon.Top = top;
        Balloon.Width = Math.Max(MinBalloonSize, right - left);
        Balloon.Height = Math.Max(MinBalloonSize, bottom - top);
    }

    // ---- Piquito --------------------------------------------------------------

    private void TailThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (Balloon is not null)
        {
            Editor?.BeginBalloonChange(Balloon);
        }
    }

    private void TailThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (Balloon is null)
        {
            return;
        }

        var current = Balloon.TailLocal ?? new Point(Balloon.Width / 2, Balloon.Height + 20);
        Balloon.SetTailLocal(new Point(current.X + e.HorizontalChange, current.Y + e.VerticalChange));
    }
}
