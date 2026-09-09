namespace KaoszRubin.Combat;

public enum NpcSpellPlanComplexity { Simple, Complex }

public enum NpcSpellPlanStatus { SeekingPosition, ReadyToCast, Failed }

/// <summary>Egy NPC varázshasználó több akción át megőrzött harci szándéka.</summary>
public sealed record NpcSpellPlan(
    Guid Id,
    string SpellId,
    WorldEntityId? TargetEnemyId,
    Position TargetPosition,
    Position? RequiredCastingPosition,
    NpcSpellPlanComplexity Complexity,
    int CreatedInCycle,
    int ExpectedTargetCount,
    double ExpectedUtility,
    NpcSpellPlanStatus Status);
