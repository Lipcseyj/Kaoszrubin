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
        var caster = new Position(4, 4);
        var target = new Position(5, 4);
        var cone = SpellImpactVisual.GetCells(catalog.GetSpell("S003"), caster, target, [], maze).ToHashSet();
        Require(cone.SetEquals(new[] { target, new Position(6, 3), new Position(6, 4), new Position(6, 5) }),
            "A lángtölcsér nem a valódi támadási területet rajzolja.");
        var area = catalog.GetSpell("S007");
        var edge = SpellImpactVisual.GetCells(area, caster, new Position(0, 0), [], maze).ToHashSet();
        Require(edge.Count == 9 && edge.All(maze.IsInside), "A területi effekt túllóg a térkép szélén.");
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

    public static void AnimationTimelineDoesNotOwnTheGameLoop()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var spell = catalog.GetSpell("S001");
        var started = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var animation = new SpellImpactAnimation(spell, new Position(2, 2), [new Position(2, 2)], started);

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
        Require(active.Count == 1 && active[0].Spell.Id == "S001" && active[0].Cells.SequenceEqual(current.Cells),
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
            onOffensiveImpact: positions =>
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
            onOffensiveImpact: _ => impactCount++);
        Require(impactCount == 1, "A védővarázslat támadó becsapódást indított.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
