using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Combat;

/// <summary>A tervválasztás csatamemóriához és stabilitásához tartozó tiszta szabályok.</summary>
public static class NpcSpellPlanningPolicy
{
    public const double PlanRetentionPercent = 12.0;
    public const double PlanUtilityCollapsePercent = 55.0;

    public static double AdjustUtilityForMemory(IReadOnlyList<NpcOffensiveSpellMemory> memories,
        string spellId, NpcSpellPlanComplexity complexity, NpcSpellAttackPattern attackPattern,
        double baseUtility)
    {
        if (memories.Count == 0) return baseUtility;

        var utility = baseUtility;
        var scale = Math.Max(1.0, Math.Abs(utility));
        var usesOfSpell = memories.Count(memory => string.Equals(memory.SpellId,
            spellId, StringComparison.OrdinalIgnoreCase));
        if (usesOfSpell == 0)
            utility += Math.Clamp(scale * 0.08, 3.0, 12.0);
        else
            utility -= Math.Min(8.0, usesOfSpell * 1.5);

        var last = memories[^1];
        if (last.Complexity != complexity)
            utility += Math.Clamp(scale * 0.08, 2.0, 10.0);
        else
            utility -= Math.Clamp(scale * 0.04, 1.0, 6.0);

        if (string.Equals(last.SpellId, spellId, StringComparison.OrdinalIgnoreCase))
        {
            var consecutiveUses = memories.AsEnumerable().Reverse().TakeWhile(memory =>
                string.Equals(memory.SpellId, spellId, StringComparison.OrdinalIgnoreCase)).Count();
            utility -= Math.Clamp(scale * 0.14, 4.0, 20.0) + Math.Min(8.0, consecutiveUses * 2.0);
        }
        else if (last.AttackPattern != attackPattern)
            utility += 2.0;

        return utility;
    }

    public static bool ShouldRetainPlan(double previousUtility, double incumbentUtility,
        double bestUtility)
    {
        var retentionMargin = Math.Max(4.0, Math.Abs(bestUtility) * PlanRetentionPercent / 100.0);
        var utilityCollapsed = previousUtility > 0 &&
                               incumbentUtility < previousUtility * PlanUtilityCollapsePercent / 100.0;
        return !utilityCollapsed && incumbentUtility >= bestUtility - retentionMargin;
    }

    public static bool CanReevaluatePlan(NpcSpellPlanStatus status, bool targetIsAlive,
        bool spellIsAvailable, bool canSpendMana) =>
        status != NpcSpellPlanStatus.Failed && targetIsAlive && spellIsAvailable && canSpendMana;

    public static bool CanMoveForPlan(bool isKnight, bool hasActiveFormation, bool isEngaged,
        bool isStaggered) =>
        !isKnight && !hasActiveFormation && !isEngaged && !isStaggered;

    public static int EnemyStrength(IEnumerable<int> strengthTiers) =>
        strengthTiers.Sum(tier => Math.Max(1, tier));

    public static bool ShouldCastOffensively(NpcSpellcasterCombatProfile tactics, int enemyStrength,
        int offensiveSpellsCast) =>
        enemyStrength >= tactics.MinimumEnemyStrength &&
        (enemyStrength >= tactics.FullOffenseEnemyStrength ||
         offensiveSpellsCast < tactics.OffensiveSpellsPerBattle);

    public static double PositionPenalty(int movementDistance, int movementAllowance,
        int adjacentEnemyStrength)
    {
        var movementTurns = movementDistance <= 0 ? 0 :
            (int)Math.Ceiling(movementDistance / (double)Math.Max(1, movementAllowance));
        return movementTurns * 3.0 + Math.Max(0, movementDistance) * 0.25 +
               Math.Max(0, adjacentEnemyStrength) * 2.0;
    }
}
