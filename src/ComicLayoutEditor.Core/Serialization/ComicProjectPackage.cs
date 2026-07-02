using System.IO.Compression;
using System.Text;
using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.Core.Serialization;

/// <summary>
/// Resultado de cargar un paquete <c>.comicproj</c>.
/// </summary>
/// <param name="Document">Documento deserializado desde <c>manifest.json</c>.</param>
/// <param name="WorkingDirectory">Carpeta donde se extrajo el paquete.</param>
/// <param name="AssetsDirectory">
/// Carpeta <c>assets/</c> dentro de <see cref="WorkingDirectory"/>; las rutas
/// <see cref="ImageRef.RelativePath"/> se resuelven relativas a esta carpeta.
/// </param>
public readonly record struct ComicProjectLoadResult(
    ComicDocument Document,
    string WorkingDirectory,
    string AssetsDirectory);

/// <summary>
/// Lectura/escritura del paquete de proyecto <c>.comicproj</c>, un ZIP que contiene
/// <c>manifest.json</c> (la serialización de <see cref="ComicDocument"/>) y una
/// carpeta <c>assets/</c> con las imágenes referenciadas.
/// </summary>
public static class ComicProjectPackage
{
    public const string ManifestEntryName = "manifest.json";
    public const string AssetsFolderName = "assets";

    /// <summary>
    /// Guarda <paramref name="document"/> en <paramref name="destinationPath"/>.
    /// Las imágenes referenciadas se leen desde
    /// <paramref name="assetsSourceDirectory"/> (donde <see cref="ImageRef.RelativePath"/>
    /// se resuelve relativo a esa carpeta) y se copian dentro del ZIP en <c>assets/</c>.
    /// </summary>
    public static void Save(ComicDocument document, string assetsSourceDirectory, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(document);

        var destDir = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // Escribir a un archivo temporal y luego mover, para no corromper el
        // destino si algo falla a mitad de la escritura.
        var tempPath = destinationPath + ".tmp";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        try
        {
            using (var zipStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                using (var writer = new StreamWriter(manifestEntry.Open(), new UTF8Encoding(false)))
                {
                    writer.Write(ComicJson.Serialize(document));
                }

                var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var image in document.EnumerateImageRefs())
                {
                    var relative = NormalizeRelative(image.RelativePath);
                    if (relative.Length == 0 || !written.Add(relative))
                    {
                        continue; // vacío o ya añadido (misma imagen en varias viñetas)
                    }

                    var source = Path.Combine(assetsSourceDirectory, relative);
                    if (!File.Exists(source))
                    {
                        throw new FileNotFoundException(
                            $"Referenced image '{relative}' was not found in '{assetsSourceDirectory}'.",
                            source);
                    }

                    var entryName = $"{AssetsFolderName}/{relative}";
                    archive.CreateEntryFromFile(source, entryName, CompressionLevel.Optimal);
                }
            }

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            File.Move(tempPath, destinationPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// Carga un paquete <c>.comicproj</c>: lo extrae en <paramref name="extractToDirectory"/>,
    /// deserializa <c>manifest.json</c> y devuelve el documento junto con las rutas de trabajo.
    /// </summary>
    public static ComicProjectLoadResult Load(string sourcePath, string extractToDirectory)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Project '{sourcePath}' was not found.", sourcePath);
        }

        Directory.CreateDirectory(extractToDirectory);

        using var archive = ZipFile.OpenRead(sourcePath);

        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException(
                $"Package '{sourcePath}' does not contain '{ManifestEntryName}'; it may be corrupt or not a valid project.");

        ComicDocument document;
        using (var manifestStream = manifestEntry.Open())
        {
            document = ComicJson.Deserialize(manifestStream);
        }

        var fullExtractDir = Path.GetFullPath(extractToDirectory);
        foreach (var entry in archive.Entries)
        {
            // Saltar directorios (nombre termina en '/').
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            var destPath = Path.GetFullPath(Path.Combine(fullExtractDir, entry.FullName));

            // Protección contra "Zip Slip": la ruta destino debe quedar dentro de la carpeta.
            if (!destPath.StartsWith(fullExtractDir + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !string.Equals(destPath, fullExtractDir, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Entrada de ZIP con ruta no válida (fuera de la carpeta destino): '{entry.FullName}'.");
            }

            var entryDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(entryDir))
            {
                Directory.CreateDirectory(entryDir);
            }

            entry.ExtractToFile(destPath, overwrite: true);
        }

        var assetsDir = Path.Combine(fullExtractDir, AssetsFolderName);
        return new ComicProjectLoadResult(document, fullExtractDir, assetsDir);
    }

    /// <summary>
    /// Crea una carpeta de trabajo temporal única para extraer o preparar un proyecto.
    /// </summary>
    public static string CreateTempWorkingDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ComicLayoutEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string NormalizeRelative(string relativePath)
        => relativePath.Replace('\\', '/').Trim().TrimStart('/');
}
