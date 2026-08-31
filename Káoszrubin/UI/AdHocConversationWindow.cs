using KaoszRubin.Application;

namespace KaoszRubin.UI;

public static class AdHocConversationWindow
{
    public const int Width = 88;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Build(AdHocConversationSnapshot conversation)
    {
        var lines = new List<(string, ConsoleColor)>
        {
            ($"⚜ {conversation.CharacterName.ToUpperInvariant()} — ÚTKÖZBEN", ConsoleColor.Yellow),
            ($"{conversation.RaceName} {conversation.CharacterClassName}", ConsoleColor.Cyan),
            (string.Empty, ConsoleColor.Gray)
        };
        foreach (var entry in conversation.Transcript.TakeLast(8))
            lines.AddRange(MessageTextLayout.Wrap(entry, 80).Select(part =>
                (part, entry.StartsWith("Te:", StringComparison.OrdinalIgnoreCase)
                    ? ConsoleColor.Yellow : ConsoleColor.Gray)));
        if (conversation.Transcript.Count > 0) lines.Add((string.Empty, ConsoleColor.Gray));
        if (!string.IsNullOrWhiteSpace(conversation.Prompt))
            lines.AddRange(conversation.Prompt.Split('|', StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .SelectMany(part => MessageTextLayout.Wrap($"„{part}”", 80))
                .Select(part => (part, ConsoleColor.White)));
        if (conversation.Choices.Count > 0)
        {
            lines.Add((string.Empty, ConsoleColor.Gray));
            foreach (var (choice, index) in conversation.Choices.Select((value, index) => (value, index)))
                lines.AddRange(MessageTextLayout.Wrap($"{index + 1}) {choice}", 80)
                    .Select(part => (part, ConsoleColor.DarkYellow)));
        }
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add((conversation.Choices.Count > 0
            ? "A host választja ki a választ…"
            : "A host folytatja a beszélgetést…", ConsoleColor.DarkCyan));
        return lines;
    }
}
