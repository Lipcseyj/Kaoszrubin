using KaoszRubin.Data;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Characters;
using KaoszRubin.UI;
using KaoszRubin.World;
using KaoszRubin.Application;

internal static class SpellImpactTests
{
    public static void CsvSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName);
        var catalog = CsvGameDataLoader.Load(path);
        Require(catalog.GetSpell("S007").ImpactPalette == SpellImpactPalette.Red &&
                catalog.GetSpell("S008").ImpactPalette == SpellImpactPalette.Blue &&
                catalog.GetSpell("P002").ImpactPalette == SpellImpactPalette.YellowBrown &&
                catalog.GetSpell("D004").ImpactPalette == SpellImpactPalette.Purple &&
                catalog.GetSpell("D009").ImpactPalette == SpellImpactPalette.SicklyGreen &&
                catalog.GetSpell("D001").ImpactPalette == SpellImpactPalette.Shadow &&
                catalog.GetSpell("D006").ImpactPalette == SpellImpactPalette.BloodRed,
            "A normál vagy sötét varázslatok becsapódási színe hibás.");
        var original = File.ReadAllLines(path);
        var spellLine = original.Single(line => line.StartsWith("S001;Mágikus lövedék;") ||
            line.StartsWith("S001,Mágikus lövedék,"));
        var separator = spellLine[4];
        var temporary = Path.GetTempFileName();
        try
        {
            foreach (var suffix in new[] { "", ",Red,725", ",Blue,0", ",YellowBrown,", ",Purple,", ",SicklyGreen,", ",Shadow,", ",BloodRed," })
            {
                File.WriteAllLines(temporary, original.Select(line =>
                    line.StartsWith($"S001{separator}Mágikus lövedék{separator}") || line.StartsWith($"S007{separator}Tűzgolyó{separator}")
                        ? string.Join(separator, line.Split(separator).Take(10)) + suffix.Replace(',', separator) : line));
                var loaded = CsvGameDataLoader.Load(temporary);
                var single = loaded.GetSpell("S001");
                var area = loaded.GetSpell("S007");
                var expected = suffix == ",Red,725" ? 725 : suffix == ",Blue,0" ? 0 : (int?)null;
                Require(single.EffectiveImpactDurationMilliseconds == (expected ?? 1500) &&
                        area.EffectiveImpactDurationMilliseconds == (expected ?? 3000),
                    "Az időfelülírás, kikapcsolás vagy régi CSV alapideje hibás.");
            }
            foreach (var suffix in new[] { ",Green,1500", ",99,1500", ",Red,-1", ",Red,NaN", ",Blue,1.5" })
            {
                File.WriteAllLines(temporary, original.Select(line => line.StartsWith($"S001{separator}Mágikus lövedék{separator}")
                    ? string.Join(separator, line.Split(separator).Take(10)) + suffix.Replace(',', separator) : line));
                var rejected = false;
                try { CsvGameDataLoader.Load(temporary); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "A hibás effektbeállítást a betöltő nem utasította el: " + suffix);
            }
        }
        finally { File.Delete(temporary); }
    }

    public static void FootprintsAndAnimation()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var maze = new Maze(9, 9);
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++) maze.Carve(new Position(x, y));
        var caster = new Position(4, 4);
        var target = new Position(5, 4);
        var cone = SpellImpactVisual.GetCells(catalog.GetSpell("S003"), caster, target, [], maze).ToHashSet();
        Require(cone.SetEquals(new[] { target, new Position(6, 3), new Position(6, 4), new Position(6, 5) }),
            "A lángtölcsér nem a valódi támadási területet rajzolja.");
        var area = catalog.GetSpell("S007");
        var edge = SpellImpactVisual.GetCells(area, caster, new Position(0, 0), [], maze).ToHashSet();
        Require(edge.Count == 4 && edge.All(maze.IsInside), "A területi effekt túllóg a térkép szélén.");
        var secondary = new Position(7, 4);
        var chain = SpellImpactVisual.GetCells(catalog.GetSpell("S016"), caster, target,
            [target, secondary, secondary], maze).ToHashSet();
        Require(chain.SetEquals(new[] { target, secondary }), "A lánc másodlagos célpontja kimaradt.");
        var centerColors = SpellImpactVisual.GetColors(area, target, target, 0);
        var outerColors = SpellImpactVisual.GetColors(area, secondary, target, 0);
        Require(centerColors != outerColors &&
                centerColors == SpellImpactVisual.GetColors(area, secondary, target, 300),
            "A területi színhullám nem terjed kifelé.");
        var single = catalog.GetSpell("S001");
        Require(SpellImpactVisual.GetColors(single, target, target, 0) !=
                SpellImpactVisual.GetColors(single, target, target, 450),
            "Az egycélpontos effekt nem pulzál.");
    }

    public static void AreaDamageHitsAllSidesButChainsSelectTargets()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var maze = new Maze(11, 11);
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++) maze.Carve(new Position(x, y));
        var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
        var characterClass = new CharacterClassDefinition("C006", "Mágus", PrimaryAbilities.Zero, true, 1.0);
        LiveCharacter MakeCharacter(string name) => new(name, race, characterClass,
            new PrimaryAbilities(5, 5, 5, 8), 200, 100, 10, 0);
        var caster = MakeCharacter("Mágus");
        var ally = MakeCharacter("Társ");
        var safe = MakeCharacter("Távoli társ");
        var enemy = new ConfiguredEnemy(new Position(5, 5), catalog.Enemies[0]);
        maze.AddEnemy(enemy);
        var party = new (LiveCharacter Character, Position Position)[]
        {
            (caster, new Position(2, 5)), (ally, new Position(5, 6)), (safe, new Position(8, 8))
        };
        var service = new SpellExecutionService(catalog, new Random(83));
        var timeStop = false;
        var startingHp = ally.CurrentVitality;
        service.ExecuteSpell(caster, party[0].Position, catalog.GetSpell("S007"), enemy.Position,
            false, null, false, ref timeStop, party, maze,
            (_, victim, damage, _) => victim.ReceiveSpellDamage(damage),
            (_, _) => false, (_, _) => "", (_, _) => "");
        Require(ally.CurrentVitality < startingHp && safe.CurrentVitality == safe.MaximumVitality,
            "A Tűzgolyó nem sebezte az érintett társat, vagy eltalált valakit a területen kívül.");
        var afterFireball = ally.CurrentVitality;
        service.ExecuteSpell(caster, party[0].Position, catalog.GetSpell("S016"), enemy.Position,
            false, null, false, ref timeStop, party, maze,
            (_, victim, damage, _) => victim.ReceiveSpellDamage(damage),
            (_, _) => false, (_, _) => "", (_, _) => "");
        Require(ally.CurrentVitality == afterFireball,
            "A láncvillám a szelektív ugrás közben partitagot sebzett.");
        var meteor = catalog.GetSpell("S011");
        var cells = SpellAreaFootprint.GetCells(meteor, party[0].Position, enemy.Position, maze);
        var dexterity = safe.EffectiveAbilities.Dexterity;
        safe.ApplySpellEffect(new ActiveSpellEffect("S008", ActiveSpellEffectType.SpeedPenalty, 3, 2));
        safe.ApplySpellEffect(new ActiveSpellEffect("S025", ActiveSpellEffectType.Storm, 0, 2,
            new DiceExpression(1, 4)));
        safe.ApplySpellEffect(new ActiveSpellEffect("S020", ActiveSpellEffectType.SkipNext, 0, 1));
        var beforeTick = safe.CurrentVitality;
        var tick = safe.AdvanceCombatSpellEffects(new Random(1));
        Require(safe.EffectiveAbilities.Dexterity == dexterity - 3 &&
                safe.CurrentVitality < beforeTick && tick.SkipAction &&
                !safe.HasSpellEffect(ActiveSpellEffectType.SkipNext),
            "A partitagot ért lassítás, időszakos sebzés vagy akcióvesztés nem hatott harcban.");        Require(cells.SetEquals(SpellImpactVisual.GetCells(meteor, party[0].Position, enemy.Position, [], maze)) &&
                cells.Count < 25 && cells.Contains(enemy.Position),
            "A Meteorzápor szétszórt becsapódása nem egyezik a látvánnyal.");
    }

    public static void MeteorCentersAndPersistentStorm()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var maze = new Maze(11, 11);
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++) maze.Carve(new Position(x, y));
        var casterPosition = new Position(2, 5);
        var target = new Position(5, 5);
        var meteor = catalog.GetSpell("S011");
        var centers = SpellAreaFootprint.MeteorImpactCenters(casterPosition, target);
        Require(centers.Count == 3 && centers.Distinct().Count() == 3 &&
                centers.All(center => SpellImpactVisual.GetGlyph(meteor, center, target,
                    casterPosition, " ") == "✹") &&
                SpellImpactVisual.GetGlyph(meteor, new Position(4, 5), target, casterPosition, " ") == " ",
            "A meteorbécsapódások középpontjai nem különülnek el a területtől.");

        var caster = new LiveCharacter("Mágus", catalog.Races[0],
            catalog.CharacterClasses.First(c => c.Id == CharacterClassIds.Mágus),
            new PrimaryAbilities(5, 5, 5, 8), 100, 100, 1, 0);
        var service = new SpellExecutionService(catalog, new Random(7));
        var timeStop = false;
        service.ExecuteSpell(caster, casterPosition, catalog.GetSpell("S009"), target,
            false, null, false, ref timeStop, [(caster, casterPosition)], maze,
            (_, victim, damage, _) => victim.ReceiveSpellDamage(damage),
            (_, _) => false, (_, _) => "", (_, _) => "");
        Require(maze.StormZones.Count == 1 && maze.StormZones[0].RemainingRounds == 3 &&
                maze.StormZones[0].Contains(target) && !maze.StormZones[0].Contains(new Position(9, 9)) &&
                !caster.HasSpellEffect(ActiveSpellEffectType.Storm),
            "A vihar nem önálló, három körig maradó térképhatásként jött létre.");
        var zone = maze.StormZones[0];
        var restored = System.Text.Json.JsonSerializer.Deserialize<ActiveStormZone>(
            System.Text.Json.JsonSerializer.Serialize(zone));
        Require(restored is not null && restored.SpellId == zone.SpellId &&
                restored.Cells.SequenceEqual(zone.Cells) && restored.RemainingRounds == 3,
            "A mentés nem őrzi meg a vihar területét és hátralévő idejét.");
        var world = new WorldSnapshot(maze.Id, maze.Width, maze.Height, null, null,
            [], [], [], [], [], [], StormZones:
            [new WorldStormZoneSnapshot(zone.Id, zone.SpellId, zone.Cells, zone.RemainingRounds)]);
        var expiredWorld = world with { StormZones = [] };
        var delta = WorldDeltaProjector.Create(1, world, 2, expiredWorld);
        Require(!delta.IsEmpty && delta.StormZones is { Count: 0 },
            "A lejárt vihar eltűnését nem továbbítja a világdelta.");
        var inside = target;
        var outside = new Position(9, 9);
        Require(zone.Contains(inside) && !zone.Contains(outside),
            "A viharsebzést nem az aktuális mező határozza meg.");
        maze.AdvanceStormZones();
        maze.AdvanceStormZones();
        Require(maze.StormZones.Count == 1 && maze.StormZones[0].RemainingRounds == 1,
            "A vihar túl korán járt le.");
        maze.AdvanceStormZones();
        Require(maze.StormZones.Count == 0, "A vihar nem járt le a harmadik ütem után.");
    }
    public static void AnimationTimelineDoesNotOwnTheGameLoop()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var spell = catalog.GetSpell("S001");
        var started = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var animation = new SpellImpactAnimation(spell, new Position(2, 2), [new Position(2, 2)], [], started);

        Require(animation.IsActiveAt(started) &&
                animation.IsActiveAt(started.AddMilliseconds(spell.EffectiveImpactDurationMilliseconds - 1)) &&
                !animation.IsActiveAt(started.AddMilliseconds(spell.EffectiveImpactDurationMilliseconds)) &&
                animation.ElapsedMillisecondsAt(started.AddMilliseconds(-100)) == 0,
            "A varázseffekt nem külső képkockaidő alapján indul vagy jár le.");
    }

    public static void ReplicatedImpactStartsGuestAnimationOnce()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var tracker = new ReplicatedSpellImpactTracker();
        var world = WorldId.New();
        var otherWorld = WorldId.New();
        var started = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var old = new SessionSpellImpactSnapshot(1, world, "S001", new(2, 2), [new(2, 2)]);
        Require(tracker.Observe(world, [old], catalog, started).Count == 0,
            "A guest belépéskor lejátszotta a korábbi varázseffektet.");

        var current = new SessionSpellImpactSnapshot(2, world, "S001", new(3, 2), [new(3, 2)]);
        var foreign = new SessionSpellImpactSnapshot(3, otherWorld, "S007", new(4, 2), [new(4, 2)]);
        var active = tracker.Observe(world, [old, current, foreign], catalog, started.AddMilliseconds(20));
        Require(active.Count == 1 && active[0].Spell.Id == "S001" &&
                active[0].FixedCells.SequenceEqual(current.Cells),
            "A host friss varázseffektje nem jutott el egyszer a guest idővonalára.");
        Require(tracker.Observe(world, [old, current, foreign], catalog, started.AddMilliseconds(40)).Count == 1,
            "Az ismételt snapshot megkettőzte a guest varázseffektjét.");
        Require(tracker.ActiveAt(started.AddSeconds(2)).Count == 0,
            "A guest varázseffektje nem járt le.");
    }

    public static void ImpactPrecedesDamage()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var caster = new LiveCharacter("Effektpróba", catalog.Races[0],
            catalog.CharacterClasses.First(c => c.Id == CharacterClassIds.Mágus),
            new PrimaryAbilities(5, 5, 5, 8), 20, 100, 1, 0);
        var maze = new Maze(9, 9);
        var enemy = new ConfiguredEnemy(new Position(4, 4), catalog.Enemies[0]);
        var second = new ConfiguredEnemy(new Position(5, 4), catalog.Enemies[0]);
        maze.Carve(enemy.Position);
        maze.Carve(second.Position);
        maze.AddEnemy(enemy);
        maze.AddEnemy(second);
        var service = new SpellExecutionService(catalog, new Random(17));
        var timeStop = false;
        var impactCount = 0;
        var damageCount = 0;
        service.ExecuteSpell(caster, new Position(2, 4), catalog.GetSpell("S016"), enemy.Position,
            false, null, false, ref timeStop, [(caster, new Position(2, 4))], maze,
            (_, victim, damage, _) =>
            {
                Require(impactCount == 1, "A sebzés az effekt előtt futott le.");
                damageCount++;
                victim.ReceiveSpellDamage(damage);
            }, (_, _) => false, (_, _) => "", (_, _) => "",
            onImpact: positions =>
            {
                impactCount++;
                Require(damageCount == 0 && enemy.CurrentHitPoints > 0 && second.CurrentHitPoints > 0,
                    "Az effekt már eltávolított célpontokat kapott.");
                Require(positions.Contains(enemy.Position) && positions.Contains(second.Position),
                    "A végrehajtás nem adta át az összes lánccélpontot.");
            });
        Require(impactCount == 1, "Egy támadás nem pontosan egy animációt indított.");
        service.ExecuteSpell(caster, new Position(2, 4), catalog.GetSpell("S021"), new Position(2, 4),
            false, null, false, ref timeStop, [(caster, new Position(2, 4))], maze,
            (_, _, _, _) => { }, (_, _) => false, (_, _) => "", (_, _) => "",
            onImpact: _ => impactCount++);
        Require(impactCount == 2,
            "A pozitív becsapódási idejű védővarázslat nem indította el a közös vizuális effektet.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
