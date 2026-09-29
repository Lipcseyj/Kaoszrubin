namespace KaoszRubin.World;

/// <summary>Egy erdőprofil részleges felülírása. A null érték megtartja az örökölt beállítást.</summary>
public sealed record ForestGenerationConfigurationPatch
{
    public double? ForestDensity { get; init; }
    public IntRange? GroveSize { get; init; }
    public int? BiomeSize { get; init; }
    public double? PineChance { get; init; }
    public double? BushChance { get; init; }
    public double? FlowerBushChance { get; init; }
    public IntRange? BushGroupSize { get; init; }
    public int? ForestEdgeWidth { get; init; }
    public double? ThicketChance { get; init; }
    public double? UndergrowthChance { get; init; }
    public double? DenseUndergrowthChance { get; init; }
    public IntRange? LakeCount { get; init; }
    public IntRange? LakeRadius { get; init; }
    public double? MarshChance { get; init; }
    public IntRange? MarshCount { get; init; }
    public IntRange? MarshRadius { get; init; }
    public int? TrailWidth { get; init; }
    public double? TrailWinding { get; init; }
    public double? ExtraTrailChance { get; init; }
    public IntRange? BuildingCount { get; init; }
    public IntRange? BuildingSize { get; init; }
    public double? BuildingPartitionChance { get; init; }
    public double? ManorBuildingChance { get; init; }
    public double? LabyrinthBuildingChance { get; init; }
    public IntRange? ManorBuildingWidth { get; init; }
    public IntRange? ManorBuildingHeight { get; init; }
    public IntRange? ManorRoomCount { get; init; }
    public int? BuildingMinimumRoomSize { get; init; }
    public IntRange? LabyrinthBuildingWidth { get; init; }
    public IntRange? LabyrinthBuildingHeight { get; init; }
    public double? BuildingExtraConnectionChance { get; init; }
    public double? BuildingSecondEntranceChance { get; init; }
    public double? LockedBuildingDoorChance { get; init; }
    public double? OpenBuildingDoorChance { get; init; }
    public ForestTerrainPalette? Palette { get; init; }
    public IReadOnlyList<ForestBuildingStyleDefinition>? BuildingStyles { get; init; }
}

public sealed record ForestAreaTemplateDefinition(
    string Id,
    string Name,
    string? BaseTemplateId,
    ForestGenerationConfigurationPatch Overrides);

public sealed record ForestAreaDefinition(
    string Id,
    string Name,
    AreaCoordinate Coordinate,
    string TemplateId = ForestAreaTemplateCatalog.MixedForest,
    ForestGenerationConfigurationPatch? Overrides = null);

public sealed record ForestAreaConnectionDefinition(string FirstAreaId, string SecondAreaId);

/// <summary>Kézzel szerkesztett erdei képernyőgráf és a hozzá tartozó helyi erdőprofilok.</summary>
public sealed record ExplicitForestAreaGraphConfiguration(
    IReadOnlyList<ForestAreaDefinition> Areas,
    IReadOnlyList<ForestAreaConnectionDefinition> Connections,
    string EntranceAreaId,
    string ExitAreaId,
    IReadOnlyList<ForestAreaTemplateDefinition>? Templates = null)
{
    public DungeonAreaGraphPlan BuildPlan()
    {
        Validate();
        var byId = Areas.ToDictionary(area => area.Id, StringComparer.Ordinal);
        var connections = Connections.Select(connection =>
        {
            var first = byId[connection.FirstAreaId];
            var second = byId[connection.SecondAreaId];
            var firstEdge = Direction(first.Coordinate, second.Coordinate);
            return new DungeonAreaConnectionPlan(first.Id, firstEdge, second.Id, Opposite(firstEdge));
        }).ToArray();
        var adjacency = Areas.ToDictionary(area => area.Id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var connection in Connections)
        {
            adjacency[connection.FirstAreaId].Add(connection.SecondAreaId);
            adjacency[connection.SecondAreaId].Add(connection.FirstAreaId);
        }
        var mainRoute = ShortestPath(adjacency, EntranceAreaId, ExitAreaId).ToHashSet(StringComparer.Ordinal);
        var nodes = Areas.Select(area => new DungeonAreaNodePlan(area.Id, area.Coordinate,
            area.Id == EntranceAreaId && area.Id == ExitAreaId ? DungeonAreaRole.EntranceAndExit :
            area.Id == EntranceAreaId ? DungeonAreaRole.Entrance :
            area.Id == ExitAreaId ? DungeonAreaRole.Exit :
            adjacency[area.Id].Count >= 3 ? DungeonAreaRole.Junction :
            mainRoute.Contains(area.Id) ? DungeonAreaRole.MainRoute :
            adjacency[area.Id].Count == 1 ? DungeonAreaRole.DeadEnd : DungeonAreaRole.Branch)).ToArray();
        return new DungeonAreaGraphPlan(nodes, connections, EntranceAreaId, ExitAreaId);
    }

    public void Validate()
    {
        if (Areas.Count == 0) throw new ArgumentException("Az explicit erdőgráf nem lehet üres.");
        if (Areas.Any(area => string.IsNullOrWhiteSpace(area.Id) || string.IsNullOrWhiteSpace(area.Name)) ||
            Areas.Select(area => area.Id).Distinct(StringComparer.Ordinal).Count() != Areas.Count ||
            Areas.Select(area => area.Coordinate).Distinct().Count() != Areas.Count)
            throw new ArgumentException("Az erdőterületek azonosítója, neve vagy koordinátája érvénytelen.");
        var ids = Areas.Select(area => area.Id).ToHashSet(StringComparer.Ordinal);
        if (!ids.Contains(EntranceAreaId) || !ids.Contains(ExitAreaId))
            throw new ArgumentException("Az erdőgráf bejárata vagy kijárata ismeretlen területre mutat.");
        var edges = new HashSet<(string First, string Second)>();
        foreach (var connection in Connections)
        {
            if (!ids.Contains(connection.FirstAreaId) || !ids.Contains(connection.SecondAreaId) ||
                connection.FirstAreaId == connection.SecondAreaId)
                throw new ArgumentException("Az erdőgráf egyik kapcsolata érvénytelen.");
            var normalized = string.CompareOrdinal(connection.FirstAreaId, connection.SecondAreaId) < 0
                ? (connection.FirstAreaId, connection.SecondAreaId)
                : (connection.SecondAreaId, connection.FirstAreaId);
            if (!edges.Add(normalized)) throw new ArgumentException("Az erdőgráf ismételt kapcsolatot tartalmaz.");
            _ = Direction(Areas.First(area => area.Id == connection.FirstAreaId).Coordinate,
                Areas.First(area => area.Id == connection.SecondAreaId).Coordinate);
        }
        var adjacency = Areas.ToDictionary(area => area.Id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var connection in Connections)
        {
            adjacency[connection.FirstAreaId].Add(connection.SecondAreaId);
            adjacency[connection.SecondAreaId].Add(connection.FirstAreaId);
        }
        if (ShortestPath(adjacency, EntranceAreaId, ExitAreaId).Count == 0 ||
            Reachable(adjacency, EntranceAreaId).Count != Areas.Count)
            throw new ArgumentException("Az explicit erdőgráfnak összefüggőnek kell lennie.");
        ForestAreaConfigurationResolver.ValidateTemplates(Templates ?? []);
        var templateIds = ForestAreaTemplateCatalog.BuiltIns.Select(template => template.Id)
            .Concat((Templates ?? []).Select(template => template.Id)).ToHashSet(StringComparer.Ordinal);
        if (Areas.Any(area => !templateIds.Contains(area.TemplateId)))
            throw new ArgumentException("Az erdőgráf egyik képernyője ismeretlen template-et használ.");
    }

    private static MazeEdge Direction(AreaCoordinate first, AreaCoordinate second) =>
        (second.X - first.X, second.Y - first.Y) switch
        {
            (0, -1) => MazeEdge.Top,
            (1, 0) => MazeEdge.Right,
            (0, 1) => MazeEdge.Bottom,
            (-1, 0) => MazeEdge.Left,
            _ => throw new ArgumentException("Csak közvetlenül szomszédos erdőképernyők köthetők össze.")
        };

    private static MazeEdge Opposite(MazeEdge edge) => edge switch
    {
        MazeEdge.Top => MazeEdge.Bottom,
        MazeEdge.Right => MazeEdge.Left,
        MazeEdge.Bottom => MazeEdge.Top,
        _ => MazeEdge.Right
    };

    private static HashSet<string> Reachable(IReadOnlyDictionary<string, List<string>> adjacency, string start)
    {
        var result = new HashSet<string>(StringComparer.Ordinal) { start };
        var pending = new Queue<string>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var current))
            foreach (var next in adjacency[current]) if (result.Add(next)) pending.Enqueue(next);
        return result;
    }

    private static IReadOnlyList<string> ShortestPath(IReadOnlyDictionary<string, List<string>> adjacency,
        string start, string destination)
    {
        var previous = new Dictionary<string, string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { start };
        var pending = new Queue<string>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var current) && !visited.Contains(destination))
            foreach (var next in adjacency[current])
                if (visited.Add(next)) { previous[next] = current; pending.Enqueue(next); }
        if (!visited.Contains(destination)) return [];
        var path = new List<string>();
        for (var current = destination;; current = previous[current])
        {
            path.Add(current);
            if (current == start) break;
        }
        path.Reverse();
        return path;
    }
}

public static class ForestAreaTemplateCatalog
{
    public const string MixedForest = "mixed-forest";
    public const string Swamp = "swamp";
    public const string LakesAndManors = "lakes-and-manors";
    public const string DenseCabinForest = "dense-cabin-forest";
    public const string ForestLabyrinth = "forest-labyrinth";
    public const string OpenGroves = "open-groves";

    public static IReadOnlyList<ForestAreaTemplateDefinition> BuiltIns { get; } =
    [
        new(MixedForest, "Normál vegyes erdő", null, new()),
        new(Swamp, "Mocsárvidék", MixedForest, new()
        {
            ForestDensity = 0.48, LakeCount = new(0, 1), MarshCount = new(3, 5),
            MarshRadius = new(5, 9), MarshChance = 0.82, TrailWidth = 1, TrailWinding = 0.90,
            BuildingCount = new(0, 1)
        }),
        new(LakesAndManors, "Tavak és kúriák", MixedForest, new()
        {
            ForestDensity = 0.42, LakeCount = new(2, 4), LakeRadius = new(3, 6),
            BuildingCount = new(2, 3), ManorBuildingChance = 0.72, LabyrinthBuildingChance = 0.08
        }),
        new(DenseCabinForest, "Sűrű erdő kunyhókkal", MixedForest, new()
        {
            ForestDensity = 0.86, GroveSize = new(8, 18), LakeCount = new(0, 1),
            BuildingCount = new(1, 2), ManorBuildingChance = 0.10, LabyrinthBuildingChance = 0.02
        }),
        new(ForestLabyrinth, "Erdei labirintus", MixedForest, new()
        {
            ForestDensity = 0.92, GroveSize = new(9, 19), TrailWidth = 1, TrailWinding = 0.95,
            ExtraTrailChance = 0.18, BuildingCount = new(1, 2), ManorBuildingChance = 0.20,
            LabyrinthBuildingChance = 0.60
        }),
        new(OpenGroves, "Nyílt ligetek", MixedForest, new()
        {
            ForestDensity = 0.22, GroveSize = new(3, 8), BushChance = 0.24,
            FlowerBushChance = 0.08, TrailWinding = 0.40, ExtraTrailChance = 0.45
        })
    ];
}

public static class ForestAreaConfigurationResolver
{
    public static ForestGenerationConfiguration Resolve(ForestGenerationConfiguration common,
        ExplicitForestAreaGraphConfiguration graph, ForestAreaDefinition area)
    {
        var templates = ForestAreaTemplateCatalog.BuiltIns.Concat(graph.Templates ?? [])
            .ToDictionary(template => template.Id, StringComparer.Ordinal);
        var resolved = ResolveTemplate(common, area.TemplateId, templates, []);
        return Apply(resolved, area.Overrides);
    }

    public static void ValidateTemplates(IReadOnlyList<ForestAreaTemplateDefinition> customTemplates)
    {
        if (customTemplates.Any(template => string.IsNullOrWhiteSpace(template.Id) ||
                string.IsNullOrWhiteSpace(template.Name)) ||
            customTemplates.Select(template => template.Id).Distinct(StringComparer.Ordinal).Count() !=
            customTemplates.Count || customTemplates.Any(template => ForestAreaTemplateCatalog.BuiltIns.Any(
                builtIn => string.Equals(builtIn.Id, template.Id, StringComparison.Ordinal))))
            throw new ArgumentException("Az erdősablonok azonosítója vagy neve érvénytelen.");
        var all = ForestAreaTemplateCatalog.BuiltIns.Concat(customTemplates)
            .ToDictionary(template => template.Id, StringComparer.Ordinal);
        foreach (var template in customTemplates) _ = ResolveTemplate(new(), template.Id, all, []);
    }

    public static ForestGenerationConfiguration Apply(ForestGenerationConfiguration source,
        ForestGenerationConfigurationPatch? patch)
    {
        if (patch is null) return Clone(source);
        return new ForestGenerationConfiguration
        {
            ForestDensity = patch.ForestDensity ?? source.ForestDensity,
            GroveSize = patch.GroveSize ?? source.GroveSize,
            BiomeSize = patch.BiomeSize ?? source.BiomeSize,
            PineChance = patch.PineChance ?? source.PineChance,
            BushChance = patch.BushChance ?? source.BushChance,
            FlowerBushChance = patch.FlowerBushChance ?? source.FlowerBushChance,
            BushGroupSize = patch.BushGroupSize ?? source.BushGroupSize,
            ForestEdgeWidth = patch.ForestEdgeWidth ?? source.ForestEdgeWidth,
            ThicketChance = patch.ThicketChance ?? source.ThicketChance,
            UndergrowthChance = patch.UndergrowthChance ?? source.UndergrowthChance,
            DenseUndergrowthChance = patch.DenseUndergrowthChance ?? source.DenseUndergrowthChance,
            LakeCount = patch.LakeCount ?? source.LakeCount,
            LakeRadius = patch.LakeRadius ?? source.LakeRadius,
            MarshChance = patch.MarshChance ?? source.MarshChance,
            MarshCount = patch.MarshCount ?? source.MarshCount,
            MarshRadius = patch.MarshRadius ?? source.MarshRadius,
            TrailWidth = patch.TrailWidth ?? source.TrailWidth,
            TrailWinding = patch.TrailWinding ?? source.TrailWinding,
            ExtraTrailChance = patch.ExtraTrailChance ?? source.ExtraTrailChance,
            BuildingCount = patch.BuildingCount ?? source.BuildingCount,
            BuildingSize = patch.BuildingSize ?? source.BuildingSize,
            BuildingPartitionChance = patch.BuildingPartitionChance ?? source.BuildingPartitionChance,
            ManorBuildingChance = patch.ManorBuildingChance ?? source.ManorBuildingChance,
            LabyrinthBuildingChance = patch.LabyrinthBuildingChance ?? source.LabyrinthBuildingChance,
            ManorBuildingWidth = patch.ManorBuildingWidth ?? source.ManorBuildingWidth,
            ManorBuildingHeight = patch.ManorBuildingHeight ?? source.ManorBuildingHeight,
            ManorRoomCount = patch.ManorRoomCount ?? source.ManorRoomCount,
            BuildingMinimumRoomSize = patch.BuildingMinimumRoomSize ?? source.BuildingMinimumRoomSize,
            LabyrinthBuildingWidth = patch.LabyrinthBuildingWidth ?? source.LabyrinthBuildingWidth,
            LabyrinthBuildingHeight = patch.LabyrinthBuildingHeight ?? source.LabyrinthBuildingHeight,
            BuildingExtraConnectionChance = patch.BuildingExtraConnectionChance ?? source.BuildingExtraConnectionChance,
            BuildingSecondEntranceChance = patch.BuildingSecondEntranceChance ?? source.BuildingSecondEntranceChance,
            LockedBuildingDoorChance = patch.LockedBuildingDoorChance ?? source.LockedBuildingDoorChance,
            OpenBuildingDoorChance = patch.OpenBuildingDoorChance ?? source.OpenBuildingDoorChance,
            Palette = patch.Palette ?? source.Palette,
            BuildingStyles = patch.BuildingStyles ?? source.BuildingStyles
        };
    }

    private static ForestGenerationConfiguration Clone(ForestGenerationConfiguration source) => Apply(source, new());

    private static ForestGenerationConfiguration ResolveTemplate(ForestGenerationConfiguration common, string id,
        IReadOnlyDictionary<string, ForestAreaTemplateDefinition> templates, HashSet<string> resolving)
    {
        if (!templates.TryGetValue(id, out var template))
            throw new ArgumentException($"Ismeretlen erdősablon: {id}.");
        if (!resolving.Add(id)) throw new ArgumentException($"Körkörös erdősablon-öröklés: {id}.");
        var basis = template.BaseTemplateId is { Length: > 0 } parent
            ? ResolveTemplate(common, parent, templates, resolving)
            : Clone(common);
        resolving.Remove(id);
        return Apply(basis, template.Overrides);
    }
}
