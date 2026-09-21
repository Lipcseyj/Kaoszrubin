using KaoszRubin.Domain.Combat;

namespace KaoszRubin.World;

/// <summary>2×2-es folyosókból, szűkületekből és ajtós szobákból álló labirintust készít.</summary>
public class MazeGenerator
{
    protected virtual int CorridorNodeWidth => 2;
    protected virtual int GridStep => 5;
    private static readonly Direction[] Directions = Enum.GetValues<Direction>();
    protected readonly Random Random;
    protected readonly MazeGenerationSettings Settings;
    private readonly IReadOnlyList<ResolvedEnemyEncounter> _roomEncounters;
    private readonly IReadOnlyList<ResolvedEnemyEncounter> _corridorEncounters;

    public MazeGenerator(MazeGenerationSettings settings, IReadOnlyList<ResolvedEnemyEncounter> roomEncounters,
        IReadOnlyList<ResolvedEnemyEncounter> corridorEncounters, Random? random = null)
    {
        Settings = settings;
        Random = random ?? new Random();
        _roomEncounters = roomEncounters;
        _corridorEncounters = corridorEncounters;
        ValidateSettings(Settings);
    }

    public Maze Create(int width, int height)
    {
        // A sikertelen elrendezést még objektumok és szereplők létrehozása előtt eldobjuk.
        for (var attempt = 0; attempt < 128; attempt++)
        {
            var maze = CreateLayout(width, height);
            if (!SpecialRoomPlacer.TryAssign(maze, Settings, Random)) continue;
            PlaceMapObjects(maze);
            return maze;
        }
        throw new InvalidOperationException("128 kísérletből sem sikerült a speciális szobák elhelyezése.");
    }

    private Maze CreateLayout(int width, int height)
    {
        var maze = new Maze(width, height, Settings.WallRune, Settings.WallColor, Settings.LevelName);
        var gridWidth = (width - CorridorNodeWidth - 1) / GridStep + 1;
        var gridHeight = (height - CorridorNodeWidth - 1) / GridStep + 1;
        var visited = new bool[gridWidth, gridHeight];

        PlaceRooms(maze, gridWidth, gridHeight);
        CarveFrom(maze, new Position(0, 0), visited, gridWidth, gridHeight);
        CreateStartingRoom(maze);
        ConnectRoomsToMaze(maze);
        maze.PlaceExit(ToMazePosition(new Position(gridWidth - 1, gridHeight - 1)));
        // Végső biztosíték: ha bármi mégis leválasztott maradt, a legkevesebb faláttöréssel visszaköti a hálózathoz.
        maze.EnsureFullAccessibility(RollDoorState);
        return maze;
    }

    /// <summary>A bejáratnál garantált 3×3-as belső teret készít a négyfős partinak.</summary>
    protected virtual void CreateStartingRoom(Maze maze)
    {
        const int size = 3;
        var room = new Room(new Position(1, 1), size, size);

        // Megőrizzük a már kivésett folyosókhoz vezető jobb és alsó kapcsolatot,
        // majd valódi falburkot építünk a 3×3-as belső tér köré.
        var doors = new List<Position>();
        var rightDoor = Enumerable.Range(room.TopLeft.Y, room.Height)
            .Select(y => new Position(room.TopLeft.X + room.Width, y))
            .FirstOrDefault(boundary => maze.IsWalkable(boundary + Direction.Right));
        if (rightDoor != default) doors.Add(rightDoor);
        var bottomDoor = Enumerable.Range(room.TopLeft.X, room.Width)
            .Select(x => new Position(x, room.TopLeft.Y + room.Height))
            .FirstOrDefault(boundary => maze.IsWalkable(boundary + Direction.Down));
        if (bottomDoor != default) doors.Add(bottomDoor);

        BuildRoomShell(maze, room.TopLeft, room.Width, room.Height);
        foreach (var position in room.InteriorPositions()) maze.Carve(position);
        foreach (var door in doors) maze.PlaceDoor(door, DoorState.Open);
        maze.SetStartingRoom(room);
    }

    private void CarveFrom(Maze maze, Position gridPosition, bool[,] visited, int gridWidth, int gridHeight)
    {
        visited[gridPosition.X, gridPosition.Y] = true;
        CarveNode(maze, gridPosition);

        foreach (var direction in Directions.OrderBy(_ => Random.Next()))
        {
            var next = gridPosition + direction;
            if (!IsInsideGrid(next, gridWidth, gridHeight) || visited[next.X, next.Y]) continue;

            CarveConnection(maze, gridPosition, direction, RollConnectionWidth());
            CarveFrom(maze, next, visited, gridWidth, gridHeight);
        }
    }

    protected virtual int RollConnectionWidth() =>
        Random.NextDouble() < Settings.DoubleWidthCorridorChance ? 2 : 1;

    private void CarveNode(Maze maze, Position gridPosition)
    {
        var topLeft = ToMazePosition(gridPosition);
        for (var y = topLeft.Y; y < topLeft.Y + CorridorNodeWidth; y++)
        for (var x = topLeft.X; x < topLeft.X + CorridorNodeWidth; x++)
            CarveOutsideRoomFootprints(maze, new Position(x, y));
    }

    private void CarveConnection(Maze maze, Position gridPosition, Direction direction, int width)
    {
        var topLeft = ToMazePosition(gridPosition);
        var connectionLength = GridStep - CorridorNodeWidth;
        if (direction is Direction.Left or Direction.Right)
        {
            var wallX = direction == Direction.Right ? topLeft.X + CorridorNodeWidth : topLeft.X - 1;
            CarveHorizontalConnection(maze, wallX, topLeft.Y, direction == Direction.Right ? 1 : -1, connectionLength, width);
            return;
        }

        var wallY = direction == Direction.Down ? topLeft.Y + CorridorNodeWidth : topLeft.Y - 1;
        CarveVerticalConnection(maze, topLeft.X, wallY, direction == Direction.Down ? 1 : -1, connectionLength, width);
    }

    private static void CarveHorizontalConnection(Maze maze, int startX, int y, int step, int length, int width)
    {
        for (var offset = 0; offset < length; offset++)
        {
            for (var lane = 0; lane < width; lane++)
                CarveOutsideRoomFootprints(maze, new Position(startX + offset * step, y + lane));
        }
    }

    private static void CarveVerticalConnection(Maze maze, int x, int startY, int step, int length, int width)
    {
        for (var offset = 0; offset < length; offset++)
        {
            for (var lane = 0; lane < width; lane++)
                CarveOutsideRoomFootprints(maze, new Position(x + lane, startY + offset * step));
        }
    }

    private void PlaceRooms(Maze maze, int gridWidth, int gridHeight)
    {
        var placedRooms = 0;
        var attempts = Settings.RoomCount * 400;
        for (var attempt = 0; attempt < attempts && placedRooms < Settings.RoomCount; attempt++)
        {
            var width = Random.Next(Settings.MinimumRoomSize, Settings.MaximumRoomSize + 1);
            var height = Random.Next(Settings.MinimumRoomSize, Settings.MaximumRoomSize + 1);
            var topLeft = new Position(Random.Next(2, maze.Width - width), Random.Next(2, maze.Height - height));
            if (TryReserveRoom(maze, topLeft, width, height, gridWidth, gridHeight)) placedRooms++;
        }
    }

    private bool TryReserveRoom(Maze maze, Position topLeft, int width, int height, int gridWidth, int gridHeight)
    {
        if (OverlapsStartingRoom(maze, topLeft, width, height) ||
            CoversPosition(topLeft, width, height, maze.Entrance) ||
            CoversPosition(topLeft, width, height, ToMazePosition(new Position(gridWidth - 1, gridHeight - 1)))) return false;
        if (maze.Rooms.Any(room => FootprintsOverlap(room.TopLeft, room.Width, room.Height, topLeft, width, height))) return false;

        BuildRoomShell(maze, topLeft, width, height);
        for (var y = topLeft.Y; y < topLeft.Y + height; y++)
        for (var x = topLeft.X; x < topLeft.X + width; x++) maze.Carve(new Position(x, y));
        maze.AddRoom(new Room(topLeft, width, height));
        return true;
    }

    private void PlaceMapObjects(Maze maze)
    {
        PlaceObjects(maze, Settings.TreasureChestCount, GetRoomPositions(maze).Where(position =>
            maze.Rooms.FirstOrDefault(room => room.Contains(position))?.AllowsRandomContent == true),
            position => new TreasureChest(position, Settings.TreasureGoldRange.Roll(Random)), maze.AddTreasureChest);
        PlaceRoomEncounters(maze);
        PlaceCorridorEncounters(maze);
    }

    private void PlaceRoomEncounters(Maze maze)
    {
        var rooms = maze.Rooms.Where(room => room.AllowsRandomContent).OrderBy(_ => Random.Next()).ToList();
        var encounters = ExpandEncounters(_roomEncounters)
            .OrderByDescending(encounter => encounter.Members.Any(member => member.Role == EnemyGroupRole.Leader))
            .ThenBy(_ => Random.Next()).ToList();
        foreach (var encounter in encounters)
        {
            var members = RollMembers(encounter);
            var roomIndex = rooms.FindIndex(room => AvailableRoomPositions(maze, room).Count >= members.Count);
            if (roomIndex < 0) continue;
            var room = rooms[roomIndex];
            rooms.RemoveAt(roomIndex);
            var center = new Position(room.TopLeft.X + room.Width / 2, room.TopLeft.Y + room.Height / 2);
            var positions = AvailableRoomPositions(maze, room)
                .OrderBy(position => Manhattan(position, center)).ThenBy(_ => Random.Next())
                .Take(members.Count).ToList();
            PlaceGroup(maze, encounter, members, positions,
                roamingHorde: encounter.Behavior == EnemyEncounterBehavior.Horde);
        }
    }

    private void PlaceCorridorEncounters(Maze maze)
    {
        foreach (var encounter in ExpandEncounters(_corridorEncounters).OrderBy(_ => Random.Next()))
        {
            var members = RollMembers(encounter);
            var available = GetOutdoorPositions(maze).Where(position => maze.GetObjectAt(position) is null &&
                position != maze.Entrance && position != maze.Exit).ToHashSet();
            if (available.Count < members.Count) return;
            var anchor = available.ElementAt(Random.Next(available.Count));
            var positions = ConnectedPositions(anchor, available, members.Count);
            if (positions.Count < members.Count) continue;
            PlaceGroup(maze, encounter, members, positions,
                roamingHorde: encounter.Behavior == EnemyEncounterBehavior.Horde);
        }
    }

    private IEnumerable<ResolvedEnemyEncounter> ExpandEncounters(IEnumerable<ResolvedEnemyEncounter> encounters) =>
        encounters.SelectMany(encounter => Enumerable.Repeat(encounter, encounter.GroupCount.Roll(Random)));

    private List<(EnemyDefinition Definition, EnemyGroupRole Role)> RollMembers(ResolvedEnemyEncounter encounter) =>
        encounter.Members
            .OrderBy(member => member.Role == EnemyGroupRole.Leader ? 0 : 1)
            .SelectMany(member => Enumerable.Repeat((member.Definition, member.Role), member.Count.Roll(Random)))
            .ToList();

    private List<Position> AvailableRoomPositions(Maze maze, Room room) => room.InteriorPositions()
        .Where(position => maze.IsWalkable(position) && maze.GetObjectAt(position) is null &&
                           !maze.Doors.Any(door => Manhattan(door.Position, position) == 1))
        .ToList();

    private static List<Position> ConnectedPositions(Position anchor, IReadOnlySet<Position> available, int count)
    {
        var result = new List<Position>();
        var visited = new HashSet<Position> { anchor };
        var queue = new Queue<Position>();
        queue.Enqueue(anchor);
        while (queue.Count > 0 && result.Count < count)
        {
            var current = queue.Dequeue();
            result.Add(current);
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (available.Contains(next) && visited.Add(next)) queue.Enqueue(next);
            }
        }
        return result;
    }

    private void PlaceGroup(Maze maze, ResolvedEnemyEncounter encounter,
        IReadOnlyList<(EnemyDefinition Definition, EnemyGroupRole Role)> members,
        IReadOnlyList<Position> positions, bool roamingHorde)
    {
        var groupId = (roamingHorde ? Enemy.HordeGroupPrefix : string.Empty) + Guid.NewGuid().ToString("N");
        var leaderIndex = roamingHorde
            ? Math.Max(0, members.ToList().FindIndex(member => member.Role == EnemyGroupRole.Leader))
            : -1;
        var placed = new List<ConfiguredEnemy>();
        for (var index = 0; index < members.Count; index++)
        {
            var member = members[index];
            var enemy = CreateEnemy(maze, positions[index], member.Definition, encounter.MovementProfile);
            var role = roamingHorde
                ? index == leaderIndex ? EnemyGroupRole.Leader : EnemyGroupRole.Member
                : member.Role;
            enemy.ConfigureGroup(groupId, role);
            maze.AddEnemy(enemy);
            placed.Add(enemy);
        }
        ConfigureGroupAlertness(maze, placed);
    }

    private void ConfigureGroupAlertness(Maze maze, IReadOnlyList<ConfiguredEnemy> group)
    {
        var isRoomGroup = group.Any(enemy => maze.Rooms.Any(room => room.Contains(enemy.Position)));
        if (!isRoomGroup || group.All(enemy => !enemy.CanSleep))
        {
            foreach (var enemy in group) enemy.ConfigureAwareness(EnemyAlertness.Alert);
            return;
        }

        var roll = Random.Next(100);
        if (roll >= 55)
        {
            foreach (var enemy in group) enemy.ConfigureAwareness(EnemyAlertness.Alert);
            return;
        }
        if (roll >= 25)
        {
            foreach (var enemy in group)
                enemy.ConfigureAwareness(enemy.CanSleep ? EnemyAlertness.Drowsy : EnemyAlertness.Alert);
            return;
        }

        var sentry = group.FirstOrDefault(enemy => enemy.GroupRole == EnemyGroupRole.Leader && enemy.CanSleep) ??
                     group.FirstOrDefault(enemy => enemy.CanSleep);
        foreach (var enemy in group)
            enemy.ConfigureAwareness(!enemy.CanSleep ? EnemyAlertness.Alert : enemy == sentry
                ? EnemyAlertness.Drowsy : EnemyAlertness.Sleeping);
    }

    private ConfiguredEnemy CreateEnemy(Maze maze, Position position, EnemyDefinition definition,
        EnemyMovementProfile? configuredProfile = null)
    {
        var isInRoom = maze.Rooms.Any(room => room.Contains(position));
        var stationaryChance = isInRoom ? 80 : 10;
        var roll = Random.Next(100);
        var profile = configuredProfile ?? (roll < stationaryChance
            ? EnemyMovementProfile.Stationary
            : (roll - stationaryChance) % 2 == 0 ? EnemyMovementProfile.Wander : EnemyMovementProfile.Patrol);
        var enemy = new ConfiguredEnemy(position, definition, Random);
        enemy.ConfigureMovement(profile, Directions[Random.Next(Directions.Length)]);
        return enemy;
    }

    private static int Manhattan(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    private void ConnectRoomsToMaze(Maze maze)
    {
        foreach (var room in maze.Rooms.Where(room => room != maze.StartingRoom))
        {
            if (!TryConnectRoom(maze, room, allowTunnelingThroughOtherRoomWalls: false))
                TryConnectRoom(maze, room, allowTunnelingThroughOtherRoomWalls: true);
        }
    }

    /// <summary>Egy szoba bekötése a hálózatba; ha a szigorú keresés nem talál utat (pl. túl zsúfolt
    /// szobaelrendezés), a mentőkör áttörhet más szobák puszta falpuffer-övezetén (nem a belsejükön).</summary>
    private bool TryConnectRoom(Maze maze, Room room, bool allowTunnelingThroughOtherRoomWalls)
    {
        var bestConnection = GetRoomDoorCandidates(room)
            .Select(candidate => (Candidate: candidate,
                Path: FindPathToCorridor(maze, candidate.Outside, room, allowTunnelingThroughOtherRoomWalls)))
            .Where(entry => entry.Path is not null)
            .OrderBy(entry => entry.Path!.Count)
            .FirstOrDefault();
        if (bestConnection.Path is null) return false;
        foreach (var position in bestConnection.Path) maze.Carve(position);
        maze.PlaceDoor(bestConnection.Candidate.Door, RollDoorState());

        var primarySide = GetRoomDoorSide(room, bestConnection.Candidate.Door);
        var oppositeConnection = GetRoomDoorCandidates(room)
            .Where(candidate => GetRoomDoorSide(room, candidate.Door) == Opposite(primarySide))
            .Where(candidate => maze.IsWalkable(candidate.Outside))
            .OrderBy(_ => Random.Next())
            .FirstOrDefault();
        if (oppositeConnection.Door != default)
            maze.PlaceDoor(oppositeConnection.Door, RollDoorState());
        return true;
    }

    private static Direction GetRoomDoorSide(Room room, Position door) => door.Y == room.TopLeft.Y - 1
        ? Direction.Up
        : door.Y == room.TopLeft.Y + room.Height
            ? Direction.Down
            : door.X == room.TopLeft.X - 1
                ? Direction.Left
                : Direction.Right;

    private static Direction Opposite(Direction direction) => direction switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        _ => Direction.Left
    };

    private static IReadOnlyList<Position>? FindPathToCorridor(Maze maze, Position start, Room room,
        bool allowTunnelingThroughOtherRoomWalls)
    {
        if (!maze.IsInside(start) || IsBlockedFootprint(maze, start, room, allowTunnelingThroughOtherRoomWalls)) return null;
        var previous = new Dictionary<Position, Position> { [start] = start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (maze.IsWalkable(current) && !maze.Rooms.Any(candidate => candidate.Contains(current)))
            {
                var path = new List<Position>();
                for (var position = current; position != start; position = previous[position]) path.Add(position);
                path.Add(start);
                path.Reverse();
                return path;
            }
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!maze.IsInside(next) || IsBlockedFootprint(maze, next, room, allowTunnelingThroughOtherRoomWalls) ||
                    !previous.TryAdd(next, current)) continue;
                queue.Enqueue(next);
            }
        }
        return null;
    }

    /// <summary>A saját szoba teljes lenyomata mindig tilos; más szobáké csak a szigorú módban (a puffer-övezetükkel együtt).</summary>
    private static bool IsBlockedFootprint(Maze maze, Position position, Room excludedRoom, bool allowTunnelingThroughOtherRoomWalls) =>
        maze.Rooms.Any(room => room == excludedRoom
            ? CoversPosition(room.TopLeft, room.Width, room.Height, position)
            : allowTunnelingThroughOtherRoomWalls ? room.Contains(position) : CoversPosition(room.TopLeft, room.Width, room.Height, position));

    private static IEnumerable<(Position Door, Position Outside)> GetRoomDoorCandidates(Room room)
    {
        for (var x = room.TopLeft.X; x < room.TopLeft.X + room.Width; x++)
        {
            yield return (new Position(x, room.TopLeft.Y - 1), new Position(x, room.TopLeft.Y - 2));
            yield return (new Position(x, room.TopLeft.Y + room.Height), new Position(x, room.TopLeft.Y + room.Height + 1));
        }
        for (var y = room.TopLeft.Y; y < room.TopLeft.Y + room.Height; y++)
        {
            yield return (new Position(room.TopLeft.X - 1, y), new Position(room.TopLeft.X - 2, y));
            yield return (new Position(room.TopLeft.X + room.Width, y), new Position(room.TopLeft.X + room.Width + 1, y));
        }
    }

    private static bool OverlapsStartingRoom(Maze maze, Position topLeft, int width, int height)
    {
        var startingRoom = maze.StartingRoom ?? new Room(new Position(1, 1), 3, 3);
        return FootprintsOverlap(startingRoom.TopLeft, startingRoom.Width, startingRoom.Height, topLeft, width, height);
    }

    private void PlaceObjects<T>(Maze maze, int requestedCount, IEnumerable<Position> candidates, Func<Position, T> factory, Action<T> add)
        where T : WorldObject
    {
        var available = candidates.Where(position => maze.GetObjectAt(position) is null && position != maze.Entrance && position != maze.Exit).ToList();
        for (var count = 0; count < requestedCount && available.Count > 0; count++)
        {
            var index = Random.Next(available.Count);
            var position = available[index];
            available.RemoveAt(index);
            add(factory(position));
        }
    }

    private static IEnumerable<Position> GetRoomPositions(Maze maze) =>
        maze.Rooms.SelectMany(room => room.InteriorPositions()).Where(maze.IsWalkable);

    private static IEnumerable<Position> GetOutdoorPositions(Maze maze)
    {
        for (var y = 1; y < maze.Height - 1; y++)
        for (var x = 1; x < maze.Width - 1; x++)
        {
            var position = new Position(x, y);
            if (maze.IsWalkable(position) && maze.GetDoorAt(position) is null && !maze.Rooms.Any(room => room.Contains(position)))
                yield return position;
        }
    }

    private static void CarveOutsideRoomFootprints(Maze maze, Position position)
    {
        if (!IsInsideAnyRoomFootprint(maze, position)) maze.Carve(position);
    }

    private static bool IsInsideAnyRoomFootprint(Maze maze, Position position) =>
        maze.Rooms.Any(room => CoversPosition(room.TopLeft, room.Width, room.Height, position));

    private static bool CoversPosition(Position topLeft, int width, int height, Position position) =>
        position.X >= topLeft.X - 1 && position.X <= topLeft.X + width &&
        position.Y >= topLeft.Y - 1 && position.Y <= topLeft.Y + height;

    private static bool FootprintsOverlap(Position firstTopLeft, int firstWidth, int firstHeight,
        Position secondTopLeft, int secondWidth, int secondHeight) =>
        firstTopLeft.X - 1 <= secondTopLeft.X + secondWidth && firstTopLeft.X + firstWidth >= secondTopLeft.X - 1 &&
        firstTopLeft.Y - 1 <= secondTopLeft.Y + secondHeight && firstTopLeft.Y + firstHeight >= secondTopLeft.Y - 1;

    private DoorState RollDoorState()
    {
        var roll = Random.Next(100);
        return roll < 80 ? DoorState.Locked : roll < 90 ? DoorState.Closed : DoorState.Open;
    }

    private static void BuildRoomShell(Maze maze, Position topLeft, int width, int height)
    {
        for (var y = topLeft.Y - 1; y <= topLeft.Y + height; y++)
        for (var x = topLeft.X - 1; x <= topLeft.X + width; x++)
        {
            var isInterior = x >= topLeft.X && x < topLeft.X + width && y >= topLeft.Y && y < topLeft.Y + height;
            if (!isInterior) maze.SetTile(new Position(x, y), maze.WallRune);
        }
    }

    private static IEnumerable<Position> GetPossibleDoors(Maze maze, Position topLeft, int width, int height)
    {
        for (var x = topLeft.X; x < topLeft.X + width; x++)
        {
            var topDoor = new Position(x, topLeft.Y - 1);
            if (maze.IsWalkable(topDoor + Direction.Up)) yield return topDoor;
            var bottomDoor = new Position(x, topLeft.Y + height);
            if (maze.IsWalkable(bottomDoor + Direction.Down)) yield return bottomDoor;
        }
        for (var y = topLeft.Y; y < topLeft.Y + height; y++)
        {
            var leftDoor = new Position(topLeft.X - 1, y);
            if (maze.IsWalkable(leftDoor + Direction.Left)) yield return leftDoor;
            var rightDoor = new Position(topLeft.X + width, y);
            if (maze.IsWalkable(rightDoor + Direction.Right)) yield return rightDoor;
        }
    }

    private Position ToMazePosition(Position gridPosition) => new(1 + gridPosition.X * GridStep, 1 + gridPosition.Y * GridStep);
    private static bool IsInsideGrid(Position position, int width, int height) => position.X >= 0 && position.X < width && position.Y >= 0 && position.Y < height;

    private static void ValidateSettings(MazeGenerationSettings settings)
    {
        var ids = settings.QuestRoomIds.Concat(settings.BossRoomIds).ToArray();
        if (settings.QuestDoorRequirements.Any(rule => !Enum.IsDefined(rule.Value) ||
            rule.Value == Domain.Quests.QuestId.None ||
            !settings.SpecialRoomPlacements.TryGetValue(rule.Key, out var placement) ||
            placement != SpecialRoomPlacement.SideBranch))
            throw new ArgumentException("Questzár csak egybejáratú mellékszobához rendelhető.", nameof(settings));
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
            ids.Length > settings.RoomCount || settings.SpecialRoomPlacements.Any(rule =>
                !ids.Contains(rule.Key) || !Enum.IsDefined(rule.Value)))
            throw new ArgumentException("Hibás vagy elhelyezhetetlen speciálisszoba-konfiguráció.", nameof(settings));
        if (settings.DoubleWidthCorridorChance is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(settings.DoubleWidthCorridorChance));
        if (settings.WideCorridorNarrowingChance is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(settings.WideCorridorNarrowingChance));
        if (settings.RoomCount < 0) throw new ArgumentOutOfRangeException(nameof(settings.RoomCount));
        if (settings.MinimumRoomSize < 2 || settings.MaximumRoomSize < settings.MinimumRoomSize)
            throw new ArgumentException("A szobaméreteknek legalább 2-nek és növekvő sorrendűnek kell lenniük.");
        if (settings.TreasureChestCount < 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Az objektumok száma nem lehet negatív.");
    }
}
