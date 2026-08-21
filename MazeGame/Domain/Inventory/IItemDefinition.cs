namespace MazeGame.Domain.Inventory;

/// <summary>Közös típus a felszerelhető vagy hátizsákba tehető tárgydefiníciókhoz.</summary>
public interface IItemDefinition
{
    string Name { get; }
}
