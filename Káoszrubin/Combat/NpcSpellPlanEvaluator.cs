using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.World;

namespace KaoszRubin.Combat;

public sealed record NpcSpellPlanTarget(Enemy Enemy, double DamageMultiplier = 1.0);

public sealed record NpcSpellPlanEvaluation(
    double ExpectedDamage,
    double UsefulDamage,
    double ControlUtility,
    double Overkill,
    double Utility);

/// <summary>
/// Dobás nélkül megbecsüli egy támadó varázsterv harci hasznát. Az értékelés
/// a tényleges varázsfeloldás képleteit követi, de a véletlen eredmények várható értékével számol.
/// </summary>
public static class NpcSpellPlanEvaluator
{
    public static NpcSpellPlanEvaluation Evaluate(LiveCharacter caster, SpellDefinition spell,
        IReadOnlyList<SpellEffectDefinition> effects, IReadOnlyList<NpcSpellPlanTarget> targets,
        int manaCost)
    {
        var expectedDamage = 0.0;
        var usefulDamage = 0.0;
        var weightedUsefulDamage = 0.0;
        var overkill = 0.0;
        var controlUtility = 0.0;
        var killUtility = 0.0;

        foreach (var target in targets)
        {
            var enemy = target.Enemy;
            var directDamage = effects.Sum(effect => ExpectedDirectDamage(caster, spell, effect, enemy)) *
                               target.DamageMultiplier;
            var periodicDamage = effects.Sum(effect => ExpectedPeriodicDamage(caster, spell, effect, enemy));
            var executeDamage = effects.Where(effect => CanExecute(effect, enemy))
                .Select(effect => Math.Max(0, enemy.CurrentHitPoints *
                    ResolutionApplicationChance(caster, spell, effect, enemy) - directDamage - periodicDamage))
                .DefaultIfEmpty().Max();
            var totalDamage = Math.Max(0, directDamage + periodicDamage + executeDamage);
            var useful = Math.Min(enemy.CurrentHitPoints, totalDamage);
            var excess = Math.Max(0, totalDamage - enemy.CurrentHitPoints);
            var threatWeight = 1.0 + Math.Max(1, enemy.Definition.StrengthTier) * 0.05 +
                               RankWeight(enemy.Definition.Rank);

            expectedDamage += totalDamage;
            usefulDamage += useful;
            weightedUsefulDamage += useful * threatWeight;
            overkill += excess;
            controlUtility += effects.Sum(effect => ExpectedControlUtility(caster, spell, effect, enemy));
            if (totalDamage >= enemy.CurrentHitPoints)
                killUtility += 4 + Math.Max(1, enemy.Definition.StrengthTier) * 2.5 +
                               RankWeight(enemy.Definition.Rank) * 10;
        }

        var extraTargetUtility = Math.Max(0, targets.Count - 1) * 2.5;
        var utility = weightedUsefulDamage + controlUtility + killUtility + extraTargetUtility -
                      overkill * 0.18 - Math.Max(0, manaCost) * 0.45;
        return new NpcSpellPlanEvaluation(expectedDamage, usefulDamage, controlUtility, overkill, utility);
    }

    private static double ExpectedDirectDamage(LiveCharacter caster, SpellDefinition spell,
        SpellEffectDefinition effect, Enemy enemy)
    {
        if (effect.Type == SpellEffectType.RandomElement)
            return BaseDiceAverage(effect.Dice) * DamageModifier(caster, spell, effect, enemy) *
                   ResolutionMultiplier(caster, spell, effect, enemy) / 3.0;
        if (effect.Type is not (SpellEffectType.Damage or SpellEffectType.ChainDamage)) return 0;
        var rolled = BaseDiceAverage(effect.Dice) +
                     Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier) +
                     caster.Level * effect.LevelMultiplier + effect.Value;
        return Math.Max(0, rolled) * DamageModifier(caster, spell, effect, enemy) *
               ResolutionMultiplier(caster, spell, effect, enemy);
    }

    private static double ExpectedPeriodicDamage(LiveCharacter caster, SpellDefinition spell,
        SpellEffectDefinition effect, Enemy enemy)
    {
        if (effect.Type is not (SpellEffectType.Burning or SpellEffectType.Storm) ||
            effect.Dice is null || effect.Duration <= 0) return 0;
        var perRound = BaseDiceAverage(effect.Dice) +
                       Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier);
        if (caster.HasPerk(PerkIds.MageElementalMaster)) perRound *= 1.25;
        return Math.Max(0, perRound) * effect.Duration * EffectChance(effect) *
               ResolutionMultiplier(caster, spell, effect, enemy);
    }

    private static double ExpectedControlUtility(LiveCharacter caster, SpellDefinition spell,
        SpellEffectDefinition effect, Enemy enemy)
    {
        var duration = Math.Max(1, effect.Duration);
        var raw = effect.Type switch
        {
            SpellEffectType.SpeedPenalty => Math.Max(0, effect.Value) * duration * 0.75,
            SpellEffectType.SkipAlternate => duration * 5.0,
            SpellEffectType.HitBonus when effect.Value < 0 => -effect.Value * duration * 0.7,
            SpellEffectType.DamageBonus when effect.Value < 0 => -effect.Value * duration * 0.7,
            SpellEffectType.InitiativeBonus when effect.Value < 0 => -effect.Value * duration * 0.35,
            SpellEffectType.VisionBonus when effect.Value < 0 => -effect.Value * duration * 0.25,
            SpellEffectType.DispelBeneficial => enemy.ActiveSpellEffects.Count(active => active.Beneficial) * 5.0,
            SpellEffectType.RandomElement => (Math.Max(0, effect.Value) * duration * 0.25 +
                                              duration * EffectChance(effect) * 1.5) / 3.0 * 2.0,
            _ => 0
        };
        return raw * EffectChance(effect) * ResolutionMultiplier(caster, spell, effect, enemy) *
               (1.0 + Math.Max(1, enemy.Definition.StrengthTier) * 0.04 + RankWeight(enemy.Definition.Rank));
    }

    private static bool CanExecute(SpellEffectDefinition effect, Enemy enemy)
    {
        if (effect.Type != SpellEffectType.Execute || enemy.Definition.StrengthTier >= 5 ||
            enemy.Definition.HitPoints is not { } maximumHitPoints || maximumHitPoints <= 0 ||
            enemy.CurrentHitPoints * 100 > maximumHitPoints * effect.Value) return false;
        return true;
    }

    private static double DamageModifier(LiveCharacter caster, SpellDefinition spell,
        SpellEffectDefinition effect, Enemy enemy)
    {
        var multiplier = 1.0;
        if (caster.HasPerk(PerkIds.MageElementalMaster)) multiplier *= 1.25;
        if (caster.SpecializationId == ClassSpecializations.PriestJudgment && spell.School == SpellSchool.Divine)
            multiplier *= 1.20;
        if (caster.SpecializationId == ClassSpecializations.MageElementalist && spell.School == SpellSchool.Arcane)
            multiplier *= 1.20;
        if (caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.MageRagingElements) &&
            spell.School == SpellSchool.Arcane) multiplier *= 1.15;
        if (SpellExecutionService.IsHolyEffect(effect) && SpellExecutionService.IsUnholy(enemy.Definition))
            multiplier *= 1.50;
        return multiplier;
    }

    private static double ResolutionMultiplier(LiveCharacter caster, SpellDefinition spell,
        SpellEffectDefinition effect, Enemy enemy)
    {
        if (effect.Resolution == SpellResolution.Auto) return 1;
        if (effect.Resolution == SpellResolution.Attack)
        {
            var bonus = caster.EffectiveAbilities.Intelligence +
                        (caster.HasPerk(PerkIds.MageArcaneFocus) ? 2 : 0) +
                        caster.GetMagicItemBonus(MagicItemEffect.Hit) +
                        caster.SpellEffectValue(ActiveSpellEffectType.Invisibility) +
                        caster.SpellEffectValue(ActiveSpellEffectType.HitBonus) -
                        caster.GetActiveCurseValue(ItemCurseEffect.HitPenalty);
            var total = 0.0;
            for (var roll = 1; roll <= 20; roll++)
            {
                if (roll == 20) total += 2;
                else if (roll != 1 && roll + bonus >= 11 + enemy.EffectiveSpeed) total += 1;
            }
            return total / 20.0;
        }

        var difficulty = 10 + caster.EffectiveAbilities.Intelligence / 2 + spell.Level;
        var saved = Enumerable.Range(1, 20).Count(roll => roll + enemy.EffectiveSpeed >= difficulty) / 20.0;
        return effect.Resolution == SpellResolution.SaveHalf ? 1 - saved * 0.5 : 1 - saved;
    }

    private static double ResolutionApplicationChance(LiveCharacter caster, SpellDefinition spell,
        SpellEffectDefinition effect, Enemy enemy)
    {
        if (effect.Resolution == SpellResolution.Auto) return 1;
        if (effect.Resolution == SpellResolution.Attack)
        {
            var bonus = caster.EffectiveAbilities.Intelligence +
                        (caster.HasPerk(PerkIds.MageArcaneFocus) ? 2 : 0) +
                        caster.GetMagicItemBonus(MagicItemEffect.Hit) +
                        caster.SpellEffectValue(ActiveSpellEffectType.Invisibility) +
                        caster.SpellEffectValue(ActiveSpellEffectType.HitBonus) -
                        caster.GetActiveCurseValue(ItemCurseEffect.HitPenalty);
            return Enumerable.Range(1, 20).Count(roll =>
                roll == 20 || roll != 1 && roll + bonus >= 11 + enemy.EffectiveSpeed) / 20.0;
        }
        var difficulty = 10 + caster.EffectiveAbilities.Intelligence / 2 + spell.Level;
        var saved = Enumerable.Range(1, 20).Count(roll => roll + enemy.EffectiveSpeed >= difficulty) / 20.0;
        return effect.Resolution == SpellResolution.SaveHalf ? 1 : 1 - saved;
    }

    private static double BaseDiceAverage(DiceExpression? dice) => dice is { } value
        ? value.Count * (value.Sides + 1) / 2.0
        : 0;

    private static double EffectChance(SpellEffectDefinition effect) =>
        Math.Clamp(effect.ChancePercent, 0, 100) / 100.0;

    private static double RankWeight(EnemyRank rank) => rank switch
    {
        EnemyRank.Elite => 0.15,
        EnemyRank.MiniBoss => 0.30,
        EnemyRank.Boss => 0.50,
        _ => 0
    };
}
