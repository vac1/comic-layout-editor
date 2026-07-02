using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComicLayoutEditor.App.Infrastructure;
using ComicLayoutEditor.Core.Models;
using ComicLayoutEditor.Core.Serialization;
using Microsoft.Win32;

namespace ComicLayoutEditor.App.ViewModels;

/// <summary>
/// ViewModel principal del editor: documento, páginas, selección, comandos de
/// edición (crear/eliminar viñetas, orden Z, alineación, distribución),
/// deshacer/rehacer, zoom y ajuste a rejilla.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private const double MinPanelSizePx = 12;
    private const double Epsilon = 0.01;

    /// <summary>Rectángulos de las viñetas al iniciar una interacción (drag/resize).</summary>
    private Dictionary<PanelViewModel, RectD>? _interactiveStart;

    /// <summary>Estado de la imagen (zoom/offset) al iniciar un ajuste interactivo.</summary>
    private (PanelViewModel Panel, double Zoom, PointD Offset)? _imageAdjustStart;

    /// <summary>Estado del bocadillo (rect + piquito) al iniciar un ajuste interactivo.</summary>
    private (BalloonViewModel Balloon, RectD Rect, PointD? Tail)? _balloonStart;

    /// <summary>Viñetas copiadas (modelos clonados), listas para pegar. Interno a la app.</summary>
    private List<Panel>? _panelClipboard;

    /// <summary>Bocadillo copiado (modelo clonado), listo para pegar. Interno a la app.</summary>
    private Balloon? _balloonClipboard;

    private AssetStore _assetStore;

    public MainWindowViewModel()
    {
        _assetStore = CreateTempAssetStore();
        Undo.Changed += (_, _) => { RefreshCommandStates(); IsDirty = true; };
        SelectedPanels.CollectionChanged += (_, _) => RefreshCommandStates();
        LoadDocument(ComicDocument.CreateNew("New comic"), _assetStore);
        DocumentTitle = "New comic";
        IsDirty = false;
    }

    private static AssetStore CreateTempAssetStore()
    {
        var dir = Path.Combine(ComicProjectPackage.CreateTempWorkingDirectory(), ComicProjectPackage.AssetsFolderName);
        return new AssetStore(dir);
    }

    // ---- Identidad del documento ---------------------------------------------

    /// <summary>Ruta del archivo <c>.comicproj</c> actual, o <c>null</c> si no se ha guardado.</summary>
    [ObservableProperty]
    private string? _currentFilePath;

    /// <summary>Título del documento (se serializa en el manifiesto).</summary>
    [ObservableProperty]
    private string _documentTitle = "New comic";

    /// <summary>Hay cambios sin guardar.</summary>
    [ObservableProperty]
    private bool _isDirty;

    partial void OnDocumentTitleChanged(string value)
    {
        IsDirty = true;
        OnPropertyChanged(nameof(WindowTitle));
    }

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));
    partial void OnCurrentFilePathChanged(string? value) => OnPropertyChanged(nameof(WindowTitle));

    /// <summary>Texto de la barra de título de la ventana.</summary>
    public string WindowTitle
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(DocumentTitle) ? "Untitled" : DocumentTitle;
            return $"{(IsDirty ? "*" : "")}{name} — ComicLayout Editor";
        }
    }

    public UndoRedoStack Undo { get; } = new();

    public ObservableCollection<PageViewModel> Pages { get; } = new();

    public ObservableCollection<PanelViewModel> SelectedPanels { get; } = new();

    [ObservableProperty]
    private PageViewModel? _currentPage;

    [ObservableProperty]
    private BalloonViewModel? _selectedBalloon;

    /// <summary>Fuentes del sistema, para el panel de propiedades del bocadillo.</summary>
    public IReadOnlyList<string> AvailableFonts { get; } =
        Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(n => n).ToList();

    public Array BalloonShapes { get; } = Enum.GetValues(typeof(BalloonShape));
    public Array TextAligns { get; } = Enum.GetValues(typeof(TextAlign));

    [ObservableProperty]
    private bool _isCreatePanelMode;

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private bool _snapToGrid;

    [ObservableProperty]
    private bool _showGrid;

    [ObservableProperty]
    private double _gridSizeMm = 5.0;

    public double GridSizePx => GridSizeMm * PageViewModel.BasePxPerMm;

    /// <summary>Tamaños de rejilla preseleccionados (mm) para la barra de herramientas.</summary>
    public IReadOnlyList<double> GridSizeOptions { get; } = new double[] { 2, 5, 10, 20 };

    partial void OnGridSizeMmChanged(double value) => OnPropertyChanged(nameof(GridSizePx));

    // ---- Preferencias de sesión (rejilla, ajuste, zoom) -----------------------

    /// <summary>Aplica los ajustes de editor restaurados al abrir la aplicación.</summary>
    public void ApplyEditorSettings(UserSettings settings)
    {
        ShowGrid = settings.ShowGrid;
        SnapToGrid = settings.SnapToGrid;
        if (settings.GridSizeMm > 0)
        {
            GridSizeMm = settings.GridSizeMm;
        }
        Zoom = settings.Zoom;

        RecentFiles.Clear();
        foreach (var path in settings.RecentFiles)
        {
            RecentFiles.Add(new RecentFileEntry(path));
        }
        OnPropertyChanged(nameof(HasRecentFiles));
    }

    /// <summary>Vuelca los ajustes de editor actuales en <paramref name="settings"/> para guardarlos.</summary>
    public void CaptureEditorSettings(UserSettings settings)
    {
        settings.ShowGrid = ShowGrid;
        settings.SnapToGrid = SnapToGrid;
        settings.GridSizeMm = GridSizeMm;
        settings.Zoom = Zoom;
    }

    partial void OnZoomChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.25, 4.0);
        if (Math.Abs(clamped - value) > double.Epsilon)
        {
            Zoom = clamped;
        }
    }

    // ---- Zoom -----------------------------------------------------------------

    /// <summary>Se solicita ajustar el zoom para que la página quepa en la ventana.</summary>
    public event EventHandler? FitToWindowRequested;

    [RelayCommand]
    private void ZoomIn() => Zoom *= 1.25;

    [RelayCommand]
    private void ZoomOut() => Zoom *= 0.8;

    [RelayCommand]
    private void ResetZoom() => Zoom = 1.0;

    [RelayCommand]
    private void FitToWindow() => FitToWindowRequested?.Invoke(this, EventArgs.Empty);

    // ---- Nudge (mover con precisión con las flechas) --------------------------

    [RelayCommand]
    private void Nudge(string direction)
    {
        var step = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0 ? 10.0 : 1.0;
        var (dx, dy) = direction switch
        {
            "Left" => (-step, 0.0),
            "Right" => (step, 0.0),
            "Up" => (0.0, -step),
            "Down" => (0.0, step),
            _ => (0.0, 0.0)
        };
        if (dx == 0 && dy == 0)
        {
            return;
        }

        if (SelectedBalloon is { } balloon)
        {
            BeginBalloonChange(balloon);
            MoveBalloonBy(balloon, dx, dy);
            EndBalloonChange();
        }
        else if (HasSelection)
        {
            BeginInteractiveChange(SelectedPanels.ToList());
            MoveSelectedBy(dx, dy);
            EndInteractiveChange();
        }
    }

    // ---- Carga de documento ---------------------------------------------------

    public void LoadDocument(ComicDocument document, AssetStore assetStore)
    {
        _assetStore = assetStore;
        SelectedBalloon = null;
        SelectedPanels.Clear();
        Pages.Clear();
        foreach (var page in document.Pages)
        {
            Pages.Add(new PageViewModel(page, assetStore));
        }
        CurrentPage = Pages.FirstOrDefault();
        Undo.Clear();
        RefreshCommandStates();
    }

    // ---- Archivos recientes ---------------------------------------------------

    /// <summary>Últimos proyectos abiertos/guardados, del más reciente al más antiguo.</summary>
    public ObservableCollection<RecentFileEntry> RecentFiles { get; } = new();

    public bool HasRecentFiles => RecentFiles.Count > 0;

    /// <summary>
    /// Registra <paramref name="path"/> como reciente: lo persiste en las
    /// preferencias y actualiza la lista en memoria (mostrada en el menú).
    /// </summary>
    private void RegisterRecentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        UserSettings.AddRecentFile(path);

        var existing = RecentFiles.FirstOrDefault(
            e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentFiles.Remove(existing);
        }
        RecentFiles.Insert(0, new RecentFileEntry(path));
        while (RecentFiles.Count > UserSettings.MaxRecentFiles)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }
        OnPropertyChanged(nameof(HasRecentFiles));
    }

    private void RemoveRecentFile(string path)
    {
        UserSettings.RemoveRecentFile(path);
        var existing = RecentFiles.FirstOrDefault(
            e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentFiles.Remove(existing);
            OnPropertyChanged(nameof(HasRecentFiles));
        }
    }

    /// <summary>Abre un proyecto de la lista de recientes (con confirmación de descartes).</summary>
    [RelayCommand]
    private void OpenRecent(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path))
        {
            MessageBox.Show(
                $"The file no longer exists and will be removed from the list:\n{path}",
                "Open Recent", MessageBoxButton.OK, MessageBoxImage.Warning);
            RemoveRecentFile(path);
            return;
        }

        if (!ConfirmDiscardChanges())
        {
            return;
        }

        LoadProjectFromPath(path);
    }

    /// <summary>Vacía la lista de archivos recientes.</summary>
    [RelayCommand]
    private void ClearRecentFiles()
    {
        UserSettings.ClearRecentFiles();
        RecentFiles.Clear();
        OnPropertyChanged(nameof(HasRecentFiles));
    }

    // ---- Nuevo / Abrir / Guardar ---------------------------------------------

    [RelayCommand]
    private void NewDocument()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        var assets = CreateTempAssetStore();
        LoadDocument(ComicDocument.CreateNew("New comic"), assets);
        CurrentFilePath = null;
        DocumentTitle = "New comic";
        IsDirty = false;
    }

    [RelayCommand]
    private void OpenDocument()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Open Project",
            Filter = "ComicLayout Project (*.comicproj)|*.comicproj|All files|*.*"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        LoadProjectFromPath(dialog.FileName);
    }

    /// <summary>
    /// Abre el proyecto <c>.comicproj</c> ubicado en <paramref name="path"/>.
    /// Pensado para la apertura por línea de comandos (doble clic en el archivo).
    /// Confirma descartar cambios pendientes antes de cargar.
    /// </summary>
    public void OpenProjectFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path))
        {
            MessageBox.Show(
                $"File not found:\n{path}",
                "Open Project", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ConfirmDiscardChanges())
        {
            return;
        }

        LoadProjectFromPath(path);
    }

    /// <summary>Carga el paquete desde <paramref name="path"/> sin pedir confirmación.</summary>
    private void LoadProjectFromPath(string path)
    {
        try
        {
            var workingDir = ComicProjectPackage.CreateTempWorkingDirectory();
            var result = ComicProjectPackage.Load(path, workingDir);
            var assets = new AssetStore(result.AssetsDirectory);
            LoadDocument(result.Document, assets);
            CurrentFilePath = path;
            DocumentTitle = result.Document.Title;
            IsDirty = false;
            RegisterRecentFile(path);
            WarnAboutMissingImages(assets);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                $"Could not open the project:\n{ex.Message}",
                "Open Project", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void SaveDocument() => Save();

    [RelayCommand]
    private void SaveDocumentAs() => SaveAs();

    /// <summary>Guarda en la ruta actual (o pide una si no hay). Devuelve si tuvo éxito.</summary>
    public bool Save()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
        {
            return SaveAs();
        }
        return SaveToPath(CurrentFilePath);
    }

    /// <summary>Pide una ruta y guarda. Devuelve si tuvo éxito.</summary>
    public bool SaveAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Project As",
            Filter = "ComicLayout Project (*.comicproj)|*.comicproj",
            DefaultExt = ".comicproj",
            FileName = string.IsNullOrWhiteSpace(DocumentTitle) ? "comic" : DocumentTitle
        };
        if (dialog.ShowDialog() != true)
        {
            return false;
        }
        return SaveToPath(dialog.FileName);
    }

    private bool SaveToPath(string path)
    {
        try
        {
            var document = new ComicDocument
            {
                Title = DocumentTitle,
                Pages = Pages.Select(p => p.Model).ToList()
            };
            ComicProjectPackage.Save(document, _assetStore.AssetsDirectory, path);
            CurrentFilePath = path;
            IsDirty = false;
            RegisterRecentFile(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                $"Could not save the project:\n{ex.Message}",
                "Save Project", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>
    /// Si hay cambios sin guardar, pregunta al usuario. Devuelve <c>false</c> solo
    /// si se debe cancelar la operación en curso (nuevo/abrir/cerrar).
    /// </summary>
    public bool ConfirmDiscardChanges()
    {
        if (!IsDirty)
        {
            return true;
        }

        var answer = MessageBox.Show(
            "You have unsaved changes. Do you want to save them?",
            "ComicLayout Editor", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        return answer switch
        {
            MessageBoxResult.Yes => Save(),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    /// <summary>
    /// Avisa si el proyecto referencia imágenes que no están en la carpeta de
    /// assets (p. ej. un paquete manipulado). Las viñetas afectadas se muestran vacías.
    /// </summary>
    private void WarnAboutMissingImages(AssetStore assets)
    {
        var missing = new List<string>();
        foreach (var page in Pages)
        {
            foreach (var panel in page.Panels)
            {
                var image = panel.Model.Image;
                if (image is not null && !File.Exists(assets.ResolvePath(image.RelativePath)))
                {
                    missing.Add(string.IsNullOrWhiteSpace(image.OriginalFileName)
                        ? image.RelativePath
                        : image.OriginalFileName);
                }
            }
        }

        if (missing.Count == 0)
        {
            return;
        }

        var list = string.Join("\n", missing.Distinct().Take(10));
        MessageBox.Show(
            $"The project was opened, but {missing.Count} image(s) are missing. The affected " +
            $"panels will appear empty:\n\n{list}",
            "Missing Images", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // ---- Impresión y exportación ---------------------------------------------

    private bool HasPages() => Pages.Count > 0;

    [RelayCommand(CanExecute = nameof(HasPages))]
    private void PrintDocument()
    {
        var dialog = new System.Windows.Controls.PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var document = DocumentRenderer.BuildFixedDocument(Pages.ToList());
            dialog.PrintDocument(document.DocumentPaginator, DocumentTitle);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not print:\n{ex.Message}",
                "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand(CanExecute = nameof(HasPages))]
    private void PrintPreview()
    {
        var document = DocumentRenderer.BuildFixedDocument(Pages.ToList());
        var viewer = new System.Windows.Controls.DocumentViewer { Document = document };
        var window = new Window
        {
            Title = $"Print Preview — {DocumentTitle}",
            Width = 900,
            Height = 720,
            Owner = Application.Current?.MainWindow,
            Content = viewer
        };
        window.Show();
    }

    [RelayCommand(CanExecute = nameof(HasPages))]
    private void ExportPdf()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export to PDF",
            Filter = "PDF Document (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = string.IsNullOrWhiteSpace(DocumentTitle) ? "comic" : DocumentTitle
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            DocumentExport.SaveToPdf(Pages.ToList(), dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Could not export the PDF:\n{ex.Message}",
                "Export to PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand(CanExecute = nameof(HasPages))]
    private void ExportPng()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export as PNG",
            Filter = "PNG Image (*.png)|*.png",
            DefaultExt = ".png",
            FileName = string.IsNullOrWhiteSpace(DocumentTitle) ? "comic" : DocumentTitle
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            DocumentExport.SavePagesToPng(Pages.ToList(), dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Could not export the images:\n{ex.Message}",
                "Export as PNG", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---- Gestión de páginas ---------------------------------------------------

    public int CurrentPageIndex => CurrentPage is null ? -1 : Pages.IndexOf(CurrentPage);
    public int PageCount => Pages.Count;
    private bool CanDeletePage() => Pages.Count > 1 && CurrentPage is not null;
    private bool CanMovePageUp() => CurrentPageIndex > 0;
    private bool CanMovePageDown() => CurrentPage is not null && CurrentPageIndex < Pages.Count - 1;

    [RelayCommand]
    private void AddPage()
    {
        var page = new PageViewModel(Page.CreateA4(CurrentPage?.IsLandscape ?? false), _assetStore);
        InsertPage(page, CurrentPageIndex + 1);
    }

    [RelayCommand]
    private void DuplicatePage()
    {
        if (CurrentPage is null)
        {
            return;
        }
        var clone = new PageViewModel(CurrentPage.Model.Clone(), _assetStore);
        InsertPage(clone, CurrentPageIndex + 1);
    }

    private void InsertPage(PageViewModel page, int index)
    {
        var previous = CurrentPage;
        index = Math.Clamp(index, 0, Pages.Count);
        Undo.Do(new DelegateAction(
            redo: () =>
            {
                Pages.Insert(index, page);
                CurrentPage = page;
                RefreshPageCommandStates();
            },
            undo: () =>
            {
                Pages.Remove(page);
                CurrentPage = previous ?? Pages.FirstOrDefault();
                RefreshPageCommandStates();
            }));
    }

    [RelayCommand(CanExecute = nameof(CanDeletePage))]
    private void DeletePage()
    {
        var page = CurrentPage;
        if (page is null || Pages.Count <= 1)
        {
            return;
        }

        var index = Pages.IndexOf(page);
        Undo.Do(new DelegateAction(
            redo: () =>
            {
                Pages.Remove(page);
                CurrentPage = Pages.ElementAtOrDefault(Math.Min(index, Pages.Count - 1));
                RefreshPageCommandStates();
            },
            undo: () =>
            {
                Pages.Insert(Math.Min(index, Pages.Count), page);
                CurrentPage = page;
                RefreshPageCommandStates();
            }));
    }

    [RelayCommand(CanExecute = nameof(CanMovePageUp))]
    private void MovePageUp() => MovePage(-1);

    [RelayCommand(CanExecute = nameof(CanMovePageDown))]
    private void MovePageDown() => MovePage(+1);

    private void MovePage(int delta)
    {
        var page = CurrentPage;
        if (page is null)
        {
            return;
        }
        var from = Pages.IndexOf(page);
        var to = from + delta;
        if (to < 0 || to >= Pages.Count)
        {
            return;
        }
        Undo.Do(new DelegateAction(
            redo: () => { Pages.Move(from, to); CurrentPage = page; RefreshPageCommandStates(); },
            undo: () => { Pages.Move(to, from); CurrentPage = page; RefreshPageCommandStates(); }));
    }

    /// <summary>Alterna la orientación (vertical/horizontal) de la página actual.</summary>
    [RelayCommand]
    private void ToggleOrientation()
    {
        var page = CurrentPage;
        if (page is null)
        {
            return;
        }
        var current = page.SizeMm;
        var swapped = new SizeD(current.Height, current.Width);
        Undo.Do(new DelegateAction(
            redo: () => page.SetSizeMm(swapped),
            undo: () => page.SetSizeMm(current)));
    }

    partial void OnCurrentPageChanged(PageViewModel? value) => RefreshPageCommandStates();

    private void RefreshPageCommandStates()
    {
        OnPropertyChanged(nameof(CurrentPageIndex));
        OnPropertyChanged(nameof(PageCount));
        DeletePageCommand.NotifyCanExecuteChanged();
        MovePageUpCommand.NotifyCanExecuteChanged();
        MovePageDownCommand.NotifyCanExecuteChanged();
        PrintDocumentCommand.NotifyCanExecuteChanged();
        PrintPreviewCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
        ExportPngCommand.NotifyCanExecuteChanged();
        NewBalloonCommand.NotifyCanExecuteChanged();
        NewCaptionCommand.NotifyCanExecuteChanged();
    }

    // ---- Selección ------------------------------------------------------------

    public bool HasSelection => SelectedPanels.Count > 0;
    public bool HasSingleSelection => SelectedPanels.Count == 1;

    /// <summary>Hay viñetas seleccionadas y ningún bocadillo activo.</summary>
    public bool HasPanelSelection => SelectedPanels.Count > 0 && SelectedBalloon is null;

    /// <summary>No hay ni viñetas ni bocadillo seleccionados (contexto de página).</summary>
    public bool IsNothingSelected => SelectedPanels.Count == 0 && SelectedBalloon is null;
    private bool HasMultiSelection => SelectedPanels.Count >= 2;
    private bool CanDistribute => SelectedPanels.Count >= 3;

    /// <summary>Viñeta seleccionada cuando hay exactamente una; si no, <c>null</c>.</summary>
    public PanelViewModel? PrimaryPanel => SelectedPanels.Count == 1 ? SelectedPanels[0] : null;

    private bool CanRemoveImage() => PrimaryPanel?.HasImage == true;

    public void ClearSelection()
    {
        foreach (var panel in SelectedPanels)
        {
            panel.IsSelected = false;
        }
        SelectedPanels.Clear();
        ClearBalloonSelection();
    }

    public void AddToSelection(PanelViewModel panel)
    {
        ClearBalloonSelection();
        if (!SelectedPanels.Contains(panel))
        {
            panel.IsSelected = true;
            SelectedPanels.Add(panel);
        }
    }

    // ---- Selección de bocadillo ----------------------------------------------

    public void SelectBalloon(BalloonViewModel balloon)
    {
        ClearSelection();
        if (SelectedBalloon is { } previous && previous != balloon)
        {
            previous.IsSelected = false;
            previous.IsEditing = false;
        }
        SelectedBalloon = balloon;
        balloon.IsSelected = true;
        RefreshCommandStates();
    }

    public void ClearBalloonSelection()
    {
        if (SelectedBalloon is { } balloon)
        {
            balloon.IsSelected = false;
            balloon.IsEditing = false;
            SelectedBalloon = null;
            RefreshCommandStates();
        }
    }

    public void SelectOnly(PanelViewModel panel)
    {
        ClearSelection();
        AddToSelection(panel);
    }

    public void ToggleSelection(PanelViewModel panel)
    {
        if (SelectedPanels.Contains(panel))
        {
            panel.IsSelected = false;
            SelectedPanels.Remove(panel);
        }
        else
        {
            AddToSelection(panel);
        }
    }

    public void SetSelection(IEnumerable<PanelViewModel> panels)
    {
        ClearSelection();
        foreach (var panel in panels)
        {
            AddToSelection(panel);
        }
    }

    // ---- Crear / eliminar viñetas ---------------------------------------------

    public void CreatePanel(RectD pixelRect)
    {
        var page = CurrentPage;
        if (page is null)
        {
            return;
        }

        var vm = new PanelViewModel(new Panel(), page);
        vm.SetPixelRect(pixelRect);
        vm.ZIndex = page.Panels.Count == 0 ? 0 : page.Panels.Max(p => p.ZIndex) + 1;

        Undo.Do(new DelegateAction(
            redo: () =>
            {
                page.AddPanel(vm);
                SelectOnly(vm);
            },
            undo: () =>
            {
                page.RemovePanel(vm);
                SelectedPanels.Remove(vm);
                vm.IsSelected = false;
            }));
    }

    private bool CanDelete() => HasSelection || SelectedBalloon is not null;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void DeleteSelected()
    {
        if (SelectedBalloon is { } balloon)
        {
            DeleteBalloon(balloon);
            return;
        }

        var page = CurrentPage;
        if (page is null || SelectedPanels.Count == 0)
        {
            return;
        }

        var removed = SelectedPanels.ToList();
        Undo.Do(new DelegateAction(
            redo: () =>
            {
                foreach (var panel in removed)
                {
                    page.RemovePanel(panel);
                }
                ClearSelection();
            },
            undo: () =>
            {
                foreach (var panel in removed)
                {
                    page.AddPanel(panel);
                }
                SetSelection(removed);
            }));
    }

    // ---- Copiar / cortar / pegar ---------------------------------------------

    // El portapapeles es interno a la aplicación (modelos clonados), no el del
    // sistema: así se conserva toda la información (imagen, formato, piquito...).
    private bool CanCopy() => SelectedBalloon is not null || HasSelection;
    private bool CanPaste() => _balloonClipboard is not null || _panelClipboard is { Count: > 0 };

    /// <summary>Copia el bocadillo seleccionado o las viñetas seleccionadas.</summary>
    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Copy()
    {
        if (SelectedBalloon is { } balloon)
        {
            _balloonClipboard = balloon.Model.Clone();
            _panelClipboard = null;
        }
        else if (HasSelection)
        {
            _panelClipboard = SelectedPanels
                .OrderBy(p => p.ZIndex)
                .Select(p => p.Model.Clone())
                .ToList();
            _balloonClipboard = null;
        }
        PasteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Copia y elimina la selección (un solo paso de deshacer: el borrado).</summary>
    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Cut()
    {
        Copy();
        DeleteSelected();
    }

    /// <summary>Pega el contenido del portapapeles desplazado y lo deja seleccionado.</summary>
    [RelayCommand(CanExecute = nameof(CanPaste))]
    private void Paste()
    {
        if (_panelClipboard is { Count: > 0 } panels)
        {
            PastePanels(panels);
        }
        else if (_balloonClipboard is { } balloon)
        {
            PasteBalloon(balloon);
        }
    }

    /// <summary>Desplazamiento del pegado, como fracción de la página/viñeta.</summary>
    private const double PasteOffset = 0.02;

    private void PastePanels(List<Panel> source)
    {
        var page = CurrentPage;
        if (page is null)
        {
            return;
        }

        var baseZ = page.Panels.Count == 0 ? 0 : page.Panels.Max(p => p.ZIndex) + 1;
        var pasted = new List<PanelViewModel>();
        for (var i = 0; i < source.Count; i++)
        {
            // Se clona de nuevo en cada pegado para que cada copia tenga su propio Id
            // y sea independiente del portapapeles (pegados repetidos no se aliasan).
            var model = source[i].Clone();
            model.Bounds = OffsetBounds(model.Bounds);
            model.ZIndex = baseZ + i;
            pasted.Add(new PanelViewModel(model, page));
        }

        Undo.Do(new DelegateAction(
            redo: () =>
            {
                foreach (var p in pasted)
                {
                    page.AddPanel(p);
                }
                SetSelection(pasted);
            },
            undo: () =>
            {
                foreach (var p in pasted)
                {
                    page.RemovePanel(p);
                }
                ClearSelection();
            }));
    }

    private void PasteBalloon(Balloon source)
    {
        // Mismo criterio de destino que al crear un bocadillo nuevo.
        var panel = PrimaryPanel
            ?? SelectedBalloon?.Panel
            ?? CurrentPage?.Panels.OrderByDescending(p => p.ZIndex).FirstOrDefault();
        if (panel is null)
        {
            return;
        }

        var model = source.Clone();
        model.Bounds = OffsetBounds(model.Bounds);
        // El piquito acompaña al bocadillo (mismas coordenadas normalizadas del panel).
        if (model.TailPoint is { } tail)
        {
            model.TailPoint = new PointD(
                Math.Clamp(tail.X + PasteOffset, 0, 1),
                Math.Clamp(tail.Y + PasteOffset, 0, 1));
        }

        var vm = new BalloonViewModel(model, panel);
        Undo.Do(new DelegateAction(
            redo: () => { panel.AddBalloon(vm); SelectBalloon(vm); },
            undo: () => { panel.RemoveBalloon(vm); if (SelectedBalloon == vm) ClearBalloonSelection(); }));
    }

    /// <summary>Desplaza un rectángulo normalizado manteniéndolo dentro de [0,1].</summary>
    private static RectD OffsetBounds(RectD b)
    {
        var x = Math.Clamp(b.X + PasteOffset, 0, Math.Max(0, 1 - b.Width));
        var y = Math.Clamp(b.Y + PasteOffset, 0, Math.Max(0, 1 - b.Height));
        return new RectD(x, y, b.Width, b.Height);
    }

    // ---- Orden Z --------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void BringToFront() => ApplyZOrder(toFront: true);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SendToBack() => ApplyZOrder(toFront: false);

    private void ApplyZOrder(bool toFront)
    {
        var page = CurrentPage;
        if (page is null || SelectedPanels.Count == 0)
        {
            return;
        }

        var oldZ = page.Panels.ToDictionary(p => p, p => p.ZIndex);
        var selected = SelectedPanels.ToList();

        if (toFront)
        {
            var start = page.Panels.Max(p => p.ZIndex) + 1;
            for (int i = 0; i < selected.Count; i++)
            {
                selected[i].ZIndex = start + i;
            }
        }
        else
        {
            var start = page.Panels.Min(p => p.ZIndex) - selected.Count;
            for (int i = 0; i < selected.Count; i++)
            {
                selected[i].ZIndex = start + i;
            }
        }

        var newZ = selected.ToDictionary(p => p, p => p.ZIndex);
        Undo.Push(new DelegateAction(
            redo: () => { foreach (var kv in newZ) kv.Key.ZIndex = kv.Value; },
            undo: () => { foreach (var kv in oldZ) kv.Key.ZIndex = kv.Value; }));
    }

    // ---- Alineación y distribución -------------------------------------------

    [RelayCommand(CanExecute = nameof(HasMultiSelection))]
    private void AlignLeft() => AlignEach(p => new RectD(SelectionBounds().X, p.Top, p.Width, p.Height));

    [RelayCommand(CanExecute = nameof(HasMultiSelection))]
    private void AlignRight() => AlignEach(p => new RectD(SelectionBounds().Right - p.Width, p.Top, p.Width, p.Height));

    [RelayCommand(CanExecute = nameof(HasMultiSelection))]
    private void AlignTop() => AlignEach(p => new RectD(p.Left, SelectionBounds().Y, p.Width, p.Height));

    [RelayCommand(CanExecute = nameof(HasMultiSelection))]
    private void AlignBottom() => AlignEach(p => new RectD(p.Left, SelectionBounds().Bottom - p.Height, p.Width, p.Height));

    [RelayCommand(CanExecute = nameof(HasMultiSelection))]
    private void AlignCenterHorizontal()
    {
        var center = SelectionBounds().X + SelectionBounds().Width / 2;
        AlignEach(p => new RectD(center - p.Width / 2, p.Top, p.Width, p.Height));
    }

    [RelayCommand(CanExecute = nameof(HasMultiSelection))]
    private void AlignMiddleVertical()
    {
        var middle = SelectionBounds().Y + SelectionBounds().Height / 2;
        AlignEach(p => new RectD(p.Left, middle - p.Height / 2, p.Width, p.Height));
    }

    [RelayCommand(CanExecute = nameof(CanDistribute))]
    private void DistributeHorizontal()
    {
        var ordered = SelectedPanels.OrderBy(p => p.Left).ToList();
        var left = ordered.First().Left;
        var right = ordered.Last().Left + ordered.Last().Width;
        var totalWidth = ordered.Sum(p => p.Width);
        var gap = (right - left - totalWidth) / (ordered.Count - 1);

        var newRects = new Dictionary<PanelViewModel, RectD>();
        var x = left;
        foreach (var p in ordered)
        {
            newRects[p] = new RectD(x, p.Top, p.Width, p.Height);
            x += p.Width + gap;
        }
        ApplyRects(newRects);
    }

    [RelayCommand(CanExecute = nameof(CanDistribute))]
    private void DistributeVertical()
    {
        var ordered = SelectedPanels.OrderBy(p => p.Top).ToList();
        var top = ordered.First().Top;
        var bottom = ordered.Last().Top + ordered.Last().Height;
        var totalHeight = ordered.Sum(p => p.Height);
        var gap = (bottom - top - totalHeight) / (ordered.Count - 1);

        var newRects = new Dictionary<PanelViewModel, RectD>();
        var y = top;
        foreach (var p in ordered)
        {
            newRects[p] = new RectD(p.Left, y, p.Width, p.Height);
            y += p.Height + gap;
        }
        ApplyRects(newRects);
    }

    private RectD SelectionBounds()
    {
        var left = SelectedPanels.Min(p => p.Left);
        var top = SelectedPanels.Min(p => p.Top);
        var right = SelectedPanels.Max(p => p.Left + p.Width);
        var bottom = SelectedPanels.Max(p => p.Top + p.Height);
        return RectD.FromLtrb(left, top, right, bottom);
    }

    private void AlignEach(Func<PanelViewModel, RectD> compute)
        => ApplyRects(SelectedPanels.ToDictionary(p => p, compute));

    private void ApplyRects(Dictionary<PanelViewModel, RectD> newRects)
    {
        var oldRects = newRects.Keys.ToDictionary(p => p, p => p.PixelRect);
        Undo.Do(new DelegateAction(
            redo: () => { foreach (var kv in newRects) kv.Key.SetPixelRect(kv.Value); },
            undo: () => { foreach (var kv in oldRects) kv.Key.SetPixelRect(kv.Value); }));
    }

    // ---- Movimiento interactivo (drag/resize) --------------------------------

    /// <summary>Captura el estado inicial de un conjunto de viñetas antes de arrastrar.</summary>
    public void BeginInteractiveChange(IEnumerable<PanelViewModel> panels)
        => _interactiveStart = panels.ToDictionary(p => p, p => p.PixelRect);

    /// <summary>Cierra la interacción y registra un paso de deshacer si hubo cambios.</summary>
    public void EndInteractiveChange()
    {
        if (_interactiveStart is null)
        {
            return;
        }

        var changed = _interactiveStart
            .Where(kv => !ApproxEqual(kv.Value, kv.Key.PixelRect))
            .ToList();
        _interactiveStart = null;

        if (changed.Count == 0)
        {
            return;
        }

        var oldRects = changed.ToDictionary(kv => kv.Key, kv => kv.Value);
        var newRects = changed.ToDictionary(kv => kv.Key, kv => kv.Key.PixelRect);
        Undo.Push(new DelegateAction(
            redo: () => { foreach (var kv in newRects) kv.Key.SetPixelRect(kv.Value); },
            undo: () => { foreach (var kv in oldRects) kv.Key.SetPixelRect(kv.Value); }));
    }

    /// <summary>Mueve todas las viñetas seleccionadas por un delta en píxeles.</summary>
    public void MoveSelectedBy(double dx, double dy)
    {
        var page = CurrentPage;
        if (page is null)
        {
            return;
        }

        foreach (var panel in SelectedPanels)
        {
            var left = Math.Clamp(panel.Left + dx, 0, Math.Max(0, page.PageWidthPx - panel.Width));
            var top = Math.Clamp(panel.Top + dy, 0, Math.Max(0, page.PageHeightPx - panel.Height));
            panel.Left = left;
            panel.Top = top;
        }

        if (SnapToGrid && SelectedPanels.Count == 1)
        {
            var p = SelectedPanels[0];
            p.Left = Snap(p.Left);
            p.Top = Snap(p.Top);
        }
    }

    public double Snap(double value)
    {
        if (!SnapToGrid || GridSizePx <= 0)
        {
            return value;
        }
        return Math.Round(value / GridSizePx) * GridSizePx;
    }

    public double MinPanelSize => MinPanelSizePx;

    // ---- Bocadillos -----------------------------------------------------------

    // Los bocadillos y cartelas no dependen de la selección: basta con que haya
    // una página con al menos una viñeta que los aloje.
    private bool CanAddBalloon() => CurrentPage?.Panels.Count > 0;

    [RelayCommand(CanExecute = nameof(CanAddBalloon))]
    private void NewBalloon()
        => AddBalloon(new Balloon
        {
            Bounds = new RectD(0.25, 0.2, 0.5, 0.35),
            Text = "Texto...",
            TailPoint = new PointD(0.5, 0.72)
        });

    [RelayCommand(CanExecute = nameof(CanAddBalloon))]
    private void NewCaption()
        => AddBalloon(new Balloon
        {
            Bounds = new RectD(0.2, 0.05, 0.6, 0.16),
            Shape = BalloonShape.Caption,
            Text = "Narración...",
            TailPoint = null
        });

    private void AddBalloon(Balloon model)
    {
        // Si no hay viñeta seleccionada, se aloja en la viñeta frontal de la página.
        var panel = PrimaryPanel
            ?? SelectedBalloon?.Panel
            ?? CurrentPage?.Panels.OrderByDescending(p => p.ZIndex).FirstOrDefault();
        if (panel is null)
        {
            return;
        }

        var vm = new BalloonViewModel(model, panel);

        Undo.Do(new DelegateAction(
            redo: () => { panel.AddBalloon(vm); SelectBalloon(vm); },
            undo: () => { panel.RemoveBalloon(vm); if (SelectedBalloon == vm) ClearBalloonSelection(); }));
    }

    private void DeleteBalloon(BalloonViewModel balloon)
    {
        var panel = balloon.Panel;
        Undo.Do(new DelegateAction(
            redo: () => { panel.RemoveBalloon(balloon); if (SelectedBalloon == balloon) ClearBalloonSelection(); },
            undo: () => { panel.AddBalloon(balloon); SelectBalloon(balloon); }));
    }

    // Movimiento / redimensionado / piquito interactivos del bocadillo.

    public void BeginBalloonChange(BalloonViewModel balloon)
        => _balloonStart = (balloon, balloon.PixelRect, balloon.Model.TailPoint);

    public void EndBalloonChange()
    {
        if (_balloonStart is not { } start)
        {
            return;
        }
        _balloonStart = null;

        var balloon = start.Balloon;
        var newRect = balloon.PixelRect;
        var newTail = balloon.Model.TailPoint;
        var rectSame = Math.Abs(newRect.X - start.Rect.X) < Epsilon
                       && Math.Abs(newRect.Y - start.Rect.Y) < Epsilon
                       && Math.Abs(newRect.Width - start.Rect.Width) < Epsilon
                       && Math.Abs(newRect.Height - start.Rect.Height) < Epsilon;
        var tailSame = Nullable.Equals(newTail, start.Tail);
        if (rectSame && tailSame)
        {
            return;
        }

        var oldRect = start.Rect;
        var oldTail = start.Tail;
        Undo.Push(new DelegateAction(
            redo: () => { balloon.SetPixelRect(newRect); balloon.SetTail(newTail); },
            undo: () => { balloon.SetPixelRect(oldRect); balloon.SetTail(oldTail); }));
    }

    public void MoveBalloonBy(BalloonViewModel balloon, double dx, double dy)
    {
        // El bocadillo puede salirse de la viñeta (o quedar a caballo de su borde),
        // pero se mantiene dentro de la página para no perderlo fuera del lienzo.
        var (minX, minY, maxX, maxY) = PageBoundsInPanelSpace(balloon.Panel);
        balloon.Left = Math.Clamp(balloon.Left + dx, minX, Math.Max(minX, maxX - balloon.Width));
        balloon.Top = Math.Clamp(balloon.Top + dy, minY, Math.Max(minY, maxY - balloon.Height));
    }

    /// <summary>
    /// Límites de la página expresados en las coordenadas locales de la viñeta
    /// (los bocadillos se posicionan relativos a su viñeta). Permite colocarlos
    /// fuera del marco pero sin salir de la página.
    /// </summary>
    public static (double MinX, double MinY, double MaxX, double MaxY) PageBoundsInPanelSpace(PanelViewModel panel)
        => (-panel.Left, -panel.Top, panel.PageWidthPx - panel.Left, panel.PageHeightPx - panel.Top);

    /// <summary>
    /// Registra un cambio de contenido con formato del bocadillo tras la edición inline.
    /// <paramref name="oldRuns"/> es la instantánea previa de <see cref="BalloonViewModel.Runs"/>.
    /// </summary>
    public void CommitBalloonRichText(BalloonViewModel balloon, IReadOnlyList<TextRun> oldRuns)
    {
        var newRuns = balloon.Runs.Select(r => r.Clone()).ToList();
        if (RunsEqual(oldRuns, newRuns))
        {
            return;
        }
        var snapshot = oldRuns.Select(r => r.Clone()).ToList();
        Undo.Push(new DelegateAction(
            redo: () => balloon.SetRichText(newRuns),
            undo: () => balloon.SetRichText(snapshot)));
    }

    private static bool RunsEqual(IReadOnlyList<TextRun> a, IReadOnlyList<TextRun> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (var i = 0; i < a.Count; i++)
        {
            var x = a[i];
            var y = b[i];
            if (x.Text != y.Text || x.Bold != y.Bold || x.Italic != y.Italic || x.Underline != y.Underline
                || x.FontFamily != y.FontFamily || !Nullable.Equals(x.FontSize, y.FontSize) || x.Color != y.Color)
            {
                return false;
            }
        }
        return true;
    }

    // ---- Imágenes -------------------------------------------------------------

    public Array ImageFits { get; } = Enum.GetValues(typeof(ImageFit));

    /// <summary>Ajuste de la imagen de la viñeta seleccionada (enlazado a la UI).</summary>
    public ImageFit SelectedImageFit
    {
        get => PrimaryPanel?.Model.ImageFit ?? ImageFit.Cover;
        set
        {
            var panel = PrimaryPanel;
            if (panel is null || panel.Model.ImageFit == value)
            {
                return;
            }

            var old = panel.Model.ImageFit;
            Undo.Do(new DelegateAction(
                redo: () => { panel.SetImageFit(value); OnPropertyChanged(nameof(SelectedImageFit)); },
                undo: () => { panel.SetImageFit(old); OnPropertyChanged(nameof(SelectedImageFit)); }));
        }
    }

    [RelayCommand(CanExecute = nameof(HasSingleSelection))]
    private void ImportImage()
    {
        var panel = PrimaryPanel;
        if (panel is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import Image",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            ImportImageToPanel(panel, dialog.FileName);
        }
    }

    /// <summary>Importa un archivo de imagen a una viñeta concreta (con deshacer).</summary>
    public void ImportImageToPanel(PanelViewModel panel, string sourceFile)
    {
        ImageRef imported;
        try
        {
            imported = _assetStore.Import(sourceFile);
        }
        catch (IOException)
        {
            return;
        }

        var oldImage = panel.Model.Image;
        var oldRotation = panel.Model.ImageRotation;
        // Endereza automáticamente según la orientación EXIF de la foto original.
        var newRotation = ExifOrientation.GetRotationDegrees(sourceFile);

        Undo.Do(new DelegateAction(
            redo: () =>
            {
                panel.SetImage(imported);
                panel.SetImageRotation(newRotation);
                RefreshCommandStates();
            },
            undo: () =>
            {
                panel.SetImage(oldImage);
                panel.SetImageRotation(oldRotation);
                RefreshCommandStates();
            }));
    }

    [RelayCommand(CanExecute = nameof(CanRemoveImage))]
    private void RemoveImage()
    {
        var panel = PrimaryPanel;
        if (panel?.Model.Image is null)
        {
            return;
        }

        var old = panel.Model.Image;
        Undo.Do(new DelegateAction(
            redo: () => { panel.SetImage(null); RefreshCommandStates(); },
            undo: () => { panel.SetImage(old); RefreshCommandStates(); }));
    }

    private bool CanRotateImage() => PrimaryPanel?.HasImage == true;

    /// <summary>Rota la imagen de la viñeta 90° a izquierda o derecha (con deshacer).</summary>
    [RelayCommand(CanExecute = nameof(CanRotateImage))]
    private void RotateImage(string direction)
    {
        var panel = PrimaryPanel;
        if (panel?.Model.Image is null)
        {
            return;
        }

        var delta = direction == "Left" ? -90 : 90;
        var old = panel.Model.ImageRotation;

        Undo.Do(new DelegateAction(
            redo: () => { panel.SetImageRotation(old + delta); RefreshCommandStates(); },
            undo: () => { panel.SetImageRotation(old); RefreshCommandStates(); }));
    }

    // Pan/zoom interno de la imagen dentro del marco.

    public void BeginImageAdjust(PanelViewModel panel)
        => _imageAdjustStart = (panel, panel.Model.ImageZoom, panel.Model.ImageOffset);

    public void EndImageAdjust()
    {
        if (_imageAdjustStart is not { } start)
        {
            return;
        }
        _imageAdjustStart = null;

        var panel = start.Panel;
        var newZoom = panel.Model.ImageZoom;
        var newOffset = panel.Model.ImageOffset;
        if (Math.Abs(newZoom - start.Zoom) < Epsilon
            && Math.Abs(newOffset.X - start.Offset.X) < 0.0001
            && Math.Abs(newOffset.Y - start.Offset.Y) < 0.0001)
        {
            return;
        }

        Undo.Push(new DelegateAction(
            redo: () => panel.SetImageTransform(newZoom, newOffset),
            undo: () => panel.SetImageTransform(start.Zoom, start.Offset)));
    }

    public void PanImage(PanelViewModel panel, double dxPx, double dyPx)
    {
        var offset = panel.Model.ImageOffset;
        var nx = offset.X + dxPx / Math.Max(1, panel.Width);
        var ny = offset.Y + dyPx / Math.Max(1, panel.Height);
        panel.SetImageTransform(panel.Model.ImageZoom, new PointD(nx, ny));
    }

    /// <summary>Aplica un factor de zoom a la imagen y lo registra como paso de deshacer.</summary>
    public void ZoomImage(PanelViewModel panel, double factor)
    {
        var oldZoom = panel.Model.ImageZoom;
        var oldOffset = panel.Model.ImageOffset;
        var newZoom = Math.Clamp(oldZoom * factor, 0.1, 10.0);
        if (Math.Abs(newZoom - oldZoom) < Epsilon)
        {
            return;
        }

        panel.SetImageTransform(newZoom, oldOffset);
        Undo.Push(new DelegateAction(
            redo: () => panel.SetImageTransform(newZoom, oldOffset),
            undo: () => panel.SetImageTransform(oldZoom, oldOffset)));
    }

    // ---- Deshacer / rehacer ---------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void UndoAction() => Undo.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void RedoAction() => Undo.Redo();

    private bool CanUndo() => Undo.CanUndo;
    private bool CanRedo() => Undo.CanRedo;

    partial void OnSelectedBalloonChanged(BalloonViewModel? value)
    {
        OnPropertyChanged(nameof(HasPanelSelection));
        OnPropertyChanged(nameof(IsNothingSelected));
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasSingleSelection));
        OnPropertyChanged(nameof(HasPanelSelection));
        OnPropertyChanged(nameof(IsNothingSelected));
        OnPropertyChanged(nameof(PrimaryPanel));
        OnPropertyChanged(nameof(SelectedImageFit));
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CutCommand.NotifyCanExecuteChanged();
        NewBalloonCommand.NotifyCanExecuteChanged();
        NewCaptionCommand.NotifyCanExecuteChanged();
        ImportImageCommand.NotifyCanExecuteChanged();
        RemoveImageCommand.NotifyCanExecuteChanged();
        RotateImageCommand.NotifyCanExecuteChanged();
        BringToFrontCommand.NotifyCanExecuteChanged();
        SendToBackCommand.NotifyCanExecuteChanged();
        AlignLeftCommand.NotifyCanExecuteChanged();
        AlignRightCommand.NotifyCanExecuteChanged();
        AlignTopCommand.NotifyCanExecuteChanged();
        AlignBottomCommand.NotifyCanExecuteChanged();
        AlignCenterHorizontalCommand.NotifyCanExecuteChanged();
        AlignMiddleVerticalCommand.NotifyCanExecuteChanged();
        DistributeHorizontalCommand.NotifyCanExecuteChanged();
        DistributeVerticalCommand.NotifyCanExecuteChanged();
        UndoActionCommand.NotifyCanExecuteChanged();
        RedoActionCommand.NotifyCanExecuteChanged();
    }

    private static bool ApproxEqual(RectD a, RectD b)
        => Math.Abs(a.X - b.X) < Epsilon
           && Math.Abs(a.Y - b.Y) < Epsilon
           && Math.Abs(a.Width - b.Width) < Epsilon
           && Math.Abs(a.Height - b.Height) < Epsilon;
}

/// <summary>Entrada del menú de archivos recientes: ruta completa y nombre para mostrar.</summary>
public sealed class RecentFileEntry
{
    public RecentFileEntry(string path) => Path = path;

    /// <summary>Ruta completa del proyecto <c>.comicproj</c>.</summary>
    public string Path { get; }

    /// <summary>Nombre del archivo (sin la carpeta), para el texto del menú.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);
}
