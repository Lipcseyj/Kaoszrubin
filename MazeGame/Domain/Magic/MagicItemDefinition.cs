using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Magic;

public sealed record MagicItemDefinition(string Id, string Name) : IItemDefinition
{
    public ItemCategory Category => ItemCategory.MagicItem;
    public string Description => string.Empty;
}
