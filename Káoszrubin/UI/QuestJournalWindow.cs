using KaoszRubin.Application;

namespace KaoszRubin.UI;

/// <summary>A host és a coop vendég közös, lapozható küldetésnapló-ablaka.</summary>
public static class QuestJournalWindow
{
    public const int Width = 84;
    public sealed record FastTravelOption(string QuestId, string QuestTitle, string QuestGiverName, int NeedCost);
    public sealed record Result(string? FastTravelQuestId = null, string? AbandonedQuestId = null);
    public readonly record struct RestorationRegion(int Left, int Top, int Width, int Height);

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Build(
        IReadOnlyList<QuestJournalEntrySnapshot> entries, string? selectedActiveQuestId = null)
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
            var marker = string.Equals(entry.QuestId, selectedActiveQuestId, StringComparison.OrdinalIgnoreCase)
                ? "▶" : "◇";
            lines.Add(($"  {marker} {entry.Title} — {entry.Progress}/{entry.RequiredCount}  " +
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
            if (!string.IsNullOrWhiteSpace(entry.CompletionExperienceSummary))
                AddWrapped(lines, $"    Kapott XP: {entry.CompletionExperienceSummary}", ConsoleColor.Cyan);
            if (!string.IsNullOrWhiteSpace(entry.CompletionItemRewardSummary))
                AddWrapped(lines, $"    Kapott tárgyak: {entry.CompletionItemRewardSummary}", ConsoleColor.Yellow);
        }

        var abandoned = entries.Where(entry => entry.Status == QuestJournalStatus.Abandoned).ToArray();
        if (abandoned.Length > 0)
        {
            lines.Add((string.Empty, ConsoleColor.Gray));
            lines.Add(("FELADOTT KÜLDETÉSEK", ConsoleColor.DarkGray));
            foreach (var entry in abandoned)
                lines.Add(($"  × {entry.Title} — {entry.QuestGiverName}", ConsoleColor.DarkGray));
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

    public static Result? Show(IReadOnlyList<QuestJournalEntrySnapshot> entries,
        IReadOnlyList<FastTravelOption>? fastTravelOptions = null, bool allowAbandon = true)
    {
        var options = fastTravelOptions ?? [];
        var selectedOption = 0;
        var selectedActiveQuest = 0;
        var confirmingAbandon = false;
        var offset = 0;
        var restorationRegion = CalculateRestorationRegion(entries, options.Count,
            Console.WindowWidth, Console.WindowHeight, allowAbandon);
        using var background = new BackgroundContentRestorer(restorationRegion.Left, restorationRegion.Top,
            restorationRegion.Width, restorationRegion.Height);
        while (true)
        {
            var activeEntries = entries.Where(entry => entry.Status == QuestJournalStatus.Active).ToArray();
            if (activeEntries.Length > 0)
                selectedActiveQuest = Math.Clamp(selectedActiveQuest, 0, activeEntries.Length - 1);
            var selectedQuestId = activeEntries.Length == 0 ? null : activeEntries[selectedActiveQuest].QuestId;
            var allLines = Build(entries, selectedQuestId).ToList();
            if (options.Count > 0)
            {
                allLines.Add((string.Empty, ConsoleColor.Gray));
                allLines.Add(("🗺️ GYORS UTAZÁSOS KÜLDETÉSLEADÁS", ConsoleColor.Cyan));
                for (var index = 0; index < options.Count; index++)
                {
                    var option = options[index];
                    allLines.Add(($"  {(index == selectedOption ? "▶" : " ")} {option.QuestTitle} — " +
                        $"{option.QuestGiverName}  🍖-{option.NeedCost} 💧-{option.NeedCost}",
                        index == selectedOption ? ConsoleColor.Yellow : ConsoleColor.DarkYellow));
                }
            }
            var pageSize = Math.Max(4, Console.WindowHeight - 8);
            var maximumOffset = Math.Max(0, allLines.Count - pageSize);
            offset = Math.Clamp(offset, 0, maximumOffset);
            var page = allLines.Skip(offset).Take(pageSize).ToList();
            page.Add((maximumOffset > 0
                ? $"↑/↓, PgUp/PgDn: görgetés  {offset + 1}–{Math.Min(allLines.Count, offset + pageSize)}/{allLines.Count}"
                : "Q / Enter / Esc: bezárás", ConsoleColor.DarkYellow));
            if (maximumOffset > 0) page.Add(("Q / Enter / Esc: bezárás", ConsoleColor.DarkYellow));
            if (options.Count > 0)
                page.Add(("←/→: küldetésválasztás, T: utazás, leadás és visszatérés", ConsoleColor.Cyan));
            if (allowAbandon && activeEntries.Length > 0)
                page.Add((confirmingAbandon
                    ? $"⚠ Feladod: {activeEntries[selectedActiveQuest].Title}? I/Y: igen | N/Esc: mégsem"
                    : "Tab: aktív küldetés választása | F: kijelölt küldetés feladása",
                    confirmingAbandon ? ConsoleColor.Red : ConsoleColor.DarkYellow));
            Draw(page);

            var key = Console.ReadKey(intercept: true).Key;
            if (confirmingAbandon)
            {
                if (key is ConsoleKey.I or ConsoleKey.Y)
                    return new Result(AbandonedQuestId: activeEntries[selectedActiveQuest].QuestId);
                if (key is ConsoleKey.N or ConsoleKey.Escape) confirmingAbandon = false;
                continue;
            }
            if (key is ConsoleKey.Q or ConsoleKey.Enter or ConsoleKey.Escape) return null;
            if (key == ConsoleKey.T && options.Count > 0)
                return new Result(FastTravelQuestId: options[selectedOption].QuestId);
            if (allowAbandon && key == ConsoleKey.Tab && activeEntries.Length > 0)
                selectedActiveQuest = (selectedActiveQuest + 1) % activeEntries.Length;
            if (allowAbandon && key == ConsoleKey.F && activeEntries.Length > 0) confirmingAbandon = true;
            if (key == ConsoleKey.LeftArrow && options.Count > 0)
                selectedOption = (selectedOption + options.Count - 1) % options.Count;
            if (key == ConsoleKey.RightArrow && options.Count > 0)
                selectedOption = (selectedOption + 1) % options.Count;
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

    public static RestorationRegion CalculateRestorationRegion(
        IReadOnlyList<QuestJournalEntrySnapshot> entries, int fastTravelOptionCount,
        int windowWidth, int windowHeight, bool allowAbandon = true)
    {
        var width = Math.Min(Width, Math.Max(20, windowWidth));
        var contentLineCount = Build(entries).Count +
                               (fastTravelOptionCount > 0 ? 2 + fastTravelOptionCount : 0);
        var pageSize = Math.Max(4, windowHeight - 8);
        var footerLineCount = (contentLineCount > pageSize ? 2 : 1) +
                              (fastTravelOptionCount > 0 ? 1 : 0) +
                              (allowAbandon && entries.Any(entry => entry.Status == QuestJournalStatus.Active) ? 1 : 0);
        var height = Math.Min(windowHeight, Math.Min(contentLineCount, pageSize) + footerLineCount + 2);
        return new RestorationRegion(Math.Max(0, (windowWidth - width) / 2),
            Math.Max(0, (windowHeight - height) / 2), width, height);
    }

    private static void Draw(IReadOnlyList<(string Text, ConsoleColor Color)> lines)
    {
        var width = Math.Min(Width, Math.Max(20, Console.WindowWidth));
        var style = WindowFrameConfiguration.For(FramedWindow.QuestJournal);
        var left = Math.Max(0, (Console.WindowWidth - width) / 2);
        var top = Math.Max(0, (Console.WindowHeight - lines.Count - 2) / 2);
        Write(left, top, WindowFrameCatalog.Horizontal(style, width), ConsoleColor.Magenta);
        for (var index = 0; index < lines.Count; index++)
        {
            var sides = WindowFrameCatalog.Sides(style, index, lines.Count);
            var contentWidth = Math.Max(0, width - sides.Left.Length - sides.Right.Length - 2);
            var text = BattleCommandPanel.TruncateToDisplayWidth(lines[index].Text, contentWidth);
            Write(left, top + index + 1, sides.Left, ConsoleColor.Magenta);
            Write(left + sides.Left.Length, top + index + 1, " " + PadRightDisplay(text, contentWidth) + " ",
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
        Console.Write(BattleCommandPanel.TruncateToDisplayWidth(text,
            Math.Max(0, Console.WindowWidth - Math.Max(0, left))));
    }

    private static string PadRightDisplay(string text, int width) => text +
        new string(' ', Math.Max(0, width - BattleCommandPanel.DisplayWidth(text)));
}
