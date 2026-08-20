namespace MazeGame;

public sealed class Player
{
    public Position Position { get; private set; }
    public Player(Position startingPosition) => Position = startingPosition;
    public bool TryMove(Direction direction, Maze maze)
    {
        var target = Position + direction;
        if (!maze.IsWalkable(target)) return false;
        Position = target;
        return true;
    }
}
