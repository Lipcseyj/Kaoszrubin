using KaoszRubin.Application;

namespace KaoszRubin.UI;

internal sealed class ReplicatedProjectileTracker
{
    internal const int CellDurationMilliseconds = 35;
    private readonly object _gate = new();
    private readonly List<Flight> _active = [];
    private WorldId? _worldId;
    private long _lastSequence;
    private bool _initialized;

    internal void Observe(WorldId worldId, IReadOnlyList<SessionProjectileSnapshot>? projectiles, DateTime utcNow)
    {
        lock (_gate)
        {
            if (_worldId != worldId)
            {
                _worldId = worldId;
                _active.Clear();
            }
            projectiles ??= [];
            if (!_initialized)
            {
                _lastSequence = projectiles.Count == 0 ? 0 : projectiles.Max(projectile => projectile.Sequence);
                _initialized = true;
                return;
            }
            foreach (var projectile in projectiles.Where(projectile => projectile.Sequence > _lastSequence)
                         .OrderBy(projectile => projectile.Sequence))
            {
                _lastSequence = projectile.Sequence;
                if (projectile.WorldId == worldId && projectile.Path.Count > 0)
                    _active.Add(new Flight(projectile, utcNow));
            }
            RemoveExpired(utcNow);
        }
    }

    internal IReadOnlyList<(Position Position, string Glyph)> ActiveAt(DateTime utcNow)
    {
        lock (_gate)
        {
            RemoveExpired(utcNow);
            return _active.Select(flight =>
            {
                var elapsed = Math.Max(0, (utcNow - flight.StartedUtc).TotalMilliseconds);
                var index = Math.Min(flight.Projectile.Path.Count - 1,
                    (int)(elapsed / CellDurationMilliseconds));
                return (flight.Projectile.Path[index], Glyph(flight.Projectile.Direction));
            }).ToArray();
        }
    }

    private void RemoveExpired(DateTime utcNow) => _active.RemoveAll(flight =>
        (utcNow - flight.StartedUtc).TotalMilliseconds >= flight.Projectile.Path.Count * CellDurationMilliseconds);

    private static string Glyph(Direction direction) => direction switch
    {
        Direction.Up => "↑",
        Direction.Down => "↓",
        Direction.Left => "←",
        _ => "→"
    };

    private sealed record Flight(SessionProjectileSnapshot Projectile, DateTime StartedUtc);
}
