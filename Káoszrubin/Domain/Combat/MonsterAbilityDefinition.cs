using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Combat;

/// <summary>CSV-ből konfigurált szörnyképesség és annak csatabeli aktiválási szabálya.</summary>
public sealed record MonsterAbilityDefinition(string Id, string Name, string Description,
    MonsterAbilityTrigger Trigger, MonsterAbilityResolutionMode ResolutionMode,
    MonsterAbilityTargeting Targeting, int ChancePercent = 100, int Cooldown = 0, int Range = 1,
    int MaximumTargets = 1, int AiWeight = 100, IReadOnlyList<string>? WeaponIds = null,
    int ChargesPerBattle = 0, bool RequiresLineOfSight = false, int PreparationTurns = 0,
    int AttackCount = 1, string? AbilityGroup = null, int RetreatStepsAfterUse = 0,
    IReadOnlyList<MonsterAbilityComponent>? ConfiguredEffects = null) : IGameDefinition
{
    public IReadOnlyList<MonsterAbilityComponent> Effects => ConfiguredEffects ?? [];
    public MonsterAbilityEffect Effect => Effects.FirstOrDefault()?.Effect ?? MonsterAbilityEffect.Trait;
    public int Value => Effects.FirstOrDefault()?.Value ?? 0;
    public string? StatusId => Effects.FirstOrDefault()?.StatusId;
    public DamageType? DamageType => Effects.FirstOrDefault()?.DamageType;
    public IReadOnlyList<MonsterAbilityComponent> AdditionalEffects => Effects.Skip(1).ToArray();
    public bool UsesRangedAttackRoll => ResolutionMode == MonsterAbilityResolutionMode.AbilityAttack;
}

public sealed record MonsterAbilityComponent(MonsterAbilityEffect Effect, int Value = 0,
    string? StatusId = null, DamageType? DamageType = null, ValueRange? Dice = null,
    int ChancePercent = 100, MonsterResistanceAbility ResistanceAbility = MonsterResistanceAbility.None,
    int ResistanceDifficulty = 0, int Duration = 0);

public sealed record MonsterAbilityEffectRow(string AbilityId, int Order, MonsterAbilityComponent Component);

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
    ApplyStatus,
    Stagger
}

public enum MonsterAbilityResolutionMode
{
    WeaponAttack,
    AbilityAttack,
    SavingThrow,
    Automatic
}

public enum MonsterAbilityTargeting
{
    Self,
    SingleEnemy,
    MultipleEnemies,
    Area,
    Ally,
    EmptyPosition
}

public enum MonsterResistanceAbility
{
    None,
    Strength,
    Dexterity,
    Health,
    Intelligence
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
