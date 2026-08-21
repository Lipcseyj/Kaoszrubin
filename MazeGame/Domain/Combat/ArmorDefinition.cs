using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Combat;

public sealed record ArmorDefinition(string Id, string Name, int? Defense) : IItemDefinition;
