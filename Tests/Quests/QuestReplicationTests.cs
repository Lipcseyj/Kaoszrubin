using System.Text.Json;
using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.World;
using static KaoszRubin.Tests.Quests.QuestTestFixture;

namespace KaoszRubin.Tests.Quests;

internal static class QuestReplicationTests
{
    public static void WorldUsesTypedStatesAndStableKeys()
    {
        var setup = new Setup();
        setup.Fixture.Manager.RestoreState([
            new(setup.Definition.Id, setup.FirstId, QuestState.Active, 1, 0),
            new(setup.Definition.Id, setup.SecondId, QuestState.Completed, 3, 1)]);
        var world = setup.Project();
        var json = JsonSerializer.Serialize(world, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<WorldSnapshot>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var first = restored.Npcs!.Single(npc => npc.EntityId == setup.First.Id);
        var second = restored.Npcs!.Single(npc => npc.EntityId == setup.Second.Id);
        Require(first.QuestInstanceId == setup.FirstId.Value && first.Quests!.Single() is
            { State: QuestState.Active, Progress: 1 } && first.Quests!.Single().Key.GiverInstanceId == setup.FirstId &&
            second.Quests!.Single() is { State: QuestState.Completed, CompletionCount: 1, Progress: 3 },
            "A világ a flat nézetből dolgozott vagy elvesztette a példánykulcsot/lezárást.");
        Require(json.Contains("NPCQ002") && json.Contains("Completed") && !json.Contains("questIds") &&
            setup.Fixture.StoredRewards.Count == 0, "A wire-adat sorszámos/legacy quest-ID-t vagy mellékhatást használt.");
        var rejected = false;
        try { WorldSnapshotProjector.Create(setup.Maze, setup.Fog); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "A látható questadóhoz típusos forrás nélkül is készült hiányos pillanatkép.");
    }

    public static void DeltaChangesOnlyTheAffectedNpc()
    {
        var setup = new Setup();
        var first = setup.Fixture.Manager.Activate(setup.Definition.Id, setup.FirstId);
        setup.Fixture.Manager.Activate(setup.Definition.Id, setup.SecondId);
        var baseline = setup.Project();
        var same = JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(setup.Project()))!;
        Require(WorldDeltaProjector.Create(1, baseline, 2, same).IsEmpty,
            "Új listapéldányok változatlan NPC-állapotnál is deltát generálnak.");
        first.Abandon();
        var current = setup.Project();
        var delta = WorldDeltaProjector.Create(1, baseline, 3, current);
        Require(delta.NpcUpserts!.Count == 1 && delta.NpcUpserts[0].EntityId == setup.First.Id &&
            delta.NpcUpserts[0].Quests!.Single().State == QuestState.Failed,
            "A feladás a másik questadót is módosította vagy nem került a deltába.");
        var reduced = WorldDeltaReducer.Apply(baseline, delta);
        Require(reduced.Npcs!.Single(npc => npc.EntityId == setup.Second.Id).Quests!.Single().State == QuestState.Active &&
            WorldDeltaProjector.Create(3, reduced, 4, current).IsEmpty, "A reducer nem a host állapotát állította elő.");
    }

    public static void VisibilityAndFollowerPositionRemainAuthoritative()
    {
        var setup = new Setup();
        var fog = new FogOfWar(9, 9, 0);
        fog.Restore([setup.First.Position], false);
        var calls = new List<WorldEntityId>();
        var visible = WorldSnapshotProjector.Create(setup.Maze, fog, projectQuests: npc =>
        {
            calls.Add(npc.Id);
            return setup.Projector.Create(npc);
        });
        Require(visible.Npcs!.Count == 1 && calls.SequenceEqual([setup.First.Id]),
            "Rejtett NPC adata vagy questlekérdezése átjutott a láthatósági határon.");
        setup.Maze.RemoveWorldNpc(setup.First);
        setup.First.BeginFollowing();
        var followerPosition = new Position(5, 5);
        setup.Maze.Carve(followerPosition);
        var avatar = new PartyMemberAvatar(followerPosition, setup.First.Character, setup.First);
        setup.Maze.AddPartyMember(avatar);
        Require(WorldSnapshotProjector.Create(setup.Maze, fog, projectQuests: setup.Projector.Create).Npcs!.Count == 0,
            "A követő régi NPC-pozíciója láthatóként szivárgott át.");
        fog.Restore([followerPosition], false);
        var follower = WorldSnapshotProjector.Create(setup.Maze, fog, projectQuests: setup.Projector.Create).Npcs!.Single();
        Require(follower.Position == followerPosition && follower.EntityId == setup.First.Id &&
            follower.QuestInstanceId == setup.FirstId.Value, "A követővé válás megváltoztatta az azonosságot vagy pozíciót.");
    }

    public static void ReplicationAndReconnectNeverReplayCompletion()
    {
        var setup = new Setup();
        setup.Fixture.Manager.RestoreState([
            new(setup.Definition.Id, setup.FirstId, QuestState.ReadyToTurnIn, 3, 0),
            new(setup.Definition.Id, setup.SecondId, QuestState.Active, 1, 0)]);
        var party = new Party();
        party.SetLeader(setup.Fixture.SelectedCharacter);
        var session = new GameSession(party, setup.Fixture.SelectedCharacter);
        var recipient = PlayerId.New();
        var publisher = new SessionReplicationPublisher();
        var store = new ClientSessionStore(recipient);
        var tracker = new QuestJournalNotificationTracker();
        var completedNotifications = 0;
        store.SnapshotChanged += snapshot => completedNotifications += tracker.Observe(snapshot.QuestJournal ?? []).Completions.Count;
        SessionSnapshot Snapshot(long sequence)
        {
            var world = setup.Project();
            var journal = world.Npcs!.SelectMany(npc => npc.Quests!.Select(quest => new QuestJournalEntrySnapshot(
                quest.Key, "Vadászat", "Leírás", npc.Name,
                quest.State == QuestState.Completed ? QuestJournalStatus.Completed : QuestJournalStatus.Active,
                quest.Progress, quest.RequiredCount, 20))).ToArray();
            return session.CreateSnapshot(new SessionSnapshotContext(1, "Teszt", new Dictionary<CharacterId, Position>(), World: world))
                with { SnapshotSequence = sequence, QuestJournal = journal };
        }
        SessionReplicationFrame Wire(SessionReplicationFrame frame) =>
            JsonSerializer.Deserialize<SessionReplicationFrame>(JsonSerializer.Serialize(frame))!;
        var baseline = Wire(publisher.CreateFrame(recipient, Snapshot(1)));
        Require(store.Apply(baseline).Status == ClientFrameApplyStatus.Applied && completedNotifications == 0,
            "A kezdeti snapshot eseményt játszott vissza.");
        Require(publisher.TryAcknowledge(recipient, 1, out _), "A baseline ACK sikertelen.");
        setup.Fixture.Manager.GetQuest(setup.Definition.Id, setup.FirstId).Complete();
        var delta = Wire(publisher.CreateFrame(recipient, Snapshot(2)));
        Require(delta.Kind == SessionReplicationFrameKind.Delta && store.Apply(delta).Status == ClientFrameApplyStatus.Applied &&
            completedNotifications == 1, "A questlezárás nem pontosan egyszer jutott a vendéghez.");
        Require(store.Apply(delta).Status == ClientFrameApplyStatus.Ignored && completedNotifications == 1,
            "Az ismételt frame megismételte a questértesítést.");
        Require(store.CurrentSnapshot!.World!.Npcs!.Single(npc => npc.EntityId == setup.First.Id).Quests!.Single() is
            { State: QuestState.Completed, CompletionCount: 1 }, "A kliens nem őrizte a questlezárást.");
        publisher.RequestFullSnapshot(recipient);
        store.Apply(Wire(publisher.CreateFrame(recipient, Snapshot(3))));
        Require(completedNotifications == 1, "A teljes resync megismételte az értesítést.");
        var reconnectTracker = new QuestJournalNotificationTracker();
        Require(reconnectTracker.Observe(store.CurrentSnapshot!.QuestJournal!).Completions.Count == 0 &&
            setup.Fixture.StoredRewards.Count == 1, "Az új kapcsolat visszajátszotta a jutalmat vagy értesítést.");
        var incompatible = baseline with { Session = baseline.Session with { ProtocolVersion = SessionProtocol.Version - 1 } };
        Require(new ClientSessionStore(recipient).Apply(incompatible).Status == ClientFrameApplyStatus.Rejected,
            "A régi quest wire-sémájú protokollt elfogadta a kliens.");
    }

    public static void NotificationsDistinguishInstancesAndSkipHistoricalBaseline()
    {
        QuestJournalEntrySnapshot Entry(int instance, QuestJournalStatus status) => new(
            new(QuestId.MonsterHunterGoblinHunt, new(instance)), "Vadászat", "", "Vadász", status, 1, 1, 20);
        var tracker = new QuestJournalNotificationTracker();
        Require(tracker.Observe([Entry(1, QuestJournalStatus.Completed)]).Completions.Count == 0,
            "A kezdeti történetből új értesítés lett.");
        var offers = tracker.Observe([Entry(1, QuestJournalStatus.Completed), Entry(2, QuestJournalStatus.Active)]);
        Require(offers.Offers.Single().Key.GiverInstanceId == new QuestNpcInstanceId(2),
            "Az azonos quest másik NPC-példányának ajánlatát elnyelte a korábbi történet.");
        var completed = tracker.Observe([Entry(1, QuestJournalStatus.Completed), Entry(2, QuestJournalStatus.Completed)]);
        Require(completed.Completions.Single().Key.GiverInstanceId == new QuestNpcInstanceId(2) &&
            tracker.Observe([Entry(2, QuestJournalStatus.Completed)]).Completions.Count == 0,
            "Másik példány lezárása elveszett vagy ismétlődött.");
    }

    private sealed class Setup
    {
        public QuestDefinition Definition { get; } = Define(new QuestObjective.DisarmTraps(3),
            QuestId.MonsterHunterGoblinHunt, QuestNpcId.MonsterHunter, QuestScope.PerNpcInstance);
        public QuestTestFixture Fixture { get; }
        public Maze Maze { get; } = new(9, 9);
        public FogOfWar Fog { get; } = new(9, 9, 0);
        public WorldNpc First { get; }
        public WorldNpc Second { get; }
        public QuestNpcInstanceId FirstId { get; }
        public QuestNpcInstanceId SecondId { get; }
        public QuestWorldSnapshotProjector Projector { get; }
        public Setup()
        {
            Fixture = new(Definition);
            First = Add(Fixture.SelectedCharacter, new(3, 3));
            Second = Add(Fixture.Companion, new(6, 3));
            Fog.Restore([First.Position, Second.Position], false);
            var registry = new QuestNpcInstanceRegistry();
            FirstId = registry.GetOrCreate(First);
            SecondId = registry.GetOrCreate(Second);
            Projector = new(Fixture.Manager, new(() => Maze, _ => 0, (_, _) => false, registry, _ => false));
        }
        private WorldNpc Add(LiveCharacter character, Position position)
        {
            var npc = new WorldNpc(position, "NPC002", character, NpcDisposition.Neutral, false, true, "");
            Maze.Carve(position);
            Maze.AddWorldNpc(npc);
            return npc;
        }
        public WorldSnapshot Project() => WorldSnapshotProjector.Create(Maze, Fog, projectQuests: Projector.Create);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
