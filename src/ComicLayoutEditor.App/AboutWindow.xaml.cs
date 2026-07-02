using System.Reflection;
using System.Windows;

namespace ComicLayoutEditor.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"Version {version?.ToString(3) ?? "1.0.0"}";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
