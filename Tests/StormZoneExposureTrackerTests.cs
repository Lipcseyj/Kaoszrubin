using KaoszRubin.Application;
using KaoszRubin.Domain.Magic;
using KaoszRubin.World;

internal static class StormZoneExposureTrackerTests
{
    public static void EntryAndPulseAreIndependent()
    {
        var world = WorldId.New();
        var inside = new Position(4, 4);
        var outside = new Position(8, 8);
        var zone = new ActiveStormZone(Guid.NewGuid(), "S009", inside, [inside], 3,
            new DiceExpression(1, 6), 0, 12, SpellResolution.SaveNegates);
        var original = new StormExposureActor(false, Guid.NewGuid());
        var entrant = new StormExposureActor(true, Guid.NewGuid());
        var positions = new Dictionary<StormExposureActor, Position>
        {
            [original] = inside,
            [entrant] = outside
        };
        var now = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        var tracker = new StormZoneExposureTracker(TimeSpan.FromSeconds(5));

        Require(tracker.Observe(world, [zone], positions, now, inCombat: false).Count == 0,
            "A varázsláskor már bent álló szereplő tévesen belépési sebzést kapott.");
        positions[entrant] = inside;
        var entries = tracker.Observe(world, [zone], positions, now.AddSeconds(1), inCombat: false);
        Require(entries.SequenceEqual([new StormZoneEntry(zone.Id, entrant)]),
            "A viharterületre kívülről belépő szereplő nem kapott belépési eseményt.");
        tracker.RecordExposure(zone.Id, entrant, now.AddSeconds(1));
        Require(tracker.DueZones(now.AddSeconds(5)).SequenceEqual([zone.Id]) &&
                !tracker.CanPulse(zone.Id, entrant, now.AddSeconds(5)) &&
                tracker.CanPulse(zone.Id, original, now.AddSeconds(5)) &&
                tracker.CanPulse(zone.Id, entrant, now.AddSeconds(6)),
            "A belépés utáni közeli ütem kétszer sebezne, vagy a bent maradó kihagyná az ütemet.");

        positions[entrant] = outside;
        Require(tracker.Observe(world, [zone], positions, now.AddSeconds(2), false).Count == 0,
            "A viharból kilépés belépési eseményt okozott.");
        positions[entrant] = inside;
        Require(tracker.Observe(world, [zone], positions, now.AddSeconds(3), false)
                .SequenceEqual([new StormZoneEntry(zone.Id, entrant)]),
            "Az újbóli belépés nem adott újabb sebzési alkalmat.");
        tracker.MarkPulsed(zone.Id, now.AddSeconds(5));
        Require(tracker.DueZones(now.AddSeconds(9)).Count == 0 &&
                tracker.DueZones(now.AddSeconds(10)).SequenceEqual([zone.Id]),
            "A felfedezési vihar nem öt másodperces ütemben jár.");

        tracker.Observe(world, [zone], positions, now.AddSeconds(11), inCombat: true);
        tracker.RecordBattleEntry(zone.Id, entrant, cycle: 2);
        Require(!tracker.CanBattlePulse(zone.Id, entrant, cycle: 3) &&
                tracker.CanBattlePulse(zone.Id, entrant, cycle: 4),
            "A harci belépés után közvetlenül újra sebezne a vihar.");
        tracker.Observe(world, [zone], positions, now.AddSeconds(20), inCombat: false);
        Require(tracker.DueZones(now.AddSeconds(24)).Count == 0 &&
                tracker.DueZones(now.AddSeconds(25)).SequenceEqual([zone.Id]),
            "A harcból visszatérés nem indította újra a felfedezési ütemet.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
