using KaoszRubin.Domain.Combat;
using KaoszRubin.World;

namespace KaoszRubin.Combat;

/// <summary>Azok a mezők, amelyek fölött egy repülő ellenfél harc közben áthaladhat, de le nem szállhat.</summary>
internal static class FlyingEnemyTraversalRules
{
    public static bool CanTraverse(Enemy? enemy, Maze maze, Position position,
        bool occupiedByActiveCombatant)
    {
        if (enemy is null || !enemy.Definition.HasTrait(EnemyTraits.Flying) || !maze.IsInside(position))
            return false;

        if (maze.IsWalkable(position)) return occupiedByActiveCombatant;

        return (maze.GetTerrainGameplayProfile(position).Tags & TerrainTag.TreeCanopy) != 0;
    }
}
