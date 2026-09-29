namespace KaoszRubin.World;

/// <summary>Egy pályaképernyő helye a teljes szint absztrakt, kétdimenziós térképén.</summary>
public readonly record struct AreaCoordinate(int X, int Y)
{
    public AreaCoordinate Move(MazeEdge edge) => edge switch
    {
        MazeEdge.Top => this with { Y = Y - 1 },
        MazeEdge.Right => this with { X = X + 1 },
        MazeEdge.Bottom => this with { Y = Y + 1 },
        MazeEdge.Left => this with { X = X - 1 },
        _ => this
    };
}

/// <summary>A képernyő topológiai szerepe; később a tartalom elhelyezése is célozhatja.</summary>
public enum DungeonAreaRole
{
    EntranceAndExit,
    Entrance,
    MainRoute,
    Junction,
    Branch,
    DeadEnd,
    Exit
}

/// <summary>Egy még tartalom nélküli pályaképernyő terve.</summary>
public sealed record DungeonAreaNodePlan(string Id, AreaCoordinate Coordinate, DungeonAreaRole Role);

/// <summary>Két szomszédos képernyő irányhelyes, kétirányú kapcsolata.</summary>
public sealed record DungeonAreaConnectionPlan(
    string FirstAreaId, MazeEdge FirstEdge, string SecondAreaId, MazeEdge SecondEdge);

public sealed record DungeonMapNode(string Id, string Name, AreaCoordinate Coordinate,
    DungeonAreaRole Role, bool IsVisited, bool IsCurrent);

public sealed record DungeonMapEdge(string FirstAreaId, string SecondAreaId);

public sealed record DungeonMapSnapshot(IReadOnlyList<DungeonMapNode> Nodes,
    IReadOnlyList<DungeonMapEdge> Edges, string CurrentAreaId);

/// <summary>A teljes, összefüggő képernyőgráf generálási eredménye.</summary>
public sealed record DungeonAreaGraphPlan(
    IReadOnlyList<DungeonAreaNodePlan> Nodes,
    IReadOnlyList<DungeonAreaConnectionPlan> Connections,
    string EntranceAreaId,
    string ExitAreaId)
{
    /// <summary>A régi egy- és többképernyős pályák kompatibilis, balról jobbra haladó topológiája.</summary>
    public static DungeonAreaGraphPlan Linear(int areaCount)
    {
        if (areaCount < 1) throw new ArgumentOutOfRangeException(nameof(areaCount));
        var nodes = Enumerable.Range(0, areaCount).Select(index => new DungeonAreaNodePlan(
            $"AREA_{index + 1}", new AreaCoordinate(index, 0), index == 0
                ? DungeonAreaRole.Entrance
                : index == areaCount - 1 ? DungeonAreaRole.Exit : DungeonAreaRole.MainRoute)).ToArray();
        if (areaCount == 1) nodes[0] = nodes[0] with { Role = DungeonAreaRole.EntranceAndExit };
        var connections = Enumerable.Range(0, areaCount - 1).Select(index =>
            new DungeonAreaConnectionPlan(nodes[index].Id, MazeEdge.Right,
                nodes[index + 1].Id, MazeEdge.Left)).ToArray();
        return new DungeonAreaGraphPlan(nodes, connections, nodes[0].Id, nodes[^1].Id);
    }
}

/// <summary>A procedurális, térbeli képernyőgráf hangolása.</summary>
public sealed record DungeonAreaGraphConfiguration(
    IntRange AreaCount,
    int MinimumExitDistance = 3,
    int MaximumDegree = 3,
    double BranchChance = 0.45,
    double ExtraConnectionChance = 0.25);

/// <summary>
/// Összefüggő, ortogonális metarácsot készít mellékágakkal és opcionális hurkokkal.
/// Egy kapcsolat iránya a két csomópont koordinátájából következik.
/// </summary>
public static class DungeonAreaGraphGenerator
{
    private static readonly MazeEdge[] Edges = Enum.GetValues<MazeEdge>();

    public static DungeonAreaGraphPlan Generate(DungeonAreaGraphConfiguration configuration, Random random)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(random);
        Validate(configuration);
        var areaCount = configuration.AreaCount.Roll(random);
        for (var attempt = 0; attempt < 128; attempt++)
        {
            var plan = TryGenerate(areaCount, configuration, random);
            if (plan is not null) return plan;
        }
        throw new InvalidOperationException("128 kísérletből sem sikerült megfelelő pályagráfot készíteni.");
    }

    private static DungeonAreaGraphPlan? TryGenerate(int areaCount,
        DungeonAreaGraphConfiguration configuration, Random random)
    {
        var coordinates = new List<AreaCoordinate> { new(0, 0) };
        var coordinateSet = new HashSet<AreaCoordinate>(coordinates);
        var treeEdges = new HashSet<(int First, int Second)>();
        var degrees = new int[areaCount];
        var current = 0;

        for (var index = 1; index < areaCount; index++)
        {
            var parents = Enumerable.Range(0, coordinates.Count)
                .Where(candidate => degrees[candidate] < configuration.MaximumDegree &&
                                    Edges.Any(edge => !coordinateSet.Contains(coordinates[candidate].Move(edge))))
                .ToArray();
            if (parents.Length == 0) return null;
            var continueForward = random.NextDouble() >= configuration.BranchChance && parents.Contains(current);
            var parent = continueForward ? current : parents[random.Next(parents.Length)];
            var availableEdges = Edges.Where(edge => !coordinateSet.Contains(coordinates[parent].Move(edge)))
                .OrderBy(_ => random.Next()).ToArray();
            if (availableEdges.Length == 0) return null;
            var coordinate = coordinates[parent].Move(availableEdges[0]);
            coordinates.Add(coordinate);
            coordinateSet.Add(coordinate);
            treeEdges.Add(Normalize(parent, index));
            degrees[parent]++;
            degrees[index]++;
            current = index;
        }

        var allEdges = new HashSet<(int First, int Second)>(treeEdges);
        for (var first = 0; first < areaCount; first++)
        for (var second = first + 1; second < areaCount; second++)
        {
            if (allEdges.Contains((first, second)) || degrees[first] >= configuration.MaximumDegree ||
                degrees[second] >= configuration.MaximumDegree || !AreAdjacent(coordinates[first], coordinates[second]) ||
                random.NextDouble() >= configuration.ExtraConnectionChance) continue;
            allEdges.Add((first, second));
            degrees[first]++;
            degrees[second]++;
        }

        var distances = Distances(areaCount, allEdges, 0);
        var exitIndex = distances.Select((distance, index) => (distance, index))
            .OrderByDescending(value => value.distance).ThenBy(_ => random.Next()).First().index;
        if (distances[exitIndex] < configuration.MinimumExitDistance) return null;
        var mainRoute = ShortestPath(areaCount, allEdges, 0, exitIndex).ToHashSet();
        var nodes = coordinates.Select((coordinate, index) => new DungeonAreaNodePlan(
            $"AREA_{index + 1}", coordinate, index == 0
                ? DungeonAreaRole.Entrance
                : index == exitIndex ? DungeonAreaRole.Exit
                : degrees[index] >= 3 ? DungeonAreaRole.Junction
                : mainRoute.Contains(index) ? DungeonAreaRole.MainRoute
                : degrees[index] == 1 ? DungeonAreaRole.DeadEnd : DungeonAreaRole.Branch)).ToArray();
        if (areaCount == 1) nodes[0] = nodes[0] with { Role = DungeonAreaRole.EntranceAndExit };
        var connections = allEdges.OrderBy(edge => edge.First).ThenBy(edge => edge.Second).Select(edge =>
        {
            var firstEdge = DirectionFrom(coordinates[edge.First], coordinates[edge.Second]);
            return new DungeonAreaConnectionPlan(nodes[edge.First].Id, firstEdge,
                nodes[edge.Second].Id, Opposite(firstEdge));
        }).ToArray();
        return new DungeonAreaGraphPlan(nodes, connections, nodes[0].Id, nodes[exitIndex].Id);
    }

    private static void Validate(DungeonAreaGraphConfiguration configuration)
    {
        if (configuration.AreaCount.Minimum < 1 ||
            configuration.AreaCount.Maximum < configuration.AreaCount.Minimum)
            throw new ArgumentOutOfRangeException(nameof(configuration), "A területszám-tartomány érvénytelen.");
        if (configuration.MinimumExitDistance < 0 ||
            configuration.MinimumExitDistance >= configuration.AreaCount.Minimum)
            throw new ArgumentOutOfRangeException(nameof(configuration),
                "A minimális kijárattávolság legyen kisebb a minimális területszámnál.");
        if (configuration.MaximumDegree is < 2 or > 4 && configuration.AreaCount.Maximum > 1)
            throw new ArgumentOutOfRangeException(nameof(configuration), "A maximális fokszám 2 és 4 közötti lehet.");
        if (configuration.BranchChance is < 0 or > 1 || configuration.ExtraConnectionChance is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(configuration), "A gráfesélyeknek 0 és 1 közé kell esniük.");
    }

    private static (int First, int Second) Normalize(int first, int second) =>
        first < second ? (first, second) : (second, first);

    private static bool AreAdjacent(AreaCoordinate first, AreaCoordinate second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y) == 1;

    private static MazeEdge DirectionFrom(AreaCoordinate first, AreaCoordinate second) =>
        (second.X - first.X, second.Y - first.Y) switch
        {
            (0, -1) => MazeEdge.Top,
            (1, 0) => MazeEdge.Right,
            (0, 1) => MazeEdge.Bottom,
            (-1, 0) => MazeEdge.Left,
            _ => throw new InvalidOperationException("Csak szomszédos gráfcsomópontok köthetők össze.")
        };

    private static MazeEdge Opposite(MazeEdge edge) => edge switch
    {
        MazeEdge.Top => MazeEdge.Bottom,
        MazeEdge.Right => MazeEdge.Left,
        MazeEdge.Bottom => MazeEdge.Top,
        _ => MazeEdge.Right
    };

    private static int[] Distances(int count, IReadOnlySet<(int First, int Second)> edges, int start)
    {
        var distances = Enumerable.Repeat(-1, count).ToArray();
        distances[start] = 0;
        var pending = new Queue<int>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var current))
            foreach (var next in Neighbors(current, edges))
                if (distances[next] < 0)
                {
                    distances[next] = distances[current] + 1;
                    pending.Enqueue(next);
                }
        return distances;
    }

    private static IEnumerable<int> ShortestPath(int count, IReadOnlySet<(int First, int Second)> edges,
        int start, int destination)
    {
        var previous = Enumerable.Repeat(-1, count).ToArray();
        var visited = new bool[count];
        visited[start] = true;
        var pending = new Queue<int>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var current) && !visited[destination])
            foreach (var next in Neighbors(current, edges))
                if (!visited[next])
                {
                    visited[next] = true;
                    previous[next] = current;
                    pending.Enqueue(next);
                }
        for (var current = destination; current >= 0; current = previous[current]) yield return current;
    }

    private static IEnumerable<int> Neighbors(int node, IEnumerable<(int First, int Second)> edges) =>
        edges.Where(edge => edge.First == node || edge.Second == node)
            .Select(edge => edge.First == node ? edge.Second : edge.First);
}
