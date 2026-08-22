using System.Text;
using MazeGame.Domain.Combat;

namespace MazeGame;

public abstract class Enemy(Position position) : WorldObject(position)
{
    public abstract EnemyDefinition Definition { get; }
    public string Name => Definition.Name;
    public int CurrentHitPoints { get; private set; }

    protected void InitializeHitPoints(int hitPoints) => CurrentHitPoints = Math.Max(0, hitPoints);
    public void SetCurrentHitPoints(int hitPoints) => CurrentHitPoints = Math.Max(0, hitPoints);

    public void MoveTo(Position position) => Position = position;
}

/// <summary>CSV-definícióból létrehozott, saját megjelenésű ellenfél.</summary>
public sealed class ConfiguredEnemy : Enemy
{
    public ConfiguredEnemy(Position position, EnemyDefinition definition) : base(position)
    {
        Definition = definition;
        Symbol = Rune.GetRuneAt(definition.Appearance, 0);
        InitializeHitPoints(definition.HitPoints ?? 0);
    }

    public override EnemyDefinition Definition { get; }
    public override Rune Symbol { get; }
}
