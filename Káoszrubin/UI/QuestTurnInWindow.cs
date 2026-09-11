using KaoszRubin.Domain;

namespace KaoszRubin.UI;

/// <summary>Küldetésleadás előtti, közösen látható megerősítő ablak tartalma.</summary>
public static class QuestTurnInWindow
{
    public const int Width = 92;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Build(string npcName,
        NpcQuestDefinition quest, int progress, int requiredCount, string rewardItemsText)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("📜 KÜLDETÉS LEADÁSA", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ($"Megbízó: {npcName}", ConsoleColor.Cyan),
            ($"Küldetés: {quest.Title}", ConsoleColor.White),
            (string.Empty, ConsoleColor.Gray),
            ("🎯 FELTÉTELEK", ConsoleColor.Magenta)
        };

        lines.AddRange(MessageTextLayout.Wrap(quest.Description, 82)
            .Select(text => ($"   {text}", ConsoleColor.Gray)));

        lines.Add(($"Állapot: {Math.Clamp(progress, 0, requiredCount)}/{requiredCount}",
            progress >= requiredCount ? ConsoleColor.Green : ConsoleColor.DarkYellow));

        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("🎁 JUTALOM", ConsoleColor.Cyan));
        lines.Add(($"⭐ Tapasztalat: {quest.ExperienceReward} XP", ConsoleColor.Cyan));
        lines.Add(($"🎁 Tárgyak: {rewardItemsText}", ConsoleColor.Yellow));
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("Enter: leadás és jutalom felvétele", ConsoleColor.Green));
        lines.Add(("Esc: most még halasztom", ConsoleColor.DarkYellow));
        return lines;
    }
}
