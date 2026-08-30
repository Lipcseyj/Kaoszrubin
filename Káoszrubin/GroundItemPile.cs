using System.Text;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin;

/// <summary>Egy mezőn fekvő, tetszőleges számú tárgy halma.</summary>
public sealed class GroundItemPile : WorldObject
{
    private readonly List<GroundItemEntry> _entries = [];

    public GroundItemPile(Position position, IItemDefinition firstItem, int? charges = null) : base(position) =>
        Add(firstItem, charges);

    public IReadOnlyList<GroundItemEntry> Entries => _entries;
    public IReadOnlyList<IItemDefinition> Items => _entries.Select(entry => entry.Item).ToArray();
    public long Revision { get; private set; }
    public override Rune Symbol => new('◆');

    public void Add(IItemDefinition item, int? charges = null)
    {
        _entries.Add(new GroundItemEntry(item, NormalizeCharges(item, charges)));
        Revision++;
    }

    public bool Remove(IItemDefinition item)
    {
        var index = _entries.FindIndex(entry => ReferenceEquals(entry.Item, item));
        if (index < 0) index = _entries.FindIndex(entry => entry.Item == item);
        if (index < 0) return false;
        _entries.RemoveAt(index);
        Revision++;
        return true;
    }

    public bool TryTake(int index, long expectedRevision, out GroundItemEntry entry)
    {
        if (Revision != expectedRevision || index < 0 || index >= _entries.Count)
        {
            entry = null!;
            return false;
        }
        entry = _entries[index];
        _entries.RemoveAt(index);
        Revision++;
        return true;
    }

    private static int NormalizeCharges(IItemDefinition item, int? charges) => item is MagicItemDefinition magic &&
        magic.Kind is MagicItemKind.Wand or MagicItemKind.Scroll
            ? Math.Clamp(charges ?? magic.MaximumCharges, 0, magic.MaximumCharges)
            : 0;
}

public sealed record GroundItemEntry(IItemDefinition Item, int Charges);
