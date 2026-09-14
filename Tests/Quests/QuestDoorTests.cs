using System.Text.Json;
using KaoszRubin.UI;
using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.World;
using Moq;
using static KaoszRubin.Tests.Quests.QuestTestFixture;

namespace KaoszRubin.Tests.Quests;

internal static class QuestDoorTests
{
    private static QuestDefinition Definition(QuestScope scope = QuestScope.Global) =>
        Define(new QuestObjective.DisarmTraps(2), QuestId.RodericFallenComradesInsignia, QuestNpcId.SirRoderic, scope);
    private static GameDataCatalog Data() => CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    public static void AccessFollowsExactQuestAndRemainsGranted()
    {
        foreach (var state in Enum.GetValues<QuestState>())
        {
            var fixture = new QuestTestFixture(Definition());
            fixture.Manager.RestoreState([new(Definition().Id, default, state,
                state is QuestState.ReadyToTurnIn or QuestState.Completed ? 2 : 0,
                state == QuestState.Completed ? 1 : 0)]);
            var door = new MazeDoor(new(3, 2), DoorState.Closed, new QuestKey(Definition().Id));
            Check(!door.TrySetState(DoorState.Open) && !door.TrySetState(DoorState.Smashed), "A questzár közvetlen állapotváltással megkerülhető.");
            var allowed = new QuestDoorAccessService(fixture.Manager).TryGrantAccess(door);
            Check(allowed == (state is QuestState.Active or QuestState.ReadyToTurnIn or QuestState.Completed),
                $"Hibás questajtó-feltétel: {state}.");
            Check(fixture.StoredRewards.Count == 0 && fixture.ConsumptionAttempts == 0, "A nyitás questmellékhatást okozott.");
        }
        var scoped = new QuestTestFixture(Definition(QuestScope.PerNpcInstance));
        scoped.Manager.Activate(Definition().Id, new(1));
        var service = new QuestDoorAccessService(scoped.Manager);
        var other = new MazeDoor(new(3, 2), DoorState.Closed, new QuestKey(Definition().Id, new(2)));
        Check(!service.TryGrantAccess(other), "Másik NPC-példány aktív questje is kinyitja az ajtót.");
        var own = new MazeDoor(new(4, 2), DoorState.Closed, new QuestKey(Definition().Id, new(1)));
        Check(service.TryGrantAccess(own) && own.TrySetState(DoorState.Open), "A megfelelő quest nem engedett be.");
        scoped.Manager.GetQuest(Definition().Id, new(1)).Abandon();
        own.TrySetState(DoorState.Closed);
        Check(service.TryGrantAccess(own) && own.TrySetState(DoorState.Open), "A feladás bezárta a már megnyitott kijáratot.");
    }

    public static void DeniedInteractionSpendsNothing()
    {
        var fixture = new QuestTestFixture(Definition());
        var data = Data();
        var actor = fixture.SelectedCharacter;
        actor.AddToBackpack(data.GetItem(MiscItemIds.Key));
        var maze = new Maze(9, 9);
        maze.Carve(new(2, 2));
        maze.PlaceDoor(new(3, 2), DoorState.Locked, new QuestKey(Definition().Id));
        var renderer = new Mock<IDoorInteractionRenderer>();
        var random = new NoRollRandom();
        var controller = new DoorInteractionController(data, renderer.Object, (_, _) => { }, random,
            tryGrantQuestAccess: new QuestDoorAccessService(fixture.Manager).TryGrantAccess);
        foreach (var keyChoice in new[] { true, false })
            controller.TryOpenAdjacentDoor(maze, new FogOfWar(9, 9, 0), new(2, 2), new(2, 2), actor, false,
                new(3, 2), keyChoice, actor.Id, [actor]);
        renderer.Verify(r => r.DrawDoorMessage(
            "Az ajtót küldetés zárja le. Előbb vedd fel a hozzá tartozó küldetést.",
            ConsoleColor.DarkYellow), Times.Exactly(2));
        Check(DoorInteractionRules.HasKey(actor) && actor.FoodLevel == 100 && actor.WaterLevel == 100 &&
            maze.GetDoorAt(new(3, 2)) is { State: DoorState.Locked, IsQuestSealed: true },
            "Az elutasított nyitás kulcsot/szükségletet fogyasztott vagy módosította az ajtót.");
        var plain = new MazeDoor(new(4, 2), DoorState.Locked);
        Check(new QuestDoorAccessService(fixture.Manager).TryGrantAccess(plain) && plain.TrySetState(DoorState.Smashed),
            "A közönséges ajtó működését is korlátozza a questzár.");
    }

    public static void SaveAndWorldDeltaPreserveGate()
    {
        var data = Data();
        var fixture = new QuestTestFixture(Definition());
        var roster = new CharacterRoster();
        roster.Add(fixture.SelectedCharacter);
        roster.Select(fixture.SelectedCharacter);
        var mapper = new GameStateMapper(data, roster, fixture.SelectedCharacter);
        var maze = new Maze(9, 9);
        for (var y = 1; y < 8; y++)
        for (var x = 1; x < 8; x++) maze.Carve(new(x, y));
        maze.PlaceExit(new(7, 7));
        maze.PlaceDoor(new(3, 2), DoorState.Closed, new QuestKey(Definition().Id));
        var fog = new FogOfWar(9, 9, 0);
        fog.Restore([new(3, 2)], false);
        GameSaveData Save() => mapper.Create(5, maze, new Player(maze.Entrance, fixture.SelectedCharacter), fog,
            Direction.Right, [], false, false, false, false, null, DateTime.UtcNow,
            new Dictionary<Enemy, DateTime>(), [], []);
        var sealedSave = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(Save()))!;
        Check(mapper.Restore(sealedSave).Maze.GetDoorAt(new(3, 2)) is { IsQuestSealed: true },
            "A mentés elvesztette a questzárat.");
        var before = WorldSnapshotProjector.Create(maze, fog);
        fixture.Manager.Activate(Definition().Id);
        var door = maze.GetDoorAt(new(3, 2))!;
        new QuestDoorAccessService(fixture.Manager).TryGrantAccess(door);
        maze.SetDoorState(door, DoorState.Open);
        var after = WorldSnapshotProjector.Create(maze, fog);
        var delta = JsonSerializer.Deserialize<WorldDelta>(JsonSerializer.Serialize(WorldDeltaProjector.Create(1, before, 2, after)))!;
        var reduced = WorldDeltaReducer.Apply(before, delta);
        Check(before.Doors.Single().IsQuestSealed && reduced.Doors.Single() is { IsQuestSealed: false, State: DoorState.Open },
            "A full/delta wire-adat nem őrizte a questzár feloldását.");
        var saved = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(Save()))!;
        var restored = mapper.Restore(saved).Maze.GetDoorAt(new(3, 2))!;
        Check(restored.RequiredQuest == door.RequiredQuest && restored.QuestAccessGranted && restored.State == DoorState.Open,
            "A megnyitott ajtó újracsatlakozás/betöltés után visszazáródik.");
        saved.Maze.Doors[0] = saved.Maze.Doors[0] with { QuestGate = new(new(Definition().Id), false) };
        var rejected = false;
        try { mapper.Restore(saved); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Nyitott, de fel nem oldott questajtó átjutott a betöltésen.");
        var old = new GameSaveData { Version = 22 };
        old.Maze.Doors.Add(new(new(3, 2), DoorState.Open));
        GameSaveFormat.MigrateToCurrent(old);
        Check(old.Version == GameSaveFormat.CurrentVersion && old.Maze.Doors.Single().QuestGate is null, "A régi mentés ajtajára utólag questzár került.");
    }

    public static void GeneratedRoomHasOneSealedQuestDoor()
    {
        var settings = MazeLevelConfigurations.Get(5).CreateGenerationSettings(new Random(19));
        var maze = new MazeGenerator(settings, [], [], new Random(20)).Create(55, 31);
        var door = maze.Doors.Single(d => d.RequiredQuest == new QuestKey(Definition().Id));
        Check(door.RequiredQuest == new QuestKey(Definition().Id) && door.IsQuestSealed &&
            door.State == DoorState.Closed && !door.IsWalkable, "A jelvényes szoba nem lezárt questajtót kapott.");
        var invalid = new MazeGenerationSettings
        {
            QuestRoomIds = ["ROOM"],
            QuestDoorRequirements = new Dictionary<string, QuestId> { ["ROOM"] = Definition().Id }
        };
        var rejected = false;
        try { _ = new MazeGenerator(invalid, [], []); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Questzár került nem garantált mellékszobára.");
    }

    private sealed class NoRollRandom : Random
    {
        public override int Next(int minValue, int maxValue) => throw new InvalidOperationException("A tiltott próbához dobás történt.");
    }
}