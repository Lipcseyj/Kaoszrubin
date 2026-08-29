namespace MazeGame.UI;

/// <summary>A host és a coop vendég közös történetiablak-tartalma.</summary>
public static class NarrativeWindow
{
    public const int Width = 108;
    public const int TextWidth = 100;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Build(string title, string subtitle,
        IReadOnlyList<string> paragraphs, string footer, ConsoleColor footerColor = ConsoleColor.Green)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ($"✦═━─  {title}  ─━═✦", ConsoleColor.Yellow),
            (subtitle, ConsoleColor.Magenta),
            (new string('─', TextWidth), ConsoleColor.DarkMagenta),
            (string.Empty, ConsoleColor.Gray)
        };
        foreach (var paragraph in paragraphs)
        {
            lines.AddRange(Wrap(paragraph, TextWidth).Select(line => (line, ConsoleColor.White)));
            lines.Add((string.Empty, ConsoleColor.Gray));
        }
        lines.Add((footer, footerColor));
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
