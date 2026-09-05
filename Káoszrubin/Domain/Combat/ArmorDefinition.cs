using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Combat;

public sealed record ArmorDefinition(string Id, string Name, ValueRange? Defense,
    IReadOnlySet<string> AllowedClassIds, string Description, int BasePrice,
    ItemRarity Rarity = ItemRarity.Normal, string? BaseArmorId = null, int MagicPower = 0, double Weight = 1,
    DamageResistance? Resistances = null) : IItemDefinition
{
    public ItemCategory Category => ItemCategory.Armor;
    public bool CanBeEquippedBy(string characterClassId) => AllowedClassIds.Contains(characterClassId);
}
