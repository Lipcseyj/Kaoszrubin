using System.Text.Json;

namespace KaoszRubin;

public enum QuickCombatMode
{
    Ask,
    Automatic,
    Never
}

public sealed class MusicSettings
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

public sealed class MusicSettingsService
{
    private readonly string _path;

    public MusicSettings Settings { get; }

    public MusicSettingsService(string? path = null)
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

    private static MusicSettings Load(string path)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<MusicSettings>(File.ReadAllText(path)) ?? new MusicSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception)
        {
            return new MusicSettings();
        }
    }
}
