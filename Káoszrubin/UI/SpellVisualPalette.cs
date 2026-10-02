using KaoszRubin.Domain.Magic;

namespace KaoszRubin.UI;

internal static class SpellVisualPalette
{
    public static (ConsoleColor Light, ConsoleColor Dark) Colors(SpellImpactPalette palette) => palette switch
    {
        SpellImpactPalette.Red => (ConsoleColor.Red, ConsoleColor.DarkRed),
        SpellImpactPalette.YellowBrown => (ConsoleColor.Yellow, ConsoleColor.DarkYellow),
        SpellImpactPalette.Purple => (ConsoleColor.Magenta, ConsoleColor.DarkMagenta),
        SpellImpactPalette.SicklyGreen => (ConsoleColor.Green, ConsoleColor.DarkGreen),
        SpellImpactPalette.Shadow => (ConsoleColor.Gray, ConsoleColor.DarkGray),
        SpellImpactPalette.BloodRed => (ConsoleColor.Red, ConsoleColor.DarkMagenta),
        _ => (ConsoleColor.Cyan, ConsoleColor.DarkBlue)
    };
}
