using KaoszRubin.Domain.Characters;

namespace KaoszRubin;

public sealed class Player
{
    public Position Position { get; private set; }
    public LiveCharacter Character { get; }
    public Player(Position startingPosition, LiveCharacter character)
    {
        Position = startingPosition;
        Character = character;
    }
    public bool TryMove(Direction direction, Maze maze)
    {
        var target = Position + direction;
        if (!maze.IsWalkable(target) || maze.GetObjectAt(target) is PartyMemberAvatar) return false;
        Position = target;
        return true;
    }

    public void TeleportTo(Position position) => Position = position;
}
