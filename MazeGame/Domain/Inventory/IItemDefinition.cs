using MazeGame.Domain;

namespace MazeGame.Domain.Inventory;

/// <summary>Közös típus a felszerelhető vagy hátizsákba tehető tárgydefiníciókhoz.</summary>
public interface IItemDefinition : IGameDefinition
{
    ItemCategory Category { get; }
}

public enum ItemCategory { Weapon, Armor, MagicItem, Miscellaneous }
public enum InventorySlotKind { Weapon, Armor, MagicItem, Backpack }

public readonly record struct InventorySlotReference(MazeGame.Domain.Characters.LiveCharacter Character, InventorySlotKind Kind, int Index);
