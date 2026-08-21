using MazeGame.Domain.Characters;

namespace MazeGame;

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
        if (!maze.IsWalkable(target)) return false;
        Position = target;
        return true;
    }
}
