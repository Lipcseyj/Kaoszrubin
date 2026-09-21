namespace KaoszRubin.World;

public enum MazeEdge { Top, Right, Bottom, Left }

public sealed record EdgePassagePlacement(Position Position, MazeEdge Edge, double RelativeOffset)
{
    public MazeEdge OppositeEdge => Edge switch
    {
        MazeEdge.Top => MazeEdge.Bottom,
        MazeEdge.Right => MazeEdge.Left,
        MazeEdge.Bottom => MazeEdge.Top,
        _ => MazeEdge.Right
    };
}

/// <summary>Egy véletlen külső falmezőt beköt a labirintus járathálózatába.</summary>
public static class MazeEdgePassageCarver
{
    public static EdgePassagePlacement Carve(Maze maze, Random random,
        MazeEdge? requiredEdge = null, double? relativeOffset = null)
    {
        ArgumentNullException.ThrowIfNull(maze);
        ArgumentNullException.ThrowIfNull(random);

        var candidates = EdgeCandidates(maze)
            .Where(candidate => requiredEdge is null || candidate.Edge == requiredEdge)
            .Where(candidate => maze.GetPassageAt(candidate.Position) is null &&
                                maze.GetObjectAt(candidate.Position) is null)
            .OrderBy(candidate => relativeOffset is null
                ? random.NextDouble()
                : Math.Abs(candidate.RelativeOffset - relativeOffset.Value))
            .ThenBy(_ => random.Next())
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException("A pálya szélén nincs szabad hely egy átjáró számára.");

        var passage = candidates[0];
        var inward = InwardDirection(passage.Edge);
        for (var position = passage.Position; maze.IsInside(position); position += inward)
        {
            if (position != passage.Position &&
                (maze.IsWalkable(position) || maze.GetDoorAt(position) is not null)) break;
            maze.Carve(position);
        }

        // Ritka, teljesen tömör sor/oszlop esetén a minimális további faláttöréssel kapcsolódunk.
        var report = maze.EnsureFullAccessibility(() => DoorState.Open);
        if (!report.IsFullyAccessible)
            throw new InvalidOperationException("A szélső átjárót nem sikerült bekötni a járathálózatba.");
        return passage;
    }

    private static IEnumerable<EdgePassagePlacement> EdgeCandidates(Maze maze)
    {
        for (var x = 2; x < maze.Width - 2; x++)
        {
            var offset = x / (double)(maze.Width - 1);
            yield return new EdgePassagePlacement(new Position(x, 0), MazeEdge.Top, offset);
            yield return new EdgePassagePlacement(new Position(x, maze.Height - 1), MazeEdge.Bottom, offset);
        }
        for (var y = 2; y < maze.Height - 2; y++)
        {
            var offset = y / (double)(maze.Height - 1);
            yield return new EdgePassagePlacement(new Position(0, y), MazeEdge.Left, offset);
            yield return new EdgePassagePlacement(new Position(maze.Width - 1, y), MazeEdge.Right, offset);
        }
    }

    private static Direction InwardDirection(MazeEdge edge) => edge switch
    {
        MazeEdge.Top => Direction.Down,
        MazeEdge.Right => Direction.Left,
        MazeEdge.Bottom => Direction.Up,
        _ => Direction.Right
    };
}
