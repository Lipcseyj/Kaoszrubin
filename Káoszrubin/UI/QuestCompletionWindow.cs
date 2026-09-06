using KaoszRubin.Application;

namespace KaoszRubin.UI;

/// <summary>A teljesített küldetés tényleges jutalmait mutató host- és vendégoldali ablak.</summary>
public static class QuestCompletionWindow
{
    public const int Width = 88;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Build(QuestJournalEntrySnapshot quest)
    {
        var lines = new List<(string, ConsoleColor)>
        {
            ("✅ KÜLDETÉS TELJESÍTVE", ConsoleColor.Green),
            (string.Empty, ConsoleColor.Gray),
            (quest.Title, ConsoleColor.Yellow),
            ($"Megbízó: {quest.QuestGiverName}", ConsoleColor.DarkYellow),
            (string.Empty, ConsoleColor.Gray),
            ("🎁 JUTALOM", ConsoleColor.Cyan)
        };
        lines.AddRange(MessageTextLayout.Wrap(
            $"⭐ Tapasztalat: {quest.CompletionExperienceSummary ?? $"{quest.ExperienceReward} XP"}", 78)
            .Select(text => (text, ConsoleColor.Cyan)));
        lines.AddRange(MessageTextLayout.Wrap(
            $"🎁 Tárgyak: {quest.CompletionItemRewardSummary ?? "nem volt tárgyjutalom"}", 78)
            .Select(text => (text, ConsoleColor.Yellow)));
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("Enter / Esc: tovább", ConsoleColor.DarkYellow));
        return lines;
    }

    public static void Show(QuestJournalEntrySnapshot quest)
    {
        var lines = Build(quest);
        var width = Math.Min(Width, Math.Max(20, Console.WindowWidth));
        var height = lines.Count + 2;
        var left = Math.Max(0, (Console.WindowWidth - width) / 2);
        var top = Math.Max(0, (Console.WindowHeight - height) / 2);
        using var background = new BackgroundContentRestorer(left, top, width, height);
        Draw(lines, left, top, width);
        while (Console.ReadKey(intercept: true).Key is not (ConsoleKey.Enter or ConsoleKey.Escape)) { }
    }

    private static void Draw(IReadOnlyList<(string Text, ConsoleColor Color)> lines, int left, int top, int width)
    {
        var style = WindowFrameConfiguration.For(FramedWindow.QuestOffer);
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
