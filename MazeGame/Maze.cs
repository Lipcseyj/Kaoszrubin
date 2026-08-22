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
    private readonly List<GroundItemPile> _groundItemPiles = [];
    private readonly Dictionary<Position, MazeDoor> _doors = [];

    public IReadOnlyList<Room> Rooms => _rooms;
    public IReadOnlyList<TreasureChest> TreasureChests => _treasureChests;
    public IReadOnlyList<Enemy> Enemies => _enemies;
    public IReadOnlyList<Corpse> Corpses => _corpses;
    public IReadOnlyList<PartyMemberAvatar> PartyMembers => _partyMembers;
    public IReadOnlyList<GroundItemPile> GroundItemPiles => _groundItemPiles;
    public IReadOnlyCollection<MazeDoor> Doors => _doors.Values;
    public Room? StartingRoom { get; private set; }
    public int Width { get; }
    public int Height { get; }
    public Position Entrance { get; }
    public Position Exit { get; private set; }

    public Maze(int width, int height)
    {
        if (width < 5 || height < 5)
            throw new ArgumentException("A labirintus méretei legalább 5-ösek legyenek.");

        Width = width;
        Height = height;
        Tiles = new Rune[width, height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            Tiles[x, y] = Wall;

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
        : Tiles[position.X, position.Y] == Wall;

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

    public void ReplaceEnemyWithCorpse(Enemy enemy)
    {
        if (!_enemies.Remove(enemy)) throw new ArgumentException("Az ellenfél nem a labirintus része.", nameof(enemy));
        _corpses.Add(new Corpse(enemy.Position, enemy.Name));
    }

    public void ReplacePartyMemberWithCorpse(PartyMemberAvatar member)
    {
        if (!_partyMembers.Remove(member)) throw new ArgumentException("A partitárs nem a labirintus része.", nameof(member));
        _corpses.Add(new Corpse(member.Position, member.Character.Name));
    }

    public WorldObject? GetObjectAt(Position position) =>
        _treasureChests.FirstOrDefault(chest => chest.Position == position) as WorldObject ??
        _enemies.FirstOrDefault(enemy => enemy.Position == position) as WorldObject ??
        _partyMembers.FirstOrDefault(member => member.Position == position) as WorldObject ??
        _corpses.FirstOrDefault(corpse => corpse.Position == position) as WorldObject ??
        _groundItemPiles.FirstOrDefault(pile => pile.Position == position) as WorldObject;

    public GroundItemPile? GetGroundItemPileAt(Position position) =>
        _groundItemPiles.FirstOrDefault(pile => pile.Position == position);

    public void DropItem(Position position, Domain.Inventory.IItemDefinition item)
    {
        if (!IsWalkable(position)) throw new ArgumentException("Tárgyat csak járható mezőre lehet dobni.", nameof(position));
        var pile = GetGroundItemPileAt(position);
        if (pile is null) _groundItemPiles.Add(new GroundItemPile(position, item));
        else pile.Add(item);
    }

    public Enemy? GetEnemyAt(Position position) => _enemies.FirstOrDefault(enemy => enemy.Position == position);
    public PartyMemberAvatar? GetPartyMemberAt(Position position) => _partyMembers.FirstOrDefault(member => member.Position == position);
    public TreasureChest? GetTreasureChestAt(Position position) => _treasureChests.FirstOrDefault(chest => chest.Position == position);

    public bool TryMoveEnemy(Enemy enemy, Position destination)
    {
        if (!IsWalkable(destination) || destination == Entrance || destination == Exit) return false;
        var occupant = GetObjectAt(destination);
        if (occupant is not null && occupant != enemy && occupant is not GroundItemPile) return false;

        enemy.MoveTo(destination);
        return true;
    }

    public bool TryMovePartyMember(PartyMemberAvatar member, Position destination, Position leaderPosition)
    {
        if (!IsWalkable(destination) || destination == leaderPosition) return false;
        var occupant = GetObjectAt(destination);
        if (occupant is not null && occupant != member && occupant is not GroundItemPile) return false;
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
}
