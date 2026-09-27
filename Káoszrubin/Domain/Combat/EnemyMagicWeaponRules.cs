using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Combat;

public sealed record EnemyMagicWeaponChanceRule(int StrengthTier, double BaseChancePercent,
    double ChancePerLevelPercent, double MaximumChancePercent);

public sealed record EnemyMagicWeaponQualityRule(int MagicPower, int MinimumLevel, int MinimumStrengthTier,
    double BaseWeight, double WeightPerLevel, double WeightPerTier);

public sealed record EnemyMagicWeaponRules(
    IReadOnlyList<EnemyMagicWeaponChanceRule> ChanceRules,
    IReadOnlyList<EnemyMagicWeaponQualityRule> QualityRules)
{
    public static EnemyMagicWeaponRules Disabled { get; } = new([], []);

    public int RollMagicPower(int difficultyLevel, int strengthTier, Random random)
    {
        var chanceRule = ChanceRules.FirstOrDefault(rule => rule.StrengthTier == strengthTier);
        if (chanceRule is null) return 0;
        var level = Math.Max(1, difficultyLevel);
        var chance = Math.Clamp(chanceRule.BaseChancePercent + level * chanceRule.ChancePerLevelPercent,
            0, chanceRule.MaximumChancePercent);
        if (random.NextDouble() * 100 >= chance) return 0;

        var candidates = QualityRules
            .Where(rule => level >= rule.MinimumLevel && strengthTier >= rule.MinimumStrengthTier)
            .Select(rule => (rule.MagicPower, Weight: Math.Max(0,
                rule.BaseWeight + (level - rule.MinimumLevel) * rule.WeightPerLevel +
                (strengthTier - rule.MinimumStrengthTier) * rule.WeightPerTier)))
            .Where(candidate => candidate.Weight > 0)
            .ToArray();
        if (candidates.Length == 0) return 0;
        var roll = random.NextDouble() * candidates.Sum(candidate => candidate.Weight);
        foreach (var candidate in candidates)
        {
            roll -= candidate.Weight;
            if (roll < 0) return candidate.MagicPower;
        }
        return candidates[^1].MagicPower;
    }
}

public sealed record EnemyMagicWeaponContext(int DifficultyLevel, EnemyMagicWeaponRules Rules,
    IReadOnlyList<WeaponDefinition> WeaponCatalog, int? RestoredMagicPower = null)
{
    public int ResolveMagicPower(EnemyDefinition definition, Random random) => RestoredMagicPower ??
        Rules.RollMagicPower(DifficultyLevel, definition.StrengthTier, random);

    public EnemyDefinition Apply(EnemyDefinition definition, int magicPower)
    {
        if (magicPower <= 0 || definition.Weapons is null) return definition;
        WeaponDefinition Upgrade(WeaponDefinition weapon)
        {
            if (weapon.Rarity != ItemRarity.Normal || weapon.IsMonsterOnly ||
                weapon.BaseWeaponId is not null) return weapon;
            return WeaponCatalog.FirstOrDefault(candidate => candidate.MagicPower == magicPower &&
                string.Equals(candidate.BaseWeaponId, weapon.Id, StringComparison.OrdinalIgnoreCase)) ?? weapon;
        }
        return definition with { Weapons = definition.Weapons.Select(Upgrade).ToArray() };
    }
}
