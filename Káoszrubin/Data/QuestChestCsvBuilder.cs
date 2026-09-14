using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Data;

internal sealed class QuestChestCsvBuilder
{
    private readonly Dictionary<QuestChestId, (string Name, int Gold)> _definitions = [];
    private readonly Dictionary<(QuestChestId Chest, string Item), int> _items = [];
    public void AddDefinition(IReadOnlyList<string> cells)
    {
        if (cells.Count < 3 || string.IsNullOrWhiteSpace(cells[1]) ||
            !int.TryParse(cells[2], out var gold) || gold < 0)
            throw new InvalidDataException("A questláda sora: ID;Név;nemnegatív Arany.");
        if (!_definitions.TryAdd(new(cells[0]), (cells[1], gold)))
            throw new InvalidDataException("Duplikált questláda-azonosító.");
    }
    public void AddItem(IReadOnlyList<string> cells)
    {
        if (cells.Count < 3 || string.IsNullOrWhiteSpace(cells[1]) ||
            !int.TryParse(cells[2], out var quantity) || quantity <= 0)
            throw new InvalidDataException("A ládatartalom sora: LádaID;TárgyID;pozitív Darab.");
        if (!_items.TryAdd((new(cells[0]), cells[1].ToUpperInvariant()), quantity))
            throw new InvalidDataException("Duplikált tárgy a questláda tartalmában.");
    }
    public IReadOnlyList<QuestChestDefinition> Build(GameDataCatalog data)
    {
        if (_items.Keys.Any(key => !_definitions.ContainsKey(key.Chest)))
            throw new InvalidDataException("A ládatartalom ismeretlen ládára hivatkozik.");
        return _definitions.Select(pair => new QuestChestDefinition(pair.Key, pair.Value.Name, pair.Value.Gold,
            Array.AsReadOnly(_items.Where(item => item.Key.Chest == pair.Key)
                .Select(item => new QuestChestItem(data.GetItemDefinition(item.Key.Item), item.Value)).ToArray())))
            .ToArray();
    }
}
