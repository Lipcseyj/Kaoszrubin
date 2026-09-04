namespace KaoszRubin.Combat;

/// <summary>Host-produced action result; presentation never rerolls or parses the short combat log.</summary>
public sealed record BattleActionDetails(Guid Id, string Actor, string Target,
    IReadOnlyList<string> Summary, IReadOnlyList<string> Calculation);

public sealed record AttackDetails(string Hit, int Damage, double CriticalChancePercent,
    int CriticalMultiplier, IReadOnlyList<string> Calculation);
