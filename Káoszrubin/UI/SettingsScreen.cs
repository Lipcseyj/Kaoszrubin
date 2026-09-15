namespace KaoszRubin.UI;

/// <summary>A főmenü, a host és a vendég közös beállításablaka.</summary>
public static class SettingsScreen
{
    private const int Width = 70;

    public static void Show(GameSettingsService settingsService, Action? applyAudioSettings = null,
        Func<string?>? coopStatusProvider = null)
    {
        var settings = settingsService.Settings;
        while (true)
        {
            Draw(settings);
            var key = CoopWindowStatusBanner.ReadKey(coopStatusProvider);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Enter) break;

            if (key.Key is ConsoleKey.Spacebar or ConsoleKey.M)
                settings.MusicEnabled = !settings.MusicEnabled;
            else if (key.Key is ConsoleKey.E)
                settings.SoundEffectsEnabled = !settings.SoundEffectsEnabled;
            else if (key.Key is ConsoleKey.G)
                settings.QuickCombat = settings.QuickCombat switch
                {
                    QuickCombatMode.Ask => QuickCombatMode.Automatic,
                    QuickCombatMode.Automatic => QuickCombatMode.Never,
                    _ => QuickCombatMode.Ask
                };
            else if (key.Key is ConsoleKey.C)
                settings.CombatSpeed = settings.CombatSpeed switch
                {
                    CombatSpeed.PauseBeforeAnyAction => CombatSpeed.PauseAfterHit,
                    CombatSpeed.PauseAfterHit => CombatSpeed.PauseBeforePlayerAction,
                    CombatSpeed.PauseBeforePlayerAction => CombatSpeed.PauseBeforeAnyAction,
                    _ => CombatSpeed.PauseBeforeAnyAction
                };
            else if (key.Key is ConsoleKey.R)
                settings.PartyAvatars = settings.PartyAvatars == PartyAvatarSet.Letters
                    ? PartyAvatarSet.Runes
                    : PartyAvatarSet.Letters;
            else if (key.Key is ConsoleKey.LeftArrow or ConsoleKey.DownArrow)
                settings.MusicVolumePercent = Math.Max(0, settings.MusicVolumePercent - 5);
            else if (key.Key is ConsoleKey.RightArrow or ConsoleKey.UpArrow)
                settings.MusicVolumePercent = Math.Min(100, settings.MusicVolumePercent + 5);
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
        const int contentRows = 22;
        var top = Math.Max(0, (Console.WindowHeight - contentRows - 2) / 2);
        var style = WindowFrameConfiguration.For(FramedWindow.Settings);
        var lines = new[]
        {
            "⚙️  BEÁLLÍTÁSOK",
            string.Empty,
            $"Zene: {(settings.MusicEnabled ? "BE" : "KI")}",
            $"Hangerő: {settings.MusicVolumePercent}%",
            VolumeBar(settings.MusicVolumePercent),
            $"Hangeffektek: {(settings.SoundEffectsEnabled ? "BE" : "KI")}",
            $"Effekthangerő: {settings.SoundEffectsVolumePercent}%",
            VolumeBar(settings.SoundEffectsVolumePercent),
            $"Gyorsharc: {QuickCombatModeName(settings.QuickCombat)}",
            $"Harci sebesség:",
            $"{CombatSpeedName(settings.CombatSpeed)}",
            $"Party avatárok: {PartyAvatarSetName(settings.PartyAvatars)}",
            string.Empty,
            "M / Space       Zene ki- és bekapcsolása",
            "← → / ↑ ↓      Hangerő módosítása",
            "E               Hangeffektek ki- és bekapcsolása",
            "A / D           Effekthangerő módosítása",
            "G               Gyorsharc módjának váltása",
            "C               A harc sebességének váltása",
            "R               Party avatárkészlet váltása",
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
                2 => settings.MusicEnabled ? ConsoleColor.Green : ConsoleColor.DarkRed,
                3 or 4 or 6 or 7 or 9 => ConsoleColor.Cyan,
                5 => settings.SoundEffectsEnabled ? ConsoleColor.Green : ConsoleColor.DarkRed,
                8 => ConsoleColor.Magenta,
                10 => settings.CombatSpeed == CombatSpeed.PauseBeforeAnyAction ? ConsoleColor.Green : 
                    (settings.CombatSpeed == CombatSpeed.PauseAfterHit ? ConsoleColor.DarkYellow : ConsoleColor.Yellow),
                11 => ConsoleColor.Blue,
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

    private static string CombatSpeedName(CombatSpeed speed) => speed switch
    {
        CombatSpeed.PauseAfterHit => "A CSATA KÖR MEGÁLL MINDEN TALÁLAT UTÁN (közepes)",
        CombatSpeed.PauseBeforePlayerAction => "A CSATA KÖR CSAK JÁTÉKOS AKCIÓ ELŐTT ÁLL MEG (gyors)",
        _ => "A CSATA KÖR MINDEN AKCIÓ ELŐTT MEGÁLL (lassú)"
    };  

    private static string PartyAvatarSetName(PartyAvatarSet avatarSet) => avatarSet switch
    {
        PartyAvatarSet.Runes => "RÚNÁK (ᚺᛒᛚᛏᛈᛗ)",
        _ => "BETŰK (HBLTPM)"
    };

    private static void WriteAt(int left, int top, string text)
    {
        if (top < 0 || top >= Console.WindowHeight || left >= Console.WindowWidth) return;
        Console.SetCursorPosition(left, top);
        Console.Write(text[..Math.Min(text.Length, Console.WindowWidth - left)]);
    }
}
