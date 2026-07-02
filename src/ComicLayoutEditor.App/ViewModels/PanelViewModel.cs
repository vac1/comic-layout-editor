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
        foreach (var balloon in Balloons)
        {
            balloon.RecomputeFromModel();
        }
    }

    partial void OnHeightChanged(double value)
    {
        SyncModelBounds();
        OnPropertyChanged(nameof(ImageTranslateY));
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

    /// <summary>Modo de estirado de WPF derivado del ajuste del modelo.</summary>
    public Stretch ImageStretch => _model.ImageFit switch
    {
        ImageFit.Cover => Stretch.UniformToFill,
        ImageFit.Contain => Stretch.Uniform,
        ImageFit.Stretch => Stretch.Fill,
        _ => Stretch.UniformToFill
    };

    public double ImageRenderZoom => _model.ImageZoom;
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

    /// <summary>Aplica el zoom y desplazamiento internos de la imagen.</summary>
    public void SetImageTransform(double zoom, PointD offset)
    {
        _model.ImageZoom = zoom;
        _model.ImageOffset = offset;
        OnPropertyChanged(nameof(ImageRenderZoom));
        OnPropertyChanged(nameof(ImageTranslateX));
        OnPropertyChanged(nameof(ImageTranslateY));
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
