using MazeGame.Domain;

namespace MazeGame.Domain.Magic;

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

public sealed record SpellDefinition(string Id, string Name, SpellSchool School, int Level,
    int ManaCost, string Description, SpellTargetType TargetType, int Range, int AreaRadius,
    bool RequiresLineOfSight, SpellUsageMode UsageMode) : IGameDefinition
{
    public bool CanUseInCombat => UsageMode is SpellUsageMode.Combat or SpellUsageMode.Both;
    public bool CanUseDuringExploration => UsageMode is SpellUsageMode.Exploration or SpellUsageMode.Both;
}
