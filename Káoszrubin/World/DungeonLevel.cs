namespace KaoszRubin.World;

public enum MazeLayoutStyle { Classic, Wide }

public abstract record MazeLayoutConfiguration(MazeLayoutStyle Style);

public sealed record ClassicMazeLayoutConfiguration(double DoubleWidthCorridorChance = 0.80)
    : MazeLayoutConfiguration(MazeLayoutStyle.Classic);

public sealed record WideMazeLayoutConfiguration(IntRange AreaCount, double NarrowingChance = 0.12)
    : MazeLayoutConfiguration(MazeLayoutStyle.Wide);

public sealed class DungeonArea(string id, Maze maze, FogOfWar fogOfWar)
{
    public string Id { get; } = string.IsNullOrWhiteSpace(id)
        ? throw new ArgumentException("A területazonosító nem lehet üres.", nameof(id))
        : id;
    public Maze Maze { get; } = maze ?? throw new ArgumentNullException(nameof(maze));
    public FogOfWar FogOfWar { get; } = fogOfWar ?? throw new ArgumentNullException(nameof(fogOfWar));
    public Dictionary<Enemy, TimeSpan> EnemyMoveDelays { get; } = [];
    public DateTime? PausedAtUtc { get; set; }
}

public sealed class DungeonLevel
{
    private readonly Dictionary<string, DungeonArea> _areas;

    public IReadOnlyList<DungeonArea> Areas { get; }
    public string ActiveAreaId { get; private set; }
    public DungeonArea ActiveArea => _areas[ActiveAreaId];
    public bool IsMultiArea => Areas.Count > 1;

    public DungeonLevel(IReadOnlyList<DungeonArea> areas, string activeAreaId)
    {
        Areas = areas.Count > 0
            ? areas
            : throw new ArgumentException("A szintnek legalább egy területet tartalmaznia kell.", nameof(areas));
        _areas = areas.ToDictionary(area => area.Id, StringComparer.Ordinal);
        ActiveAreaId = activeAreaId;
        _ = ActiveArea;
        var now = DateTime.UtcNow;
        foreach (var area in Areas)
            area.PausedAtUtc = string.Equals(area.Id, activeAreaId, StringComparison.Ordinal) ? null : now;
    }

    public DungeonArea GetArea(string id) => _areas.TryGetValue(id, out var area)
        ? area
        : throw new KeyNotFoundException($"Ismeretlen terület: {id}.");

    public void Activate(string id)
    {
        _ = GetArea(id);
        ActiveAreaId = id;
    }
}

public sealed record MazePassage(Position Position, string DestinationAreaId, Position DestinationPosition)
{
    public static readonly System.Text.Rune Symbol = new('⇄');
}
