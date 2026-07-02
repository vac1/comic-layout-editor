using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.Tests;

/// <summary>
/// Fábricas de documentos de ejemplo para los tests.
/// </summary>
internal static class TestDocuments
{
    /// <summary>
    /// Documento con dos páginas, viñetas, una imagen y bocadillos, cubriendo
    /// campos nulos y no nulos (piquito, imagen, etc.).
    /// </summary>
    public static ComicDocument CreateRich(string imageRelativePath = "img_0001.png")
    {
        var doc = new ComicDocument { Title = "Mi historieta", SchemaVersion = 1 };

        var page1 = Page.CreateA4();
        var panelWithImage = new Panel
        {
            Bounds = new RectD(0.05, 0.05, 0.4, 0.3),
            Rotation = 2.5,
            ZIndex = 1,
            Image = new ImageRef { RelativePath = imageRelativePath, OriginalFileName = "foto.png" },
            ImageFit = ImageFit.Contain,
            ImageZoom = 1.75,
            ImageOffset = new PointD(-0.1, 0.2),
            Balloons =
            {
                new Balloon
                {
                    Bounds = new RectD(0.1, 0.1, 0.5, 0.25),
                    Shape = BalloonShape.Thought,
                    Text = "¡Hola, mundo!",
                    FontFamily = "Comic Sans MS",
                    FontSize = 18,
                    TextAlign = TextAlign.Left,
                    TailPoint = new PointD(0.3, 0.9)
                }
            }
        };
        var emptyPanel = new Panel
        {
            Bounds = new RectD(0.5, 0.5, 0.45, 0.4),
            ZIndex = 2
            // sin imagen, sin bocadillos, sin piquito → cubre los casos null
        };
        page1.Panels.Add(panelWithImage);
        page1.Panels.Add(emptyPanel);

        var page2 = Page.CreateA4(landscape: true);

        doc.Pages.Add(page1);
        doc.Pages.Add(page2);
        return doc;
    }
}
