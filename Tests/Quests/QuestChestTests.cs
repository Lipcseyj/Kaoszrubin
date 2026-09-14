using System.Text.Json;
using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.World;
using static KaoszRubin.Tests.Quests.QuestTestFixture;

namespace KaoszRubin.Tests.Quests;

internal static class QuestChestTests
{
    private static readonly QuestChestId ChestId = new("RELIC_TEST");
    private const string Rows = @"
#Quest ládák
RELIC_TEST;Próba ereklyeláda;17
#Quest láda tartalom
RELIC_TEST;T001;2
RELIC_TEST;T011;3
";
    private static string Original => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    private static GameDataCatalog Load(string text)
    {
        var file = Path.GetTempFileName();
        try { File.WriteAllText(file, text); return CsvGameDataLoader.Load(file); }
        finally { File.Delete(file); }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException or JsonException) { return; }
        throw new InvalidOperationException("Hibás ládaadat átjutott az ellenőrzésen.");
    }

    public static void CsvResolvesChestAndObjective()
    {
        var text = Original.Replace("NPCQ019;NPC012;Explore;EXIT", "NPCQ019;NPC012;OpenQuestChest;RELIC_TEST") + Rows;
        var data = Load(text);
        var chest = data.GetQuestChest(new("relic_test"));
        Check(chest.Id == ChestId && chest.Gold == 17 && chest.Items.Sum(item => item.Quantity) == 5 &&
            ReferenceEquals(chest.Items[0].Item, data.GetItem("T001")) &&
            data.Quests.Get(QuestId.GraveKeeperLostGraveMarker).Objective is QuestObjective.OpenQuestChest objective &&
            objective.ChestId == ChestId, "A CSV-tartalom vagy ládaobjective hibás.");
        foreach (var invalid in new[]
        {
            text.Replace("RELIC_TEST;T001;2", "UNKNOWN;T001;2"),
            text.Replace("RELIC_TEST;T001;2", "RELIC_TEST;MISSING;2"),
            text.Replace("RELIC_TEST;T001;2", "RELIC_TEST;T001;0"),
            text.Replace("RELIC_TEST;T001;2", @"RELIC_TEST;T001;2
RELIC_TEST;T001;1"),
            text.Replace("Próba ereklyeláda;17", "Próba ereklyeláda;-1"),
            text.Replace("OpenQuestChest;RELIC_TEST", "OpenQuestChest;UNKNOWN")
        }) Reject(() => Load(invalid));
    }

    public static void FirstOpeningCountsButEmptyingDoesNot()
    {
        var data = Load(Original + Rows);
        var specific = Define(new QuestObjective.OpenQuestChest(ChestId));
        var any = Define(new QuestObjective.OpenChests(3), QuestId.MonsterHunterGoblinHunt);
        var fixture = new QuestTestFixture(specific, any);
        fixture.Manager.Activate(specific.Id);
        fixture.Manager.Activate(any.Id);
        fixture.Manager.RegisterChestOpened();
        fixture.Manager.RegisterChestOpened(new("OTHER"));
        Check(fixture.Manager.GetQuest(specific.Id).Progress == 0, "Másik láda is teljesítette a konkrét objective-ot.");
        var chest = new TreasureChest(new(3, 3), data.GetQuestChest(ChestId));
        var gold = 0;
        var service = new QuestChestService(fixture.Manager);
        var first = service.Collect(chest, _ => false, amount => gold += amount);
        Check(first.FirstOpening && first.RemainingCount == 5 && gold == 17 &&
            fixture.Manager.GetQuest(specific.Id).IsReadyToTurnIn && fixture.Manager.GetQuest(any.Id).Progress == 3,
            "A teli inventory akadályozta a ládanyitási objective-ot.");
        var quota = 2;
        var second = service.Collect(chest, _ => quota-- > 0, amount => gold += amount);
        Check(!second.FirstOpening && second.Changes.Count == 0 && second.ItemCount == 2 &&
            second.RemainingCount == 3 && gold == 17, "Az újranyitás új aranyat/progresst adott vagy elvesztette a maradékot.");
        var final = service.Collect(chest, _ => true, amount => gold += amount);
        Check(final.ItemCount == 3 && chest.RemainingItems.Count == 0 && chest.IsOpened &&
            final.Changes.Count == 0 && gold == 17, "A végső kipakolás nem őrizte a mennyiségeket.");
        var late = new QuestTestFixture(specific);
        late.OpenedChests.Add(ChestId);
        Check(late.Manager.Activate(specific.Id).IsReadyToTurnIn,
            "A jelen lévő, már kinyitott ládához később felvett quest megakadt.");
    }

    public static void FullBackpackKeepsContentInChest()
    {
        var data = Load(Original + Rows);
        var fixture = new QuestTestFixture();
        var actor = fixture.SelectedCharacter;
        while (actor.AddToBackpack(data.GetWeapon("W001"))) { }
        var chest = new TreasureChest(new(3, 3), data.GetQuestChest(ChestId));
        var service = new QuestChestService(fixture.Manager);
        var first = service.Collect(chest, item => actor.AddToBackpack(item), _ => { });
        Check(first.ItemCount == 0 && first.RemainingCount == 5, "Teli hátizsáknál eltűnt a ládatartalom.");
        for (var count = 0; count < LiveCharacter.MaximumBackpackStackSize; count++) actor.RemoveFromBackpack("W001");
        var next = service.Collect(chest, item => actor.AddToBackpack(item), _ => { });
        Check(next.ItemCount > 0 && next.RemainingCount > 0 && next.ItemCount + next.RemainingCount == 5,
            "Egy szabad hely nem eredményezett veszteségmentes részleges felvételt.");
    }

    public static void PartialContentSurvivesSaveAndReplication()
    {
        var data = Load(Original + Rows);
        var fixture = new QuestTestFixture();
        var roster = new CharacterRoster();
        roster.Add(fixture.SelectedCharacter);
        roster.Select(fixture.SelectedCharacter);
        var mapper = new GameStateMapper(data, roster, fixture.SelectedCharacter);
        var maze = new Maze(9, 9);
        for (var y = 1; y < 8; y++)
        for (var x = 1; x < 8; x++) maze.Carve(new(x, y));
        maze.PlaceExit(new(7, 7));
        var chest = new TreasureChest(new(3, 3), data.GetQuestChest(ChestId));
        maze.AddTreasureChest(chest);
        var fog = new FogOfWar(9, 9, 0);
        fog.Restore([chest.Position], false);
        var before = WorldSnapshotProjector.Create(maze, fog);
        var service = new QuestChestService(fixture.Manager);
        var quota = 2;
        service.Collect(chest, _ => quota-- > 0, _ => { });
        var after = WorldSnapshotProjector.Create(maze, fog);
        var delta = JsonSerializer.Deserialize<WorldDelta>(JsonSerializer.Serialize(WorldDeltaProjector.Create(1, before, 2, after)))!;
        Check(WorldDeltaReducer.Apply(before, delta).Chests.Single() is
            { DefinitionId: "RELIC_TEST", IsOpened: true, RemainingItemCount: 3 }, "A kliens elvesztette a részleges láda állapotát.");
        GameSaveData Save() => mapper.Create(5, maze, new Player(maze.Entrance, fixture.SelectedCharacter), fog,
            Direction.Right, [], false, false, false, false, null, DateTime.UtcNow, new Dictionary<Enemy, DateTime>(), [], []);
        var saved = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(Save()))!;
        var restored = mapper.Restore(saved).Maze.TreasureChests.Single();
        Check(restored.Definition!.Id == ChestId && restored.IsOpened && restored.GoldAmount == 0 &&
            restored.RemainingItems.Sum(item => item.Quantity) == 3, "A mentés újratöltötte a ládát.");
        var taken = service.Collect(restored, _ => true, _ => throw new InvalidOperationException("Új arany betöltésből."));
        Check(!taken.FirstOpening && taken.ItemCount == 3 && taken.Changes.Count == 0, "Betöltés után ismételt nyitási esemény történt.");
        saved.Maze.Chests[0] = saved.Maze.Chests[0] with
        { QuestChest = saved.Maze.Chests[0].QuestChest! with { RemainingItems = [] } };
        Check(mapper.Restore(saved).Maze.TreasureChests.Single().RemainingItems.Count == 0, "Az üres mentett láda feltöltődött.");
        saved.Maze.Chests.Add(saved.Maze.Chests[0] with { Position = new(4, 3) });
        Reject(() => mapper.Restore(saved));
        var old = new GameSaveData { Version = 23 };
        old.Maze.Chests.Add(new(new(3, 3), 42));
        GameSaveFormat.MigrateToCurrent(old);
        Check(old.Version == 24 && old.Maze.Chests.Single().QuestChest is null, "A régi aranyláda questládává változott.");
    }

    public static void PlacementUsesNamedRoomAndRejectsDuplicates()
    {
        var data = Load(Original + Rows);
        var settings = new MazeGenerationSettings
        { RoomCount = 5, QuestRoomIds = ["RELIC_ROOM"], TreasureChestCount = 0 };
        var maze = new MazeGenerator(settings, [], [], new Random(91)).Create(55, 31);
        var placements = new Dictionary<string, QuestChestId> { ["RELIC_ROOM"] = ChestId };
        QuestChestPlacement.Place(maze, data, placements);
        Check(maze.GetRoomByContentId("RELIC_ROOM")!.Contains(maze.TreasureChests.Single().Position),
            "A questláda nem a megadott szobába került.");
        Reject(() => QuestChestPlacement.Place(maze, data, placements));
        Check(maze.TreasureChests.Count == 1, "Duplikált láda jött létre hibás elhelyezéskor.");
    }
}