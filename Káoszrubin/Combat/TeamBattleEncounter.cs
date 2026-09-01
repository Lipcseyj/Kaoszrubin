using KaoszRubin.Domain.Characters;

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

/// <summary>A játékvilág objektumait a tiszta taktikai körsorrendhez kapcsoló futásidejű összecsapás.</summary>
public sealed class TeamBattleEncounter
{
    private readonly Dictionary<CombatantId, LiveCharacter> _characters = [];
    private readonly Dictionary<CombatantId, Enemy> _enemies = [];
    private readonly Dictionary<CharacterId, TeamCharacterBattleRuntime> _characterRuntimes = [];
    private readonly Dictionary<CharacterId, (int Vitality, int Mana)> _startingResources = [];
    private readonly HashSet<WorldEntityId> _resolvedEnemyDeaths = [];
    private readonly HashSet<CharacterId> _resolvedCharacterDeaths = [];
    private readonly HashSet<(CharacterId CharacterId, WorldEntityId EnemyId)> _engagements = [];
    private int _queuedExtraActions;

    public TeamBattleEncounter(Position center,
        IEnumerable<TeamCharacterParticipant> characters,
        IEnumerable<TeamEnemyParticipant> enemies,
        CharacterId initiatingCharacterId,
        WorldEntityId initiatingEnemyId,
        bool enemyStrikesFirst = false,
        int radius = TacticalDistance.DefaultBattleRadius,
        int openingCycles = 1)
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
        var tacticalParticipants = new List<TacticalBattleParticipant>();
        foreach (var participant in characterList)
        {
            var id = CombatantId.ForCharacter(participant.Character.Id);
            _characters.Add(id, participant.Character);
            _characterRuntimes.Add(participant.Character.Id, participant.Runtime);
            _startingResources.Add(participant.Character.Id,
                (participant.Character.CurrentVitality, participant.Character.CurrentMana));
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
    public TacticalBattleState Turns { get; }
    public int ActionNumber { get; private set; }
    public IReadOnlyCollection<LiveCharacter> Characters => _characters.Values;
    public IReadOnlyCollection<Enemy> Enemies => _enemies.Values;
    public bool FriendlySideDefeated => _characters.Values.All(character => !character.IsAlive);
    public bool HostileSideDefeated => _enemies.Values.All(enemy => enemy.CurrentHitPoints <= 0);
    public bool IsCompleted => FriendlySideDefeated || HostileSideDefeated;
    public TacticalBattleParticipant Current => Turns.CurrentParticipant ?? Turns.StartTurns();
    public LiveCharacter? CurrentCharacter => _characters.GetValueOrDefault(Current.Id);
    public Enemy? CurrentEnemy => _enemies.GetValueOrDefault(Current.Id);

    public TeamCharacterBattleRuntime RuntimeFor(LiveCharacter character) =>
        _characterRuntimes[character.Id];

    public (int Vitality, int Mana) StartingResourcesFor(LiveCharacter character) =>
        _startingResources[character.Id];

    public LiveCharacter? CharacterFor(CombatantId id) => _characters.GetValueOrDefault(id);
    public Enemy? EnemyFor(CombatantId id) => _enemies.GetValueOrDefault(id);

    public void Engage(LiveCharacter character, Enemy enemy) => _engagements.Add((character.Id, enemy.Id));

    public bool IsEngaged(LiveCharacter character) => _engagements.Any(pair =>
        pair.CharacterId == character.Id &&
        _enemies.Values.Any(enemy => enemy.Id == pair.EnemyId && enemy.CurrentHitPoints > 0));

    public bool IsEngaged(Enemy enemy) => _engagements.Any(pair =>
        pair.EnemyId == enemy.Id &&
        _characters.Values.Any(character => character.Id == pair.CharacterId && character.IsAlive));

    public TacticalBattleParticipant AdvanceTurn()
    {
        ActionNumber++;
        if (_queuedExtraActions > 0)
        {
            _queuedExtraActions--;
            return Turns.RepeatCurrentTurn();
        }
        return Turns.AdvanceTurn();
    }

    public void GrantExtraActions(int count) => _queuedExtraActions += Math.Max(0, count);
    public bool TryResolveDeath(Enemy enemy) => _resolvedEnemyDeaths.Add(enemy.Id);
    public bool TryResolveDeath(LiveCharacter character) => _resolvedCharacterDeaths.Add(character.Id);

    public void MarkDefeated(LiveCharacter character) =>
        Turns.TrySetState(CombatantId.ForCharacter(character.Id), TacticalParticipantState.Defeated);

    public void MarkDefeated(Enemy enemy) =>
        Turns.TrySetState(CombatantId.ForEnemy(enemy.Id), TacticalParticipantState.Defeated);

    public void UpdatePosition(LiveCharacter character, Position position) =>
        Turns.TryUpdatePosition(CombatantId.ForCharacter(character.Id), position);

    public void UpdatePosition(Enemy enemy) =>
        Turns.TryUpdatePosition(CombatantId.ForEnemy(enemy.Id), enemy.Position);

}
