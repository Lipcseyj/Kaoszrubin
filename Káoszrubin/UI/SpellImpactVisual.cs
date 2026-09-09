using KaoszRubin.Domain.Magic;

namespace KaoszRubin.UI;

internal static class SpellImpactVisual
{
    public static IEnumerable<Position> GetCells(SpellDefinition spell, Position caster, Position target,
        IEnumerable<Position> enemyTargets, Maze maze)
    {
        if (!spell.HasAreaImpact)
            return enemyTargets.Append(target).Where(maze.IsInside).Distinct().ToArray();

        var center = spell.TargetType == SpellTargetType.Direction ? caster : target;
        var radius = spell.TargetType == SpellTargetType.Direction ? 2 : spell.AreaRadius;
        var cells = new List<Position>();
        for (var y = Math.Max(0, center.Y - radius); y <= Math.Min(maze.Height - 1, center.Y + radius); y++)
            for (var x = Math.Max(0, center.X - radius); x <= Math.Min(maze.Width - 1, center.X + radius); x++)
            {
                var position = new Position(x, y);
                if (spell.TargetType != SpellTargetType.Direction ||
                    SpellExecutionService.IsInSpellCone(caster, position, target))
                    cells.Add(position);
            }
        return cells;
    }

    public static (ConsoleColor Foreground, ConsoleColor Background) GetColors(
        SpellDefinition spell, Position position, Position origin, double elapsedMilliseconds)
    {
        var (light, dark) = spell.ImpactPalette switch
        {
            SpellImpactPalette.Red => (ConsoleColor.Red, ConsoleColor.DarkRed),
            SpellImpactPalette.YellowBrown => (ConsoleColor.Yellow, ConsoleColor.DarkYellow),
            _ => (ConsoleColor.Cyan, ConsoleColor.DarkBlue)
        };
        var distance = Math.Max(Math.Abs(position.X - origin.X), Math.Abs(position.Y - origin.Y));
        // Area rings travel outwards; single targets pulse together.
        var phase = ((int)(elapsedMilliseconds / 150) - (spell.HasAreaImpact ? distance : 0)) % 6;
        if (phase < 0) phase += 6;
        return phase switch
        {
            0 => (ConsoleColor.White, light),
            1 or 2 => (ConsoleColor.White, dark),
            _ => (light, ConsoleColor.Black)
        };
    }
}
