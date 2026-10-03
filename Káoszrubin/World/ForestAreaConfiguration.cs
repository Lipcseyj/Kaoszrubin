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
            var first = Areas.First(area => area.Id == connection.FirstAreaId);
            var second = Areas.First(area => area.Id == connection.SecondAreaId);
            if (!AreAdjacent(first.Coordinate, second.Coordinate))
                throw new ArgumentException($"Nem szomszédos erdőképernyők: {first.Id} " +
                    $"({first.Coordinate.X},{first.Coordinate.Y}) és {second.Id} " +
                    $"({second.Coordinate.X},{second.Coordinate.Y}).");
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

    public static bool AreAdjacent(AreaCoordinate first, AreaCoordinate second) =>
        Math.Abs((long)first.X - second.X) + Math.Abs((long)first.Y - second.Y) == 1;

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
    public const string HugeSwamp = "huge-swamp";
    public const string BlackwaterBog = "blackwater-bog";
    public const string ReedMarsh = "reed-marsh";
    public const string FloodedWood = "flooded-wood";
    public const string MirrorLakes = "mirror-lakes";
    public const string IslandGroves = "island-groves";
    public const string LostManor = "lost-manor";
    public const string RuinedEstate = "ruined-estate";
    public const string OldPines = "old-pines";
    public const string ThornMaze = "thorn-maze";
    public const string AncientForest = "ancient-forest";
    public const string HunterCamp = "hunter-camp";
    public const string Wildwood = "wildwood";
    public const string AshenWood = "ashen-wood";
    public const string FlowerMeadow = "flower-meadow";
    public const string SunlitGlades = "sunlit-glades";
    public const string SparsePines = "sparse-pines";
    public const string WoodlandHamlet = "woodland-hamlet";

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
        }),
        new(HugeSwamp, "Óriásmocsár", Swamp, new()
        {
            ForestDensity = 0.34, LakeCount = new(1, 2), LakeRadius = new(4, 7),
            MarshCount = new(6, 9), MarshRadius = new(7, 12), MarshChance = 0.94,
            BuildingCount = new(0, 0), TrailWinding = 0.96
        }),
        new(BlackwaterBog, "Feketevizű láp", Swamp, new()
        {
            ForestDensity = 0.38, PineChance = 0.04, LakeCount = new(2, 3),
            LakeRadius = new(4, 7), MarshCount = new(3, 5), MarshChance = 0.92,
            BuildingCount = new(0, 1),
            Palette = new ForestTerrainPalette
            {
                Water = new("bog-water", new('≈'), ConsoleColor.DarkBlue,
                    ConsoleColor.Black, false, false),
                Marsh = new("bog-marsh", new('≋'), ConsoleColor.DarkYellow,
                    ConsoleColor.DarkBlue, true, false),
                Tree = new("bog-tree", new('♠'), ConsoleColor.DarkGreen,
                    ConsoleColor.Black, false, true)
            }
        }),
        new(ReedMarsh, "Nádas mocsár", Swamp, new()
        {
            ForestDensity = 0.25, BushChance = 0.38, FlowerBushChance = 0.02,
            ThicketChance = 0.04, UndergrowthChance = 0.36,
            MarshCount = new(4, 7), MarshRadius = new(4, 8),
            BuildingCount = new(0, 0), TrailWidth = 1
        }),
        new(FloodedWood, "Elárasztott erdő", LakesAndManors, new()
        {
            ForestDensity = 0.62, LakeCount = new(3, 5), LakeRadius = new(4, 7),
            MarshCount = new(3, 5), MarshChance = 0.82,
            BuildingCount = new(0, 1), TrailWinding = 0.88
        }),
        new(MirrorLakes, "Tükörtavak", LakesAndManors, new()
        {
            ForestDensity = 0.27, LakeCount = new(3, 4), LakeRadius = new(5, 8),
            MarshCount = new(0, 1), MarshChance = 0.12,
            BuildingCount = new(0, 0), TrailWinding = 0.52
        }),
        new(IslandGroves, "Tószigeti ligetek", LakesAndManors, new()
        {
            ForestDensity = 0.35, GroveSize = new(3, 7),
            LakeCount = new(4, 6), LakeRadius = new(3, 5),
            MarshCount = new(1, 3), BuildingCount = new(0, 1),
            ExtraTrailChance = 0.55
        }),
        new(LostManor, "Elveszett kúria", LakesAndManors, new()
        {
            ForestDensity = 0.72, ThicketChance = 0.25,
            LakeCount = new(0, 1), BuildingCount = new(2, 3),
            ManorBuildingChance = 0.88, LabyrinthBuildingChance = 0.06,
            LockedBuildingDoorChance = 0.68, OpenBuildingDoorChance = 0.08
        }),
        new(RuinedEstate, "Romos birtok", LostManor, new()
        {
            DenseUndergrowthChance = 0.28, UndergrowthChance = 0.38,
            BuildingCount = new(2, 4), ManorBuildingChance = 0.62,
            LabyrinthBuildingChance = 0.28,
            LockedBuildingDoorChance = 0.38, OpenBuildingDoorChance = 0.34
        }),
        new(OldPines, "Ősi fenyves", DenseCabinForest, new()
        {
            ForestDensity = 0.76, PineChance = 0.86, BiomeSize = 12,
            ThicketChance = 0.07, LakeCount = new(0, 1),
            BuildingCount = new(0, 1), TrailWinding = 0.55
        }),
        new(ThornMaze, "Tövislabirintus", ForestLabyrinth, new()
        {
            ForestDensity = 0.89, PineChance = 0.02, ThicketChance = 0.42,
            DenseUndergrowthChance = 0.29, TrailWidth = 1,
            ExtraTrailChance = 0.10, BuildingCount = new(0, 1)
        }),
        new(AncientForest, "Őserdő", DenseCabinForest, new()
        {
            ForestDensity = 0.96, GroveSize = new(12, 24), BiomeSize = 32,
            PineChance = 0.34, ThicketChance = 0.29,
            BuildingCount = new(0, 0), TrailWidth = 1,
            Palette = new ForestTerrainPalette
            {
                Tree = new("ancient-tree", new('♠'), ConsoleColor.Green,
                    ConsoleColor.Black, false, true),
                Thicket = new("ancient-thicket", new('#'), ConsoleColor.DarkGreen,
                    ConsoleColor.Black, false, true),
                DenseUndergrowth = new("ancient-growth", new('▒'), ConsoleColor.DarkGreen,
                    ConsoleColor.Black, true, false)
            }
        }),
        new(HunterCamp, "Vadásztábor", DenseCabinForest, new()
        {
            ForestDensity = 0.64, BuildingCount = new(2, 4),
            ManorBuildingChance = 0.02, LabyrinthBuildingChance = 0,
            TrailWidth = 2, ExtraTrailChance = 0.68,
            OpenBuildingDoorChance = 0.42, LockedBuildingDoorChance = 0.08
        }),
        new(Wildwood, "Vad rengeteg", ForestLabyrinth, new()
        {
            ForestDensity = 0.97, BushChance = 0.24, ThicketChance = 0.26,
            UndergrowthChance = 0.35, DenseUndergrowthChance = 0.24,
            BuildingCount = new(0, 0), TrailWinding = 0.98
        }),
        new(AshenWood, "Hamuszürke erdő", MixedForest, new()
        {
            ForestDensity = 0.57, PineChance = 0.36,
            BushChance = 0.07, FlowerBushChance = 0.01,
            LakeCount = new(0, 1), MarshCount = new(0, 1),
            BuildingCount = new(0, 1),
            Palette = new ForestTerrainPalette
            {
                Tree = new("ashen-tree", new('♠'), ConsoleColor.Gray,
                    ConsoleColor.Black, false, true),
                Pine = new("ashen-pine", new('▲'), ConsoleColor.DarkGray,
                    ConsoleColor.Black, false, true),
                Bush = new("ashen-bush", new('♣'), ConsoleColor.DarkGreen,
                    ConsoleColor.Black, true, false),
                FlowerBush = new("ashen-flower", new('✿'), ConsoleColor.DarkRed,
                    ConsoleColor.Black, true, false),
                Thicket = new("ashen-thicket", new('#'), ConsoleColor.DarkGray,
                    ConsoleColor.Black, false, true)
            }
        }),
        new(FlowerMeadow, "Virágos rét", OpenGroves, new()
        {
            ForestDensity = 0.14, BushChance = 0.32, FlowerBushChance = 0.38,
            ThicketChance = 0.02, UndergrowthChance = 0.14,
            DenseUndergrowthChance = 0.02, LakeCount = new(0, 1),
            MarshCount = new(0, 0), BuildingCount = new(0, 0),
            Palette = new ForestTerrainPalette
            {
                FlowerBush = new("meadow-flowers", new('✿'), ConsoleColor.Yellow,
                    ConsoleColor.Black, true, false),
                Bush = new("meadow-bush", new('♣'), ConsoleColor.Green,
                    ConsoleColor.Black, true, false)
            }
        }),
        new(SunlitGlades, "Napos tisztások", OpenGroves, new()
        {
            ForestDensity = 0.30, GroveSize = new(4, 7),
            BushChance = 0.23, FlowerBushChance = 0.16,
            LakeCount = new(1, 2), MarshCount = new(0, 0),
            ExtraTrailChance = 0.64, BuildingCount = new(0, 1)
        }),
        new(SparsePines, "Ritkás fenyves", OpenGroves, new()
        {
            ForestDensity = 0.33, PineChance = 0.92, GroveSize = new(3, 6),
            BushChance = 0.08, FlowerBushChance = 0.01,
            LakeCount = new(0, 1), BuildingCount = new(0, 0)
        }),
        new(WoodlandHamlet, "Erdei telep", OpenGroves, new()
        {
            ForestDensity = 0.39, LakeCount = new(0, 1),
            BuildingCount = new(3, 5), ManorBuildingChance = 0.15,
            LabyrinthBuildingChance = 0.02, TrailWidth = 3,
            ExtraTrailChance = 0.73,
            LockedBuildingDoorChance = 0.05, OpenBuildingDoorChance = 0.55
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
