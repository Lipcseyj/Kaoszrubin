namespace MazeGame;

/// <summary>A játékos által már felderített pályacellákat tartja nyilván.</summary>
public sealed class FogOfWar
{
    private readonly bool[,] _revealed;
    public int VisionRange { get; }

    public FogOfWar(int width, int height, int visionRange)
    {
        if (visionRange < 0) throw new ArgumentOutOfRangeException(nameof(visionRange));
        _revealed = new bool[width, height];
        VisionRange = visionRange;
    }

    public bool IsRevealed(Position position) =>
        position.X >= 0 && position.X < _revealed.GetLength(0) &&
        position.Y >= 0 && position.Y < _revealed.GetLength(1) && _revealed[position.X, position.Y];

    public IReadOnlyList<Position> RevealFrom(Maze maze, Position origin)
    {
        var newlyRevealed = new List<Position>();
        for (var y = origin.Y - VisionRange; y <= origin.Y + VisionRange; y++)
        for (var x = origin.X - VisionRange; x <= origin.X + VisionRange; x++)
        {
            var target = new Position(x, y);
            if (!maze.IsInside(target) || Math.Max(Math.Abs(x - origin.X), Math.Abs(y - origin.Y)) > VisionRange) continue;
            if (!HasLineOfSight(maze, origin, target) || _revealed[x, y]) continue;
            _revealed[x, y] = true;
            newlyRevealed.Add(target);
        }
        return newlyRevealed;
    }

    private static bool HasLineOfSight(Maze maze, Position origin, Position target)
    {
        var x = origin.X;
        var y = origin.Y;
        var deltaX = Math.Abs(target.X - origin.X);
        var deltaY = Math.Abs(target.Y - origin.Y);
        var stepX = origin.X < target.X ? 1 : -1;
        var stepY = origin.Y < target.Y ? 1 : -1;
        var error = deltaX - deltaY;

        while (true)
        {
            if (x == target.X && y == target.Y) return true;
            if ((x != origin.X || y != origin.Y) && maze.Tiles[x, y] == Maze.Wall) return false;
            var doubleError = 2 * error;
            if (doubleError > -deltaY) { error -= deltaY; x += stepX; }
            if (doubleError < deltaX) { error += deltaX; y += stepY; }
        }
    }
}
