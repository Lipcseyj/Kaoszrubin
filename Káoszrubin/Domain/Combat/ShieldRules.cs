using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Combat;

public sealed record ShieldDefenseSnapshot(WeaponDefinition Shield,
    WeaponProficiencyRank? Proficiency = null, bool HasShieldWall = false,
    EquipmentCondition Condition = EquipmentCondition.Intact)
{
    public int BlockRating
    {
        get
        {
            var rating = ShieldRules.CriticalBlockRating(Shield, Proficiency, HasShieldWall);
            return Condition == EquipmentCondition.Damaged ? Math.Min(1, rating) : rating;
        }
    }
}

public sealed record ShieldBlockResult(bool Attempted, int Roll, int BlockRating, bool IsCriticalBlock)
{
    public int RequiredRoll => Attempted ? 21 - BlockRating : 0;
    public static ShieldBlockResult NotAttempted => new(false, 0, 0, false);
}

public static class ShieldRules
{
    public const int MaximumCriticalBlockRating = 4;
    public const int BaseStaggerStabilityBonus = 2;

    public static bool IsShield(WeaponDefinition? weapon) =>
        weapon is not null && (string.Equals(weapon.FamilyId, WeaponFamilies.Shield,
            StringComparison.OrdinalIgnoreCase) ||
            string.Equals(weapon.WeaponTypeId, "WT003", StringComparison.OrdinalIgnoreCase));

    public static int CriticalBlockRating(WeaponDefinition? shield,
        WeaponProficiencyRank? proficiency = null, bool hasShieldWall = false)
    {
        if (!IsShield(shield) || shield!.ShieldTier <= 0) return 0;
        var proficiencyBonus = proficiency switch
        {
            WeaponProficiencyRank.Master => 2,
            WeaponProficiencyRank.Trained => 1,
            _ => 0
        };
        return Math.Clamp(shield.ShieldTier + proficiencyBonus + (hasShieldWall ? 1 : 0),
            0, MaximumCriticalBlockRating);
    }

    public static int BashPower(WeaponDefinition? shield,
        WeaponProficiencyRank? proficiency = null, bool hasShieldWall = false)
    {
        if (!IsShield(shield) || shield!.ShieldTier <= 0) return 0;
        var proficiencyBonus = proficiency switch
        {
            WeaponProficiencyRank.Master => 2,
            WeaponProficiencyRank.Trained => 1,
            _ => 0
        };
        return shield.ShieldTier + proficiencyBonus + (hasShieldWall ? 1 : 0);
    }

    /// <summary>A pajzs tömegéből származó lendület- és stabilitásbónusz.</summary>
    public static int StaggerWeightBonus(WeaponDefinition? shield) =>
        IsShield(shield) ? Math.Max(1, (int)Math.Floor(shield!.Weight / 2d)) : 0;

    public static int StaggerStabilityBonus(WeaponDefinition? shield) =>
        IsShield(shield) ? BaseStaggerStabilityBonus + StaggerWeightBonus(shield) : 0;

    public static bool IsCriticalBlock(int d20Roll, int blockRating) =>
        blockRating > 0 && d20Roll >= 21 - Math.Clamp(blockRating, 1, MaximumCriticalBlockRating);

    public static ShieldBlockResult ResolveCriticalBlock(ShieldDefenseSnapshot? shield,
        DamageType damageType, int d20Roll)
    {
        var rating = shield?.BlockRating ?? 0;
        if (!damageType.IsPhysical() || rating <= 0) return ShieldBlockResult.NotAttempted;
        var roll = Math.Clamp(d20Roll, 1, 20);
        return new ShieldBlockResult(true, roll, rating, IsCriticalBlock(roll, rating));
    }
}
