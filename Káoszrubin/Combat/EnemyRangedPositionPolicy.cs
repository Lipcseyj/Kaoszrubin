using KaoszRubin.Domain.Combat;

namespace KaoszRubin.Combat;

/// <summary>Az íjász csak közvetlen veszélyben, élő közelharci fedezettel keres új lőállást.</summary>
public static class EnemyRangedPositionPolicy
{
    public static bool ShouldReposition(BattleEncounter battle, Enemy enemy, WeaponDefinition? weapon,
        bool closeThreatOrBelowMinimumRange, bool movementBlocked)
    {
        if (weapon?.IsRanged != true || !closeThreatOrBelowMinimumRange ||
            movementBlocked || battle.IsEngaged(enemy)) return false;

        return HasMeleeCover(battle, enemy);
    }

    public static bool AllowsCloseRangedShot(BattleEncounter battle, Enemy enemy) =>
        battle.IsEngaged(enemy) || !HasMeleeCover(battle, enemy);

    private static bool HasMeleeCover(BattleEncounter battle, Enemy enemy) =>
        battle.Enemies.Any(ally => ally != enemy && ally.CurrentHitPoints > 0 &&
            (ally.EquippedWeapon is { IsRanged: false } ||
             ally.EquippedWeapon is null && ally.AttackWeapons.Count > 0 &&
             ally.AttackWeapons.All(candidate => !candidate.IsRanged)));
}
