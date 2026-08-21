using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Magic;

public sealed record MagicItemDefinition(string Name) : IItemDefinition;
