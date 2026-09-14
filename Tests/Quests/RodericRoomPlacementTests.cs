using KaoszRubin.World;

namespace KaoszRubin.Tests.Quests;

internal static class RodericRoomPlacementTests
{
    public static void PlacementSurvivesMultipleSeeds()
    {
        for (var seed = 0; seed < 80; seed++)
        {
            var settings = seed < 40
                ? MazeLevelConfigurations.Get(5).CreateGenerationSettings(new Random(seed))
                : new MazeGenerationSettings
                {
                    RoomCount = 8, MinimumRoomSize = 4, MaximumRoomSize = 7, TreasureChestCount = 0,
                    QuestRoomIds = ["MEETING"],
                    BossRoomIds = ["INSIGNIA", "PATRIARCHS", "RELICS"],
                    SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
                    {
                        ["MEETING"] = SpecialRoomPlacement.MiddleRoute,
                        ["INSIGNIA"] = SpecialRoomPlacement.SideBranch,
                        ["PATRIARCHS"] = SpecialRoomPlacement.SideBranch,
                        ["RELICS"] = SpecialRoomPlacement.SideBranch
                    }
                };
            var width = seed % 2 == 0 ? 55 : 170;
            var height = seed % 2 == 0 ? 31 : 44;
            var maze = new MazeGenerator(settings, [], [], new Random(seed)).Create(width, height);
            var rooms = maze.Rooms.Where(room => room.ContentId is not null).ToArray();
            Check(rooms.Length == settings.QuestRoomIds.Count + settings.BossRoomIds.Count, seed, "Hiányzó speciális szoba.");
            var blocked = new HashSet<Position>();
            foreach (var room in rooms.Where(room => settings.SpecialRoomPlacements[room.ContentId!] == SpecialRoomPlacement.SideBranch))
            {
                var boundary = Boundary(room).ToArray();
                var openings = boundary.Where(p => maze.IsWalkable(p) || maze.GetDoorAt(p) is not null).ToArray();
                Check(openings.Length == 1 && maze.GetDoorAt(openings[0]) is not null, seed, "A mellékszobának nem egy ajtaja van.");
                blocked.UnionWith(room.InteriorPositions());
                blocked.UnionWith(boundary);
            }
            var reachable = Distances(maze, maze.Entrance, blocked);
            Check(reachable.ContainsKey(maze.Exit), seed, "A lezárt szobák elvágják a kijáratot.");
            for (var y = 0; y < maze.Height; y++)
            for (var x = 0; x < maze.Width; x++)
            {
                var p = new Position(x, y);
                if (!blocked.Contains(p) && (maze.IsWalkable(p) || maze.GetDoorAt(p) is not null))
                    Check(reachable.ContainsKey(p), seed, "Másik pályarész is a lezárt szobák mögé került.");
            }
            var meeting = rooms.Single(room => settings.SpecialRoomPlacements[room.ContentId!] == SpecialRoomPlacement.MiddleRoute);
            var center = new Position(meeting.TopLeft.X + meeting.Width / 2, meeting.TopLeft.Y + meeting.Height / 2);
            var fromExit = Distances(maze, maze.Exit, blocked);
            var routeLength = reachable[maze.Exit];
            var progress = (reachable[center] - fromExit[center] + routeLength) / 2.0;
            var detour = (reachable[center] + fromExit[center] - routeLength) / 2.0;
            Check(progress >= routeLength / 3.0 && progress <= routeLength * 2 / 3.0 &&
                detour <= Math.Max(8, routeLength * 0.15), seed, "Roderic túl korán/későn vagy túl nagy kitérővel érhető el.");
            Check(maze.TreasureChests.All(chest => !rooms.Any(room => room.Contains(chest.Position))),
                seed, "Véletlen láda került speciális szobába.");
        }
    }

    public static void SeedAndConfigurationAreValidated()
    {
        var settings = MazeLevelConfigurations.Get(5).CreateGenerationSettings(new Random(123));
        var first = new MazeGenerator(settings, [], [], new Random(456)).Create(55, 31);
        var second = new MazeGenerator(settings, [], [], new Random(456)).Create(55, 31);
        Check(first.Rooms.SequenceEqual(second.Rooms) && first.Tiles.Cast<System.Text.Rune>().SequenceEqual(second.Tiles.Cast<System.Text.Rune>()) &&
            first.Doors.Select(d => (d.Position, d.State)).SequenceEqual(second.Doors.Select(d => (d.Position, d.State))),
            456, "Azonos seed eltérő elrendezést eredményez.");
        foreach (var invalid in new[]
        {
            new MazeGenerationSettings { RoomCount = 0, QuestRoomIds = ["MISSING"] },
            new MazeGenerationSettings { QuestRoomIds = ["SAME"], BossRoomIds = ["SAME"] },
            new MazeGenerationSettings { SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
                { ["UNKNOWN"] = SpecialRoomPlacement.SideBranch } }
        })
        {
            var rejected = false;
            try { _ = new MazeGenerator(invalid, [], []); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, 0, "A hibás konfigurációt nem utasította el a generátor.");
        }
    }

    private static IEnumerable<Position> Boundary(Room room) =>
        room.InteriorPositions().SelectMany(p => Enum.GetValues<Direction>().Select(d => p + d))
            .Where(p => !room.Contains(p)).Distinct();

    private static Dictionary<Position, int> Distances(Maze maze, Position start, HashSet<Position> blocked)
    {
        var distances = new Dictionary<Position, int> { [start] = 0 };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var p))
            foreach (var d in Enum.GetValues<Direction>())
            {
                var next = p + d;
                if (blocked.Contains(next) || !maze.IsInside(next) ||
                    !(maze.IsWalkable(next) || maze.GetDoorAt(next) is not null) ||
                    !distances.TryAdd(next, distances[p] + 1)) continue;
                queue.Enqueue(next);
            }
        return distances;
    }

    private static void Check(bool condition, int seed, string message)
    {
        if (!condition) throw new InvalidOperationException($"Seed {seed}: {message}");
    }
}