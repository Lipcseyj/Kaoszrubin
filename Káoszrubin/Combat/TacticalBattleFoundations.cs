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
    public bool CanActIn(int cycle) => State == TacticalParticipantState.Active && cycle >= EligibleFromCycle;
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

    public TacticalBattleState(BattleId id, Position center,
        IEnumerable<TacticalBattleParticipant> participants,
        int radius = TacticalDistance.DefaultBattleRadius, int openingCycles = 2)
    {
        if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        if (openingCycles < 0) throw new ArgumentOutOfRangeException(nameof(openingCycles));
        ArgumentNullException.ThrowIfNull(participants);

        Id = id;
        Center = center;
        Radius = radius;
        OpeningCycles = openingCycles;
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
    public IReadOnlyCollection<TacticalBattleParticipant> Participants => _participants.Values;

    public IReadOnlyList<TacticalBattleParticipant> InitiativeOrder => _participants.Values
        .Where(participant => participant.CanActIn(Cycle))
        .OrderByDescending(participant => participant.InitiativeBase)
        .ThenBy(participant => participant.Id.Value, StringComparer.Ordinal)
        .ToArray();

    public bool IsInsideBattleArea(Position position) => TacticalDistance.IsWithin(Center, position, Radius);

    public void AdvanceCycle() => Cycle++;
}

public sealed record EncounterThreatAssessment(
    int FriendlyPower,
    int HostilePower,
    double HostileToFriendlyRatio,
    bool IsOverwhelminglySafe)
{
    public const double DefaultQuickResolutionRatio = 0.25;
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
        var weapon = character.WeaponSlots.Where(value => value?.Damage is not null)
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
                  Math.Max(1, enemy.StrengthTier) * 4 + enemy.AbilityIds.Count * 3;
        return Math.Max(1, (int)Math.Ceiling(raw * rankMultiplier));
    }
}
