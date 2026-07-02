using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicLayoutEditor.App.Infrastructure;
using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.App.ViewModels;

/// <summary>
/// ViewModel de un bocadillo. Posición/tamaño en píxeles locales del panel padre;
/// sincroniza al modelo como fracciones normalizadas [0,1] del panel.
/// </summary>
public partial class BalloonViewModel : ObservableObject
{
    private readonly Balloon _model;
    private readonly PanelViewModel _panel;
    private bool _suppressSync;

    public BalloonViewModel(Balloon model, PanelViewModel panel)
    {
        _model = model;
        _panel = panel;
        RecomputeFromModel();
    }

    public Balloon Model => _model;
    public PanelViewModel Panel => _panel;
    public Guid Id => _model.Id;

    [ObservableProperty]
    private double _left;

    [ObservableProperty]
    private double _top;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEditing;

    partial void OnLeftChanged(double value) { SyncBounds(); RaiseGeometry(); }
    partial void OnTopChanged(double value) { SyncBounds(); RaiseGeometry(); }
    partial void OnWidthChanged(double value) { SyncBounds(); RaiseGeometry(); }
    partial void OnHeightChanged(double value) { SyncBounds(); RaiseGeometry(); }

    // ---- Texto y estilo -------------------------------------------------------

    public string Text
    {
        get => _model.Text;
        set { if (_model.Text != value) { _model.Text = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Fragmentos de texto con formato. Si el modelo no tiene formato enriquecido,
    /// devuelve un único fragmento sin formato con el texto plano.
    /// </summary>
    public IReadOnlyList<TextRun> Runs =>
        _model.RichText is { Count: > 0 } rich
            ? rich
            : new List<TextRun> { new() { Text = _model.Text } };

    /// <summary>Sustituye el contenido con formato y actualiza el texto plano derivado.</summary>
    public void SetRichText(IReadOnlyList<TextRun> runs)
    {
        var list = runs.Select(r => r.Clone()).ToList();
        _model.RichText = list.Count > 0 ? list : null;
        _model.Text = RichTextIo.ToPlainText(list);
        OnPropertyChanged(nameof(Runs));
        OnPropertyChanged(nameof(Text));
    }

    public BalloonShape Shape
    {
        get => _model.Shape;
        set { if (_model.Shape != value) { _model.Shape = value; OnPropertyChanged(); OnPropertyChanged(nameof(SupportsTail)); RaiseGeometry(); } }
    }

    public string FontFamily
    {
        get => _model.FontFamily;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && _model.FontFamily != value)
            {
                _model.FontFamily = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FontFamilyValue));
            }
        }
    }

    public double FontSize
    {
        get => _model.FontSize;
        set { if (Math.Abs(_model.FontSize - value) > 0.01) { _model.FontSize = value; OnPropertyChanged(); } }
    }

    public TextAlign TextAlign
    {
        get => _model.TextAlign;
        set { if (_model.TextAlign != value) { _model.TextAlign = value; OnPropertyChanged(); OnPropertyChanged(nameof(TextAlignmentValue)); } }
    }

    public FontFamily FontFamilyValue => new(_model.FontFamily);

    public TextAlignment TextAlignmentValue => _model.TextAlign switch
    {
        Core.Models.TextAlign.Left => TextAlignment.Left,
        Core.Models.TextAlign.Right => TextAlignment.Right,
        Core.Models.TextAlign.Justify => TextAlignment.Justify,
        _ => TextAlignment.Center
    };

    // ---- Piquito --------------------------------------------------------------

    /// <summary>Formas que admiten piquito (todas salvo cartela y grito).</summary>
    public bool SupportsTail => _model.Shape is not (BalloonShape.Caption or BalloonShape.Shout);

    /// <summary>
    /// Indica si el bocadillo tiene piquito. Al activarlo se coloca uno por defecto
    /// bajo el centro inferior; al desactivarlo se elimina.
    /// </summary>
    public bool HasTail
    {
        get => _model.TailPoint.HasValue;
        set
        {
            if (value == HasTail)
            {
                return;
            }
            if (value)
            {
                SetTailLocal(new Point(Width / 2, Height + 24));
            }
            else
            {
                SetTail(null);
            }
        }
    }

    /// <summary>Punto del piquito en coordenadas locales del bocadillo (px), o <c>null</c>.</summary>
    public Point? TailLocal => _model.TailPoint is { } t
        ? new Point(t.X * _panel.Width - Left, t.Y * _panel.Height - Top)
        : null;

    public double TailHandleX => (TailLocal?.X ?? Width / 2);
    public double TailHandleY => (TailLocal?.Y ?? Height + 20);

    /// <summary>Fija el piquito a partir de un punto en coordenadas locales del bocadillo.</summary>
    public void SetTailLocal(Point local)
    {
        var panelX = (local.X + Left) / Math.Max(1, _panel.Width);
        var panelY = (local.Y + Top) / Math.Max(1, _panel.Height);
        _model.TailPoint = new PointD(panelX, panelY);
        RaiseTail();
    }

    public void SetTail(PointD? tail)
    {
        _model.TailPoint = tail;
        RaiseTail();
    }

    // ---- Geometría ------------------------------------------------------------

    /// <summary>Cuerpo puro (sin piquito), usado como área de arrastre para mover.</summary>
    public Geometry BodyGeometry => BalloonGeometry.BuildBody(_model.Shape, Width, Height);

    /// <summary>Silueta visible: cuerpo con el piquito fundido en un solo contorno.</summary>
    public Geometry OutlineGeometry => BalloonGeometry.BuildOutline(_model.Shape, Width, Height, TailLocal);

    /// <summary>Globos del bocadillo de pensamiento (figuras aparte), o <c>null</c>.</summary>
    public Geometry? ThoughtBubblesGeometry => BalloonGeometry.BuildThoughtBubbles(_model.Shape, Width, Height, TailLocal);

    // ---- Conversión px <-> normalizado ---------------------------------------

    public RectD PixelRect => new(Left, Top, Width, Height);

    public void SetPixelRect(RectD rect)
    {
        Left = rect.X;
        Top = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
    }

    /// <summary>Recalcula los píxeles desde el modelo (p. ej. al redimensionar el panel).</summary>
    public void RecomputeFromModel()
    {
        _suppressSync = true;
        Left = _model.Bounds.X * _panel.Width;
        Top = _model.Bounds.Y * _panel.Height;
        Width = _model.Bounds.Width * _panel.Width;
        Height = _model.Bounds.Height * _panel.Height;
        _suppressSync = false;
        RaiseGeometry();
    }

    private void SyncBounds()
    {
        if (_suppressSync)
        {
            return;
        }
        _model.Bounds = new RectD(
            Left / Math.Max(1, _panel.Width),
            Top / Math.Max(1, _panel.Height),
            Width / Math.Max(1, _panel.Width),
            Height / Math.Max(1, _panel.Height));
    }

    private void RaiseGeometry()
    {
        OnPropertyChanged(nameof(BodyGeometry));
        RaiseTail();
    }

    private void RaiseTail()
    {
        OnPropertyChanged(nameof(HasTail));
        OnPropertyChanged(nameof(TailLocal));
        OnPropertyChanged(nameof(OutlineGeometry));
        OnPropertyChanged(nameof(ThoughtBubblesGeometry));
        OnPropertyChanged(nameof(TailHandleX));
        OnPropertyChanged(nameof(TailHandleY));
    }
}
