using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Data;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.World;
using static KaoszRubin.Tests.Quests.QuestTestFixture;

namespace KaoszRubin.Tests.Quests;

internal static class QuestMigrationTests
{
    public static void RodericStoryStartsOnlyTheChosenQuest()
    {
        var data = LoadData();
        var fixture = new QuestTestFixture(new QuestCatalogBuilder(data).Build().All.ToArray());
        var npc = fixture.Manager.For(QuestNpcId.SirRoderic);
        Require(npc.ActivateAvailableQuests().Count == 0, "Roderic már a bemutatkozás előtt küldetéseket adott.");
        foreach (var (legacyId, typedId) in new[]
        {
            ("NPCQ040", QuestId.RodericTheDeadAreNotPrey),
            ("NPCQ037", QuestId.RodericFallenComradesInsignia)
        })
        {
            var choice = data.NpcStoryChoices.Single(choice => choice.ActionParameter == legacyId);
            fixture.StoryStates[(QuestNpcId.SirRoderic, default)] =
                LegacyQuestStoryStateMap.ToQuestStoryState(choice.NextStateId);
            Require(npc.ActivateAvailableQuests().Count == 0,
                "A történeti választással nyíló questet az automatikus ajánlat is felvette.");
            npc.GetQuest(typedId).Activate();
            Require(npc.GetQuest(typedId).IsActive, "A választás utáni történetállapot nem engedte a konkrét felvételt.");
        }
        fixture.StoryStates[(QuestNpcId.SirRoderic, default)] = QuestStoryState.Following;
        Require(npc.ActivateAvailableQuests().Single().Id == QuestId.RodericSharedBladeTrial,
            "A követés megkezdése nem kizárólag a közös pengepróbát indította el.");
        fixture.StoryStates[(QuestNpcId.SirRoderic, default)] = QuestStoryState.Trusted;
        Require(npc.ActivateAvailableQuests().Count == 0 && npc.GetQuest(QuestId.RodericOathbreakerKnight).IsLocked,
            "Malrec küldetése már TRUSTED állapotban elindulhatott.");
        fixture.StoryStates[(QuestNpcId.SirRoderic, default)] = QuestStoryState.MalrecApproach;
        Require(npc.ActivateAvailableQuests().Count == 0, "Malrec küldetése automatikus ajánlat lett.");
        npc.GetQuest(QuestId.RodericOathbreakerKnight).Activate();
        Require(npc.GetQuest(QuestId.RodericOathbreakerKnight).IsActive,
            "A helyszínre indulás tényleges állapotában nem indult el Malrec küldetése.");
    }

    public static void DiscoveryNeverCompletesEscort()
    {
        var explore = Define(new QuestObjective.ExploreLocation(QuestLocation.Exit));
        var escort = Define(new QuestObjective.EscortNpc(QuestNpcId.EliraSilverbranch, QuestLocation.Exit),
            QuestId.EliraRescue, QuestNpcId.EliraSilverbranch);
        var fixture = new QuestTestFixture(explore, escort) { AliveAndFollowing = true, AtLocation = true };
        var exploration = fixture.Manager.Activate(explore.Id);
        var rescue = fixture.Manager.Activate(escort.Id);
        Require(fixture.Manager.RegisterLocationDiscovered(QuestLocation.Exit).Single().QuestId == explore.Id &&
            exploration.IsReadyToTurnIn && rescue.IsActive, "A kijárat meglátása teljesítette a kísérést.");
        Require(fixture.Manager.RegisterLocationReached(QuestLocation.Exit).Count == 0 && rescue.IsActive,
            "A parti helyszíneseménye konkrét kísérő nélkül teljesítette a kísérést.");
        var instance = new QuestNpcInstanceId(1);
        fixture.AtLocation = false;
        Require(fixture.Manager.RegisterNpcReachedLocation(escort.Giver, instance, QuestLocation.Exit).Count == 0,
            "A távol lévő kísérő célba értnek számít.");
        fixture.AtLocation = true;
        fixture.AliveAndFollowing = false;
        Require(fixture.Manager.RegisterNpcReachedLocation(escort.Giver, instance, QuestLocation.Exit).Count == 0,
            "A halott vagy nem követő NPC célba értnek számít.");
        fixture.AliveAndFollowing = true;
        Require(fixture.Manager.RegisterNpcReachedLocation(escort.Giver, instance, QuestLocation.Exit).Single().BecameReadyToTurnIn &&
            rescue.IsReadyToTurnIn, "Az élő, közeli kísérővel végrehajtott kijárathasználat nem teljesítette a célt.");
    }

    public static void LateExploreActivationUsesCurrentWorldDiscovery()
    {
        var definition = Define(new QuestObjective.ExploreLocation(QuestLocation.Exit), scope: QuestScope.PerNpcInstance);
        var fixture = new QuestTestFixture(definition);
        fixture.DiscoveredLocations.Add(QuestLocation.Exit);
        var first = fixture.Manager.Activate(definition.Id, new QuestNpcInstanceId(1));
        var second = fixture.Manager.For(definition.Giver, new QuestNpcInstanceId(2)).ActivateAvailableQuests().Single();
        Require(first.IsReadyToTurnIn && second.IsReadyToTurnIn,
            "A korábban felfedezett kijáratot az egyedi vagy tömeges felvétel nem számolta be.");
        fixture.DiscoveredLocations.Clear(); // Új pálya: a régi felfedezés nem globális emlékezet.
        var third = fixture.Manager.Activate(definition.Id, new QuestNpcInstanceId(3));
        Require(third.IsActive && third.Progress == 0, "Az új pálya a régi kijárat felfedezését örökölte.");
    }

    public static void EscortArrivalIsBoundToNpcInstance()
    {
        var definition = Define(new QuestObjective.EscortNpc(QuestNpcId.WanderingHerbalist, QuestLocation.Exit),
            scope: QuestScope.PerNpcInstance);
        var fixture = new QuestTestFixture(definition) { AliveAndFollowing = true, AtLocation = true };
        var first = fixture.Manager.Activate(definition.Id, new QuestNpcInstanceId(1));
        var second = fixture.Manager.Activate(definition.Id, new QuestNpcInstanceId(2));
        var changes = fixture.Manager.RegisterNpcReachedLocation(definition.Giver, first.GiverInstanceId, QuestLocation.Exit);
        Require(changes.Single().GiverInstanceId == first.GiverInstanceId && first.IsReadyToTurnIn && second.IsActive,
            "Az egyik NPC célba érkezése a másik példány kísérését is teljesítette.");
    }

    public static void MazeEscortChecksActualDistanceAndLife()
    {
        var fixture = new QuestTestFixture();
        var maze = new Maze(11, 11);
        maze.Carve(new Position(9, 9));
        maze.PlaceExit(new Position(9, 9));
        var npc = new WorldNpc(new Position(5, 9), "NPC020", fixture.SelectedCharacter,
            NpcDisposition.Neutral, true, true, "Elira", questIds: ["NPCQ034"]);
        npc.BeginFollowing();
        var avatar = new PartyMemberAvatar(npc.Position, npc.Character, npc);
        maze.Carve(avatar.Position);
        maze.AddPartyMember(avatar);
        var context = new MazeQuestWorldContext(() => maze, _ => 0, (_, _) => false,
            new QuestNpcInstanceRegistry(), _ => true);
        var id = context.GetInstanceId(npc);
        Require(!context.IsNpcAtLocation(QuestNpcId.EliraSilverbranch, id, QuestLocation.Exit),
            "A négy mezőre lévő kísérő túl közelnek számít.");
        avatar.MoveTo(new Position(6, 9));
        Require(context.IsNpcAtLocation(QuestNpcId.EliraSilverbranch, id, QuestLocation.Exit) &&
            context.IsNpcAliveAndFollowing(QuestNpcId.EliraSilverbranch, id),
            "Az élő, három mezőre lévő követő nem teljesíti a kijárat közelségi szabályát.");
        Require(!context.IsNpcAtLocation(QuestNpcId.EliraSilverbranch, new QuestNpcInstanceId(id.Value + 1), QuestLocation.Exit),
            "Másik NPC-példány helyzete is megfelelt.");
        npc.Character.ReceiveDamage(1000);
        Require(!context.IsNpcAliveAndFollowing(QuestNpcId.EliraSilverbranch, id), "A halott követő továbbra is élőnek számít.");
    }

    public static void JournalProjectsChangesWithoutNpcOrReverseWrites()
    {
        var data = LoadData();
        var definition = new QuestCatalogBuilder(data).Build().Get(QuestId.HerbalistHealingSupplies);
        var fixture = new QuestTestFixture(definition);
        var world = new MazeQuestWorldContext(() => new Maze(7, 7), _ => 0, (_, _) => false,
            new QuestNpcInstanceRegistry(), _ => false);
        var coordinator = new NpcQuestCoordinator(data, fixture.Manager, world);
        var journal = new Dictionary<string, QuestJournalEntrySnapshot>();
        fixture.Manager.QuestChanged += quest => coordinator.SynchronizeQuestJournal(journal, quest);
        var item = ((QuestObjective.CollectItem)definition.Objective).Item;
        fixture.Inventory[item] = 2;
        var quest = fixture.Manager.Activate(definition.Id, new QuestNpcInstanceId(1));
        Require(journal["NPCQ001"] is { Status: QuestJournalStatus.Active, Progress: 2 },
            "NPC jelenléte nélkül hiányzik az aktiválás vagy a kezdeti collect progress a naplóból.");
        journal["NPCQ001"] = journal["NPCQ001"] with { Status = QuestJournalStatus.Abandoned };
        coordinator.SynchronizeQuestJournal(journal, quest);
        Require(quest.IsActive && journal["NPCQ001"].Status == QuestJournalStatus.Active,
            "A napló visszaírt a domainbe, vagy felülírta a hiteles állapotot.");
        fixture.Inventory[item] = 3;
        Require(quest.CanComplete && journal["NPCQ001"].Progress == 3,
            "A CanComplete belső inventory-szinkronja nem frissítette a naplót.");
        quest.Abandon();
        Require(journal["NPCQ001"].Status == QuestJournalStatus.Abandoned,
            "A feladás nem jutott el ugyanabban a hívásban a naplóig.");
        fixture.Inventory[item] = 0;
        Require(fixture.Manager.RegisterInventoryChanged(item).Count == 0 && journal["NPCQ001"].Progress == 3,
            "A feladott naplósor később tovább változott.");
    }

    public static void CompletionRefreshesOtherCollectProjections()
    {
        var first = Define(new QuestObjective.CollectItem(Supplies, 3));
        var second = first with { Id = QuestId.HerbalistCleanBandages };
        var fixture = new QuestTestFixture(first, second);
        var projected = new Dictionary<QuestId, (QuestState State, int Progress)>();
        fixture.Manager.QuestChanged += quest => projected[quest.Id] = (quest.State, quest.Progress);
        fixture.Inventory[Supplies] = 3;
        fixture.Manager.For(first.Giver).ActivateAvailableQuests();
        fixture.Manager.GetQuest(first.Id).Complete();
        Require(projected[first.Id].State == QuestState.Completed && projected[second.Id] == (QuestState.Active, 0),
            "A leadás fogyasztása után a másik collect projekciója leadható maradt.");
        // Nem collect quest jutalma is teljesíthet egy gyűjtést.
        var rewardQuest = Define(new QuestObjective.DisarmTraps(1), QuestId.GraveKeeperDesecratedSeals) with
            { FixedRewardItem = Supplies, FixedRewardItemCount = 3 };
        var rewardFixture = new QuestTestFixture(first, rewardQuest) { StoreRewardsInInventory = true };
        var collecting = rewardFixture.Manager.Activate(first.Id);
        rewardFixture.Manager.Activate(rewardQuest.Id);
        rewardFixture.Manager.RegisterTrapDisarmed();
        rewardFixture.Manager.GetQuest(rewardQuest.Id).Complete();
        Require(collecting.IsReadyToTurnIn, "A nem collect küldetés jutalma nem frissítette a gyűjtést.");
    }

    public static void InventoryBoundaryTracksMutationsAndPartyMembership()
    {
        var fixture = new QuestTestFixture(Define(new QuestObjective.CollectItem(Supplies, 3)))
            { UseCharacterInventories = true };
        var synchronizer = new QuestInventorySynchronizer(fixture.Manager);
        var quest = fixture.Manager.Activate(QuestId.HerbalistHealingSupplies);
        synchronizer.Synchronize(fixture.PartyMembers);
        for (var index = 0; index < 3; index++) Require(fixture.SelectedCharacter.AddToBackpack(Supplies), "Nem fért el a tesztkészlet.");
        Require(synchronizer.Synchronize(fixture.PartyMembers).Single().BecameReadyToTurnIn,
            "A valódi inventory készletnövekedése nem jutott el a collect questhez.");
        Require(fixture.SelectedCharacter.RemoveOneInventoryItem(InventorySlotKind.Backpack, 0), "Nem fogyott a tesztkészlet.");
        Require(synchronizer.Synchronize(fixture.PartyMembers).Single().LostReadyToTurnIn,
            "A fogyasztás/eladás/eldobás közös inventory-módosítása nem okozott visszaesést.");
        fixture.Companion.AddToBackpack(Supplies);
        synchronizer.Synchronize(fixture.PartyMembers);
        // Egy készletáthelyezés két karaktert érint, a végső összmennyiség változatlan.
        fixture.SelectedCharacter.RemoveOneInventoryItem(InventorySlotKind.Backpack, 0);
        fixture.Companion.AddToBackpack(Supplies);
        Require(synchronizer.Synchronize(fixture.PartyMembers).Count == 0 && quest.IsReadyToTurnIn,
            "A partin belüli áthelyezés hamis collect-visszaesést jelzett.");
        fixture.Members.Remove(fixture.Companion);
        Require(synchronizer.Synchronize(fixture.PartyMembers).Single().LostReadyToTurnIn && quest.Progress == 1,
            "A partitagság változása nem módosította az elérhető készletet.");
        Require(synchronizer.Synchronize(fixture.PartyMembers).Count == 0, "A változatlan inventory ismételt eseményt generált.");
    }

    private static GameDataCatalog LoadData() => CsvGameDataLoader.Load(
        Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
