namespace KaoszRubin.Combat;

public enum StaggerSeverity
{
    Light = 1,
    Normal = 2,
    Heavy = 3
}

public sealed record StaggerActionResolution(StaggerSeverity Severity, int DisruptionChance,
    int Roll, bool BlocksMovement, bool BlocksOffensiveActions);

public sealed record StaggerState(StaggerSeverity? PendingSeverity = null,
    StaggerActionResolution? ActiveResolution = null, long ActiveTurnId = 0);

public sealed record StaggerSnapshot(StaggerSeverity Severity, bool IsResolved,
    bool BlocksMovement, bool? BlocksOffensiveActions);

public static class StaggerRules
{
    public static int DisruptionChance(StaggerSeverity severity) => severity switch
    {
        StaggerSeverity.Light => 25,
        StaggerSeverity.Normal => 45,
        StaggerSeverity.Heavy => 70,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
    };

    public static StaggerActionResolution Resolve(StaggerSeverity severity, int d100Roll)
    {
        var roll = Math.Clamp(d100Roll, 1, 100);
        var chance = DisruptionChance(severity);
        return new StaggerActionResolution(severity, chance, roll, BlocksMovement: true,
            BlocksOffensiveActions: roll <= chance);
    }

    public static StaggerSeverity Stronger(StaggerSeverity first, StaggerSeverity second) =>
        (StaggerSeverity)Math.Max((int)first, (int)second);
}
