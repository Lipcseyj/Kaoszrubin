internal static partial class Program
{
    static void ForestCanopyRevealOnlyExpandsExploredMap()
    {
        var maze = new Maze(17, 11);
        var tree = new MazeTerrainStyle("test-tree", new Rune('T'), ConsoleColor.DarkGreen,
            ConsoleColor.Black, false, true);
        var bush = new MazeTerrainStyle("test-bush", new Rune('B'), ConsoleColor.Green,
            ConsoleColor.Black, false, false);
        maze.ConfigureConnectedTerrainReveal([tree, bush]);
        for (var y = 1; y < maze.Height - 1; y++)
        for (var x = 5; x < maze.Width - 1; x++)
            maze.SetTerrain(new Position(x, y), x == 5 ? bush : tree);
        for (var y = 1; y < maze.Height - 1; y++)
        for (var x = 1; x < 5; x++)
            maze.Carve(new Position(x, y));

        var fog = new FogOfWar(maze.Width, maze.Height, 4);
        var changes = fog.UpdatePartyVisibility(maze, [(new Position(3, 5), 4)], false);
        var canopyInterior = new Position(9, 5);

        Assert(fog.IsRevealed(canopyInterior),
            "A meglátott erdőszegély mögött nem rajzolódott ki a lombkorona.");
        Assert(!fog.IsCurrentlyVisible(canopyInterior, includeDeveloperReveal: false),
            "A közvetett lombkorona-felfedés bekerült az aktuális látótérbe.");
        Assert(changes.Contains(canopyInterior),
            "A közvetetten felfedett erdőcellát nem jelölte újrarajzolandónak a rendszer.");

        var ordinaryMaze = new Maze(17, 11, new Rune('T'));
        for (var y = 1; y < ordinaryMaze.Height - 1; y++)
        for (var x = 1; x < 5; x++)
            ordinaryMaze.Carve(new Position(x, y));
        var ordinaryFog = new FogOfWar(ordinaryMaze.Width, ordinaryMaze.Height, 4);
        ordinaryFog.UpdatePartyVisibility(ordinaryMaze, [(new Position(3, 5), 4)], false);
        Assert(!ordinaryFog.IsRevealed(canopyInterior),
            "A külön erdőbeállítás nélküli pálya fala mögé is továbbterjedt a felfedés.");
    }

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
        Assert(PerkProgressionRules.TriggerLevel(race, 1) == 3 &&
               PerkProgressionRules.TriggerLevel(race, 2) == 13 &&
               PerkProgressionRules.TriggerLevel(race, 3) == 25 &&
               PerkProgressionRules.TriggerLevel(race with { Traits = RaceTraits.None }, 1) == 5 &&
               PerkProgressionRules.TriggerLevel(race with { Traits = RaceTraits.None }, 2) == 15,
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
            10, 0, 12).Single(line => line.Row == 4);
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
        var summon = data.GetMonsterSummonForAbility("MA023");
        Assert(medusa.AbilityIds.Contains("MA011") &&
               gaze.Trigger == MonsterAbilityTrigger.Active && gaze.Cooldown == 3 &&
               gaze.Range == 3 && gaze.StatusId == "STATUS006" &&
               troll.AbilityIds.Contains(MonsterAbilityIds.StrongRegeneration) &&
               dragon.HasTrait(EnemyTraits.Flying) &&
               data.GetEnemy("E004").HasTrait(EnemyTraits.Undead) &&
               !data.GetEnemy("E004").AbilityIds.Contains(MonsterAbilityIds.Undead) &&
               data.GetEnemy("E018").AbilityIds.Contains("MA012") &&
               data.GetEnemy("E022").AbilityIds.Contains("MA013") &&
               data.GetMonsterAbility("MA013").MaximumTargets == 2 &&
               summon is { MinimumCount: 1, MaximumCount: 4, SpawnRadius: 2,
                   MaximumLivingSummons: 4, GrantsRewardsAndLoot: false, Prepared: true } &&
               summon.EnemyIds.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(["E004", "E006"]) &&
               data.GetMonsterAbility("MA023") is { ChargesPerBattle: 1, PreparationTurns: 1 } &&
               data.GetEnemy("E074").AbilityIds.Contains("MA023") &&
               data.GetEnemy("E022").AbilityIds.Contains("MA023"),
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
        foreach (var id in new[] { "S027", "S028", "S029", "S030", "P026", "P027", "P028", "P029", "P030" })
            Assert(catalog.GetSpellEffects(id).Count > 0, $"A(z) {id} varázslat hatásai hiányoznak.");

        var weapon = catalog.GetWeapon("W005");
        var enchanted = CreateCharacter("Fegyverbűvös");
        Assert(enchanted.EffectiveWeaponDamageType(weapon) == weapon.DamageType,
            "Varázshatás nélkül megváltozott a fegyver sebzéstípusa.");
        var fireEffect = catalog.GetSpellEffects("P029").Single();
        var necroticEffect = catalog.GetSpellEffects("S029").Single(effect => effect.Type == SpellEffectType.WeaponDamageType);
        new SpellExecutionService(catalog, new Random(2)).ApplyCharacterEffect(enchanted, enchanted,
            fireEffect, catalog.GetSpell("P029"), ActiveSpellEffectType.WeaponDamageType);
        Assert(enchanted.EffectiveWeaponDamageType(weapon) == DamageType.Fire,
            "A Lángáldás nem változtatta tűzzé a támadó fegyver sebzéstípusát.");
        new SpellExecutionService(catalog, new Random(3)).ApplyCharacterEffect(enchanted, enchanted,
            necroticEffect, catalog.GetSpell("S029"), ActiveSpellEffectType.WeaponDamageType);
        Assert(enchanted.EffectiveWeaponDamageType(weapon) == DamageType.Necrotic &&
               enchanted.ActiveSpellEffects.Count(effect =>
                   effect.Type == ActiveSpellEffectType.WeaponDamageType) == 1,
            "A Sírpenge nem írta felül egységesen a korábbi fegyverbűvölést.");
        var focusedBonus = catalog.GetSpellEffects("S029").Single(effect =>
            effect.Type == SpellEffectType.DamageBonus);
        var partyBonus = catalog.GetSpellEffects("S030").Single(effect =>
            effect.Type == SpellEffectType.DamageBonus);
        Assert(focusedBonus.Value == 4 && partyBonus.Value == 2 &&
               focusedBonus.Duration == 4 && partyBonus.Duration == 4,
            "A fókuszált Sírpenge és a teljes partira ható fegyverbűvölés sebzésbónusza nem különül el.");
        Assert(catalog.GetSpellEffects("S014") is [{ Type: SpellEffectType.SpeedPenalty, Duration: 6 }] &&
               catalog.GetSpellEffects("S022").Any(effect => effect.Type == SpellEffectType.SkipAlternate &&
                   effect.Parameter == "Next" && effect.Duration == 1),
            "A tartós Lassítás és a következő akciót megakasztó Jégbilincs nem különül el.");        new SpellExecutionService(catalog, new Random(4)).ApplyCharacterEffect(enchanted, enchanted,
            focusedBonus, catalog.GetSpell("S029"), ActiveSpellEffectType.DamageBonus);
        Assert(enchanted.SpellEffectValue(ActiveSpellEffectType.DamageBonus) == 4,
            "A Sírpenge fegyversebzés-bónusza nem került fel a célpontra.");
        var shackled = new ConfiguredEnemy(new Position(4, 4), catalog.GetEnemy("E001"));
        shackled.ApplySpellEffect(new ActiveSpellEffect("S022", ActiveSpellEffectType.SkipNext, 0, 1));
        Assert(shackled.AdvanceSpellEffects(new Random(5)).SkipAction,
            "A Jégbilincs nem a célpont következő akcióját szakítja meg.");        for (var round = 0; round < 4; round++) enchanted.AdvanceSpellEffects();
        Assert(enchanted.EffectiveWeaponDamageType(weapon) == weapon.DamageType,
            "A fegyver eredeti sebzéstípusa nem állt vissza a varázslat lejártakor.");

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
                blackDragonDefinition.MagicResistance == 50,
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
        Assert(troll.CurrentHitPoints == 450 && start.Entries.Any(entry => entry.Message.Contains("regenerálódik")),
            "A Troll maximum HP-alapú erős regenerációja nem működik.");
        troll.SetCurrentHitPoints(400);
        troll.SuppressRegeneration(DamageType.Fire);
        var suppressed = battle.BeginEnemyTurn(troll);
        Assert(troll.CurrentHitPoints == 400 && suppressed.Entries.All(entry =>
                   !entry.Message.Contains("regenerálódik", StringComparison.OrdinalIgnoreCase)),
            "A tűzsebzés nem állította le egy saját körre a regenerációt.");
        battle.BeginEnemyTurn(troll);
        Assert(troll.CurrentHitPoints == 450, "A regeneráció nem indult újra a tiltott saját kör után.");
        troll.SetCurrentHitPoints(troll.MaximumHitPoints);
        Assert(battle.BeginEnemyTurn(troll).Entries.All(entry =>
                   !entry.Message.Contains("regenerálódik", StringComparison.OrdinalIgnoreCase)),
            "A teljes HP-jú szörny felesleges regenerációs naplóbejegyzést kapott.");

        var hydra = new ConfiguredEnemy(new Position(1, 1), data.GetEnemy("E043"));
        hydra.SetCurrentHitPoints(1000);
        battle.BeginEnemyTurn(hydra);
        Assert(hydra.CurrentHitPoints == 1030,
            "A sima regeneráció nem a maximum HP 5%-os, 30 HP-ban korlátozott értékét használja.");

        var vampire = new ConfiguredEnemy(new Position(1, 1), data.GetEnemy("E019"));
        vampire.SetCurrentHitPoints(100);
        var victim = CreateCharacter("Vércél", vitality: 1000);
        var victimRuntime = battle.PrepareCharacter(victim).Runtime;
        EnemyAttackResolution? drain = null;
        for (var attempt = 0; attempt < 20 && drain is not { Hit: true }; attempt++)
            drain = battle.ResolveEnemyActionDetailed(vampire, victim, victimRuntime, data.GetWeapon("WN019"));
        Assert(drain is { Hit: true, DamageDealt: > 0 } &&
               vampire.CurrentHitPoints == 100 + drain.DamageDealt / 2 &&
               drain.Entry.Message.Contains("ÉLETSZÍVÁS", StringComparison.Ordinal),
            "Az életszívás nem a ténylegesen elvesztett HP 50%-át gyógyította vissza.");

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

    static void PoisonAndBleedingAdvanceDuringExploration()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Sebesült", vitality: 100);
        character.AddStatus(data.GetStatus(CharacterStatusIds.Poisoned));
        character.AddStatus(data.GetStatus(CharacterStatusIds.Bleeding));
        character.AddStatus(data.GetStatus(CharacterStatusIds.Diseased));
        var random = new Random(7281);
        var damage = 0;
        for (var tick = 0; tick < 4; tick++)
            damage += character.ApplyExplorationStatusEffects(random).Sum(result => result.Damage);
        Assert(damage > 0 && !character.HasStatus(CharacterStatusIds.Bleeding) &&
               character.HasStatus(CharacterStatusIds.Poisoned) &&
               character.GetStatusDuration(CharacterStatusIds.Poisoned) == 2 &&
               character.HasStatus(CharacterStatusIds.Diseased),
            "A felfedezési állapottick nem sebezte vagy nem megfelelően léptette a mérgezést és vérzést.");
        character.ApplyExplorationStatusEffects(random);
        var final = character.ApplyExplorationStatusEffects(random);
        Assert(!character.HasStatus(CharacterStatusIds.Poisoned) &&
               final.Any(result => result.Name == "Mérgezés" && result.Expired) &&
               character.HasStatus(CharacterStatusIds.Diseased),
            "A mérgezés nem járt le hat felfedezési aktiválás után, vagy a tartós betegség is léptetődött.");
    }

    static void ExplorationClockAdvancesSpellEffectsInsteadOfSteps()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Időmágus", vitality: 100);
        character.ApplySpellEffect(new ActiveSpellEffect("S-IDO", ActiveSpellEffectType.DefenseBonus, 2, 2,
            Beneficial: true));
        for (var step = 0; step < 20; step++) character.RegisterExplorationStep();
        Assert(character.ActiveSpellEffects.Single().RemainingRounds == 2,
            "A varázshatás továbbra is a megtett lépésekből fogy.");
        character.AdvanceExplorationSpellEffects(new Random(1));
        Assert(character.ActiveSpellEffects.Single().RemainingRounds == 1,
            "A felfedezési időzítő nem fogyasztott egy varázshatáskört.");
        character.AdvanceExplorationSpellEffects(new Random(1));
        Assert(character.ActiveSpellEffects.Count == 0,
            "A varázshatás nem járt le a második időzített felfedezési körben.");

        var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var next = now + TimeSpan.FromSeconds(30);
        Assert(Game.ExplorationClockFrame(next, now, true) == "⌛" &&
               Game.ExplorationClockFrame(next, now + TimeSpan.FromSeconds(3), true) == "⏳" &&
               Game.ExplorationClockFrame(next, now + TimeSpan.FromSeconds(6), true) == "⌛" &&
               Game.ExplorationClockFrame(next, now, false).Contains("⏸", StringComparison.Ordinal),
            "A homokóra nem három másodpercenként fordul vagy nem jelzi a szünetet.");
        var header = CharacterSheetPanel.BuildWorldHeaderLine(12, 7, 12, "⏳", 34);
        Assert(header.Text.Contains("🔑 7/12", StringComparison.Ordinal) &&
               header.Text.EndsWith("⏳", StringComparison.Ordinal),
            "A homokóra nem az aranykulcsok után jelenik meg a karakterlapon.");
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

        var corridor = new Maze(11, 7);
        for (var x = 1; x <= 9; x++) corridor.Carve(new Position(x, 3));
        var corridorLeader = new Player(new Position(9, 3), leader);
        var blocker = new PartyMemberAvatar(new Position(7, 3), rearLeft);
        var lagging = new PartyMemberAvatar(new Position(2, 3), frontLeft);
        corridor.AddPartyMember(blocker);
        corridor.AddPartyMember(lagging);
        var corridorTrail = Enumerable.Range(1, 9).Select(x => new Position(x, 3)).ToArray();
        var catchingUpStep = PartyMovementController.FollowLeaderTrail(lagging, 2, corridor,
            corridorLeader, corridorTrail);
        Assert(catchingUpStep == new Position(3, 3),
            "A lemaradt társ nem indult el az egymezős folyosón az utat később elzáró csapattárs felé.");

        lagging.MoveTo(new Position(6, 3));
        Assert(PartyMovementController.FollowLeaderTrail(lagging, 2, corridor, corridorLeader,
                   corridorTrail) is null,
            "A követő megpróbált a közvetlenül előtte álló csapattárs foglalt mezőjére lépni.");
    }

    static void PartyMembersLeaveRoomAroundIdleLeader()
    {
        var maze = new Maze(9, 9);
        for (var y = 2; y <= 6; y++)
        for (var x = 2; x <= 6; x++) maze.Carve(new Position(x, y));
        var leader = new Player(new Position(4, 4), CreateCharacter("Vezér"));
        var companion = new PartyMemberAvatar(new Position(4, 5), CreateCharacter("Társ"));
        maze.AddPartyMember(companion);
        var trail = new[] { new Position(4, 6), companion.Position, leader.Position };

        var retreat = PartyMovementController.ChoosePartyMemberStep(companion, maze, leader,
            Direction.Up, trail, 0, leaderIdle: true);
        Assert(retreat is { } next && PartyMovementController.Manhattan(next, leader.Position) == 2,
            "A megállt vezér mellett álló társ nem húzódott arrébb.");
        companion.MoveTo(retreat!.Value);
        Assert(PartyMovementController.ChoosePartyMemberStep(companion, maze, leader,
                   Direction.Up, trail, 0, leaderIdle: true) is null,
            "A társ nyugalomban visszalépett a vezér mellé vagy tovább bolyongott.");

        var narrow = new Maze(9, 9);
        narrow.Carve(leader.Position);
        narrow.Carve(new Position(3, 4));
        narrow.Carve(new Position(3, 5));
        var approacher = new PartyMemberAvatar(new Position(3, 5), CreateCharacter("Másik társ"));
        narrow.AddPartyMember(approacher);
        Assert(!PartyMovementController.PreservesLeaderExit(approacher, new Position(3, 4),
                   narrow, leader),
            "A társ elfoglalhatta a vezér utolsó szabad kijáratát.");
    }

    static void UndeadSpellImmunityAndTypedMagicResistanceWork()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory,
            CsvGameDataLoader.GameDataFileName));
        var service = new SpellExecutionService(catalog, new Random(42));
        var undead = new ConfiguredEnemy(new Position(3, 3), catalog.GetEnemy("E004"));
        var living = new ConfiguredEnemy(new Position(4, 3), catalog.GetEnemy("E001"));
        var maze = new Maze(7, 7);
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++) maze.Carve(new Position(x, y));
        maze.AddEnemy(undead);
        maze.AddEnemy(living);
        var casterPosition = new Position(3, 2);
        var fog = new FogOfWar(maze.Width, maze.Height, 5);
        fog.RevealFrom(maze, casterPosition);
        var blind = catalog.GetSpell("S027");
        var fright = catalog.GetSpell("S028");
        Assert(blind.ExcludesUndead && fright.ExcludesUndead &&
               !service.ValidateSpellTarget(casterPosition, blind, undead.Position, undead, maze, fog,
                   casterPosition, null).IsValid &&
               service.ValidateSpellTarget(casterPosition, blind, living.Position, living, maze, fog,
                   casterPosition, null).IsValid &&
               !service.ResolveEnemySpellTargets(fright, undead.Position, undead, casterPosition, maze)
                   .Contains(undead) &&
               service.ResolveEnemySpellTargets(fright, undead.Position, undead, casterPosition, maze)
                   .Contains(living),
            "A Vakítás vagy Rémkép élőholtat célzott, vagy az élő célpontot is kizárta.");

        var testDefinition = catalog.GetEnemy("E001") with
        {
            Resistances = new DamageResistance(Fire: 5, Acid: -3, Necrotic: 8),
            MagicResistance = 0
        };
        var resistant = new ConfiguredEnemy(new Position(3, 3), testDefinition);
        var caster = CreateCharacter("Varázsló");
        var spell = catalog.GetSpell("S031");
        var baseEffect = new SpellEffectDefinition("TEST-TYPED", spell.Id, 1, SpellEffectType.Damage,
            null, 0, 0, 20, 0, 100, SpellResolution.Auto, null, "");
        int Damage(DamageType type) => service.ResolveSpellDamage(caster,
            baseEffect with { DamageType = type }, spell, resistant,
            new Dictionary<(Enemy, SpellResolution), SpellResolutionResult>(), []);
        Assert(catalog.GetSpellEffects("S031").Single().DamageType == DamageType.Acid &&
               catalog.GetSpellEffects("S032").Single().DamageType == DamageType.Necrotic &&
               catalog.GetSpellEffects("S007").Single(effect => effect.Type == SpellEffectType.Damage)
                   .DamageType == DamageType.Fire &&
               Damage(DamageType.Fire) == 10 && Damage(DamageType.Acid) == 26 &&
               Damage(DamageType.Necrotic) == 4,
            "A varázssebzés típusa vagy az előjeles elemi ellenállás nem érvényesül.");

        var burning = new ActiveSpellEffect("TEST-FIRE", ActiveSpellEffectType.Burning, 0, 2,
            new DiceExpression(1, 2), 10, DamageType: DamageType.Fire);
        resistant.ApplySpellEffect(burning);
        var neutral = new ConfiguredEnemy(new Position(4, 3), testDefinition with
            { Resistances = new DamageResistance() });
        neutral.ApplySpellEffect(burning);
        Assert(resistant.AdvanceSpellEffects(new Random(7)).Damage ==
               DamageResistance.ApplySpellPercent(neutral.AdvanceSpellEffects(new Random(7)).Damage, 5),
            "A tartós tűzsebzésre nem hat az ellenállás.");

        var acidic = burning with { SourceSpellId = "TEST-ACID", DamageType = DamageType.Acid };
        var acidResistant = new ConfiguredEnemy(new Position(3, 3), testDefinition);
        var acidNeutral = new ConfiguredEnemy(new Position(4, 3), neutral.Definition);
        acidResistant.ApplySpellEffect(acidic);
        acidNeutral.ApplySpellEffect(acidic);
        Assert(acidResistant.AdvanceSpellEffects(new Random(8)).Damage ==
               DamageResistance.ApplySpellPercent(acidNeutral.AdvanceSpellEffects(new Random(8)).Damage, -3),
            "A negatív savvédelem nem növelte a tartós varázssebzést.");
        var storm = new ActiveStormZone(Guid.NewGuid(), "TEST-FIRE", resistant.Position,
            [resistant.Position], 2, new DiceExpression(1, 2), 10, 100, SpellResolution.Auto,
            DamageType: DamageType.Fire);
        Assert(storm.RollDamage(new Random(9), 0, 0, testDefinition.Resistances!.Fire) ==
               DamageResistance.ApplySpellPercent(storm.RollDamage(new Random(9), 0, 0), 5) &&
               storm.RollDamage(new Random(9), 0, 0, 10) == 0 &&
               storm.RollDamage(new Random(9), 0, 0, -10) ==
               2 * storm.RollDamage(new Random(9), 0, 0),
            "A tűzörvény sebzésére nem hat az elemi ellenállás.");
        Assert(DamageResistance.ApplySpellPercent(20, 10) == 0 &&
               DamageResistance.ApplySpellPercent(20, -10) == 40 &&
               DamageResistance.ApplySpellPercent(20, 5) == 10,
            "A típusvédelmek tízszázalékos skálája hibás.");
    }

    static void PlayerSpellLogShowsDamageModifiersAndRemainingHealth()
    {
        var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory,
            CsvGameDataLoader.GameDataFileName));
        var damagingSpells = catalog.SpellEffects
            .Where(effect => effect.Type is SpellEffectType.Damage or SpellEffectType.ChainDamage)
            .Select(effect => catalog.GetSpell(effect.SpellId))
            .Where(spell => !spell.EnemyOnly)
            .DistinctBy(spell => spell.Id);
        Assert(damagingSpells.All(spell => !string.IsNullOrWhiteSpace(spell.LogEmoji) &&
                                          spell.LogEmoji != "✨"),
            "A játékos sebző varázslatainak hiányzik a CSV-ben megadott naplóikonja.");

        var maze = new Maze(9, 9);
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++) maze.Carve(new Position(x, y));
        var baseDefinition = catalog.GetEnemy("E001") with { HitPoints = 200 };
        var resistant = new ConfiguredEnemy(new Position(4, 4), baseDefinition with
        { Name = "Ellenálló", Resistances = new DamageResistance(Fire: 5), MagicResistance = 25 });
        var vulnerable = new ConfiguredEnemy(new Position(5, 4), baseDefinition with
        { Name = "Sérülékeny", Resistances = new DamageResistance(Fire: -5) });
        var immune = new ConfiguredEnemy(new Position(4, 5), baseDefinition with
        { Name = "Immunis", Resistances = new DamageResistance(Fire: 10) });
        var fragile = new ConfiguredEnemy(new Position(5, 5), baseDefinition with
        { Name = "Sebzett", HitPoints = 1, Resistances = new DamageResistance() });
        maze.AddEnemy(resistant);
        maze.AddEnemy(vulnerable);
        maze.AddEnemy(immune);
        maze.AddEnemy(fragile);
        var caster = CreateCharacter("Mágus");
        var spell = catalog.GetSpell("S007");
        var timeStop = false;
        var result = new SpellExecutionService(catalog, new Random(91)).ExecuteSpell(caster,
            new Position(4, 2), spell, resistant.Position, true, resistant, false, ref timeStop,
            [(caster, new Position(4, 2))], maze,
            (_, enemy, amount, _) => enemy.ReceiveSpellDamage(amount),
            (_, _) => false, (_, _) => "", (_, _) => "");
        var vulnerableDamage = 200 - vulnerable.CurrentHitPoints;
        var targetLines = result.Summary.Split("; ", StringSplitOptions.None);
        var resistantLine = targetLines.Single(line => line.StartsWith("Ellenálló:", StringComparison.Ordinal));
        var vulnerableLine = targetLines.Single(line => line.StartsWith("Sérülékeny:", StringComparison.Ordinal));
        var immuneLine = targetLines.Single(line => line.StartsWith("Immunis:", StringComparison.Ordinal));
        var fragileLine = targetLines.Single(line => line.StartsWith("Sebzett:", StringComparison.Ordinal));
        Assert(spell.LogEmoji == "☄️🔥" && result.DamageToCurrentEnemy > 0 &&
               resistant.CurrentHitPoints == 200 && vulnerableDamage > 0 &&
               resistantLine.Contains($"❤️-{result.DamageToCurrentEnemy} " +
                   "(🛡️ tűz ellenállás 50%, 🔮 varázsvédelem 25%, 🎲 ") &&
               resistantLine.EndsWith($"(❤️{200 - result.DamageToCurrentEnemy}/200)") &&
               vulnerableLine.Contains($"❤️-{vulnerableDamage} " +
                   "(⚠️ tűz sérülékenység +50%, 🎲 ") &&
               vulnerableLine.EndsWith($"(❤️{vulnerable.CurrentHitPoints}/200)") &&
               immuneLine.Contains("❤️-0 (🛡️ tűz ellenállás 100%, 🎲 ") &&
               immuneLine.EndsWith("(❤️200/200)") &&
               fragile.CurrentHitPoints == 0 && fragileLine.Contains("Sebzett: ❤️-1") &&
               fragileLine.EndsWith("(❤️0/1)"),
            "A többcélú varázslat naplója nem mutatja célpontonként a tényleges sebzést, védelmet és maradék HP-t.");
    }

    static void InvisibilityPreventsExplorationDetection()
    {
        var invisible = CreateCharacter("Láthatatlan");
        var visible = CreateCharacter("Látható");
        invisible.ApplySpellEffect(new ActiveSpellEffect("S006", ActiveSpellEffectType.Invisibility, 5, 3));
        var candidates = new[]
        {
            (invisible, new Position(2, 1)),
            (visible, new Position(3, 1))
        };
        Assert(!EnemyTargeting.CanDetectDuringExploration(invisible) &&
               EnemyTargeting.ChooseNearestVisible(new Position(1, 1), candidates, _ => true,
                   new Random(1))?.Character == visible &&
               EnemyTargeting.ChooseNearestSensed(new Position(1, 1), candidates, 5,
                   position => position.X - 1, new Random(1))?.Character == visible,
            "A láthatatlan karaktert a térképi látás vagy nyomérzék továbbra is célpontnak tekinti.");

        invisible.BreakInvisibility();
        Assert(EnemyTargeting.CanDetectDuringExploration(invisible) &&
               EnemyTargeting.ChooseNearestVisible(new Position(1, 1), candidates, _ => true,
                   new Random(1))?.Character == invisible,
            "A láthatatlanság megszűnése után az ellenség nem észleli újra a karaktert.");
    }

    static void PartyMovementProfilesHaveDistinctEnemyLeashes()
    {
        var maze = new Maze(13, 9);
        for (var y = 1; y < 8; y++)
        for (var x = 1; x < 12; x++) maze.Carve(new Position(x, y));
        var leader = new Player(new Position(4, 4), CreateCharacter("Vezér"));
        var character = CreateCharacter("Társ");
        var companion = new PartyMemberAvatar(new Position(3, 4), character);
        maze.AddPartyMember(companion);
        var enemy = CreateEnemyAt(new Position(8, 4), "E-PROFILE");
        maze.AddEnemy(enemy);
        var trail = new[] { companion.Position, leader.Position };

        character.SetNpcBehavior(NpcBehavior.Defensive);
        var defensive = PartyMovementController.ChoosePartyMemberStep(companion, maze, leader,
            Direction.Right, trail, 0);
        character.SetNpcBehavior(NpcBehavior.Aggressive);
        var aggressive = PartyMovementController.ChoosePartyMemberStep(companion, maze, leader,
            Direction.Right, trail, 0);

        Assert(defensive is null && aggressive is not null,
            "A támadó és a védő társ ugyanúgy reagált a vezértől távolabb álló ellenségre.");

        enemy.MoveTo(new Position(6, 4));
        character.SetNpcBehavior(NpcBehavior.Bodyguard);
        var bodyguard = PartyMovementController.ChoosePartyMemberStep(companion, maze, leader,
            Direction.Right, trail, 0);
        character.SetNpcBehavior(NpcBehavior.Rearguard);
        var rearguard = PartyMovementController.ChoosePartyMemberStep(companion, maze, leader,
            Direction.Right, trail, 0);
        Assert(bodyguard is not null && rearguard is null,
            "A testőr nem fogta fel a vezér közeli támadóját, vagy a hátvéd előretört rá.");
    }

    static void ForwardProfilesOvertakeLeaderWhenThereIsRoom()
    {
        var maze = new Maze(25, 15);
        for (var y = 1; y < maze.Height - 1; y++)
        for (var x = 1; x < maze.Width - 1; x++) maze.Carve(new Position(x, y));
        var leader = new Player(new Position(8, 7), CreateCharacter("Vezér"));
        var character = CreateCharacter("Társ");
        var initial = new Position(6, 7);
        var member = new PartyMemberAvatar(initial, character);
        maze.AddPartyMember(member);
        var trail = new[] { new Position(7, 7), leader.Position };

        int Advance(NpcBehavior behavior)
        {
            character.SetNpcBehavior(behavior);
            member.MoveTo(initial);
            for (var turn = 0; turn < 16; turn++)
            {
                var next = PartyMovementController.ChoosePartyMemberStep(member, maze, leader,
                    Direction.Up, trail, 0, leaderIdle: true);
                if (next is null) break;
                Assert(maze.TryMovePartyMember(member, next.Value, leader.Position),
                    "A profil nem járható mezőre tervezett.");
            }
            return member.Position.X - leader.Position.X;
        }

        var aggressiveLead = Advance(NpcBehavior.Aggressive);
        var scoutLead = Advance(NpcBehavior.Scout);
        Assert(PartyMovementController.RecentTravelDirection(trail, leader.Position, Direction.Up) == Direction.Right &&
               aggressiveLead > 0 && scoutLead > aggressiveLead,
            "Az előremenő profilok nem jutottak a vezér korábbi lépésiránya elé, vagy a felderítő nem ment messzebb.");

        var threat = CreateEnemyAt(new Position(18, 7), "E-SCOUT-THREAT");
        maze.AddEnemy(threat);
        var cautiousScoutLead = Advance(NpcBehavior.Scout);
        Assert(cautiousScoutLead > aggressiveLead &&
               PartyMovementController.Manhattan(member.Position, threat.Position) >= 4,
            "A felderítő távoli ellenfél láttán visszahúzódott, vagy túl közel merészkedett hozzá.");
    }

    static void LeaderCanSwapWithAdjacentPartyMember()
    {
        var maze = new Maze(7, 7);
        var leaderPosition = new Position(3, 3);
        var memberPosition = new Position(4, 3);
        maze.Carve(leaderPosition);
        maze.Carve(memberPosition);
        var leader = new Player(leaderPosition, CreateCharacter("Vezér"));
        var member = new PartyMemberAvatar(memberPosition, CreateCharacter("Társ"));
        maze.AddPartyMember(member);

        Assert(maze.TrySwapLeaderAndPartyMember(leader, member) &&
               leader.Position == memberPosition && member.Position == leaderPosition &&
               maze.GetPartyMemberAt(leaderPosition) == member &&
               maze.GetPartyMemberAt(memberPosition) is null,
            "A helycsere után a karakterpozíciók vagy a térképi nyilvántartás eltérnek.");
        Assert(maze.TrySwapLeaderAndPartyMember(leader, member) &&
               leader.Position == leaderPosition && member.Position == memberPosition,
            "A visszafelé végzett helycsere meghiúsult.");
    }

    static void PartyMembersRouteAroundDetectedTraps()
    {
        var maze = new Maze(7, 7);
        var start = new Position(2, 3);
        var trapPosition = new Position(3, 3);
        var target = new Position(4, 3);
        foreach (var position in new[] { start, trapPosition, target,
                     new Position(2, 2), new Position(3, 2), new Position(4, 2), new Position(5, 3) })
            maze.Carve(position);
        var leader = new Player(new Position(5, 3), CreateCharacter("Vezér"));
        var member = new PartyMemberAvatar(start, CreateCharacter("Társ"));
        var trap = new MazeTrap(trapPosition,
            new TrapDefinition("TR-ROUTE", "Tesztcsapda", new Rune('⌄'), TrapEffect.Damage,
                1, 7, 7, 3, 7, 0, 25, 75, "Teszt."));
        maze.AddTrap(trap);
        trap.Detect();

        Assert(PartyMovementController.FindNextStep(member, [target], maze, leader) == new Position(2, 2) &&
               PartyMovementController.FollowLeaderTrail(member, 1, maze, leader,
                   [target, leader.Position]) == new Position(2, 2) &&
               !PartyMovementController.CanPartyTraverse(member, trapPosition, maze, leader),
            "A társ a felfedezett csapdán át tervezett, pedig volt járható kerülő.");

        trap.Disarm();
        Assert(PartyMovementController.FindNextStep(member, [target], maze, leader) == trapPosition,
            "A társ a hatástalanított csapdát sem használta járható útvonalként.");
    }

    static void ExploredDoorGapDoesNotLeaveBlackMapHole()
    {
        var maze = new Maze(9, 7);
        for (var x = 2; x <= 6; x++) maze.Carve(new Position(x, 2));
        for (var x = 3; x <= 5; x++) maze.Carve(new Position(x, 4));
        var doorPosition = new Position(4, 2);
        maze.PlaceDoor(doorPosition, DoorState.Smashed);

        var fog = new FogOfWar(maze.Width, maze.Height, 0);
        fog.Restore([new Position(3, 2), new Position(3, 4), new Position(5, 4)], false);
        var changed = fog.RevealFrom(maze, new Position(5, 2));
        Assert(fog.IsRevealed(doorPosition) && changed.Contains(doorPosition) &&
               !fog.IsRevealed(new Position(4, 4)),
            "A két oldalról ismert, bezúzott ajtó helye fekete lyuk maradt a térképen.");

        var partyFog = new FogOfWar(maze.Width, maze.Height, 0);
        partyFog.Restore([new Position(3, 2)], false);
        var partyChanges = partyFog.UpdatePartyVisibility(maze,
            [(new Position(5, 2), 0)], advanceEnemyMemory: false);
        Assert(partyFog.IsRevealed(doorPosition) && partyChanges.Contains(doorPosition),
            "A normál partifelfedezés nem töltötte ki az ajtó melletti helyi rést.");

        var unexploredFog = new FogOfWar(maze.Width, maze.Height, 0);
        unexploredFog.Restore([new Position(3, 2)], false);
        unexploredFog.RevealFrom(maze, new Position(2, 2));
        Assert(!unexploredFog.IsRevealed(doorPosition) && !unexploredFog.IsRevealed(new Position(5, 2)),
            "Az ajtó rése mögötti ismeretlen terület idő előtt felfedődött.");
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

    static void NpcChestOrdersUseWeightedReachableOpener()
    {
        var maze = new Maze(9, 7);
        for (var x = 1; x <= 6; x++) maze.Carve(new Position(x, 2));
        maze.Carve(new Position(1, 3));
        maze.Carve(new Position(3, 3));
        var leaderPosition = new Position(3, 3);
        var chest = new TreasureChest(new Position(4, 2), 25);
        maze.AddTreasureChest(chest);
        var first = new PartyMemberAvatar(new Position(1, 2), CreateCharacter("Ládás harcos"));
        var second = new PartyMemberAvatar(new Position(2, 2), CreateCharacter("Ládás társ"));
        var thief = new PartyMemberAvatar(new Position(1, 3),
            CreateCharacter("Ládás tolvaj", characterClassId: CharacterClassIds.Tolvaj));
        maze.AddPartyMember(first);
        maze.AddPartyMember(second);
        maze.AddPartyMember(thief);

        var candidates = new[] { first, second, thief };
        var random = new Random(123);
        var thiefDraws = Enumerable.Range(0, 10_000)
            .Count(_ => NpcChestOpeningController.ChooseOpener(candidates, random) == thief);
        var path = NpcChestOpeningController.FindPath(maze, first.Position, chest.Position,
            leaderPosition);
        Assert(GameInputBindings.LeaderAction(ConsoleKey.L, false) == LeaderAction.OrderNpcToOpenChest &&
               thiefDraws is > 4100 and < 4500 &&
               path is { Count: 3 } && path[0] == second.Position && path[^1] == chest.Position &&
               maze.TrySwapPartyMembers(first, second, leaderPosition) &&
               maze.TryMovePartyMember(first, path[1], leaderPosition) &&
               maze.TryMovePartyMember(first, path[2], leaderPosition, allowTreasureChest: true) &&
               first.Position == chest.Position,
            "Az NPC ládanyitó súlyozása vagy a társakon átvezető útvonala hibás.");

        var blocked = new Maze(9, 7);
        blocked.Carve(new Position(1, 2));
        blocked.Carve(chest.Position);
        blocked.PlaceDoor(new Position(2, 2), DoorState.Closed);
        blocked.AddTreasureChest(new TreasureChest(chest.Position, 1));
        Assert(NpcChestOpeningController.FindPath(blocked, new Position(1, 2), chest.Position,
                   leaderPosition) is null,
            "Az NPC ládanyitó zárt ajtón keresztül tervezett útvonalat.");
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
