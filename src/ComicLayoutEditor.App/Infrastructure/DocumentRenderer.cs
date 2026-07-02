using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ComicLayoutEditor.App.Controls;
using ComicLayoutEditor.App.ViewModels;

namespace ComicLayoutEditor.App.Infrastructure;

/// <summary>
/// Construye representaciones no interactivas de las páginas para imprimir,
/// exportar y previsualizar. Un píxel de escala base equivale a un DIU (1/96")
/// y la página se dimensiona en mm, por lo que el resultado sale a tamaño real.
/// </summary>
public static class DocumentRenderer
{
    /// <summary>Crea un elemento visual, medido y dispuesto, para una página.</summary>
    public static FrameworkElement CreatePageElement(PageViewModel page)
    {
        var element = new PagePrintControl
        {
            DataContext = page,
            Width = page.PageWidthPx,
            Height = page.PageHeightPx
        };
        var size = new Size(page.PageWidthPx, page.PageHeightPx);
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();
        return element;
    }

    /// <summary>Rasteriza una página a un mapa de bits a la resolución indicada (DPI).</summary>
    public static RenderTargetBitmap RenderToBitmap(PageViewModel page, double dpi)
    {
        var element = CreatePageElement(page);
        var pixelWidth = (int)Math.Ceiling(page.PageWidthPx * dpi / 96.0);
        var pixelHeight = (int)Math.Ceiling(page.PageHeightPx * dpi / 96.0);
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(element);
        return bitmap;
    }

    /// <summary>
    /// Construye un <see cref="FixedDocument"/> con una página física por cada
    /// página del documento, a su tamaño real (para imprimir o previsualizar).
    /// </summary>
    public static FixedDocument BuildFixedDocument(IReadOnlyList<PageViewModel> pages)
    {
        var document = new FixedDocument();
        foreach (var page in pages)
        {
            var element = CreatePageElement(page);
            var fixedPage = new FixedPage
            {
                Width = page.PageWidthPx,
                Height = page.PageHeightPx
            };
            FixedPage.SetLeft(element, 0);
            FixedPage.SetTop(element, 0);
            fixedPage.Children.Add(element);

            var size = new Size(page.PageWidthPx, page.PageHeightPx);
            fixedPage.Measure(size);
            fixedPage.Arrange(new Rect(size));
            fixedPage.UpdateLayout();

            var content = new PageContent();
            ((IAddChild)content).AddChild(fixedPage);
            document.Pages.Add(content);
        }

        if (pages.Count > 0)
        {
            document.DocumentPaginator.PageSize = new Size(pages[0].PageWidthPx, pages[0].PageHeightPx);
        }
        return document;
    }
}
