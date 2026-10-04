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
    YellowBrown,
    Purple,
    SicklyGreen,
    Shadow,
    BloodRed
}

public enum SpellImpactPattern
{
    Ripple,
    Flash,
    Bolt,
    Pillars,
    FallingFlames,
    Halo,
    Ward,
    Vortex
}

public enum StormVisualPattern
{
    Drift,
    Embers,
    Rain,
    Crackle
}

public sealed record SpellDefinition(string Id, string Name, SpellSchool School, int Level,
    int ManaCost, string Description, SpellTargetType TargetType, int Range, int AreaRadius,
    bool RequiresLineOfSight, SpellUsageMode UsageMode) : IGameDefinition
{
    public SpellImpactPalette ImpactPalette { get; init; } = SpellImpactPalette.Blue;
    public SpellImpactPattern ImpactPattern { get; init; } = SpellImpactPattern.Ripple;
    public StormVisualPattern StormPattern { get; init; } = StormVisualPattern.Drift;
    public SpellImpactPalette? StormPalette { get; init; }
    public int? ImpactDurationMilliseconds { get; init; }
    public bool EnemyOnly { get; init; }
    public bool ExcludesUndead { get; init; }
    public string LogEmoji { get; init; } = "✨";
    public bool HasAreaImpact => TargetType is SpellTargetType.Area or SpellTargetType.Direction;
    public int EffectiveImpactDurationMilliseconds => ImpactDurationMilliseconds ?? (HasAreaImpact ? 3000 : 1500);
    public bool CanUseInCombat => UsageMode is SpellUsageMode.Combat or SpellUsageMode.Both;
    public bool CanUseDuringExploration => UsageMode is SpellUsageMode.Exploration or SpellUsageMode.Both;
}
