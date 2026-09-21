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
        foreach (var enemyId in expected)
        {
            var enemy = data.GetEnemy(enemyId);
            Assert(enemy.SpellcasterProfile is { SpellIds.Count: > 0, MaximumMana: > 0, Intelligence: > 0 },
                $"A(z) {enemyId} ellenség feloldott varázsprofilja hiányzik.");
        }
    }

    static void EnemySpellcastingUsesManaTargetsPartyAndPersists()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var baseDefinition = data.GetEnemy(MonsterIds.GoblinVajákos);
        var profile = baseDefinition.SpellcasterProfile! with
        {
            SpellIds = ["S001"], CastingChancePercent = 100, ManaReservePercent = 0
        };
        var caster = new ConfiguredEnemy(new Position(2, 2), baseDefinition with { SpellcasterProfile = profile },
            new Random(1));
        var target = CreateCharacter("Varázscél", vitality: 100);
        var service = new EnemySpellcastingService(data, new Random(2));

        var plan = service.SelectSpell(caster, [caster], [(target, new Position(4, 2))], (_, _, _) => true);
        Assert(plan is { Spell.Id: "S001", HostileTargets.Count: 1 } && plan.HostileTargets[0] == target,
            "Az ellenséges varázsló nem a parti érvényes célpontját választotta.");
        var manaBefore = caster.CurrentMana;
        var hpBefore = target.CurrentVitality;
        service.Execute(caster, plan!);
        Assert(caster.CurrentMana == manaBefore - plan!.Spell.ManaCost && target.CurrentVitality < hpBefore &&
               !caster.IsSpellReady(plan.Spell.Id),
            "A varázslás nem fogyasztott mannát, nem sebzett vagy nem indította el a lehűlést.");

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
            { SpellIds = ["S007"], CastingChancePercent = 100, ManaReservePercent = 0 }
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
            { SpellIds = ["P005"], CastingChancePercent = 100, ManaReservePercent = 0 }
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
}
