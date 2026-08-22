using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Combat;

public sealed record WeaponDefinition(string Id, string Name, string? WeaponTypeId, ValueRange? Damage, string Description) : IItemDefinition
{
    public ItemCategory Category => ItemCategory.Weapon;
}
