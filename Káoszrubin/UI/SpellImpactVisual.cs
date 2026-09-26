using KaoszRubin.Domain.Magic;
using KaoszRubin.Application;
using KaoszRubin.Data;

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
            SpellImpactPalette.Purple => (ConsoleColor.Magenta, ConsoleColor.DarkMagenta),
            SpellImpactPalette.SicklyGreen => (ConsoleColor.Green, ConsoleColor.DarkGreen),
            SpellImpactPalette.Shadow => (ConsoleColor.Gray, ConsoleColor.DarkGray),
            SpellImpactPalette.BloodRed => (ConsoleColor.Red, ConsoleColor.DarkMagenta),
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

internal sealed record SpellImpactAnimation(SpellDefinition Spell, Position Origin,
    IReadOnlyList<Position> Cells, DateTime StartedUtc)
{
    public double ElapsedMillisecondsAt(DateTime utcNow) =>
        Math.Max(0, (utcNow - StartedUtc).TotalMilliseconds);

    public bool IsActiveAt(DateTime utcNow) =>
        ElapsedMillisecondsAt(utcNow) < Spell.EffectiveImpactDurationMilliseconds;
}

internal sealed class ReplicatedSpellImpactTracker
{
    private readonly object _gate = new();
    private readonly List<SpellImpactAnimation> _active = [];
    private long _lastSequence;
    private bool _initialized;
    private WorldId? _worldId;

    public IReadOnlyList<SpellImpactAnimation> Observe(WorldId worldId,
        IReadOnlyList<SessionSpellImpactSnapshot>? impacts, GameDataCatalog gameData, DateTime utcNow)
    {
        lock (_gate)
        {
            if (_worldId != worldId)
            {
                _worldId = worldId;
                _active.Clear();
            }
            impacts ??= [];
            if (!_initialized)
            {
                _lastSequence = impacts.Count == 0 ? 0 : impacts.Max(impact => impact.Sequence);
                _initialized = true;
                RemoveExpired(utcNow);
                return _active.ToArray();
            }
            foreach (var impact in impacts.Where(impact => impact.Sequence > _lastSequence)
                         .OrderBy(impact => impact.Sequence))
            {
                _lastSequence = impact.Sequence;
                if (impact.WorldId != worldId || impact.Cells.Count == 0) continue;
                _active.Add(new SpellImpactAnimation(gameData.GetSpell(impact.SpellId), impact.Origin,
                    impact.Cells.Distinct().ToArray(), utcNow));
            }
            RemoveExpired(utcNow);
            return _active.ToArray();
        }
    }

    public IReadOnlyList<SpellImpactAnimation> ActiveAt(DateTime utcNow)
    {
        lock (_gate)
        {
            RemoveExpired(utcNow);
            return _active.ToArray();
        }
    }

    private void RemoveExpired(DateTime utcNow) => _active.RemoveAll(impact => !impact.IsActiveAt(utcNow));
}
