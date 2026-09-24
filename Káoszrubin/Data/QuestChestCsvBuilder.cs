using System.Text;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Data;

internal sealed class QuestChestCsvBuilder
{
    private readonly Dictionary<QuestChestId,
        (string Name, int Gold, Rune Symbol, ConsoleColor Foreground, ConsoleColor Background)> _definitions = [];
    private readonly Dictionary<(QuestChestId Chest, string Item), int> _items = [];
    public void AddDefinition(IReadOnlyList<string> cells)
    {
        if (cells.Count < 6 || string.IsNullOrWhiteSpace(cells[1]) ||
            !int.TryParse(cells[2], out var gold) || gold < 0)
            throw new InvalidDataException(
                "A questláda sora: ID;Név;nemnegatív Arany;egy térképjel;Előtérszín;Háttérszín.");

        var runes = cells[3].Trim().EnumerateRunes().ToArray();
        if (runes.Length != 1 || Rune.IsControl(runes[0]))
            throw new InvalidDataException("A questláda térképjele pontosan egy látható karakter legyen.");
        if (!Enum.TryParse<ConsoleColor>(cells[4], true, out var foreground) || !Enum.IsDefined(foreground))
            throw new InvalidDataException($"Ismeretlen questláda-előtérszín: '{cells[4]}'.");
        if (!Enum.TryParse<ConsoleColor>(cells[5], true, out var background) || !Enum.IsDefined(background))
            throw new InvalidDataException($"Ismeretlen questláda-háttérszín: '{cells[5]}'.");

        if (!_definitions.TryAdd(new(cells[0]), (cells[1], gold, runes[0], foreground, background)))
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
                .Select(item => new QuestChestItem(data.GetItemDefinition(item.Key.Item), item.Value)).ToArray()),
            pair.Value.Symbol, pair.Value.Foreground, pair.Value.Background))
            .ToArray();
    }
}
