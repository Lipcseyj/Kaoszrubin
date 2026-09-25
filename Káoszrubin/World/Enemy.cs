using System.Text;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.World;

public enum EnemyMovementProfile { Wander, Stationary, Patrol }
public enum EnemyPursuitState { Undecided, Pursuing, Declined }
public enum EnemyGroupRole { Member, Leader }
public enum EnemyAlertness { Sleeping, Drowsy, Alert }
public enum EnemySearchRole { None, Scout, Returning, Guarding }

public sealed record EnemyEquipmentSelection(string? WeaponId, string? ShieldId);

public abstract class Enemy(Position position) : WorldObject(position)
{
    public const string HordeGroupPrefix = "HORDE:";
    public const int MinimumPursuitMemoryMoves = 8;
    public const int MaximumPursuitMemoryMoves = 12;
    public const int PursuitPathFailureTolerance = 3;
    public const int SearchCohesionRadius = 6;
    public const int SearchGuardRadius = 2;
    public const int MinimumSearchMoves = 30;
    public const int MaximumSearchMoves = 120;
    public abstract EnemyDefinition Definition { get; }
    public IReadOnlyList<WeaponDefinition> AttackWeapons { get; protected init; } = [];
    public WeaponDefinition? EquippedWeapon { get; protected init; }
    public WeaponDefinition? EquippedShield { get; protected init; }
    public string LongName => Definition.ChoosesWeapon && EquippedWeapon is { } weapon
        ? (EquippedShield is { } shield ? $"{Definition.Name} ({weapon.Name} + {shield.Name})" : $"{Definition.Name} ({weapon.Name})")
        : Definition.Name;
    public string Name => Definition.ChoosesWeapon && EquippedWeapon is { } weapon
        ? (EquippedShield is { } shield ? $"{Definition.Name} ({weapon.GetIcon()} + {shield.GetIcon()})" : $"{Definition.Name} ({weapon.GetIcon()})")
        : Definition.Name;
    public string ShortName => Definition.Name;
    public int CurrentHitPoints { get; private set; }
    /// <summary>A példány saját kulcsboss-bónusza; normál és miniboss ellenfélnél nulla.</summary>
    public int BossHitPointBonusPercent { get; private set; }
    public int BossTier => BossHitPointBonusPercent == 0 ? 0 : BossTierRules.TierForBonus(BossHitPointBonusPercent);
    public int MaximumHitPoints => Definition.HitPoints is { } baseHitPoints
        ? (int)Math.Ceiling(baseHitPoints * (100 + BossHitPointBonusPercent) / 100.0)
        : CurrentHitPoints;
    public int EffectiveStrength => (Definition.Strength ?? 1) + (BossTier > 0 ? 1 : 0);
    public EnemyMovementProfile MovementProfile { get; private set; } = EnemyMovementProfile.Wander;
    public Direction PatrolDirection { get; private set; } = Direction.Right;
    public EnemyPursuitState PursuitState { get; private set; } = EnemyPursuitState.Undecided;
    public CharacterId? PursuitTargetCharacterId { get; private set; }
    public int PursuitMemoryRemainingMoves { get; private set; }
    public EnemyAlertness Alertness { get; private set; } = EnemyAlertness.Alert;
    public EnemySearchRole SearchRole { get; private set; }
    public Position HomePosition { get; private set; } = position;
    public Position? LastKnownTargetPosition { get; private set; }
    public Direction? LastKnownTargetDirection { get; private set; }
    public int ConsecutivePursuitPathFailures { get; private set; }
    public int ReactionDelayMovesRemaining { get; private set; }
    public int SearchMovesRemaining { get; private set; }
    public int ReturnDelayMovesRemaining { get; private set; }
    public Position? SearchAnchorPosition { get; private set; }
    private readonly HashSet<Position> _searchVisitedPositions = [];
    public IReadOnlySet<Position> SearchVisitedPositions => _searchVisitedPositions;
    public string? GroupId { get; private set; }
    public EnemyGroupRole GroupRole { get; private set; } = EnemyGroupRole.Member;
    public WorldEntityId? SummonerId { get; private set; }
    public bool GrantsRewardsAndLoot { get; private set; } = true;
    public bool IsRoamingHordeMember => GroupId?.StartsWith(HordeGroupPrefix,
        StringComparison.Ordinal) == true;
    public Position? HordeDestination { get; private set; }
    public DateTime? HordeCampUntilUtc { get; private set; }
    private readonly List<ActiveSpellEffect> _activeSpellEffects = [];
    private int _spellActionCounter;
    public IReadOnlyList<ActiveSpellEffect> ActiveSpellEffects => _activeSpellEffects;
    public bool IsPerceptiblyActive { get; private set; }
    public IReadOnlyList<string> GuaranteedLootIds => _guaranteedLootIds;
    private readonly List<string> _guaranteedLootIds = [];
    private readonly Dictionary<string, int> _abilityCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _weaponCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _spellCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _remainingAbilityCharges = new(StringComparer.OrdinalIgnoreCase);
    private int _regenerationSuppressedTurns;
    public IReadOnlyDictionary<string, int> AbilityCooldowns => _abilityCooldowns;
    public IReadOnlyDictionary<string, int> WeaponCooldowns => _weaponCooldowns;
    public IReadOnlyDictionary<string, int> SpellCooldowns => _spellCooldowns;
    public int MaximumMana => Definition.SpellcasterProfile?.MaximumMana ?? 0;
    public int CurrentMana { get; private set; }
    public IReadOnlyDictionary<string, int> RemainingAbilityCharges => _remainingAbilityCharges;
    public string? PreparedWeaponId { get; private set; }
    public string? PreparedAbilityId { get; private set; }
    public int PreparedAbilityTurnsRemaining { get; private set; }
    public Position? PreparedAbilityTargetPosition { get; private set; }
    public bool PreparedAbilityRequiresHeavyStagger { get; private set; }
    public IReadOnlyList<string> CarriedWeaponIds
    {
        get
        {
            IEnumerable<string> weaponIds = Definition.ChoosesWeapon
                ? EquippedWeapon is { IsMonsterOnly: false } selected
                    ? [selected.Id]
                    : []
                : AttackWeapons
                    .Where(weapon => !weapon.IsMonsterOnly)
                    .Select(weapon => weapon.Id);

            if (EquippedShield is { IsMonsterOnly: false } shield)
                weaponIds = weaponIds.Append(shield.Id);

            return weaponIds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    protected void InitializeHitPoints(int hitPoints) => CurrentHitPoints = Math.Max(0, hitPoints);
    public void SetCurrentHitPoints(int hitPoints) => CurrentHitPoints = Math.Max(0, hitPoints);
    public void ReceiveSpellDamage(int damage) => SetCurrentHitPoints(CurrentHitPoints - Math.Max(0, damage));
    public int RestoreHitPoints(int amount)
    {
        var before = CurrentHitPoints;
        CurrentHitPoints = Math.Min(MaximumHitPoints, CurrentHitPoints + Math.Max(0, amount));
        return CurrentHitPoints - before;
    }

    public void SuppressRegeneration(DamageType damageType)
    {
        if (damageType is DamageType.Fire or DamageType.Acid)
            _regenerationSuppressedTurns = Math.Max(_regenerationSuppressedTurns, 1);
    }

    public bool ConsumeRegenerationSuppression()
    {
        if (_regenerationSuppressedTurns <= 0) return false;
        _regenerationSuppressedTurns--;
        return true;
    }

    public bool IsAbilityReady(string abilityId) => _abilityCooldowns.GetValueOrDefault(abilityId) <= 0;
    public bool IsAbilityReady(MonsterAbilityDefinition ability) => IsAbilityReady(ability.Id) &&
        (string.IsNullOrWhiteSpace(ability.AbilityGroup) ||
         _abilityCooldowns.GetValueOrDefault(AbilityGroupCooldownId(ability.AbilityGroup)) <= 0);
    public bool IsWeaponReady(string weaponId) => _weaponCooldowns.GetValueOrDefault(weaponId) <= 0;
    public void StartAbilityCooldown(string abilityId, int turns) => SetCooldown(_abilityCooldowns, abilityId, turns);
    public void StartAbilityCooldown(MonsterAbilityDefinition ability)
    {
        StartAbilityCooldown(ability.Id, ability.Cooldown);
        if (!string.IsNullOrWhiteSpace(ability.AbilityGroup))
            SetCooldown(_abilityCooldowns, AbilityGroupCooldownId(ability.AbilityGroup), ability.Cooldown);
    }
    public void StartWeaponCooldown(string weaponId, int turns) => SetCooldown(_weaponCooldowns, weaponId, turns);
    public bool IsSpellReady(string spellId) => _spellCooldowns.GetValueOrDefault(spellId) <= 0;
    public void StartSpellCooldown(string spellId, int turns) => SetCooldown(_spellCooldowns, spellId, turns);
    public bool SpendMana(int amount)
    {
        if (amount < 0 || CurrentMana < amount) return false;
        CurrentMana -= amount;
        return true;
    }
    public void InitializeSpellcasting() => CurrentMana = MaximumMana;
    public void RestoreSpellcasting(int currentMana, IEnumerable<KeyValuePair<string, int>>? spellCooldowns)
    {
        CurrentMana = Math.Clamp(currentMana, 0, MaximumMana);
        _spellCooldowns.Clear();
        foreach (var item in spellCooldowns?.Where(item => item.Value > 0) ?? [])
            _spellCooldowns[item.Key] = item.Value;
    }
    public bool IsWeaponPrepared(string weaponId) =>
        string.Equals(PreparedWeaponId, weaponId, StringComparison.OrdinalIgnoreCase);
    public void PrepareWeapon(string weaponId) => PreparedWeaponId = weaponId;
    public void ClearPreparedWeapon() => PreparedWeaponId = null;
    public bool IsAbilityPrepared(string abilityId) =>
        string.Equals(PreparedAbilityId, abilityId, StringComparison.OrdinalIgnoreCase);
    public bool IsPreparedAbilityReady(string abilityId) =>
        IsAbilityPrepared(abilityId) && PreparedAbilityTurnsRemaining <= 0;
    public void PrepareAbility(string abilityId, int turns, Position? targetPosition = null,
        bool requiresHeavyStagger = false)
    {
        PreparedAbilityId = abilityId;
        PreparedAbilityTurnsRemaining = Math.Max(1, turns);
        PreparedAbilityTargetPosition = targetPosition;
        PreparedAbilityRequiresHeavyStagger = requiresHeavyStagger;
    }
    public void ClearPreparedAbility()
    {
        PreparedAbilityId = null;
        PreparedAbilityTurnsRemaining = 0;
        PreparedAbilityTargetPosition = null;
        PreparedAbilityRequiresHeavyStagger = false;
    }
    public void PrepareAbilityCharges(IEnumerable<MonsterAbilityDefinition> abilities)
    {
        _remainingAbilityCharges.Clear();
        foreach (var ability in abilities.Where(ability => ability.ChargesPerBattle > 0))
            _remainingAbilityCharges[ability.Id] = ability.ChargesPerBattle;
    }
    public bool HasAbilityCharge(MonsterAbilityDefinition ability) =>
        ability.ChargesPerBattle <= 0 || _remainingAbilityCharges.GetValueOrDefault(ability.Id) > 0;
    public void ConsumeAbilityCharge(MonsterAbilityDefinition ability)
    {
        if (ability.ChargesPerBattle <= 0) return;
        var remaining = _remainingAbilityCharges.GetValueOrDefault(ability.Id);
        if (remaining > 0) _remainingAbilityCharges[ability.Id] = remaining - 1;
    }
    public void RestoreCombatCooldowns(IEnumerable<KeyValuePair<string, int>> abilityCooldowns,
        IEnumerable<KeyValuePair<string, int>> weaponCooldowns, string? preparedWeaponId = null,
        IEnumerable<KeyValuePair<string, int>>? remainingAbilityCharges = null,
        string? preparedAbilityId = null, int preparedAbilityTurnsRemaining = 0,
        Position? preparedAbilityTargetPosition = null, bool preparedAbilityRequiresHeavyStagger = false)
    {
        _abilityCooldowns.Clear();
        _weaponCooldowns.Clear();
        foreach (var item in abilityCooldowns.Where(item => item.Value > 0)) _abilityCooldowns[item.Key] = item.Value;
        foreach (var item in weaponCooldowns.Where(item => item.Value > 0)) _weaponCooldowns[item.Key] = item.Value;
        PreparedWeaponId = string.IsNullOrWhiteSpace(preparedWeaponId) ? null : preparedWeaponId;
        PreparedAbilityId = string.IsNullOrWhiteSpace(preparedAbilityId) ? null : preparedAbilityId;
        PreparedAbilityTurnsRemaining = PreparedAbilityId is null ? 0 : Math.Max(0, preparedAbilityTurnsRemaining);
        PreparedAbilityTargetPosition = PreparedAbilityId is null ? null : preparedAbilityTargetPosition;
        PreparedAbilityRequiresHeavyStagger = PreparedAbilityId is not null && preparedAbilityRequiresHeavyStagger;
        _remainingAbilityCharges.Clear();
        foreach (var item in remainingAbilityCharges?.Where(item => item.Value >= 0) ?? [])
            _remainingAbilityCharges[item.Key] = item.Value;
    }

    public void AdvanceCombatCooldowns()
    {
        AdvanceCooldowns(_abilityCooldowns);
        AdvanceCooldowns(_weaponCooldowns);
        AdvanceCooldowns(_spellCooldowns);
        if (PreparedAbilityId is not null && PreparedAbilityTurnsRemaining > 0)
            PreparedAbilityTurnsRemaining--;
    }

    private static string AbilityGroupCooldownId(string group) => $"@group:{group.Trim()}";

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
    public int EffectiveSpeed => Math.Max(0, (Definition.Speed ?? 1) + (BossTier > 0 ? 1 : 0) -
        _activeSpellEffects.Where(effect => effect.Type is ActiveSpellEffectType.SpeedPenalty or ActiveSpellEffectType.Frost)
            .Sum(effect => effect.Value));
    public int SpellEffectValue(ActiveSpellEffectType type) => _activeSpellEffects
        .Where(effect => effect.Type == type).Sum(effect => effect.Value);

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
            if (effect.RemainingRounds <= 0) continue;
            var remaining = effect.RemainingRounds - 1;
            if (remaining == 0) _activeSpellEffects.RemoveAt(index);
            else _activeSpellEffects[index] = effect with { RemainingRounds = remaining };
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
            ? Math.Clamp(pursuitMemoryRemainingMoves, MinimumPursuitMemoryMoves, MaximumPursuitMemoryMoves)
            : MinimumPursuitMemoryMoves;
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
        PursuitMemoryRemainingMoves = pursue && targetCharacterId is not null ? MinimumPursuitMemoryMoves : 0;
    }

    public void RefreshPursuitMemory(int moves = MinimumPursuitMemoryMoves) =>
        PursuitMemoryRemainingMoves = Math.Clamp(moves, MinimumPursuitMemoryMoves, MaximumPursuitMemoryMoves);

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
        LastKnownTargetDirection = null;
        ConsecutivePursuitPathFailures = 0;
        ReactionDelayMovesRemaining = 0;
        SearchMovesRemaining = 0;
        ReturnDelayMovesRemaining = 0;
        SearchAnchorPosition = null;
        _searchVisitedPositions.Clear();
        SearchRole = EnemySearchRole.None;
    }

    public bool CanSleep => Definition.CanSleep;

    public int EffectiveVisionRange => Math.Max(1, (Alertness switch
    {
        EnemyAlertness.Sleeping => 1,
        EnemyAlertness.Drowsy => Math.Max(1, Definition.VisionRange / 2),
        _ => Definition.VisionRange
    }) + SpellEffectValue(ActiveSpellEffectType.VisionBonus));

    public void ConfigureAwareness(EnemyAlertness alertness, Position? homePosition = null,
        EnemySearchRole searchRole = EnemySearchRole.None, Position? lastKnownTargetPosition = null,
        int reactionDelayMovesRemaining = 0, int searchMovesRemaining = 0,
        int returnDelayMovesRemaining = 0, Direction? lastKnownTargetDirection = null,
        int consecutivePursuitPathFailures = 0, Position? searchAnchorPosition = null,
        IReadOnlyList<Position>? searchVisitedPositions = null)
    {
        Alertness = CanSleep ? alertness : EnemyAlertness.Alert;
        HomePosition = homePosition ?? Position;
        SearchRole = searchRole;
        LastKnownTargetPosition = lastKnownTargetPosition;
        LastKnownTargetDirection = lastKnownTargetDirection;
        ConsecutivePursuitPathFailures = Math.Clamp(consecutivePursuitPathFailures, 0,
            PursuitPathFailureTolerance);
        ReactionDelayMovesRemaining = Math.Max(0, reactionDelayMovesRemaining);
        SearchMovesRemaining = Math.Clamp(searchMovesRemaining, 0, MaximumSearchMoves);
        ReturnDelayMovesRemaining = Math.Max(0, returnDelayMovesRemaining);
        SearchAnchorPosition = searchAnchorPosition;
        _searchVisitedPositions.Clear();
        foreach (var visited in searchVisitedPositions ?? []) _searchVisitedPositions.Add(visited);
    }

    public void BeginPursuit(CharacterId targetCharacterId, Position lastKnownPosition, int reactionDelay,
        int pursuitMemoryMoves = MinimumPursuitMemoryMoves)
    {
        var previousKnownPosition = PursuitTargetCharacterId == targetCharacterId
            ? LastKnownTargetPosition
            : null;
        PursuitState = EnemyPursuitState.Pursuing;
        PursuitTargetCharacterId = targetCharacterId;
        LastKnownTargetPosition = lastKnownPosition;
        LastKnownTargetDirection = ObservedDirection(previousKnownPosition, lastKnownPosition) ??
                                   (previousKnownPosition is null ? null : LastKnownTargetDirection);
        RefreshPursuitMemory(pursuitMemoryMoves);
        ConsecutivePursuitPathFailures = 0;
        ReactionDelayMovesRemaining = Math.Max(0, reactionDelay);
        SearchMovesRemaining = 0;
        ReturnDelayMovesRemaining = 0;
        SearchAnchorPosition = null;
        _searchVisitedPositions.Clear();
        SearchRole = EnemySearchRole.None;
        Alertness = EnemyAlertness.Alert;
        HordeDestination = null;
        HordeCampUntilUtc = null;
    }

    public void RefreshKnownTarget(Position position, int pursuitMemoryMoves = MinimumPursuitMemoryMoves)
    {
        LastKnownTargetDirection = ObservedDirection(LastKnownTargetPosition, position) ?? LastKnownTargetDirection;
        LastKnownTargetPosition = position;
        RefreshPursuitMemory(pursuitMemoryMoves);
        ConsecutivePursuitPathFailures = 0;
    }

    public void AdvancePredictedTarget(Position position) => LastKnownTargetPosition = position;

    public bool RegisterPursuitPathFailure()
    {
        ConsecutivePursuitPathFailures++;
        return ConsecutivePursuitPathFailures >= PursuitPathFailureTolerance;
    }

    public void ResetPursuitPathFailures() => ConsecutivePursuitPathFailures = 0;

    public bool ConsumeReactionDelay()
    {
        if (ReactionDelayMovesRemaining <= 0) return false;
        ReactionDelayMovesRemaining--;
        return true;
    }

    public void BeginSearch(int moves, Position anchorPosition, EnemySearchRole role)
    {
        if (role is not (EnemySearchRole.Scout or EnemySearchRole.Guarding))
            throw new ArgumentOutOfRangeException(nameof(role));
        PursuitState = EnemyPursuitState.Undecided;
        PursuitTargetCharacterId = null;
        PursuitMemoryRemainingMoves = 0;
        ReactionDelayMovesRemaining = 0;
        SearchRole = role;
        SearchMovesRemaining = Math.Clamp(moves, MinimumSearchMoves, MaximumSearchMoves);
        ReturnDelayMovesRemaining = 0;
        SearchAnchorPosition = anchorPosition;
        _searchVisitedPositions.Clear();
        _searchVisitedPositions.Add(Position);
        ConsecutivePursuitPathFailures = 0;
    }

    public void BeginReturn(int delayMoves)
    {
        PursuitState = EnemyPursuitState.Undecided;
        PursuitTargetCharacterId = null;
        PursuitMemoryRemainingMoves = 0;
        ReactionDelayMovesRemaining = 0;
        SearchRole = EnemySearchRole.Returning;
        SearchMovesRemaining = 0;
        ReturnDelayMovesRemaining = Math.Max(0, delayMoves);
        SearchAnchorPosition = null;
        _searchVisitedPositions.Clear();
        ConsecutivePursuitPathFailures = 0;
    }

    public bool ConsumeReturnDelay()
    {
        if (ReturnDelayMovesRemaining <= 0) return false;
        ReturnDelayMovesRemaining--;
        return true;
    }

    public bool ConsumeSearchMove()
    {
        if (SearchMovesRemaining > 0) SearchMovesRemaining--;
        return SearchMovesRemaining > 0;
    }

    public void RecordSearchVisit(Position position) => _searchVisitedPositions.Add(position);

    public void CompleteReturn()
    {
        ResetPursuit();
        Alertness = CanSleep ? EnemyAlertness.Drowsy : EnemyAlertness.Alert;
    }

    public void RememberTravelDirection(Direction direction) => PatrolDirection = direction;

    private static Direction? ObservedDirection(Position? previous, Position current)
    {
        if (previous is not { } old) return null;
        var deltaX = current.X - old.X;
        var deltaY = current.Y - old.Y;
        if (deltaX == 0 && deltaY == 0) return null;
        if (Math.Abs(deltaX) >= Math.Abs(deltaY)) return deltaX > 0 ? Direction.Right : Direction.Left;
        return deltaY > 0 ? Direction.Down : Direction.Up;
    }
    public void ConfigureGroup(string? groupId, EnemyGroupRole role = EnemyGroupRole.Member)
    {
        GroupId = string.IsNullOrWhiteSpace(groupId) ? null : groupId;
        GroupRole = role;
    }

    public void ConfigureSummon(WorldEntityId summonerId, bool grantsRewardsAndLoot)
    {
        SummonerId = summonerId;
        GrantsRewardsAndLoot = grantsRewardsAndLoot;
    }

    public void SetHordeDestination(Position destination)
    {
        if (!IsRoamingHordeMember) throw new InvalidOperationException("Csak hordatag kaphat közös vándorlási célt.");
        HordeDestination = destination;
        HordeCampUntilUtc = null;
    }

    public void BeginHordeCamp(DateTime untilUtc)
    {
        if (!IsRoamingHordeMember) throw new InvalidOperationException("Csak hordatag táborozhat hordaként.");
        HordeDestination = null;
        HordeCampUntilUtc = untilUtc.Kind == DateTimeKind.Utc ? untilUtc : untilUtc.ToUniversalTime();
    }

    public void RestoreHordeRoaming(Position? destination, DateTime? campUntilUtc)
    {
        if (!IsRoamingHordeMember) return;
        HordeDestination = destination;
        HordeCampUntilUtc = campUntilUtc;
    }

    public void ShiftHordeCamp(TimeSpan duration)
    {
        if (HordeCampUntilUtc is { } until) HordeCampUntilUtc = until + duration;
    }
    public void ConfigureGuaranteedLoot(IEnumerable<string> itemIds)
    {
        _guaranteedLootIds.Clear();
        _guaranteedLootIds.AddRange(itemIds.Where(id => !string.IsNullOrWhiteSpace(id)));
    }

    /// <summary>Új kulcsboss bónuszát állítja be, vagy mentésből állítja vissza.</summary>
    public void ConfigureBossHitPointBonus(int bonusPercent)
    {
        if (!Definition.IsBoss || Definition.Rank != EnemyRank.Boss)
        {
            if (bonusPercent != 0) throw new InvalidOperationException("Csak kulcsboss kaphat bossbónuszt.");
            return;
        }
        if (bonusPercent != 0) BossTierRules.TierForBonus(bonusPercent);
        BossHitPointBonusPercent = bonusPercent;
        if (CurrentHitPoints > MaximumHitPoints) CurrentHitPoints = MaximumHitPoints;
    }

    public void MoveTo(Position position)
    {
        SetPosition(position);
        IsPerceptiblyActive = true;
    }

    public void ClearPerceptibleActivity() => IsPerceptiblyActive = false;
}

/// <summary>CSV-definícióból létrehozott, saját megjelenésű ellenfél.</summary>
public sealed class ConfiguredEnemy : Enemy
{
    private const int ShieldChancePercent = 50;

    public ConfiguredEnemy(
        Position position,
        EnemyDefinition definition,
        Random? random = null,
        EnemyEquipmentSelection? equipment = null,
        int? bossHitPointBonusPercent = null)
        : this(position, definition, random, equipment, null, bossHitPointBonusPercent)
    {
    }

    private ConfiguredEnemy(Position position, EnemyDefinition definition, Random? random,
        EnemyEquipmentSelection? equipment, string? legacySelectedWeaponId,
        int? bossHitPointBonusPercent) : base(position)
    {
        var rng = random ?? Random.Shared;
        var weapons = definition.Weapons ?? [];

        var oneHandedWeapons = weapons
            .Where(weapon => !weapon.IsTwoHanded)
            .ToArray();

        if (equipment is not null && definition.ChoosesWeapon != (equipment.WeaponId is not null))
            throw new ArgumentException(definition.ChoosesWeapon
                    ? "A fegyvert választó ellenfél mentett felszereléséből hiányzik a fegyver."
                    : "A támadásonként fegyvert választó ellenfélnek nem lehet felszerelt fegyvere.",
                nameof(equipment));

        var restoredWeaponId = equipment?.WeaponId ?? legacySelectedWeaponId;
        var restoredWeapon = restoredWeaponId is { Length: > 0 }
            ? weapons.FirstOrDefault(weapon =>
                string.Equals(
                    weapon.Id,
                    restoredWeaponId,
                    StringComparison.OrdinalIgnoreCase))
            : null;

        if (equipment is not null && restoredWeaponId is not null && restoredWeapon is null)
            throw new ArgumentException($"Az ellenfél nem használhatja a mentett fegyvert: {restoredWeaponId}.",
                nameof(equipment));

        // Mentésből visszaállított vagy explicit módon megadott fegyver
        // elsőbbséget élvez a véletlen választással szemben.
        var selectedWeapon = restoredWeapon;

        var canUseShield =
            definition.ShieldOption is not null &&
            oneHandedWeapons.Length > 0 &&
            selectedWeapon?.IsTwoHanded != true;

        var usesShield = equipment is not null
            ? equipment.ShieldId is not null
            : canUseShield && rng.Next(100) < ShieldChancePercent;

        if (equipment?.ShieldId is { } restoredShieldId &&
            !string.Equals(restoredShieldId, definition.ShieldOption?.Id, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Az ellenfél nem használhatja a mentett pajzsot: {restoredShieldId}.",
                nameof(equipment));

        if (usesShield && !canUseShield)
            throw new ArgumentException("Az ellenfél mentett fegyvere és pajzsa nem használható együtt.",
                nameof(equipment));

        var selectedShield = usesShield
            ? definition.ShieldOption
            : null;

        IReadOnlyList<WeaponDefinition> usableWeapons =
            usesShield
                ? oneHandedWeapons
                : weapons;

        if (definition.ChoosesWeapon && usableWeapons.Count > 0)
        {
            // Csak akkor sorsolunk, ha sem mentett, sem explicit fegyver nincs.
            if (selectedWeapon is null)
                selectedWeapon = usableWeapons[rng.Next(usableWeapons.Count)];

            selectedWeapon ??= usableWeapons[0];
        }

        Definition = definition;
        InitializeSpellcasting();
        AttackWeapons = usableWeapons;
        EquippedWeapon = definition.ChoosesWeapon ? selectedWeapon : null;
        EquippedShield = selectedShield;

        if (definition.IsBoss && definition.Rank == EnemyRank.Boss)
            ConfigureBossHitPointBonus(bossHitPointBonusPercent ?? rng.Next(10, 51));
        InitializeHitPoints(MaximumHitPoints);
    }

    public static ConfiguredEnemy RestoreLegacy(Position position, EnemyDefinition definition,
        string? selectedWeaponId, Random? random = null, int? bossHitPointBonusPercent = null) =>
        new(position, definition, random, null, selectedWeaponId, bossHitPointBonusPercent);

    public override EnemyDefinition Definition { get; }
    public override Rune Symbol => Rune.GetRuneAt(Definition.Appearance, 0);
}
