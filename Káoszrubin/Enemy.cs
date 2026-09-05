using System.Text;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin;

public enum EnemyMovementProfile { Wander, Stationary, Patrol }
public enum EnemyPursuitState { Undecided, Pursuing, Declined }
public enum EnemyGroupRole { Member, Leader }
public enum EnemyAlertness { Sleeping, Drowsy, Alert }
public enum EnemySearchRole { None, Scout, Returning }

public abstract class Enemy(Position position) : WorldObject(position)
{
    public const int MinimumSearchMoves = 30;
    public const int MaximumSearchMoves = 120;
    public abstract EnemyDefinition Definition { get; }
    public string Name => Definition.ChoosesWeapon && Definition.Weapon is { } weapon
        ? $"{Definition.Name} ({weapon.Name})"
        : Definition.Name;
    public int CurrentHitPoints { get; private set; }
    public EnemyMovementProfile MovementProfile { get; private set; } = EnemyMovementProfile.Wander;
    public Direction PatrolDirection { get; private set; } = Direction.Right;
    public EnemyPursuitState PursuitState { get; private set; } = EnemyPursuitState.Undecided;
    public CharacterId? PursuitTargetCharacterId { get; private set; }
    public int PursuitMemoryRemainingMoves { get; private set; }
    public EnemyAlertness Alertness { get; private set; } = EnemyAlertness.Alert;
    public EnemySearchRole SearchRole { get; private set; }
    public Position HomePosition { get; private set; } = position;
    public Position? LastKnownTargetPosition { get; private set; }
    public int ReactionDelayMovesRemaining { get; private set; }
    public int SearchMovesRemaining { get; private set; }
    public int ReturnDelayMovesRemaining { get; private set; }
    public string? GroupId { get; private set; }
    public EnemyGroupRole GroupRole { get; private set; } = EnemyGroupRole.Member;
    private readonly List<ActiveSpellEffect> _activeSpellEffects = [];
    private int _spellActionCounter;
    public IReadOnlyList<ActiveSpellEffect> ActiveSpellEffects => _activeSpellEffects;
    public bool IsPerceptiblyActive { get; private set; }
    public IReadOnlyList<string> GuaranteedLootIds => _guaranteedLootIds;
    private readonly List<string> _guaranteedLootIds = [];
    private readonly Dictionary<string, int> _abilityCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _weaponCooldowns = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, int> AbilityCooldowns => _abilityCooldowns;
    public IReadOnlyDictionary<string, int> WeaponCooldowns => _weaponCooldowns;
    public string? PreparedWeaponId { get; private set; }
    public IReadOnlyList<string> CarriedWeaponIds
    {
        get
        {
            if (Definition.ChoosesWeapon)
                return Definition.Weapon is { IsMonsterOnly: false } selected ? [selected.Id] : [];
            return (Definition.Weapons ?? []).Where(weapon => !weapon.IsMonsterOnly)
                .Select(weapon => weapon.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    protected void InitializeHitPoints(int hitPoints) => CurrentHitPoints = Math.Max(0, hitPoints);
    public void SetCurrentHitPoints(int hitPoints) => CurrentHitPoints = Math.Max(0, hitPoints);
    public void ReceiveSpellDamage(int damage) => SetCurrentHitPoints(CurrentHitPoints - Math.Max(0, damage));
    public int RestoreHitPoints(int amount)
    {
        var before = CurrentHitPoints;
        CurrentHitPoints = Math.Min(Definition.HitPoints ?? CurrentHitPoints, CurrentHitPoints + Math.Max(0, amount));
        return CurrentHitPoints - before;
    }

    public bool IsAbilityReady(string abilityId) => _abilityCooldowns.GetValueOrDefault(abilityId) <= 0;
    public bool IsWeaponReady(string weaponId) => _weaponCooldowns.GetValueOrDefault(weaponId) <= 0;
    public void StartAbilityCooldown(string abilityId, int turns) => SetCooldown(_abilityCooldowns, abilityId, turns);
    public void StartWeaponCooldown(string weaponId, int turns) => SetCooldown(_weaponCooldowns, weaponId, turns);
    public bool IsWeaponPrepared(string weaponId) =>
        string.Equals(PreparedWeaponId, weaponId, StringComparison.OrdinalIgnoreCase);
    public void PrepareWeapon(string weaponId) => PreparedWeaponId = weaponId;
    public void ClearPreparedWeapon() => PreparedWeaponId = null;
    public void RestoreCombatCooldowns(IEnumerable<KeyValuePair<string, int>> abilityCooldowns,
        IEnumerable<KeyValuePair<string, int>> weaponCooldowns, string? preparedWeaponId = null)
    {
        _abilityCooldowns.Clear();
        _weaponCooldowns.Clear();
        foreach (var item in abilityCooldowns.Where(item => item.Value > 0)) _abilityCooldowns[item.Key] = item.Value;
        foreach (var item in weaponCooldowns.Where(item => item.Value > 0)) _weaponCooldowns[item.Key] = item.Value;
        PreparedWeaponId = string.IsNullOrWhiteSpace(preparedWeaponId) ? null : preparedWeaponId;
    }

    public void AdvanceCombatCooldowns()
    {
        AdvanceCooldowns(_abilityCooldowns);
        AdvanceCooldowns(_weaponCooldowns);
    }

    private static void SetCooldown(IDictionary<string, int> cooldowns, string id, int turns)
    {
        if (turns > 0) cooldowns[id] = turns;
        else cooldowns.Remove(id);
    }

    private static void AdvanceCooldowns(IDictionary<string, int> cooldowns)
    {
        foreach (var id in cooldowns.Keys.ToArray())
        {
            var remaining = cooldowns[id] - 1;
            if (remaining <= 0) cooldowns.Remove(id);
            else cooldowns[id] = remaining;
        }
    }
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
        PursuitState = pursuitState == EnemyPursuitState.Declined
            ? EnemyPursuitState.Undecided
            : pursuitState;
        PursuitTargetCharacterId = pursuitTargetCharacterId;
        PursuitMemoryRemainingMoves = pursuitTargetCharacterId is null ? 0 : pursuitMemoryRemainingMoves >= 0
            ? pursuitMemoryRemainingMoves
            : MinimumSearchMoves;
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
        PursuitState = pursue ? EnemyPursuitState.Pursuing : EnemyPursuitState.Undecided;
        PursuitTargetCharacterId = pursue ? targetCharacterId : null;
        PursuitMemoryRemainingMoves = pursue && targetCharacterId is not null ? MinimumSearchMoves : 0;
    }

    public void RefreshPursuitMemory() => PursuitMemoryRemainingMoves = MinimumSearchMoves;

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
        LastKnownTargetPosition = null;
        ReactionDelayMovesRemaining = 0;
        SearchMovesRemaining = 0;
        ReturnDelayMovesRemaining = 0;
        SearchRole = EnemySearchRole.None;
    }

    public bool CanSleep => Definition.CanSleep;

    public int EffectiveVisionRange => Alertness switch
    {
        EnemyAlertness.Sleeping => 1,
        EnemyAlertness.Drowsy => Math.Max(1, Definition.VisionRange / 2),
        _ => Definition.VisionRange
    };

    public void ConfigureAwareness(EnemyAlertness alertness, Position? homePosition = null,
        EnemySearchRole searchRole = EnemySearchRole.None, Position? lastKnownTargetPosition = null,
        int reactionDelayMovesRemaining = 0, int searchMovesRemaining = 0,
        int returnDelayMovesRemaining = 0)
    {
        Alertness = CanSleep ? alertness : EnemyAlertness.Alert;
        HomePosition = homePosition ?? Position;
        SearchRole = searchRole;
        LastKnownTargetPosition = lastKnownTargetPosition;
        ReactionDelayMovesRemaining = Math.Max(0, reactionDelayMovesRemaining);
        SearchMovesRemaining = Math.Clamp(searchMovesRemaining, 0, MaximumSearchMoves);
        ReturnDelayMovesRemaining = Math.Max(0, returnDelayMovesRemaining);
    }

    public void BeginPursuit(CharacterId targetCharacterId, Position lastKnownPosition, int reactionDelay)
    {
        PursuitState = EnemyPursuitState.Pursuing;
        PursuitTargetCharacterId = targetCharacterId;
        LastKnownTargetPosition = lastKnownPosition;
        ReactionDelayMovesRemaining = Math.Max(0, reactionDelay);
        SearchMovesRemaining = 0;
        ReturnDelayMovesRemaining = 0;
        SearchRole = EnemySearchRole.None;
        Alertness = EnemyAlertness.Alert;
    }

    public void RefreshKnownTarget(Position position)
    {
        LastKnownTargetPosition = position;
    }

    public bool ConsumeReactionDelay()
    {
        if (ReactionDelayMovesRemaining <= 0) return false;
        ReactionDelayMovesRemaining--;
        return true;
    }

    public void BeginSearch(int moves)
    {
        PursuitState = EnemyPursuitState.Undecided;
        PursuitTargetCharacterId = null;
        ReactionDelayMovesRemaining = 0;
        SearchRole = EnemySearchRole.Scout;
        SearchMovesRemaining = Math.Clamp(moves, MinimumSearchMoves, MaximumSearchMoves);
        ReturnDelayMovesRemaining = 0;
    }

    public void BeginReturn(int delayMoves)
    {
        PursuitState = EnemyPursuitState.Undecided;
        PursuitTargetCharacterId = null;
        ReactionDelayMovesRemaining = 0;
        SearchRole = EnemySearchRole.Returning;
        SearchMovesRemaining = 0;
        ReturnDelayMovesRemaining = Math.Max(0, delayMoves);
    }

    public bool ConsumeReturnDelay()
    {
        if (ReturnDelayMovesRemaining <= 0) return false;
        ReturnDelayMovesRemaining--;
        return true;
    }

    public bool RecordSearchStep()
    {
        if (SearchMovesRemaining > 0) SearchMovesRemaining--;
        return SearchMovesRemaining > 0;
    }

    public void CompleteReturn()
    {
        ResetPursuit();
        Alertness = CanSleep ? EnemyAlertness.Drowsy : EnemyAlertness.Alert;
    }

    public void RememberTravelDirection(Direction direction) => PatrolDirection = direction;
    public void ConfigureGroup(string? groupId, EnemyGroupRole role = EnemyGroupRole.Member)
    {
        GroupId = string.IsNullOrWhiteSpace(groupId) ? null : groupId;
        GroupRole = role;
    }
    public void ConfigureGuaranteedLoot(IEnumerable<string> itemIds)
    {
        _guaranteedLootIds.Clear();
        _guaranteedLootIds.AddRange(itemIds.Where(id => !string.IsNullOrWhiteSpace(id)));
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
    public ConfiguredEnemy(Position position, EnemyDefinition definition, Random? random = null,
        string? selectedWeaponId = null) : base(position)
    {
        var weapons = definition.Weapons ?? [];
        var selectedWeapon = definition.Weapon;
        if (definition.ChoosesWeapon && weapons.Count > 0)
        {
            if (selectedWeaponId is { Length: > 0 })
                selectedWeapon = weapons.FirstOrDefault(weapon => string.Equals(weapon.Id, selectedWeaponId,
                    StringComparison.OrdinalIgnoreCase));
            else if (selectedWeapon is null)
                selectedWeapon = weapons[(random ?? Random.Shared).Next(weapons.Count)];
            selectedWeapon ??= weapons[0];
        }
        Definition = definition with { Weapon = selectedWeapon };
        Symbol = Rune.GetRuneAt(definition.Appearance, 0);
        InitializeHitPoints(definition.HitPoints ?? 0);
    }

    public override EnemyDefinition Definition { get; }
    public override Rune Symbol { get; }
}
