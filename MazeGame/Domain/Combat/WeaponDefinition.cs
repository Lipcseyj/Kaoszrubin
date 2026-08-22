using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Combat;

public sealed record WeaponDefinition(string Id, string Name, string? WeaponTypeId, ValueRange? Damage,
    bool IsTwoHanded, IReadOnlySet<string> AllowedClassIds, string Description, int BasePrice) : IItemDefinition
{
    public ItemCategory Category => ItemCategory.Weapon;
    public bool CanBeEquippedBy(string characterClassId) => AllowedClassIds.Contains(characterClassId);
}
