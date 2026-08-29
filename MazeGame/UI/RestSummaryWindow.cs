using MazeGame.Application;

namespace MazeGame.UI;

public static class RestSummaryWindow
{
    public const int Width = 100;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Build(PartyRestSnapshot rest,
        string footer, ConsoleColor footerColor = ConsoleColor.Green)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            (rest.AtInn ? "🛏️💤  FOGADÓI PIHENÉS  💤🛏️" : "🏕️💤  TÁBORI PIHENÉS  💤🏕️", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            (rest.AtInn
                ? "A parti kényelmes ágyakban pihente ki az út fáradalmait."
                : "A parti biztonságba zárta a szobát, majd rövid pihenőt tartott.", ConsoleColor.Cyan),
            (string.Empty, ConsoleColor.Gray),
            ("💤 Regenerálódás és megszűnt állapotok:", ConsoleColor.Green)
        };

        foreach (var character in rest.Characters)
        {
            var mana = character.UsesMana
                ? $"   🔷+{character.ManaRestored} ({character.CurrentMana}/{character.MaximumMana})"
                : string.Empty;
            lines.Add(($"❤️ {character.CharacterName,-13} +{character.HealedAmount} " +
                       $"({character.CurrentVitality}/{character.MaximumVitality}){mana}", character.Color));
            lines.Add((character.RemovedNegativeStatuses.Count > 0
                ? $"   ✨ Megszűnt: {string.Join(", ", character.RemovedNegativeStatuses)}"
                : "   ✨ Megszűnt állapot: nincs", ConsoleColor.DarkCyan));
        }

        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add((footer, footerColor));
        return lines;
    }
}
