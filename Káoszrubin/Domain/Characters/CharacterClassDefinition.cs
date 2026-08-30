using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Characters;

public sealed record CharacterClassDefinition(string Id, string Name, PrimaryAbilities MinimumAbilities, bool UsesMana, double ExperienceModifier) : IGameDefinition;
