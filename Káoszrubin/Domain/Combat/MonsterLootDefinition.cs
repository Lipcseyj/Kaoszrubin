using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Combat;

/// <summary>Egy szörny felszerelés-zsákmányának adatvezérelt korlátai.</summary>
public sealed record MonsterLootDefinition(string EnemyId, int EquipmentChancePercent,
    bool CanDropWeapon, bool CanDropArmor, bool CanDropMagicItem,
    ItemRarity MinimumRarity, ItemRarity MaximumRarity, int MaximumMagicPower, int MaximumBasePrice);

/// <summary>A minden szörnyre érvényes keresési és pénzszabályok.</summary>
public sealed record LootRules(int KeyChancePercent, int GoldChancePercent, int GoldPerStrengthTier,
    int ThiefChanceMultiplierPercent, int IntelligenceChanceBonusPerPoint,
    int ChestJackpotChancePercent, int ChestJackpotMultiplier);
