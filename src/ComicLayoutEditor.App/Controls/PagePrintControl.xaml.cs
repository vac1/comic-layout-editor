using System.Windows.Controls;

namespace ComicLayoutEditor.App.Controls;

/// <summary>
/// Render no interactivo de una página, usado para imprimir, exportar (PDF/PNG)
/// y la vista previa de impresión. Su <c>DataContext</c> es un <c>PageViewModel</c>.
/// </summary>
public partial class PagePrintControl : UserControl
{
    public PagePrintControl()
    {
        InitializeComponent();
    }
}
