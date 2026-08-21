using System.Text;

namespace MazeGame;

public abstract class Enemy(Position position) : WorldObject(position)
{
    public abstract string Name { get; }

    public void MoveTo(Position position) => Position = position;
}

/// <summary>Az első, később további típusokkal bővíthető ellenfél.</summary>
public sealed class Skeleton(Position position) : Enemy(position)
{
    public override string Name => "Csontváz";
    public override Rune Symbol { get; } = new('♟');
}
