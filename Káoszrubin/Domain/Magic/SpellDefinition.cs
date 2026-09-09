using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Magic;

public enum SpellSchool
{
    Arcane,
    Divine
}

public enum SpellTargetType
{
    Self,
    Party,
    PartyMember,
    Enemy,
    Corpse,
    Cell,
    Area,
    Direction
}

public enum SpellUsageMode
{
    Exploration,
    Combat,
    Both
}

public enum SpellImpactPalette
{
    Red,
    Blue,
    YellowBrown
}

public sealed record SpellDefinition(string Id, string Name, SpellSchool School, int Level,
    int ManaCost, string Description, SpellTargetType TargetType, int Range, int AreaRadius,
    bool RequiresLineOfSight, SpellUsageMode UsageMode) : IGameDefinition
{
    public SpellImpactPalette ImpactPalette { get; init; } = SpellImpactPalette.Blue;
    public int? ImpactDurationMilliseconds { get; init; }
    public bool HasAreaImpact => TargetType is SpellTargetType.Area or SpellTargetType.Direction;
    public int EffectiveImpactDurationMilliseconds => ImpactDurationMilliseconds ?? (HasAreaImpact ? 3000 : 1500);
    public bool CanUseInCombat => UsageMode is SpellUsageMode.Combat or SpellUsageMode.Both;
    public bool CanUseDuringExploration => UsageMode is SpellUsageMode.Exploration or SpellUsageMode.Both;
}
