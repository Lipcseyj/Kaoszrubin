using MazeGame.Domain;

namespace MazeGame.Domain.Characters;

/// <summary>Egy osztályhoz és tehetségfokozathoz tartozó, CSV-ben definiált választható tehetség.</summary>
public sealed record PerkDefinition(string Id, string Name, string Description, string CharacterClassId, int Tier) : IGameDefinition;

/// <summary>Egy szintlépéskor felkínált, egymást kizáró tehetségpár.</summary>
public sealed record PerkOffer(int Tier, int TriggerLevel, IReadOnlyList<PerkDefinition> Choices);
