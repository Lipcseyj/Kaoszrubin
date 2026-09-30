using KaoszRubin.Domain.Combat;

namespace KaoszRubin.World;

/// <summary>
/// Összefüggő tisztásokat, kanyargó ösvényeket, változatos növényzetet és tavakat készítő
/// erdei képernyőgenerátor. A szobaként nyilvántartott tisztásokra a közös encounter- és
/// kincselhelyezés változtatás nélkül ráépül.
/// </summary>
public sealed class ForestMazeGenerator : MazeGenerator
{
    private static readonly Direction[] Directions = Enum.GetValues<Direction>();
    private readonly ForestGenerationConfiguration _forest;
    private bool[,] _reserved = null!;
    private ForestTerrainField _routeField = null!;
    private MazeTerrainStyle _buildingWall = null!;
    private sealed record BuildingSite(Room Footprint, ForestBuildingLayout Layout,
        ForestBuildingStyleDefinition Style);

    public ForestMazeGenerator(MazeGenerationSettings settings, ForestGenerationConfiguration forest,
        IReadOnlyList<ResolvedEnemyEncounter> clearingEncounters,
        IReadOnlyList<ResolvedEnemyEncounter> trailEncounters, Random? random = null,
        EnemyMagicWeaponContext? enemyMagicWeaponContext = null)
        : base(settings, clearingEncounters, trailEncounters, random, enemyMagicWeaponContext)
    {
        _forest = forest ?? throw new ArgumentNullException(nameof(forest));
        ValidateForestConfiguration(forest);
    }

    protected override Maze CreateLayout(int width, int height)
    {
        var palette = _forest.Palette;
        var maze = new Maze(width, height, palette.Tree.Rune, palette.Tree.ForegroundColor, Settings.LevelName);
        foreach (var style in palette.All) maze.RegisterTerrainStyle(style);
        foreach (var style in _forest.BuildingStyles) maze.RegisterTerrainStyle(style.Wall);
        maze.ConfigureConnectedTerrainReveal(
            [palette.Tree, palette.Pine, palette.Bush, palette.FlowerBush, palette.Thicket]);
        _reserved = new bool[width, height];
        _routeField = new ForestTerrainField(width, height, new IntRange(4, 9), Random);
        FillForest(maze);

        var startingRoom = new Room(new Position(1, 1), 3, 3);
        CarveRoom(maze, startingRoom);
        ReserveRoom(maze, startingRoom);
        maze.SetStartingRoom(startingRoom);
        _reserved[maze.Exit.X, maze.Exit.Y] = true;

        var (clearings, buildings) = PlaceClearingsAndBuildings(maze);
        PlaceWetlands(maze);
        DecorateTerrain(maze);
        var connected = new List<Position> { maze.Entrance };
        var trailEdges = new HashSet<(Position, Position)>();
        foreach (var clearing in clearings.OrderBy(_ => Random.Next()))
        {
            var center = Center(clearing);
            var anchor = connected.OrderBy(position => Manhattan(position, center)).First();
            CarveMeanderingTrail(maze, anchor, center);
            trailEdges.Add((anchor, center));
            connected.Add(center);
        }

        var exit = maze.Exit;
        var exitAnchor = connected.OrderBy(position => Manhattan(position, exit)).First();
        CarveMeanderingTrail(maze, exitAnchor, exit);
        maze.Carve(exit);
        connected.Add(exit);
        trailEdges.Add((exitAnchor, exit));
        foreach (var start in connected)
        {
            if (Random.NextDouble() >= _forest.ExtraTrailChance) continue;
            var alternatives = connected.Where(end => end != start &&
                    !trailEdges.Contains((start, end)) && !trailEdges.Contains((end, start)))
                .OrderBy(end => Manhattan(start, end)).Take(3).ToArray();
            if (alternatives.Length == 0) continue;
            var end = alternatives[Random.Next(alternatives.Length)];
            CarveMeanderingTrail(maze, start, end);
            trailEdges.Add((start, end));
        }
        // A természetes tisztásokat is bekötjük: a közös labirintusjavító itt ajtókat tenne az erdőbe.
        ConnectOpenRegions(maze);
        BuildStructures(maze, buildings);
        maze.PlaceExit(exit);
        var report = maze.EnsureFullAccessibility(() => DoorState.Open);
        if (!report.IsFullyAccessible)
            throw new InvalidOperationException("Az erdei képernyő járathálózata nem lett teljesen bejárható.");
        return maze;
    }

    private void FillForest(Maze maze)
    {
        var palette = _forest.Palette;
        var woodland = new ForestTerrainField(maze.Width, maze.Height, _forest.GroveSize, Random);
        var biome = new ForestTerrainField(maze.Width, maze.Height,
            new IntRange(_forest.BiomeSize, _forest.BiomeSize), Random);
        var thicket = new ForestTerrainField(maze.Width, maze.Height, _forest.BushGroupSize, Random);
        var woodlandThreshold = woodland.Threshold(_forest.ForestDensity);
        var pineThreshold = biome.Threshold(_forest.PineChance);
        var thicketThreshold = thicket.Threshold(_forest.ThicketChance);
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++)
        {
            var position = new Position(x, y);
            var boundary = !IsInterior(maze, position);
            if (!boundary && woodland[position] < woodlandThreshold)
                maze.Carve(position);
            else
                maze.SetTerrain(position, thicket[position] >= thicketThreshold ? palette.Thicket :
                    biome[position] >= pineThreshold ? palette.Pine : palette.Tree);
        }
    }

    private void PlaceWetlands(Maze maze)
    {
        var shore = new ForestTerrainField(maze.Width, maze.Height, new IntRange(3, 6), Random);
        var shoreThreshold = shore.Threshold(_forest.MarshChance);
        PlacePatches(_forest.MarshCount.Roll(Random), _forest.MarshRadius, lake: false);
        PlacePatches(_forest.LakeCount.Roll(Random), _forest.LakeRadius, lake: true);

        void PlacePatches(int count, IntRange radiusRange, bool lake)
        {
            for (var index = 0; index < count; index++)
            for (var attempt = 0; attempt < 60; attempt++)
            {
                var radius = radiusRange.Roll(Random);
                var center = new Position(Random.Next(2, maze.Width - 2), Random.Next(2, maze.Height - 2));
                var patch = OrganicPatch(maze, center, radius * (0.9 + Random.NextDouble() * 0.5),
                    radius * (0.7 + Random.NextDouble() * 0.5), lake ? 1.4 : 1).ToArray();
                if (patch.Length == 0 || patch.Any(cell => _reserved[cell.Position.X, cell.Position.Y]) ||
                    !patch.Any(cell => cell.Distance <= 0.7)) continue;
                foreach (var (position, distance) in patch)
                {
                    // Víz sosem zárja el a teljes képernyőt: a külső keret belső szomszédja száraz marad.
                    if (position.X < 2 || position.Y < 2 ||
                        position.X >= maze.Width - 2 || position.Y >= maze.Height - 2) continue;
                    if (lake && distance <= 1)
                        maze.SetTerrain(position, _forest.Palette.Water);
                    else if (!lake || shore[position] >= shoreThreshold)
                        maze.SetTerrain(position, _forest.Palette.Marsh);
                }
                break;
            }
        }
    }

    private IEnumerable<(Position Position, double Distance)> OrganicPatch(Maze maze, Position center,
        double radiusX, double radiusY, double extent = 1)
    {
        var rotation = Random.NextDouble() * Math.PI;
        var phase = Random.NextDouble() * Math.PI * 2;
        var reach = Math.Min(Math.Max(maze.Width, maze.Height),
            (int)Math.Ceiling(Math.Max(radiusX, radiusY) * extent * 2.1));
        for (var y = Math.Max(1, center.Y - reach); y <= Math.Min(maze.Height - 2, center.Y + reach); y++)
        for (var x = Math.Max(1, center.X - reach); x <= Math.Min(maze.Width - 2, center.X + reach); x++)
        {
            var dx = (x - center.X) * 0.65;
            var dy = (double)(y - center.Y);
            var u = (dx * Math.Cos(rotation) - dy * Math.Sin(rotation)) / radiusX;
            var v = (dx * Math.Sin(rotation) + dy * Math.Cos(rotation)) / radiusY;
            var angle = Math.Atan2(v, u);
            var edge = 1 + 0.17 * Math.Sin(angle * 3 + phase) + 0.10 * Math.Sin(angle * 5 - phase);
            var distance = Math.Sqrt(u * u + v * v) / edge;
            if (distance <= extent) yield return (new Position(x, y), distance);
        }
    }

    private (List<Room> Rooms, List<BuildingSite> Buildings) PlaceClearingsAndBuildings(Maze maze)
    {
        var result = new List<Room>();
        var buildings = new List<BuildingSite>();
        var buildingCount = Math.Min(Settings.RoomCount, _forest.BuildingCount.Roll(Random));
        var attempts = Math.Max(100, Settings.RoomCount * 300);
        for (var attempt = 0; attempt < attempts && result.Count < Settings.RoomCount; attempt++)
        {
            var isBuilding = result.Count < buildingCount;
            var layout = isBuilding ? RollBuildingLayout() : ForestBuildingLayout.Cabin;
            var (width, height) = isBuilding ? RollBuildingSize(layout) :
                (Random.Next(Settings.MinimumRoomSize, Settings.MaximumRoomSize + 1),
                    Random.Next(Settings.MinimumRoomSize, Settings.MaximumRoomSize + 1));
            if (width >= maze.Width - 5 || height >= maze.Height - 5) continue;
            var room = new Room(new Position(Random.Next(2, maze.Width - width - 2),
                Random.Next(2, maze.Height - height - 2)), width, height);
            if (room.Contains(maze.Entrance) || room.Contains(maze.Exit) ||
                maze.StartingRoom is { } startingRoom && Overlaps(room, startingRoom) ||
                result.Any(other => Overlaps(room, other)))
                continue;
            CarveRoom(maze, room);
            if (!isBuilding)
                foreach (var (position, _) in OrganicPatch(maze, Center(room), width * 0.5 + 1, height * 0.5 + 1))
                    maze.Carve(position);
            ReserveRoom(maze, room);
            result.Add(room);
            if (isBuilding) buildings.Add(new BuildingSite(room, layout, RollBuildingStyle(layout)));
            else maze.AddRoom(room with { Kind = RoomKind.Clearing });
        }
        if (result.Count < Settings.RoomCount)
            throw new InvalidOperationException(
                $"Az erdei képernyőn csak {result.Count}/{Settings.RoomCount} tisztás fért el.");
        return (result, buildings);
    }

    private ForestBuildingLayout RollBuildingLayout()
    {
        var roll = Random.NextDouble();
        if (roll < _forest.LabyrinthBuildingChance) return ForestBuildingLayout.Labyrinth;
        return roll < _forest.LabyrinthBuildingChance + _forest.ManorBuildingChance
            ? ForestBuildingLayout.Manor
            : ForestBuildingLayout.Cabin;
    }

    private (int Width, int Height) RollBuildingSize(ForestBuildingLayout layout) => layout switch
    {
        ForestBuildingLayout.Manor =>
            (_forest.ManorBuildingWidth.Roll(Random), _forest.ManorBuildingHeight.Roll(Random)),
        ForestBuildingLayout.Labyrinth =>
            (_forest.LabyrinthBuildingWidth.Roll(Random), _forest.LabyrinthBuildingHeight.Roll(Random)),
        _ => (_forest.BuildingSize.Roll(Random), _forest.BuildingSize.Roll(Random))
    };

    private ForestBuildingStyleDefinition RollBuildingStyle(ForestBuildingLayout layout)
    {
        var candidates = _forest.BuildingStyles.Where(style => style.Allows(layout) && style.Weight > 0).ToArray();
        if (candidates.Length == 0)
            return new ForestBuildingStyleDefinition("default", _forest.Palette.BuildingWall);
        var total = candidates.Sum(style => style.Weight);
        var roll = Random.NextDouble() * total;
        foreach (var candidate in candidates)
        {
            roll -= candidate.Weight;
            if (roll <= 0) return candidate;
        }
        return candidates[^1];
    }

    private void ReserveRoom(Maze maze, Room room)
    {
        for (var y = Math.Max(0, room.TopLeft.Y - 2); y <= Math.Min(maze.Height - 1, room.TopLeft.Y + room.Height + 1); y++)
        for (var x = Math.Max(0, room.TopLeft.X - 2); x <= Math.Min(maze.Width - 1, room.TopLeft.X + room.Width + 1); x++)
            _reserved[x, y] = true;
    }

    private void BuildStructures(Maze maze, IEnumerable<BuildingSite> buildings)
    {
        foreach (var building in buildings)
        {
            var room = building.Footprint;
            _buildingWall = building.Style.Wall;
            var interiorRooms = building.Layout switch
            {
                ForestBuildingLayout.Manor => BuildManorInterior(maze, room),
                ForestBuildingLayout.Labyrinth => BuildLabyrinthInterior(maze, room),
                _ => BuildCabinInterior(maze, room)
            };

            foreach (var position in BuildingBoundary(room))
            {
                maze.RemoveDoor(position);
                maze.SetTerrain(position, _buildingWall);
            }

            var connections = BuildingBoundaryConnections(room)
                // A külső ajtó ne essen egy belső válaszfal síkjába. Korábban ilyenkor
                // a bekötés kifaragta a válaszfal első celláját, ajtó nélküli lyukat hagyva rajta.
                // Csak eleve járható helyiségbe nyíló falszakasz lehet bejárat.
                .Where(connection => maze.IsWalkable(connection.Inside))
                .ToArray();
            if (connections.Length == 0)
                throw new InvalidOperationException("Az erdei épülethez nem található elérhető bejárat.");
            var connectedEntrances = connections.Where(connection => maze.IsWalkable(connection.Outside)).ToArray();
            var entranceCandidates = connectedEntrances.Length > 0 ? connectedEntrances : connections;
            var entrance = entranceCandidates[Random.Next(entranceCandidates.Length)];
            ConnectBuildingExterior(maze, room, entrance.Outside);
            maze.PlaceDoor(entrance.Wall, RollBuildingDoorState());

            if (building.Layout != ForestBuildingLayout.Cabin && connections.Length > 1 &&
                Random.NextDouble() < _forest.BuildingSecondEntranceChance)
            {
                var second = connections.Where(candidate => candidate.Wall != entrance.Wall)
                    .OrderByDescending(candidate => Manhattan(candidate.Wall, entrance.Wall)).First();
                ConnectBuildingExterior(maze, room, second.Outside);
                maze.PlaceDoor(second.Wall, RollBuildingDoorState());
            }

            var roomKind = building.Layout switch
            {
                ForestBuildingLayout.Manor => RoomKind.Manor,
                ForestBuildingLayout.Labyrinth => RoomKind.Labyrinth,
                _ => RoomKind.Cabin
            };
            var buildingId = $"BUILDING_{room.TopLeft.X}_{room.TopLeft.Y}";
            foreach (var interiorRoom in interiorRooms)
                maze.AddRoom(interiorRoom with { Kind = roomKind, BuildingId = buildingId });
        }
    }

    private IReadOnlyList<Room> BuildCabinInterior(Maze maze, Room room)
    {
        if (room.Width < 5 || room.Height < 5 || Random.NextDouble() >= _forest.BuildingPartitionChance)
            return [room];
        if (room.Width >= room.Height)
        {
            var x = Random.Next(room.TopLeft.X + 2, room.TopLeft.X + room.Width - 2);
            var doorY = Random.Next(room.TopLeft.Y + 1, room.TopLeft.Y + room.Height - 1);
            for (var y = room.TopLeft.Y; y < room.TopLeft.Y + room.Height; y++)
                maze.SetTerrain(new Position(x, y), _buildingWall);
            maze.PlaceDoor(new Position(x, doorY), RollBuildingDoorState());
            return
            [
                new Room(room.TopLeft, x - room.TopLeft.X, room.Height),
                new Room(new Position(x + 1, room.TopLeft.Y), room.TopLeft.X + room.Width - x - 1, room.Height)
            ];
        }
        var splitY = Random.Next(room.TopLeft.Y + 2, room.TopLeft.Y + room.Height - 2);
        var splitDoorX = Random.Next(room.TopLeft.X + 1, room.TopLeft.X + room.Width - 1);
        for (var x = room.TopLeft.X; x < room.TopLeft.X + room.Width; x++)
            maze.SetTerrain(new Position(x, splitY), _buildingWall);
        maze.PlaceDoor(new Position(splitDoorX, splitY), RollBuildingDoorState());
        return
        [
            new Room(room.TopLeft, room.Width, splitY - room.TopLeft.Y),
            new Room(new Position(room.TopLeft.X, splitY + 1), room.Width,
                room.TopLeft.Y + room.Height - splitY - 1)
        ];
    }

    private IReadOnlyList<Room> BuildManorInterior(Maze maze, Room footprint)
    {
        var rooms = new List<Room> { footprint };
        var targetCount = _forest.ManorRoomCount.Roll(Random);
        var minimum = _forest.BuildingMinimumRoomSize;
        while (rooms.Count < targetCount)
        {
            var candidates = rooms.Where(room => room.Width >= minimum * 2 + 1 ||
                    room.Height >= minimum * 2 + 1)
                .OrderByDescending(room => room.Width * room.Height).ThenBy(_ => Random.Next()).ToArray();
            if (candidates.Length == 0) break;
            var room = candidates[0];
            var canSplitVertically = room.Width >= minimum * 2 + 1;
            var canSplitHorizontally = room.Height >= minimum * 2 + 1;
            var splitVertically = canSplitVertically && (!canSplitHorizontally ||
                room.Width > room.Height || room.Width == room.Height && Random.Next(2) == 0);
            rooms.Remove(room);

            if (splitVertically)
            {
                var x = Random.Next(room.TopLeft.X + minimum,
                    room.TopLeft.X + room.Width - minimum);
                for (var y = room.TopLeft.Y; y < room.TopLeft.Y + room.Height; y++)
                    maze.SetTerrain(new Position(x, y), _buildingWall);
                maze.PlaceDoor(new Position(x, Random.Next(room.TopLeft.Y, room.TopLeft.Y + room.Height)),
                    RollBuildingDoorState());
                rooms.Add(new Room(room.TopLeft, x - room.TopLeft.X, room.Height));
                rooms.Add(new Room(new Position(x + 1, room.TopLeft.Y),
                    room.TopLeft.X + room.Width - x - 1, room.Height));
            }
            else
            {
                var y = Random.Next(room.TopLeft.Y + minimum,
                    room.TopLeft.Y + room.Height - minimum);
                for (var x = room.TopLeft.X; x < room.TopLeft.X + room.Width; x++)
                    maze.SetTerrain(new Position(x, y), _buildingWall);
                maze.PlaceDoor(new Position(Random.Next(room.TopLeft.X, room.TopLeft.X + room.Width), y),
                    RollBuildingDoorState());
                rooms.Add(new Room(room.TopLeft, room.Width, y - room.TopLeft.Y));
                rooms.Add(new Room(new Position(room.TopLeft.X, y + 1), room.Width,
                    room.TopLeft.Y + room.Height - y - 1));
            }
        }

        AddExtraManorConnections(maze, rooms);
        return rooms;
    }

    private void AddExtraManorConnections(Maze maze, IReadOnlyList<Room> rooms)
    {
        for (var firstIndex = 0; firstIndex < rooms.Count; firstIndex++)
        for (var secondIndex = firstIndex + 1; secondIndex < rooms.Count; secondIndex++)
        {
            if (Random.NextDouble() >= _forest.BuildingExtraConnectionChance) continue;
            var first = rooms[firstIndex];
            var second = rooms[secondIndex];
            if (first.TopLeft.X + first.Width == second.TopLeft.X - 1 ||
                second.TopLeft.X + second.Width == first.TopLeft.X - 1)
            {
                var wallX = first.TopLeft.X < second.TopLeft.X
                    ? first.TopLeft.X + first.Width
                    : second.TopLeft.X + second.Width;
                var from = Math.Max(first.TopLeft.Y, second.TopLeft.Y);
                var to = Math.Min(first.TopLeft.Y + first.Height, second.TopLeft.Y + second.Height);
                if (from < to) TryPlaceExtraDoor(maze, Enumerable.Range(from, to - from)
                    .Select(y => new Position(wallX, y)).ToArray());
            }
            else if (first.TopLeft.Y + first.Height == second.TopLeft.Y - 1 ||
                     second.TopLeft.Y + second.Height == first.TopLeft.Y - 1)
            {
                var wallY = first.TopLeft.Y < second.TopLeft.Y
                    ? first.TopLeft.Y + first.Height
                    : second.TopLeft.Y + second.Height;
                var from = Math.Max(first.TopLeft.X, second.TopLeft.X);
                var to = Math.Min(first.TopLeft.X + first.Width, second.TopLeft.X + second.Width);
                if (from < to) TryPlaceExtraDoor(maze, Enumerable.Range(from, to - from)
                    .Select(x => new Position(x, wallY)).ToArray());
            }
        }
    }

    private void TryPlaceExtraDoor(Maze maze, IReadOnlyList<Position> sharedWall)
    {
        if (sharedWall.Any(position => maze.GetDoorAt(position) is not null)) return;
        var candidates = sharedWall.Where(position => maze.GetTerrainStyle(position)?.Id ==
            _buildingWall.Id).ToArray();
        if (candidates.Length > 0) maze.PlaceDoor(candidates[Random.Next(candidates.Length)], RollBuildingDoorState());
    }

    private IReadOnlyList<Room> BuildLabyrinthInterior(Maze maze, Room footprint)
    {
        const int nodeSize = 3;
        const int gridStep = 5;
        foreach (var position in footprint.InteriorPositions())
        {
            maze.RemoveDoor(position);
            maze.SetTerrain(position, _buildingWall);
        }

        var gridWidth = (footprint.Width - nodeSize) / gridStep + 1;
        var gridHeight = (footprint.Height - nodeSize) / gridStep + 1;
        var visited = new bool[gridWidth, gridHeight];
        var rooms = new List<Room>(gridWidth * gridHeight);
        var passages = new HashSet<(int FirstX, int FirstY, int SecondX, int SecondY)>();

        Room NodeRoom(int x, int y) => new(new Position(
            footprint.TopLeft.X + x * gridStep, footprint.TopLeft.Y + y * gridStep), nodeSize, nodeSize);

        void CarveNode(int x, int y)
        {
            var node = NodeRoom(x, y);
            foreach (var position in node.InteriorPositions()) maze.Carve(position);
        }

        void CarvePassage(int firstX, int firstY, int secondX, int secondY)
        {
            var first = NodeRoom(firstX, firstY);
            var second = NodeRoom(secondX, secondY);
            if (firstX != secondX)
            {
                var from = Math.Min(first.TopLeft.X, second.TopLeft.X) + nodeSize;
                var to = Math.Max(first.TopLeft.X, second.TopLeft.X);
                var y = first.TopLeft.Y + nodeSize / 2;
                for (var x = from; x < to; x++) maze.Carve(new Position(x, y));
            }
            else
            {
                var from = Math.Min(first.TopLeft.Y, second.TopLeft.Y) + nodeSize;
                var to = Math.Max(first.TopLeft.Y, second.TopLeft.Y);
                var x = first.TopLeft.X + nodeSize / 2;
                for (var y = from; y < to; y++) maze.Carve(new Position(x, y));
            }
            passages.Add(NormalizePassage(firstX, firstY, secondX, secondY));
        }

        void CarveFrom(int x, int y)
        {
            visited[x, y] = true;
            CarveNode(x, y);
            var neighbors = new[] { (X: x + 1, Y: y), (X: x - 1, Y: y), (X: x, Y: y + 1), (X: x, Y: y - 1) }
                .Where(next => next.X >= 0 && next.X < gridWidth && next.Y >= 0 && next.Y < gridHeight)
                .OrderBy(_ => Random.Next()).ToArray();
            foreach (var next in neighbors)
            {
                if (visited[next.X, next.Y]) continue;
                CarvePassage(x, y, next.X, next.Y);
                CarveFrom(next.X, next.Y);
            }
        }

        CarveFrom(0, 0);
        for (var y = 0; y < gridHeight; y++)
        for (var x = 0; x < gridWidth; x++)
        {
            rooms.Add(NodeRoom(x, y));
            foreach (var next in new[] { (X: x + 1, Y: y), (X: x, Y: y + 1) })
            {
                if (next.X >= gridWidth || next.Y >= gridHeight ||
                    passages.Contains(NormalizePassage(x, y, next.X, next.Y)) ||
                    Random.NextDouble() >= _forest.BuildingExtraConnectionChance) continue;
                CarvePassage(x, y, next.X, next.Y);
            }
        }
        return rooms;

        static (int FirstX, int FirstY, int SecondX, int SecondY) NormalizePassage(
            int firstX, int firstY, int secondX, int secondY) =>
            firstX < secondX || firstX == secondX && firstY <= secondY
                ? (firstX, firstY, secondX, secondY)
                : (secondX, secondY, firstX, firstY);
    }

    private void ConnectBuildingExterior(Maze maze, Room footprint, Position outside)
    {
        if (maze.IsWalkable(outside)) return;
        var boundary = BuildingBoundary(footprint).ToHashSet();
        var pending = new Queue<Position>();
        var previous = new Dictionary<Position, Position>();
        var visited = new HashSet<Position> { outside };
        pending.Enqueue(outside);
        Position? destination = null;
        while (pending.TryDequeue(out var current) && destination is null)
        {
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!IsInterior(maze, next) || footprint.Contains(next) || boundary.Contains(next) ||
                    !visited.Add(next)) continue;
                previous[next] = current;
                if (maze.IsWalkable(next)) { destination = next; break; }
                pending.Enqueue(next);
            }
        }
        if (destination is null)
            throw new InvalidOperationException("Az erdei épület bejárata nem köthető a külső ösvényhez.");
        var position = destination.Value;
        while (position != outside)
        {
            position = previous[position];
            if (maze.GetTerrainStyle(position)?.Id == _forest.Palette.Water.Id)
                maze.SetTerrain(position, _forest.Palette.Marsh);
            else
                maze.Carve(position);
        }
    }

    private DoorState RollBuildingDoorState()
    {
        var roll = Random.NextDouble();
        if (roll < _forest.LockedBuildingDoorChance) return DoorState.Locked;
        return roll < _forest.LockedBuildingDoorChance + _forest.OpenBuildingDoorChance
            ? DoorState.Open
            : DoorState.Closed;
    }

    private static IEnumerable<Position> BuildingBoundary(Room room)
    {
        for (var x = room.TopLeft.X - 1; x <= room.TopLeft.X + room.Width; x++)
        {
            yield return new Position(x, room.TopLeft.Y - 1);
            yield return new Position(x, room.TopLeft.Y + room.Height);
        }
        for (var y = room.TopLeft.Y; y < room.TopLeft.Y + room.Height; y++)
        {
            yield return new Position(room.TopLeft.X - 1, y);
            yield return new Position(room.TopLeft.X + room.Width, y);
        }
    }

    private static IEnumerable<(Position Wall, Position Outside, Position Inside)> BuildingBoundaryConnections(Room room)
    {
        for (var x = room.TopLeft.X; x < room.TopLeft.X + room.Width; x++)
        {
            yield return (new Position(x, room.TopLeft.Y - 1), new Position(x, room.TopLeft.Y - 2),
                new Position(x, room.TopLeft.Y));
            yield return (new Position(x, room.TopLeft.Y + room.Height),
                new Position(x, room.TopLeft.Y + room.Height + 1),
                new Position(x, room.TopLeft.Y + room.Height - 1));
        }
        for (var y = room.TopLeft.Y; y < room.TopLeft.Y + room.Height; y++)
        {
            yield return (new Position(room.TopLeft.X - 1, y), new Position(room.TopLeft.X - 2, y),
                new Position(room.TopLeft.X, y));
            yield return (new Position(room.TopLeft.X + room.Width, y),
                new Position(room.TopLeft.X + room.Width + 1, y),
                new Position(room.TopLeft.X + room.Width - 1, y));
        }
    }

    private static void CarveRoom(Maze maze, Room room)
    {
        foreach (var position in room.InteriorPositions()) maze.Carve(position);
    }

    private void CarveMeanderingTrail(Maze maze, Position start, Position destination)
    {
        var dx = destination.X - start.X;
        var dy = destination.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var segments = Math.Max(1, (int)Math.Ceiling(length / 9));
        var phase = Random.NextDouble() * Math.PI * 2;
        var amplitude = Math.Min(10, length * 0.28) * _forest.TrailWinding;
        var current = start;
        for (var step = 1; step <= segments; step++)
        {
            var t = step / (double)segments;
            var offset = Math.Sin(t * Math.PI) * Math.Sin(t * Math.PI * 3 + phase) * amplitude;
            var waypoint = step == segments ? destination : NearestDryPosition(maze, new Position(
                Math.Clamp((int)Math.Round(start.X + dx * t - dy / Math.Max(1, length) * offset), 1, maze.Width - 2),
                Math.Clamp((int)Math.Round(start.Y + dy * t + dx / Math.Max(1, length) * offset), 1, maze.Height - 2)));
            foreach (var position in FindTrail(maze, current, waypoint)) CarveTrailWidth(maze, position);
            current = waypoint;
        }
    }

    private Position NearestDryPosition(Maze maze, Position center)
    {
        if (maze.GetTerrainStyle(center)?.Id != _forest.Palette.Water.Id) return center;
        for (var radius = 1; radius < Math.Max(maze.Width, maze.Height); radius++)
        for (var y = Math.Max(1, center.Y - radius); y <= Math.Min(maze.Height - 2, center.Y + radius); y++)
        for (var x = Math.Max(1, center.X - radius); x <= Math.Min(maze.Width - 2, center.X + radius); x++)
        {
            if (Math.Abs(x - center.X) + Math.Abs(y - center.Y) != radius) continue;
            var position = new Position(x, y);
            if (maze.GetTerrainStyle(position)?.Id != _forest.Palette.Water.Id) return position;
        }
        throw new InvalidOperationException("Az erdei ösvényhez nincs száraz mező.");
    }

    private List<Position> FindTrail(Maze maze, Position start, Position? destination, bool[,]? network = null,
        bool allowWater = false)
    {
        var frontier = new PriorityQueue<(Position Position, double Cost), double>();
        var costs = new Dictionary<Position, double> { [start] = 0 };
        var previous = new Dictionary<Position, Position>();
        frontier.Enqueue((start, 0), 0);
        while (frontier.TryDequeue(out var entry, out _))
        {
            var current = entry.Position;
            if (entry.Cost > costs[current]) continue;
            if (current == destination || network is not null && network[current.X, current.Y])
            {
                var path = new List<Position> { current };
                while (current != start) { current = previous[current]; path.Add(current); }
                path.Reverse();
                return path;
            }
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!IsInterior(maze, next)) continue;
                var terrain = maze.GetTerrainStyle(next);
                var water = terrain?.Id == _forest.Palette.Water.Id;
                if (water && !allowWater) continue;
                var cost = water ? 80 : network is not null
                    ? maze.IsWalkable(next) ? 1 : 8
                    : 1 + _routeField[next] * 5 * _forest.TrailWinding +
                      (terrain?.Id == _forest.Palette.Marsh.Id ? 5 : maze.IsWalkable(next) ? 0 : 1.5);
                var total = entry.Cost + cost;
                if (costs.TryGetValue(next, out var known) && known <= total) continue;
                costs[next] = total;
                previous[next] = current;
                frontier.Enqueue((next, total), total + (destination is { } target ? Manhattan(next, target) : 0));
            }
        }
        // Összeérő tavak körbezárhatnak egy száraz szigetet. Csak ilyenkor készül keskeny mocsári átkelő.
        if (!allowWater) return FindTrail(maze, start, destination, network, allowWater: true);
        throw new InvalidOperationException("Az erdei területeket nem sikerült ösvénnyel összekötni.");
    }

    private void CarveTrailWidth(Maze maze, Position center)
    {
        if (maze.GetTerrainStyle(center)?.Id == _forest.Palette.Water.Id)
        {
            maze.SetTerrain(center, _forest.Palette.Marsh);
            return;
        }
        var left = (_forest.TrailWidth - 1) / 2;
        var right = _forest.TrailWidth / 2;
        for (var y = center.Y - left; y <= center.Y + right; y++)
        for (var x = center.X - left; x <= center.X + right; x++)
        {
            var position = new Position(x, y);
            if (!IsInterior(maze, position) ||
                maze.GetTerrainStyle(position)?.Id == _forest.Palette.Water.Id) continue;
            maze.Carve(position);
        }
    }

    private void ConnectOpenRegions(Maze maze)
    {
        var network = new bool[maze.Width, maze.Height];
        Flood(maze.Entrance);
        for (var y = 1; y < maze.Height - 1; y++)
        for (var x = 1; x < maze.Width - 1; x++)
        {
            var position = new Position(x, y);
            if (network[x, y] || !maze.IsWalkable(position)) continue;
            var path = FindTrail(maze, position, null, network);
            foreach (var cell in path) CarveTrailWidth(maze, cell);
            // Az út kiszélesítése több korábbi szigetet is elérhetett.
            foreach (var cell in path) Flood(cell);
        }

        void Flood(Position start)
        {
            var queue = new Queue<Position>();
            network[start.X, start.Y] = true;
            queue.Enqueue(start);
            while (queue.TryDequeue(out var current))
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!IsInterior(maze, next) || network[next.X, next.Y] || !maze.IsWalkable(next)) continue;
                network[next.X, next.Y] = true;
                queue.Enqueue(next);
            }
        }
    }

    private void DecorateTerrain(Maze maze)
    {
        var shrubs = new ForestTerrainField(maze.Width, maze.Height, _forest.BushGroupSize, Random);
        var ground = new ForestTerrainField(maze.Width, maze.Height, new IntRange(3, 8), Random);
        var bushThreshold = shrubs.Threshold(_forest.BushChance + _forest.FlowerBushChance);
        var flowerThreshold = shrubs.Threshold(_forest.FlowerBushChance);
        var undergrowthThreshold = ground.Threshold(_forest.UndergrowthChance + _forest.DenseUndergrowthChance);
        var denseThreshold = ground.Threshold(_forest.DenseUndergrowthChance);
        var edgeDistances = new int[maze.Width, maze.Height];
        var frontier = new Queue<Position>();
        for (var y = 1; y < maze.Height - 1; y++)
        for (var x = 1; x < maze.Width - 1; x++)
        {
            var position = new Position(x, y);
            edgeDistances[x, y] = int.MaxValue;
            var style = maze.GetTerrainStyle(position);
            if (style?.Id != _forest.Palette.Tree.Id && style?.Id != _forest.Palette.Pine.Id &&
                style?.Id != _forest.Palette.Thicket.Id) continue;
            edgeDistances[x, y] = 0;
            frontier.Enqueue(position);
        }
        while (frontier.TryDequeue(out var current))
        foreach (var direction in Directions)
        {
            var next = current + direction;
            var distance = edgeDistances[current.X, current.Y] + 1;
            if (!IsInterior(maze, next) || distance > _forest.ForestEdgeWidth ||
                distance >= edgeDistances[next.X, next.Y]) continue;
            edgeDistances[next.X, next.Y] = distance;
            frontier.Enqueue(next);
        }
        for (var y = 1; y < maze.Height - 1; y++)
        for (var x = 1; x < maze.Width - 1; x++)
        {
            var position = new Position(x, y);
            if (_reserved[x, y] || maze.Tiles[x, y] != Maze.Floor) continue;
            if (edgeDistances[x, y] <= _forest.ForestEdgeWidth && shrubs[position] >= bushThreshold)
                maze.SetTerrain(position, shrubs[position] >= flowerThreshold ? _forest.Palette.FlowerBush : _forest.Palette.Bush);
            else if (ground[position] >= denseThreshold)
                maze.SetTerrain(position, _forest.Palette.DenseUndergrowth);
            else if (ground[position] >= undergrowthThreshold)
                maze.SetTerrain(position, _forest.Palette.Undergrowth);
        }
    }

    private static bool IsInterior(Maze maze, Position position) =>
        position.X > 0 && position.X < maze.Width - 1 && position.Y > 0 && position.Y < maze.Height - 1;

    private static Position Center(Room room) =>
        new(room.TopLeft.X + room.Width / 2, room.TopLeft.Y + room.Height / 2);

    private static int Manhattan(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    private static bool Overlaps(Room first, Room second) =>
        first.TopLeft.X - 2 <= second.TopLeft.X + second.Width + 1 &&
        first.TopLeft.X + first.Width + 1 >= second.TopLeft.X - 2 &&
        first.TopLeft.Y - 2 <= second.TopLeft.Y + second.Height + 1 &&
        first.TopLeft.Y + first.Height + 1 >= second.TopLeft.Y - 2;

    private static void ValidateForestConfiguration(ForestGenerationConfiguration configuration)
    {
        var probabilities = new[]
        {
            configuration.ForestDensity, configuration.PineChance, configuration.BushChance,
            configuration.FlowerBushChance, configuration.ThicketChance, configuration.UndergrowthChance,
            configuration.DenseUndergrowthChance, configuration.MarshChance, configuration.TrailWinding,
            configuration.ExtraTrailChance, configuration.BuildingPartitionChance,
            configuration.ManorBuildingChance, configuration.LabyrinthBuildingChance,
            configuration.BuildingExtraConnectionChance, configuration.BuildingSecondEntranceChance,
            configuration.LockedBuildingDoorChance, configuration.OpenBuildingDoorChance
        };
        if (probabilities.Any(value => !double.IsFinite(value) || value is < 0 or > 1) ||
            configuration.BushChance + configuration.FlowerBushChance > 1 ||
            configuration.UndergrowthChance + configuration.DenseUndergrowthChance > 1 ||
            configuration.ManorBuildingChance + configuration.LabyrinthBuildingChance > 1 ||
            configuration.LockedBuildingDoorChance + configuration.OpenBuildingDoorChance > 1)
            throw new ArgumentOutOfRangeException(nameof(configuration),
                "Az erdei gyakoriságoknak 0 és 1 közé kell esniük, a részarányok összege legfeljebb 1 lehet.");
        if (!ValidRange(configuration.LakeCount, 0) || !ValidRange(configuration.LakeRadius, 1) ||
            !ValidRange(configuration.MarshCount, 0) || !ValidRange(configuration.MarshRadius, 1) ||
            !ValidRange(configuration.GroveSize, 2) || !ValidRange(configuration.BushGroupSize, 1) ||
            !ValidRange(configuration.BuildingCount, 0) || !ValidRange(configuration.BuildingSize, 3) ||
            !ValidRange(configuration.ManorBuildingWidth, 7) ||
            !ValidRange(configuration.ManorBuildingHeight, 7) ||
            !ValidRange(configuration.ManorRoomCount, 3) ||
            !ValidRange(configuration.LabyrinthBuildingWidth, 8) ||
            !ValidRange(configuration.LabyrinthBuildingHeight, 8) ||
            ((configuration.LabyrinthBuildingWidth.Minimum - 3) / 5 + 1) *
            ((configuration.LabyrinthBuildingHeight.Minimum - 3) / 5 + 1) < 6 ||
            configuration.BuildingMinimumRoomSize < 3 ||
            configuration.BiomeSize < 2 || configuration.ForestEdgeWidth is < 1 or > 10 ||
            configuration.TrailWidth is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(configuration), "Az erdei méretbeállítások érvénytelenek.");
        if (configuration.Palette.All.Select(style => style.Rune.Value).Distinct().Count() !=
            configuration.Palette.All.Count)
            throw new ArgumentException("Az erdei tereprúnáknak egyedinek kell lenniük.", nameof(configuration));
        if (configuration.BuildingStyles.Any(style => string.IsNullOrWhiteSpace(style.Id) ||
                !double.IsFinite(style.Weight) || style.Weight <= 0) ||
            configuration.BuildingStyles.Select(style => style.Id).Distinct(StringComparer.Ordinal).Count() !=
            configuration.BuildingStyles.Count ||
            configuration.Palette.All.Concat(configuration.BuildingStyles.Select(style => style.Wall))
                .GroupBy(style => style.Rune.Value).Any(group => group.Select(style => style.Id).Distinct().Count() > 1))
            throw new ArgumentException("Az épületstílusok azonosítója, súlya vagy falrúnája érvénytelen.",
                nameof(configuration));

        static bool ValidRange(IntRange range, int minimum) =>
            range.Minimum >= minimum && range.Maximum >= range.Minimum && range.Maximum < int.MaxValue;
    }
}
