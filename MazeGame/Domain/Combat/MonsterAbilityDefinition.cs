using MazeGame.Domain;

namespace MazeGame.Domain.Combat;

/// <summary>CSV-ből konfigurált szörnyképesség és annak csatabeli aktiválási szabálya.</summary>
public sealed record MonsterAbilityDefinition(string Id, string Name, MonsterAbilityEffect Effect,
    int ChancePercent, int Value, string Description) : IGameDefinition;

public enum MonsterAbilityEffect
{
    Trait,
    Poison,
    Disease,
    Bleeding,
    ExtraDamage,
    InitiativeBonus,
    ArmorBonus
}

public static class MonsterAbilityIds
{
    public const string Undead = "MA001";
}
