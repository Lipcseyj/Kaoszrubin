namespace MazeGame;

/// <summary>Mélységi bejárással teljesen összefüggő labirintust generál.</summary>
public sealed class MazeGenerator
{
    private static readonly Direction[] Directions = Enum.GetValues<Direction>();
    private readonly Random _random = new();

    public Maze Create(int width, int height)
    {
        var maze = new Maze(width, height);
        CarveFrom(maze, maze.Entrance);
        maze.PlaceExit();
        return maze;
    }

    private void CarveFrom(Maze maze, Position current)
    {
        maze.Carve(current);
        foreach (var direction in Directions.OrderBy(_ => _random.Next()))
        {
            var next = current + direction + direction;
            if (!IsInnerCell(maze, next) || maze.IsWalkable(next)) continue;
            maze.Carve(current + direction);
            CarveFrom(maze, next);
        }
    }

    private static bool IsInnerCell(Maze maze, Position position) => position.X > 0 && position.X < maze.Width - 1 && position.Y > 0 && position.Y < maze.Height - 1;
}
