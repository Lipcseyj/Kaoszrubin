using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Domain.Combat;

public static class ShieldRules
{
    public const int MaximumCriticalBlockRating = 4;

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

    public static bool IsCriticalBlock(int d20Roll, int blockRating) =>
        blockRating > 0 && d20Roll >= 21 - Math.Clamp(blockRating, 1, MaximumCriticalBlockRating);
}
