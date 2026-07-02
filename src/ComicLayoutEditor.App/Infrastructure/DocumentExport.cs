using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using ComicLayoutEditor.App.ViewModels;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace ComicLayoutEditor.App.Infrastructure;

/// <summary>
/// Exportación del documento a PDF y a imágenes PNG. Cada página se rasteriza a
/// alta resolución y se coloca a tamaño real (A4 u orientación configurada).
/// </summary>
public static class DocumentExport
{
    /// <summary>Resolución por defecto de exportación (puntos por pulgada).</summary>
    public const double DefaultDpi = 200;

    /// <summary>
    /// Exporta todas las páginas a un único PDF, una página por hoja, a su
    /// tamaño real en milímetros.
    /// </summary>
    public static void SaveToPdf(IReadOnlyList<PageViewModel> pages, string path, double dpi = DefaultDpi)
    {
        using var pdf = new PdfDocument();
        var streams = new List<MemoryStream>();
        try
        {
            foreach (var page in pages)
            {
                var stream = EncodePng(DocumentRenderer.RenderToBitmap(page, dpi));
                streams.Add(stream);

                var image = XImage.FromStream(stream);
                var pdfPage = pdf.AddPage();
                pdfPage.Width = XUnit.FromMillimeter(page.SizeMm.Width);
                pdfPage.Height = XUnit.FromMillimeter(page.SizeMm.Height);

                using var gfx = XGraphics.FromPdfPage(pdfPage);
                gfx.DrawImage(image, 0, 0, pdfPage.Width.Point, pdfPage.Height.Point);
            }
            pdf.Save(path);
        }
        finally
        {
            foreach (var stream in streams)
            {
                stream.Dispose();
            }
        }
    }

    /// <summary>
    /// Exporta las páginas como PNG. Con una sola página usa <paramref name="basePath"/>;
    /// con varias, añade un sufijo numérico (<c>_01</c>, <c>_02</c>, ...).
    /// </summary>
    public static void SavePagesToPng(IReadOnlyList<PageViewModel> pages, string basePath, double dpi = DefaultDpi)
    {
        for (var i = 0; i < pages.Count; i++)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(DocumentRenderer.RenderToBitmap(pages[i], dpi)));

            var path = pages.Count == 1 ? basePath : InsertIndex(basePath, i + 1);
            using var file = File.Create(path);
            encoder.Save(file);
        }
    }

    private static MemoryStream EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        return stream;
    }

    private static string InsertIndex(string path, int index)
    {
        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        return Path.Combine(dir, $"{name}_{index:D2}{ext}");
    }
}
