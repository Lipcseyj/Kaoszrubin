using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Combat;

/// <summary>CSV-ből konfigurált szörnyképesség és annak csatabeli aktiválási szabálya.</summary>
public sealed record MonsterAbilityDefinition(string Id, string Name, MonsterAbilityEffect Effect,
    int ChancePercent, int Value, string Description,
    MonsterAbilityTrigger Trigger = MonsterAbilityTrigger.OnHit, int Cooldown = 0, int Range = 1,
    int MaximumTargets = 1, string? StatusId = null, int AiWeight = 100,
    IReadOnlyList<string>? WeaponIds = null, DamageType? DamageType = null) : IGameDefinition;

public enum MonsterAbilityTrigger
{
    Passive,
    OnHit,
    TurnStart,
    Active
}

public enum MonsterAbilityEffect
{
    Trait,
    Poison,
    Disease,
    Bleeding,
    ExtraDamage,
    InitiativeBonus,
    ArmorBonus,
    Regeneration,
    ApplyStatus
}

[Flags]
public enum EnemyTraits
{
    None = 0,
    Undead = 1,
    Demonic = 2,
    Flying = 4
}

public static class MonsterAbilityIds
{
    public const string Undead = "MA001";
    public const string Demonic = "MA010";
    public const string Flying = "MA009";
}
