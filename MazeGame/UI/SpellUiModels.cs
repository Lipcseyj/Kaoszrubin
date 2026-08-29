using MazeGame.Application;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Magic;

namespace MazeGame.UI;

/// <summary>Közös host–vendég varázslatinformációs karakterlap.</summary>
public static class SpellInfoPanel
{
    private const int VisibleSpellRows = 20;
    private const int DescriptionWidth = CharacterSheetPanel.Width;
    private const int DescriptionRows = 5;

    public static IReadOnlyList<CharacterSheetPanelLine> Build(string characterName, string characterClassId,
        int characterLevel, SpellInfoSnapshot info, int selectedIndex, bool focused = false)
    {
        var spells = info.KnownSpells;
        selectedIndex = spells.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, spells.Count - 1);
        var lines = new List<CharacterSheetPanelLine>
        {
            new(0, $"VARÁZSLATOK - {characterName}", ConsoleColor.Yellow, Background:
                focused ? ConsoleColor.Green : ConsoleColor.Black),
            new(1, $"Fókusz: {info.FocusName}", info.FocusName == "HIÁNYZIK" ? ConsoleColor.Red : ConsoleColor.Cyan),
            new(2, $"Memória: {spells.Count(spell => spell.IsMemorized)}/{info.MemorizationCapacity}", ConsoleColor.Magenta),
            new(3, "[M] memorizált  [F#] gyors", ConsoleColor.DarkCyan),
            new(4, "ISMERT VARÁZSLATOK", ConsoleColor.White)
        };
        var start = Math.Clamp(selectedIndex - 9, 0, Math.Max(0, spells.Count - VisibleSpellRows));
        for (var row = 0; row < VisibleSpellRows; row++)
        {
            var index = start + row;
            if (index >= spells.Count) { lines.Add(new(5 + row, string.Empty, ConsoleColor.Gray)); continue; }
            var spell = spells[index];
            var quick = spell.QuickSlot is { } slot ? $"F{slot + 1}" : "  ";
            var selected = index == selectedIndex;
            lines.Add(new(5 + row, $"{(selected ? ">" : " ")}[{(spell.IsMemorized ? "M" : " ")}][{quick}] " +
                $"{spell.Level}. {spell.Name}", selected ? ConsoleColor.Yellow :
                    spell.IsMemorized ? ConsoleColor.Cyan : ConsoleColor.Gray,
                Background: selected ? ConsoleColor.DarkCyan : ConsoleColor.Black));
        }
        if (spells.Count > 0)
        {
            var selected = spells[selectedIndex];
            lines.Add(new(26, "KIJELÖLT VARÁZSLAT", ConsoleColor.White));
            lines.Add(new(27, selected.Name, ConsoleColor.Yellow));
            lines.Add(new(28, $"L{selected.Level} {selected.ManaCost}M {ConsoleRenderer.SpellTargetName(selected.TargetType)}", ConsoleColor.Blue));
            lines.Add(new(29, selected.IsMemorized
                ? $"Memorizált{(selected.QuickSlot is { } slot ? $", F{slot + 1}" : string.Empty)}"
                : "Csak ismert", ConsoleColor.Magenta));
            var description = Wrap(selected.Description, DescriptionWidth).Take(DescriptionRows).ToArray();
            for (var row = 0; row < DescriptionRows; row++)
                lines.Add(new(30 + row, row < description.Length ? description[row] : string.Empty, ConsoleColor.Gray));
        }
        lines.Add(new(36, "VARÁZSLATSZINTEK", ConsoleColor.White));
        var unlocks = characterClassId == CharacterClassIds.Lovag ? new[] { 1, 8 } : new[] { 1, 5, 10, 15, 20 };
        for (var index = 0; index < unlocks.Length; index++)
            lines.Add(new(37 + index, $"{index + 1}. szint: L{unlocks[index]} " +
                (characterLevel >= unlocks[index] ? "feloldva" : $"még {unlocks[index] - characterLevel}"),
                characterLevel >= unlocks[index] ? ConsoleColor.Green : ConsoleColor.DarkYellow));
        var nextUnlock = unlocks.FirstOrDefault(level => level > characterLevel);
        lines.Add(new(43, nextUnlock == 0 ? "Minden szint feloldva." : $"Következő feloldás: L{nextUnlock}", ConsoleColor.Cyan));
        lines.Add(new(45, "Fel/le | F1-F8 gyors", ConsoleColor.Green));
        lines.Add(new(46, "Enter elsüt | Esc vissza", ConsoleColor.DarkYellow));
        return lines;
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var remaining = text;
        while (remaining.Length > width)
        {
            var split = remaining.LastIndexOf(' ', width);
            if (split <= 0) split = width;
            yield return remaining[..split];
            remaining = remaining[split..].TrimStart();
        }
        yield return remaining;
    }
}

public sealed record SpellSelectorOption(string Name, int Level, int ManaCost, SpellTargetType TargetType,
    string QuickLabel, bool Affordable);

/// <summary>Közös host–vendég varázslatválasztó ablak tartalma.</summary>
public static class SpellSelectorWindow
{
    public const int Width = 76;
    public const int PageSize = 12;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Build(string characterName, int currentMana,
        int maximumMana, bool inCombat, IReadOnlyList<SpellSelectorOption> options, int selectedIndex,
        int firstVisibleIndex, int casterIndex = 0, int casterCount = 1)
    {
        var switchHint = casterCount > 1 ? "  ◄► váltás" : string.Empty;
        var casterHint = casterCount > 1 ? $"   ({casterIndex + 1}/{casterCount})" : string.Empty;
        var visible = options.Skip(firstVisibleIndex).Take(PageSize).ToArray();
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            (inCombat ? "⚔️ HARCI VARÁZSLÁS" : "🔮 VARÁZSLÁS", ConsoleColor.Magenta),
            ($"{characterName}  ◆ {currentMana}/{maximumMana} manna{casterHint}", ConsoleColor.Cyan),
            ("↑↓ választ  Enter célzás  Esc bezár" + switchHint, ConsoleColor.Green),
            (new string('─', 68), ConsoleColor.DarkMagenta)
        };
        if (options.Count == 0)
            lines.Add(("Ebben a helyzetben nincs használható memorizált vagy tárgyban tárolt varázslat.",
                ConsoleColor.DarkYellow));
        else
        {
            lines.AddRange(visible.Select((spell, visibleIndex) =>
            {
                var index = firstVisibleIndex + visibleIndex;
                return ($"{(index == selectedIndex ? "▶" : " ")} [{spell.QuickLabel}] L{spell.Level}  " +
                        $"{spell.Name,-24} {spell.ManaCost}M  {ConsoleRenderer.SpellTargetName(spell.TargetType)}",
                    !spell.Affordable ? ConsoleColor.DarkRed :
                    index == selectedIndex ? ConsoleColor.Yellow : ConsoleColor.Gray);
            }));
            if (options.Count > PageSize)
                lines.Add(($"{firstVisibleIndex + 1}–{firstVisibleIndex + visible.Length} / {options.Count}",
                    ConsoleColor.DarkCyan));
        }
        return lines;
    }
}
