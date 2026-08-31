using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Combat;

public enum EnemyRank { Normal, Elite, MiniBoss, Boss }

/// <summary>A CSV-ből betöltött ellenféltípus. Az üres statisztikák még nincsenek meghatározva.</summary>
public sealed record EnemyDefinition(string Id, string Name, string Appearance, int? Strength, int? HitPoints,
    int? Armor, int? Speed, int ExperienceReward, int StrengthTier, IReadOnlyList<string> AbilityIds,
    bool IsBoss = false, int VisionRange = 5, int Stealth = 0, int Noise = 2,
    EnemyRank Rank = EnemyRank.Normal) : IGameDefinition;
