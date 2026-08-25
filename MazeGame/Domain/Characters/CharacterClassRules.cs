namespace MazeGame.Domain.Characters;

/// <summary>Az osztályok CSV-n kívüli alapvető játékszabályai.</summary>
public static class CharacterClassRules
{
    private static readonly HashSet<string> NonManaClassIds = new(StringComparer.OrdinalIgnoreCase)
    {
        CharacterClassIds.Harcos,
        CharacterClassIds.Barbár,
        CharacterClassIds.Tolvaj
    };

    public static bool UsesMana(string characterClassId) => !NonManaClassIds.Contains(characterClassId);
    public static bool IsThief(string characterClassId) => string.Equals(characterClassId, CharacterClassIds.Tolvaj, StringComparison.OrdinalIgnoreCase);

    public static int AdjustStartingMana(string characterClassId, int mana) => characterClassId switch
    {
        CharacterClassIds.Pap => ScaleMana(mana, 90),
        CharacterClassIds.Lovag => ScaleMana(mana, 50),
        _ => Math.Max(0, mana)
    };

    public static int AdjustManaGrowth(string characterClassId, int manaGrowth) =>
        characterClassId == CharacterClassIds.Lovag ? ScaleMana(manaGrowth, 50) : Math.Max(0, manaGrowth);

    private static int ScaleMana(int mana, int percentage) => mana <= 0
        ? 0
        : Math.Max(1, (int)Math.Round(mana * percentage / 100.0, MidpointRounding.AwayFromZero));
}
