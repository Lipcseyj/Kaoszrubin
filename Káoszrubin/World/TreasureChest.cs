using System.Text;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.World;

public sealed class TreasureChest : WorldObject
{
    public int GoldAmount { get; private set; }
    public QuestChestDefinition? Definition { get; }
    public bool IsOpened { get; private set; }
    private readonly List<QuestChestItem> _items = [];
    public IReadOnlyList<QuestChestItem> RemainingItems => _items.AsReadOnly();

    public TreasureChest(Position position, int goldAmount) : base(position) => GoldAmount = Math.Max(0, goldAmount);

    public TreasureChest(Position position, QuestChestDefinition definition,
        bool isOpened = false, int? remainingGold = null, IReadOnlyList<QuestChestItem>? remainingItems = null) : base(position)
    {
        if (string.IsNullOrWhiteSpace(definition.Id.Value) || definition.Gold < 0 || remainingGold < 0)
            throw new ArgumentException("Érvénytelen questláda.");
        Definition = definition;
        IsOpened = isOpened;
        GoldAmount = remainingGold ?? definition.Gold;
        _items.AddRange(remainingItems ?? definition.Items);
        if (_items.Any(item => item.Item is null || item.Quantity <= 0) ||
            _items.Select(item => item.Item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _items.Count)
            throw new ArgumentException("Érvénytelen vagy duplikált ládatartalom.");
    }

    public bool Open()
    {
        if (IsOpened) return false;
        IsOpened = true;
        return true;
    }

    public int TakeGold()
    {
        if (!IsOpened) throw new InvalidOperationException("A láda még nincs kinyitva.");
        var gold = GoldAmount;
        GoldAmount = 0;
        return gold;
    }

    public int TakeItems(Func<Domain.Inventory.IItemDefinition, bool> tryStore)
    {
        if (!IsOpened) throw new InvalidOperationException("A láda még nincs kinyitva.");
        var count = 0;
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            var entry = _items[index];
            var remaining = entry.Quantity;
            while (remaining > 0 && tryStore(entry.Item)) { remaining--; count++; }
            if (remaining == 0) _items.RemoveAt(index);
            else _items[index] = entry with { Quantity = remaining };
        }
        return count;
    }
    public override Rune Symbol => new(IsOpened ? '□' : '▣');
}
