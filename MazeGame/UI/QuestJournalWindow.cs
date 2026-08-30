using MazeGame.Application;

namespace MazeGame.UI;

/// <summary>A host és a coop vendég közös, lapozható küldetésnapló-ablaka.</summary>
public static class QuestJournalWindow
{
    public const int Width = 84;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Build(
        IReadOnlyList<QuestJournalEntrySnapshot> entries)
    {
        var lines = new List<(string, ConsoleColor)>
        {
            ("📜 KÜLDETÉSEK", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ("AKTÍV KÜLDETÉSEK", ConsoleColor.Cyan)
        };
        var active = entries.Where(entry => entry.Status == QuestJournalStatus.Active).ToArray();
        if (active.Length == 0) lines.Add(("  — Nincs aktív küldetés.", ConsoleColor.DarkGray));
        foreach (var entry in active)
        {
            lines.Add(($"  ◇ {entry.Title} — {entry.Progress}/{entry.RequiredCount}  " +
                $"{entry.QuestGiverName} ({entry.ExperienceReward} XP)",
                ConsoleColor.Yellow));
            AddWrapped(lines, $"    {entry.Description}", ConsoleColor.Gray);
        }

        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("TELJESÍTETT KÜLDETÉSEK", ConsoleColor.Green));
        var completed = entries.Where(entry => entry.Status == QuestJournalStatus.Completed).ToArray();
        if (completed.Length == 0) lines.Add(("  — Még nincs teljesített küldetés.", ConsoleColor.DarkGray));
        foreach (var entry in completed)
        {
            lines.Add(($"  ✅ {entry.Title} — {entry.QuestGiverName} (+{entry.ExperienceReward} XP)",
                ConsoleColor.Green));
            AddWrapped(lines, $"    {entry.Description}", ConsoleColor.Gray);
        }
        return lines;
    }

    private static void AddWrapped(ICollection<(string Text, ConsoleColor Color)> lines, string text,
        ConsoleColor color)
    {
        const int maximumLength = 74;
        var remaining = text;
        while (remaining.Length > maximumLength)
        {
            var split = remaining.LastIndexOf(' ', maximumLength);
            if (split < 8) split = maximumLength;
            lines.Add((remaining[..split], color));
            remaining = "      " + remaining[split..].TrimStart();
        }
        lines.Add((remaining, color));
    }

    public static void Show(IReadOnlyList<QuestJournalEntrySnapshot> entries)
    {
        var allLines = Build(entries);
        var offset = 0;
        while (true)
        {
            var pageSize = Math.Max(4, Console.WindowHeight - 8);
            var maximumOffset = Math.Max(0, allLines.Count - pageSize);
            offset = Math.Clamp(offset, 0, maximumOffset);
            var page = allLines.Skip(offset).Take(pageSize).ToList();
            page.Add((maximumOffset > 0
                ? $"↑/↓, PgUp/PgDn: görgetés  {offset + 1}–{Math.Min(allLines.Count, offset + pageSize)}/{allLines.Count}"
                : "Q / Enter / Esc: bezárás", ConsoleColor.DarkYellow));
            if (maximumOffset > 0) page.Add(("Q / Enter / Esc: bezárás", ConsoleColor.DarkYellow));
            Draw(page);

            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.Q or ConsoleKey.Enter or ConsoleKey.Escape) return;
            offset = key switch
            {
                ConsoleKey.UpArrow => offset - 1,
                ConsoleKey.DownArrow => offset + 1,
                ConsoleKey.PageUp => offset - pageSize,
                ConsoleKey.PageDown => offset + pageSize,
                _ => offset
            };
        }
    }

    private static void Draw(IReadOnlyList<(string Text, ConsoleColor Color)> lines)
    {
        Console.Clear();
        var width = Math.Min(Width, Math.Max(20, Console.WindowWidth));
        var style = WindowFrameConfiguration.For(FramedWindow.QuestJournal);
        var left = Math.Max(0, (Console.WindowWidth - width) / 2);
        var top = Math.Max(0, (Console.WindowHeight - lines.Count - 2) / 2);
        Write(left, top, WindowFrameCatalog.Horizontal(style, width), ConsoleColor.Magenta);
        for (var index = 0; index < lines.Count; index++)
        {
            var sides = WindowFrameCatalog.Sides(style, index, lines.Count);
            var contentWidth = Math.Max(0, width - sides.Left.Length - sides.Right.Length - 2);
            var text = lines[index].Text.Length <= contentWidth
                ? lines[index].Text : lines[index].Text[..contentWidth];
            Write(left, top + index + 1, sides.Left, ConsoleColor.Magenta);
            Write(left + sides.Left.Length, top + index + 1, " " + text.PadRight(contentWidth) + " ",
                lines[index].Color);
            Write(left + width - sides.Right.Length, top + index + 1, sides.Right, ConsoleColor.Magenta);
        }
        Write(left, top + lines.Count + 1, WindowFrameCatalog.Horizontal(style, width, bottom: true),
            ConsoleColor.Magenta);
        Console.ResetColor();
    }

    private static void Write(int left, int top, string text, ConsoleColor color)
    {
        if (top < 0 || top >= Console.WindowHeight || left >= Console.WindowWidth) return;
        Console.SetCursorPosition(Math.Max(0, left), top);
        Console.ForegroundColor = color;
        Console.Write(text.Length <= Console.WindowWidth - Math.Max(0, left)
            ? text : text[..Math.Max(0, Console.WindowWidth - Math.Max(0, left))]);
    }
}
