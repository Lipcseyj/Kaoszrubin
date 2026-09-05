using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Combat;

public enum EnemyRank { Normal, Elite, MiniBoss, Boss }

/// <summary>A CSV-ből betöltött ellenféltípus. Az üres statisztikák még nincsenek meghatározva.</summary>
public sealed record EnemyDefinition(string Id, string Name, string Appearance, int? Strength, int? HitPoints,
    int? Armor, int? Speed, int ExperienceReward, int StrengthTier, IReadOnlyList<string> AbilityIds,
    bool IsBoss = false, int VisionRange = 5, int Stealth = 0, int Noise = 2,
    EnemyRank Rank = EnemyRank.Normal, bool CanSleep = true, IReadOnlyList<string>? WeaponIds = null,
    bool ChoosesWeapon = false, DamageResistance? Resistances = null,
    IReadOnlyList<WeaponDefinition>? Weapons = null, WeaponDefinition? Weapon = null,
    EnemyTraits Traits = EnemyTraits.None, int AbilityThreat = 0) : IGameDefinition
{
    public bool HasTrait(EnemyTraits trait) => (Traits & trait) != 0 || trait switch
    {
        EnemyTraits.Undead => AbilityIds.Contains(MonsterAbilityIds.Undead, StringComparer.OrdinalIgnoreCase),
        EnemyTraits.Demonic => AbilityIds.Contains(MonsterAbilityIds.Demonic, StringComparer.OrdinalIgnoreCase),
        EnemyTraits.Flying => AbilityIds.Contains(MonsterAbilityIds.Flying, StringComparer.OrdinalIgnoreCase),
        _ => false
    };

    public bool MatchesAbilityOrLegacyTrait(string id) =>
        AbilityIds.Contains(id, StringComparer.OrdinalIgnoreCase) ||
        string.Equals(id, MonsterAbilityIds.Undead, StringComparison.OrdinalIgnoreCase) && HasTrait(EnemyTraits.Undead) ||
        string.Equals(id, MonsterAbilityIds.Demonic, StringComparison.OrdinalIgnoreCase) && HasTrait(EnemyTraits.Demonic) ||
        string.Equals(id, MonsterAbilityIds.Flying, StringComparison.OrdinalIgnoreCase) && HasTrait(EnemyTraits.Flying);
}
