using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Combat;

public sealed record TeamCharacterParticipant(
    LiveCharacter Character,
    Position Position,
    TacticalParticipantKind Kind,
    int Initiative,
    int MovementAllowance,
    int EligibleFromCycle,
    TeamCharacterBattleRuntime Runtime);

public sealed record TeamEnemyParticipant(
    Enemy Enemy,
    int Initiative,
    int MovementAllowance,
    int EligibleFromCycle);

public sealed record TeamBattleKill(
    CharacterId KillerId,
    string KillerName,
    string EnemyDefinitionId,
    string EnemyName,
    int AwardedExperience);

public sealed record TeamBattleCharacterResult(
    string Name,
    int VitalityLost,
    int ManaUsed,
    bool Fell,
    IReadOnlyList<string> GainedStatusIcons,
    int SpellsCast);

/// <summary>A játékvilág objektumait a tiszta taktikai körsorrendhez kapcsoló futásidejű összecsapás.</summary>
public sealed class TeamBattleEncounter
{
    public const int InactiveCycleLimit = 2;

    private readonly Dictionary<CombatantId, LiveCharacter> _characters = [];
    private readonly Dictionary<CombatantId, Enemy> _enemies = [];
    private readonly Dictionary<CharacterId, TeamCharacterBattleRuntime> _characterRuntimes = [];
    private readonly Dictionary<CharacterId, (int Vitality, int Mana)> _startingResources = [];
    private readonly Dictionary<CharacterId, HashSet<string>> _startingStatusIds = [];
    private readonly Dictionary<CharacterId, List<string>> _gainedStatusIcons = [];
    private readonly Dictionary<CharacterId, int> _spellCasts = [];
    private readonly HashSet<WorldEntityId> _resolvedEnemyDeaths = [];
    private readonly HashSet<CharacterId> _resolvedCharacterDeaths = [];
    private readonly HashSet<(CharacterId CharacterId, WorldEntityId EnemyId)> _engagements = [];
    private readonly HashSet<BattleSide> _activeSidesThisCycle = [];
    private readonly Dictionary<BattleSide, int> _inactiveCycleStreaks = Enum.GetValues<BattleSide>()
        .ToDictionary(side => side, _ => 0);
    private readonly List<TeamBattleKill> _kills = [];
    private int _queuedExtraActions;
    private int _reinforcementsCheckedThroughCycle;

    public TeamBattleEncounter(Position center,
        IEnumerable<TeamCharacterParticipant> characters,
        IEnumerable<TeamEnemyParticipant> enemies,
        CharacterId initiatingCharacterId,
        WorldEntityId initiatingEnemyId,
        bool enemyStrikesFirst = false,
        int radius = TacticalDistance.DefaultBattleRadius,
        int openingCycles = 1,
        PartyFormationSnapshot? formation = null)
    {
        var characterList = characters.ToList();
        var enemyList = enemies.ToList();
        if (characterList.All(value => value.Character.Id != initiatingCharacterId))
            throw new ArgumentException("A kezdeményező karakter nem résztvevő.", nameof(initiatingCharacterId));
        if (enemyList.All(value => value.Enemy.Id != initiatingEnemyId))
            throw new ArgumentException("A kezdeményező ellenfél nem résztvevő.", nameof(initiatingEnemyId));

        Id = BattleId.New();
        InitiatingCharacterId = initiatingCharacterId;
        InitiatingEnemyId = initiatingEnemyId;
        Formation = formation?.State == PartyFormationState.Locked ? formation : null;
        var tacticalParticipants = new List<TacticalBattleParticipant>();
        foreach (var participant in characterList)
        {
            var id = CombatantId.ForCharacter(participant.Character.Id);
            _characters.Add(id, participant.Character);
            _characterRuntimes.Add(participant.Character.Id, participant.Runtime);
            _startingResources.Add(participant.Character.Id,
                (participant.Character.CurrentVitality, participant.Character.CurrentMana));
            _startingStatusIds.Add(participant.Character.Id, participant.Character.Statuses
                .Select(status => status.Id).ToHashSet(StringComparer.OrdinalIgnoreCase));
            _gainedStatusIcons.Add(participant.Character.Id, []);
            _spellCasts.Add(participant.Character.Id, 0);
            tacticalParticipants.Add(new TacticalBattleParticipant(id, BattleSide.Friendly,
                participant.Kind, participant.Position, participant.Initiative,
                participant.MovementAllowance, participant.EligibleFromCycle,
                participant.EligibleFromCycle > 1 ? TacticalParticipantState.Approaching : TacticalParticipantState.Active));
        }
        foreach (var participant in enemyList)
        {
            var id = CombatantId.ForEnemy(participant.Enemy.Id);
            _enemies.Add(id, participant.Enemy);
            tacticalParticipants.Add(new TacticalBattleParticipant(id, BattleSide.Hostile,
                TacticalParticipantKind.Enemy, participant.Enemy.Position, participant.Initiative,
                participant.MovementAllowance, participant.EligibleFromCycle,
                participant.EligibleFromCycle > 1 ? TacticalParticipantState.Approaching : TacticalParticipantState.Active));
        }
        var initiatingCharacter = CombatantId.ForCharacter(initiatingCharacterId);
        var initiatingEnemy = CombatantId.ForEnemy(initiatingEnemyId);
        var openingOrder = enemyStrikesFirst
            ? new[] { initiatingEnemy, initiatingCharacter }
            : new[] { initiatingCharacter, initiatingEnemy };
        Turns = new TacticalBattleState(Id, center, tacticalParticipants, radius, openingCycles, openingOrder);
    }

    public BattleId Id { get; }
    public CharacterId InitiatingCharacterId { get; }
    public WorldEntityId InitiatingEnemyId { get; }
    public PartyFormationSnapshot? Formation { get; private set; }
    public bool HasActiveFormation => Formation is { State: PartyFormationState.Locked };
    public TacticalBattleState Turns { get; }
    public int ActionNumber { get; private set; }
    public IReadOnlySet<BattleSide> InactiveSidesLastCompletedCycle { get; private set; } = new HashSet<BattleSide>();
    public IReadOnlyCollection<LiveCharacter> Characters => _characters.Values;
    public IReadOnlyCollection<Enemy> Enemies => _enemies.Values;
    public IReadOnlyList<TeamBattleKill> Kills => _kills;
    public bool FriendlySideDefeated => _characters.Values.All(character => !character.IsAlive);
    public bool HostileSideDefeated => _enemies.Values.All(enemy => enemy.CurrentHitPoints <= 0);
    public bool IsCompleted => FriendlySideDefeated || HostileSideDefeated;
    public TacticalBattleParticipant Current => Turns.CurrentParticipant ?? Turns.StartTurns();
    public LiveCharacter? CurrentCharacter => _characters.GetValueOrDefault(Current.Id);
    public Enemy? CurrentEnemy => _enemies.GetValueOrDefault(Current.Id);
    public WorldEntityId? SelectedTargetEnemyId { get; private set; }

    public TeamCharacterBattleRuntime RuntimeFor(LiveCharacter character) =>
        _characterRuntimes[character.Id];

    public (int Vitality, int Mana) StartingResourcesFor(LiveCharacter character) =>
        _startingResources[character.Id];

    public TeamBattleCharacterResult ResultFor(LiveCharacter character)
    {
        CaptureNewStatuses();
        var starting = StartingResourcesFor(character);
        return new TeamBattleCharacterResult(character.Name,
            Math.Max(0, starting.Vitality - character.CurrentVitality),
            Math.Max(0, starting.Mana - character.CurrentMana), !character.IsAlive,
            _gainedStatusIcons[character.Id].ToArray(), _spellCasts[character.Id]);
    }

    public void CaptureNewStatuses()
    {
        foreach (var character in _characters.Values)
            foreach (var status in character.Statuses.Where(status =>
                         !_startingStatusIds[character.Id].Contains(status.Id)))
                if (!_gainedStatusIcons[character.Id].Contains(status.Icon, StringComparer.Ordinal))
                    _gainedStatusIcons[character.Id].Add(status.Icon);
    }

    public LiveCharacter? CharacterFor(CombatantId id) => _characters.GetValueOrDefault(id);
    public Enemy? EnemyFor(CombatantId id) => _enemies.GetValueOrDefault(id);
    public bool ContainsEnemy(Enemy enemy) => _enemies.ContainsKey(CombatantId.ForEnemy(enemy.Id));

    public FormationSlot? FormationSlotFor(LiveCharacter character)
    {
        if (Formation is not { } formation) return null;
        for (var index = 0; index < formation.Slots.Count; index++)
            if (formation.Slots[index] == character.Id) return (FormationSlot)index;
        return null;
    }

    public bool IsFrontRow(LiveCharacter character) => FormationSlotFor(character) is
        FormationSlot.FrontLeft or FormationSlot.FrontRight;

    public bool IsRearRow(LiveCharacter character) => FormationSlotFor(character) is
        FormationSlot.RearLeft or FormationSlot.RearRight;

    public bool CanUseItem(LiveCharacter character, MiscItemDefinition item) =>
        item.UsableInCombat && !IsEngaged(character) &&
        (!HasActiveFormation || IsRearRow(character));

    public LiveCharacter? RearPartnerOf(LiveCharacter character) => FormationSlotFor(character) switch
    {
        FormationSlot.FrontLeft => CharacterInSlot(FormationSlot.RearLeft),
        FormationSlot.FrontRight => CharacterInSlot(FormationSlot.RearRight),
        _ => null
    };

    public LiveCharacter? FrontPartnerOf(LiveCharacter character) => FormationSlotFor(character) switch
    {
        FormationSlot.RearLeft => CharacterInSlot(FormationSlot.FrontLeft),
        FormationSlot.RearRight => CharacterInSlot(FormationSlot.FrontRight),
        _ => null
    };

    public bool IsProtectedRearTarget(LiveCharacter character, Position attackerPosition)
    {
        if (!HasActiveFormation || !character.IsAlive || !IsRearRow(character) ||
            FrontPartnerOf(character) is not { IsAlive: true } protector || Formation is not { } formation)
            return false;
        var rearPosition = Turns.Find(CombatantId.ForCharacter(character.Id))?.Position;
        if (rearPosition is null || Turns.Find(CombatantId.ForCharacter(protector.Id)) is null) return false;
        var forward = formation.Facing switch
        {
            Direction.Up => new Position(0, -1),
            Direction.Right => new Position(1, 0),
            Direction.Down => new Position(0, 1),
            _ => new Position(-1, 0)
        };
        var attackerDelta = new Position(attackerPosition.X - rearPosition.Value.X,
            attackerPosition.Y - rearPosition.Value.Y);
        return attackerDelta.X * forward.X + attackerDelta.Y * forward.Y > 0;
    }

    public IReadOnlyList<Enemy> RearFormationEnemiesInReach(LiveCharacter character)
    {
        if (!IsRearRow(character) ||
            !character.WeaponSlots.Any(weapon => WeaponFamilies.ForWeapon(weapon) == WeaponFamilies.Polearm) ||
            FrontPartnerOf(character) is not { IsAlive: true } front)
            return [];
        return EngagedEnemies(front);
    }

    public IReadOnlyDictionary<LiveCharacter, Position> FormationDestinations(Direction direction)
    {
        if (!HasActiveFormation) return new Dictionary<LiveCharacter, Position>();
        return _characters.Values.Where(character => character.IsAlive && FormationSlotFor(character) is not null)
            .ToDictionary(character => character,
                character => Turns.Find(CombatantId.ForCharacter(character.Id))!.Position + direction);
    }

    public bool PreservesEngagements(IReadOnlyDictionary<LiveCharacter, Position> destinations) =>
        _engagements.All(pair =>
        {
            var character = _characters.Values.FirstOrDefault(value => value.Id == pair.CharacterId);
            var enemy = _enemies.Values.FirstOrDefault(value => value.Id == pair.EnemyId);
            return character is null || enemy is null || !character.IsAlive || enemy.CurrentHitPoints <= 0 ||
                   !destinations.TryGetValue(character, out var destination) ||
                   TacticalDistance.IsMeleeAdjacent(destination, enemy.Position);
        });

    public void UpdateFormationPositions(IReadOnlyDictionary<LiveCharacter, Position> destinations)
    {
        foreach (var (character, position) in destinations) UpdatePosition(character, position);
    }

    public bool TrySwapToRear(LiveCharacter front, out LiveCharacter? rear,
        out Position frontPosition, out Position rearPosition, out int transferredEngagements)
    {
        rear = RearPartnerOf(front);
        frontPosition = default;
        rearPosition = default;
        transferredEngagements = 0;
        if (!HasActiveFormation || rear is not { IsAlive: true } || Formation is not { } formation) return false;
        var frontParticipant = Turns.Find(CombatantId.ForCharacter(front.Id));
        var rearParticipant = Turns.Find(CombatantId.ForCharacter(rear.Id));
        if (frontParticipant is null || rearParticipant is null) return false;
        frontPosition = frontParticipant.Position;
        rearPosition = rearParticipant.Position;
        var slots = formation.Slots.ToArray();
        var frontIndex = Array.IndexOf(slots, front.Id);
        var rearIndex = Array.IndexOf(slots, rear.Id);
        if (frontIndex < 0 || rearIndex < 0) return false;
        (slots[frontIndex], slots[rearIndex]) = (slots[rearIndex], slots[frontIndex]);
        Formation = PartyFormationRules.WithSlots(formation, slots) with { State = PartyFormationState.Locked };
        UpdatePosition(front, rearPosition);
        UpdatePosition(rear, frontPosition);

        var transferred = _engagements.Where(pair => pair.CharacterId == front.Id).ToArray();
        foreach (var engagement in transferred)
        {
            _engagements.Remove(engagement);
            _engagements.Add((rear.Id, engagement.EnemyId));
        }
        transferredEngagements = transferred.Length;
        return true;
    }

    private LiveCharacter? CharacterInSlot(FormationSlot slot) => Formation?.CharacterAt(slot) is { } id
        ? _characters.Values.FirstOrDefault(character => character.Id == id)
        : null;

    public bool TryAddEnemy(TeamEnemyParticipant participant)
    {
        var id = CombatantId.ForEnemy(participant.Enemy.Id);
        if (_enemies.ContainsKey(id)) return false;
        var tactical = new TacticalBattleParticipant(id, BattleSide.Hostile, TacticalParticipantKind.Enemy,
            participant.Enemy.Position, participant.Initiative, participant.MovementAllowance,
            participant.EligibleFromCycle, TacticalParticipantState.Approaching);
        if (!Turns.TryAddParticipant(tactical)) return false;
        _enemies.Add(id, participant.Enemy);
        return true;
    }

    public bool BeginReinforcementCheckForCurrentCycle()
    {
        if (_reinforcementsCheckedThroughCycle >= Turns.Cycle) return false;
        _reinforcementsCheckedThroughCycle = Turns.Cycle;
        return true;
    }

    public bool TrySelectTarget(Enemy enemy)
    {
        if (!ContainsEnemy(enemy) || enemy.CurrentHitPoints <= 0) return false;
        SelectedTargetEnemyId = enemy.Id;
        return true;
    }

    public Enemy? SelectedTargetEnemy() => SelectedTargetEnemyId is { } id
        ? _enemies.Values.FirstOrDefault(enemy => enemy.Id == id && enemy.CurrentHitPoints > 0)
        : null;

    public void Engage(LiveCharacter character, Enemy enemy) => _engagements.Add((character.Id, enemy.Id));

    public bool IsEngaged(LiveCharacter character) => _engagements.Any(pair =>
        pair.CharacterId == character.Id &&
        _enemies.Values.Any(enemy => enemy.Id == pair.EnemyId && enemy.CurrentHitPoints > 0));

    public bool IsEngaged(Enemy enemy) => _engagements.Any(pair =>
        pair.EnemyId == enemy.Id &&
        _characters.Values.Any(character => character.Id == pair.CharacterId && character.IsAlive));

    public IReadOnlyList<Enemy> EngagedEnemies(LiveCharacter character) => _engagements
        .Where(pair => pair.CharacterId == character.Id)
        .Select(pair => _enemies.Values.FirstOrDefault(enemy => enemy.Id == pair.EnemyId))
        .Where(enemy => enemy is { CurrentHitPoints: > 0 }).Cast<Enemy>().ToArray();

    public TacticalBattleParticipant AdvanceTurn()
    {
        ActionNumber++;
        SelectedTargetEnemyId = null;
        if (_queuedExtraActions > 0)
        {
            _queuedExtraActions--;
            return Turns.RepeatCurrentTurn();
        }
        var completedCycle = Turns.Cycle;
        var next = Turns.AdvanceTurn();
        if (Turns.Cycle > completedCycle)
        {
            var inactiveAtLimit = new HashSet<BattleSide>();
            foreach (var side in Enum.GetValues<BattleSide>())
            {
                _inactiveCycleStreaks[side] = _activeSidesThisCycle.Contains(side)
                    ? 0
                    : _inactiveCycleStreaks[side] + 1;
                if (_inactiveCycleStreaks[side] >= InactiveCycleLimit) inactiveAtLimit.Add(side);
            }
            InactiveSidesLastCompletedCycle = inactiveAtLimit;
            _activeSidesThisCycle.Clear();
        }
        return next;
    }

    public void RecordMovement(BattleSide side) => _activeSidesThisCycle.Add(side);
    public void RecordAttack(BattleSide side) => _activeSidesThisCycle.Add(side);
    public void RecordSpellCast(LiveCharacter caster) => _spellCasts[caster.Id]++;
    public void RecordCompletedFinalAction() => ActionNumber++;
    public void RecordKill(LiveCharacter killer, Enemy enemy, int awardedExperience) =>
        _kills.Add(new TeamBattleKill(killer.Id, killer.Name, enemy.Definition.Id, enemy.Name,
            Math.Max(0, awardedExperience)));

    public void GrantExtraActions(int count) => _queuedExtraActions += Math.Max(0, count);
    public bool TryResolveDeath(Enemy enemy) => _resolvedEnemyDeaths.Add(enemy.Id);
    public bool TryResolveDeath(LiveCharacter character) => _resolvedCharacterDeaths.Add(character.Id);

    public void MarkDefeated(LiveCharacter character) =>
        Turns.TrySetState(CombatantId.ForCharacter(character.Id), TacticalParticipantState.Defeated);

    public void MarkDefeated(Enemy enemy) =>
        Turns.TrySetState(CombatantId.ForEnemy(enemy.Id), TacticalParticipantState.Defeated);

    public void MarkRetreated(LiveCharacter character) =>
        Turns.TrySetState(CombatantId.ForCharacter(character.Id), TacticalParticipantState.Retreated);

    public void UpdatePosition(LiveCharacter character, Position position) =>
        Turns.TryUpdatePosition(CombatantId.ForCharacter(character.Id), position);

    public void UpdatePosition(Enemy enemy) =>
        Turns.TryUpdatePosition(CombatantId.ForEnemy(enemy.Id), enemy.Position);

}
