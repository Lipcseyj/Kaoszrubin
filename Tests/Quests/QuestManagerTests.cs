using KaoszRubin.Application.Quests;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Quests;
using KaoszRubin.World;
using static KaoszRubin.Tests.Quests.QuestTestFixture;

namespace KaoszRubin.Tests.Quests;

internal static class QuestManagerTests
{
    public static void ActivationHonorsStoryGate()
    {
        var definition = Define(new QuestObjective.DisarmTraps(2)) with
        {
            ActivationRequirement = new QuestActivationRequirement.StoryStateEquals(QuestStoryState.Trusted)
        };
        var fixture = new QuestTestFixture(definition);
        var npc = fixture.Manager.For(definition.Giver);
        var quest = npc.GetQuest(definition.Id);
        Require(quest.IsLocked && npc.GetAvailableQuests().Count == 0, "A történeti kapu nélkül felvehető a küldetés.");
        Throws<InvalidOperationException>(quest.Activate);

        fixture.StoryStates[(definition.Giver, default)] = QuestStoryState.Trusted;
        Require(npc.GetAvailableQuests().Single().Id == definition.Id && quest.IsAvailable,
            "A történeti kapu nem nyitja meg a meglévő handle küldetését.");
        fixture.StoryStates.Clear();
        Require(npc.GetAvailableQuests().Count == 0 && quest.IsLocked, "A még fel nem vett küldetés kapuja nem zár vissza.");
        fixture.StoryStates[(definition.Giver, default)] = QuestStoryState.Trusted;
        quest.Activate();
        fixture.Manager.RegisterTrapDisarmed();
        fixture.StoryStates.Clear();
        Require(npc.GetAvailableQuests().Count == 0 && quest.IsActive && quest.Progress == 1,
            "A történet változása visszazárta vagy lenullázta az aktív küldetést.");
        Throws<InvalidOperationException>(quest.Activate);
        Require(quest.Progress == 1, "Az ismételt aktiválási kísérlet lenullázta a haladást.");
    }

    public static void BulkActivationLeavesLockedAndResolvedQuestsAlone()
    {
        var first = Define(new QuestObjective.DisarmTraps(1));
        var second = first with { Id = QuestId.HerbalistCleanBandages,
            ActivationRequirement = new QuestActivationRequirement.StoryStateEquals(QuestStoryState.Trusted) };
        var fixture = new QuestTestFixture(first, second);
        var npc = fixture.Manager.For(first.Giver);
        var activated = npc.ActivateAvailableQuests();
        Require(activated.Count == 1 && activated[0].Id == first.Id && npc.GetQuest(second.Id).IsLocked,
            "A tömeges aktiválás figyelmen kívül hagyta a történeti kaput.");
        activated[0].Abandon();
        Require(npc.ActivateAvailableQuests().Count == 0 && !npc.AreAllQuestsResolved,
            "A feladott küldetés újraindult vagy a zárolt küldetés lezártnak számít.");
    }

    public static void ProgressOnlyUsesMatchingActiveObjectives()
    {
        var fixture = new QuestTestFixture(Define(new QuestObjective.DisarmTraps(2)));
        var quest = fixture.Manager.GetQuest(QuestId.HerbalistHealingSupplies);
        Require(fixture.Manager.RegisterTrapDisarmed().Count == 0 && quest.Progress == 0,
            "Felvétel előtt haladt a küldetés.");
        quest.Activate();
        Require(fixture.Manager.RegisterChestOpened().Count == 0, "Más objective eseménye növelte a haladást.");
        var first = fixture.Manager.RegisterTrapDisarmed().Single();
        Require(first.PreviousProgress == 0 && first.CurrentProgress == 1 && !first.BecameReadyToTurnIn,
            "Hibás a részhaladás eseménye.");
        var last = fixture.Manager.RegisterTrapDisarmed().Single();
        Require(last.PreviousState == QuestState.Active && last.BecameReadyToTurnIn && quest.IsReadyToTurnIn,
            "A cél elérése nem váltott leadható állapotra.");
        Require(fixture.Manager.RegisterTrapDisarmed().Count == 0 && quest.Progress == 2,
            "A leadható küldetés a szükséges mennyiségen túl haladt.");
        Require(fixture.Manager.GetActiveQuests().Single().Id == quest.Id &&
            fixture.Manager.GetReadyToTurnInQuests().Single().Id == quest.Id,
            "A leadható küldetés hiányzik a folyamatban lévő vagy leadható listából.");
    }

    public static void CollectTracksInventoryInBothDirections()
    {
        var fixture = new QuestTestFixture(Define(new QuestObjective.CollectItem(Supplies, 3)));
        fixture.Inventory[Supplies] = 2;
        var quest = fixture.Manager.Activate(QuestId.HerbalistHealingSupplies);
        Require(quest.IsActive && quest.Progress == 2, "A felvétel nem számolta be a már meglévő készletet.");
        fixture.Inventory[Supplies] = 3;
        Require(fixture.Manager.RegisterInventoryChanged(Reward).Count == 0 && quest.Progress == 2,
            "Másik tárgy eseménye módosította a collect küldetést.");
        var ready = fixture.Manager.RegisterInventoryChanged(Supplies).Single();
        Require(ready.BecameReadyToTurnIn && ready.ProgressDelta == 1 && quest.CanComplete,
            "A 2/3 → 3/3 változás nem tette leadhatóvá a küldetést.");
        fixture.Inventory[Supplies] = 2;
        var lost = fixture.Manager.RegisterInventoryChanged(Supplies).Single();
        Require(lost.LostReadyToTurnIn && lost.ProgressDelta == -1 && quest.IsActive && !quest.CanComplete,
            "A 3/3 → 2/3 visszaesés nem jutott el az állapothoz és a változáseseményhez.");
        Require(fixture.Manager.SynchronizeCollectQuests().Count == 0, "A változatlan készlet új eseményt generált.");
        fixture.Inventory[Supplies] = 8;
        fixture.Manager.SynchronizeCollectQuests();
        Require(quest.IsReadyToTurnIn && quest.Progress == 3, "A gyűjtés túllépte a célmennyiséget.");
    }

    public static void CompletionGrantsRewardsOnlyOnce()
    {
        foreach (var backpackFull in new[] { false, true })
        {
            var definition = Define(new QuestObjective.CollectItem(Supplies, 3)) with { RandomRewardCount = 1 };
            var fixture = new QuestTestFixture(definition) { BackpackFull = backpackFull };
            fixture.Inventory[Supplies] = 5;
            var quest = fixture.Manager.Activate(definition.Id);
            Require(quest.CanComplete && fixture.Manager.GetPendingQuestCompletions().Count == 1,
                "A meglévő készletből felvett küldetés nem leadható.");
            Require(fixture.ConsumptionAttempts == 0 && fixture.SelectedCharacter.Experience == 0 &&
                fixture.StoredRewards.Count == 0 && fixture.DroppedRewards.Count == 0,
                "A leadhatóság lekérdezése már fogyasztott vagy jutalmazott.");
            var result = quest.Complete();
            Require(quest.IsCompleted && quest.CompletionCount == 1 && result.CompletionCount == 1 &&
                result.QuestId == quest.Id && result.GiverInstanceId == quest.GiverInstanceId,
                "A sikeres leadás állapota vagy eredményazonossága hibás.");
            Require(fixture.CountPartyItem(Supplies) == 2 && fixture.ConsumedItems == 3 &&
                fixture.SelectedCharacter.Experience == 10 && fixture.Companion.Experience == 10,
                "A leadás nem pontosan a szükséges készletet fogyasztotta vagy hibásan osztotta szét az XP-t.");
            Require(fixture.RandomRewardRolls == 1 &&
                fixture.StoredRewards.Count == (backpackFull ? 0 : 2) &&
                fixture.DroppedRewards.Count == (backpackFull ? 2 : 0), "A fix/véletlen jutalom elhelyezése hibás.");
            Throws<InvalidOperationException>(() => quest.Complete());
            Throws<InvalidOperationException>(quest.Activate);
            Throws<InvalidOperationException>(quest.Abandon);
            fixture.Inventory[Supplies] = 8;
            Require(fixture.Manager.RegisterInventoryChanged(Supplies).Count == 0 &&
                fixture.Manager.GetActiveQuests().Count == 0 && fixture.Manager.GetPendingQuestCompletions().Count == 0,
                "A lezárt küldetés újra bekerült a folyamatban lévő küldetések közé.");
            Require(quest.IsCompleted && quest.CompletionCount == 1 && fixture.ConsumptionAttempts == 1 &&
                fixture.SelectedCharacter.Experience + fixture.Companion.Experience == 20 &&
                fixture.RandomRewardRolls == 1 && fixture.StoredRewards.Count + fixture.DroppedRewards.Count == 2,
                "Az ismételt művelet még egyszer fogyasztott vagy jutalmazott.");
        }
    }

    public static void CompletionRechecksChangedInventory()
    {
        var fixture = new QuestTestFixture(Define(new QuestObjective.CollectItem(Supplies, 3)));
        fixture.Inventory[Supplies] = 3;
        var quest = fixture.Manager.Activate(QuestId.HerbalistHealingSupplies);
        Require(quest.CanComplete, "A küldetés a megerősítés előtt nem volt leadható.");
        fixture.Inventory[Supplies] = 2; // A megerősítő ablak közben elfogy egy tárgy; nincs külön quest-esemény.
        Throws<InvalidOperationException>(() => quest.Complete());
        Require(quest.IsActive && quest.Progress == 2 && quest.CompletionCount == 0 &&
            fixture.ConsumptionAttempts == 0 && fixture.SelectedCharacter.Experience == 0 && fixture.StoredRewards.Count == 0,
            "A leadás az elavult, korábban leadható állapotból teljesült.");
    }

    public static void FailedConsumptionDoesNotGrantRewards()
    {
        var fixture = new QuestTestFixture(Define(new QuestObjective.CollectItem(Supplies, 3))) { RejectConsumption = true };
        fixture.Inventory[Supplies] = 3;
        var quest = fixture.Manager.Activate(QuestId.HerbalistHealingSupplies);
        Throws<InvalidOperationException>(() => quest.Complete());
        Require(quest.IsReadyToTurnIn && quest.CompletionCount == 0 && fixture.ConsumptionAttempts == 1 &&
            fixture.ConsumedItems == 0 && fixture.CountPartyItem(Supplies) == 3 &&
            fixture.SelectedCharacter.Experience == 0 && fixture.Companion.Experience == 0 && fixture.StoredRewards.Count == 0,
            "A sikertelen tárgyelvétel ellenére lezárás vagy jutalmazás történt.");
        fixture.RejectConsumption = false;
        quest.Complete();
        Require(quest.IsCompleted && quest.CompletionCount == 1 && fixture.ConsumedItems == 3,
            "A sikertelen tárgyelvétel után nem lehetett szabályosan leadni a küldetést.");
    }

    public static void AbandonImmediatelyStopsProgress()
    {
        foreach (var initialCount in new[] { 1, 3 })
        {
            var fixture = new QuestTestFixture(Define(new QuestObjective.CollectItem(Supplies, 3)));
            var npc = fixture.Manager.For(QuestNpcId.WanderingHerbalist);
            var quest = npc.GetQuest(QuestId.HerbalistHealingSupplies);
            Throws<InvalidOperationException>(quest.Abandon);
            fixture.Inventory[Supplies] = initialCount;
            quest.Activate();
            quest.Abandon();
            fixture.Inventory[Supplies] = 5;
            Require(quest.IsFailed && quest.IsResolved && quest.Progress == initialCount && npc.AreAllQuestsResolved &&
                fixture.Manager.RegisterInventoryChanged(Supplies).Count == 0 &&
                fixture.Manager.GetActiveQuests().Count == 0 && npc.ActivateAvailableQuests().Count == 0,
                "A feladás nem azonnal állította meg a küldetést vagy engedte az újrafelvételt.");
            Throws<InvalidOperationException>(() => quest.Complete());
            Throws<InvalidOperationException>(quest.Activate);
            Require(fixture.ConsumptionAttempts == 0 && fixture.SelectedCharacter.Experience == 0 &&
                fixture.StoredRewards.Count == 0, "Feladás után fogyasztás vagy jutalmazás történt.");
        }
    }

    public static void NpcInstancesHaveIndependentLifecycles()
    {
        var definition = Define(new QuestObjective.DisarmTraps(2), scope: QuestScope.PerNpcInstance);
        var fixture = new QuestTestFixture(definition);
        var firstNpc = fixture.Manager.For(definition.Giver, new QuestNpcInstanceId(1));
        var secondNpc = fixture.Manager.For(definition.Giver, new QuestNpcInstanceId(2));
        var first = firstNpc.GetQuest(definition.Id);
        var second = secondNpc.GetQuest(definition.Id);
        first.Activate();
        fixture.Manager.RegisterTrapDisarmed();
        Require(first.Progress == 1 && second.Progress == 0 && second.IsLocked,
            "Az egyik NPC felvett küldetése a másik példányt is módosította.");
        second.Activate();
        var changes = fixture.Manager.RegisterTrapDisarmed();
        Require(changes.Count == 2 && changes.Select(change => change.GiverInstanceId).ToHashSet()
            .SetEquals(new[] { first.GiverInstanceId, second.GiverInstanceId }) &&
            first.IsReadyToTurnIn && second.IsActive && second.Progress == 1,
            "A két aktív példány változásai összecsúsztak.");
        first.Complete();
        second.Abandon();
        Require(first.IsCompleted && first.CompletionCount == 1 && second.IsFailed && second.CompletionCount == 0 &&
            firstNpc.AreAllQuestsResolved && secondNpc.AreAllQuestsResolved &&
            fixture.Manager.RegisterTrapDisarmed().Count == 0,
            "A célzott leadás/feladás a másik NPC példányát is lezárta vagy átírta.");
    }

    public static void GlobalIdentityAndNpcValidation()
    {
        var definition = Define(new QuestObjective.DisarmTraps(1));
        var fixture = new QuestTestFixture(definition);
        var first = fixture.Manager.GetQuest(definition.Id);
        var second = fixture.Manager.GetQuest(definition.Id, new QuestNpcInstanceId(2));
        first.Activate();
        fixture.Manager.RegisterTrapDisarmed();
        Require(second.IsReadyToTurnIn && second.GiverInstanceId.IsNone && fixture.Manager.GetActiveQuests().Count == 1,
            "A globális küldetésből több állapot keletkezett NPC-példányazonosító megadásával.");
        Throws<InvalidOperationException>(() => fixture.Manager.For(QuestNpcId.SirRoderic).GetQuest(definition.Id));
        Throws<ArgumentException>(() => fixture.Manager.For(QuestNpcId.None));
        Throws<ArgumentException>(() => fixture.Manager.GetQuest(QuestId.None));
        var perInstance = new QuestTestFixture(definition with { Scope = QuestScope.PerNpcInstance });
        Throws<InvalidOperationException>(() => perInstance.Manager.GetQuest(definition.Id));
        Throws<ArgumentOutOfRangeException>(() => new QuestNpcInstanceId(0));
    }

    public static void ConversationKeepsNpcInstanceIdentity()
    {
        var fixture = new QuestTestFixture(Define(new QuestObjective.DisarmTraps(1)));
        var instance = new QuestNpcInstanceId(42);
        fixture.Manager.For(QuestNpcId.WanderingHerbalist, instance).StartConversation();
        Require(fixture.Conversations.SequenceEqual(new[] { (QuestNpcId.WanderingHerbalist, instance) }),
            "A beszélgetési adapter nem a kiválasztott NPC-példányt kapta.");
    }

    public static void FollowerKillHonorsEnemyParticipationAndStory()
    {
        var enemyDefinition = new EnemyDefinition("TEST-ENEMY", "Ellenfél", "E", 1, 10, 0, 1, 0, 1, []);
        var objective = new QuestObjective.KillEnemy(enemyDefinition, 1,
            new QuestObjective.QuestFollowerRequirement(QuestNpcId.SirRoderic, QuestStoryState.MalrecFight, 6));
        var definition = Define(objective, QuestId.RodericOathbreakerKnight, QuestNpcId.SirRoderic);
        var fixture = new QuestTestFixture(definition);
        var quest = fixture.Manager.Activate(definition.Id);
        var enemy = new ConfiguredEnemy(new Position(2, 2), enemyDefinition);
        Require(fixture.Manager.RegisterKill(enemy).Count == 0, "A harcban részt nem vevő követő mellett haladt a quest.");
        fixture.ParticipatingInCombat = true;
        Require(fixture.Manager.RegisterKill(enemy).Count == 0, "Hibás történetállapotban haladt a follower quest.");
        fixture.StoryStates[(QuestNpcId.SirRoderic, default)] = QuestStoryState.MalrecFight;
        Require(fixture.Manager.RegisterKill(new ConfiguredEnemy(new Position(2, 2),
            enemyDefinition with { Id = "OTHER" })).Count == 0, "Másik ellenféltípus is teljesítette a konkrét célpontot.");
        Require(fixture.Manager.RegisterKill(enemy).Single().BecameReadyToTurnIn && quest.IsReadyToTurnIn &&
            fixture.CombatChecks.All(check => check.Npc == QuestNpcId.SirRoderic && check.Instance.IsNone &&
                ReferenceEquals(check.Enemy, enemy) && check.Distance == 6),
            "A follower ellenőrzés hibás NPC-t, példányt, ellenfelet vagy távolsághatárt kapott.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"A műveletnek {typeof(T).Name} kivételt kellett volna dobnia.");
    }
}
