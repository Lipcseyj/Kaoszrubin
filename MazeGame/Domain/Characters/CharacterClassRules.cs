namespace MazeGame.Domain.Characters;

/// <summary>Az osztályok CSV-n kívüli alapvető játékszabályai.</summary>
public static class CharacterClassRules
{
    private static readonly HashSet<string> NonManaClassIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "C001",
        "C002",
        "C004"
    };

    public static bool UsesMana(string characterClassId) => !NonManaClassIds.Contains(characterClassId);
    public static bool IsThief(string characterClassId) => string.Equals(characterClassId, "C004", StringComparison.OrdinalIgnoreCase);
}
