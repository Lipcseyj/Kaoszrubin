using System.Text;

namespace KaoszRubin.World;

/// <summary>Pályán belüli, egyszer használható fogadói megálló.</summary>
public sealed record ForestInnConfiguration(string RoomId, string Name, string AreaId);

public sealed class ForestInn : WorldObject
{
    private int _visited;
    public string RoomId { get; }
    public string Name { get; }
    public bool Visited => Volatile.Read(ref _visited) != 0;
    public override Rune Symbol => new('♨');
    public ConsoleColor Color => Visited ? ConsoleColor.DarkGray : ConsoleColor.Yellow;

    public ForestInn(Position position, string roomId, string name, bool visited = false) : base(position)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A fogadónak szobaazonosító és név kell.");
        RoomId = roomId;
        Name = name;
        _visited = visited ? 1 : 0;
    }

    public bool TryVisit() => Interlocked.CompareExchange(ref _visited, 1, 0) == 0;
}

public static class ForestInnPlacement
{
    public static void Place(DungeonLevel level, IReadOnlyList<ForestInnConfiguration> configurations)
    {
        foreach (var configuration in configurations)
        {
            var area = level.GetArea(configuration.AreaId);
            var room = area.Maze.GetRoomByContentId(configuration.RoomId) ??
                throw new InvalidOperationException($"Hiányzik a fogadószoba: {configuration.RoomId}.");
            if (room.Purpose != RoomPurpose.Inn || room.BuildingId is null)
                throw new InvalidOperationException("Erdei fogadó csak elkülönített épületbelső lehet.");
            var center = new Position(room.TopLeft.X + room.Width / 2, room.TopLeft.Y + room.Height / 2);
            var position = room.InteriorPositions().Where(position => area.Maze.IsWalkable(position) &&
                    area.Maze.GetObjectAt(position) is null && area.Maze.GetTrapAt(position) is null &&
                    area.Maze.GetPassageAt(position) is null && position != area.Maze.Exit)
                .OrderBy(position => Math.Abs(position.X - center.X) + Math.Abs(position.Y - center.Y))
                .Cast<Position?>().FirstOrDefault() ??
                throw new InvalidOperationException("Nincs szabad mező az erdei fogadóban.");
            area.Maze.AddForestInn(new(position, configuration.RoomId, configuration.Name));
            var buildingRooms = area.Maze.Rooms.Where(interior => interior.BuildingId == room.BuildingId).ToArray();
            foreach (var door in area.Maze.Doors.Where(door => !door.IsQuestSealed && buildingRooms.Any(interior =>
                door.Position.X >= interior.TopLeft.X - 1 && door.Position.X <= interior.TopLeft.X + interior.Width &&
                door.Position.Y >= interior.TopLeft.Y - 1 && door.Position.Y <= interior.TopLeft.Y + interior.Height)).ToArray())
                area.Maze.PlaceDoor(door.Position, DoorState.Open);
        }
    }

    public static bool CanEnter(ForestInn inn, Position leaderPosition, IEnumerable<PartyMemberAvatar> members,
        bool inBattle, out string? reason)
    {
        reason = inBattle ? "Harc közben nem térhettek be a fogadóba." :
            inn.Visited ? "Ezt a fogadói megállót már felhasználtátok." :
            leaderPosition != inn.Position ? "A fogadó jelére kell állnotok." :
            PartyGatheringRules.FirstDistantLivingMember(members, leaderPosition)
                is { } distant ? $"{distant.Character.Name} túl messze van. Előbb gyűljön össze a parti." : null;
        return reason is null;
    }
}
