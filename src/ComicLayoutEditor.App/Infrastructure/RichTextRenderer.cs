using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.App.Infrastructure;

/// <summary>
/// Propiedad adjunta para pintar fragmentos con formato (<see cref="TextRun"/>) en los
/// <c>Inline</c>s de un <see cref="TextBlock"/> no editable (render de impresión/exportación).
/// La fuente/tamaño/color no especificados en cada fragmento heredan del propio TextBlock.
/// </summary>
public static class RichTextRenderer
{
    public static readonly DependencyProperty RunsProperty = DependencyProperty.RegisterAttached(
        "Runs",
        typeof(IReadOnlyList<TextRun>),
        typeof(RichTextRenderer),
        new PropertyMetadata(null, OnRunsChanged));

    public static void SetRuns(DependencyObject element, IReadOnlyList<TextRun>? value)
        => element.SetValue(RunsProperty, value);

    public static IReadOnlyList<TextRun>? GetRuns(DependencyObject element)
        => (IReadOnlyList<TextRun>?)element.GetValue(RunsProperty);

    private static void OnRunsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
        {
            return;
        }

        textBlock.Inlines.Clear();
        if (e.NewValue is IReadOnlyList<TextRun> runs)
        {
            RichTextIo.BuildInlines(textBlock.Inlines, runs);
        }
    }
}
