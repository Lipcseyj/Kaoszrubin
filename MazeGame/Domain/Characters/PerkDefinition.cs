using MazeGame.Domain;

namespace MazeGame.Domain.Characters;

/// <summary>Egy osztályhoz és tehetségfokozathoz tartozó, CSV-ben definiált választható tehetség.</summary>
public sealed record PerkDefinition(string Id, string Name, string Description, string CharacterClassId, int Tier) : IGameDefinition;

/// <summary>Egy szintlépéskor felkínált, egymást kizáró tehetségpár.</summary>
public sealed record PerkOffer(int Tier, int TriggerLevel, IReadOnlyList<PerkDefinition> Choices);

public static class PerkProgressionRules
{
    private static readonly int[] Milestones = [5, 15, 25];

    public static int TriggerLevel(RaceDefinition race, int tier)
    {
        if (tier is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(tier));
        return tier == 1 && race.HasTrait(RaceTraits.Adaptable) ? 4 : Milestones[tier - 1];
    }
}

/// <summary>A beépített tehetséghatások stabil CSV-azonosítói.</summary>
public static class PerkIds
{
    public const string FighterFirstStrike = "PERK-C001-1A";
    public const string FighterRobustness = "PERK-C001-1B";
    public const string FighterWeaponMaster = "PERK-C001-2A";
    public const string FighterUnbreakable = "PERK-C001-2B";
    public const string FighterSteelStorm = "PERK-C001-3A";
    public const string FighterLastFortress = "PERK-C001-3B";
    public const string BarbarianBloodlust = "PERK-C002-1A";
    public const string BarbarianThickSkin = "PERK-C002-1B";
    public const string BarbarianRage = "PERK-C002-2A";
    public const string BarbarianPainTolerance = "PERK-C002-2B";
    public const string BarbarianBerserkerRage = "PERK-C002-3A";
    public const string BarbarianPrimalStrength = "PERK-C002-3B";
    public const string KnightShieldWall = "PERK-C003-1A";
    public const string KnightChallenge = "PERK-C003-1B";
    public const string KnightArmorMaster = "PERK-C003-2A";
    public const string KnightHolyOath = "PERK-C003-2B";
    public const string KnightGuardianAngel = "PERK-C003-3A";
    public const string KnightInvincible = "PERK-C003-3B";
    public const string ThiefAmbush = "PERK-C004-1A";
    public const string ThiefEvasion = "PERK-C004-1B";
    public const string ThiefPoisoner = "PERK-C004-2A";
    public const string ThiefShadowStep = "PERK-C004-2B";
    public const string ThiefDeadlyAccuracy = "PERK-C004-3A";
    public const string ThiefMasterThief = "PERK-C004-3B";
    public const string PriestHealingGrace = "PERK-C005-1A";
    public const string PriestBlessedWeapon = "PERK-C005-1B";
    public const string PriestSanctuary = "PERK-C005-2A";
    public const string PriestFaithSource = "PERK-C005-2B";
    public const string PriestResurrection = "PERK-C005-3A";
    public const string PriestDivineJudgment = "PERK-C005-3B";
    public const string MageArcaneFocus = "PERK-C006-1A";
    public const string MageManaReserve = "PERK-C006-1B";
    public const string MageElementalMaster = "PERK-C006-2A";
    public const string MageMagicShield = "PERK-C006-2B";
    public const string MageChainSpell = "PERK-C006-3A";
    public const string MageArchmage = "PERK-C006-3B";
}
