using KaoszRubin.World;

namespace KaoszRubin.Domain.Combat;

public static class ExplorationRangedAttackRules
{
    public static IReadOnlyList<Position> Trace(Maze maze, Position origin, Direction direction, int maximumRange)
    {
        ArgumentNullException.ThrowIfNull(maze);
        if (!Enum.IsDefined(direction) || maximumRange < 1) return [];

        var path = new List<Position>(maximumRange);
        var position = origin;
        for (var distance = 0; distance < maximumRange; distance++)
        {
            position += direction;
            if (!maze.IsWalkable(position)) break;
            path.Add(position);
        }
        return path;
    }
}
