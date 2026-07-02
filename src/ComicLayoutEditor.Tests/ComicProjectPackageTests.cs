using System.IO.Compression;
using ComicLayoutEditor.Core.Models;
using ComicLayoutEditor.Core.Serialization;

namespace ComicLayoutEditor.Tests;

public class ComicProjectPackageTests : IDisposable
{
    private readonly string _root;

    public ComicProjectPackageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ComicLayoutEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    private string Sub(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void SaveThenLoad_RoundTripsDocumentAndImage()
    {
        var doc = TestDocuments.CreateRich("img_0001.png");

        // Preparar la carpeta de assets de origen con un "imagen" de prueba.
        var assetsSource = Sub("work-assets");
        var imageBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        File.WriteAllBytes(Path.Combine(assetsSource, "img_0001.png"), imageBytes);

        var projectPath = Path.Combine(_root, "proyecto.comicproj");
        ComicProjectPackage.Save(doc, assetsSource, projectPath);

        Assert.True(File.Exists(projectPath));

        var extractDir = Sub("extract");
        var result = ComicProjectPackage.Load(projectPath, extractDir);

        // El documento coincide (comparando JSON).
        Assert.Equal(ComicJson.Serialize(doc), ComicJson.Serialize(result.Document));

        // La imagen se extrajo con los mismos bytes.
        var extractedImage = Path.Combine(result.AssetsDirectory, "img_0001.png");
        Assert.True(File.Exists(extractedImage));
        Assert.Equal(imageBytes, File.ReadAllBytes(extractedImage));
    }

    [Fact]
    public void Save_DeduplicatesSharedImage()
    {
        // Dos viñetas apuntando a la misma imagen no deben duplicar la entrada.
        var doc = ComicDocument.CreateNew();
        var shared = new ImageRef { RelativePath = "shared.png", OriginalFileName = "s.png" };
        doc.Pages[0].Panels.Add(new Panel { Image = shared });
        doc.Pages[0].Panels.Add(new Panel { Image = shared });

        var assetsSource = Sub("work-assets");
        File.WriteAllBytes(Path.Combine(assetsSource, "shared.png"), new byte[] { 42 });

        var projectPath = Path.Combine(_root, "dup.comicproj");
        ComicProjectPackage.Save(doc, assetsSource, projectPath);

        using var archive = ZipFile.OpenRead(projectPath);
        var imageEntries = archive.Entries.Count(e => e.FullName == "assets/shared.png");
        Assert.Equal(1, imageEntries);
    }

    [Fact]
    public void Save_MissingImage_Throws()
    {
        var doc = TestDocuments.CreateRich("no_existe.png");
        var assetsSource = Sub("empty-assets"); // sin la imagen

        var projectPath = Path.Combine(_root, "fallo.comicproj");

        Assert.Throws<FileNotFoundException>(
            () => ComicProjectPackage.Save(doc, assetsSource, projectPath));

        // No debe quedar un archivo destino corrupto.
        Assert.False(File.Exists(projectPath));
    }

    [Fact]
    public void Load_InvalidPackage_Throws()
    {
        // Un ZIP sin manifest.json no es un proyecto válido.
        var badZip = Path.Combine(_root, "malo.comicproj");
        using (var zip = ZipFile.Open(badZip, ZipArchiveMode.Create))
        {
            zip.CreateEntry("otro.txt");
        }

        var extractDir = Sub("extract-bad");
        Assert.Throws<InvalidDataException>(() => ComicProjectPackage.Load(badZip, extractDir));
    }
}
