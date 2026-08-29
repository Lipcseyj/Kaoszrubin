using System.Text;

namespace MazeGame;

/// <summary>A pálya látható és bejárható rácsa.</summary>
public sealed class Maze
{
    public static readonly Rune Wall = new('█');
    public static readonly Rune Floor = new(' ');
    public static readonly Rune ExitMarker = new('⌂');

    /// <summary>A pálya cellái. Minden elem egyetlen, keskeny konzolcellára rajzolható rúna.</summary>
    public Rune[,] Tiles { get; }
    private readonly List<Room> _rooms = [];
    private readonly List<TreasureChest> _treasureChests = [];
    private readonly List<Enemy> _enemies = [];
    private readonly List<Corpse> _corpses = [];
    private readonly List<PartyMemberAvatar> _partyMembers = [];
    private readonly List<WorldNpc> _worldNpcs = [];
    private readonly List<GroundItemPile> _groundItemPiles = [];
    private readonly List<MazeTrap> _traps = [];
    private readonly Dictionary<Position, MazeDoor> _doors = [];

    public WorldId Id { get; } = WorldId.New();
    public IReadOnlyList<Room> Rooms => _rooms;
    public IReadOnlyList<TreasureChest> TreasureChests => _treasureChests;
    public IReadOnlyList<Enemy> Enemies => _enemies;
    public IReadOnlyList<Corpse> Corpses => _corpses;
    public IReadOnlyList<PartyMemberAvatar> PartyMembers => _partyMembers;
    public IReadOnlyList<WorldNpc> WorldNpcs => _worldNpcs;
    public IReadOnlyList<GroundItemPile> GroundItemPiles => _groundItemPiles;
    public IReadOnlyList<MazeTrap> Traps => _traps;
    public IReadOnlyCollection<MazeDoor> Doors => _doors.Values;
    public Room? StartingRoom { get; private set; }
    public int Width { get; }
    public int Height { get; }
    public Position Entrance { get; }
    public Position Exit { get; private set; }
    public Rune WallRune { get; }
    public ConsoleColor WallColor { get; }
    public string LevelName { get; }

    public Maze(int width, int height, Rune? wallRune = null, ConsoleColor wallColor = ConsoleColor.DarkGray,
        string? levelName = null)
    {
        if (width < 5 || height < 5)
            throw new ArgumentException("A labirintus méretei legalább 5-ösek legyenek.");

        Width = width;
        Height = height;
        WallRune = wallRune ?? Wall;
        WallColor = wallColor;
        LevelName = string.IsNullOrWhiteSpace(levelName) ? "Labirintus" : levelName;
        Tiles = new Rune[width, height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            Tiles[x, y] = WallRune;

        // A garantált 3×3-as kezdőterem középső cellája; így a vezető nem indul fal mellett.
        Entrance = new Position(2, 2);
        Exit = new Position(LastInnerOddCoordinate(width), LastInnerOddCoordinate(height));
    }

    public bool IsInside(Position position) => position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
    public bool IsWalkable(Position position)
    {
        if (!IsInside(position)) return false;
        if (_doors.TryGetValue(position, out var door)) return door.IsWalkable;
        return Tiles[position.X, position.Y] == Floor || Tiles[position.X, position.Y] == ExitMarker;
    }

    public void Carve(Position position)
    {
        if (!IsInside(position)) throw new ArgumentOutOfRangeException(nameof(position));
        Tiles[position.X, position.Y] = Floor;
    }

    public void PlaceExit(Position position)
    {
        if (!IsWalkable(position)) throw new ArgumentException("A kijáratnak járható cellán kell lennie.", nameof(position));
        Exit = position;
        Tiles[Exit.X, Exit.Y] = ExitMarker;
    }

    public void SetTile(Position position, Rune tile)
    {
        if (!IsInside(position)) throw new ArgumentOutOfRangeException(nameof(position));
        Tiles[position.X, position.Y] = tile;
    }

    public void PlaceDoor(Position position, DoorState state)
    {
        if (!IsInside(position)) throw new ArgumentOutOfRangeException(nameof(position));
        var door = new MazeDoor(position, state);
        _doors[position] = door;
        Tiles[position.X, position.Y] = door.Symbol;
    }

    public MazeDoor? GetDoorAt(Position position) => _doors.GetValueOrDefault(position);

    public bool RemoveDoor(Position position) => _doors.Remove(position);

    public bool SetDoorState(MazeDoor door, DoorState state)
    {
        if (!_doors.TryGetValue(door.Position, out var existing) || existing != door || !door.TrySetState(state)) return false;
        Tiles[door.Position.X, door.Position.Y] = door.Symbol;
        return true;
    }

    public bool BlocksSight(Position position) => _doors.TryGetValue(position, out var door)
        ? door.BlocksSight
        : Tiles[position.X, position.Y] == WallRune;

    public void AddRoom(Room room) => _rooms.Add(room);

    public void SetStartingRoom(Room room)
    {
        if (!room.Contains(Entrance)) throw new ArgumentException("A kezdőteremnek tartalmaznia kell a bejáratot.", nameof(room));
        StartingRoom = room;
        _rooms.Insert(0, room);
    }

    public void AddTreasureChest(TreasureChest chest)
    {
        EnsureObjectPositionIsFree(chest.Position);
        _treasureChests.Add(chest);
    }

    public bool RemoveTreasureChest(TreasureChest chest) => _treasureChests.Remove(chest);

    public void AddCorpse(Corpse corpse) => _corpses.Add(corpse);
    public bool RemoveCorpse(Corpse corpse) => _corpses.Remove(corpse);

    public void AddEnemy(Enemy enemy)
    {
        EnsureObjectPositionIsFree(enemy.Position);
        _enemies.Add(enemy);
    }

    public void AddPartyMember(PartyMemberAvatar member)
    {
        EnsureObjectPositionIsFree(member.Position);
        _partyMembers.Add(member);
    }

    public bool RemovePartyMember(PartyMemberAvatar member) => _partyMembers.Remove(member);

    public void AddWorldNpc(WorldNpc npc)
    {
        EnsureObjectPositionIsFree(npc.Position);
        _worldNpcs.Add(npc);
    }

    public bool RemoveWorldNpc(WorldNpc npc) => _worldNpcs.Remove(npc);

    public void ReplaceEnemyWithCorpse(Enemy enemy)
    {
        if (!_enemies.Remove(enemy)) throw new ArgumentException("Az ellenfél nem a labirintus része.", nameof(enemy));
        _corpses.Add(new MonsterCorpse(enemy.Position, enemy.Name, enemy.Definition.Id));
    }

    public void ReplacePartyMemberWithCorpse(PartyMemberAvatar member)
    {
        if (!_partyMembers.Remove(member)) throw new ArgumentException("A partitárs nem a labirintus része.", nameof(member));
        _corpses.Add(new PartyMemberCorpse(member.Position, member.Character));
    }

    public WorldObject? GetObjectAt(Position position) =>
        _treasureChests.FirstOrDefault(chest => chest.Position == position) as WorldObject ??
        _enemies.FirstOrDefault(enemy => enemy.Position == position) as WorldObject ??
        _partyMembers.FirstOrDefault(member => member.Position == position) as WorldObject ??
        _worldNpcs.FirstOrDefault(npc => npc.Position == position) as WorldObject ??
        _corpses.FirstOrDefault(corpse => corpse.Position == position) as WorldObject ??
        _groundItemPiles.FirstOrDefault(pile => pile.Position == position) as WorldObject;

    public GroundItemPile? GetGroundItemPileAt(Position position) =>
        _groundItemPiles.FirstOrDefault(pile => pile.Position == position);
    public Corpse? GetCorpseAt(Position position) => _corpses.FirstOrDefault(corpse => corpse.Position == position);
    public bool RemoveGroundItemPile(GroundItemPile pile) => _groundItemPiles.Remove(pile);

    public void DropItem(Position position, Domain.Inventory.IItemDefinition item, int? charges = null)
    {
        if (!IsWalkable(position)) throw new ArgumentException("Tárgyat csak járható mezőre lehet dobni.", nameof(position));
        var pile = GetGroundItemPileAt(position);
        if (pile is null) _groundItemPiles.Add(new GroundItemPile(position, item, charges));
        else pile.Add(item, charges);
    }

    public Enemy? GetEnemyAt(Position position) => _enemies.FirstOrDefault(enemy => enemy.Position == position);
    public PartyMemberAvatar? GetPartyMemberAt(Position position) => _partyMembers.FirstOrDefault(member => member.Position == position);
    public WorldNpc? GetWorldNpcAt(Position position) => _worldNpcs.FirstOrDefault(npc => npc.Position == position);
    public TreasureChest? GetTreasureChestAt(Position position) => _treasureChests.FirstOrDefault(chest => chest.Position == position);
    public MazeTrap? GetTrapAt(Position position) => _traps.FirstOrDefault(trap => trap.Position == position);

    public void AddTrap(MazeTrap trap)
    {
        if (!IsWalkable(trap.Position) || trap.Position == Entrance || trap.Position == Exit ||
            GetObjectAt(trap.Position) is not null || GetTrapAt(trap.Position) is not null)
            throw new ArgumentException("Csapda csak üres, járható mezőre helyezhető.", nameof(trap));
        _traps.Add(trap);
    }

    public bool TryMoveEnemy(Enemy enemy, Position destination)
    {
        if (!IsWalkable(destination) || destination == Entrance || destination == Exit) return false;
        var occupant = GetObjectAt(destination);
        if (occupant is not null && occupant != enemy && occupant is not (GroundItemPile or Corpse)) return false;

        enemy.MoveTo(destination);
        return true;
    }

    public bool TryMovePartyMember(PartyMemberAvatar member, Position destination, Position leaderPosition,
        bool allowTreasureChest = false)
    {
        if (!IsWalkable(destination) || destination == leaderPosition) return false;
        var occupant = GetObjectAt(destination);
        if (occupant is not null && occupant != member && occupant is not (GroundItemPile or Corpse) &&
            !(allowTreasureChest && occupant is TreasureChest)) return false;
        member.MoveTo(destination);
        return true;
    }

    private void EnsureObjectPositionIsFree(Position position)
    {
        if (!IsWalkable(position) || position == Entrance || position == Exit || GetObjectAt(position) is not null)
            throw new ArgumentException("Az objektum helyének üres, járható mezőnek kell lennie.", nameof(position));
    }

    private static int LastInnerOddCoordinate(int length)
    {
        var coordinate = length - 2;
        return coordinate % 2 == 0 ? coordinate - 1 : coordinate;
    }

    /// <summary>Bejárja a teljes labirintust a bejárattól (ajtókon is áthaladva, állapotuktól függetlenül),
    /// és összeveti az elért mezőket az összes padló- és ajtócellával, hogy minden terület elérhető-e.</summary>
    public MazeAccessibilityReport CheckFullAccessibility()
    {
        var allFloorCells = GetAllFloorOrDoorPositions();
        var reachable = ComputeReachableFromEntrance();
        var unreachable = allFloorCells.Where(position => !reachable.Contains(position)).ToList();
        return new MazeAccessibilityReport(unreachable.Count == 0, reachable.Count, allFloorCells.Count, unreachable);
    }

    /// <summary>Amíg az önellenőrzés leválasztott területet talál, a legkevesebb faláttöréssel köti azt
    /// vissza a bejárattól elérhető hálózathoz. Végeredményként garantáltan teljesen bejárható labirintust ad.</summary>
    public MazeAccessibilityReport EnsureFullAccessibility(Func<DoorState> rollDoorState, int maxRepairAttempts = 500)
    {
        for (var attempt = 0; attempt < maxRepairAttempts; attempt++)
        {
            var report = CheckFullAccessibility();
            if (report.IsFullyAccessible) return report;
            CarveConnectionToReachableNetwork(report.UnreachablePositions[0], rollDoorState);
        }
        return CheckFullAccessibility();
    }

    /// <summary>0-1 BFS: a már járható cellák felé ingyen, a falak felé egy-egy áttörés árán lép,
    /// így a leválasztott cellától a legkevesebb új nyílással érhető el az elérhető hálózat.</summary>
    private void CarveConnectionToReachableNetwork(Position isolated, Func<DoorState> rollDoorState)
    {
        var reachable = ComputeReachableFromEntrance();
        var distances = new Dictionary<Position, int> { [isolated] = 0 };
        var previous = new Dictionary<Position, Position>();
        var deque = new LinkedList<Position>();
        deque.AddLast(isolated);
        Position? target = null;

        while (deque.Count > 0)
        {
            var current = deque.First!.Value;
            deque.RemoveFirst();
            if (reachable.Contains(current) && current != isolated) { target = current; break; }
            foreach (var direction in Enum.GetValues<Direction>())
            {
                var next = current + direction;
                if (!IsInside(next)) continue;
                var weight = IsFloorOrDoor(next) ? 0 : 1;
                var newDistance = distances[current] + weight;
                if (distances.TryGetValue(next, out var known) && known <= newDistance) continue;
                distances[next] = newDistance;
                previous[next] = current;
                if (weight == 0) deque.AddFirst(next); else deque.AddLast(next);
            }
        }

        if (target is null)
            throw new InvalidOperationException("Nem található kapcsolat a leválasztott labirintusterülethez.");
        for (var position = target.Value; position != isolated; position = previous[position])
        {
            if (IsFloorOrDoor(position)) continue;
            if (IsAdjacentToRoomInterior(position)) PlaceDoor(position, rollDoorState());
            else Carve(position);
        }
    }

    private bool IsAdjacentToRoomInterior(Position position) => Enum.GetValues<Direction>()
        .Select(direction => position + direction).Any(neighbor => _rooms.Any(room => room.Contains(neighbor)));

    private HashSet<Position> ComputeReachableFromEntrance()
    {
        var reachable = new HashSet<Position> { Entrance };
        var queue = new Queue<Position>();
        queue.Enqueue(Entrance);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Enum.GetValues<Direction>())
            {
                var next = current + direction;
                if (!IsFloorOrDoor(next) || !reachable.Add(next)) continue;
                queue.Enqueue(next);
            }
        }
        return reachable;
    }

    private List<Position> GetAllFloorOrDoorPositions()
    {
        var cells = new List<Position>();
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var position = new Position(x, y);
            if (IsFloorOrDoor(position)) cells.Add(position);
        }
        return cells;
    }

    private bool IsFloorOrDoor(Position position) => IsInside(position) &&
        (_doors.ContainsKey(position) || Tiles[position.X, position.Y] == Floor || Tiles[position.X, position.Y] == ExitMarker);
}

/// <summary>A teljes bejárhatósági önellenőrzés eredménye.</summary>
public sealed record MazeAccessibilityReport(bool IsFullyAccessible, int ReachableCount, int TotalWalkableCount,
    IReadOnlyList<Position> UnreachablePositions);
