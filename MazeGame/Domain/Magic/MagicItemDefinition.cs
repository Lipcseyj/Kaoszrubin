using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Magic;

public sealed record MagicItemDefinition(string Id, string Name, MagicItemKind Kind, ItemRarity Rarity,
    int BasePrice, int MaximumCharges, string? SpellId, MagicItemEffect Effect, int EffectValue,
    IReadOnlySet<string> AllowedClassIds, string Description, int MagicPower) : IItemDefinition
{
    public ItemCategory Category => ItemCategory.MagicItem;
    public bool CanBeEquippedBy(string characterClassId) => AllowedClassIds.Contains(characterClassId);
}

public enum MagicItemKind { Ring, Amulet, Wand, Scroll }

public enum MagicItemEffect
{
    None, Initiative, Hit, Damage, Defense, BattleHeal, BattleMana,
    Strength, Dexterity, Health, Intelligence
}
