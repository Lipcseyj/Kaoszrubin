using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application.Quests;

public sealed record QuestChestCollection(bool FirstOpening, int Gold, int ItemCount, int RemainingCount,
    IReadOnlyList<QuestProgressChange> Changes, string? BlockingGuardianId = null);

public sealed class QuestChestService(QuestManager manager)
{
    public QuestChestCollection Collect(TreasureChest chest, Func<IItemDefinition, bool> tryStore, Action<int> addGold, Func<string, bool>? guardianAlive = null)
    {
        if (chest.Definition is null) throw new ArgumentException("Nem questláda.", nameof(chest));
        if (!chest.IsOpened && chest.Definition.GuardianEnemyId is { } guardian && (guardianAlive?.Invoke(guardian) ?? true))
            return new(false, 0, 0, chest.RemainingItems.Sum(item => item.Quantity), [], guardian);
        var first = chest.Open();
        var changes = first ? manager.RegisterChestOpened(chest.Definition.Id) : [];
        var gold = chest.TakeGold();
        if (gold > 0) addGold(gold);
        var count = chest.TakeItems(tryStore);
        return new(first, gold, count, chest.RemainingItems.Sum(item => item.Quantity), changes);
    }
}
