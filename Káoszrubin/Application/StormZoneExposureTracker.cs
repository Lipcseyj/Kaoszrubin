using KaoszRubin.World;

namespace KaoszRubin.Application;

internal readonly record struct StormExposureActor(bool IsEnemy, Guid Id);
internal readonly record struct StormZoneEntry(Guid ZoneId, StormExposureActor Actor);

/// <summary>A viharterületbe való belépést és a felfedezési sebzésütemet külön követi.</summary>
internal sealed class StormZoneExposureTracker(TimeSpan pulseInterval)
{
    private WorldId? _worldId;
    private readonly HashSet<Guid> _knownZones = [];
    private readonly HashSet<(Guid ZoneId, StormExposureActor Actor)> _inside = [];
    private readonly Dictionary<Guid, DateTime> _nextPulseUtc = [];
    private readonly Dictionary<(Guid ZoneId, StormExposureActor Actor), DateTime> _lastExposureUtc = [];
    private readonly Dictionary<(Guid ZoneId, StormExposureActor Actor), int> _battleEntryCycle = [];
    private bool _wasInCombat;

    public IReadOnlyList<StormZoneEntry> Observe(WorldId worldId, IReadOnlyList<ActiveStormZone> zones,
        IReadOnlyDictionary<StormExposureActor, Position> occupants, DateTime now, bool inCombat)
    {
        if (_worldId != worldId)
        {
            _worldId = worldId;
            _knownZones.Clear();
            _inside.Clear();
            _nextPulseUtc.Clear();
            _lastExposureUtc.Clear();
            _battleEntryCycle.Clear();
            _wasInCombat = false;
        }

        var activeIds = zones.Select(zone => zone.Id).ToHashSet();
        _knownZones.IntersectWith(activeIds);
        foreach (var id in _nextPulseUtc.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            _nextPulseUtc.Remove(id);
        foreach (var key in _lastExposureUtc.Keys.Where(key => !activeIds.Contains(key.ZoneId)).ToArray())
            _lastExposureUtc.Remove(key);
        foreach (var key in _battleEntryCycle.Keys.Where(key => !activeIds.Contains(key.ZoneId)).ToArray())
            _battleEntryCycle.Remove(key);

        if (_wasInCombat && !inCombat)
        {
            foreach (var id in activeIds) _nextPulseUtc[id] = now + pulseInterval;
            _wasInCombat = false;
        }
        if (inCombat) _wasInCombat = true;

        var newZones = new HashSet<Guid>();
        var current = new HashSet<(Guid ZoneId, StormExposureActor Actor)>();
        foreach (var zone in zones)
        {
            if (_knownZones.Add(zone.Id))
            {
                newZones.Add(zone.Id);
                _nextPulseUtc[zone.Id] = now + pulseInterval;
            }
            foreach (var occupant in occupants)
                if (zone.Contains(occupant.Value)) current.Add((zone.Id, occupant.Key));
        }
        var entries = current.Where(key => !newZones.Contains(key.ZoneId) && !_inside.Contains(key))
            .Select(key => new StormZoneEntry(key.ZoneId, key.Actor)).ToArray();
        _inside.Clear();
        _inside.UnionWith(current);
        return entries;
    }

    public IReadOnlyList<Guid> DueZones(DateTime now) => _nextPulseUtc
        .Where(entry => now >= entry.Value).Select(entry => entry.Key).ToArray();

    public bool CanPulse(Guid zoneId, StormExposureActor actor, DateTime now) =>
        !_lastExposureUtc.TryGetValue((zoneId, actor), out var last) || now - last >= pulseInterval;

    public void RecordExposure(Guid zoneId, StormExposureActor actor, DateTime now) =>
        _lastExposureUtc[(zoneId, actor)] = now;

    public void RecordBattleEntry(Guid zoneId, StormExposureActor actor, int cycle) =>
        _battleEntryCycle[(zoneId, actor)] = cycle;

    public bool CanBattlePulse(Guid zoneId, StormExposureActor actor, int cycle) =>
        (!_battleEntryCycle.TryGetValue((zoneId, actor), out var enteredCycle) ||
         enteredCycle < cycle - 1);

    public void MarkPulsed(Guid zoneId, DateTime now) => _nextPulseUtc[zoneId] = now + pulseInterval;
}
