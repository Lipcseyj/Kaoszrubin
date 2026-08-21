using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Combat;

public sealed record WeaponDefinition(string Name, string? Type, int? Damage) : IItemDefinition;
