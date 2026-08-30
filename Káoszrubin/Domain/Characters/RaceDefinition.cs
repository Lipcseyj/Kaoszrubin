using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Characters;

[Flags]
public enum RaceTraits
{
    None = 0,
    Adaptable = 1,
    Resilient = 2,
    KeenSenses = 4,
    Relentless = 8
}

public sealed record RaceDefinition(string Id, string Name, PrimaryAbilities AbilityBonuses,
    RaceTraits Traits = RaceTraits.None) : IGameDefinition
{
    public bool HasTrait(RaceTraits trait) => (Traits & trait) != 0;
}
