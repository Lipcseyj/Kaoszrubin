using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Characters;

/// <summary>Egy karakteren megjeleníthető, CSV-ben definiált állapot.</summary>
public sealed record StatusDefinition(
    string Id,
    string Name,
    string Icon,
    int? DefaultDuration,
    int PeriodicDamageMinimum,
    int PeriodicDamageMaximum,
    int PhysicalDamagePenalty,
    int InitiativePenalty,
    int HitPenalty,
    int MaximumVitalityPercent,
    int MaximumManaPercent,
    int VitalityRecoveryPercent,
    int ManaRecoveryPercent,
    int BattleStartVitalityLossPercent,
    int BattleStartManaLossPercent,
    int ZeroNeedMultiplier,
    string Description) : IGameDefinition;

public sealed record StatusTickResult(string Name, string Icon, int Damage, bool Expired);
public sealed record BattleStartStatusResult(int VitalityLost, int ManaLost);

public static class CharacterStatusIds
{
    public const string Hungry = "STATUS001";
    public const string Thirsty = "STATUS002";
    public const string Poisoned = "STATUS003";
    public const string Diseased = "STATUS004";
    public const string Bleeding = "STATUS005";
}
