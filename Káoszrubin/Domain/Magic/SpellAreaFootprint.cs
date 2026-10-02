using KaoszRubin.World;

namespace KaoszRubin.Domain.Magic;

/// <summary>The cells reached by an area or directional spell without crossing sight-blocking terrain.</summary>
public static class SpellAreaFootprint
{
    public static IReadOnlySet<Position> GetCells(SpellDefinition spell, Position caster,
        Position target, Maze maze)
    {
        if (spell.TargetType is not (SpellTargetType.Area or SpellTargetType.Direction))
            return new HashSet<Position>();

        var origin = spell.TargetType == SpellTargetType.Direction ? caster : target;
        var radius = spell.TargetType == SpellTargetType.Direction ? 2 : spell.AreaRadius;
        return GetCells(origin, radius, maze, spell.TargetType == SpellTargetType.Direction
            ? position => SpellExecutionService.IsInSpellCone(caster, position, target)
            : null);
    }

    private static IReadOnlySet<Position> GetCells(Position origin, int radius, Maze maze,
        Func<Position, bool>? shape = null)
    {
        var cells = new HashSet<Position>();
        for (var y = Math.Max(0, origin.Y - radius); y <= Math.Min(maze.Height - 1, origin.Y + radius); y++)
        for (var x = Math.Max(0, origin.X - radius); x <= Math.Min(maze.Width - 1, origin.X + radius); x++)
        {
            var position = new Position(x, y);
            if (shape is not null && !shape(position)) continue;
            if (maze.BlocksSight(position) || !FogOfWar.CanSee(maze, origin, position, radius)) continue;
            cells.Add(position);
        }
        return cells;
    }
}
