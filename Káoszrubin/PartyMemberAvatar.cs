using System.Text;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin;

/// <summary>Egy NPC-ként mozgó partitárs térképi megjelenése.</summary>
public sealed class PartyMemberAvatar(Position position, LiveCharacter character,
    WorldNpc? temporaryFollower = null) : WorldObject(position)
{
    public LiveCharacter Character { get; } = character;
    public WorldNpc? TemporaryFollower { get; private set; } = temporaryFollower;
    public bool IsTemporaryFollower => TemporaryFollower is not null;
    public override Rune Symbol { get; } = Rune.GetRuneAt(character.CharacterClass.Name.ToUpperInvariant(), 0);
    public void MoveTo(Position position)
    {
        Position = position;
        TemporaryFollower?.MoveTo(position);
    }
    public void MakePermanent() => TemporaryFollower = null;
}
