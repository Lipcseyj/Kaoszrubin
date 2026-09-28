namespace KaoszRubin.World;

public enum MazeLayoutStyle { Classic, Wide, Forest }

public abstract record MazeLayoutConfiguration(MazeLayoutStyle Style);

public sealed record ClassicMazeLayoutConfiguration(double DoubleWidthCorridorChance = 0.80)
    : MazeLayoutConfiguration(MazeLayoutStyle.Classic);

public sealed record WideMazeLayoutConfiguration(IntRange AreaCount, double NarrowingChance = 0.12)
    : MazeLayoutConfiguration(MazeLayoutStyle.Wide);

/// <summary>
/// Többképernyős, gráf szerkezetű erdei pálya. A Graph a képernyők kapcsolatát, a Forest az egyes
/// képernyők növényzetét és vizes területeit szabályozza.
/// </summary>
public sealed record ForestMazeLayoutConfiguration(
    DungeonAreaGraphConfiguration Graph,
    ForestGenerationConfiguration Forest)
    : MazeLayoutConfiguration(MazeLayoutStyle.Forest);

public sealed class DungeonArea(string id, Maze maze, FogOfWar fogOfWar,
    string? name = null, AreaCoordinate? coordinate = null, DungeonAreaRole role = DungeonAreaRole.MainRoute)
{
    public string Id { get; } = string.IsNullOrWhiteSpace(id)
        ? throw new ArgumentException("A területazonosító nem lehet üres.", nameof(id))
        : id;
    public Maze Maze { get; } = maze ?? throw new ArgumentNullException(nameof(maze));
    public FogOfWar FogOfWar { get; } = fogOfWar ?? throw new ArgumentNullException(nameof(fogOfWar));
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? maze.LevelName : name;
    public AreaCoordinate Coordinate { get; } = coordinate ?? new AreaCoordinate(0, 0);
    public DungeonAreaRole Role { get; } = role;
    public Dictionary<Enemy, TimeSpan> EnemyMoveDelays { get; } = [];
    public DateTime? PausedAtUtc { get; set; }
}

public sealed class DungeonLevel
{
    private readonly Dictionary<string, DungeonArea> _areas;

    public IReadOnlyList<DungeonArea> Areas { get; }
    public string EntranceAreaId { get; }
    public string ExitAreaId { get; }
    public string ActiveAreaId { get; private set; }
    public DungeonArea ActiveArea => _areas[ActiveAreaId];
    public DungeonArea EntranceArea => _areas[EntranceAreaId];
    public DungeonArea ExitArea => _areas[ExitAreaId];
    public bool IsMultiArea => Areas.Count > 1;

    public DungeonLevel(IReadOnlyList<DungeonArea> areas, string activeAreaId,
        string? entranceAreaId = null, string? exitAreaId = null)
    {
        Areas = areas.Count > 0
            ? areas
            : throw new ArgumentException("A szintnek legalább egy területet tartalmaznia kell.", nameof(areas));
        _areas = areas.ToDictionary(area => area.Id, StringComparer.Ordinal);
        EntranceAreaId = string.IsNullOrWhiteSpace(entranceAreaId) ? Areas[0].Id : entranceAreaId;
        ExitAreaId = string.IsNullOrWhiteSpace(exitAreaId) ? Areas[^1].Id : exitAreaId;
        ActiveAreaId = activeAreaId;
        _ = ActiveArea;
        _ = EntranceArea;
        _ = ExitArea;
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
