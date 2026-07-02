using System.Windows;
using ComicLayoutEditor.App.ViewModels;

namespace ComicLayoutEditor.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var window = new MainWindow();
        window.Show();

        // Si se invoca con la ruta de un .comicproj (doble clic en el Explorador
        // o "Abrir con..."), Windows pasa la ruta como primer argumento.
        var path = e.Args.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
        if (path is not null && window.DataContext is MainWindowViewModel vm)
        {
            vm.OpenProjectFile(path);
        }
    }
}
