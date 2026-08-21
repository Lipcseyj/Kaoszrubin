namespace MazeGame.Domain.Inventory;

/// <summary>Általános, még részletes statisztika nélküli tárgy (például élelem vagy kulacs).</summary>
public sealed record MiscItemDefinition(string Id, string Name) : IItemDefinition;
