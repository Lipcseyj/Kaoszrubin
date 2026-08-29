using MazeGame.Application;

namespace MazeGame.UI;

/// <summary>A host és a coop vendég közös szintlépési összegző tartalma.</summary>
public static class LevelUpWindow
{
    public const int Width = 88;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> BuildSummary(string characterName,
        int previousLevel, int currentLevel, IReadOnlyList<LevelUpBonusSnapshot> bonuses,
        int vitalityGained, int manaGained, bool usesMana, int currentVitality, int maximumVitality,
        int currentMana, int maximumMana, string continueMessage)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("✨🏆✨  SZINTLÉPÉS!  ✨🏆✨", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ($"⚔️  {characterName} új ereje felébredt!", ConsoleColor.Cyan),
            ($"📜  {previousLevel}. szint  ➜  {currentLevel}. szint", ConsoleColor.Magenta),
            (string.Empty, ConsoleColor.Gray)
        };
        lines.AddRange(bonuses.Select(bonus =>
            ($"⭐ {bonus.Level}. szint:  ❤️ +{bonus.Vitality} HP" +
             (usesMana ? $"     🔷 +{bonus.Mana} manna" : string.Empty), ConsoleColor.Green)));
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add((usesMana
            ? $"💖 Összes növekedés: +{vitalityGained} HP   💠 +{manaGained} manna"
            : $"💖 Összes növekedés: +{vitalityGained} HP", ConsoleColor.White));
        lines.Add(($"🛡️  Jelenlegi értékek: {currentVitality}/{maximumVitality} HP" +
                   (usesMana ? $"   {currentMana}/{maximumMana} manna" : string.Empty), ConsoleColor.Cyan));
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add((continueMessage, ConsoleColor.Yellow));
        return lines;
    }
}
