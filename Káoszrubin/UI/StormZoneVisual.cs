namespace KaoszRubin.UI;

/// <summary>Lassan változó, széttöredezett viharminta; a szereplők jele látható marad.</summary>
internal static class StormZoneVisual
{
    public static (ConsoleColor Foreground, ConsoleColor Background, char EmptyGlyph) Cell(
        string spellId, Position position, DateTime utcNow)
    {
        var frame = utcNow.Ticks / TimeSpan.TicksPerSecond;
        var pattern = (int)((position.X * 17L + position.Y * 29L + frame * 3) % 11);
        var background = spellId switch
        {
            "S008" => pattern < 4 ? ConsoleColor.DarkCyan : ConsoleColor.DarkBlue,
            "S009" => pattern < 4 ? ConsoleColor.DarkYellow : ConsoleColor.DarkBlue,
            "S025" => pattern < 4 ? ConsoleColor.DarkMagenta : ConsoleColor.DarkBlue,
            _ => pattern < 4 ? ConsoleColor.DarkGreen : ConsoleColor.DarkMagenta
        };
        var foreground = spellId switch
        {
            "S008" => ConsoleColor.Cyan,
            "S009" => ConsoleColor.Yellow,
            "S025" => ConsoleColor.Magenta,
            _ => ConsoleColor.Green
        };
        return (foreground, background, pattern is 0 or 5 ? '╱' : pattern is 2 or 8 ? '·' : ' ');
    }
}
