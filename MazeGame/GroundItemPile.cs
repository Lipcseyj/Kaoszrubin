using System.Text;
using MazeGame.Domain.Inventory;

namespace MazeGame;

/// <summary>Egy mezőn fekvő, tetszőleges számú tárgy halma.</summary>
public sealed class GroundItemPile : WorldObject
{
    private readonly List<IItemDefinition> _items = [];

    public GroundItemPile(Position position, IItemDefinition firstItem) : base(position) => _items.Add(firstItem);

    public IReadOnlyList<IItemDefinition> Items => _items;
    public override Rune Symbol => new('◆');

    public void Add(IItemDefinition item) => _items.Add(item);
}
