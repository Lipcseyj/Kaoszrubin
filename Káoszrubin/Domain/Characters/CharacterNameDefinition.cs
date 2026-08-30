using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Characters;

/// <summary>Osztályhoz kapcsolt, karakterekhez és később NPC-khez is használható név.</summary>
public sealed record CharacterNameDefinition(string Id, string Name, string CharacterClassId) : IGameDefinition;
