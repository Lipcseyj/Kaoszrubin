using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;

namespace KaoszRubin.UI;

/// <summary>Blokkoló, a parti dokumentált szörnyismeretét lapozó ablak.</summary>
public static class BestiaryWindow
{
    internal sealed record Entry(EnemyDefinition Enemy, int KillCount);
    internal sealed record Line(string Text, ConsoleColor Color);

    public static IReadOnlyDictionary<string, int> AggregateKills(
        IEnumerable<SessionCharacterSnapshot> party) => party
        .Where(character => !character.IsTemporaryFollower)
        .SelectMany(character => character.History?.MonsterKills ?? [])
        .Where(kill => kill.Count > 0)
        .GroupBy(kill => kill.EnemyDefinitionId, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Sum(kill => kill.Count),
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, int> AggregateKills(IEnumerable<LiveCharacter> party) => party
        .SelectMany(character => character.MonsterKills)
        .Where(kill => kill.Value > 0)
        .GroupBy(kill => kill.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Sum(kill => kill.Value),
            StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyList<Entry> CreateEntries(GameDataCatalog gameData,
        IReadOnlyDictionary<string, int> kills) => kills
        .Where(kill => kill.Value > 0)
        .Select(kill => (Enemy: gameData.Enemies.FirstOrDefault(enemy =>
            string.Equals(enemy.Id, kill.Key, StringComparison.OrdinalIgnoreCase)), kill.Value))
        .Where(entry => entry.Enemy is not null)
        .Select(entry => new Entry(entry.Enemy!, entry.Value))
        .OrderBy(entry => entry.Enemy.StrengthTier)
        .ThenBy(entry => entry.Enemy.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    public static void Show(GameDataCatalog gameData, IReadOnlyDictionary<string, int> kills,
        Func<string?>? coopStatusProvider = null)
    {
        var entries = CreateEntries(gameData, kills);
        var width = Math.Max(54, Math.Min(116, Console.WindowWidth - 4));
        var height = Math.Max(16, Math.Min(40, Console.WindowHeight - 4));
        var left = Math.Max(0, (Console.WindowWidth - width) / 2);
        var top = Math.Max(0, (Console.WindowHeight - height) / 2);
        var style = WindowFrameConfiguration.For(FramedWindow.Bestiary);
        var index = 0;
        using var background = new BackgroundContentRestorer(left, top, width, height);
        while (true)
        {
            index = entries.Count == 0 ? 0 : Math.Clamp(index, 0, entries.Count - 1);
            DrawFrame(left, top, width, height, style);
            if (entries.Count == 0)
                Write(left + 3, top + 3,
                    "A parti még egyetlen szörnytípust sem győzött le.", ConsoleColor.DarkGray, width - 6);
            else
                DrawEntry(gameData, entries[index], index, entries.Count, left, top, width, height);

            var key = CoopWindowStatusBanner.ReadKey(coopStatusProvider).Key;
            if (key is ConsoleKey.Escape or ConsoleKey.Enter) return;
            if (entries.Count == 0) continue;
            if (key is ConsoleKey.LeftArrow or ConsoleKey.UpArrow or ConsoleKey.PageUp)
                index = (index - 1 + entries.Count) % entries.Count;
            else if (key is ConsoleKey.RightArrow or ConsoleKey.DownArrow or ConsoleKey.PageDown)
                index = (index + 1) % entries.Count;
            else if (key == ConsoleKey.Home) index = 0;
            else if (key == ConsoleKey.End) index = entries.Count - 1;
        }
    }

    internal static IReadOnlyList<Line> BuildDetails(GameDataCatalog gameData, Entry entry)
    {
        var enemy = entry.Enemy;
        var rank = enemy.Rank switch
        {
            EnemyRank.Elite => "elit",
            EnemyRank.MiniBoss => "miniboss",
            EnemyRank.Boss => "főellenfél",
            _ => "normál"
        };
        var traits = new List<string>();
        if (enemy.HasTrait(EnemyTraits.Undead)) traits.Add("élőholt");
        if (enemy.HasTrait(EnemyTraits.Demonic)) traits.Add("démoni");
        if (enemy.HasTrait(EnemyTraits.Flying)) traits.Add("repülő");
        var weapons = (enemy.Weapons ?? []).Select(weapon => weapon.Name).Distinct().ToArray();
        var lines = new List<Line>
        {
            new($"☠ Legyőzve: {entry.KillCount}", ConsoleColor.Red),
            new($"Rang: {rank}   erősségi fok: {enemy.StrengthTier}", ConsoleColor.Yellow),
            new($"HP: {Value(enemy.HitPoints)}   Erő: {Value(enemy.Strength)}   Páncél: {enemy.Armor?.ToString() ?? "?"}", ConsoleColor.Cyan),
            new($"Gyorsaság: {Value(enemy.Speed)}   XP: {enemy.ExperienceReward}", ConsoleColor.Cyan),
            new($"Látás: {enemy.VisionRange}   Lopakodás: {enemy.Stealth}   Zaj: {enemy.Noise}   Nyomkövetés: {enemy.TrackingSense}", ConsoleColor.Gray),
            new($"Mágiaellenállás: {enemy.MagicResistance}%", ConsoleColor.Magenta),
            new($"Sebzésellenállás: {enemy.Resistances ?? new DamageResistance()}", ConsoleColor.Gray),
            new($"Jellemzők: {(traits.Count == 0 ? "nincs" : string.Join(", ", traits))}", ConsoleColor.DarkYellow),
            new($"Fegyverek: {(weapons.Length == 0 ? "természetes vagy ismeretlen" : string.Join(", ", weapons))}", ConsoleColor.Green)
        };
        var abilities = enemy.AbilityIds.Select(id => gameData.MonsterAbilities.FirstOrDefault(ability =>
                string.Equals(ability.Id, id, StringComparison.OrdinalIgnoreCase)))
            .Where(ability => ability is not null).ToArray();
        lines.Add(new(abilities.Length == 0 ? "Képességek: nincs" : "Képességek:", ConsoleColor.Magenta));
        lines.AddRange(abilities.Select(ability => new Line($"• {ability!.Name}: {ability.Description}",
            ConsoleColor.Gray)));
        return lines;
    }

    private static void DrawEntry(GameDataCatalog gameData, Entry entry, int index, int count,
        int left, int top, int width, int height)
    {
        var contentWidth = width - 6;
        Write(left + 3, top + 1,
            $"📖 BESTIÁRIUM — {entry.Enemy.Appearance} {entry.Enemy.Name}", ConsoleColor.Yellow, contentWidth);
        var portrait = AsciiPortraits.ForEnemy(entry.Enemy.Id);
        var portraitWidth = Math.Min(portrait.CanvasWidth, Math.Max(20, contentWidth / 2));
        var detailsLeft = left + 3 + portraitWidth + 3;
        var detailsWidth = Math.Max(18, left + width - 3 - detailsLeft);
        var usableRows = height - 5;
        for (var row = 0; row < Math.Min(usableRows, portrait.Lines.Count); row++)
            Write(left + 3, top + 3 + row, portrait.Lines[row], EnemyColor(entry.Enemy), portraitWidth);

        var detailLines = BuildDetails(gameData, entry)
            .SelectMany(line => Wrap(line, detailsWidth)).Take(usableRows).ToArray();
        for (var row = 0; row < detailLines.Length; row++)
            Write(detailsLeft, top + 3 + row, detailLines[row].Text, detailLines[row].Color, detailsWidth);

        Write(left + 3, top + height - 2,
            $" ←/→ vagy ↑/↓ lapoz   Home/End   Esc/Enter vissza   {index + 1}/{count} ",
            ConsoleColor.DarkYellow, contentWidth);
    }

    private static IEnumerable<Line> Wrap(Line line, int width)
    {
        var text = line.Text;
        if (string.IsNullOrEmpty(text)) { yield return line; yield break; }
        while (BattleCommandPanel.DisplayWidth(text) > width)
        {
            var length = Math.Min(width, text.Length);
            var split = text.LastIndexOf(' ', Math.Max(0, length - 1), length);
            if (split <= 0) split = length;
            yield return line with { Text = text[..split] };
            text = text[split..].TrimStart();
        }
        yield return line with { Text = text };
    }

    private static void DrawFrame(int left, int top, int width, int height, WindowFrameStyle style)
    {
        Write(left, top, WindowFrameCatalog.Horizontal(style, width), ConsoleColor.Magenta, width);
        var interiorRows = height - 2;
        for (var row = 0; row < interiorRows; row++)
        {
            var sides = WindowFrameCatalog.Sides(style, row, interiorRows);
            Write(left, top + row + 1, sides.Left, ConsoleColor.Magenta, sides.Left.Length);
            Write(left + sides.Left.Length, top + row + 1, string.Empty, ConsoleColor.Gray,
                width - sides.Left.Length - sides.Right.Length);
            Write(left + width - sides.Right.Length, top + row + 1, sides.Right,
                ConsoleColor.Magenta, sides.Right.Length);
        }
        Write(left, top + height - 1, WindowFrameCatalog.Horizontal(style, width, bottom: true),
            ConsoleColor.Magenta, width);
    }

    private static ConsoleColor EnemyColor(EnemyDefinition enemy) => enemy.Rank switch
    {
        EnemyRank.Boss => ConsoleColor.Red,
        EnemyRank.MiniBoss => ConsoleColor.Magenta,
        EnemyRank.Elite => ConsoleColor.Yellow,
        _ => ConsoleColor.DarkCyan
    };

    private static string Value(int? value) => value?.ToString() ?? "?";

    private static void Write(int x, int y, string text, ConsoleColor color, int width)
    {
        if (width <= 0 || x < 0 || y < 0 || x >= Console.WindowWidth || y >= Console.WindowHeight) return;
        var fitted = BattleCommandPanel.FitToDisplayWidth(text, Math.Min(width, Console.WindowWidth - x));
        Console.SetCursorPosition(x, y);
        Console.ForegroundColor = color;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Write(fitted);
    }
}
