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
}
