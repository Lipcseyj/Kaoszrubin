using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Combat;

public enum NpcSpellAttackPattern { None, SingleTarget, Area, Direction, Chain }

[Flags]
public enum NpcSpellTacticalRole
{
    None = 0,
    Damage = 1 << 0,
    Control = 1 << 1,
    Healing = 1 << 2,
    Cleanse = 1 << 3,
    Buff = 1 << 4,
    Mobility = 1 << 5,
    Dispel = 1 << 6,
    Utility = 1 << 7
}

public sealed record NpcSpellTacticalClassification(
    NpcSpellAttackPattern AttackPattern,
    NpcSpellTacticalRole Roles,
    NpcSpellPlanComplexity Complexity)
{
    public bool IsOffensive => AttackPattern != NpcSpellAttackPattern.None &&
                               (Roles & (NpcSpellTacticalRole.Damage |
                                         NpcSpellTacticalRole.Control)) != 0;
}

/// <summary>Az adatvezérelt varázslatokat taktikai szerep és célzási geometria szerint osztályozza.</summary>
public static class NpcSpellTacticalClassifier
{
    public static NpcSpellTacticalClassification Classify(SpellDefinition spell,
        IEnumerable<SpellEffectDefinition> effects)
    {
        var spellEffects = effects as IReadOnlyCollection<SpellEffectDefinition> ?? effects.ToArray();
        var roles = NpcSpellTacticalRole.None;
        if (spellEffects.Any(IsDamageEffect)) roles |= NpcSpellTacticalRole.Damage;
        if (spellEffects.Any(IsControlEffect)) roles |= NpcSpellTacticalRole.Control;
        if (spellEffects.Any(effect => effect.Type is SpellEffectType.Heal or SpellEffectType.Resurrect))
            roles |= NpcSpellTacticalRole.Healing;
        if (spellEffects.Any(effect => effect.Type is SpellEffectType.CureStatus or
                SpellEffectType.BreakItemCurse)) roles |= NpcSpellTacticalRole.Cleanse;
        if (spellEffects.Any(effect => NpcSpellcastingPolicy.IsBuffEffect(effect.Type)))
            roles |= NpcSpellTacticalRole.Buff;
        if (spellEffects.Any(effect => effect.Type is SpellEffectType.TeleportSelf or
                SpellEffectType.TeleportParty)) roles |= NpcSpellTacticalRole.Mobility;
        if (spellEffects.Any(effect => effect.Type is SpellEffectType.Dispel or
                SpellEffectType.DispelBeneficial)) roles |= NpcSpellTacticalRole.Dispel;
        if (spellEffects.Any(effect => effect.Type is SpellEffectType.ExtraActions or
                SpellEffectType.RestoreNeeds)) roles |= NpcSpellTacticalRole.Utility;

        var offensive = (roles & (NpcSpellTacticalRole.Damage |
                                  NpcSpellTacticalRole.Control)) != 0;
        var pattern = !offensive ? NpcSpellAttackPattern.None
            : spellEffects.Any(effect => effect.Type == SpellEffectType.ChainDamage)
                ? NpcSpellAttackPattern.Chain
                : spell.TargetType == SpellTargetType.Direction
                    ? NpcSpellAttackPattern.Direction
                    : spell.TargetType == SpellTargetType.Area || spell.AreaRadius > 0
                        ? NpcSpellAttackPattern.Area
                        : spell.TargetType is SpellTargetType.Enemy or SpellTargetType.Cell
                            ? NpcSpellAttackPattern.SingleTarget
                            : NpcSpellAttackPattern.None;
        var complexity = pattern is NpcSpellAttackPattern.Area or NpcSpellAttackPattern.Direction or
            NpcSpellAttackPattern.Chain
            ? NpcSpellPlanComplexity.Complex
            : NpcSpellPlanComplexity.Simple;
        return new NpcSpellTacticalClassification(pattern, roles, complexity);
    }

    private static bool IsDamageEffect(SpellEffectDefinition effect) => effect.Type is
        SpellEffectType.Damage or SpellEffectType.ChainDamage or SpellEffectType.Burning or
        SpellEffectType.Storm or SpellEffectType.Execute or SpellEffectType.RandomElement;

    private static bool IsControlEffect(SpellEffectDefinition effect) => effect.Type is
        SpellEffectType.SpeedPenalty or SpellEffectType.SkipAlternate or SpellEffectType.DispelBeneficial ||
        effect.Type is SpellEffectType.HitBonus or SpellEffectType.DamageBonus or
            SpellEffectType.InitiativeBonus or SpellEffectType.VisionBonus && effect.Value < 0;
}
