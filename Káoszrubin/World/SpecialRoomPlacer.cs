namespace KaoszRubin.World;

/// <summary>Objektumok nélküli pályán választ speciális szobákat, a tényleges járathálózat alapján.</summary>
internal static class SpecialRoomPlacer
{
    private static readonly Direction[] Directions = Enum.GetValues<Direction>();

    public static bool TryAssign(Maze maze, MazeGenerationSettings settings, Random random,
        bool questRoomsRequireBuilding = false)
    {
        var requests = settings.QuestRoomIds.Select(id => (Id: id, Purpose: RoomPurpose.Quest))
            .Concat(settings.BossRoomIds.Select(id => (Id: id, Purpose: RoomPurpose.Boss)))
            .Concat(settings.InnRoomIds.Select(id => (Id: id, Purpose: RoomPurpose.Inn))).ToArray();
        var available = maze.Rooms.Where(room => room.AllowsRandomContent).ToList();
        if (available.Count < requests.Length) return false;
        bool Eligible(Room room, RoomPurpose purpose, string? id = null) =>
            (!questRoomsRequireBuilding || (purpose != RoomPurpose.Quest && purpose != RoomPurpose.Inn) || room.BuildingId is not null) &&
            (purpose != RoomPurpose.Inn || room.BuildingId is not null &&
                !maze.Rooms.Any(other => other.BuildingId == room.BuildingId && !other.AllowsRandomContent)) &&
            (id is null || room.InteriorPositions().Count(position => maze.IsWalkable(position) &&
                maze.Doors.All(door => Math.Abs(door.Position.X - position.X) + Math.Abs(door.Position.Y - position.Y) > 1))
                >= settings.SpecialRoomMinimumFreeCells.GetValueOrDefault(id));
        if (available.Count(room => Eligible(room, RoomPurpose.Quest)) < settings.QuestRoomIds.Count + settings.InnRoomIds.Count) return false;

        void Assign(Room room, RoomPurpose purpose, string id)
        {
            if (purpose == RoomPurpose.Inn)
            {
                foreach (var interior in maze.Rooms.Where(other => other.BuildingId == room.BuildingId).ToArray())
                    maze.AssignRoomPurpose(interior, purpose, interior == room ? id : null);
            }
            else maze.AssignRoomPurpose(room, purpose, id);
        }

        // Először lezárható mellékágakat alakítunk ki, utána mérjük a végleges főút távolságait.
        foreach (var request in requests.Where(request =>
            settings.SpecialRoomPlacements.GetValueOrDefault(request.Id) == SpecialRoomPlacement.SideBranch))
        {
            Room? selected = null;
            foreach (var room in available.Where(room => Eligible(room, request.Purpose, request.Id)).OrderBy(_ => random.Next()))
            {
                var boundary = Boundary(room).ToHashSet();
                var blocked = room.InteriorPositions().Concat(boundary).ToHashSet();
                var reachable = Distances(maze, maze.Entrance, blocked);
                if (!reachable.ContainsKey(maze.Exit) || FloorPositions(maze)
                    .Any(position => !blocked.Contains(position) && !reachable.ContainsKey(position))) continue;
                var entrance = boundary.Where(position => Passable(maze, position) &&
                        Directions.Any(direction => room.Contains(position + direction)) &&
                        Directions.Any(direction => reachable.ContainsKey(position + direction)))
                    .OrderBy(position => position.Y).ThenBy(position => position.X).Cast<Position?>().FirstOrDefault();
                if (entrance is null) continue;
                foreach (var position in boundary.Where(position => position != entrance.Value))
                {
                    maze.RemoveDoor(position);
                    maze.SetTile(position, maze.WallRune);
                }
                var state = maze.GetDoorAt(entrance.Value)?.State ?? DoorState.Closed;
                if (settings.QuestDoorRequirements.TryGetValue(request.Id, out var questId))
                    maze.PlaceDoor(entrance.Value, DoorState.Closed, new Domain.Quests.QuestKey(questId));
                else
                    maze.PlaceDoor(entrance.Value, state);
                selected = room;
                break;
            }
            if (selected is null) return false;
            Assign(selected, request.Purpose, request.Id);
            available.RemoveAll(room => room == selected || request.Purpose == RoomPurpose.Inn && room.BuildingId == selected.BuildingId);
        }

        var fromStart = Distances(maze, maze.Entrance);
        var fromExit = Distances(maze, maze.Exit);
        if (!fromStart.TryGetValue(maze.Exit, out var length)) return false;
        foreach (var request in requests.Where(request => settings.SpecialRoomPlacements.TryGetValue(request.Id, out var rule) &&
            rule == SpecialRoomPlacement.MiddleRoute))
        {
            var candidate = available.Where(room => Eligible(room, request.Purpose, request.Id)).Select(room =>
            {
                var center = new Position(room.TopLeft.X + room.Width / 2, room.TopLeft.Y + room.Height / 2);
                var start = fromStart[center];
                var end = fromExit[center];
                // Közös oda-vissza kitérő nélkül mért hely a főút mentén; a kitérőt külön korlátozzuk.
                var progress = (start - end + length) / 2.0;
                var detour = (start + end - length) / 2.0;
                return (Room: room, Progress: progress, Detour: detour);
            }).Where(entry => entry.Progress >= length / 3.0 && entry.Progress <= length * 2 / 3.0 &&
                entry.Detour <= Math.Max(8, length * 0.15))
                .OrderBy(entry => Math.Abs(entry.Progress - length / 2.0) + entry.Detour)
                .FirstOrDefault();
            if (candidate.Room is null) return false;
            Assign(candidate.Room, request.Purpose, request.Id);
            available.RemoveAll(room => room == candidate.Room || request.Purpose == RoomPurpose.Inn && room.BuildingId == candidate.Room.BuildingId);
        }

        var remaining = available.OrderByDescending(room =>
                Math.Abs(room.TopLeft.X + room.Width / 2 - maze.Entrance.X) +
                Math.Abs(room.TopLeft.Y + room.Height / 2 - maze.Entrance.Y))
            .ThenBy(_ => random.Next()).ToList();
        foreach (var request in requests.Where(request => !settings.SpecialRoomPlacements.ContainsKey(request.Id)))
        {
            var selected = remaining.FirstOrDefault(room => Eligible(room, request.Purpose, request.Id));
            if (selected is null) return false;
            Assign(selected, request.Purpose, request.Id);
            remaining.RemoveAll(room => room == selected || request.Purpose == RoomPurpose.Inn && room.BuildingId == selected.BuildingId);
        }
        return true;
    }

    // A közönséges zárt/zárolt ajtók nyithatók: a topológiához járatként számítanak.
    private static bool Passable(Maze maze, Position position) =>
        maze.IsInside(position) && (maze.GetDoorAt(position) is not null || maze.IsWalkable(position));

    private static Dictionary<Position, int> Distances(Maze maze, Position start, HashSet<Position>? blocked = null)
    {
        var result = new Dictionary<Position, int> { [start] = 0 };
        var pending = new Queue<Position>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var current))
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (blocked?.Contains(next) == true || !Passable(maze, next) ||
                    !result.TryAdd(next, result[current] + 1)) continue;
                pending.Enqueue(next);
            }
        return result;
    }

    private static IEnumerable<Position> FloorPositions(Maze maze)
    {
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++)
            if (Passable(maze, new(x, y))) yield return new(x, y);
    }

    private static IEnumerable<Position> Boundary(Room room)
    {
        for (var y = room.TopLeft.Y - 1; y <= room.TopLeft.Y + room.Height; y++)
        for (var x = room.TopLeft.X - 1; x <= room.TopLeft.X + room.Width; x++)
            if (!room.Contains(new(x, y))) yield return new(x, y);
    }
}
