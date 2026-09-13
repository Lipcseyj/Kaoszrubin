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

internal static class QuestPersistenceTests
{
    public static void RuntimeRoundTripDoesNotReplayGameplay()
    {
        var definition = Define(new QuestObjective.DisarmTraps(3), scope: QuestScope.PerNpcInstance);
        var fixture = new QuestTestFixture(definition);
        QuestStateSnapshot Snapshot(int instance, QuestState state, int progress, int completions = 0) =>
            new(definition.Id, new(instance), state, progress, completions);
        fixture.Manager.RestoreState([
            Snapshot(1, QuestState.Active, 1), Snapshot(2, QuestState.ReadyToTurnIn, 3),
            Snapshot(3, QuestState.Completed, 3, 1), Snapshot(4, QuestState.Failed, 2), Snapshot(5, QuestState.Locked, 0)]);
        var saved = fixture.Manager.ExportState();
        var restored = new QuestTestFixture(definition);
        restored.Manager.RestoreState(saved);
        Require(saved.SequenceEqual(restored.Manager.ExportState()), "Az állapotok nem fordultak körbe.");
        Require(restored.Manager.GetQuest(definition.Id, new(5)).IsAvailable,
            "Az Offered megfelelője nem értékelődött újra, vagy automatikusan elindult.");
        Reject(() => restored.Manager.GetQuest(definition.Id, new(3)).Complete());
        Require(restored.ConsumptionAttempts == 0 && restored.StoredRewards.Count == 0 &&
            restored.RandomRewardRolls == 0 && restored.SelectedCharacter.Experience == 0,
            "A betöltés vagy egy korábban teljesített quest újbóli leadása mellékhatással járt.");
        restored.Manager.GetQuest(definition.Id, new(1)).Abandon();
        Require(saved[0].State == QuestState.Active, "Az export élő, utólag módosuló állapotot adott vissza.");
    }

    public static void InvalidRuntimeImportIsAtomic()
    {
        var definition = Define(new QuestObjective.DisarmTraps(3));
        var store = new QuestStateStore(new QuestCatalog([definition]));
        var valid = new QuestStateSnapshot(definition.Id, default, QuestState.Active, 1, 0);
        store.Restore([valid]);
        var original = store.Get(definition.Id);
        foreach (var invalid in new[]
        {
            valid with { QuestId = (QuestId)99999 }, valid with { GiverInstanceId = new(1) },
            valid with { Progress = -1 }, valid with { Progress = 3 },
            valid with { State = QuestState.ReadyToTurnIn }, valid with { CompletionCount = 1 },
            valid with { State = QuestState.Completed, Progress = 3 }, valid with { State = (QuestState)999 }
        })
        {
            Reject(() => store.Restore([valid with { Progress = 2 }, invalid]));
            Require(store.Export().SequenceEqual([valid]) && ReferenceEquals(original, store.Get(definition.Id)),
                "A hibás import részben módosította az élő állapotot.");
        }
        Reject(() => store.Restore([valid, valid]));
        store.Restore([valid with { Progress = 2 }]);
        Require(original.Progress == 2, "A meglévő handle mögötti objektum nem frissült.");
        store.Restore([]);
        Require(store.Export().Count == 0, "A teljes importban nem szereplő régi futás továbbra is mentődik.");
    }

    public static void CollectRestoreUsesLoadedInventory()
    {
        var definition = Define(new QuestObjective.CollectItem(Supplies, 3), scope: QuestScope.PerNpcInstance);
        var fixture = new QuestTestFixture(definition);
        fixture.Inventory[Supplies] = 1;
        var notifications = new List<QuestState>();
        fixture.Manager.QuestChanged += quest => notifications.Add(quest.State);
        fixture.Manager.RestoreState([
            new(definition.Id, new(1), QuestState.ReadyToTurnIn, 3, 0),
            new(definition.Id, new(2), QuestState.Completed, 3, 1)]);
        Require(fixture.Manager.GetQuest(definition.Id, new(1)).Progress == 1 &&
            fixture.Manager.GetQuest(definition.Id, new(1)).IsActive &&
            fixture.Manager.GetQuest(definition.Id, new(2)).IsCompleted &&
            !notifications.Contains(QuestState.ReadyToTurnIn), "A mentett collect-progress felülírta a betöltött készletet.");
        Require(fixture.Inventory[Supplies] == 1 && fixture.ConsumptionAttempts == 0 && fixture.StoredRewards.Count == 0,
            "A collect visszaállítása fogyasztott vagy jutalmazott.");
    }

    public static void RegistryRetainsIdentityAcrossWorldObjects()
    {
        var fixture = new QuestTestFixture();
        var registry = new QuestNpcInstanceRegistry();
        WorldNpc Npc(LiveCharacter character) => new(new(2, 2), "NPC002", character,
            NpcDisposition.Neutral, false, true, "");
        var first = registry.GetOrCreate(Npc(fixture.SelectedCharacter));
        var second = registry.GetOrCreate(Npc(fixture.Companion));
        Require(first != second && registry.GetOrCreate(Npc(fixture.SelectedCharacter)) == first,
            "Két NPC összeolvadt, vagy ugyanaz a karakter új objektumként más azonosítót kapott.");
        var restored = new QuestNpcInstanceRegistry();
        restored.Import(registry.Export());
        restored.Restore(Npc(fixture.SelectedCharacter), first);
        Reject(() => restored.Restore(Npc(fixture.Companion), first));
        Reject(() => restored.Import([registry.Export()[0], registry.Export()[0]]));
        Require(restored.Export().SequenceEqual(registry.Export()), "A hibás registry-import nem volt atomi.");
        var newcomer = restored.GetOrCreate(Npc(new QuestTestFixture().SelectedCharacter));
        Require(newcomer.Value > second.Value, "Egy már eltávozott NPC azonosítója újra felhasználódott.");
    }

    public static void LegacySaveRoundTripsSeparateNpcStatesAndArchive()
    {
        var data = LoadData();
        var roster = Roster(6);
        var definition = new QuestCatalogBuilder(data).Build().Get(QuestId.MonsterHunterGoblinHunt);
        var count = definition.Objective.RequiredCount;
        var save = EmptySave();
        save.Version = 21;
        save.Maze.Npcs = [
            LegacyNpc(1, "NPC002", ("NPCQ002", 1, 1)), LegacyNpc(2, "NPC002", ("NPCQ002", 1, count)),
            LegacyNpc(3, "NPC002", ("NPCQ002", 2, count)), LegacyNpc(4, "NPC002", ("NPCQ002", 3, 1)),
            LegacyNpc(5, "NPC002", ("NPCQ002", 0, 0))];
        save.QuestJournal = [new("NPCQ002", QuestJournalStatus.Completed, count, 20),
            new("NPCQ037", QuestJournalStatus.Completed, 999, 123, "régi XP", "régi tárgy"),
            new("UNKNOWN", QuestJournalStatus.Completed, 1, 1)];
        GameSaveFormat.MigrateToCurrent(save);
        var registry = new QuestNpcInstanceRegistry();
        var adapter = new QuestSaveAdapter(data, registry);
        var fixture = new QuestTestFixture(new QuestCatalogBuilder(data).Build().All.ToArray());
        fixture.Manager.RestoreState(adapter.PrepareRestore(save, roster));
        var states = fixture.Manager.ExportState().Where(state => state.QuestId == definition.Id).ToArray();
        Require(states.Select(state => state.State).SequenceEqual([
            QuestState.Active, QuestState.ReadyToTurnIn, QuestState.Completed, QuestState.Failed, QuestState.Available]),
            "Az import összeolvasztotta a külön NPC-k állapotát vagy automatikusan aktivált.");
        Require(save.Quests!.LegacyJournalArchive.Count == 2 && save.Quests.MigrationNotes.Count >= 2,
            "Az ismeretlen vagy nem egyértelmű naplóbejegyzés elveszett.");
        var history = adapter.CreateJournal(fixture.Manager).Single(entry => entry.QuestId == "NPCQ037");
        Require(history.CompletionExperienceSummary == "régi XP" && history.CompletionItemRewardSummary == "régi tárgy",
            "Az NPC nélkül fennmaradt globális jutalomtörténet elveszett.");
        save.Quests = adapter.Export(fixture.Manager);
        var reloaded = Clone(save);
        var nextAdapter = new QuestSaveAdapter(data, new());
        var nextFixture = new QuestTestFixture(new QuestCatalogBuilder(data).Build().All.ToArray());
        var characters = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-quest-roster.json"), data);
        var loadedRoster = characters.Deserialize(characters.Serialize(roster));
        Require(!ReferenceEquals(roster.Characters[1], loadedRoster.Characters[1]) &&
            roster.Characters.Select(character => character.Id).SequenceEqual(loadedRoster.Characters.Select(character => character.Id)),
            "A karaktermentés nem őrizte meg a questadók tartós azonosságát.");
        nextFixture.Manager.RestoreState(nextAdapter.PrepareRestore(reloaded, loadedRoster));
        Require(fixture.Manager.ExportState().SequenceEqual(nextFixture.Manager.ExportState()) &&
            nextAdapter.Export(nextFixture.Manager).LegacyJournalArchive.Count == 2,
            "A régi → új → új mentési kör nem őrizte az állapotot és az archívumot.");
        Require(nextFixture.StoredRewards.Count == 0 && nextFixture.ConsumptionAttempts == 0 &&
            nextFixture.SelectedCharacter.Experience == 0, "A migrált mentés betöltése jutalmazott.");
    }

    public static void LegacyTerminalConflictsNeverReactivate()
    {
        var data = LoadData();
        var roster = Roster(2);
        var save = EmptySave();
        save.Maze.Npcs = [LegacyNpc(1, "NPC002", ("NPCQ002", 1, 1))];
        save.QuestJournal = [new("NPCQ002", QuestJournalStatus.Abandoned, 1, 20),
            new("NPCQ039", QuestJournalStatus.Active, 0, 20)];
        save.SuspendedCampaign = EmptySave();
        save.SuspendedCampaign.Maze.Npcs = [LegacyNpc(1, "NPC002", ("NPCQ002", 2, 0))];
        save.SuspendedCampaign.QuestJournal = [new("NPCQ039", QuestJournalStatus.Abandoned, 0, 20)];
        var states = new QuestSaveAdapter(data, new()).PrepareRestore(save, roster);
        Require(states.Single(state => state.QuestId == QuestId.MonsterHunterGoblinHunt).State == QuestState.Completed &&
            states.Single(state => state.QuestId == QuestId.RodericOathbreakerKnight).State == QuestState.Failed,
            "A régi napló vagy felfüggesztett világ újraaktivált egy terminális questet.");
        Require(save.Maze.Npcs[0].QuestInstanceId == save.SuspendedCampaign.Maze.Npcs[0].QuestInstanceId &&
            save.Quests!.NpcIdentities.Count == 1, "Ugyanaz a karakter két snapshotban két questadóvá vált.");
    }

    public static void TypedSaveRejectsCorruptionWithoutLegacyFallback()
    {
        var data = LoadData();
        var roster = Roster(2);
        var save = EmptySave();
        save.Maze.Npcs = [LegacyNpc(1, "NPC002", ("NPCQ002", 1, 1))];
        var registry = new QuestNpcInstanceRegistry();
        var adapter = new QuestSaveAdapter(data, registry);
        adapter.PrepareRestore(save, roster);
        // A legacy napló szándékosan ellentmond a hiteles típusos blokknak.
        save.QuestJournal = [new("NPCQ002", QuestJournalStatus.Completed, 999, 20)];
        Require(adapter.PrepareRestore(Clone(save), roster).Single().State == QuestState.Active,
            "Az új mentés állapotát felülírta a legacy napló.");
        var oldIdentities = registry.Export();
        foreach (var corrupt in new Action<GameSaveData>[]
        {
            s => s.Quests!.States[0] = s.Quests.States[0] with { State = "123" },
            s => s.Quests!.States.Add(s.Quests.States[0]),
            s => s.Quests!.NpcIdentities.Clear(),
            s => s.Quests!.States[0] = s.Quests.States[0] with { GiverInstanceId = 0 },
            s => s.Quests!.States[0] = s.Quests.States[0] with { GiverInstanceId = 999 },
            s => s.Quests!.NpcIdentities[0] = s.Quests.NpcIdentities[0] with { NpcId = "NPC001" },
            s => s.Maze.Npcs[0] = s.Maze.Npcs[0] with { CharacterId = Guid.NewGuid() },
            s => s.Maze.Npcs.Add(s.Maze.Npcs[0])
        })
        {
            var broken = Clone(save);
            corrupt(broken);
            Reject(() => adapter.PrepareRestore(broken, roster));
            Require(registry.Export().SequenceEqual(oldIdentities), "A hibás mentés átírta a meglévő NPC-regisztert.");
        }
    }

    public static void SuspendedWorldRetainsCurrentQuestStateAndFollowerIdentity()
    {
        var data = LoadData();
        var roster = Roster(2);
        var registry = new QuestNpcInstanceRegistry();
        var adapter = new QuestSaveAdapter(data, registry);
        var save = EmptySave();
        var follower = LegacyNpc(1, "NPC021", ("NPCQ039", 1, 0)) with
        { State = WorldNpcState.Following, StoryId = "RODERIC", StoryStateId = "MALREC_APPROACH" };
        save.LocationKind = AdventureLocationKind.Quest;
        save.Maze.PartyAvatars = [new(new(2, 2), 1, follower)];
        save.SuspendedCampaign = EmptySave();
        save.SuspendedCampaign.Maze.PartyAvatars = [new(new(2, 2), 1, follower)];
        var fixture = new QuestTestFixture(new QuestCatalogBuilder(data).Build().All.ToArray());
        fixture.Manager.RestoreState(adapter.PrepareRestore(save, roster));
        var mapper = new GameStateMapper(data, roster, roster.SelectedCharacter!, registry);
        var questWorld = mapper.Restore(save);
        var firstNpc = questWorld.Maze.PartyMembers.Single().TemporaryFollower!;
        var firstId = registry.GetOrCreate(firstNpc);
        // A külön helyszínen már teljesült; a kampánypillanatkép még a korábbi állapotot őrzi.
        var completed = fixture.Manager.ExportState().Single() with
        { State = QuestState.Completed, Progress = 1, CompletionCount = 1 };
        fixture.Manager.RestoreState([completed]);
        adapter.RecordCompletion(fixture.Manager.GetQuest(completed.QuestId), "megtartott XP", "megtartott tárgy");
        save.Quests = adapter.Export(fixture.Manager);
        var reloaded = Clone(save);
        var restoredRegistry = new QuestNpcInstanceRegistry();
        var restoredAdapter = new QuestSaveAdapter(data, restoredRegistry);
        var restoredFixture = new QuestTestFixture(new QuestCatalogBuilder(data).Build().All.ToArray());
        restoredFixture.Manager.RestoreState(restoredAdapter.PrepareRestore(reloaded, roster));
        var restoredMapper = new GameStateMapper(data, roster, roster.SelectedCharacter!, restoredRegistry);
        var returned = restoredMapper.Restore(reloaded.SuspendedCampaign!);
        var secondNpc = returned.Maze.PartyMembers.Single().TemporaryFollower!;
        restoredFixture.Manager.QuestChanged += quest => LegacyQuestProgressProjection.Synchronize(secondNpc, quest);
        restoredFixture.Manager.SynchronizeCollectQuests();
        restoredFixture.Manager.PublishState();
        Require(!ReferenceEquals(firstNpc, secondNpc) && restoredRegistry.GetOrCreate(secondNpc) == firstId &&
            restoredFixture.Manager.ExportState().Single() == completed,
            "A kampány visszaállítása megváltoztatta a követő azonosságát vagy visszatekerte a questet.");
        Require(restoredAdapter.CreateJournal(restoredFixture.Manager).Single().CompletionExperienceSummary == "megtartott XP" &&
            restoredFixture.StoredRewards.Count == 0, "A visszatérés elvesztette vagy újra kiosztotta a jutalmat.");
        var savedAgain = restoredMapper.Create(1, returned.Maze, returned.Player, returned.FogOfWar,
            Direction.Right, [], false, false, false, false, null, DateTime.UtcNow, new Dictionary<Enemy, DateTime>(), [], []);
        Require(savedAgain.Maze.PartyAvatars.Single().TemporaryFollower!.QuestInstanceId == firstId.Value,
            "A visszatért követő újramentése elvesztette az azonosítót.");
        // A pillanatkép még tartalmazhat egy közben végleg eltávozott követőt; nem támadhat fel.
        reloaded.Maze.PartyAvatars.Clear();
        roster.Remove(roster.Characters[1]);
        var afterDeparture = restoredAdapter.PrepareRestore(reloaded, roster);
        var withoutFollower = restoredMapper.Restore(reloaded.SuspendedCampaign!, skipDepartedNpcCharacters: true);
        Require(withoutFollower.Maze.PartyMembers.Count == 0 && afterDeparture.Single() == completed,
            "Az eltávozott követő visszatért, vagy a hiánya elvesztette a lezárt questtörténetet.");
    }

    public static void PersistedCharacterIdSurvivesRosterReordering()
    {
        var data = LoadData();
        var roster = Roster(3);
        var save = EmptySave();
        save.Maze.Npcs = [LegacyNpc(2, "NPC002", ("NPCQ002", 1, 1))];
        var registry = new QuestNpcInstanceRegistry();
        new QuestSaveAdapter(data, registry).PrepareRestore(save, roster);
        var originalId = roster.Characters[2].Id;
        roster.Remove(roster.Characters[1]);
        var restored = new GameStateMapper(data, roster, roster.SelectedCharacter!, registry).Restore(save);
        Require(restored.Maze.WorldNpcs.Single().Character.Id == originalId,
            "Az elmozdult karakterindex miatt az NPC más karakterhez került vagy kimaradt.");
    }

    public static void WorldRestoreUsesOnlyTypedProgress()
    {
        var data = LoadData();
        var roster = Roster(2);
        var save = EmptySave();
        save.Maze.Npcs = [LegacyNpc(1, "NPC002", ("NPCQ002", 1, 1))];
        var registry = new QuestNpcInstanceRegistry();
        var adapter = new QuestSaveAdapter(data, registry);
        var states = adapter.PrepareRestore(save, roster);
        var original = save.Maze.Npcs[0];
        save.Maze.Npcs[0] = LegacyNpc(1, "NPC002", ("NPCQ002", 2, 99)) with
        { CharacterId = original.CharacterId, QuestInstanceId = original.QuestInstanceId, QuestIds = [] };
        var mapper = new GameStateMapper(data, roster, roster.SelectedCharacter!, registry);
        var world = mapper.Restore(save);
        var fixture = new QuestTestFixture(new QuestCatalogBuilder(data).Build().All.ToArray());
        fixture.Manager.QuestChanged += quest => LegacyQuestProgressProjection.Synchronize(world.Maze.WorldNpcs.Single(), quest);
        fixture.Manager.RestoreState(states);
        var projected = mapper.Create(1, world.Maze, world.Player, world.FogOfWar, Direction.Right,
            [], false, false, false, false, null, DateTime.UtcNow, new Dictionary<Enemy, DateTime>(), [], []);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(projected));
        var questProgress = json.RootElement.GetProperty("Maze").GetProperty("Npcs")[0].GetProperty("Quests")
            .EnumerateArray().Single(q => q.GetProperty("QuestId").GetString() == "NPCQ002");
        Require(questProgress.GetProperty("State").GetInt32() == 1 && questProgress.GetProperty("Progress").GetInt32() == 1,
            "A világ flat questnézete eltér a típusos állapottól, vagy a hiányzó legacy questlista elnyelte a projekciót.");
    }

    public static void LegacyNpcRecordTakesPrecedenceOverUnboundJournal()
    {
        var data = LoadData();
        var roster = Roster(2);
        var save = EmptySave();
        save.Maze.Npcs = [LegacyNpc(1, "NPC002", ("NPCQ002", 1, 1))];
        save.QuestJournal = [new("NPCQ002", QuestJournalStatus.Active, 999, 20),
            new("NPCQ002", QuestJournalStatus.Completed, 999, 20)];
        var states = new QuestSaveAdapter(data, new()).PrepareRestore(save, roster);
        Require(states.Single().State == QuestState.Active && states.Single().Progress == 1 &&
            save.Quests!.LegacyJournalArchive.Single().Status == QuestJournalStatus.Completed,
            "A példányhoz nem kötött napló felülírta az NPC saját rekordját.");
    }

    public static void VersionMigrationIncludesSuspendedCampaign()
    {
        var save = new GameSaveData { Version = 21, SuspendedCampaign = new() { Version = 12 } };
        GameSaveFormat.MigrateToCurrent(save);
        Require(save.Version == 22 && save.SuspendedCampaign.Version == 22 &&
            save.Quests is null && save.SuspendedCampaign.Quests is null,
            "A felfüggesztett mentés verziója vagy az egyszeri legacy import jelölése hibás.");
        GameSaveFormat.MigrateToCurrent(save);
        Require(save.Quests is null, "Az ismételt formátummigráció elvesztette a pending legacy importot.");
        Reject(() => GameSaveFormat.MigrateToCurrent(new() { Version = 23 }));
    }

    private static GameSaveData EmptySave() => new()
    {
        PlayerPosition = new(1, 1),
        Maze = new() { Width = 9, Height = 9, Exit = new(7, 7),
            TileCodePoints = Enumerable.Repeat(Maze.Floor.Value, 81).ToList() }
    };

    // Valódi régi wire-alak: a questállapot számai a 21-es séma Offered/Active/Completed/Abandoned értékei.
    private static WorldNpcSaveData LegacyNpc(int characterIndex, string npcId,
        params (string QuestId, int State, int Progress)[] quests) => JsonSerializer.Deserialize<WorldNpcSaveData>(
        JsonSerializer.Serialize(new
        {
            Position = new Position(characterIndex + 1, 3), DefinitionId = npcId, CharacterIndex = characterIndex,
            IsQuestNpc = true, Dialogue = "", QuestIds = quests.Select(q => q.QuestId).ToArray(),
            Quests = quests.Select(q => new { q.QuestId, q.State, q.Progress }).ToArray()
        }))!;

    private static CharacterRoster Roster(int count)
    {
        var roster = new CharacterRoster();
        for (var i = 0; i < count; i++) roster.Add(new QuestTestFixture().SelectedCharacter);
        roster.Select(roster.Characters[0]);
        return roster;
    }

    private static GameSaveData Clone(GameSaveData save) => JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(save))!;
    private static GameDataCatalog LoadData() => CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException or KeyNotFoundException) { return; }
        throw new InvalidOperationException("Az érvénytelen művelet nem lett elutasítva.");
    }
}
