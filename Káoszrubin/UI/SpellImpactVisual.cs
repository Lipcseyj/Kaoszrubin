using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Characters;
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

        return SpellAreaFootprint.GetCells(spell, caster, target, maze);
    }

    public static (ConsoleColor Foreground, ConsoleColor Background) GetColors(
        SpellDefinition spell, Position position, Position origin, double elapsedMilliseconds,
        IReadOnlyList<Position>? meteorCenters = null,
        IReadOnlyList<Position>? meteorHitCells = null)
    {
        if (spell.Id == "S011" && meteorCenters is { Count: > 0 })
        {
            if (meteorCenters.Contains(position))
            {
                var flashing = meteorCenters.Select((center, index) => (center, index)).Any(entry =>
                    entry.center == position && elapsedMilliseconds >= entry.index * 450 &&
                    elapsedMilliseconds < entry.index * 450 + 700);
                return flashing ? (ConsoleColor.White, ConsoleColor.Red) :
                    (ConsoleColor.Yellow, ConsoleColor.DarkRed);
            }
            return (meteorHitCells?.Contains(position) ??
                    SpellAreaFootprint.MeteorDamagePercent(meteorCenters, position) > 0)
                ? (ConsoleColor.Yellow, ConsoleColor.DarkRed)
                : (ConsoleColor.DarkRed, ConsoleColor.Black);
        }
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

    public static string GetGlyph(SpellDefinition spell, Position position, Position origin,
        IReadOnlyList<Position>? meteorCenters, string underlying,
        IReadOnlyList<Position>? meteorHitCells = null)
    {
        if (spell.Id != "S011" || meteorCenters is not { Count: > 0 }) return underlying;
        var impacts = meteorCenters.Count(center => center == position);
        if (impacts > 1) return impacts.ToString();
        if (impacts == 1) return "✹";
        if (underlying != " ") return underlying;
        return (meteorHitCells?.Contains(position) ??
                SpellAreaFootprint.MeteorDamagePercent(meteorCenters, position) > 0) ? "▒" : "·";
    }
}

internal sealed record SpellImpactAnimation(SpellDefinition Spell, Position Origin,
    IReadOnlyList<Position> FixedCells, IReadOnlyList<SpellImpactTrackedTargetSnapshot> TrackedTargets,
    DateTime StartedUtc, Position? CasterPosition = null,
    IReadOnlyList<Position>? MeteorCenters = null,
    IReadOnlyList<Position>? MeteorHitCells = null)
{
    public double ElapsedMillisecondsAt(DateTime utcNow) =>
        Math.Max(0, (utcNow - StartedUtc).TotalMilliseconds);

    public bool IsActiveAt(DateTime utcNow) =>
        ElapsedMillisecondsAt(utcNow) < Spell.EffectiveImpactDurationMilliseconds;

    public IReadOnlyList<Position> CellsAt(IReadOnlyDictionary<CharacterId, Position> characterPositions,
        IReadOnlyDictionary<WorldEntityId, Position> enemyPositions)
    {
        if (TrackedTargets.Count == 0) return FixedCells;
        return FixedCells.Concat(TrackedTargets.Select(target => target.CharacterId is { } characterId &&
                                               characterPositions.TryGetValue(characterId, out var characterPosition)
                ? (Position?)characterPosition
                : target.EnemyId is { } enemyId && enemyPositions.TryGetValue(enemyId, out var enemyPosition)
                    ? enemyPosition
                    : null)
            .Where(position => position.HasValue).Select(position => position!.Value))
            .Distinct().ToArray();
    }
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
                if (impact.WorldId != worldId || impact.Cells.Count == 0 &&
                    impact.TrackedTargets is not { Count: > 0 }) continue;
                _active.Add(new SpellImpactAnimation(gameData.GetSpell(impact.SpellId), impact.Origin,
                    impact.Cells.Distinct().ToArray(), impact.TrackedTargets?.Distinct().ToArray() ?? [], utcNow,
                    impact.CasterPosition, impact.MeteorCenters, impact.MeteorHitCells));
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
