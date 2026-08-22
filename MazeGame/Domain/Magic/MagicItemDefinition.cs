using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Magic;

public sealed record MagicItemDefinition(string Id, string Name, int BasePrice) : IItemDefinition
{
    public ItemCategory Category => ItemCategory.MagicItem;
    public string Description => string.Empty;
    public ItemRarity Rarity => ItemRarity.Magic;
    public int MagicPower => 1;
}
