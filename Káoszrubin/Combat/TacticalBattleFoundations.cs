using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;

namespace KaoszRubin.Combat;

/// <summary>
/// A térkép karaktercelláinak oldalarányát figyelembe vevő harctéri távolság.
/// Egy taktikai egység függőlegesen egy, vízszintesen két konzolcellát jelent.
/// </summary>
public static class TacticalDistance
{
    public const int HorizontalCellsPerUnit = 2;
    public const int DefaultBattleRadius = 5;

    public static int Between(Position first, Position second)
    {
        var horizontal = (Math.Abs(first.X - second.X) + HorizontalCellsPerUnit - 1) /
                         HorizontalCellsPerUnit;
        return horizontal + Math.Abs(first.Y - second.Y);
    }

    public static bool IsWithin(Position origin, Position position,
        int radius = DefaultBattleRadius)
    {
        if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
        return Between(origin, position) <= radius;
    }

    /// <summary>A nyolc környező mező valamelyikén álló ellenfél közelharcban elérhető.</summary>
    public static bool IsMeleeAdjacent(Position first, Position second)
    {
        var horizontal = Math.Abs(first.X - second.X);
        var vertical = Math.Abs(first.Y - second.Y);
        return horizontal <= 1 && vertical <= 1 && horizontal + vertical > 0;
    }
}

public readonly record struct CombatantId(string Value)
{
    public static CombatantId ForCharacter(CharacterId id) => new($"character:{id}");
    public static CombatantId ForEnemy(WorldEntityId id) => new($"enemy:{id}");

    public override string ToString() => Value;
}

public enum BattleSide { Friendly, Hostile }
public enum TacticalParticipantKind { PartyMember, Follower, Enemy }
public enum TacticalParticipantState { Approaching, Active, Defeated, Retreated }

/// <summary>A későbbi csapatharc minden szereplőjének hálózaton is átadható közös állapota.</summary>
public sealed record TacticalBattleParticipant(
    CombatantId Id,
    BattleSide Side,
    TacticalParticipantKind Kind,
    Position Position,
    int InitiativeBase,
    int MovementAllowance,
    int EligibleFromCycle = 1,
    TacticalParticipantState State = TacticalParticipantState.Active)
{
    public bool CanActIn(int cycle) => State is TacticalParticipantState.Active or TacticalParticipantState.Approaching &&
                                       cycle >= EligibleFromCycle;
}

public enum TacticalActionKind
{
    Move,
    PhysicalAttack,
    CastSpell,
    UseItem,
    Special,
    Retreat,
    MoveFormation,
    SwapToRear
}

/// <summary>
/// Szemantikus harci parancs: nem tartalmaz hostoldalon kiszámított sebzést vagy más eredményt.
/// </summary>
public sealed record TacticalBattleAction(
    CombatantId ActorId,
    TacticalActionKind Kind,
    CombatantId? TargetId = null,
    Position? Destination = null,
    string? DefinitionId = null);

/// <summary>A több résztvevős harc kezdeti, még a régi párviadaltól független állapotmodellje.</summary>
public sealed class TacticalBattleState
{
    private readonly Dictionary<CombatantId, TacticalBattleParticipant> _participants;
    private readonly IReadOnlyList<CombatantId> _openingOrder;
    private IReadOnlyList<CombatantId> _cycleOrder = [];
    private int _turnIndex = -1;

    public TacticalBattleState(BattleId id, Position center,
        IEnumerable<TacticalBattleParticipant> participants,
        int radius = TacticalDistance.DefaultBattleRadius, int openingCycles = 1,
        IReadOnlyList<CombatantId>? openingOrder = null)
    {
        if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        if (openingCycles < 0) throw new ArgumentOutOfRangeException(nameof(openingCycles));
        ArgumentNullException.ThrowIfNull(participants);

        Id = id;
        Center = center;
        Radius = radius;
        OpeningCycles = openingCycles;
        _openingOrder = openingOrder ?? [];
        _participants = new Dictionary<CombatantId, TacticalBattleParticipant>();
        foreach (var participant in participants)
        {
            if (string.IsNullOrWhiteSpace(participant.Id.Value))
                throw new ArgumentException("A harci résztvevő azonosítója nem lehet üres.", nameof(participants));
            if (!_participants.TryAdd(participant.Id, participant))
                throw new ArgumentException($"Duplikált harci résztvevő: {participant.Id}.", nameof(participants));
        }
        if (_participants.Count == 0)
            throw new ArgumentException("A csapatharchoz legalább egy résztvevő szükséges.", nameof(participants));
    }

    public BattleId Id { get; }
    public Position Center { get; }
    public int Radius { get; }
    public int OpeningCycles { get; }
    public int Cycle { get; private set; } = 1;
    public long TurnId { get; private set; } = 1;
    public bool HasStarted => _turnIndex >= 0;
    public IReadOnlyCollection<TacticalBattleParticipant> Participants => _participants.Values;
    public TacticalBattleParticipant? CurrentParticipant => HasStarted && _turnIndex < _cycleOrder.Count
        ? _participants.GetValueOrDefault(_cycleOrder[_turnIndex])
        : null;

    public IReadOnlyList<TacticalBattleParticipant> InitiativeOrder => _participants.Values
        .Where(participant => participant.CanActIn(Cycle))
        .OrderByDescending(participant => participant.InitiativeBase)
        .ThenBy(participant => participant.Id.Value, StringComparer.Ordinal)
        .ToArray();

    public bool IsInsideBattleArea(Position position) => TacticalDistance.IsWithin(Center, position, Radius);

    public TacticalBattleParticipant StartTurns()
    {
        if (HasStarted) throw new InvalidOperationException("A csapatharc körsorrendje már elindult.");
        BuildCycleOrder();
        return ActivateCurrent();
    }

    public TacticalBattleParticipant AdvanceTurn()
    {
        if (!HasStarted) return StartTurns();
        TurnId++;
        while (true)
        {
            _turnIndex++;
            if (_turnIndex >= _cycleOrder.Count)
            {
                Cycle++;
                BuildCycleOrder();
            }
            if (CurrentParticipant?.CanActIn(Cycle) == true) break;
        }
        return ActivateCurrent();
    }

    public TacticalBattleParticipant RepeatCurrentTurn()
    {
        if (!HasStarted) return StartTurns();
        TurnId++;
        return ActivateCurrent();
    }

    public bool TryUpdatePosition(CombatantId id, Position position)
    {
        if (!_participants.TryGetValue(id, out var participant)) return false;
        _participants[id] = participant with { Position = position };
        return true;
    }

    public bool TryAddParticipant(TacticalBattleParticipant participant)
    {
        if (string.IsNullOrWhiteSpace(participant.Id.Value) || _participants.ContainsKey(participant.Id)) return false;
        _participants.Add(participant.Id, participant);
        return true;
    }

    public bool TrySetState(CombatantId id, TacticalParticipantState state)
    {
        if (!_participants.TryGetValue(id, out var participant)) return false;
        _participants[id] = participant with { State = state };
        return true;
    }

    public TacticalBattleParticipant? Find(CombatantId id) => _participants.GetValueOrDefault(id);

    private void BuildCycleOrder()
    {
        var initiativeOrder = InitiativeOrder.Select(participant => participant.Id).ToArray();
        _cycleOrder = Cycle == 1 && _openingOrder.Count > 0
            ? _openingOrder.Where(id => initiativeOrder.Contains(id))
                .Concat(initiativeOrder.Where(id => !_openingOrder.Contains(id))).ToArray()
            : initiativeOrder;
        _turnIndex = 0;
        if (_cycleOrder.Count == 0)
            throw new InvalidOperationException($"A(z) {Cycle}. ciklusban nincs cselekvőképes résztvevő.");
    }

    private TacticalBattleParticipant ActivateCurrent()
    {
        var current = CurrentParticipant ?? throw new InvalidOperationException("Nincs cselekvőképes résztvevő.");
        if (current.State != TacticalParticipantState.Approaching) return current;
        current = current with { State = TacticalParticipantState.Active };
        _participants[current.Id] = current;
        return current;
    }
}

public sealed record EncounterThreatAssessment(
    int FriendlyPower,
    int HostilePower,
    double HostileToFriendlyRatio,
    bool IsOverwhelminglySafe)
{
    public const double DefaultQuickResolutionRatio = 1.0;
}

/// <summary>
/// Konzervatív, determinisztikus erőbecslés. Nem dönt automatikus harcról; ehhez később
/// a küldetés-, csoport-, erősítés- és emberi irányítási feltételeket is ellenőrizni kell.
/// </summary>
public static class EncounterThreatEvaluator
{
    public static EncounterThreatAssessment Assess(IEnumerable<LiveCharacter> friendlies,
        IEnumerable<EnemyDefinition> hostiles,
        double safeRatio = EncounterThreatAssessment.DefaultQuickResolutionRatio)
    {
        ArgumentNullException.ThrowIfNull(friendlies);
        ArgumentNullException.ThrowIfNull(hostiles);
        if (safeRatio is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(safeRatio));

        var friendlyPower = friendlies.Where(character => character.IsAlive).Sum(CharacterPower);
        var hostilePower = hostiles.Sum(EnemyPower);
        var ratio = friendlyPower == 0
            ? hostilePower == 0 ? 0 : double.PositiveInfinity
            : (double)hostilePower / friendlyPower;
        return new EncounterThreatAssessment(friendlyPower, hostilePower, ratio,
            friendlyPower > 0 && hostilePower > 0 && ratio <= safeRatio);
    }

    public static int CharacterPower(LiveCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (!character.IsAlive) return 0;
        var weapon = character.ActiveWeapons.Where(value => value?.Damage is not null)
            .Select(value => value!.Damage!.Maximum).DefaultIfEmpty(2).Max();
        var armor = character.Armor?.Defense?.Maximum ?? 0;
        var abilities = character.EffectiveAbilities;
        return Math.Max(1, character.CurrentVitality + weapon * 3 + armor * 2 +
                           abilities.Strength + abilities.Dexterity + character.Level * 4);
    }

    public static int EnemyPower(EnemyDefinition enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        var rankMultiplier = enemy.Rank switch
        {
            EnemyRank.Elite => 1.5,
            EnemyRank.MiniBoss => 2.0,
            EnemyRank.Boss => 3.0,
            _ => 1.0
        };
        var raw = Math.Max(1, enemy.HitPoints ?? 1) + Math.Max(1, enemy.Strength ?? 1) * 3 +
                  Math.Max(0, enemy.Armor ?? 0) * 2 + Math.Max(1, enemy.Speed ?? 1) * 2 +
                  Math.Max(1, enemy.StrengthTier) * 4 + enemy.AbilityThreat;
        return Math.Max(1, (int)Math.Ceiling(raw * rankMultiplier));
    }
}

public static class TacticalArrivalRules
{
    public const int MaximumArrivalCycles = 3;

    public static bool CanReachWithin(Position origin, int maximumSteps,
        Func<Position, bool> canEnter, Func<Position, bool> hasArrived)
    {
        if (maximumSteps < 0) throw new ArgumentOutOfRangeException(nameof(maximumSteps));
        ArgumentNullException.ThrowIfNull(canEnter);
        ArgumentNullException.ThrowIfNull(hasArrived);
        if (hasArrived(origin)) return true;

        var visited = new HashSet<Position> { origin };
        var queue = new Queue<(Position Position, int Distance)>();
        queue.Enqueue((origin, 0));
        while (queue.Count > 0)
        {
            var (position, distance) = queue.Dequeue();
            if (distance >= maximumSteps) continue;
            foreach (var direction in Enum.GetValues<Direction>())
            {
                var next = position + direction;
                if (!visited.Add(next) || !canEnter(next)) continue;
                if (hasArrived(next)) return true;
                queue.Enqueue((next, distance + 1));
            }
        }
        return false;
    }
}

public sealed record QuickCombatAssessment(
    bool IsEligible,
    string Reason,
    EncounterThreatAssessment Threat,
    int PredictedVitalityLoss,
    double PredictedInjuryRatio);

/// <summary>A legfeljebb három közönséges ellenfél elleni automatikus lejátszás kapuja.</summary>
public static class QuickCombatRules
{
    public const int MaximumEnemyCount = 3;
    public const double PredictedDamagePowerRatio = 0.15;
    public const double MaximumPredictedInjuryRatio = 0.60;

    public static QuickCombatAssessment Assess(IEnumerable<LiveCharacter> friendlies,
        IEnumerable<EnemyDefinition> hostiles,
        bool hasAvailableReinforcements = false,
        bool hasActiveFormation = false,
        bool isQuestImportant = false,
        bool enemyStrikesFirst = false)
    {
        ArgumentNullException.ThrowIfNull(friendlies);
        ArgumentNullException.ThrowIfNull(hostiles);
        var party = friendlies.Where(character => character.IsAlive).ToArray();
        var enemies = hostiles.ToArray();
        var threat = EncounterThreatEvaluator.Assess(party, enemies);
        var totalVitality = party.Sum(character => character.CurrentVitality);
        var predictedLoss = (int)Math.Ceiling(threat.HostilePower * PredictedDamagePowerRatio);
        var injuryRatio = totalVitality == 0 ? double.PositiveInfinity : (double)predictedLoss / totalVitality;

        string? reason = null;
        if (party.Length == 0) reason = "Nincs harcképes csapattag.";
        else if (enemies.Length is < 1 or > MaximumEnemyCount)
            reason = $"A gyorsharc legfeljebb {MaximumEnemyCount} ellenfélnél használható.";
        else if (enemies.Any(enemy => enemy.IsBoss || enemy.Rank != EnemyRank.Normal))
            reason = "Kiemelt ellenféllel szemben taktikai harc szükséges.";
        else if (isQuestImportant) reason = "Küldetés szempontjából fontos ellenfél.";
        else if (hasAvailableReinforcements) reason = "Az ellenfél erősítést hívhat.";
        else if (hasActiveFormation) reason = "Az aktív alakzat taktikai döntést igényel.";
        else if (enemyStrikesFirst) reason = "Az ellenséges rajtaütés túl kockázatos.";
        else if (!threat.IsOverwhelminglySafe) reason = "Az ellenfél fenyegetési szintje túl magas.";
        else if (injuryRatio > MaximumPredictedInjuryRatio)
            reason = $"A várható sérülés meghaladja a csapat életerejének " +
                     $"{MaximumPredictedInjuryRatio * 100:0}%-át.";
        return new QuickCombatAssessment(reason is null, reason ?? "Jelentéktelen, biztonságos ütközet.",
            threat, predictedLoss, injuryRatio);
    }
}
