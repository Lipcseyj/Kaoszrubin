using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Combat;

public sealed record ArmorDefinition(string Id, string Name, ValueRange? Defense,
    IReadOnlySet<string> AllowedClassIds, string Description) : IItemDefinition
{
    public ItemCategory Category => ItemCategory.Armor;
    public bool CanBeEquippedBy(string characterClassId) => AllowedClassIds.Contains(characterClassId);
}
