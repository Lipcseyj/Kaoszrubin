using System.Text;

namespace KaoszRubin;

public sealed class TreasureChest(Position position, int goldAmount) : WorldObject(position)
{
    public int GoldAmount { get; } = Math.Max(0, goldAmount);
    public override Rune Symbol { get; } = new('▣');
}
