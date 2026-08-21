using System.Text;
using MazeGame.Domain.Combat;

namespace MazeGame;

public abstract class Enemy(Position position) : WorldObject(position)
{
    public abstract string Name { get; }

    public void MoveTo(Position position) => Position = position;
}

/// <summary>Az első, később további típusokkal bővíthető ellenfél.</summary>
public sealed class Skeleton(Position position, EnemyDefinition definition) : Enemy(position)
{
    public EnemyDefinition Definition { get; } = definition;
    public override string Name => Definition.Name;
    public override Rune Symbol { get; } = new('*');
}
