using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Characters;

public sealed record AbilityDefinition(string Id, string Name) : IGameDefinition;
