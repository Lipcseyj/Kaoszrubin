using System.Text;

namespace KaoszRubin.Domain;

public enum TrapEffect { Damage, Poison, Alert, Darkness }

/// <summary>CSV-ből betöltött csapdatípus.</summary>
public sealed record TrapDefinition(string Id, string Name, Rune Symbol, TrapEffect Effect,
    int MinimumLevel, int DetectionDifficulty, int DisarmDifficulty, int MinimumDamage,
    int MaximumDamage, int StatusChancePercent, int DetectionExperience, int DisarmExperience,
    string Description) : IGameDefinition;
