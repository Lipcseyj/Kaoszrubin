namespace KaoszRubin.UI;

/// <summary>A főmenü, a host és a vendég közös beállításablaka.</summary>
public static class SettingsScreen
{
    private const int Width = 58;

    public static void Show(GameSettingsService settingsService, Action? applyAudioSettings = null)
    {
        var settings = settingsService.Settings;
        while (true)
        {
            Draw(settings);
            var key = Console.ReadKey(intercept: true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Enter) break;

            if (key.Key is ConsoleKey.Spacebar or ConsoleKey.M)
                settings.Enabled = !settings.Enabled;
            else if (key.Key is ConsoleKey.E)
                settings.SoundEffectsEnabled = !settings.SoundEffectsEnabled;
            else if (key.Key is ConsoleKey.G)
                settings.QuickCombat = settings.QuickCombat switch
                {
                    QuickCombatMode.Ask => QuickCombatMode.Automatic,
                    QuickCombatMode.Automatic => QuickCombatMode.Never,
                    _ => QuickCombatMode.Ask
                };
            else if (key.Key is ConsoleKey.LeftArrow or ConsoleKey.DownArrow)
                settings.VolumePercent = Math.Max(0, settings.VolumePercent - 5);
            else if (key.Key is ConsoleKey.RightArrow or ConsoleKey.UpArrow)
                settings.VolumePercent = Math.Min(100, settings.VolumePercent + 5);
            else if (key.Key is ConsoleKey.A)
                settings.SoundEffectsVolumePercent = Math.Max(0, settings.SoundEffectsVolumePercent - 5);
            else if (key.Key is ConsoleKey.D)
                settings.SoundEffectsVolumePercent = Math.Min(100, settings.SoundEffectsVolumePercent + 5);
            else
                continue;

            applyAudioSettings?.Invoke();
        }

        settingsService.Save();
    }

    private static void Draw(GameSettings settings)
    {
        Console.Clear();
        var left = Math.Max(0, (Console.WindowWidth - Width) / 2);
        const int contentRows = 17;
        var top = Math.Max(0, (Console.WindowHeight - contentRows - 2) / 2);
        var style = WindowFrameConfiguration.For(FramedWindow.Settings);
        var lines = new[]
        {
            "⚙️  BEÁLLÍTÁSOK",
            string.Empty,
            $"Zene: {(settings.Enabled ? "BE" : "KI")}",
            $"Hangerő: {settings.VolumePercent}%",
            VolumeBar(settings.VolumePercent),
            $"Hangeffektek: {(settings.SoundEffectsEnabled ? "BE" : "KI")}",
            $"Effekthangerő: {settings.SoundEffectsVolumePercent}%",
            VolumeBar(settings.SoundEffectsVolumePercent),
            $"Gyorsharc: {QuickCombatModeName(settings.QuickCombat)}",
            string.Empty,
            "M / Space       Zene ki- és bekapcsolása",
            "← → / ↑ ↓      Hangerő módosítása",
            "E               Hangeffektek ki- és bekapcsolása",
            "A / D           Effekthangerő módosítása",
            "G               Gyorsharc módjának váltása",
            string.Empty,
            "Enter / Esc     Vissza"
        };

        Console.ForegroundColor = ConsoleColor.Magenta;
        WriteAt(left, top, WindowFrameCatalog.Horizontal(style, Width));
        for (var row = 0; row < contentRows; row++)
        {
            var sides = WindowFrameCatalog.Sides(style, row, contentRows);
            var interiorWidth = Width - sides.Left.Length - sides.Right.Length;
            var text = lines[row];
            if (text.Length > interiorWidth - 2) text = text[..(interiorWidth - 2)];
            Console.ForegroundColor = ConsoleColor.Magenta;
            WriteAt(left, top + row + 1, sides.Left + " ");
            Console.ForegroundColor = row switch
            {
                0 => ConsoleColor.Yellow,
                2 => settings.Enabled ? ConsoleColor.Green : ConsoleColor.DarkRed,
                3 or 4 or 6 or 7 or 8 => ConsoleColor.Cyan,
                5 => settings.SoundEffectsEnabled ? ConsoleColor.Green : ConsoleColor.DarkRed,
                _ => ConsoleColor.Gray
            };
            Console.Write(text.PadRight(interiorWidth - 2));
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write(" " + sides.Right);
        }
        WriteAt(left, top + contentRows + 1, WindowFrameCatalog.Horizontal(style, Width, bottom: true));
        Console.ResetColor();
    }

    private static string VolumeBar(int volume) =>
        "[" + new string('■', volume / 5) + new string('·', 20 - volume / 5) + "]";

    private static string QuickCombatModeName(QuickCombatMode mode) => mode switch
    {
        QuickCombatMode.Automatic => "AUTOMATIKUS",
        QuickCombatMode.Never => "SOHA",
        _ => "RÁKÉRDEZ"
    };

    private static void WriteAt(int left, int top, string text)
    {
        if (top < 0 || top >= Console.WindowHeight || left >= Console.WindowWidth) return;
        Console.SetCursorPosition(left, top);
        Console.Write(text[..Math.Min(text.Length, Console.WindowWidth - left)]);
    }
}
