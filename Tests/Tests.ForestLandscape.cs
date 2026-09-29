internal static partial class Program
{
    static void ForestAreaConfigurationAndMapRoundTrip()
    {
        var graph = new ExplicitForestAreaGraphConfiguration(
        [
            new("GATE", "Mohakapu", new(0, 0), ForestAreaTemplateCatalog.MixedForest),
            new("SWAMP", "Feketevíz", new(1, 0), ForestAreaTemplateCatalog.Swamp,
                new() { BuildingCount = new(2, 2) }),
            new("EXIT", "Szélcsend", new(2, 0), ForestAreaTemplateCatalog.OpenGroves)
        ],
        [new("GATE", "SWAMP"), new("SWAMP", "EXIT")], "GATE", "EXIT");
        var plan = graph.BuildPlan();
        var swamp = ForestAreaConfigurationResolver.Resolve(new ForestGenerationConfiguration(), graph,
            graph.Areas[1]);
        Assert(plan.Nodes.Count == 3 && plan.Connections.Count == 2 && plan.ExitAreaId == "EXIT" &&
               swamp.MarshCount.Minimum == 3 && swamp.BuildingCount == new IntRange(2, 2),
            "Az explicit gráf vagy a template + képernyőfelülírás feloldása hibás.");
        var restored = ForestConfigurationJson.Deserialize(ForestConfigurationJson.Serialize(graph));
        Assert(restored.Areas.Select(area => area.Id).SequenceEqual(graph.Areas.Select(area => area.Id)) &&
               restored.Connections.Count == 2 && restored.Areas[1].Overrides?.BuildingCount == new IntRange(2, 2),
            "Az erdőgráf JSON round-tripja adatot veszített.");

        var firstMaze = MapAreaMaze("SWAMP", new Position(6, 3));
        var secondMaze = MapAreaMaze("GATE", new Position(0, 3), "EXIT", new Position(6, 3));
        var thirdMaze = MapAreaMaze("SWAMP", new Position(0, 3));
        var firstFog = new FogOfWar(7, 7, 1);
        firstFog.Restore([new Position(6, 3)], false);
        var level = new DungeonLevel(
        [
            new("GATE", firstMaze, firstFog, "Mohakapu", new(0, 0), DungeonAreaRole.Entrance),
            new("SWAMP", secondMaze, new FogOfWar(7, 7, 1), "Feketevíz", new(1, 0), DungeonAreaRole.MainRoute),
            new("EXIT", thirdMaze, new FogOfWar(7, 7, 1), "Szélcsend", new(2, 0), DungeonAreaRole.Exit)
        ], "GATE", "GATE", "EXIT");
        var map = level.CreateDiscoveredMap();
        Assert(map.Nodes.Count == 2 && map.Nodes.Single(node => node.Id == "GATE").Name == "Mohakapu" &&
               map.Nodes.Single(node => node.Id == "SWAMP") is { Name: "?", IsVisited: false } &&
               map.Edges.Count == 1 && !map.Nodes.Any(node => node.Id == "EXIT"),
            "A felfedezett régiótérkép rejtett területet szivárogtatott vagy kihagyta az ismert átjárót.");

        var campaignLayout = (ForestMazeLayoutConfiguration)MazeLevelConfigurations.Get(6).Layout!;
        Assert(campaignLayout.ExplicitGraph is { Areas.Count: 8 } && campaignLayout.Forest.BuildingStyles.Count >= 3,
            "A kampányerdő nem kapta meg az explicit területeket vagy a többféle épületfalat.");
        var campaignGraph = campaignLayout.ExplicitGraph!;
        foreach (var area in campaignGraph.Areas)
        {
            var areaConfiguration = ForestAreaConfigurationResolver.Resolve(
                campaignLayout.Forest, campaignGraph, area);
            var generated = new ForestMazeGenerator(LandscapeSettings(6), areaConfiguration, [], [],
                new Random(area.Id.GetHashCode(StringComparison.Ordinal))).Create(170, 44);
            Assert(generated.CheckFullAccessibility().IsFullyAccessible &&
                   generated.Rooms.Any(room => room.Kind != RoomKind.Generic),
                $"A(z) {area.Id} explicit erdőképernyő nem generálható vagy elvesztette a szobatípusait.");
        }

        static Maze MapAreaMaze(string destination, Position passagePosition,
            string? secondDestination = null, Position? secondPosition = null)
        {
            var maze = new Maze(7, 7);
            maze.Carve(passagePosition);
            maze.AddPassage(new MazePassage(passagePosition, destination, new Position(3, 3)));
            if (secondDestination is not null && secondPosition is { } other)
            {
                maze.Carve(other);
                maze.AddPassage(new MazePassage(other, secondDestination, new Position(3, 3)));
            }
            return maze;
        }
    }

    static void ForestBuildingsSupportMultipleLayoutTypes()
    {
        for (var seed = 0; seed < 8; seed++)
        {
            var cabin = BuildingLandscape(seed, manorChance: 0, labyrinthChance: 0,
                buildingSize: new IntRange(7, 7));
            var cabinRooms = cabin.Rooms.Where(room => room.Purpose != RoomPurpose.Starting).ToArray();
            Assert(cabinRooms.Length == 2 && cabin.CheckFullAccessibility().IsFullyAccessible,
                $"A kétszobás kunyhó hibás vagy nem bejárható: seed={seed}.");

            var manor = BuildingLandscape(seed, manorChance: 1, labyrinthChance: 0);
            var manorRooms = manor.Rooms.Where(room => room.Purpose != RoomPurpose.Starting).ToArray();
            Assert(manorRooms.Length == 6 && manorRooms.All(room => room.Width >= 3 && room.Height >= 3) &&
                   manor.CheckFullAccessibility().IsFullyAccessible,
                $"A hatszobás nagy épület hibás vagy nem bejárható: seed={seed}, rooms={manorRooms.Length}.");

            var labyrinth = BuildingLandscape(seed, manorChance: 0, labyrinthChance: 1);
            var labyrinthRooms = labyrinth.Rooms.Where(room => room.Purpose != RoomPurpose.Starting).ToArray();
            Assert(labyrinthRooms.Length == 9 && labyrinthRooms.All(room => room.Width == 3 && room.Height == 3) &&
                   labyrinth.CheckFullAccessibility().IsFullyAccessible,
                $"A kilencszobás labirintusépület hibás vagy nem bejárható: seed={seed}, rooms={labyrinthRooms.Length}.");
        }
    }

    static Maze BuildingLandscape(int seed, double manorChance, double labyrinthChance,
        IntRange? buildingSize = null) =>
        new ForestMazeGenerator(LandscapeSettings(1), new ForestGenerationConfiguration
        {
            ForestDensity = 0.65,
            LakeCount = new(0, 0), MarshCount = new(0, 0),
            BuildingCount = new(1, 1), BuildingSize = buildingSize ?? new IntRange(7, 7),
            BuildingPartitionChance = 1,
            ManorBuildingChance = manorChance, LabyrinthBuildingChance = labyrinthChance,
            ManorBuildingWidth = new IntRange(16, 16), ManorBuildingHeight = new IntRange(12, 12),
            ManorRoomCount = new IntRange(6, 6),
            LabyrinthBuildingWidth = new IntRange(13, 13), LabyrinthBuildingHeight = new IntRange(13, 13),
            BuildingSecondEntranceChance = 1
        }, [], [], new Random(seed)).Create(170, 44);

    static void ForestDensityControlsCoverageAndGroveScale()
    {
        foreach (var seed in Enumerable.Range(0, 12))
        {
            var empty = Landscape(0, seed);
            var sparse = Landscape(0.18, seed);
            var medium = Landscape(0.58, seed);
            var dense = Landscape(0.78, seed);
            var full = Landscape(1, seed);
            var sparseOpen = OpenFraction(sparse);
            var denseOpen = OpenFraction(dense);
            Assert(OpenFraction(empty) == 1 && sparseOpen > 0.70 && denseOpen < 0.48 &&
                   sparseOpen - denseOpen > 0.35 && OpenFraction(medium) < sparseOpen &&
                   OpenFraction(medium) > denseOpen && OpenFraction(full) < denseOpen,
                $"Forest density failed for seed {seed}: sparse={sparseOpen:P1}, dense={denseOpen:P1}.");
            foreach (var maze in new[] { empty, sparse, medium, dense, full })
            {
                Assert(maze.CheckFullAccessibility().IsFullyAccessible, $"Disconnected landscape: {seed}.");
                Assert(LandscapeCells(maze, border: true).All(p => !maze.IsWalkable(p)), "Forest border was opened.");
                Assert(maze.Doors.Count == 0, "An outdoor clearing acquired a door.");
            }
            var repeat = Landscape(0.58, seed);
            Assert(LandscapeCells(medium).All(p => medium.Tiles[p.X, p.Y] == repeat.Tiles[p.X, p.Y]),
                "A fixed seed did not reproduce the terrain.");
        }
        var smallEdges = 0;
        var largeEdges = 0;
        for (var seed = 0; seed < 8; seed++)
        {
            smallEdges += WoodlandEdgeCount(Landscape(0.45, seed, new IntRange(3, 5)));
            largeEdges += WoodlandEdgeCount(Landscape(0.45, seed, new IntRange(12, 20)));
        }
        Assert(smallEdges > largeEdges * 1.25, $"Grove scale has little effect: {smallEdges}/{largeEdges}.");
    }

    static void ForestBiomesAndShrubsFormCoherentGroups()
    {
        var treePairs = 0;
        var mixedPairs = 0;
        var shrubCount = 0;
        var groupedShrubs = 0;
        var edgeShrubs = 0;
        var pineCount = 0;
        var broadleafCount = 0;
        for (var seed = 0; seed < 12; seed++)
        {
            var forest = new ForestGenerationConfiguration
            {
                ForestDensity = 0.55, PineChance = 0.4, ThicketChance = 0,
                BushChance = 0.4, FlowerBushChance = 0.1, ForestEdgeWidth = 3,
                LakeCount = new(0, 0), MarshCount = new(0, 0), BuildingCount = new(0, 0)
            };
            var maze = new ForestMazeGenerator(LandscapeSettings(4), forest, [], [], new Random(seed)).Create(170, 44);
            foreach (var p in LandscapeCells(maze))
            {
                var id = maze.GetTerrainStyle(p)?.Id;
                if (id == "forest-pine") pineCount++;
                if (id == "forest-tree") broadleafCount++;
                if (id is "forest-tree" or "forest-pine")
                    foreach (var direction in new[] { Direction.Right, Direction.Down })
                    {
                        var neighbor = maze.GetTerrainStyle(p + direction)?.Id;
                        if (neighbor is not ("forest-tree" or "forest-pine")) continue;
                        treePairs++;
                        if (neighbor != id) mixedPairs++;
                    }
                if (id is not ("forest-bush" or "forest-flower-bush")) continue;
                shrubCount++;
                if (Enum.GetValues<Direction>().Any(d => maze.GetTerrainStyle(p + d)?.Id is
                    "forest-bush" or "forest-flower-bush")) groupedShrubs++;
                if (Enumerable.Range(-3, 7).Any(dx => Enumerable.Range(-3, 7).Any(dy =>
                    Math.Abs(dx) + Math.Abs(dy) <= 3 && maze.IsWalkable(new Position(p.X + dx, p.Y + dy))))) edgeShrubs++;
            }
        }
        Assert(pineCount > 1000 && broadleafCount > 1000 && mixedPairs < treePairs * 0.06,
            $"Tree species are interleaved: mixed={mixedPairs}, pairs={treePairs}.");
        Assert(shrubCount > 500 && groupedShrubs > shrubCount * 0.90 && edgeShrubs > shrubCount * 0.95,
            $"Shrubs are not clearing-edge groups: total={shrubCount}, grouped={groupedShrubs}, edges={edgeShrubs}.");
    }

    static void ForestIndependentMarshesAndWindingTrailsWork()
    {
        var directLength = 0;
        var windingLength = 0;
        var plainArea = 0;
        var loopArea = 0;
        for (var seed = 0; seed < 10; seed++)
        {
            var wet = new ForestGenerationConfiguration
            {
                ForestDensity = 0.35, LakeCount = new(0, 0), MarshCount = new(2, 2), MarshRadius = new(4, 7),
                BuildingCount = new(0, 0)
            };
            var marsh = new ForestMazeGenerator(LandscapeSettings(2), wet, [], [], new Random(seed)).Create(170, 44);
            var marshCells = LandscapeCells(marsh).Where(p => marsh.GetTerrainStyle(p)?.Id == wet.Palette.Marsh.Id).ToArray();
            Assert(marshCells.Length > 50 && marshCells.All(marsh.IsWalkable) &&
                   LandscapeCells(marsh).All(p => marsh.GetTerrainStyle(p)?.Id != wet.Palette.Water.Id) &&
                   marsh.CheckFullAccessibility().IsFullyAccessible, "Independent marshes are missing or disconnected.");
            var direct = TrailLandscape(seed, 0, 0, 0);
            var winding = TrailLandscape(seed, 1, 0, 0);
            directLength += EntranceExitDistance(direct);
            windingLength += EntranceExitDistance(winding);
            var noLoops = TrailLandscape(seed, 0.8, 0, 5);
            plainArea += LandscapeCells(noLoops).Count(noLoops.IsWalkable);
            var loops = TrailLandscape(seed, 0.8, 1, 5);
            loopArea += LandscapeCells(loops).Count(loops.IsWalkable);
        }
        Assert(windingLength > directLength * 1.04,
            $"Winding trails are too direct: {windingLength}/{directLength}.");
        Assert(loopArea > plainArea * 1.10, $"Extra trails did not add alternatives: {loopArea}/{plainArea}.");
    }

    static void ForestLandscapeRejectsInvalidConfiguration()
    {
        var invalid = new ForestGenerationConfiguration[]
        {
            new() { ForestDensity = double.NaN }, new() { TrailWinding = double.PositiveInfinity },
            new() { ExtraTrailChance = -0.1 }, new() { GroveSize = new(0, 4) },
            new() { BushGroupSize = new(5, 2) }, new() { BiomeSize = 1 },
            new() { MarshCount = new(-1, 2) }, new() { MarshRadius = new(0, 2) },
            new() { ForestEdgeWidth = 0 }, new() { PineChance = 1.1 },
            new() { BushChance = 0.8, FlowerBushChance = 0.8 }
        };
        foreach (var configuration in invalid)
        {
            var rejected = false;
            try { _ = new ForestMazeGenerator(LandscapeSettings(0), configuration, [], [], new Random(1)); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Assert(rejected, "An invalid landscape configuration was accepted.");
        }
        // Pine, thicket and edge shrubs are independent layers, not one probability sum.
        _ = new ForestMazeGenerator(LandscapeSettings(0), new ForestGenerationConfiguration
            { PineChance = 1, ThicketChance = 1, BushChance = 1, FlowerBushChance = 0 }, [], []);
    }

    static MazeGenerationSettings LandscapeSettings(int rooms) => new()
        { RoomCount = rooms, MinimumRoomSize = 4, MaximumRoomSize = 7, TreasureChestCount = 0 };

    static Maze Landscape(double density, int seed, IntRange? grove = null) =>
        new ForestMazeGenerator(LandscapeSettings(3), new ForestGenerationConfiguration
        {
            ForestDensity = density, GroveSize = grove ?? new(5, 13),
            LakeCount = new(0, 0), MarshCount = new(0, 0), BuildingCount = new(0, 0)
        }, [], [], new Random(seed)).Create(170, 44);

    static Maze TrailLandscape(int seed, double winding, double loops, int rooms) =>
        new ForestMazeGenerator(LandscapeSettings(rooms), new ForestGenerationConfiguration
        {
            ForestDensity = 1, ThicketChance = 0, LakeCount = new(0, 0), MarshCount = new(0, 0),
            BuildingCount = new(0, 0), TrailWidth = 1, TrailWinding = winding, ExtraTrailChance = loops
        }, [], [], new Random(seed)).Create(170, 44);

    static IEnumerable<Position> LandscapeCells(Maze maze, bool border = false)
    {
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++)
            if (border == (x == 0 || y == 0 || x == maze.Width - 1 || y == maze.Height - 1))
                yield return new Position(x, y);
    }

    static double OpenFraction(Maze maze) => LandscapeCells(maze).Count(maze.IsWalkable) /
        (double)((maze.Width - 2) * (maze.Height - 2));

    static int WoodlandEdgeCount(Maze maze) => LandscapeCells(maze).Sum(p =>
        new[] { Direction.Right, Direction.Down }.Count(d => maze.IsWalkable(p) != maze.IsWalkable(p + d)));

    static int EntranceExitDistance(Maze maze)
    {
        var distances = new Dictionary<Position, int> { [maze.Entrance] = 0 };
        var pending = new Queue<Position>();
        pending.Enqueue(maze.Entrance);
        while (pending.TryDequeue(out var current))
        {
            if (current == maze.Exit) return distances[current];
            foreach (var direction in Enum.GetValues<Direction>())
            {
                var next = current + direction;
                if (maze.IsWalkable(next) && distances.TryAdd(next, distances[current] + 1)) pending.Enqueue(next);
            }
        }
        throw new InvalidOperationException("Exit is unreachable.");
    }
}
