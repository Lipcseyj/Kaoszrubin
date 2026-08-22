using System.Text;

namespace MazeGame;

/// <summary>Egy csatában elesett szereplő maradványa a pályán.</summary>
public class Corpse(Position position, string formerName) : WorldObject(position)
{
    public string FormerName { get; } = formerName;
    public override Rune Symbol { get; } = new('†');
}
