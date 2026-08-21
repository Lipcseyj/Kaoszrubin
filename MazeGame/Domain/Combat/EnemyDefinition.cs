namespace MazeGame.Domain.Combat;

/// <summary>A CSV-ből betöltött ellenféltípus. Az üres statisztikák még nincsenek meghatározva.</summary>
public sealed record EnemyDefinition(string Name, int? Strength, int? HitPoints, int? Armor, int? Speed);
