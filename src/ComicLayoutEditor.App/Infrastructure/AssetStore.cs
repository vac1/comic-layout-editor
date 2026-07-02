using System;
using System.IO;
using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.App.Infrastructure;

/// <summary>
/// Gestiona la carpeta <c>assets/</c> de trabajo del proyecto: importa imágenes
/// (copiándolas con un nombre único) y resuelve rutas relativas a absolutas.
/// </summary>
public sealed class AssetStore
{
    public string AssetsDirectory { get; }

    public AssetStore(string assetsDirectory)
    {
        AssetsDirectory = assetsDirectory;
        Directory.CreateDirectory(assetsDirectory);
    }

    /// <summary>Ruta absoluta de una imagen a partir de su ruta relativa.</summary>
    public string ResolvePath(string relativePath)
        => Path.Combine(AssetsDirectory, relativePath);

    /// <summary>
    /// Copia <paramref name="sourceFile"/> a la carpeta de assets con un nombre
    /// único y devuelve la referencia correspondiente.
    /// </summary>
    public ImageRef Import(string sourceFile)
    {
        var extension = Path.GetExtension(sourceFile);
        var name = NextAvailableName(extension);
        File.Copy(sourceFile, Path.Combine(AssetsDirectory, name), overwrite: false);
        return new ImageRef
        {
            RelativePath = name,
            OriginalFileName = Path.GetFileName(sourceFile)
        };
    }

    private string NextAvailableName(string extension)
    {
        for (var i = 1; i < 100000; i++)
        {
            var candidate = $"img_{i:D4}{extension}";
            if (!File.Exists(Path.Combine(AssetsDirectory, candidate)))
            {
                return candidate;
            }
        }
        // Salvaguarda extremadamente improbable.
        return $"img_{Guid.NewGuid():N}{extension}";
    }
}
