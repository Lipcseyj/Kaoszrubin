namespace MazeGame.Domain;

/// <summary>Egy CSV-ben meghatározott, stabil azonosítóval rendelkező játékelem.</summary>
public interface IGameDefinition
{
    string Id { get; }
    string Name { get; }
}
