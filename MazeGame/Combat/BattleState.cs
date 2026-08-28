using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;

namespace MazeGame.Combat;

public readonly record struct BattleId(Guid Value)
{
    public static BattleId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public enum BattleTactic
{
    FighterPrecise,
    FighterPowerful,
    FighterDefensive,
    ThiefAmbush,
    ThiefObserve,
    ThiefPoison
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
    public BattleTactic? Tactic => Context.Tactic;
    public bool RequiresTacticSelection => Context.RequiresTacticSelection && Context.Tactic is null;
    public bool IsAwaitingTacticSelection => IsPlayerTurn && RequiresTacticSelection;
    public bool IsOpeningEnemyTurn => !IsPlayerTurn && Round == 0;
    public bool IsBarbarianRaging => Context.BarbarianRageActionsRemaining > 0;

    public bool TryChooseTactic(BattleTactic tactic)
    {
        if (!RequiresTacticSelection) return false;
        var valid = Player.CharacterClass.Id switch
        {
            CharacterClassIds.Harcos => tactic is BattleTactic.FighterPrecise or BattleTactic.FighterPowerful or BattleTactic.FighterDefensive,
            CharacterClassIds.Tolvaj => tactic is BattleTactic.ThiefAmbush or BattleTactic.ThiefObserve or BattleTactic.ThiefPoison,
            _ => false
        };
        if (!valid) return false;
        Context.Tactic = tactic;
        Context.AmbushAvailable |= tactic == BattleTactic.ThiefAmbush;
        return true;
    }

    public void SetKnightProtection(LiveCharacter knight)
    {
        Context.KnightProtector = knight;
        Context.KnightProtectionAvailable = true;
    }

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
        RequiresTacticSelection = player.CharacterClass.Id is CharacterClassIds.Harcos or CharacterClassIds.Tolvaj;
        KnightRetaliationReady = player.ConsumeKnightRetaliation();
    }

    public bool ChallengeAvailable { get; set; }
    public bool GuardianAngelAvailable { get; set; }
    public bool LastFortressAvailable { get; set; }
    public bool AmbushAvailable { get; set; }
    public bool ShadowStepReady { get; set; }
    public int ConsecutivePlayerHits { get; set; }
    public bool RequiresTacticSelection { get; }
    public BattleTactic? Tactic { get; set; }
    public int BarbarianRageActionsRemaining { get; set; }
    public bool KnightRetaliationReady { get; set; }
    public bool BarbarianRageTriggered { get; set; }
    public bool KnightProtectionAvailable { get; set; }
    public LiveCharacter? KnightProtector { get; set; }
}
