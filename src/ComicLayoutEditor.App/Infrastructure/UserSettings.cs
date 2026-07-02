using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ComicLayoutEditor.App.Infrastructure;

/// <summary>
/// Preferencias de sesión que se guardan al cerrar la aplicación y se restauran
/// al abrirla: posición/estado de la ventana y ajustes del editor (rejilla,
/// ajuste a rejilla, tamaño de rejilla y zoom). No forman parte de ningún
/// documento; se serializan a <c>%AppData%\ComicLayout Editor\settings.json</c>.
/// </summary>
public sealed class UserSettings
{
    // ---- Ventana --------------------------------------------------------------
    public bool WindowMaximized { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    // ---- Editor ---------------------------------------------------------------
    public bool ShowGrid { get; set; }
    public bool SnapToGrid { get; set; }
    public double GridSizeMm { get; set; } = 5.0;
    public double Zoom { get; set; } = 1.0;

    // ---- Archivos recientes ---------------------------------------------------

    /// <summary>Número máximo de archivos recientes que se conservan.</summary>
    public const int MaxRecentFiles = 10;

    /// <summary>
    /// Rutas de los últimos proyectos abiertos o guardados, del más reciente al
    /// más antiguo. Se actualiza al abrir/guardar y se muestra en el menú Archivo.
    /// </summary>
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>
    /// Coloca <paramref name="path"/> al principio de la lista de recientes,
    /// eliminando duplicados (sin distinguir mayúsculas) y recortando al máximo.
    /// </summary>
    public void PushRecentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecentFiles)
        {
            RecentFiles.RemoveRange(MaxRecentFiles, RecentFiles.Count - MaxRecentFiles);
        }
    }

    /// <summary>
    /// Registra <paramref name="path"/> como reciente de forma persistente:
    /// recarga las preferencias del disco (para no pisar otros cambios), añade la
    /// ruta al principio y vuelve a guardar. Errores de E/S se ignoran.
    /// </summary>
    public static void AddRecentFile(string path)
    {
        var settings = Load();
        settings.PushRecentFile(path);
        settings.Save();
    }

    /// <summary>Vacía de forma persistente la lista de archivos recientes.</summary>
    public static void ClearRecentFiles()
    {
        var settings = Load();
        settings.RecentFiles.Clear();
        settings.Save();
    }

    /// <summary>
    /// Quita <paramref name="path"/> de la lista de recientes de forma persistente
    /// (p. ej. cuando el archivo ya no existe). Errores de E/S se ignoran.
    /// </summary>
    public static void RemoveRecentFile(string path)
    {
        var settings = Load();
        var before = settings.RecentFiles.Count;
        settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        if (settings.RecentFiles.Count != before)
        {
            settings.Save();
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Ruta del archivo de preferencias en el perfil del usuario.</summary>
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ComicLayout Editor", "settings.json");

    /// <summary>
    /// Carga las preferencias. Si el archivo no existe o está corrupto, devuelve
    /// una instancia con los valores por defecto (nunca lanza).
    /// </summary>
    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                if (JsonSerializer.Deserialize<UserSettings>(json, Options) is { } settings)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Preferencias ilegibles: se ignoran y se parte de las de por defecto.
        }
        return new UserSettings();
    }

    /// <summary>
    /// Guarda las preferencias. Los errores de E/S se ignoran: no guardar las
    /// preferencias nunca debe impedir que la aplicación se cierre.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No se pudieron guardar las preferencias; no es crítico.
        }
    }
}
