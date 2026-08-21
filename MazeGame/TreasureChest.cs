using System.Text;

namespace MazeGame;

public sealed class TreasureChest(Position position) : WorldObject(position)
{
    public override Rune Symbol { get; } = new('▣');
}
