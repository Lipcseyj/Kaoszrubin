using System.Text;

namespace KaoszRubin.Combat;

/// <summary>Az alsó csatanapló szereplővel kezdődő sorait a támadási összegzés névoszlopához igazítja.</summary>
public static class BattleLogFormatter
{
    public const int ActorColumnWidth = 23;

    public static string Format(string message, IEnumerable<string> actorNames)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(actorNames);
        foreach (var actor in actorNames.Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.Ordinal).OrderByDescending(name => name.Length))
        {
            var start = message.IndexOf(actor, StringComparison.Ordinal);
            if (start < 0 || !IsDecorativePrefix(message[..start])) continue;
            var afterName = start + actor.Length;
            if (afterName < message.Length && message[afterName] is not (' ' or ':' or '\t' or '.' or ','))
                continue;
            var remainder = message[afterName..].TrimStart(' ', ':', '\t');
            // A támadási összegzés már pontosan ugyanebben az oszlopban van.
            if (start == 0 && remainder.StartsWith('→')) return message;
            if (remainder.Length == 0) return message;
            var decoration = message[..start].Trim();
            var action = decoration.Length == 0 ? remainder :
                decoration == "⚔️ Mellékkéz —"
                    ? $"{decoration} {remainder.TrimStart('→', ' ')}"
                    : $"{decoration} {remainder}";
            return $"{BattleSystem.PadRightDisplay(actor, ActorColumnWidth)} → {action}";
        }
        return message;
    }

    private static bool IsDecorativePrefix(string prefix) =>
        (prefix.Length <= 6 && !prefix.EnumerateRunes().Any(rune => Rune.IsLetterOrDigit(rune))) ||
        prefix == "⚔️ Mellékkéz — ";
}
