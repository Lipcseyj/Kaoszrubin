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
        Assert(data.Spells.Count(spell => spell.EnemyOnly) == 17 &&
               data.Spells.Where(spell => spell.EnemyOnly).All(spell => spell.Id.StartsWith('D')),
            "A sötét ellenséges varázslatkészlet hiányos vagy hibásan van megjelölve.");
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

    static void EnemyCastersLeadRareThematicLevelGroups()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var casterIds = data.EnemySpellcasters.Select(profile => profile.EnemyId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var configuredLevels = new[] { 3, 4, 6, 8, 9, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 };

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
