using System.Text.Json;

namespace KaoszRubin;

public enum QuickCombatMode
{
    Ask,
    Automatic,
    Never
}

public sealed class GameSettings
{
    public bool Enabled { get; set; } = true;
    public int VolumePercent { get; set; } = 50;
    public QuickCombatMode QuickCombat { get; set; } = QuickCombatMode.Ask;

    public void Normalize()
    {
        VolumePercent = Math.Clamp(VolumePercent, 0, 100);
        if (!Enum.IsDefined(QuickCombat)) QuickCombat = QuickCombatMode.Ask;
    }
}

public sealed class GameSettingsService
{
    private readonly string _path;

    public GameSettings Settings { get; }

    public GameSettingsService(string? path = null)
    {
        _path = path ?? Path.Combine(AppContext.BaseDirectory, "beallitasok.json");
        Settings = Load(_path);
    }

    public void Save()
    {
        Settings.Normalize();
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Settings, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch (Exception)
        {
            // A beállítás ettől még az aktuális futásban érvényes marad.
        }
    }

    private static GameSettings Load(string path)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(path)) ?? new GameSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception)
        {
            return new GameSettings();
        }
    }
}
