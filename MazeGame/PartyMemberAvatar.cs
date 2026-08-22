using System.Text;
using MazeGame.Domain.Characters;

namespace MazeGame;

/// <summary>Egy NPC-ként mozgó partitárs térképi megjelenése.</summary>
public sealed class PartyMemberAvatar(Position position, LiveCharacter character) : WorldObject(position)
{
    public LiveCharacter Character { get; } = character;
    public override Rune Symbol { get; } = Rune.GetRuneAt(character.CharacterClass.Name.ToUpperInvariant(), 0);
    public void MoveTo(Position position) => Position = position;
}
