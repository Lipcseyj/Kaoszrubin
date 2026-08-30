using KaoszRubin.Application;

namespace KaoszRubin.UI;

/// <summary>A host és a vendég közös varázstanulási és -memorizálási ablaktartalma.</summary>
public static class MagicProgressionWindow
{
    public const int LearningWidth = 88;
    public const int PreparationWidth = 92;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> BuildLearning(string characterName,
        string progress, IReadOnlyList<LevelUpChoiceSnapshot> spells, int selectedIndex)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("📖  ÚJ VARÁZSLAT TANULÁSA", ConsoleColor.Magenta),
            (string.Empty, ConsoleColor.Gray),
            ($"{characterName} — {progress}", ConsoleColor.Cyan),
            ("Fel/le: választás     Enter: megtanulás", ConsoleColor.Green),
            (string.Empty, ConsoleColor.Gray)
        };
        lines.AddRange(spells.Select((spell, index) =>
            ($"{(index == selectedIndex ? "▶" : " ")}  {spell.Name}",
                index == selectedIndex ? ConsoleColor.Yellow : ConsoleColor.Gray)));
        return lines;
    }

    public static IReadOnlyList<(string Text, ConsoleColor Color)> BuildPreparation(string characterName,
        int selectedCount, int capacity, IReadOnlyList<KnownSpellSnapshot> spells,
        IReadOnlySet<string> selectedSpellIds, int cursor)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("🧠✨  VARÁZSLATOK MEMORIZÁLÁSA", ConsoleColor.Magenta),
            (string.Empty, ConsoleColor.Gray),
            ($"{characterName} — kapacitás: {selectedCount}/{capacity}", ConsoleColor.Cyan),
            ("Fel/le: mozgás   Space: ki/be   Enter: kész", ConsoleColor.Green),
            (string.Empty, ConsoleColor.Gray)
        };
        if (spells.Count == 0)
            lines.Add(("Nincs ismert varázslat. Enter: kész.", ConsoleColor.DarkYellow));
        else
            lines.AddRange(spells.Select((spell, index) =>
                ($"{(index == cursor ? "▶" : " ")} [{(selectedSpellIds.Contains(spell.SpellId) ? "X" : " ")}]  " +
                 $"{spell.Level}. szint — {spell.Name}",
                    index == cursor ? ConsoleColor.Yellow : ConsoleColor.Gray)));
        return lines;
    }
}
