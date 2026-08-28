namespace MazeGame.Domain;

/// <summary>A fogadóban hallható, játékmenettől független hangulatpletyka.</summary>
public sealed record InnRumorDefinition(string Id, string Name) : IGameDefinition;
