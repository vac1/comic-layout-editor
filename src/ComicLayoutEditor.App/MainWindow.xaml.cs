using System.ComponentModel;
using System.Windows;
using ComicLayoutEditor.App.Infrastructure;
using ComicLayoutEditor.App.ViewModels;

namespace ComicLayoutEditor.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RestoreSession();
    }

    // ---- Persistencia de la sesión (ventana + ajustes de editor) --------------

    private void RestoreSession()
    {
        var settings = UserSettings.Load();

        if (DataContext is MainWindowViewModel vm)
        {
            vm.ApplyEditorSettings(settings);
        }

        ApplyWindowPlacement(settings);
    }

    private void ApplyWindowPlacement(UserSettings s)
    {
        if (s.WindowLeft is { } left && s.WindowTop is { } top &&
            s.WindowWidth is > 0 && s.WindowHeight is > 0 &&
            IsOnScreen(left, top, s.WindowWidth.Value, s.WindowHeight.Value))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
            Width = s.WindowWidth.Value;
            Height = s.WindowHeight.Value;
        }

        if (s.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>Comprueba que el rectángulo guardado sigue siendo visible en algún monitor.</summary>
    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        var window = new Rect(left, top, width, height);
        window.Intersect(virtualScreen);

        // Exige una zona visible mínima para no restaurar la ventana casi fuera de pantalla
        // (p. ej. si se desconectó el monitor donde estaba).
        return window.Width >= 100 && window.Height >= 100;
    }

    private void SaveSession()
    {
        // Se recargan las preferencias para preservar cualquier clave que no gestione la ventana.
        var settings = UserSettings.Load();

        settings.WindowMaximized = WindowState == WindowState.Maximized;

        // Si está maximizada/minimizada, RestoreBounds da el tamaño "normal" a restaurar.
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;
        settings.WindowLeft = bounds.Left;
        settings.WindowTop = bounds.Top;
        settings.WindowWidth = bounds.Width;
        settings.WindowHeight = bounds.Height;

        if (DataContext is MainWindowViewModel vm)
        {
            vm.CaptureEditorSettings(settings);
        }

        settings.Save();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && !vm.ConfirmDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        SaveSession();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }
}
