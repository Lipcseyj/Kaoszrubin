internal static partial class Program
{
    static void AdaptableRaceGainsChosenAbility()
    {
        var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero, RaceTraits.Adaptable);
        var characterClass = new CharacterClassDefinition("C001", "Harcos", PrimaryAbilities.Zero, false, 1.0);
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = LiveCharacterFactory.Create("Ember", race, characterClass,
            new PrimaryAbilities(5, 5, 5, 5), 1, 1, data, ConsoleColor.Cyan,
            new PrimaryAbilities(0, 0, 0, 1));
        Assert(character.Abilities == new PrimaryAbilities(5, 5, 5, 6),
            "Az Alkalmazkodó tulajdonság nem a kiválasztott képességre adta a +1-et.");
        Assert(PerkProgressionRules.TriggerLevel(race, 1) == 4 &&
               PerkProgressionRules.TriggerLevel(race, 2) == 15,
            "Az Alkalmazkodó ember tehetségszintjei hibásak.");
    }

    static void CharacterVisionRangeUsesClassRaceAndEffects()
    {
        var abilities = new PrimaryAbilities(5, 5, 5, 5);
        var human = new RaceDefinition("R-HUMAN", "Ember", PrimaryAbilities.Zero);
        var elf = new RaceDefinition("R-ELF", "Elf", PrimaryAbilities.Zero, RaceTraits.KeenSenses);
        var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos", PrimaryAbilities.Zero,
            false, 1.0);
        var thiefClass = new CharacterClassDefinition(CharacterClassIds.Tolvaj, "Tolvaj", PrimaryAbilities.Zero,
            false, 1.0);
        var fighter = new LiveCharacter("Harcos", human, fighterClass, abilities, 20, 0, 1, 0);
        var thief = new LiveCharacter("Tolvaj", human, thiefClass, abilities, 20, 0, 1, 0);
        var elfThief = new LiveCharacter("Elf tolvaj", elf, thiefClass, abilities, 20, 0, 1, 0);
        Assert(CharacterClassRules.VisionRange(fighter) == 5 &&
               CharacterClassRules.VisionRange(thief) == 7 &&
               CharacterClassRules.VisionRange(elfThief) == 8,
            "A karakter 5/7/8-as alap-, tolvaj- vagy elf látótávja hibás.");

        var darkLevelLine = CharacterSheetPanel.Build(fighter, new Dictionary<int, int> { [2] = 100 },
            9, 0, 12).Single(line => line.Row == 4);
        Assert(CharacterClassRules.VisionRange(fighter, -2) == 3 && darkLevelLine.Text.EndsWith("3") &&
               darkLevelLine.ColoredTextStart == darkLevelLine.Text.Length - 1 &&
               darkLevelLine.ColoredTextColor == ConsoleColor.Red,
            "Az extra sötét pálya nem csökkenti vagy nem pirosítja a látótávot.");

        fighter.ApplySpellEffect(new ActiveSpellEffect("LIGHT", ActiveSpellEffectType.VisionBonus, 2, 12, Beneficial: true));
        Assert(CharacterClassRules.NaturalVisionRange(fighter) == 5 &&
               CharacterClassRules.VisionRange(fighter) == 7,
            "A pozitív látótávhatás nem különül el a természetes látótávtól.");
        var increasedLine = CharacterSheetPanel.Build(fighter, new Dictionary<int, int> { [2] = 100 },
            1, 0, 12).Single(line => line.Row == 4);
        Assert(increasedLine.Text.EndsWith("7") && increasedLine.ColoredTextStart == increasedLine.Text.Length - 1 &&
               increasedLine.ColoredTextColor == ConsoleColor.Green,
            "A növelt látótáv száma nem zöld a karakterlapon.");

        fighter.ApplySpellEffect(new ActiveSpellEffect("DARKNESS", ActiveSpellEffectType.VisionBonus, -4, 12));
        var decreasedLine = CharacterSheetPanel.Build(fighter, new Dictionary<int, int> { [2] = 100 },
            1, 0, 12).Single(line => line.Row == 4);
        Assert(CharacterClassRules.VisionRange(fighter) == 3 && decreasedLine.Text.EndsWith("3") &&
               decreasedLine.ColoredTextStart == decreasedLine.Text.Length - 1 &&
               decreasedLine.ColoredTextColor == ConsoleColor.Red,
            "A csökkentett látótáv értéke vagy piros kijelzése hibás.");
    }

    static void EnemyVisionRangesLoadFromCsv()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(data.GetEnemy("E001").VisionRange == 3 && data.GetEnemy("E001").Stealth == 1 &&
               data.GetEnemy("E026").Noise == 4 && data.GetEnemy("E045").Stealth == 3 &&
               data.GetEnemy("E003").VisionRange == 4 &&
               data.GetEnemy("E019").VisionRange == 7 && data.GetEnemy("E050").VisionRange == 8,
            "A patkány, goblin, vámpír vagy káoszsárkány CSV-látótávja hibás.");
    }

    static void FogRevealUsesVariableRangeAndLineOfSight()
    {
        var maze = new Maze(22, 13);
        for (var x = 2; x <= 20; x++) maze.Carve(new Position(x, 2));
        for (var y = 2; y <= 11; y++) maze.Carve(new Position(2, y));
        var origin = maze.Entrance;
        var normalFog = new FogOfWar(maze.Width, maze.Height, 5);
        normalFog.RevealFrom(maze, origin, 5);
        var horizontallyFar = new Position(13, 2);
        var verticallyFar = new Position(2, 8);
        Assert(!normalFog.IsRevealed(horizontallyFar) && !normalFog.IsRevealed(verticallyFar),
            "Az ötrácsos látótáv túl messzire fedett fel.");

        var scoutFog = new FogOfWar(maze.Width, maze.Height, 5);
        scoutFog.RevealFrom(maze, origin, 8);
        Assert(scoutFog.IsRevealed(horizontallyFar) && scoutFog.IsRevealed(verticallyFar) &&
               FogOfWar.IsWithinVisionRange(origin, new Position(18, 2), 8) &&
               !FogOfWar.IsWithinVisionRange(origin, new Position(19, 2), 8),
            "A nyolcas látótáv nem alkalmazza a vízszintes 2:1 képarány-korrekciót.");

        maze.PlaceDoor(new Position(4, 2), DoorState.Closed);
        var blockedFog = new FogOfWar(maze.Width, maze.Height, 5);
        blockedFog.RevealFrom(maze, origin, 8);
        Assert(!blockedFog.IsRevealed(new Position(5, 2)), "A zárt ajtó mögé átlátott a felfedés.");
    }

    static void MonsterTraitsAndAbilitiesAreDataDriven()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var medusa = data.GetEnemy(MonsterIds.Medúza);
        var troll = data.GetEnemy("E013");
        var dragon = data.GetEnemy("E021");
        var gaze = data.GetMonsterAbility("MA011");
        Assert(medusa.AbilityIds.Contains("MA011") &&
               gaze.Trigger == MonsterAbilityTrigger.Active && gaze.Cooldown == 3 &&
               gaze.Range == 3 && gaze.StatusId == "STATUS006" &&
               troll.AbilityIds.Contains("MA008") &&
               dragon.HasTrait(EnemyTraits.Flying) &&
               data.GetEnemy("E004").HasTrait(EnemyTraits.Undead) &&
               !data.GetEnemy("E004").AbilityIds.Contains(MonsterAbilityIds.Undead) &&
               data.GetEnemy("E018").AbilityIds.Contains("MA012") &&
               data.GetEnemy("E022").AbilityIds.Contains("MA013") &&
               data.GetMonsterAbility("MA013").MaximumTargets == 2,
            "A jellemzők és a paraméterezett képességek szétválasztása hibás.");
    }

    static void SpellBuffDurationLoadsAsRounds()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var blessing = catalog.GetSpellEffects("P003");
        Assert(blessing.Count == 3 && blessing.All(effect => effect.Duration == 4) &&
               blessing.All(effect => effect.Description.Contains("kör", StringComparison.OrdinalIgnoreCase)),
            "Az Áldás CSV-ben megadott négykörös időtartama vagy leírása nem töltődött be.");

        var active = new ActiveSpellEffect("P003", ActiveSpellEffectType.HitBonus, 1, 4, Beneficial: true);
        var json = JsonSerializer.Serialize(active);
        var restored = JsonSerializer.Deserialize<ActiveSpellEffect>(json);
        Assert(json.Contains("RemainingActions", StringComparison.Ordinal) && restored?.RemainingRounds == 4,
            "A köralapú varázshatás nem kompatibilis a korábbi mentések RemainingActions mezőjével.");
    }

    static void NewSpellEffectsAreSupported()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        foreach (var id in new[] { "S027", "S028", "P026", "P027", "P028" })
            Assert(catalog.GetSpellEffects(id).Count > 0, $"A(z) {id} varázslat hatásai hiányoznak.");

        var enemy = new ConfiguredEnemy(new Position(3, 3), catalog.GetEnemy("E001"));
        enemy.ApplySpellEffect(new ActiveSpellEffect("S027", ActiveSpellEffectType.HitBonus, -4, 4));
        enemy.ApplySpellEffect(new ActiveSpellEffect("S027", ActiveSpellEffectType.VisionBonus, -3, 4));
        Assert(enemy.SpellEffectValue(ActiveSpellEffectType.HitBonus) == -4 &&
               enemy.EffectiveVisionRange == Math.Max(1, enemy.Definition.VisionRange - 3),
            "A Vakítás nem rontja az ellenfél találatát és látótávját.");

        var ally = CreateCharacter("Átok sújtott");
        ally.ApplySpellEffect(new ActiveSpellEffect("P003", ActiveSpellEffectType.DefenseBonus, 2, 4,
            Beneficial: true));
        ally.ApplySpellEffect(new ActiveSpellEffect("S027", ActiveSpellEffectType.HitBonus, -4, 4));
        var service = new SpellExecutionService(catalog, new Random(1));
        var maze = new Maze(7, 7);
        service.DispelAt(new Position(2, 2), 0, maze, [(ally, new Position(2, 2))], "HarmfulOnly");
        Assert(ally.HasSpellEffect(ActiveSpellEffectType.DefenseBonus) &&
               !ally.HasSpellEffect(ActiveSpellEffectType.HitBonus),
            "Az Átoktörés a káros hatás helyett a hasznos buffot is eltávolította.");

        Assert(service.IsOffensiveSpell(catalog.GetSpell("S027")) &&
               service.IsOffensiveSpell(catalog.GetSpell("S028")) &&
               service.IsOffensiveSpell(catalog.GetSpell("P028")),
            "Az új támadó vagy kontrollvarázslatok nem minősülnek támadónak.");

        var golemDefinition = catalog.GetEnemy("E039");
        var blackDragonDefinition = catalog.GetEnemy("E025");
        Assert(golemDefinition.Armor is { Minimum: 7, Maximum: 13 } &&
               golemDefinition.AverageArmor == 10 && golemDefinition.MagicResistance == 45 &&
               blackDragonDefinition.Armor is { Minimum: 7, Maximum: 13 } &&
               blackDragonDefinition.MagicResistance == 35,
            "Az ellenfél CSV-ben megadott páncélja vagy varázsvédelme hibás.");

        var resistantEnemy = new ConfiguredEnemy(new Position(3, 3), golemDefinition);
        var testSpell = new SpellDefinition("TEST-MAGIC-RESISTANCE", "Próbavarázs", SpellSchool.Arcane,
            1, 0, "", SpellTargetType.Enemy, 6, 0, true, SpellUsageMode.Combat);
        var testDamage = new SpellEffectDefinition("TEST-MAGIC-DAMAGE", testSpell.Id, 1,
            SpellEffectType.Damage, null, 0, 0, 100, 0, 100, SpellResolution.Auto, null, "");
        var resistedDamage = service.ResolveSpellDamage(ally, testDamage, testSpell, resistantEnemy,
            new Dictionary<(Enemy, SpellResolution), SpellResolutionResult>(), []);
        Assert(resistedDamage == 55,
            "A 45 százalékos varázsvédelem nem csökkentette 100-ról 55-re a közvetlen varázssebzést.");
    }

    static void MonsterRegenerationAndBreathCooldownWork()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var troll = new ConfiguredEnemy(new Position(1, 1), data.GetEnemy("E013"));
        troll.SetCurrentHitPoints(400);
        var battle = new BattleSystem(new Random(7), data.MonsterAbilities, data.Statuses, data.StrengthHitBonuses);
        var start = battle.BeginEnemyTurn(troll);
        Assert(troll.CurrentHitPoints == 405 && start.Entries.Any(entry => entry.Message.Contains("regenerálódik")),
            "A Troll kör eleji regenerációja nem működik.");

        var dragon = new ConfiguredEnemy(new Position(1, 1), data.GetEnemy("E021"));
        var breath = data.GetWeapon("WN006");
        var preparation = battle.PrepareEnemyWeapon(dragon, breath);
        Assert(dragon.IsWeaponPrepared(breath.Id) &&
               battle.SelectEnemyAttackWeapon(dragon)?.Id == breath.Id &&
               preparation.Message.Contains("következő saját körében"),
            "A lehelet előkészítése vagy előrejelzése hibás.");
        battle.MarkEnemyWeaponUsed(dragon, breath);
        Assert(!dragon.IsWeaponPrepared(breath.Id) && !dragon.IsWeaponReady(breath.Id),
            "A lehelet elsütése nem törölte az előkészítést vagy nem indította el a lehűlést.");
        battle.BeginEnemyTurn(dragon);
        battle.BeginEnemyTurn(dragon);
        Assert(!dragon.IsWeaponReady(breath.Id), "A lehelet túl korán vált újra használhatóvá.");
        battle.BeginEnemyTurn(dragon);
        Assert(dragon.IsWeaponReady(breath.Id), "A lehelet nem vált használhatóvá három saját kör után.");
    }

    static void TimedNonDamageStatusExpires()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Dermedt");
        character.AddStatus(data.GetStatus("STATUS006"));
        var first = character.ApplyTurnEndStatusEffects(new Random(1));
        var second = character.ApplyTurnEndStatusEffects(new Random(1));
        Assert(first.Count == 1 && first[0].Damage == 0 && !first[0].Expired &&
               second.Count == 1 && second[0].Expired && !character.HasStatus("STATUS006"),
            "A sebzés nélküli időzített állapot nem két saját akció után járt le.");
    }
    static void CompositeMonsterAbilityAppliesAllEffects()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var source = data.GetMonsterAbility("MA012");
        var ability = source with { ChancePercent = 100 };
        var definition = data.GetEnemy("E018") with { AbilityIds = ["MA012"] };
        var enemy = new ConfiguredEnemy(new Position(1, 1), definition);
        var target = CreateCharacter("Sugárcél", vitality: 100);
        var battle = new BattleSystem(new Random(1), [ability], data.Statuses, data.StrengthHitBonuses);
        var runtime = battle.PrepareCharacter(target).Runtime;
        battle.PrepareEnemyForBattle(enemy);

        var before = target.CurrentVitality;
        var result = battle.ResolveEnemyAbility(enemy, target, runtime, ability);

        Assert(target.HasStatus("STATUS006") && target.CurrentVitality == before - 4 &&
               enemy.RemainingAbilityCharges.GetValueOrDefault("MA012") == 1 &&
               result.Message.Contains("nekrotikus", StringComparison.OrdinalIgnoreCase) &&
               result.Message.Contains("4 nekrotikus", StringComparison.Ordinal),
            "Az összetett Bénító sugár nem alkalmazta együtt az állapotot, sebzést és töltetfogyást.");
    }

    static void MonsterAbilityRespectsWeaponBinding()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var poison = data.GetMonsterAbility("MA002") with { ChancePercent = 100, WeaponIds = ["WN018"] };
        var dagger = data.GetWeapon("W001");
        var fangs = data.GetWeapon("WN018");
        var enemy = new ConfiguredEnemy(new Position(1, 1), new EnemyDefinition(
            "E-BIND", "Kötési próba", "k", 1, 100, 0, 100, 1, 1, ["MA002"],
            Weapons: [dagger, fangs]));
        var target = CreateCharacter("Kötési cél", vitality: 500);
        var battle = new BattleSystem(new Random(2), [poison], data.Statuses, data.StrengthHitBonuses);
        var runtime = battle.PrepareCharacter(target).Runtime;

        for (var i = 0; i < 20; i++) battle.ResolveEnemyAction(enemy, target, runtime, dagger);
        Assert(!target.HasStatus(CharacterStatusIds.Poisoned),
            "A mérgezés a hozzá nem kötött tőrrel is aktiválódott.");

        for (var i = 0; i < 20 && !target.HasStatus(CharacterStatusIds.Poisoned); i++)
            battle.ResolveEnemyAction(enemy, target, runtime, fangs);
        Assert(target.HasStatus(CharacterStatusIds.Poisoned),
            "A mérgezés a hozzá kötött méregfogakkal sem aktiválódott.");
    }
    static void EnemyAwarenessAndSearchAreDataDriven()
    {
        var definition = new EnemyDefinition("E-SLEEP", "Alvó őr", "e", 1, 10, 0, 1,
            1, 1, Array.Empty<string>(), VisionRange: 6, CanSleep: true);
        var enemy = new ConfiguredEnemy(new Position(4, 5), definition);
        enemy.ConfigureMovement(EnemyMovementProfile.Stationary, Direction.Left);
        enemy.ConfigureAwareness(EnemyAlertness.Sleeping, new Position(4, 5));
        Assert(enemy.MovementProfile == EnemyMovementProfile.Stationary &&
               enemy.Alertness == EnemyAlertness.Sleeping && enemy.EffectiveVisionRange == 1,
            "Az alvó álló ellenfél profilja vagy csökkentett észlelése hibás.");

        var target = CharacterId.New();
        enemy.BeginPursuit(target, new Position(9, 5), 4);
        enemy.RefreshKnownTarget(new Position(10, 5));
        Assert(enemy.Alertness == EnemyAlertness.Alert && enemy.PursuitState == EnemyPursuitState.Pursuing &&
               enemy.EffectiveVisionRange == 6 && enemy.ConsumeReactionDelay() &&
               enemy.ReactionDelayMovesRemaining == 3,
            "Az észlelés nem ébresztette fel késleltetve az álló ellenfelet.");
        enemy.BeginSearch(1, enemy.LastKnownTargetPosition ?? enemy.Position, EnemySearchRole.Scout);
        enemy.RecordSearchVisit(new Position(9, 5));
        Assert(enemy.SearchRole == EnemySearchRole.Scout &&
               enemy.SearchMovesRemaining == Enemy.MinimumSearchMoves,
            "A felderítés nem tartja be a harminclépéses minimumot.");

        var undead = new ConfiguredEnemy(new Position(1, 1), new EnemyDefinition(
            "E-UNDEAD", "Élőholt", "u", 1, 10, 0, 1, 1, 1, [MonsterAbilityIds.Undead], CanSleep: false));
        undead.ConfigureAwareness(EnemyAlertness.Sleeping);
        Assert(undead.Alertness == EnemyAlertness.Alert,
            "Az alvásra képtelen ellenfél alvó állapotba került.");

        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(data.GetEnemy("E003").CanSleep && data.GetEnemy("E007").CanSleep &&
               !data.GetEnemy("E004").CanSleep && !data.GetEnemy("E006").CanSleep,
            "A goblin, ork vagy élőholt alvásképessége hibásan töltődött be a CSV-ből.");

        var saved = new EnemySaveData(enemy.Position, enemy.Definition.Id, enemy.CurrentHitPoints,
            Alertness: enemy.Alertness, SearchRole: enemy.SearchRole, HomePosition: enemy.HomePosition,
            LastKnownTargetPosition: enemy.LastKnownTargetPosition,
            ReactionDelayMovesRemaining: enemy.ReactionDelayMovesRemaining,
            SearchMovesRemaining: enemy.SearchMovesRemaining,
            LastKnownTargetDirection: enemy.LastKnownTargetDirection,
            SearchAnchorPosition: enemy.SearchAnchorPosition,
            SearchVisitedPositions: enemy.SearchVisitedPositions.ToList());
        var restored = JsonSerializer.Deserialize<EnemySaveData>(JsonSerializer.Serialize(saved));
        Assert(restored?.SearchRole == EnemySearchRole.Scout &&
               restored.SearchMovesRemaining == Enemy.MinimumSearchMoves &&
               restored.HomePosition == new Position(4, 5) &&
               restored.SearchAnchorPosition == new Position(10, 5) &&
               restored.SearchVisitedPositions?.Contains(new Position(9, 5)) == true,
            "Az éberségi és felderítési állapot nem élte túl a mentési JSON-körutat.");
    }

    static void EnemyPackSearchStaysCoordinated()
    {
        var target = CharacterId.New();
        var anchor = new Position(8, 6);
        var group = Enumerable.Range(0, 5).Select(index =>
        {
            var definition = new EnemyDefinition($"E-PACK-{index}", $"Falkatag {index}", "f", 1, 20, 0,
                index + 1, 1, 1, []);
            var enemy = new ConfiguredEnemy(new Position(4 + index, 4), definition);
            enemy.BeginPursuit(target, anchor, 0, 10);
            return (Enemy)enemy;
        }).ToArray();

        EnemySearchCoordinator.BeginCoordinatedSearch(group, group[0], new Random(7));

        Assert(group.Count(enemy => enemy.SearchRole == EnemySearchRole.Scout) == 2 &&
               group.Count(enemy => enemy.SearchRole == EnemySearchRole.Guarding) == 3 &&
               group.All(enemy => enemy.SearchAnchorPosition == anchor) &&
               group.Select(enemy => enemy.SearchMovesRemaining).Distinct().Count() == 1 &&
               group.Where(enemy => enemy.SearchRole == EnemySearchRole.Scout)
                   .All(enemy => group.All(member => enemy.SearchVisitedPositions.Contains(member.Position))) &&
               group.All(enemy => enemy.PursuitState == EnemyPursuitState.Undecided),
            "A falka nem közös pont körül, összehangolt szerepekkel kezdte meg a keresést.");
    }

    static void CorridorGroupsBecomeLedHordes()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var hordeEncounter = new ResolvedEnemyEncounter(new IntRange(1, 1),
            [new ResolvedEnemyGroupMember(data.GetEnemy(MonsterIds.Ork), new IntRange(4, 4), EnemyGroupRole.Member)],
            EnemyMovementProfile.Wander, EnemyEncounterBehavior.Horde);
        var regularEncounter = hordeEncounter with
        {
            MovementProfile = EnemyMovementProfile.Patrol,
            Behavior = EnemyEncounterBehavior.Default
        };
        var settings = new MazeGenerationSettings
        {
            RoomCount = 0,
            TreasureChestCount = 0
        };
        var horde = new MazeGenerator(settings, [], [hordeEncounter], new Random(701))
            .Create(43, 31).Enemies.ToArray();
        var regularGroup = new MazeGenerator(settings, [], [regularEncounter], new Random(702))
            .Create(43, 31).Enemies.ToArray();

        Assert(horde.Length == 4 && horde.Select(enemy => enemy.GroupId).Distinct().Count() == 1 &&
               horde.All(enemy => enemy.IsRoamingHordeMember) &&
               horde.Count(enemy => enemy.GroupRole == EnemyGroupRole.Leader) == 1 &&
               regularGroup.Length == 4 && regularGroup.All(enemy => !enemy.IsRoamingHordeMember) &&
               regularGroup.All(enemy => enemy.MovementProfile == EnemyMovementProfile.Patrol),
            "A Horde jelölés nem explicit módon vezérli a folyosói horda létrejöttét.");
    }

    static void HordeRoamingStatePersistsAndYieldsToPursuit()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Hordaőr");
        var roster = new CharacterRoster();
        roster.Add(character);
        roster.Select(character);
        var maze = new Maze(9, 9);
        var enemyPosition = new Position(3, 2);
        maze.Carve(enemyPosition);
        maze.Carve(maze.Exit);
        maze.PlaceExit(maze.Exit);
        var leader = new ConfiguredEnemy(enemyPosition, data.GetEnemy(MonsterIds.Ork));
        leader.ConfigureGroup(Enemy.HordeGroupPrefix + "SAVE", EnemyGroupRole.Leader);
        var campUntil = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        leader.BeginHordeCamp(campUntil);
        maze.AddEnemy(leader);
        var mapper = new GameStateMapper(data, roster, character);
        var save = mapper.Create(6, maze, new Player(maze.Entrance, character),
            new FogOfWar(maze.Width, maze.Height, 5), Direction.Right, [maze.Entrance],
            false, false, false, false, null, DateTime.UtcNow,
            new Dictionary<Enemy, DateTime> { [leader] = DateTime.UtcNow }, [], []);
        var restored = mapper.Restore(JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(save))!);
        var restoredLeader = restored.Maze.Enemies.Single();

        Assert(restoredLeader.IsRoamingHordeMember &&
               restoredLeader.GroupRole == EnemyGroupRole.Leader &&
               restoredLeader.HordeCampUntilUtc is { } restoredUntil &&
               restoredUntil > DateTime.UtcNow + TimeSpan.FromMinutes(1),
            "A horda táborozási állapota nem élte túl a mentési kört.");
        restoredLeader.BeginPursuit(character.Id, new Position(6, 6), 0);
        Assert(restoredLeader.HordeCampUntilUtc is null && restoredLeader.HordeDestination is null &&
               restoredLeader.PursuitState == EnemyPursuitState.Pursuing,
            "A horda táborozása nem szakadt meg, amikor észlelte a partit.");
    }

    static void EnemySearchExploresCorridorFrontiers()
    {
        var directions = Enum.GetValues<Direction>();
        var anchor = new Position(2, 2);
        var junction = new Position(3, 2);
        var corridor = new HashSet<Position>
    {
        anchor, junction, new(4, 2), new(3, 1), new(3, 3)
    };
        var searched = new HashSet<Position> { anchor, junction, new(4, 2) };
        var branch = EnemySearchNavigator.ChooseScoutDirection(junction, anchor, Direction.Right,
            Enemy.SearchCohesionRadius, searched, directions, corridor.Contains, new Random(2));
        Assert(branch is Direction.Up or Direction.Down,
            "A felderítő a már bejárt folyosó helyett nem választott új elágazást.");

        var radiusAnchor = new Position(10, 10);
        var boundary = new Position(16, 10);
        var boundaryCorridor = new HashSet<Position> { new(15, 10), boundary, new(17, 10) };
        var inward = EnemySearchNavigator.ChooseScoutDirection(boundary, radiusAnchor, Direction.Right,
            Enemy.SearchCohesionRadius, [boundary, new Position(15, 10)], directions,
            boundaryCorridor.Contains, new Random(3));
        Assert(inward == Direction.Left,
            "A felderítő elhagyhatta a falka hatmezős keresési körzetét.");
    }

    static void EnemyTrackingSenseIsDataDriven()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(data.GetEnemy("E001").TrackingSense == 6 && data.GetEnemy("E005").TrackingSense == 8 &&
               data.GetEnemy("E004").TrackingSense == 5 && data.GetEnemy("E022").TrackingSense == 8 &&
               data.GetEnemy("E028").TrackingSense == 1,
            "A patkányok, farkasok, élőholtak vagy emberek nyomérzéke hibásan töltődött be.");

        var nearer = CreateCharacter("Közelebbi");
        var preferred = CreateCharacter("Üldözött");
        var positions = new[]
        {
        (nearer, new Position(2, 1)),
        (preferred, new Position(4, 1))
    };
        int? Distance(Position position) => position.X - 1;
        var sensed = EnemyTargeting.ChooseNearestSensed(new Position(1, 1), positions, 4, Distance,
            new Random(1), preferred.Id);
        Assert(sensed?.Character == preferred &&
               EnemyTargeting.ChooseNearestSensed(new Position(1, 1), positions, 0, Distance,
                   new Random(1)) is null &&
               EnemyTargeting.ChooseNearestSensed(new Position(1, 1), positions, 1, Distance,
                   new Random(1))?.Character == nearer,
            "A nyomérzék hatótávja vagy a már üldözött célpont elsőbbsége hibás.");
    }

    static void PartyFormationPositionsFollowFacing()
    {
        var leader = CreateCharacter("Alakzatvezer");
        var right = CreateCharacter("Jobbszel");
        var rearLeft = CreateCharacter("Hatso bal");
        var rearRight = CreateCharacter("Hatso jobb");
        var formation = new PartyFormationSnapshot(leader.Id, right.Id, rearLeft.Id, rearRight.Id,
            Direction.Up, PartyFormationState.Locked);
        var up = PartyFormationRules.Positions(formation, leader.Id, new Position(10, 10));
        var turned = PartyFormationRules.Rotate(formation, clockwise: true);
        var facingRight = PartyFormationRules.Positions(turned, leader.Id, new Position(10, 10));
        Assert(up[leader.Id] == new Position(10, 10) && up[right.Id] == new Position(11, 10) &&
               up[rearLeft.Id] == new Position(10, 11) && up[rearRight.Id] == new Position(11, 11) &&
               facingRight[leader.Id] == new Position(10, 10) &&
               facingRight[right.Id] == new Position(10, 11) &&
               facingRight[rearLeft.Id] == new Position(9, 10) &&
               facingRight[rearRight.Id] == new Position(9, 11),
            "Az alakzat slotjai nem fordultak el helyesen a vezér körül.");
    }

    static void PartyFormationTurnsInPlace()
    {
        var leader = CreateCharacter("Fordulo");
        var right = CreateCharacter("Jobb");
        var rearLeft = CreateCharacter("HatsoBal");
        var rearRight = CreateCharacter("HatsoJobb");
        var formation = new PartyFormationSnapshot(leader.Id, right.Id, rearLeft.Id, rearRight.Id,
            Direction.Up, PartyFormationState.Locked);
        var before = PartyFormationRules.Positions(formation, leader.Id, new Position(10, 10));
        var clockwise = PartyFormationRules.RotateInPlace(formation, clockwise: true);
        var afterClockwise = PartyFormationRules.PositionsInSameFootprint(formation, leader.Id,
            new Position(10, 10), clockwise.Facing);
        var facingLeft = PartyFormationRules.FaceInPlace(formation, Direction.Left);
        var afterFacingLeft = PartyFormationRules.PositionsInSameFootprint(formation, leader.Id,
            new Position(10, 10), facingLeft.Facing);

        Assert(clockwise.Facing == Direction.Right &&
               clockwise.Slots.SequenceEqual(formation.Slots) &&
               facingLeft.Facing == Direction.Left &&
               before.Values.ToHashSet().SetEquals(afterClockwise.Values) &&
               before.Values.ToHashSet().SetEquals(afterFacingLeft.Values) &&
               afterClockwise[leader.Id] == new Position(11, 10) &&
               afterClockwise[right.Id] == new Position(11, 11) &&
               formation.Facing == Direction.Up,
            "A helyben fordulas kilépett a 2x2-es területből, átírta a slotokat vagy idő előtt módosította az állapotot.");
    }

    static void FormationSlotsControlFreeFollowOrder()
    {
        var leader = CreateCharacter("Vezer");
        var frontLeft = CreateCharacter("BalElso");
        var frontRight = CreateCharacter("JobbElso");
        var rearLeft = CreateCharacter("BalHatso");
        var formation = new PartyFormationSnapshot(frontLeft.Id, frontRight.Id, rearLeft.Id, leader.Id,
            Direction.Up, PartyFormationState.Disbanded);
        var followOrder = PartyFormationRules.FollowOrder(formation, leader.Id,
            [leader.Id, rearLeft.Id, frontRight.Id, frontLeft.Id]);

        var maze = new Maze(11, 11);
        var memberPosition = new Position(5, 5);
        var leftTarget = new Position(4, 5);
        var upTarget = new Position(5, 4);
        var rightTarget = new Position(6, 5);
        foreach (var position in new[] { memberPosition, leftTarget, upTarget, rightTarget }) maze.Carve(position);
        var member = new PartyMemberAvatar(memberPosition, frontLeft);
        var player = new Player(new Position(5, 6), leader);
        IReadOnlyList<Position> trail =
        [
            new Position(1, 1), new Position(1, 2), leftTarget, upTarget, rightTarget,
        new Position(6, 6), player.Position
        ];
        var firstStep = PartyMovementController.FollowLeaderTrail(member, 2, maze, player, trail, followOrder: 0);
        var secondStep = PartyMovementController.FollowLeaderTrail(member, 2, maze, player, trail, followOrder: 1);
        var thirdStep = PartyMovementController.FollowLeaderTrail(member, 2, maze, player, trail, followOrder: 2);

        Assert(followOrder.SequenceEqual([frontLeft.Id, frontRight.Id, rearLeft.Id]) &&
               firstStep == rightTarget && secondStep == upTarget && thirdStep == leftTarget,
            "A szabad követés továbbra is a csatlakozási sorrendet vagy közös nyompontot használt a slotsorrend helyett.");
    }

    static void LockedFormationUsesSingleFileLayout()
    {
        var leader = CreateCharacter("Libasorvezér");
        var second = CreateCharacter("Libasor ketto");
        var third = CreateCharacter("Libasor harom");
        var fourth = CreateCharacter("Libasor negy");
        var formation = new PartyFormationSnapshot(leader.Id, second.Id, third.Id, fourth.Id,
            Direction.Up, PartyFormationState.Locked, PartyFormationLayout.SingleFile);
        var positions = PartyFormationRules.Positions(formation, leader.Id, new Position(10, 10));
        var maze = new Maze(17, 17);
        var blockFormation = formation with { Layout = PartyFormationLayout.Block };
        var currentBlock = PartyFormationRules.Positions(blockFormation, leader.Id, new Position(10, 10));
        foreach (var position in currentBlock.Values) maze.Carve(position);
        maze.Carve(new Position(10, 9));
        maze.Carve(new Position(9, 9));
        var blockDestinations = currentBlock.ToDictionary(pair => pair.Key, pair => pair.Value + Direction.Up);
        var shifted = PartyFormationController.SingleFileDestinations(formation, currentBlock, leader.Id,
            new Position(10, 9));
        var turned = PartyFormationController.SingleFileDestinations(formation, shifted, leader.Id,
            new Position(9, 9));

        Assert(formation.State == PartyFormationState.Locked &&
               positions[leader.Id] == new Position(10, 10) &&
               positions[second.Id] == new Position(10, 11) &&
               positions[third.Id] == new Position(10, 12) &&
               positions[fourth.Id] == new Position(10, 13) &&
               PartyFormationController.IsSingleFilePassage(blockDestinations, shifted, maze) &&
               shifted[leader.Id] == new Position(10, 9) &&
               shifted[second.Id] == new Position(10, 10) &&
               shifted[fourth.Id] == new Position(11, 10) &&
               shifted[third.Id] == new Position(11, 11) &&
               turned[leader.Id] == new Position(9, 9) &&
               turned[second.Id] == new Position(10, 9) &&
               turned[fourth.Id] == new Position(10, 10) &&
               turned[third.Id] == new Position(11, 10) &&
               ConsoleRenderer.CharacterSheetRenderer.FormationStatusText(formation).Contains("zárt · libasor", StringComparison.Ordinal),
            "A libasor nem maradt zárt, nem fűződött ki a szobából vagy nem követte a folyosó kanyarját.");
    }

    static void FormationEscortPositionsFollowRearEdge()
    {
        var leader = CreateCharacter("Kísérővezér");
        var second = CreateCharacter("Kísérőtárs");
        var block = new PartyFormationSnapshot(leader.Id, second.Id, null, null,
            Direction.Up, PartyFormationState.Locked);
        var blockPositions = PartyFormationRules.Positions(block, leader.Id, new Position(10, 10));
        var blockEscorts = PartyFormationController.EscortPositions(blockPositions, block.Facing);
        var singleFile = block with { Layout = PartyFormationLayout.SingleFile };
        var filePositions = PartyFormationRules.Positions(singleFile, leader.Id, new Position(10, 10));
        var fileEscorts = PartyFormationController.EscortPositions(filePositions, singleFile.Facing);

        Assert(blockEscorts.Take(2).ToHashSet().SetEquals([new Position(10, 11), new Position(11, 11)]) &&
               fileEscorts.First() == new Position(10, 12),
            "A követő elsődleges kísérőhelye nem az alakzat hátsó éle mögé került.");
    }

    static void TemporaryFollowerCanYieldToFormation()
    {
        var maze = new Maze(7, 7);
        var destination = new Position(3, 3);
        maze.Carve(destination);
        var followerCharacter = CreateCharacter("Kitérő követő");
        var followerNpc = new WorldNpc(destination, "NPC-YIELD", followerCharacter,
            NpcDisposition.Friendly, false, false, string.Empty, WorldNpcState.Following);
        var follower = new PartyMemberAvatar(destination, followerCharacter, followerNpc);
        maze.AddPartyMember(follower);
        var positions = new Dictionary<CharacterId, Position> { [CharacterId.New()] = destination };

        Assert(!PartyFormationController.CanFormationOccupy(positions, maze, _ => null) &&
               PartyFormationController.CanFormationOccupy(positions, maze, _ => null,
                   avatar => avatar.IsTemporaryFollower),
            "A követő nem különbözik meg az alakzat elől kitérni képtelen akadálytól.");
    }

    static void LockedFormationSharesDoorInteractionOrigins()
    {
        var leader = CreateCharacter("Ajtóvezér");
        var rear = CreateCharacter("Ajtótárs");
        var leaderPosition = new Position(5, 5);
        var rearPosition = new Position(5, 6);
        var positions = new Dictionary<CharacterId, Position>
        {
            [leader.Id] = leaderPosition,
            [rear.Id] = rearPosition
        };
        var locked = new PartyFormationSnapshot(leader.Id, null, rear.Id, null,
            Direction.Up, PartyFormationState.Locked);
        var disbanded = locked with { State = PartyFormationState.Disbanded };

        Assert(PartyFormationRules.InteractionOrigins(locked, leader.Id, leaderPosition, positions)
                   .ToHashSet().SetEquals([leaderPosition, rearPosition]) &&
               PartyFormationRules.InteractionOrigins(disbanded, leader.Id, leaderPosition, positions)
                   .SequenceEqual([leaderPosition]),
            "Az ajtó-interakció hatósugara nem csak zárt alakzatban terjed ki a többi slot pozíciójára.");
    }

    static void FormationDoorKeyOwnerTakesPriority()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var leader = CreateCharacter("Tolvajvezér", characterClassId: CharacterClassIds.Tolvaj);
        var secondThief = CreateCharacter("Másik tolvaj", characterClassId: CharacterClassIds.Tolvaj);
        var keyOwner = CreateCharacter("Kulcstartó");
        var otherOwner = CreateCharacter("Másik kulcs");
        Assert(secondThief.AddToBackpack(data.GetItem(MiscItemIds.Key)) &&
               keyOwner.AddToBackpack(data.GetItem(MiscItemIds.Key)) &&
               otherOwner.AddToBackpack(data.GetItem(MiscItemIds.Key)),
            "A tesztparti nem kapta meg a három kulcsot.");
        LiveCharacter[] owners = [leader, secondThief, keyOwner, otherOwner];

        var selectedOwner = DoorInteractionRules.SelectKeyOwner(leader, owners, useKeyChoice: true, keyOwner.Id);
        Assert(selectedOwner == keyOwner && selectedOwner.RemoveFromBackpack(MiscItemIds.Key) &&
               !DoorInteractionRules.HasKey(keyOwner) && DoorInteractionRules.HasKey(secondThief) &&
               DoorInteractionRules.HasKey(otherOwner),
            "Az alakzatos zárnyitás nem pontosan a kiválasztott partitag kulcsát fogyasztotta el.");
        Assert(DoorInteractionRules.SelectKeyOwner(leader, owners, useKeyChoice: false, otherOwner.Id) is null,
            "A visszautasított kulcshasználat mégis kiválasztott egy kulcstulajdonost.");
    }

    static void FormationAssemblySwapsFriendlyAvatars()
    {
        var maze = new Maze(7, 7);
        var firstPosition = new Position(2, 3);
        var secondPosition = new Position(3, 3);
        maze.Carve(firstPosition);
        maze.Carve(secondPosition);
        var member = new PartyMemberAvatar(firstPosition, CreateCharacter("Alakzattag"));
        var followerCharacter = CreateCharacter("Koveto");
        var followerNpc = new WorldNpc(secondPosition, "NPC-SWAP", followerCharacter,
            NpcDisposition.Friendly, false, false, string.Empty, WorldNpcState.Following);
        var follower = new PartyMemberAvatar(secondPosition, followerCharacter, followerNpc);
        maze.AddPartyMember(member);
        maze.AddPartyMember(follower);

        Assert(maze.TrySwapPartyMembers(member, follower, maze.Entrance) &&
               member.Position == secondPosition && follower.Position == firstPosition &&
               followerNpc.Position == firstPosition && maze.GetPartyMemberAt(firstPosition) == follower &&
               maze.GetPartyMemberAt(secondPosition) == member,
            "A baratsagos helycsere atfedest hagyott vagy nem mozgatta a koveto world-NPC allapotat.");
    }

    static void LockedFormationRejectsRemoteMovement()
    {
        var (session, _, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var error), error);
        session.SetFormationMovementLocked(true);
        var rejectedEvents = CollectEvents(session);
        session.Submit(new MoveCharacterCommand(remote, 1, companion.Id, Direction.Right));
        Assert(!session.TryReadCommand(out _) && rejectedEvents.OfType<GameCommandRejectedEvent>().Any(entry =>
                   entry.Reason.Contains("alakzat", StringComparison.OrdinalIgnoreCase)),
            "A zart alakzatbol erkezo vendegmozgas atjutott a host validaciojan.");
        session.SetFormationMovementLocked(false);
        var move = new MoveCharacterCommand(remote, 2, companion.Id, Direction.Right);
        session.Submit(move);
        Assert(session.TryReadCommand(out var accepted) && accepted == move,
            "Feloszlatott alakzat utan sem kapta vissza a vendeg a mozgast.");
    }
}
