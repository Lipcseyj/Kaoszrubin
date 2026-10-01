using System.Text.Json;

namespace KaoszRubin.MapEditor;

internal sealed class MapEditorSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public Dictionary<string, Dictionary<string, int>> GridColumnWidths { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Kaoszrubin", "MapEditor", "settings.json");

    public static MapEditorSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<MapEditorSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new()
                : new();
        }
        catch (Exception) when (File.Exists(FilePath))
        {
            // Egy hibás helyi UI-beállítás ne akadályozza meg a szerkesztő indulását.
            return new();
        }
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
