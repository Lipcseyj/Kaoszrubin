namespace MazeGame.Domain.Magic;

public enum SpellSchool
{
    Arcane,
    Divine
}

public sealed record SpellDefinition(string Name, SpellSchool School);
