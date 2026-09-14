using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application.Quests;

public sealed record QuestChestCollection(bool FirstOpening, int Gold, int ItemCount, int RemainingCount,
    IReadOnlyList<QuestProgressChange> Changes);

public sealed class QuestChestService(QuestManager manager)
{
    public QuestChestCollection Collect(TreasureChest chest, Func<IItemDefinition, bool> tryStore, Action<int> addGold)
    {
        if (chest.Definition is null) throw new ArgumentException("Nem questláda.", nameof(chest));
        var first = chest.Open();
        var changes = first ? manager.RegisterChestOpened(chest.Definition.Id) : [];
        var gold = chest.TakeGold();
        if (gold > 0) addGold(gold);
        var count = chest.TakeItems(tryStore);
        return new(first, gold, count, chest.RemainingItems.Sum(item => item.Quantity), changes);
    }
}
