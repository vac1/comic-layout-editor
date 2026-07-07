using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.App.ViewModels;

/// <summary>
/// ViewModel de una viñeta. Expone posición/tamaño en píxeles del espacio de la
/// página (a escala base, sin zoom) para enlazar directamente con el lienzo, y
/// sincroniza los cambios al modelo como fracciones normalizadas [0,1].
/// </summary>
public partial class PanelViewModel : ObservableObject
{
    private readonly Panel _model;
    private readonly PageViewModel _page;

    public PanelViewModel(Panel model, PageViewModel page)
    {
        _model = model;
        _page = page;
        _left = model.Bounds.X * page.PageWidthPx;
        _top = model.Bounds.Y * page.PageHeightPx;
        _width = model.Bounds.Width * page.PageWidthPx;
        _height = model.Bounds.Height * page.PageHeightPx;
        _zIndex = model.ZIndex;
        ReloadImage();
        foreach (var balloon in model.Balloons)
        {
            Balloons.Add(new BalloonViewModel(balloon, this));
        }
    }

    public Panel Model => _model;
    public PageViewModel Page => _page;
    public Guid Id => _model.Id;

    /// <summary>Bocadillos contenidos en esta viñeta.</summary>
    public ObservableCollection<BalloonViewModel> Balloons { get; } = new();

    public void AddBalloon(BalloonViewModel balloon)
    {
        if (!Balloons.Contains(balloon))
        {
            Balloons.Add(balloon);
        }
        if (!_model.Balloons.Contains(balloon.Model))
        {
            _model.Balloons.Add(balloon.Model);
        }
    }

    public void RemoveBalloon(BalloonViewModel balloon)
    {
        Balloons.Remove(balloon);
        _model.Balloons.Remove(balloon.Model);
    }

    public double PageWidthPx => _page.PageWidthPx;
    public double PageHeightPx => _page.PageHeightPx;

    [ObservableProperty]
    private double _left;

    [ObservableProperty]
    private double _top;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    [ObservableProperty]
    private int _zIndex;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnLeftChanged(double value) => SyncModelBounds();
    partial void OnTopChanged(double value) => SyncModelBounds();

    partial void OnWidthChanged(double value)
    {
        SyncModelBounds();
        OnPropertyChanged(nameof(ImageTranslateX));
        OnPropertyChanged(nameof(ImageRenderZoom));
        foreach (var balloon in Balloons)
        {
            balloon.RecomputeFromModel();
        }
    }

    partial void OnHeightChanged(double value)
    {
        SyncModelBounds();
        OnPropertyChanged(nameof(ImageTranslateY));
        OnPropertyChanged(nameof(ImageRenderZoom));
        foreach (var balloon in Balloons)
        {
            balloon.RecomputeFromModel();
        }
    }

    partial void OnZIndexChanged(int value) => _model.ZIndex = value;

    // ---- Imagen ---------------------------------------------------------------

    public bool HasImage => _model.Image is not null;
    public bool PlaceholderVisible => !HasImage;

    public ImageSource? ImageSource { get; private set; }

    /// <summary>
    /// Modo de estirado del control <c>Image</c>. Cover y Contain usan <c>Uniform</c>
    /// (que nunca recorta contra los límites del Image); el efecto "cubrir" se logra
    /// con <see cref="ImageBaseScale"/> en el RenderTransform, de modo que el recorte
    /// lo aplique el marco (Grid con ClipToBounds) y el pan revele las zonas ocultas.
    /// </summary>
    public Stretch ImageStretch => _model.ImageFit switch
    {
        ImageFit.Stretch => Stretch.Fill,
        _ => Stretch.Uniform // Cover y Contain
    };

    /// <summary>
    /// Escala base que lleva el ajuste <c>Uniform</c> del control al modo deseado:
    /// para Cover amplía hasta cubrir el marco; para Contain/Stretch vale 1. Se combina
    /// con el zoom del usuario en <see cref="ImageRenderZoom"/>.
    /// </summary>
    public double ImageBaseScale
    {
        get
        {
            if (_model.ImageFit != ImageFit.Cover
                || ImageSource is not { Width: > 0, Height: > 0 } src
                || Width <= 0 || Height <= 0)
            {
                return 1.0;
            }
            var uniform = Math.Min(Width / src.Width, Height / src.Height);
            var cover = Math.Max(Width / src.Width, Height / src.Height);
            return uniform > 0 ? cover / uniform : 1.0;
        }
    }

    public double ImageRenderZoom => ImageBaseScale * _model.ImageZoom;
    public double ImageTranslateX => _model.ImageOffset.X * Width;
    public double ImageTranslateY => _model.ImageOffset.Y * Height;

    /// <summary>Establece (o quita) la imagen de la viñeta y recarga la vista.</summary>
    public void SetImage(ImageRef? image)
    {
        _model.Image = image;
        ReloadImage();
    }

    /// <summary>Cambia el modo de ajuste de la imagen.</summary>
    public void SetImageFit(ImageFit fit)
    {
        _model.ImageFit = fit;
        OnPropertyChanged(nameof(ImageStretch));
        OnPropertyChanged(nameof(ImageRenderZoom));
        // El desplazamiento válido depende del ajuste: re-acotarlo para el nuevo modo.
        SetImageTransform(_model.ImageZoom, _model.ImageOffset);
    }

    /// <summary>Rotación actual de la imagen (grados horarios, múltiplos de 90).</summary>
    public int ImageRotation => _model.ImageRotation;

    /// <summary>
    /// Establece la rotación de la imagen (se normaliza a 0/90/180/270) y recarga
    /// la fuente para que el ajuste (Cover/Contain) se recalcule sobre la imagen
    /// ya orientada.
    /// </summary>
    public void SetImageRotation(int degrees)
    {
        _model.ImageRotation = ((degrees % 360) + 360) % 360;
        ReloadImage();
        OnPropertyChanged(nameof(ImageRotation));
    }

    /// <summary>Aplica el zoom y desplazamiento internos de la imagen (el pan se acota al marco).</summary>
    public void SetImageTransform(double zoom, PointD offset)
    {
        _model.ImageZoom = zoom;
        _model.ImageOffset = ClampOffset(zoom, offset);
        OnPropertyChanged(nameof(ImageRenderZoom));
        OnPropertyChanged(nameof(ImageTranslateX));
        OnPropertyChanged(nameof(ImageTranslateY));
    }

    /// <summary>
    /// Acota el desplazamiento (pan) de la imagen para que no pueda sacarse del marco:
    /// si la imagen renderizada es mayor que la viñeta, se impide que aparezcan huecos;
    /// si es menor, se impide que salga del marco. El desplazamiento se expresa como
    /// fracción del tamaño de la viñeta.
    /// </summary>
    private PointD ClampOffset(double zoom, PointD offset)
    {
        if (ImageSource is not { Width: > 0, Height: > 0 } src || Width <= 0 || Height <= 0)
        {
            return offset;
        }

        double iw = src.Width;
        double ih = src.Height;

        // Tamaño de la imagen ya ajustada al marco (Cover/Contain/Stretch) y con el zoom.
        double renderW, renderH;
        if (_model.ImageFit == ImageFit.Stretch)
        {
            renderW = Width * zoom;
            renderH = Height * zoom;
        }
        else
        {
            var baseScale = _model.ImageFit == ImageFit.Contain
                ? Math.Min(Width / iw, Height / ih)
                : Math.Max(Width / iw, Height / ih); // Cover (por defecto)
            renderW = iw * baseScale * zoom;
            renderH = ih * baseScale * zoom;
        }

        double maxOffX = Math.Abs(renderW - Width) / 2.0 / Width;
        double maxOffY = Math.Abs(renderH - Height) / 2.0 / Height;
        return new PointD(
            Math.Clamp(offset.X, -maxOffX, maxOffX),
            Math.Clamp(offset.Y, -maxOffY, maxOffY));
    }

    private void ReloadImage()
    {
        ImageSource = LoadImageSource();
        OnPropertyChanged(nameof(ImageSource));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(PlaceholderVisible));
        OnPropertyChanged(nameof(ImageStretch));
        OnPropertyChanged(nameof(ImageRenderZoom));
        OnPropertyChanged(nameof(ImageTranslateX));
        OnPropertyChanged(nameof(ImageTranslateY));
    }

    private ImageSource? LoadImageSource()
    {
        if (_model.Image is null)
        {
            return null;
        }

        var path = _page.Assets.ResolvePath(_model.Image.RelativePath);
        if (!File.Exists(path))
        {
            return null;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();

        var rotation = ((_model.ImageRotation % 360) + 360) % 360;
        if (rotation == 0)
        {
            return bitmap;
        }

        // Rota el propio bitmap (solo pasos de 90°, admitidos por TransformedBitmap)
        // para que el recorte/ajuste al marco se calcule sobre la imagen ya orientada.
        var rotated = new TransformedBitmap(bitmap, new RotateTransform(rotation));
        rotated.Freeze();
        return rotated;
    }

    /// <summary>Rectángulo actual en píxeles de la página (escala base).</summary>
    public RectD PixelRect => new(Left, Top, Width, Height);

    public void SetPixelRect(RectD rect)
    {
        Left = rect.X;
        Top = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
    }

    /// <summary>
    /// Recalcula la posición/tamaño en píxeles a partir de las fracciones del
    /// modelo. Se usa cuando cambia el tamaño de la página (p. ej. orientación).
    /// </summary>
    public void RefreshFromModel()
    {
        Left = _model.Bounds.X * _page.PageWidthPx;
        Top = _model.Bounds.Y * _page.PageHeightPx;
        Width = _model.Bounds.Width * _page.PageWidthPx;
        Height = _model.Bounds.Height * _page.PageHeightPx;
    }

    private void SyncModelBounds()
    {
        _model.Bounds = new RectD(
            Left / _page.PageWidthPx,
            Top / _page.PageHeightPx,
            Width / _page.PageWidthPx,
            Height / _page.PageHeightPx);
    }
}
