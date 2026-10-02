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

    /// <summary>Három egymástól független becsapódás a látható, érvényes 5×5 mezőkön.</summary>
    public static IReadOnlyList<Position> RollMeteorImpactCenters(SpellDefinition spell, Position caster,
        Position target, Maze maze, Random random) => RollMeteorImpactCenters(
        GetCells(spell, caster, target, maze), random);

    public static IReadOnlyList<Position> RollMeteorImpactCenters(IEnumerable<Position> eligibleCells,
        Random random)
    {
        var cells = eligibleCells.OrderBy(position => position.Y).ThenBy(position => position.X).ToArray();
        if (cells.Length == 0) return [];
        return Enumerable.Range(0, 3).Select(_ => cells[random.Next(cells.Length)]).ToArray();
    }

    /// <summary>Az egymásra eső meteorok sebzése összeadódik; a hívó az 5×5 célterületre szűr.</summary>
    public static int MeteorDamagePercent(IReadOnlyList<Position> impacts, Position position, Maze? maze = null)
    {
        return impacts.Sum(impact =>
        {
            var distance = Math.Max(Math.Abs(position.X - impact.X), Math.Abs(position.Y - impact.Y));
            if (distance > 1 || maze is not null &&
                (maze.BlocksSight(position) || !FogOfWar.CanSee(maze, impact, position, 1))) return 0;
            return distance == 0 ? 100 : 60;
        });
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
