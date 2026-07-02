namespace ComicLayoutEditor.Core.Models;

/// <summary>
/// Documento completo de historieta: título y páginas.
/// Es la raíz que se serializa a <c>manifest.json</c> dentro del paquete <c>.comicproj</c>.
/// </summary>
public sealed class ComicDocument
{
    /// <summary>
    /// Versión del esquema del manifiesto, para permitir migraciones futuras.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    public string Title { get; set; } = "Untitled";

    public List<Page> Pages { get; set; } = new();

    /// <summary>Crea un documento nuevo con una página A4 vacía.</summary>
    public static ComicDocument CreateNew(string title = "Untitled")
    {
        var doc = new ComicDocument { Title = title };
        doc.Pages.Add(Page.CreateA4());
        return doc;
    }

    /// <summary>Enumera todas las imágenes referenciadas por el documento.</summary>
    public IEnumerable<ImageRef> EnumerateImageRefs()
    {
        foreach (var page in Pages)
        {
            foreach (var panel in page.Panels)
            {
                if (panel.Image is not null)
                {
                    yield return panel.Image;
                }
            }
        }
    }
}
