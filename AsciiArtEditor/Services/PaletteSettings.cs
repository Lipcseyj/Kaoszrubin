using System.Text.Json;

namespace AsciiArtEditor.Services;

public sealed class PaletteSettings
{
    public List<string> Favourites { get; set; } = [];
    public Dictionary<int, string> PageNames { get; set; } = [];
}

public sealed class PaletteSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public PaletteSettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KaoszRubin",
            "AsciiArtEditor",
            "palette-settings.json");
    }

    public string FilePath { get; }

    public bool TryLoad(out PaletteSettings settings, out string? error)
    {
        settings = new PaletteSettings();
        error = null;
        if (!File.Exists(FilePath))
            return true;

        try
        {
            settings = JsonSerializer.Deserialize<PaletteSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new PaletteSettings();
            settings.Favourites ??= [];
            settings.PageNames ??= [];
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySave(PaletteSettings settings, out string? error)
    {
        error = null;
        var directory = Path.GetDirectoryName(FilePath);
        var temporaryPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporaryPath, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error = ex.Message;
            TryDelete(temporaryPath);
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
