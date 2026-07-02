using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.App.Infrastructure;

/// <summary>
/// Conversión entre el modelo portable de fragmentos (<see cref="TextRun"/>) y el
/// <see cref="FlowDocument"/> que edita el <c>RichTextBox</c>. Las propiedades nulas
/// de un fragmento heredan de los valores por defecto del bocadillo.
/// </summary>
public static class RichTextIo
{
    /// <summary>Construye el documento a partir de los fragmentos y los valores por defecto del bocadillo.</summary>
    public static FlowDocument ToDocument(IReadOnlyList<TextRun> runs, string defaultFont, double defaultSize, TextAlignment align)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily(defaultFont),
            FontSize = defaultSize,
            PagePadding = new Thickness(0)
        };

        var para = new Paragraph { TextAlignment = align, Margin = new Thickness(0) };
        BuildInlines(para.Inlines, runs);
        doc.Blocks.Add(para);
        return doc;
    }

    /// <summary>Añade a <paramref name="target"/> los <c>Inline</c>s de los fragmentos con su formato.</summary>
    public static void BuildInlines(InlineCollection target, IReadOnlyList<TextRun> runs)
    {
        foreach (var run in runs)
        {
            var parts = run.Text.Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    target.Add(new LineBreak());
                }
                if (parts[i].Length == 0)
                {
                    continue;
                }

                var r = new Run(parts[i]);
                if (run.Bold) r.FontWeight = FontWeights.Bold;
                if (run.Italic) r.FontStyle = FontStyles.Italic;
                if (run.Underline) r.TextDecorations = TextDecorations.Underline;
                if (!string.IsNullOrEmpty(run.FontFamily)) r.FontFamily = new FontFamily(run.FontFamily);
                if (run.FontSize is { } fs && fs > 0) r.FontSize = fs;
                if (TryParseColor(run.Color, out var color)) r.Foreground = new SolidColorBrush(color);
                target.Add(r);
            }
        }
    }

    /// <summary>Lee el documento y produce los fragmentos, marcando como <c>null</c> lo que coincide con el defecto.</summary>
    public static List<TextRun> FromDocument(FlowDocument doc, string defaultFont, double defaultSize)
    {
        var tokens = new List<(string Text, TextRun Fmt)>();
        var first = true;
        foreach (var block in doc.Blocks)
        {
            if (!first)
            {
                Append(tokens, "\n", tokens.Count > 0 ? tokens[^1].Fmt : new TextRun());
            }
            first = false;
            if (block is Paragraph p)
            {
                WalkInlines(p.Inlines, tokens, underline: false, defaultFont, defaultSize);
            }
        }

        return Coalesce(tokens);
    }

    /// <summary>Concatena el texto de los fragmentos (proyección plana para <see cref="Balloon.Text"/>).</summary>
    public static string ToPlainText(IReadOnlyList<TextRun> runs)
        => string.Concat(runs.Select(r => r.Text));

    private static void WalkInlines(InlineCollection inlines, List<(string, TextRun)> tokens, bool underline, string defaultFont, double defaultSize)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    var fmt = ReadFormat(run, underline || HasUnderline(run.TextDecorations), defaultFont, defaultSize);
                    Append(tokens, run.Text, fmt);
                    break;
                case LineBreak:
                    Append(tokens, "\n", tokens.Count > 0 ? tokens[^1].Item2 : new TextRun());
                    break;
                case Span span:
                    WalkInlines(span.Inlines, tokens, underline || HasUnderline(span.TextDecorations), defaultFont, defaultSize);
                    break;
            }
        }
    }

    private static TextRun ReadFormat(Run run, bool underline, string defaultFont, double defaultSize)
    {
        var font = run.FontFamily?.Source;
        var size = run.FontSize;
        string? color = null;
        if (run.Foreground is SolidColorBrush { Color: var c } && c != Colors.Black)
        {
            color = c.A == 255
                ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
                : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        return new TextRun
        {
            Bold = run.FontWeight.ToOpenTypeWeight() >= FontWeights.Bold.ToOpenTypeWeight(),
            Italic = run.FontStyle == FontStyles.Italic,
            Underline = underline,
            FontFamily = string.Equals(font, defaultFont, StringComparison.Ordinal) ? null : font,
            FontSize = Math.Abs(size - defaultSize) < 0.01 ? null : size,
            Color = color
        };
    }

    private static void Append(List<(string Text, TextRun Fmt)> tokens, string text, TextRun fmt)
    {
        if (text.Length == 0)
        {
            return;
        }
        tokens.Add((text, fmt));
    }

    private static List<TextRun> Coalesce(List<(string Text, TextRun Fmt)> tokens)
    {
        var runs = new List<TextRun>();
        foreach (var (text, fmt) in tokens)
        {
            if (runs.Count > 0 && SameFormat(runs[^1], fmt))
            {
                runs[^1].Text += text;
            }
            else
            {
                var r = fmt.Clone();
                r.Text = text;
                runs.Add(r);
            }
        }
        return runs;
    }

    private static bool SameFormat(TextRun a, TextRun b)
        => a.Bold == b.Bold && a.Italic == b.Italic && a.Underline == b.Underline
           && a.FontFamily == b.FontFamily
           && Nullable.Equals(a.FontSize, b.FontSize)
           && a.Color == b.Color;

    private static bool HasUnderline(TextDecorationCollection? decorations)
        => decorations is { Count: > 0 } && decorations.Any(d => d.Location == TextDecorationLocation.Underline);

    private static bool TryParseColor(string? hex, out Color color)
    {
        color = Colors.Black;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
