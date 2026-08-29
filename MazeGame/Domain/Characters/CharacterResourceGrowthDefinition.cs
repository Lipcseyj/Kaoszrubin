using MazeGame.Domain;

namespace MazeGame.Domain.Characters;

/// <summary>Az osztály szintlépésenkénti HP- és mannanövekedésének módosítói.</summary>
public sealed record CharacterResourceGrowthDefinition(string Id, int VitalityModifier,
    int ManaModifier, int ManaPercentage) : IGameDefinition
{
    public string Name => Id;

    public int AdjustVitality(int rolled) => Math.Max(1, rolled + VitalityModifier);

    public int AdjustMana(int rolled) => ManaPercentage <= 0 ? 0 : Math.Max(1,
        (int)Math.Round(Math.Max(0, rolled + ManaModifier) * ManaPercentage / 100.0,
            MidpointRounding.AwayFromZero));
}
