using MazeGame.Domain;

namespace MazeGame.Domain.Magic;

public enum SpellSchool
{
    Arcane,
    Divine
}

public sealed record SpellDefinition(string Id, string Name, SpellSchool School, int Level) : IGameDefinition;
