using MazeGame.Domain;

namespace MazeGame.Domain.Characters;

public sealed record RaceDefinition(string Id, string Name, PrimaryAbilities AbilityBonuses) : IGameDefinition;
