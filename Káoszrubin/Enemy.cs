using System.Text;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin;

public enum EnemyMovementProfile { Wander, Stationary, Patrol }
public enum EnemyPursuitState { Undecided, Pursuing, Declined }
public enum EnemyGroupRole { Member, Leader }

public abstract class Enemy(Position position) : WorldObject(position)
{
    public const int DefaultPursuitMemoryMoves = 3;
    public abstract EnemyDefinition Definition { get; }
    public string Name => Definition.Name;
    public int CurrentHitPoints { get; private set; }
    public EnemyMovementProfile MovementProfile { get; private set; } = EnemyMovementProfile.Wander;
    public Direction PatrolDirection { get; private set; } = Direction.Right;
    public EnemyPursuitState PursuitState { get; private set; } = EnemyPursuitState.Undecided;
    public CharacterId? PursuitTargetCharacterId { get; private set; }
    public int PursuitMemoryRemainingMoves { get; private set; }
    public string? GroupId { get; private set; }
    public EnemyGroupRole GroupRole { get; private set; } = EnemyGroupRole.Member;
    private readonly List<ActiveSpellEffect> _activeSpellEffects = [];
    private int _spellActionCounter;
    public IReadOnlyList<ActiveSpellEffect> ActiveSpellEffects => _activeSpellEffects;
    public bool IsPerceptiblyActive { get; private set; }

    protected void InitializeHitPoints(int hitPoints) => CurrentHitPoints = Math.Max(0, hitPoints);
    public void SetCurrentHitPoints(int hitPoints) => CurrentHitPoints = Math.Max(0, hitPoints);
    public void ReceiveSpellDamage(int damage) => SetCurrentHitPoints(CurrentHitPoints - Math.Max(0, damage));
    public int EffectiveSpeed => Math.Max(0, (Definition.Speed ?? 1) -
        _activeSpellEffects.Where(effect => effect.Type is ActiveSpellEffectType.SpeedPenalty or ActiveSpellEffectType.Frost)
            .Sum(effect => effect.Value));

    public void ApplySpellEffect(ActiveSpellEffect effect)
    {
        _activeSpellEffects.RemoveAll(existing => existing.Type == effect.Type);
        _activeSpellEffects.Add(effect);
        if (effect.Type == ActiveSpellEffectType.SkipAlternate) _spellActionCounter = 0;
    }

    public void RestoreSpellEffect(ActiveSpellEffect effect) => ApplySpellEffect(effect);
    public int RemoveSpellEffects(Func<ActiveSpellEffect, bool>? predicate = null) =>
        _activeSpellEffects.RemoveAll(effect => predicate?.Invoke(effect) ?? true);

    public SpellEffectTickResult AdvanceSpellEffects(Random random)
    {
        var damage = 0;
        var notes = new List<string>();
        foreach (var effect in _activeSpellEffects)
        {
            if (effect.PeriodicDamage is not { } dice) continue;
            var rolled = (dice.Roll(random) + effect.IntelligenceBonus) * effect.DamageMultiplierPercent / 100;
            damage += rolled;
            notes.Add($"{EffectName(effect.Type)} -{rolled} HP");
        }
        _spellActionCounter++;
        var skip = _activeSpellEffects.Any(effect => effect.Type == ActiveSpellEffectType.SkipNext) ||
                   _activeSpellEffects.Any(effect => effect.Type == ActiveSpellEffectType.SkipAlternate) &&
                   _spellActionCounter % 2 == 0;
        for (var index = _activeSpellEffects.Count - 1; index >= 0; index--)
        {
            var effect = _activeSpellEffects[index];
            if (effect.RemainingActions <= 0) continue;
            var remaining = effect.RemainingActions - 1;
            if (remaining == 0) _activeSpellEffects.RemoveAt(index);
            else _activeSpellEffects[index] = effect with { RemainingActions = remaining };
        }
        return new SpellEffectTickResult(damage, skip, notes);
    }

    private static string EffectName(ActiveSpellEffectType type) => type switch
    {
        ActiveSpellEffectType.Burning => "🔥 Égés",
        ActiveSpellEffectType.Storm => "⚡ Vihar",
        _ => "✨ Varázshatás"
    };
    public void ConfigureMovement(EnemyMovementProfile profile, Direction patrolDirection,
        EnemyPursuitState pursuitState = EnemyPursuitState.Undecided,
        CharacterId? pursuitTargetCharacterId = null, int pursuitMemoryRemainingMoves = -1)
    {
        MovementProfile = profile;
        PatrolDirection = patrolDirection;
        PursuitState = pursuitState;
        PursuitTargetCharacterId = pursuitTargetCharacterId;
        PursuitMemoryRemainingMoves = pursuitTargetCharacterId is null ? 0 : pursuitMemoryRemainingMoves >= 0
            ? pursuitMemoryRemainingMoves
            : DefaultPursuitMemoryMoves;
    }
    public void ReversePatrolDirection() => PatrolDirection = PatrolDirection switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        _ => Direction.Right
    };
    public void ResolvePursuit(bool pursue, CharacterId? targetCharacterId = null)
    {
        PursuitState = pursue ? EnemyPursuitState.Pursuing : EnemyPursuitState.Declined;
        PursuitTargetCharacterId = pursue ? targetCharacterId : null;
        PursuitMemoryRemainingMoves = pursue && targetCharacterId is not null ? DefaultPursuitMemoryMoves : 0;
    }

    public void RefreshPursuitMemory() => PursuitMemoryRemainingMoves = DefaultPursuitMemoryMoves;

    public bool TryRememberPursuitTarget()
    {
        if (PursuitMemoryRemainingMoves <= 0) return false;
        PursuitMemoryRemainingMoves--;
        return true;
    }

    public void ResetPursuit()
    {
        PursuitState = EnemyPursuitState.Undecided;
        PursuitTargetCharacterId = null;
        PursuitMemoryRemainingMoves = 0;
    }
    public void ConfigureGroup(string? groupId, EnemyGroupRole role = EnemyGroupRole.Member)
    {
        GroupId = string.IsNullOrWhiteSpace(groupId) ? null : groupId;
        GroupRole = role;
    }

    public void MoveTo(Position position)
    {
        Position = position;
        IsPerceptiblyActive = true;
    }

    public void ClearPerceptibleActivity() => IsPerceptiblyActive = false;
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
