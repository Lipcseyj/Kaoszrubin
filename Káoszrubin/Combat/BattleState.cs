using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;

namespace KaoszRubin.Combat;

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
        PolearmMasterOpeningAvailable = true;
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
    public bool PolearmMasterOpeningAvailable { get; set; }
    public bool BarbarianRageTriggered { get; set; }
    public bool KnightProtectionAvailable { get; set; }
    public LiveCharacter? KnightProtector { get; set; }
}

/// <summary>Egy karakter csapatharcon belüli, a teljes összecsapás alatt megőrzött harci állapota.</summary>
public sealed class TeamCharacterBattleRuntime
{
    internal TeamCharacterBattleRuntime(LiveCharacter character) => Context = new BattleRuntimeContext(character);
    internal BattleRuntimeContext Context { get; }
    public BattleTactic? Tactic => Context.Tactic;
    public bool RequiresTacticSelection => Context.RequiresTacticSelection && Context.Tactic is null;

    public bool TryChooseTactic(LiveCharacter character, BattleTactic tactic)
    {
        if (!RequiresTacticSelection) return false;
        var valid = character.CharacterClass.Id switch
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
}

public sealed record TeamCombatantPreparation(TeamCharacterBattleRuntime Runtime, int Initiative,
    int OpeningInitiative, IReadOnlyList<BattleLogEntry> Entries);
