using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ComicLayoutEditor.App.Infrastructure;
using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.App.ViewModels;

/// <summary>
/// ViewModel de una página. Traduce el tamaño en milímetros a píxeles de escala
/// base para el lienzo y mantiene la colección observable de viñetas.
/// </summary>
public sealed class PageViewModel : ObservableObject
{
    /// <summary>Píxeles por milímetro a escala base (96 dpi). El zoom se aplica aparte.</summary>
    public const double BasePxPerMm = 96.0 / 25.4;

    private readonly Page _model;

    public PageViewModel(Page model, AssetStore assets)
    {
        _model = model;
        Assets = assets;
        foreach (var panel in model.Panels)
        {
            Panels.Add(new PanelViewModel(panel, this));
        }
    }

    public Page Model => _model;

    /// <summary>Almacén de imágenes del proyecto, compartido por las viñetas.</summary>
    public AssetStore Assets { get; }

    public double PageWidthPx => _model.SizeMm.Width * BasePxPerMm;
    public double PageHeightPx => _model.SizeMm.Height * BasePxPerMm;

    public SizeD SizeMm => _model.SizeMm;

    /// <summary><c>true</c> si la página es más ancha que alta (horizontal).</summary>
    public bool IsLandscape => _model.SizeMm.Width > _model.SizeMm.Height;

    /// <summary>Cambia el tamaño de la página y reajusta la geometría de las viñetas.</summary>
    public void SetSizeMm(SizeD size)
    {
        _model.SizeMm = size;
        RefreshGeometry();
    }

    /// <summary>Notifica el cambio de dimensiones y recalcula las viñetas.</summary>
    public void RefreshGeometry()
    {
        OnPropertyChanged(nameof(PageWidthPx));
        OnPropertyChanged(nameof(PageHeightPx));
        OnPropertyChanged(nameof(SizeMm));
        OnPropertyChanged(nameof(IsLandscape));
        foreach (var panel in Panels)
        {
            panel.RefreshFromModel();
        }
    }

    public ObservableCollection<PanelViewModel> Panels { get; } = new();

    /// <summary>Añade la viñeta a la colección de la VM y al modelo subyacente.</summary>
    public void AddPanel(PanelViewModel panel)
    {
        if (!Panels.Contains(panel))
        {
            Panels.Add(panel);
        }
        if (!_model.Panels.Contains(panel.Model))
        {
            _model.Panels.Add(panel.Model);
        }
    }

    public void RemovePanel(PanelViewModel panel)
    {
        Panels.Remove(panel);
        _model.Panels.Remove(panel.Model);
    }
}
