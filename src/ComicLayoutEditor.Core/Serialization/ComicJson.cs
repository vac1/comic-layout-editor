using System.Text.Json;
using System.Text.Json.Serialization;
using ComicLayoutEditor.Core.Models;

namespace ComicLayoutEditor.Core.Serialization;

/// <summary>
/// Serialización de <see cref="ComicDocument"/> a/desde JSON (el contenido de
/// <c>manifest.json</c>). Los enums se escriben como texto para legibilidad y
/// estabilidad frente a cambios de orden.
/// </summary>
public static class ComicJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static string Serialize(ComicDocument document)
        => JsonSerializer.Serialize(document, Options);

    public static ComicDocument Deserialize(string json)
        => JsonSerializer.Deserialize<ComicDocument>(json, Options)
           ?? throw new InvalidDataException("El manifiesto JSON no pudo deserializarse (resultó nulo).");

    public static ComicDocument Deserialize(Stream utf8Json)
        => JsonSerializer.Deserialize<ComicDocument>(utf8Json, Options)
           ?? throw new InvalidDataException("El manifiesto JSON no pudo deserializarse (resultó nulo).");
}
