using KaoszRubin.Domain.Characters;

namespace KaoszRubin.World;

/// <summary>Egy elesett társ karakterhez kötött teste; későbbi feltámasztás célpontja.</summary>
public sealed class PartyMemberCorpse(Position position, LiveCharacter character)
    : Corpse(position, character.Name)
{
    public LiveCharacter Character { get; } = character;
}
