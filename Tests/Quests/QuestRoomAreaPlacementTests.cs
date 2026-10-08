using System.Reflection;
using System.Runtime.CompilerServices;
using KaoszRubin.Infrastructure.Quests;

namespace KaoszRubin.Tests.Quests;

internal static class QuestRoomAreaPlacementTests
{
    private static readonly string[] AreaIds = ["EXIT", "ENTRY", "MIDDLE"];
    private static GameDataCatalog Data() => CsvGameDataLoader.Load(
        Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        catch (InvalidDataException) { return; }
        throw new Exception("A hibás szobacél elfogadásra került.");
    }
    private static MazeGenerationSettings Settings(IReadOnlyDictionary<string, QuestRoomPlacementConfiguration>? placements = null) => new()
    {
        RoomCount = 6, MinimumRoomSize = 4, MaximumRoomSize = 6, TreasureChestCount = 0,
        QuestRoomIds = ["ON_SCREEN", "ON_AREA", "DEFAULT"], BossRoomIds = ["BOSS"],
        QuestRoomPlacements = placements ?? new Dictionary<string, QuestRoomPlacementConfiguration>
        {
            ["ON_SCREEN"] = new(ScreenNumber: 2), ["ON_AREA"] = new(AreaId: "MIDDLE")
        },
        SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
        { ["ON_SCREEN"] = SpecialRoomPlacement.SideBranch },
        QuestDoorRequirements = new Dictionary<string, QuestId>
        { ["ON_SCREEN"] = QuestId.RodericFallenComradesInsignia }
    };

    public static void QuestRoomAreaTargetsAndLocalRules()
    {
        var distributed = Game.DistributeSpecialRooms(Settings(), AreaIds, "EXIT");
        Check(distributed[0].QuestRoomIds.SequenceEqual(["DEFAULT"]) &&
              distributed[0].BossRoomIds.SequenceEqual(["BOSS"]) &&
              distributed[1].QuestRoomIds.SequenceEqual(["ON_SCREEN"]) &&
              distributed[2].QuestRoomIds.SequenceEqual(["ON_AREA"]), "Nem a kijelölt területek kapták a szobákat.");
        Check(distributed[1].SpecialRoomPlacements["ON_SCREEN"] == SpecialRoomPlacement.SideBranch &&
              distributed[1].QuestDoorRequirements.ContainsKey("ON_SCREEN") &&
              distributed[0].QuestDoorRequirements.Count == 0 && distributed[2].SpecialRoomPlacements.Count == 0,
            "A mellékág- vagy ajtószabály rossz képernyőre került.");
        var legacy = Game.DistributeSpecialRooms(Settings(new Dictionary<string, QuestRoomPlacementConfiguration>()), AreaIds, "EXIT");
        Check(legacy[0].QuestRoomIds.Count == 3 && legacy[1].QuestRoomIds.Count == 0,
            "A régi, cél nélküli konfiguráció nem a kijárati területet használja.");
        var single = Game.DistributeSpecialRooms(Settings(new Dictionary<string, QuestRoomPlacementConfiguration>
            { ["ON_SCREEN"] = new(1, "ONLY") }), ["ONLY"], "ONLY");
        Check(single[0].QuestRoomIds.Count == 3 && single[0].RoomCount >= 4,
            "Az egyképernyős szobák nem férnek el.");
    }

    public static void QuestRoomAreaRejectsInvalidTargets()
    {
        foreach (var target in new[]
        {
            new QuestRoomPlacementConfiguration(ScreenNumber: 0), new(ScreenNumber: -1), new(ScreenNumber: 4),
            new(AreaId: "MISSING"), new(AreaId: ""), new(2, "MIDDLE")
        })
            Reject(() => Game.DistributeSpecialRooms(Settings(new Dictionary<string, QuestRoomPlacementConfiguration>
                { ["ON_SCREEN"] = target }), AreaIds, "EXIT"));
        Reject(() => Game.DistributeSpecialRooms(Settings(new Dictionary<string, QuestRoomPlacementConfiguration>
            { ["MISSING_ROOM"] = new(1) }), AreaIds, "EXIT"));
        Reject(() => Game.DistributeSpecialRooms(Settings(), AreaIds, "MISSING_EXIT"));
        Reject(() => Game.DistributeSpecialRooms(Settings(), ["EXIT", "EXIT"], "EXIT"));
        var matching = Game.DistributeSpecialRooms(Settings(new Dictionary<string, QuestRoomPlacementConfiguration>
            { ["ON_SCREEN"] = new(2, "ENTRY") }), AreaIds, "EXIT");
        Check(matching[1].QuestRoomIds.Contains("ON_SCREEN"), "Az egyező kettős cél nem működik.");
    }

    private static MazeLevelConfiguration Configuration(MazeLayoutConfiguration layout,
        IReadOnlyDictionary<string, QuestRoomPlacementConfiguration> placements) => new()
    {
        Level = 1, Name = "Szobacél-teszt", Layout = layout, RoomCount = new(12, 12), RoomSize = new(4, 6),
        TreasureChestCount = new(0, 0), TreasureGold = new(0, 0), RoomEncounters = [], CorridorEncounters = [],
        QuestRoomIds = ["ON_SCREEN", "ON_AREA", "DEFAULT"], BossRoomIds = ["BOSS"],
        QuestRoomPlacements = placements
    };

    private static (Game Game, DungeonLevel Level) Generate(MazeLevelConfiguration configuration, GameDataCatalog data, int seed)
    {
        // Csak a generálási függvényt futtatjuk, konzolablak és játékhurok nélkül.
        var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
        foreach (var (name, value) in new (string, object)[]
                 { ("_random", new Random(seed)), ("_gameData", data), ("_difficultyLevel", 1) })
            typeof(Game).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, value);
        try
        {
            var level = (DungeonLevel)typeof(Game).GetMethod("GenerateDungeonLevel", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(game, [configuration, null])!;
            typeof(Game).GetField("_dungeonLevel", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, level);
            return (game, level);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        { throw exception.InnerException; }
    }

    public static void QuestRoomAreaWideAndForestGeneration()
    {
        var data = Data();
        var forest = new ForestMazeLayoutConfiguration(new(new(3, 3)), new ForestGenerationConfiguration(),
            new ExplicitForestAreaGraphConfiguration(
                [new("EXIT", "Kijárat", new(2, 0)), new("ENTRY", "Bejárat", new(0, 0)), new("MIDDLE", "Közép", new(1, 0))],
                [new("ENTRY", "MIDDLE"), new("MIDDLE", "EXIT")], "ENTRY", "EXIT"));
        foreach (var layout in new MazeLayoutConfiguration[] { new WideMazeLayoutConfiguration(new(3, 3)), forest })
        {
            var areaId = layout == forest ? "MIDDLE" : "AREA_3";
            var exitId = layout == forest ? "EXIT" : "AREA_3";
            var configuration = Configuration(layout, new Dictionary<string, QuestRoomPlacementConfiguration>
                { ["ON_SCREEN"] = new(2), ["ON_AREA"] = new(AreaId: areaId) });
            for (var seed = 1; seed <= 3; seed++)
            {
                var (_, level) = Generate(configuration, data, seed);
                Check(level.Areas[1].Maze.GetRoomByContentId("ON_SCREEN") is not null &&
                      level.Areas.Single(area => area.Id == areaId).Maze.GetRoomByContentId("ON_AREA") is not null &&
                      level.Areas.Single(area => area.Id == exitId).Maze.GetRoomByContentId("DEFAULT") is not null &&
                      level.Areas.Single(area => area.Id == exitId).Maze.GetRoomByContentId("BOSS") is not null,
                    $"A tényleges {layout.Style} generálás eltér a szobacéloktól (seed {seed}).");
                Check(level.Areas.Sum(area => area.Maze.Rooms.Count(room => room.ContentId is not null)) == 4,
                    "Hiányzó vagy duplikált szoba a generált pályán.");
            }
        }
    }

    public static void QuestRoomAreaContentsFollowRooms()
    {
        var data = Data();
        var ids = data.QuestChests.Take(2).Select(chest => chest.Id).ToArray();
        var configuration = Configuration(new WideMazeLayoutConfiguration(new(3, 3)),
            new Dictionary<string, QuestRoomPlacementConfiguration>
                { ["ON_SCREEN"] = new(1), ["ON_AREA"] = new(AreaId: "AREA_2") });
        var (game, level) = Generate(configuration, data, 31);
        var content = new MazeLevelConfiguration
        {
            Level = 1, Name = "Tartalom", RoomCount = new(1, 1), RoomSize = new(4, 6),
            TreasureChestCount = new(0, 0), TreasureGold = new(0, 0), RoomEncounters = [], CorridorEncounters = [],
            QuestChestPlacements = new Dictionary<string, QuestChestId>
                { ["ON_SCREEN"] = ids[0], ["ON_AREA"] = ids[1] },
            QuestRoomEnemyEncounters = [new("ON_SCREEN", MonsterIds.Csontváz, 1), new("ON_AREA", MonsterIds.Zombi, 2)]
        };
        typeof(Game).GetMethod("PlaceSpecialRoomContent", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(game, [content]);
        for (var index = 0; index < 2; index++)
        {
            var roomId = index == 0 ? "ON_SCREEN" : "ON_AREA";
            var maze = level.Areas[index].Maze;
            var room = maze.GetRoomByContentId(roomId)!;
            Check(maze.TreasureChests.Count == 1 && maze.TreasureChests[0].Definition!.Id == ids[index] &&
                  room.Contains(maze.TreasureChests[0].Position) && maze.Enemies.Count == index + 1 &&
                  maze.Enemies.All(enemy => room.Contains(enemy.Position)), "A láda vagy ellenfél nem követte a szobát.");
        }
        Check(level.Areas[2].Maze.TreasureChests.Count == 0 && level.Areas[2].Maze.Enemies.Count == 0,
            "A kijárati terület téves questtartalmat kapott.");
        // A többterületes elhelyezés megismétlése nem hozhat létre új ládát.
        Reject(() => QuestChestPlacement.Place(level, data, content.QuestChestPlacements));
    }

    public static void QuestRoomAreaChestValidationAcrossScreens()
    {
        var data = Data();
        var (_, level) = Generate(Configuration(new WideMazeLayoutConfiguration(new(3, 3)),
            new Dictionary<string, QuestRoomPlacementConfiguration>
            { ["ON_SCREEN"] = new(1), ["ON_AREA"] = new(2) }), data, 51);
        var chestId = data.QuestChests[0].Id;
        Reject(() => QuestChestPlacement.Place(level, data, new Dictionary<string, QuestChestId>
            { ["ON_SCREEN"] = chestId, ["ON_AREA"] = chestId }));
        Reject(() => QuestChestPlacement.Place(level, data, new Dictionary<string, QuestChestId>
            { ["MISSING_ROOM"] = chestId }));
        Check(level.Areas.All(area => area.Maze.TreasureChests.Count == 0), "Hibás cél ellenére láda keletkezett.");
    }
}
