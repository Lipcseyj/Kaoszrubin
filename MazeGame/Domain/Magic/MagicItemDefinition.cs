using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Magic;

public sealed record MagicItemDefinition(string Id, string Name) : IItemDefinition;
