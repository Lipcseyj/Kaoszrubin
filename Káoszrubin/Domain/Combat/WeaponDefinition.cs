using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Combat;

public sealed record WeaponDefinition(string Id, string Name, string? WeaponTypeId, ValueRange? Damage,
    int MinimumStrength, bool IsTwoHanded, IReadOnlySet<string> AllowedClassIds, string Description, int BasePrice,
    ItemRarity Rarity = ItemRarity.Normal, string? BaseWeaponId = null, int MagicPower = 0, double Weight = 1,
    DamageType DamageType = DamageType.Bludgeoning, int MaximumTargets = 1, bool CanAttackFromRear = false,
    string? FamilyId = null) : IItemDefinition
{
    public ItemCategory Category => ItemCategory.Weapon;
    public bool IsMonsterOnly => BasePrice <= 0 || FamilyId == "NATURAL";
    public bool CanBeEquippedBy(string characterClassId, int strength) =>
        !IsMonsterOnly && AllowedClassIds.Contains(characterClassId) && strength >= MinimumStrength;
}
