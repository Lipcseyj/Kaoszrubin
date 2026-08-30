using KaoszRubin.Application;

namespace KaoszRubin.UI;

/// <summary>A host és a coop vendég közös szintlépési összegző tartalma.</summary>
public static class LevelUpWindow
{
    public const int Width = 88;

    public static bool UsesSwordFrame(LevelUpPromptKind kind) => kind is
        LevelUpPromptKind.PerkChoice or LevelUpPromptKind.SpecializationChoice or
        LevelUpPromptKind.ClassFeatureChoice or LevelUpPromptKind.AbilityChoice or
        LevelUpPromptKind.WeaponProficiencyChoice;

    public static int ChoiceWidth(LevelUpPromptKind kind) => kind switch
    {
        LevelUpPromptKind.PerkChoice => 112,
        LevelUpPromptKind.SpecializationChoice => 76,
        LevelUpPromptKind.ClassFeatureChoice => 82,
        LevelUpPromptKind.AbilityChoice => 78,
        LevelUpPromptKind.WeaponProficiencyChoice => 86,
        _ => 76
    };

    public static IReadOnlyList<(string Text, ConsoleColor Color)> BuildChoice(LevelUpPromptKind kind,
        IReadOnlyList<LevelUpTextLineSnapshot> contextLines, IReadOnlyList<LevelUpChoiceSnapshot> choices,
        int selectedIndex)
    {
        if (!UsesSwordFrame(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            (kind switch
            {
                LevelUpPromptKind.PerkChoice => "🌟⚔️🌟  TEHETSÉGVÁLASZTÁS  🌟⚔️🌟",
                LevelUpPromptKind.SpecializationChoice => "✨  SPECIALIZÁCIÓ  ✨",
                LevelUpPromptKind.ClassFeatureChoice => "🌟  OSZTÁLYKÉPESSÉG FEJLESZTÉSE  🌟",
                LevelUpPromptKind.AbilityChoice => "💪🏹❤️🧠  KÉPESSÉGPONT  💪🏹❤️🧠",
                LevelUpPromptKind.WeaponProficiencyChoice => "⚔️  FEGYVERJÁRTASSÁG  ⚔️",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            }, ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray)
        };
        lines.AddRange(contextLines.Select(line => (line.Text, line.Color)));
        lines.Add((string.Empty, ConsoleColor.Gray));
        for (var index = 0; index < choices.Count; index++)
        {
            var selected = index == selectedIndex;
            var marker = kind == LevelUpPromptKind.PerkChoice
                ? index == 0 ? "🟥 " : "🟦 "
                : string.Empty;
            lines.Add(($"{(selected ? "▶" : " ")}  {marker}{choices[index].Name}",
                selected ? ConsoleColor.Yellow : ConsoleColor.Gray));
            lines.Add(($"     {choices[index].Description}",
                selected ? ConsoleColor.White : ConsoleColor.DarkGray));
            lines.Add((string.Empty, ConsoleColor.Gray));
        }
        lines.Add((kind switch
        {
            LevelUpPromptKind.PerkChoice => "⬅️  Bal/jobb vagy fel/le: választás     ✅ Enter: véglegesítés",
            LevelUpPromptKind.AbilityChoice => "Nyilak: választás   Enter: növelés",
            _ => "Nyilak: választás   Enter: véglegesítés"
        }, ConsoleColor.Green));
        return lines;
    }

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
