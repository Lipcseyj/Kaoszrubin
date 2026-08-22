namespace MazeGame;

/// <summary>A játékos által már felderített pályacellákat tartja nyilván.</summary>
public sealed class FogOfWar
{
    private const int MaximumBridgedFogGapLength = 3;
    private readonly bool[,] _revealed;
    public int VisionRange { get; }
    public bool IsDeveloperRevealActive { get; private set; }

    public FogOfWar(int width, int height, int visionRange)
    {
        if (visionRange < 0) throw new ArgumentOutOfRangeException(nameof(visionRange));
        _revealed = new bool[width, height];
        VisionRange = visionRange;
    }

    public bool IsRevealed(Position position) =>
        position.X >= 0 && position.X < _revealed.GetLength(0) &&
        position.Y >= 0 && position.Y < _revealed.GetLength(1) && _revealed[position.X, position.Y];

    public bool IsVisible(Position position) => IsDeveloperRevealActive || IsRevealed(position);

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
        BridgeShortFogGaps(maze, newlyRevealed);
        return newlyRevealed;
    }

    /// <summary>Fejlesztői módban ideiglenesen felfedi, majd visszakapcsolva újra elfedi a térképet.</summary>
    public bool ToggleDeveloperReveal()
    {
        IsDeveloperRevealActive = !IsDeveloperRevealActive;
        return IsDeveloperRevealActive;
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
            if ((x != origin.X || y != origin.Y) && maze.BlocksSight(new Position(x, y))) return false;
            var doubleError = 2 * error;
            if (doubleError > -deltaY) { error -= deltaY; x += stepX; }
            if (doubleError < deltaX) { error += deltaX; y += stepY; }
        }
    }

    /// <summary>
    /// Ha egy rövid (legfeljebb három cellás) ködcsík két végét a játékos már
    /// felfedezte, a köztes cellák is ténylegesen felderítődnek. Ez lehet fal vagy járat.
    /// </summary>
    private void BridgeShortFogGaps(Maze maze, ICollection<Position> newlyRevealed)
    {
        var bridgedPositions = new HashSet<Position>();
        for (var y = 0; y < maze.Height; y++)
            FindBridgedGaps(maze, maze.Width, x => new Position(x, y), bridgedPositions);

        for (var x = 0; x < maze.Width; x++)
            FindBridgedGaps(maze, maze.Height, y => new Position(x, y), bridgedPositions);

        foreach (var position in bridgedPositions)
        {
            _revealed[position.X, position.Y] = true;
            newlyRevealed.Add(position);
        }
    }

    private void FindBridgedGaps(Maze maze, int lineLength, Func<int, Position> positionAt, ISet<Position> bridgedPositions)
    {
        var index = 0;
        while (index < lineLength)
        {
            if (IsVisible(positionAt(index)))
            {
                index++;
                continue;
            }

            var start = index;
            while (index < lineLength && !IsVisible(positionAt(index))) index++;
            var gapLength = index - start;
            var hasExploredEnds = start > 0 && index < lineLength && IsRevealed(positionAt(start - 1)) && IsRevealed(positionAt(index));
            var containsDoor = Enumerable.Range(start, gapLength).Any(gapIndex => maze.GetDoorAt(positionAt(gapIndex)) is not null);
            if (!hasExploredEnds || gapLength > MaximumBridgedFogGapLength || containsDoor) continue;

            for (var gapIndex = start; gapIndex < index; gapIndex++)
                bridgedPositions.Add(positionAt(gapIndex));
        }
    }

}
