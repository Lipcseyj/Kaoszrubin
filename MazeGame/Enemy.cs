using System.Text;
using MazeGame.Domain.Combat;

namespace MazeGame;

public abstract class Enemy(Position position) : WorldObject(position)
{
    public abstract EnemyDefinition Definition { get; }
    public string Name => Definition.Name;

    public void MoveTo(Position position) => Position = position;
}

/// <summary>CSV-definícióból létrehozott, saját megjelenésű ellenfél.</summary>
public sealed class ConfiguredEnemy(Position position, EnemyDefinition definition) : Enemy(position)
{
    public override EnemyDefinition Definition { get; } = definition;
    public override Rune Symbol { get; } = Rune.GetRuneAt(definition.Appearance, 0);
}
