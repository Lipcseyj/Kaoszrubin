using KaoszRubin.Application.Quests;
using KaoszRubin.Domain.Quests;
using KaoszRubin.UI;
using static KaoszRubin.Tests.Quests.QuestTestFixture;

namespace KaoszRubin.Tests.Quests;

internal static class QuestPresentationTests
{
    public static void PresentationIsAnImmutableTypedSnapshot()
    {
        var fixture = new QuestTestFixture(Define(new QuestObjective.DisarmTraps(2), scope: QuestScope.PerNpcInstance));
        var quest = fixture.Manager.Activate(QuestId.HerbalistHealingSupplies, new(7));
        var snapshot = QuestPresentationSnapshot.From(quest);
        fixture.Manager.RegisterTrapDisarmed();
        Require(snapshot.Key == quest.Key && snapshot.Progress == 0 && quest.Progress == 1,
            "A megjelenítési adat élő állapotot tartalmaz vagy elvesztette a futáskulcsot.");
        var lines = QuestTurnInWindow.Build("Megbízó", snapshot);
        Require(lines.Any(line => line.Text.Contains("Állapot: 0/2")) &&
            lines.Any(line => line.Text.Contains("Jutalom ×1")) && lines.Any(line => line.Text.Contains("20 XP")),
            "A típusos leadási ablakból hiányzik a progress vagy a jutalom.");
        Require(fixture.StoredRewards.Count == 0 && fixture.ConsumptionAttempts == 0,
            "A megjelenítés mellékhatást okozott.");
    }

    public static void DeferredTurnInNeverConsumesOrRewards()
    {
        var fixture = new QuestTestFixture(Define(new QuestObjective.CollectItem(Supplies, 2)));
        fixture.Inventory[Supplies] = 2;
        var quest = fixture.Manager.Activate(QuestId.HerbalistHealingSupplies);
        Require(!QuestTurnInService.TryComplete(quest, _ => false, out _) && quest.IsReadyToTurnIn &&
            fixture.Inventory[Supplies] == 2 && fixture.ConsumptionAttempts == 0 && fixture.StoredRewards.Count == 0,
            "Az elhalasztott leadás fogyasztott, jutalmazott vagy lezárt.");
        Require(QuestTurnInService.TryComplete(quest, _ => true, out var result) && result.QuestId == quest.Id &&
            quest.IsCompleted && fixture.ConsumedItems == 2 && fixture.StoredRewards.Count == 1,
            "Az elfogadott leadás nem a kiválasztott futást zárta le egyszer.");
    }

    public static void ConfirmationDoesNotFreezeInventoryEligibility()
    {
        var fixture = new QuestTestFixture(Define(new QuestObjective.CollectItem(Supplies, 2)));
        fixture.Inventory[Supplies] = 2;
        var quest = fixture.Manager.Activate(QuestId.HerbalistHealingSupplies);
        var rejected = false;
        try
        {
            QuestTurnInService.TryComplete(quest, snapshot =>
            {
                Require(snapshot.Progress == 2, "A megerősítés hibás progressből készült.");
                fixture.Inventory[Supplies] = 1;
                return true;
            }, out _);
        }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && quest.IsActive && fixture.StoredRewards.Count == 0 && fixture.ConsumedItems == 0,
            "A megerősítés alatt megváltozott készlet ellenére jutalmazott a leadás.");
    }

    public static void EliraDepartureRequiresActualCompletion()
    {
        var rescue = Define(new QuestObjective.EscortNpc(QuestNpcId.EliraSilverbranch, QuestLocation.Exit),
            QuestId.EliraRescue, QuestNpcId.EliraSilverbranch);
        var fixture = new QuestTestFixture(rescue) { AliveAndFollowing = true, AtLocation = true };
        var quest = fixture.Manager.Elira.Quests.Rescue;
        Require(!fixture.Manager.Elira.CanResolveDeparture, "Elira még a mentés előtt távozhat.");
        quest.Activate();
        fixture.Manager.RegisterNpcReachedLocation(QuestNpcId.EliraSilverbranch, new(1), QuestLocation.Exit);
        Require(quest.IsReadyToTurnIn && !fixture.Manager.Elira.CanResolveDeparture,
            "A célba érés önmagában lezártnak számított.");
        QuestTurnInService.TryComplete(quest, _ => false, out _);
        Require(!fixture.Manager.Elira.CanResolveDeparture && fixture.StoredRewards.Count == 0,
            "Az elhalasztott leadás megnyitotta a történeti továbblépést.");
        QuestTurnInService.TryComplete(quest, _ => true, out _);
        Require(fixture.Manager.Elira.CanResolveDeparture && quest.CompletionCount == 1,
            "A tényleges leadás után nem nyílt meg a kijárati döntés.");
        var abandoned = new QuestTestFixture(rescue);
        abandoned.Manager.Elira.Quests.Rescue.Activate();
        abandoned.Manager.Elira.Quests.Rescue.Abandon();
        Require(!abandoned.Manager.Elira.CanResolveDeparture, "A feladás sikeres megmentésnek számított.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
