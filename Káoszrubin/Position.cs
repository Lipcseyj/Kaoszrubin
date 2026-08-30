namespace KaoszRubin;

public readonly record struct Position(int X, int Y)
{
    public static Position operator +(Position position, Direction direction) => direction switch
    {
        Direction.Up => new Position(position.X, position.Y - 1),
        Direction.Down => new Position(position.X, position.Y + 1),
        Direction.Left => new Position(position.X - 1, position.Y),
        Direction.Right => new Position(position.X + 1, position.Y),
        _ => position
    };
}
