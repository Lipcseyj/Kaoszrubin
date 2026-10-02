using KaoszRubin.Domain.Magic;

namespace KaoszRubin.UI;

/// <summary>Lassan változó, széttöredezett viharminta; a szereplők jele látható marad.</summary>
internal static class StormZoneVisual
{
    public static (ConsoleColor Foreground, ConsoleColor Background, char EmptyGlyph) Cell(
        SpellDefinition? spell, Position position, DateTime utcNow)
    {
        var frame = utcNow.Ticks / TimeSpan.TicksPerSecond;
        var visualPattern = spell?.StormPattern ?? StormVisualPattern.Drift;
        var pattern = visualPattern switch
        {
            StormVisualPattern.Embers => (position.X * 31L + position.Y * 17L + frame * 7) % 13,
            StormVisualPattern.Rain => (position.X * 23L + position.Y * 5L + frame * 4) % 11,
            StormVisualPattern.Crackle => (position.X * 19L + position.Y * 37L + frame * 6) % 11,
            _ => (position.X * 17L + position.Y * 29L + frame * 3) % 11
        };
        var (light, dark) = SpellVisualPalette.Colors(spell?.StormPalette ?? spell?.ImpactPalette ?? SpellImpactPalette.Blue);
        var background = visualPattern switch
        {
            StormVisualPattern.Embers => pattern < 3 ? dark : ConsoleColor.Black,
            StormVisualPattern.Rain => pattern < 5 ? dark : ConsoleColor.Black,
            StormVisualPattern.Crackle => pattern < 4 ? dark : ConsoleColor.Black,
            _ => pattern < 4 ? dark : ConsoleColor.Black
        };
        var glyph = visualPattern switch
        {
            StormVisualPattern.Embers => pattern is 0 or 1 ? '✦' : pattern is 3 or 8 ? '·' : ' ',
            StormVisualPattern.Rain => pattern is 0 or 5 ? '│' : pattern is 2 or 7 ? '·' : ' ',
            StormVisualPattern.Crackle => pattern is 0 or 5 ? '╱' : pattern is 2 or 8 ? '╲' : ' ',
            _ => pattern is 0 or 5 ? '╱' : pattern is 2 or 8 ? '·' : ' '
        };
        return (light, background, glyph);
    }
}
