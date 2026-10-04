internal static partial class Program
{
    static void EnemySpellcasterProfilesAreDataDrivenAndComplete()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var expected = new[]
        {
            MonsterIds.GoblinVajákos, MonsterIds.KáoszmágusTanítvány, MonsterIds.Káoszpap,
            MonsterIds.OrkSámán, MonsterIds.Boszorkány, MonsterIds.OrkVérpap, MonsterIds.Kígyópap,
            MonsterIds.Káoszmágus, MonsterIds.SötétDruida, MonsterIds.Vérmágus, MonsterIds.KáoszFőpap,
            MonsterIds.Nekromanta, MonsterIds.Lich, MonsterIds.Drakolich, MonsterIds.Feketemágus
        };
        Assert(data.EnemySpellcasters.Select(profile => profile.EnemyId).SequenceEqual(expected),
            "Az ellenséges varázshasználók erősorrendje vagy készlete eltér a tervezettől.");
        var enemyOnlySpells = data.Spells.Where(spell => spell.EnemyOnly).ToArray();
        Assert(enemyOnlySpells.Length > 0 &&
               enemyOnlySpells.All(spell => spell.Id.StartsWith('D')),
            "A sötét ellenséges varázslatkészlet hiányos vagy hibásan van megjelölve.");
        Assert(enemyOnlySpells.All(spell => !string.IsNullOrWhiteSpace(spell.LogEmoji) && spell.LogEmoji != "✨"),
            "Az ellenséges varázslatokhoz nem töltődtek be a tematikus naplóikonok.");
        foreach (var enemyId in expected)
        {
            var enemy = data.GetEnemy(enemyId);
            var profile = enemy.SpellcasterProfile;
            Assert(profile is { SpellIds.Count: > 0, MaximumMana: > 0, Intelligence: > 0 },
                $"A(z) {enemyId} ellenség feloldott varázsprofilja hiányzik.");
            Assert(profile!.SpellIds.Select(data.GetSpell).Any(spell => spell.EnemyOnly),
                $"A(z) {enemyId} ellenség nem kapott sötét varázslatot.");
        }
    }

    static void DarkSpellsRemainEnemyOnly()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var darkSpell = data.GetSpell("D001");
        var mage = CreateCharacter("Tiltott mágus", vitality: 30, characterClassId: CharacterClassIds.Mágus);

        Assert(darkSpell.EnemyOnly && !data.GetSpells(darkSpell.School, darkSpell.Level)
                .Any(spell => string.Equals(spell.Id, darkSpell.Id, StringComparison.OrdinalIgnoreCase)),
            "A sötét varázslat bekerült a játékosok választható varázslatlistájába.");
        Assert(!SpellcastingRules.AvailableUnknownSpells(mage, data, mage.Level)
                .Any(spell => spell.EnemyOnly) && !mage.LearnSpell(darkSpell),
            "A játékos meg tudta tanulni a csak ellenségeknek szánt varázslatot.");
        var rejection = new SpellExecutionService(data, new Random(1)).ValidateSpellCast(
            mage, new Position(1, 1), darkSpell, true, null);
        Assert(rejection?.Message.Contains("csak ellenséges", StringComparison.OrdinalIgnoreCase) == true,
            "A végrehajtási védelem nem utasította el az ellenséges varázslatot.");
    }

    static void EnemySpellcastingUsesManaTargetsPartyAndPersists()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var baseDefinition = data.GetEnemy(MonsterIds.GoblinVajákos);
        var profile = baseDefinition.SpellcasterProfile! with
        {
            SpellIds = ["D001"], CastingChancePercent = 100, ManaReservePercent = 0
        };
        var caster = new ConfiguredEnemy(new Position(2, 2), baseDefinition with { SpellcasterProfile = profile },
            new Random(1));
        var target = CreateCharacter("Varázscél", vitality: 100);
        var service = new EnemySpellcastingService(data, new Random(2));

        var plan = service.SelectSpell(caster, [caster], [(target, new Position(4, 2))], (_, _, _) => true);
        Assert(plan is { Spell.Id: "D001", HostileTargets.Count: 1 } && plan.HostileTargets[0] == target,
            "Az ellenséges varázsló nem a parti érvényes célpontját választotta.");
        var manaBefore = caster.CurrentMana;
        var hpBefore = target.CurrentVitality;
        service.Execute(caster, plan!);
        Assert(caster.CurrentMana == manaBefore - plan!.Spell.ManaCost && target.CurrentVitality < hpBefore &&
               !caster.IsSpellReady(plan.Spell.Id),
            "A varázslás nem fogyasztott mannát, nem sebzett vagy nem indította el a lehűlést.");

        var interruptedCaster = new ConfiguredEnemy(new Position(2, 2),
            baseDefinition with { SpellcasterProfile = profile }, new Random(1));
        var interruptedTarget = CreateCharacter("Lekötött cél", vitality: 100);
        var interruptedPlan = service.SelectSpell(interruptedCaster, [interruptedCaster],
            [(interruptedTarget, new Position(3, 2))], (_, _, _) => true)!;
        var interruptedMana = interruptedCaster.CurrentMana;
        var interruptedHp = interruptedTarget.CurrentVitality;
        var interrupted = service.Execute(interruptedCaster, interruptedPlan, combatFailureChance: 100);
        Assert(interrupted.Message.Contains("meghiúsul", StringComparison.OrdinalIgnoreCase) &&
               interruptedCaster.CurrentMana == interruptedMana - interruptedPlan.Spell.ManaCost &&
               !interruptedCaster.IsSpellReady(interruptedPlan.Spell.Id) &&
               interruptedTarget.CurrentVitality == interruptedHp,
            "A lekötött ellenség elrontott varázslata nem veszítette el szabályosan a mannát és az akciót.");

        var saved = new EnemySaveData(caster.Position, caster.Definition.Id, caster.CurrentHitPoints,
            CurrentMana: caster.CurrentMana,
            SpellCooldowns: caster.SpellCooldowns.ToDictionary(item => item.Key, item => item.Value));
        var roundTrip = JsonSerializer.Deserialize<EnemySaveData>(JsonSerializer.Serialize(saved))!;
        var restored = new ConfiguredEnemy(caster.Position, caster.Definition, new Random(1));
        restored.RestoreSpellcasting(roundTrip.CurrentMana ?? restored.MaximumMana, roundTrip.SpellCooldowns);
        Assert(restored.CurrentMana == caster.CurrentMana && !restored.IsSpellReady(plan.Spell.Id),
            "Az ellenséges manna vagy varázslatlehűlés elveszett a mentési körben.");

        var artillery = new ConfiguredEnemy(new Position(2, 2), data.GetEnemy(MonsterIds.Káoszmágus) with
        {
            SpellcasterProfile = data.GetEnemy(MonsterIds.Káoszmágus).SpellcasterProfile! with
            { SpellIds = ["D008"], CastingChancePercent = 100, ManaReservePercent = 0 }
        });
        var secondTarget = CreateCharacter("Második cél", vitality: 100);
        var areaPlan = service.SelectSpell(artillery, [artillery],
            [(target, new Position(5, 2)), (secondTarget, new Position(5, 3))], (_, _, _) => true);
        Assert(areaPlan is { HostileTargets.Count: 2 },
            "A területi ellenséges varázslat nem a legtöbb partitagot lefedő célpontot választotta.");
        var walledMaze = new Maze(9, 9);
        for (var y = 0; y < walledMaze.Height; y++)
        for (var x = 0; x < walledMaze.Width; x++) walledMaze.Carve(new Position(x, y));
        for (var y = 0; y < walledMaze.Height; y++)
            walledMaze.SetTile(new Position(5, y), Maze.Wall);
        var wallPlan = service.SelectSpell(artillery, [artillery],
            [(target, new Position(4, 2)), (secondTarget, new Position(6, 2))],
            (origin, destination, range) => FogOfWar.CanSee(walledMaze, origin, destination, range),
            walledMaze);
        Assert(wallPlan is null || wallPlan is { HostileTargets.Count: 1 } && wallPlan.HostileTargets[0] == target,
            "Az ellenséges területi varázslat a fal túloldali csapattagot is érintette.");

        var blastAlly = new ConfiguredEnemy(new Position(5, 3), baseDefinition);
        var blastAllyHp = blastAlly.CurrentHitPoints;
        var fragileTarget = CreateCharacter("Sebzett cél", vitality: 1);
        var targetHpBeforeBlast = target.CurrentVitality;
        var directBlast = new EnemySpellPlan(data.GetSpell("D008"), new Position(5, 2),
            [target, fragileTarget], [], 100, [blastAlly]);
        var blastLog = service.Execute(artillery, directBlast).Message;
        Assert(blastAlly.CurrentHitPoints < blastAllyHp,
            "Az ellenséges területi varázslat nem sebezte a saját oldalán álló lényt.");
        Assert(blastLog.StartsWith($"{directBlast.Spell.LogEmoji} {artillery.Name}") &&
               blastLog.Contains($"{target.Name}: ❤️-{targetHpBeforeBlast - target.CurrentVitality} " +
                                 $"(❤️{target.CurrentVitality}/{target.MaximumVitality})") &&
               blastLog.Contains($"{fragileTarget.Name}: ❤️-1 (❤️0/{fragileTarget.MaximumVitality})") &&
               blastLog.Contains($"{blastAlly.ShortName}: baráti tűz ❤️-{blastAllyHp - blastAlly.CurrentHitPoints} " +
                                 $"(❤️{blastAlly.CurrentHitPoints}/{blastAlly.MaximumHitPoints})"),
            "Az ellenséges területi varázslat naplója nem a tényleges sebzést és a célpontok maradék HP-ját mutatja.");
        var healerDefinition = data.GetEnemy(MonsterIds.Káoszpap);
        var healer = new ConfiguredEnemy(new Position(2, 2), healerDefinition with
        {
            SpellcasterProfile = healerDefinition.SpellcasterProfile! with
            { SpellIds = ["D017"], CastingChancePercent = 100, ManaReservePercent = 0 }
        });
        var woundedAlly = new ConfiguredEnemy(new Position(3, 2), baseDefinition);
        woundedAlly.SetCurrentHitPoints(1);
        var healPlan = service.SelectSpell(healer, [healer, woundedAlly],
            [(target, new Position(4, 2))], (_, _, _) => true);
        Assert(healPlan is { AlliedTargets.Count: 1 } && healPlan.AlliedTargets[0] == woundedAlly,
            "Az ellenséges gyógyító nem a legsérültebb szörnytársat választotta.");
    }

    static void PartySpellResistancesCoverEnemyMagicAndFriendlyFire()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var protectedMember = CreateCharacter("Védett társ", vitality: 200);
        Assert(protectedMember.AddMagicItem(data.GetMagicItem("M025")) &&
               protectedMember.AddMagicItem(data.GetMagicItem("M031")) &&
               CharacterSpellResistance.Percent(protectedMember, DamageType.Fire) == 25 &&
               CharacterSpellResistance.MagicPercent(protectedMember) == 25 &&
               CharacterSpellResistance.Apply(protectedMember, 100, DamageType.Fire) == 56 &&
               CharacterSpellResistance.Apply(protectedMember, 100, DamageType.Acid) == 75,
            "A felszerelt tűzgyűrű és az általános varázsvédő amulett százalékai nem megfelelőek.");
        Assert(data.GetMagicItem("M026").EffectValue == 50 &&
               data.GetMagicItem("M028").EffectValue == 50 &&
               data.GetMagicItem("M030").EffectValue == 50 &&
               data.GetMagicItem("M032").EffectValue == 50 &&
               data.GetMagicItem("M034").EffectValue == 50 &&
               data.GetMagicItem("M036").EffectValue == 50 &&
               data.GetSpellEffects("S008").First(effect => effect.Type == SpellEffectType.Damage)
                   .DamageType == DamageType.Frost &&
               data.GetSpellEffects("S009").First(effect => effect.Type == SpellEffectType.Damage)
                   .DamageType == DamageType.Lightning,
            "Az erős védőékszerek nem ötven százalékosak.");
        var frostWard = CreateCharacter("Jégvédett", vitality: 100);
        Assert(frostWard.AddMagicItem(data.GetMagicItem("M034")) &&
               CharacterSpellResistance.Apply(frostWard, 100, DamageType.Frost) == 50 &&
               CharacterSpellResistance.Apply(frostWard, 100, DamageType.Lightning) == 100,
            "A jégvédő gyűrű más elemi sebzést is csökkent, vagy a jégsebzést nem felezi.");
        protectedMember.ApplySpellEffect(new ActiveSpellEffect("WARD-CAP",
            ActiveSpellEffectType.FireResistance, 90, 1, Beneficial: true));
        Assert(CharacterSpellResistance.Percent(protectedMember, DamageType.Fire) == 100 &&
               CharacterSpellResistance.Apply(protectedMember, 100, DamageType.Fire) == 0,
            "Az összeadódó ellenállás nem áll meg a teljes immunitásnál.");
        protectedMember.RemoveSpellEffects(effect => effect.SourceSpellId == "WARD-CAP");

        var priest = CreateCharacter("Védőpap", vitality: 100, characterClassId: CharacterClassIds.Pap);
        var mage = CreateCharacter("Védőmágus", vitality: 100, characterClassId: CharacterClassIds.Mágus);
        priest.SetProgress(10, 0);
        mage.SetProgress(10, 0);
        var spellService = new SpellExecutionService(data, new Random(17));
        var maze = new Maze(8, 8);
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++) maze.Carve(new Position(x, y));
        var party = new (LiveCharacter Character, Position Position)[]
        {
            (priest, new Position(2, 2)), (mage, new Position(2, 3)),
            (protectedMember, new Position(3, 2))
        };
        var timeStop = false;
        void Cast(LiveCharacter caster, string spellId, Position target) => spellService.ExecuteSpell(
            caster, party.First(member => member.Character == caster).Position, data.GetSpell(spellId),
            target, false, null, false, ref timeStop, party, maze,
            (_, _, _, _) => { }, (_, _) => false, (_, _) => "", (_, _) => "");
        Cast(priest, "P032", new Position(2, 2));
        Cast(priest, "P033", new Position(3, 2));
        Cast(mage, "S033", new Position(2, 3));
        Assert(CharacterSpellResistance.Percent(protectedMember, DamageType.Fire) == 50 &&
               CharacterSpellResistance.Percent(protectedMember, DamageType.Necrotic) == 25 &&
               CharacterSpellResistance.MagicPercent(protectedMember) == 55 &&
               CharacterSpellResistance.Apply(protectedMember, 100, DamageType.Fire) == 22,
            "Az új papi és mágusi védővarázslatok nem növelik helyesen az ellenállást.");
        var sheet = CharacterSheetSnapshotProjector.Create(protectedMember, data.ExperienceByLevel);
        Assert(sheet.SpellResistanceDetails?.Contains("tűz: 50%") == true &&
               sheet.SpellResistanceDetails.Contains("általános varázsvédelem: 55%"),
            "A host és a guest részletes karakterlapja nem kapja meg a tényleges ellenállásokat.");
        for (var round = 0; round < 5; round++) protectedMember.AdvanceSpellEffects();
        Assert(CharacterSpellResistance.Percent(protectedMember, DamageType.Fire) == 25 &&
               CharacterSpellResistance.Percent(protectedMember, DamageType.Necrotic) == 0 &&
               CharacterSpellResistance.MagicPercent(protectedMember) == 25,
            "Az ellenállás varázslatok nem jártak le, vagy a tárgyi védelem is eltűnt.");

        var definition = data.GetEnemy(MonsterIds.Káoszmágus);
        var enemyCaster = new ConfiguredEnemy(new Position(1, 1), definition);
        var enemyPlan = new EnemySpellPlan(data.GetSpell("D008"), new Position(3, 2),
            [protectedMember], [], 100);
        var enemyBefore = protectedMember.CurrentVitality;
        var enemyLog = new EnemySpellcastingService(data, new Random(21))
            .Execute(enemyCaster, enemyPlan).Message;
        Assert(enemyBefore > protectedMember.CurrentVitality &&
               enemyLog.Contains("tűz ellenállás 25%") &&
               enemyLog.Contains("varázsvédelem 25%") &&
               enemyLog.Contains($"❤️{protectedMember.CurrentVitality}/{protectedMember.MaximumVitality}"),
            "Az ellenséges tűzvarázslat nem használta vagy nem naplózta a parti ellenállásait.");

        int FriendlyFire(LiveCharacter target, out string summary)
        {
            var fireMaze = new Maze(8, 8);
            for (var y = 0; y < fireMaze.Height; y++)
            for (var x = 0; x < fireMaze.Width; x++) fireMaze.Carve(new Position(x, y));
            var enemy = new ConfiguredEnemy(new Position(4, 4), data.Enemies[0]);
            fireMaze.AddEnemy(enemy);
            var caster = CreateCharacter("Tűzmágus", vitality: 200);
            var fireParty = new (LiveCharacter Character, Position Position)[]
            { (caster, new Position(2, 4)), (target, new Position(4, 5)) };
            var used = false;
            var before = target.CurrentVitality;
            summary = new SpellExecutionService(data, new Random(42)).ExecuteSpell(caster,
                fireParty[0].Position, data.GetSpell("S007"), enemy.Position,
                false, null, false, ref used, fireParty, fireMaze,
                (_, victim, damage, _) => victim.ReceiveSpellDamage(damage),
                (_, _) => false, (_, _) => "", (_, _) => "").Summary;
            return before - target.CurrentVitality;
        }
        var plainDamage = FriendlyFire(CreateCharacter("Védett társ", vitality: 200), out _);
        var protectedDamage = FriendlyFire(protectedMember, out var friendlyLog);
        Assert(plainDamage > 0 && protectedDamage ==
               CharacterSpellResistance.Apply(protectedMember, plainDamage, DamageType.Fire) &&
               friendlyLog.Contains("tűz ellenállás 25%") &&
               friendlyLog.Contains("varázsvédelem 25%") &&
               friendlyLog.Contains($"❤️{protectedMember.CurrentVitality}/{protectedMember.MaximumVitality}"),
            "A játékos tűzvarázslatának baráti tüze nem a tényleges ellenállásokat és HP-t mutatja.");
        var burningTarget = CreateCharacter("Égő társ", vitality: 100);
        Assert(burningTarget.AddMagicItem(data.GetMagicItem("M025")) &&
               burningTarget.AddMagicItem(data.GetMagicItem("M031")),
            "A folyamatos sebzés próbájához nem kerültek fel a védőékszerek.");
        var burningDice = new DiceExpression(1, 2);
        burningTarget.ApplySpellEffect(new ActiveSpellEffect("D008", ActiveSpellEffectType.Burning,
            0, 2, burningDice, 10, DamageType: DamageType.Fire));
        var rawBurning = burningDice.Roll(new Random(9)) + 10;
        var burningTick = burningTarget.AdvanceCombatSpellEffects(new Random(9));
        Assert(burningTick.Damage == CharacterSpellResistance.Apply(burningTarget, rawBurning, DamageType.Fire) &&
               burningTick.Notes.Any(note => note.Contains("tűz ellenállás 25%") &&
                   note.Contains("varázsvédelem 25%")),
            "Az időszakos tűzsebzés nem érvényesíti vagy nem mutatja az ellenállásokat.");
        Assert(protectedMember.SetInventoryItem(InventorySlotKind.MagicItem, 0, null) &&
               CharacterSpellResistance.Percent(protectedMember, DamageType.Fire) == 0,
            "A levett védőgyűrű továbbra is védi a karaktert.");
    }

    static void EnemySpellResistanceKnowledgeIsReactiveAndImprecise()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var definition = data.GetEnemy(MonsterIds.KáoszmágusTanítvány);
        var profile = definition.SpellcasterProfile! with
        {
            SpellIds = ["D001", "D004"], CastingChancePercent = 100, ManaReservePercent = 0,
            Style = EnemySpellcastingStyle.Artillery
        };
        var caster = new ConfiguredEnemy(new Position(2, 2), definition with { SpellcasterProfile = profile });
        var otherCaster = new ConfiguredEnemy(new Position(2, 3), definition with { SpellcasterProfile = profile });
        var protectedTarget = CreateCharacter("Tűzvédett", vitality: 100);
        protectedTarget.ApplySpellEffect(new ActiveSpellEffect("TEST-FIRE", ActiveSpellEffectType.FireResistance,
            100, 5, Beneficial: true));
        var anotherTarget = CreateCharacter("Másik cél", vitality: 100);
        var service = new EnemySpellcastingService(data, new AlwaysHitRandom());
        var hostiles = new[] { (protectedTarget, new Position(4, 2)) };
        var first = service.SelectSpell(caster, [caster], hostiles, (_, _, _) => true);
        Assert(first?.Spell.Id == "D004" && caster.SpellResistanceEstimates.Count == 0 &&
               otherCaster.SpellResistanceEstimates.Count == 0,
            "Az ellenséges mágus már az első találat előtt ismerte a tűzvédelmet.");
        service.Execute(caster, first!);
        var estimate = caster.EstimatedSpellResistance(protectedTarget.Id, DamageType.Fire);
        Assert(estimate is >= 0 and <= 100 && estimate > 0 &&
               caster.EstimatedSpellResistance(protectedTarget.Id, DamageType.Necrotic) is null &&
               caster.EstimatedSpellResistance(anotherTarget.Id, DamageType.Fire) is null &&
               otherCaster.EstimatedSpellResistance(protectedTarget.Id, DamageType.Fire) is null,
            "A megfigyelt ellenállás más típusra, célpontra vagy mágusra is átszivárgott.");
        caster.AdvanceCombatCooldowns();
        var revised = service.SelectSpell(caster, [caster], hostiles, (_, _, _) => true);
        Assert(revised?.Spell.Id == "D001",
            "A megtapasztalt tűzellenállás után a mágus nem értékelte fel a másik sebzéstípust.");
        var alternateAim = service.SelectSpell(caster, [caster],
            [(protectedTarget, new Position(4, 2)), (anotherTarget, new Position(4, 3))],
            (_, _, _) => true);
        Assert(alternateAim?.Spell.Id == "D004" && alternateAim.HostileTargets.Single() == anotherTarget,
            "Az ismert tűzvédelem mellett a mágus nem a védtelenebb célpontot választotta.");
        var interruptedPlan = service.SelectSpell(otherCaster, [otherCaster],
            [(protectedTarget, new Position(4, 2))], (_, _, _) => true)!;
        service.Execute(otherCaster, interruptedPlan, combatFailureChance: 100);
        Assert(otherCaster.SpellResistanceEstimates.Count == 0,
            "A meghiúsult varázslat is elárulta a célpont ellenállását.");
        otherCaster.AdvanceCombatCooldowns();
        Assert(service.SelectSpell(otherCaster, [otherCaster],
                   [(protectedTarget, new Position(4, 2))], (_, _, _) => true)?.Spell.Id == "D004",
            "A másik mágus saját megfigyelés nélkül is módosította a választását.");

        var lowIntelligence = new ConfiguredEnemy(new Position(1, 1),
            definition with { SpellcasterProfile = profile with { Intelligence = 1 } });
        var highIntelligence = new ConfiguredEnemy(new Position(1, 1),
            definition with { SpellcasterProfile = profile with { Intelligence = 20 } });
        foreach (var fraction in new[] { 0d, 1d })
        {
            lowIntelligence.ObserveSpellResistance(protectedTarget.Id, DamageType.Fire,
                40, 1, new FixedFractionRandom(fraction));
            highIntelligence.ObserveSpellResistance(protectedTarget.Id, DamageType.Fire,
                40, 20, new FixedFractionRandom(fraction));
            var lowEstimate = lowIntelligence.EstimatedSpellResistance(protectedTarget.Id, DamageType.Fire)!.Value;
            var highEstimate = highIntelligence.EstimatedSpellResistance(protectedTarget.Id, DamageType.Fire)!.Value;
            Assert(lowEstimate is >= 20 and <= 60 && highEstimate is >= 39.2 and <= 40.8 &&
                   Math.Abs(highEstimate - 40) < Math.Abs(lowEstimate - 40),
                "A mágus Intelligenciája nem csökkenti a becslés legfeljebb ±50%-os hibáját.");
        }
        var saved = new EnemySaveData(caster.Position, caster.Definition.Id, caster.CurrentHitPoints,
            SpellResistanceEstimates: caster.SpellResistanceEstimates.ToList());
        var roundTrip = JsonSerializer.Deserialize<EnemySaveData>(JsonSerializer.Serialize(saved))!;
        var restored = new ConfiguredEnemy(caster.Position, caster.Definition);
        restored.RestoreSpellResistanceEstimates(roundTrip.SpellResistanceEstimates);
        Assert(restored.EstimatedSpellResistance(protectedTarget.Id, DamageType.Fire) == estimate,
            "Az ellenség megszerzett, pontatlan ellenállásismerete elveszett a mentési körben.");
    }

    private sealed class FixedFractionRandom(double fraction) : Random
    {
        public override double NextDouble() => fraction;
    }

    private sealed class AlwaysHitRandom : Random
    {
        public override int Next(int maxValue) => 0;
        public override int Next(int minValue, int maxValue) => maxValue - 1;
        public override double NextDouble() => .5;
    }

    static void EnemyCastersLeadRareThematicLevelGroups()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var casterIds = data.EnemySpellcasters.Select(profile => profile.EnemyId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var configuredLevels = new[] { 3, 4, 6, 7, 9, 10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 };

        foreach (var level in configuredLevels)
        {
            var configuration = MazeLevelConfigurations.Get(level);
            var encounters = configuration.RoomEncounters.Concat(configuration.CorridorEncounters).ToArray();
            Assert(encounters.Any(encounter => encounter.Members.Any(member =>
                       casterIds.Contains(member.EnemyId) && member.Role == EnemyGroupRole.Leader)),
                $"A(z) {level}. szinten nincs caster által vezetett csoport.");
            Assert(encounters.Any(encounter => encounter.Members.All(member => !casterIds.Contains(member.EnemyId))),
                $"A(z) {level}. szint minden találkozása casteres lett.");
            Assert(encounters.SelectMany(encounter => encounter.Members)
                    .Where(member => casterIds.Contains(member.EnemyId) && member.Role != EnemyGroupRole.Leader)
                    .All(member => member.Count.Maximum <= Amount.Handful.Range().Maximum),
                $"A(z) {level}. szinten túl nagy tömegben kerültek kísérőszerepbe casterek.");
        }
    }

    static void EnemyActionSelectionUsesScoredShortlist()
    {
        var clearlyBest = new ScoredAction("varázslat", 100);
        var closeAlternative = new ScoredAction("fegyver", 94);
        var weakAlternative = new ScoredAction("rossz képesség", 60);
        var candidates = new[] { clearlyBest, closeAlternative, weakAlternative };
        var selectedNames = Enumerable.Range(0, 100)
            .Select(seed => EnemyActionSelectionPolicy.Select(candidates, candidate => candidate.Score,
                new Random(seed))!.Name)
            .ToHashSet();

        Assert(selectedNames.Contains(clearlyBest.Name) && selectedNames.Contains(closeAlternative.Name),
            "A közeli értékű akciók között nincs meg a tervezett kis változatosság.");
        Assert(!selectedNames.Contains(weakAlternative.Name),
            "A 10%-os eltérés egy egyértelműen gyengébb akciót is kiválaszthatott.");
    }

    private sealed record ScoredAction(string Name, double Score);
}
