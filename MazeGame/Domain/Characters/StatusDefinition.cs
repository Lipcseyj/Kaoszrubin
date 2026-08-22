using MazeGame.Domain;

namespace MazeGame.Domain.Characters;

/// <summary>Egy karakteren megjeleníthető, CSV-ben definiált állapot.</summary>
public sealed record StatusDefinition(string Id, string Name, string Description) : IGameDefinition;

public static class CharacterStatusIds
{
    public const string Hungry = "STATUS001";
    public const string Thirsty = "STATUS002";
    public const string Poisoned = "STATUS003";
    public const string Diseased = "STATUS004";
    public const string Bleeding = "STATUS005";
}
