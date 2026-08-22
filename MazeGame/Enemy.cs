using System.Text;
using MazeGame.Domain.Combat;

namespace MazeGame;

public enum EnemyMovementProfile { Wander, Stationary, Patrol }
public enum EnemyPursuitState { Undecided, Pursuing, Declined }
public enum EnemyGroupRole { Member, Leader }

public abstract class Enemy(Position position) : WorldObject(position)
{
    public abstract EnemyDefinition Definition { get; }
    public string Name => Definition.Name;
    public int CurrentHitPoints { get; private set; }
    public EnemyMovementProfile MovementProfile { get; private set; } = EnemyMovementProfile.Wander;
    public Direction PatrolDirection { get; private set; } = Direction.Right;
    public EnemyPursuitState PursuitState { get; private set; } = EnemyPursuitState.Undecided;
    public string? GroupId { get; private set; }
    public EnemyGroupRole GroupRole { get; private set; } = EnemyGroupRole.Member;

    protected void InitializeHitPoints(int hitPoints) => CurrentHitPoints = Math.Max(0, hitPoints);
    public void SetCurrentHitPoints(int hitPoints) => CurrentHitPoints = Math.Max(0, hitPoints);
    public void ConfigureMovement(EnemyMovementProfile profile, Direction patrolDirection,
        EnemyPursuitState pursuitState = EnemyPursuitState.Undecided)
    {
        MovementProfile = profile;
        PatrolDirection = patrolDirection;
        PursuitState = pursuitState;
    }
    public void ReversePatrolDirection() => PatrolDirection = PatrolDirection switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        _ => Direction.Right
    };
    public void ResolvePursuit(bool pursue) => PursuitState = pursue
        ? EnemyPursuitState.Pursuing
        : EnemyPursuitState.Declined;
    public void ConfigureGroup(string? groupId, EnemyGroupRole role = EnemyGroupRole.Member)
    {
        GroupId = string.IsNullOrWhiteSpace(groupId) ? null : groupId;
        GroupRole = role;
    }

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
