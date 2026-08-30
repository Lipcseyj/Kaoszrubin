using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Combat;

public sealed record WeaponDefinition(string Id, string Name, string? WeaponTypeId, ValueRange? Damage,
    int MinimumStrength, bool IsTwoHanded, IReadOnlySet<string> AllowedClassIds, string Description, int BasePrice,
    ItemRarity Rarity = ItemRarity.Normal, string? BaseWeaponId = null, int MagicPower = 0) : IItemDefinition
{
    public ItemCategory Category => ItemCategory.Weapon;
    public bool CanBeEquippedBy(string characterClassId, int strength) =>
        AllowedClassIds.Contains(characterClassId) && strength >= MinimumStrength;
}
