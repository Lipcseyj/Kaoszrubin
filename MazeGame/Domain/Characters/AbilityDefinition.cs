using MazeGame.Domain;

namespace MazeGame.Domain.Characters;

public sealed record AbilityDefinition(string Id, string Name) : IGameDefinition;
