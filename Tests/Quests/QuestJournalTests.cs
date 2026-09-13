using System.Text.Json;
using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.UI;
using KaoszRubin.World;
using static KaoszRubin.Tests.Quests.QuestTestFixture;

namespace KaoszRubin.Tests.Quests;

internal static class QuestJournalTests
{
    public static void SeparateRowsAndTargetedAbandon()
    {
        var data = LoadData();
        var definition = new QuestCatalogBuilder(data).Build().Get(QuestId.HerbalistHealingSupplies);
        var fixture = new QuestTestFixture(definition);
        var (maze, registry, world) = World();
        var firstNpc = Add(maze, fixture.SelectedCharacter, "NPC001", 2);
        var secondNpc = Add(maze, fixture.Companion, "NPC001", 4);
        var coordinator = new NpcQuestCoordinator(data, fixture.Manager, world);
        var journal = new Dictionary<QuestKey, QuestJournalEntrySnapshot>();
        fixture.Manager.QuestChanged += quest => coordinator.SynchronizeQuestJournal(journal, quest);
        var first = fixture.Manager.Activate(definition.Id, registry.GetOrCreate(firstNpc));
        var second = fixture.Manager.Activate(definition.Id, registry.GetOrCreate(secondNpc));
        Require(journal.Count == 2 && journal[first.Key].QuestGiverName == firstNpc.Character.Name &&
            journal[second.Key].QuestGiverName == secondNpc.Character.Name, "Két futás összeolvadt a naplóban.");
        var lines = QuestJournalWindow.Build(NpcQuestCoordinator.OrderedQuestJournal(journal.Values), second.Key);
        Require(lines.Count(line => line.Text.Contains('▶')) == 1 && lines.Count(line => line.Text.Contains('◇')) == 1,
            "A napló kijelölése egyszerre több azonos questsort jelölt.");
        var result = new QuestJournalWindow.Result(AbandonedQuestId: second.Key);
        Require(fixture.Manager.TryGetQuest(result.AbandonedQuestId!.Value, out var chosen), "A kijelölt futás elveszett.");
        chosen.Abandon();
        Require(first.IsActive && second.IsFailed && journal[first.Key].Status == QuestJournalStatus.Active &&
            journal[second.Key].Status == QuestJournalStatus.Abandoned, "A feladás másik NPC futását is érintette.");
        var before = fixture.Manager.ExportState();
        Require(!fixture.Manager.TryGetQuest(new(definition.Id, new(999)), out _) &&
            !fixture.Manager.TryGetQuest(new(definition.Id), out _) && before.SequenceEqual(fixture.Manager.ExportState()),
            "Egy lejárt vagy hiányos kiválasztás új questállapotot hozott létre.");
        maze.RemoveWorldNpc(firstNpc);
        fixture.Inventory[((QuestObjective.CollectItem)definition.Objective).Item] = 1;
        fixture.Manager.SynchronizeCollectQuests();
        Require(journal[first.Key].Progress == 1 && journal[first.Key].QuestGiverName == firstNpc.Character.Name,
            "Az eltűnt NPC aktív futása elveszett, vagy a megőrzött globális eseménykezelés megváltozott.");
    }

    public static void TravelTargetsOneInstanceAndRechecksWorld()
    {
        var definition = Define(new QuestObjective.DisarmTraps(1), QuestId.MonsterHunterGoblinHunt,
            QuestNpcId.MonsterHunter, QuestScope.PerNpcInstance);
        var fixture = new QuestTestFixture(definition);
        var (maze, registry, world) = World();
        var firstNpc = Add(maze, fixture.SelectedCharacter, "NPC002", 2);
        var secondNpc = Add(maze, fixture.Companion, "NPC002", 4);
        var first = fixture.Manager.Activate(definition.Id, registry.GetOrCreate(firstNpc));
        var second = fixture.Manager.Activate(definition.Id, registry.GetOrCreate(secondNpc));
        fixture.Manager.RegisterTrapDisarmed();
        var travel = new QuestTravelService(fixture.Manager, world);
        var options = travel.BuildOptions(maze.WorldNpcs, npc => npc == firstNpc ? 1 : 20);
        var selection = options.Single(option => option.Key == second.Key);
        Require(options.Count == 2 && selection.GiverInstanceId == second.GiverInstanceId,
            "A gyorsutazási választás nem különbözteti meg az NPC-ket.");
        Require(travel.TryResolve(selection, maze.WorldNpcs, _ => 40, out var updated, out var npc) &&
            ReferenceEquals(npc, secondNpc) && updated.NeedCost == 4, "Rossz questadó vagy lejárt utazási költség.");
        Require(!travel.TryResolve(selection, maze.WorldNpcs, _ => null, out _, out _),
            "A megszűnt útvonalon is elindulhatott az utazás.");
        fixture.Manager.TryGetQuest(updated.Key, out var selected);
        selected.Complete();
        Require(first.IsReadyToTurnIn && second.IsCompleted && fixture.StoredRewards.Count == 1,
            "A kiválasztott futás leadása a másik NPC-nek is jutalmazott.");
        Require(!travel.TryResolve(selection, maze.WorldNpcs, _ => 1, out _, out _), "A lezárt quest régi opciója érvényes maradt.");
        var firstOption = options.Single(option => option.Key == first.Key);
        maze.RemoveWorldNpc(firstNpc);
        Require(!travel.TryResolve(firstOption, maze.WorldNpcs, _ => 1, out _, out _),
            "Az eltávozott NPC-t másik, azonos típusú questadó helyettesítette.");
    }

    public static void TravelRechecksCollectInventory()
    {
        var definition = Define(new QuestObjective.CollectItem(Supplies, 2), scope: QuestScope.PerNpcInstance);
        var fixture = new QuestTestFixture(definition);
        fixture.Inventory[Supplies] = 2;
        var (maze, registry, world) = World();
        var npc = Add(maze, fixture.Companion, "NPC001", 2);
        var quest = fixture.Manager.Activate(definition.Id, registry.GetOrCreate(npc));
        var travel = new QuestTravelService(fixture.Manager, world);
        var selection = travel.BuildOptions(maze.WorldNpcs, _ => 10).Single();
        fixture.Inventory[Supplies] = 1;
        Require(!travel.TryResolve(selection, maze.WorldNpcs, _ => 10, out _, out _) && quest.IsActive &&
            quest.Progress == 1 && fixture.ConsumptionAttempts == 0 && fixture.StoredRewards.Count == 0,
            "A lejárt collect-opció átment a végrehajtási ellenőrzésen vagy mellékhatást okozott.");
    }

    public static void GlobalTravelRetainsConcreteGiver()
    {
        var definition = Define(new QuestObjective.DisarmTraps(1), QuestId.EliraRescue, QuestNpcId.EliraSilverbranch);
        var fixture = new QuestTestFixture(definition);
        var (maze, _, world) = World();
        var original = Add(maze, fixture.SelectedCharacter, "NPC020", 2);
        fixture.Manager.Activate(definition.Id);
        fixture.Manager.RegisterTrapDisarmed();
        var travel = new QuestTravelService(fixture.Manager, world);
        var selection = travel.BuildOptions(maze.WorldNpcs, _ => 2).Single();
        Require(selection.Key.GiverInstanceId.IsNone && !selection.GiverInstanceId.IsNone,
            "A globális futáskulcs összekeveredett a fizikai célpont azonosságával.");
        maze.RemoveWorldNpc(original);
        Add(maze, fixture.Companion, "NPC020", 2);
        Require(!travel.TryResolve(selection, maze.WorldNpcs, _ => 2, out _, out _),
            "A globális quest régi utazási opciója másik karaktert választott.");
    }

    public static void SeparateHistorySurvivesSaveWithoutNpc()
    {
        var data = LoadData();
        var definition = new QuestCatalogBuilder(data).Build().Get(QuestId.MonsterHunterGoblinHunt);
        var fixture = new QuestTestFixture(definition);
        var (maze, registry, world) = World();
        var first = registry.GetOrCreate(Add(maze, fixture.SelectedCharacter, "NPC002", 2));
        var second = registry.GetOrCreate(Add(maze, fixture.Companion, "NPC002", 4));
        fixture.Manager.RestoreState([
            new(definition.Id, first, QuestState.Completed, definition.Objective.RequiredCount, 1),
            new(definition.Id, second, QuestState.Completed, definition.Objective.RequiredCount, 1)]);
        var journal = new Dictionary<QuestKey, QuestJournalEntrySnapshot>();
        var coordinator = new NpcQuestCoordinator(data, fixture.Manager, world);
        fixture.Manager.QuestChanged += quest => coordinator.SynchronizeQuestJournal(journal, quest);
        fixture.Manager.PublishState();
        var firstKey = new QuestKey(definition.Id, first);
        var secondKey = new QuestKey(definition.Id, second);
        journal[firstKey] = journal[firstKey] with { CompletionExperienceSummary = "első jutalom" };
        journal[secondKey] = journal[secondKey] with { CompletionExperienceSummary = "második jutalom" };
        // A napló állapota nem tekinthető bemenetnek a mentéskor sem.
        journal[firstKey] = journal[firstKey] with { Status = QuestJournalStatus.Active, Progress = 0 };
        var save = new GameSaveData { Quests = new QuestSaveAdapter(data, registry).Export(fixture.Manager, journal.Values) };
        save = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(save))!;
        var restored = new QuestTestFixture(definition);
        var adapter = new QuestSaveAdapter(data, new());
        restored.Manager.RestoreState(adapter.PrepareRestore(save, new CharacterRoster()));
        var history = adapter.CreateJournal(restored.Manager).ToDictionary(entry => entry.Key);
        Require(history.Count == 2 && history[firstKey].Status == QuestJournalStatus.Completed &&
            history[firstKey].CompletionExperienceSummary == "első jutalom" &&
            history[secondKey].CompletionExperienceSummary == "második jutalom" &&
            history[secondKey].QuestGiverName == fixture.Companion.Name && restored.StoredRewards.Count == 0,
            "Az NPC nélküli mentés összekeverte a két futás történetét vagy visszaírt a naplóból.");
    }

    public static void JournalKeysSurviveFullDeltaAndReconnect()
    {
        var fixture = new QuestTestFixture();
        var party = new Party();
        party.SetLeader(fixture.SelectedCharacter);
        var session = new GameSession(party, fixture.SelectedCharacter);
        QuestJournalEntrySnapshot Entry(int instance) => new(new(QuestId.MonsterHunterGoblinHunt, new(instance)),
            "Vadászat", "Két külön futás", "Vadász", QuestJournalStatus.Active, 1, 3, 20);
        var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Teszt", new Dictionary<CharacterId, Position>())) with
        {
            SnapshotSequence = 1, QuestJournal = [Entry(1), Entry(2)],
            World = new(WorldId.New(), 7, 7, null, null, [], [], [], [], [], [])
        };
        var publisher = new SessionReplicationPublisher();
        var first = publisher.CreateFrame(session.HostPlayerId, snapshot);
        var copy = JsonSerializer.Deserialize<SessionReplicationFrame>(JsonSerializer.Serialize(first))!;
        Require(copy.Session.QuestJournal!.Select(entry => entry.Key).SequenceEqual(snapshot.QuestJournal.Select(entry => entry.Key)),
            "A teljes snapshot elvesztette a per-NPC kulcsokat.");
        Require(publisher.TryAcknowledge(session.HostPlayerId, 1, out _), "A snapshot nem nyugtázható.");
        var next = snapshot with { SnapshotSequence = 2,
            QuestJournal = [Entry(1), Entry(2) with { Status = QuestJournalStatus.Completed, Progress = 3 }] };
        var delta = publisher.CreateFrame(session.HostPlayerId, next);
        var deltaCopy = JsonSerializer.Deserialize<SessionReplicationFrame>(JsonSerializer.Serialize(delta))!;
        Require(deltaCopy.Kind == SessionReplicationFrameKind.Delta && deltaCopy.Session.QuestJournal![0].Status == QuestJournalStatus.Active &&
            deltaCopy.Session.QuestJournal[1].Key == Entry(2).Key && deltaCopy.Session.QuestJournal[1].Status == QuestJournalStatus.Completed,
            "A delta naplóadata másik futásra helyezte a lezárást.");
        publisher.RemoveClient(session.HostPlayerId);
        var reconnect = publisher.CreateFrame(session.HostPlayerId, next with { SnapshotSequence = 3 });
        Require(reconnect.Kind == SessionReplicationFrameKind.FullSnapshot && reconnect.Session.QuestJournal!.Count == 2,
            "Az újracsatlakozás nem adta vissza mindkét futást.");
        var json = JsonSerializer.Serialize(Entry(2), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Require(json.Contains("NPCQ002") && JsonSerializer.Deserialize<QuestJournalEntrySnapshot>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Key == Entry(2).Key,
            "A webes JSON-beállítások nem stabil szöveges kulcsot visznek át.");
        foreach (var bad in new[] { json.Replace("NPCQ002", "UNKNOWN"), json.Replace("\"GiverInstanceId\":2", "\"GiverInstanceId\":-1"), "{}" })
        {
            try { JsonSerializer.Deserialize<QuestJournalEntrySnapshot>(bad, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { continue; }
            throw new InvalidOperationException("A hibás hálózati questkulcs elfogadásra került.");
        }
    }

    private static (Maze Maze, QuestNpcInstanceRegistry Registry, MazeQuestWorldContext World) World()
    {
        var maze = new Maze(9, 9);
        var registry = new QuestNpcInstanceRegistry();
        return (maze, registry, new(() => maze, _ => 0, (_, _) => false, registry, _ => false));
    }
    private static WorldNpc Add(Maze maze, LiveCharacter character, string id, int x)
    {
        var npc = new WorldNpc(new(x, 2), id, character, NpcDisposition.Neutral, false, true, "");
        maze.Carve(npc.Position);
        maze.AddWorldNpc(npc);
        return npc;
    }
    private static GameDataCatalog LoadData() => CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
