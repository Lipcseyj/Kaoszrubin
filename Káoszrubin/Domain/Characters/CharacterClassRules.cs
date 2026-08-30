namespace KaoszRubin.Domain.Characters;

/// <summary>Az osztályok CSV-n kívüli alapvető játékszabályai.</summary>
public static class CharacterClassRules
{
    public const int BaseVisionRange = 5;
    public const int MaximumVisionRange = 8;

    public static int VisionRange(LiveCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var range = BaseVisionRange;
        if (IsThief(character.CharacterClass.Id)) range += 2;
        if (character.Race.HasTrait(RaceTraits.KeenSenses)) range++;
        return Math.Clamp(range, 2, MaximumVisionRange);
    }

    public static bool IsMartial(string characterClassId) => characterClassId is
        CharacterClassIds.Harcos or CharacterClassIds.Barbár or CharacterClassIds.Lovag;
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

    private static int ScaleMana(int mana, int percentage) => mana <= 0
        ? 0
        : Math.Max(1, (int)Math.Round(mana * percentage / 100.0, MidpointRounding.AwayFromZero));
}
