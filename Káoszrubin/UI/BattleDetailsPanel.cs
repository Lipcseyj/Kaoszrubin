using KaoszRubin.Combat;
using System.Globalization;

namespace KaoszRubin.UI;

/// <summary>Eight rows embedded in the character sheet, without another pair of side borders.</summary>
public static class BattleDetailsPanel
{
    public const int FirstRow = 9;
    public const int Height = 8;
    public const int ContentRows = 6;
    public const int ExtendedWidth = CharacterSheetPanel.Width + 2;

    public static IReadOnlyList<CharacterSheetPanelLine> Build(BattleActionDetails? details, int page)
    {
        var pages = Pages(details);
        page = Normalize(page, pages.Count);
        var header = FillSeparator("├─ ⚔ CSATARÉSZLET ");
        var lines = new List<CharacterSheetPanelLine>
        {
            new(FirstRow, header, ConsoleColor.DarkCyan, ExtendsToDivider: true,
                Segments: [new(header, ConsoleColor.DarkCyan)])
        };
        for (var row = 0; row < ContentRows; row++)
        {
            var text = row < pages[page].Count ? pages[page][row] : string.Empty;
            lines.Add(new(FirstRow + 1 + row, text,
                text.Contains("KRITIKUS") ? ConsoleColor.Yellow :
                text.StartsWith("💥") ? ConsoleColor.Red :
                text.StartsWith("🎯") ? ConsoleColor.Green : ConsoleColor.Gray));
        }
        const string footerStart = "├─ ";
        const string pagingKeys = "−/+";
        var footerEnd = $" Részletek {page + 1}/{pages.Count} ";
        footerEnd += new string('─', Math.Max(0,
            ExtendedWidth - footerStart.Length - pagingKeys.Length - footerEnd.Length - 1)) + "┤";
        lines.Add(new(FirstRow + Height - 1, footerStart + pagingKeys + footerEnd,
            ConsoleColor.DarkCyan, ExtendsToDivider: true,
            Segments: [new(footerStart, ConsoleColor.DarkCyan), new(pagingKeys, ConsoleColor.Yellow),
                new(footerEnd, ConsoleColor.DarkCyan)]));
        return lines;
    }

    private static string FillSeparator(string start) =>
        start + new string('─', Math.Max(0, ExtendedWidth - start.Length - 1)) + "┤";

    public static int PageCount(BattleActionDetails? details) => Pages(details).Count;
    public static int Normalize(int page, int count) => (page % count + count) % count;

    private static List<List<string>> Pages(BattleActionDetails? details)
    {
        if (details is null) return [["⌛ Az első akcióra vár..."]];
        var heading = string.IsNullOrWhiteSpace(details.Target) ? details.Actor : $"{details.Actor} → {details.Target}";
        // The outcome (including critical chance) must always fit on the first page.
        var lines = new[] { Wrap(heading).FirstOrDefault() ?? string.Empty }
            .Concat(details.Summary).Concat(details.Calculation).SelectMany(Wrap).ToArray();
        return lines.Chunk(ContentRows).Select(chunk => chunk.ToList()).ToList();
    }

    // Keep surrogate pairs, combining marks and emoji sequences intact when wrapping.
    private static IEnumerable<string> Wrap(string text)
    {
        var line = string.Empty;
        var elements = StringInfo.GetTextElementEnumerator(text.Replace('\t', ' '));
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (line.Length + element.Length > CharacterSheetPanel.Width && line.Length > 0)
            { yield return line; line = string.Empty; }
            line += element;
        }
        if (line.Length > 0) yield return line;
    }
}
