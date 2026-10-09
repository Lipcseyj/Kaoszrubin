using System.Reflection;

internal static partial class Program
{
    static void HiddenEnemiesNeverTintExploredTerrain()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var party = new Party();
        party.SetLeader(CreateCharacter("Vezér"));
        var renderer = new ConsoleRenderer(data, party);
        var draw = typeof(ConsoleRenderer).GetMethod("GetMapCellVisual", BindingFlags.Instance | BindingFlags.NonPublic)!;
        (Rune Rune, ConsoleColor Foreground, ConsoleColor Background) Visual(Maze maze, FogOfWar fog, Position position)
        {
            var value = draw.Invoke(renderer, [maze, fog, position, null])!;
            object Property(string name) => value.GetType().GetProperty(name)!.GetValue(value)!;
            return ((Rune)Property("Rune"), (ConsoleColor)Property("ForegroundColor"),
                (ConsoleColor)Property("BackgroundColor"));
        }
        foreach (var style in new[]
        {
            new MazeTerrainStyle("marsh", new('≋'), ConsoleColor.DarkYellow, ConsoleColor.DarkGreen, true, false),
            new MazeTerrainStyle("undergrowth", new('░'), ConsoleColor.Green, ConsoleColor.DarkBlue, true, false)
        })
        {
            var maze = new Maze(21, 11);
            for (var y = 1; y < 10; y++)
            for (var x = 1; x < 20; x++) maze.Carve(new(x, y));
            var target = new Position(14, 5);
            var other = new Position(15, 5);
            maze.SetTerrain(target, style);
            maze.SetTerrain(other, style);
            var fog = new FogOfWar(21, 11, 2);
            fog.RevealFrom(maze, target, 1);
            fog.UpdatePartyVisibility(maze, [(new Position(2, 5), 2)], false);
            var empty = Visual(maze, fog, target);
            var snapshotBefore = WorldSnapshotProjector.Create(maze, fog);
            var enemy = new ConfiguredEnemy(target, data.GetEnemy("E001") with
            { Stealth = 20, Noise = 0, StrengthTier = 4 }, new Random(1));
            maze.AddEnemy(enemy);
            fog.UpdatePartyVisibility(maze, [(new Position(2, 5), 2)], false);
            Assert(fog.IsRevealed(target) && !fog.IsEnemyVisible(enemy.Id, enemy.Position) &&
                fog.EnemyMemoryAt(target) is null && Visual(maze, fog, target) == empty,
                $"A rejtett ellenfél elszínezte a(z) {style.Id} terepet.");
            enemy.MoveTo(other);
            Assert(Visual(maze, fog, target) == empty && Visual(maze, fog, other) == empty,
                "A távoli ellenfél mozgása színnyomot hagyott a felfedezett terepen.");
            var hidden = WorldSnapshotProjector.Create(maze, fog);
            Assert(hidden.Enemies.Count == 0 && WorldDeltaProjector.Create(1, snapshotBefore, 2, hidden).IsEmpty,
                "A kliens megkapta a rejtett ellenfél helyét vagy színét.");
            fog.UpdatePartyVisibility(maze, [(other, 2)], false);
            Assert(fog.IsEnemyVisible(enemy.Id, enemy.Position) &&
                Visual(maze, fog, other).Rune == enemy.Symbol &&
                Visual(maze, fog, other).Foreground == ConsoleColor.Red, "Az észrevett ellenfél nem rajzolódott ki.");
            fog.UpdatePartyVisibility(maze, [(new Position(2, 5), 2)], false);
            Assert(Visual(maze, fog, other).Rune == new Rune('?'), "Eltűnt az ellenfél szabályos utolsó ismert helyének jelzése.");
        }
    }

    static void ForestQuestSideBranchesPreserveBuildingWalls()
    {
        var palette = new ForestTerrainPalette();
        var maze = new Maze(23, 17, palette.Tree.Rune, palette.Tree.ForegroundColor);
        maze.ConfigureConnectedTerrainReveal([palette.Tree]);
        var wall = palette.BuildingWall with { Id = "golden-stone", ForegroundColor = ConsoleColor.Yellow };
        var room = new Room(new(8, 6), 5, 5, Kind: RoomKind.Cabin, BuildingId: "CABIN");
        for (var y = 1; y < maze.Height - 1; y++)
        for (var x = 1; x < maze.Width - 1; x++) maze.Carve(new(x, y));
        var boundary = new List<Position>();
        for (var y = 5; y <= 11; y++)
        for (var x = 7; x <= 13; x++)
            if (!room.Contains(new(x, y)))
            { var position = new Position(x, y); boundary.Add(position); maze.SetTerrain(position, wall); }
        maze.AddRoom(room);
        var entrance = new Position(8, 5);
        var removedDoor = new Position(8, 11);
        var breach = new Position(13, 8);
        maze.PlaceDoor(entrance, DoorState.Closed);
        maze.PlaceDoor(removedDoor, DoorState.Open);
        maze.Carve(breach);
        var settings = new MazeGenerationSettings
        {
            RoomCount = 1, QuestRoomIds = ["RAVENS_LOOT_ROOM"],
            SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
            { ["RAVENS_LOOT_ROOM"] = SpecialRoomPlacement.SideBranch }
        };
        Assert(SpecialRoomPlacer.TryAssign(maze, settings, new Random(1), questRoomsRequireBuilding: true),
            "A kézzel előállított, kétajtós questépület nem helyezhető el.");
        Assert(maze.Doors.Count == 1 && maze.GetDoorAt(entrance) is { State: DoorState.Closed } &&
            maze.GetRoomByContentId("RAVENS_LOOT_ROOM")!.BuildingId == room.BuildingId,
            "A questszoba nem egybejáratú épület maradt.");
        Assert(boundary.Where(position => position != entrance).All(position => maze.GetTerrainStyle(position) == wall) &&
            maze.CheckFullAccessibility().IsFullyAccessible, "A szoba lezárása fára cserélte a falakat vagy elzárta a szobát.");

        var configuration = MazeLevelConfigurations.Get(6);
        var graph = ((ForestMazeLayoutConfiguration)configuration.Layout!).ExplicitGraph!;
        var forest = (ForestMazeLayoutConfiguration)configuration.Layout!;
        var area = graph.Areas.Single(value => value.Id == "RAVEN_CROSSING");
        var landscape = ForestAreaConfigurationResolver.Resolve(forest.Forest, graph, area);
        for (var seed = 1; seed <= 12; seed++)
        {
            var generated = new ForestMazeGenerator(new MazeGenerationSettings
            {
                RoomCount = 8, MinimumRoomSize = 5, MaximumRoomSize = 8, TreasureChestCount = 0,
                QuestRoomIds = ["RAVENS_LOOT_ROOM"], InnRoomIds = ["RAVEN_INN"],
                SpecialRoomPlacements = settings.SpecialRoomPlacements,
                SpecialRoomMinimumFreeCells = new Dictionary<string, int>
                { ["RAVENS_LOOT_ROOM"] = 6, ["RAVEN_INN"] = 9 }
            }, landscape, [], [], new Random(seed)).Create(170, 44);
            var questRoom = generated.GetRoomByContentId("RAVENS_LOOT_ROOM")!;
            for (var y = questRoom.TopLeft.Y - 1; y <= questRoom.TopLeft.Y + questRoom.Height; y++)
            for (var x = questRoom.TopLeft.X - 1; x <= questRoom.TopLeft.X + questRoom.Width; x++)
            {
                var position = new Position(x, y);
                if (questRoom.Contains(position) || generated.GetDoorAt(position) is not null) continue;
                Assert(generated.GetTerrainStyle(position) is { Walkable: false, BlocksSight: true } &&
                    !generated.IsConnectedRevealTerrain(position), $"A Holló-szoba épületfala erdei növényzet lett: seed {seed}.");
            }
            Assert(generated.CheckFullAccessibility().IsFullyAccessible, $"Nem bejárható kereszteződés: seed {seed}.");
        }
    }
}
