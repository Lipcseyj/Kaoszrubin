namespace MazeGame.Domain.Characters;

/// <summary>Az osztályok CSV-n kívüli alapvető játékszabályai.</summary>
public static class CharacterClassRules
{
    private static readonly HashSet<string> NonManaClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "harcos",
        "barbár",
        "tolvaj"
    };

    public static bool UsesMana(string characterClassName) => !NonManaClassNames.Contains(characterClassName);
}
