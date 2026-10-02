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
        var reachable = GetCells(origin, radius, maze, spell.TargetType == SpellTargetType.Direction
            ? position => SpellExecutionService.IsInSpellCone(caster, position, target)
            : null);
        if (spell.Id != "S011") return reachable;

        // Three separate impacts in the selected 5x5 zone. The stable layout lets the
        // targeting, damage and animation use precisely the same cells.
        return reachable.Where(position => MeteorDamagePercent(caster, target, position) > 0).ToHashSet();
    }

    public static int MeteorDamagePercent(Position caster, Position target, Position position)
    {
        var offsets = new (int X, int Y)[] { (-1, -1), (1, -1), (-1, 1), (1, 1) };
        var first = (int)(Math.Abs((long)target.X * 31 + (long)target.Y * 17 + caster.X * 7 + caster.Y) % 4);
        var impacts = new[] { target,
            new Position(target.X + offsets[first].X, target.Y + offsets[first].Y),
            new Position(target.X + offsets[(first + 2) % 4].X, target.Y + offsets[(first + 2) % 4].Y) };
        var overlaps = impacts.Count(impact => Math.Max(Math.Abs(position.X - impact.X),
            Math.Abs(position.Y - impact.Y)) <= 1);
        if (overlaps == 0) return 0;
        return Math.Min(130, (impacts.Contains(position) ? 100 : 60) + (overlaps - 1) * 15);
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
