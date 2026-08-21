using MazeGame.Domain;

namespace MazeGame.Domain.Characters;

public sealed record CharacterClassDefinition(string Id, string Name, PrimaryAbilities MinimumAbilities, bool UsesMana) : IGameDefinition;
