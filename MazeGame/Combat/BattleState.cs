using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;

namespace MazeGame.Combat;

public readonly record struct BattleId(Guid Value)
{
    public static BattleId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

/// <summary>Egy megkezdett, több inputcikluson vagy hálózati várakozáson át folytatható csata.</summary>
public sealed class BattleState
{
    internal BattleState(LiveCharacter player, Enemy enemy, EnemyDefinition defender,
        BattleRuntimeContext context, bool playerTurn, IEnumerable<string> events)
    {
        Id = BattleId.New();
        Player = player;
        Enemy = enemy;
        Defender = defender;
        Context = context;
        IsPlayerTurn = playerTurn;
        Events.AddRange(events);
    }

    public BattleId Id { get; }
    public CharacterId PlayerCharacterId => Player.Id;
    public string EnemyDefinitionId => Enemy.Definition.Id;
    public bool IsPlayerTurn { get; internal set; }
    public bool IsCompleted { get; internal set; }
    public int Round { get; internal set; }
    public long TurnId { get; internal set; } = 1;
    public int CurrentEnemyHitPoints => Defender.HitPoints ?? 0;
    public BattleResult? Result { get; internal set; }

    internal LiveCharacter Player { get; }
    internal Enemy Enemy { get; }
    internal EnemyDefinition Defender { get; set; }
    internal BattleRuntimeContext Context { get; }
    internal int QueuedPlayerActions { get; set; }
    internal List<string> Events { get; } = [];
}

public sealed record BattleStartResult(BattleState State, IReadOnlyList<BattleLogEntry> Entries);

public sealed record BattleStepResult(BattleState State, IReadOnlyList<BattleLogEntry> Entries)
{
    public bool IsCompleted => State.IsCompleted;
    public bool IsAwaitingPlayerAction => !State.IsCompleted && State.IsPlayerTurn;
    public BattleResult? Result => State.Result;
}

internal sealed class BattleRuntimeContext
{
    public BattleRuntimeContext(LiveCharacter player)
    {
        ChallengeAvailable = player.HasPerk(PerkIds.KnightChallenge);
        GuardianAngelAvailable = player.HasPerk(PerkIds.KnightGuardianAngel);
        LastFortressAvailable = player.HasPerk(PerkIds.FighterLastFortress);
        AmbushAvailable = player.HasPerk(PerkIds.ThiefAmbush);
    }

    public bool ChallengeAvailable { get; set; }
    public bool GuardianAngelAvailable { get; set; }
    public bool LastFortressAvailable { get; set; }
    public bool AmbushAvailable { get; set; }
    public bool ShadowStepReady { get; set; }
    public int ConsecutivePlayerHits { get; set; }
}
