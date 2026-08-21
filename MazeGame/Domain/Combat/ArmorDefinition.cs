using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Combat;

public sealed record ArmorDefinition(string Name, int? Defense) : IItemDefinition;
