namespace KaoszRubin.UI;

internal enum ConsoleBackdropStyle
{
    Maze,
    Blocks,
    Brick,
    Textile,
    Planks,
    Vault,
    Diamonds,
    Foam,
    Runes,
    Chain
}

internal readonly record struct ConsoleBackdropSelection(ConsoleBackdropStyle Style, ConsoleColor Color);

/// <summary>Deterministic, low-contrast console backgrounds shared by menu-like screens.</summary>
internal static class ConsoleBackdropCatalog
{
    internal static IReadOnlyList<ConsoleBackdropStyle> Styles { get; } =
        Enum.GetValues<ConsoleBackdropStyle>();

    internal static IReadOnlyList<ConsoleColor> Colors { get; } =
    [
        ConsoleColor.DarkBlue,
        ConsoleColor.DarkGreen,
        ConsoleColor.DarkCyan,
        ConsoleColor.DarkRed,
        ConsoleColor.DarkMagenta,
        ConsoleColor.DarkYellow,
        ConsoleColor.DarkGray
    ];

    internal static ConsoleBackdropStyle RandomStyle() =>
        Styles[Random.Shared.Next(Styles.Count)];

    internal static ConsoleBackdropSelection RandomSelection() =>
        new(Styles[Random.Shared.Next(Styles.Count)], Colors[Random.Shared.Next(Colors.Count)]);

    internal static ConsoleBackdropStyle ForInn(string innName, int mazeLevel)
        => ForInnSelection(innName, mazeLevel).Style;

    internal static ConsoleBackdropSelection ForInnSelection(string innName, int mazeLevel)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in innName) hash = hash * 31 + character;
            hash = hash * 31 + mazeLevel;
            var positiveHash = hash & int.MaxValue;
            return new ConsoleBackdropSelection(
                Styles[positiveHash % Styles.Count],
                Colors[positiveHash / Styles.Count % Colors.Count]);
        }
    }

    internal static string BuildRow(ConsoleBackdropStyle style, int y, int width, int xOffset = 0)
    {
        if (width <= 0) return string.Empty;
        return string.Create(width, (style, y, xOffset), static (span, state) =>
        {
            for (var index = 0; index < span.Length; index++)
                span[index] = Glyph(state.style, state.xOffset + index, state.y);
        });
    }

    internal static char Glyph(ConsoleBackdropStyle style, int x, int y) => style switch
    {
        ConsoleBackdropStyle.Maze => MazeGlyph(x, y),
        ConsoleBackdropStyle.Blocks => "░▒▓██▓▒░"[(x / 3 + y / 2 + (x + y) / 17) % 8],
        ConsoleBackdropStyle.Brick => y % 3 == 0 ? '─' : (x + (y / 3 % 2) * 6) % 12 == 0 ? '│' : '·',
        ConsoleBackdropStyle.Textile => (x + y) % 6 == 0 ? '×' : (x - y + 6000) % 6 == 0 ? '·' : ' ',
        ConsoleBackdropStyle.Planks => y % 4 == 0 ? '═' : (x + y * 7) % 29 == 0 ? '·' : ' ',
        ConsoleBackdropStyle.Vault => y % 6 == 0 ? '◠' : (x + y * 2) % 12 == 0 ? '·' : ' ',
        ConsoleBackdropStyle.Diamonds => (x + y * 2) % 10 == 0 ? '◆' : (x - y * 2 + 10000) % 10 == 0 ? '·' : ' ',
        ConsoleBackdropStyle.Foam => (x * 13 + y * 29) % 37 == 0 ? '◦' : (x * 7 + y * 11) % 23 == 0 ? '·' : ' ',
        ConsoleBackdropStyle.Runes => (x + y * 5) % 17 == 0 ? "ᚠᚱᛉᛞᛟ"[(x / 17 + y) % 5] : ' ',
        ConsoleBackdropStyle.Chain => (x + y) % 8 == 0 ? '∞' : (x - y + 8000) % 8 == 0 ? '·' : ' ',
        _ => ' '
    };

    private static char MazeGlyph(int x, int y)
    {
        var horizontal = y % 4 == 0 && ((x / 7 + y / 4 * 3) % 5 != 1);
        var vertical = x % 7 == 0 && ((y / 4 + x / 7 * 2) % 5 != 2);
        if (horizontal && vertical) return '┼';
        if (horizontal) return '─';
        if (vertical) return '│';
        return (x * 17 + y * 31) % 97 == 0 ? '·' : ' ';
    }
}
