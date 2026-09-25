internal static partial class Program
{
    static void NpcThiefTrapCommandAcceptsTemporaryFollowers()
    {
        var leaderPosition = new Position(5, 5);
        var thief = CreateCharacter("Tolvaj", characterClassId: CharacterClassIds.Tolvaj);
        var permanent = new PartyMemberAvatar(new Position(7, 5), thief);
        var followerNpc = new WorldNpc(new Position(6, 5), "NPC-THIEF", thief,
            NpcDisposition.Friendly, recruitable: false, isQuestNpc: false, "Próba");
        var follower = new PartyMemberAvatar(followerNpc.Position, thief, followerNpc);

        Assert(!Game.IsNpcThiefTrapDisarmCandidate(permanent, new HashSet<CharacterId>(), leaderPosition) &&
               Game.IsNpcThiefTrapDisarmCandidate(permanent, new HashSet<CharacterId> { thief.Id }, leaderPosition) &&
               Game.IsNpcThiefTrapDisarmCandidate(follower, new HashSet<CharacterId>(), leaderPosition),
            "A parancs nem különbözteti meg helyesen a kézzel irányított karaktert, az NPC-társat és a követőt.");

        follower.MoveTo(new Position(10, 5));
        Assert(!Game.IsNpcThiefTrapDisarmCandidate(follower, new HashSet<CharacterId>(), leaderPosition),
            "A parancs a négymezős hatótávon kívüli követőt is kiválasztotta.");
    }

    static void RestLimitIsTrackedPerScreen()
    {
        var rests = new DungeonRestState();
        Assert(rests.TryMarkRested("AREA_1") && rests.HasRested("AREA_1") &&
               !rests.HasRested("AREA_2") && !rests.TryMarkRested("AREA_1"),
            "Az egyik képernyő pihenése lezárta a másikat, vagy ugyanott kétszer engedett pihenni.");

        var restored = new DungeonRestState();
        restored.Restore(["AREA_2"], legacyHasRestedThisLevel: false, ["AREA_1", "AREA_2", "AREA_3"]);
        Assert(!restored.HasRested("AREA_1") && restored.HasRested("AREA_2") &&
               restored.RestedAreaIds.Count == 1,
            "A képernyőnkénti pihenési állapot nem állt vissza pontosan.");

        var legacy = new DungeonRestState();
        legacy.Restore([], legacyHasRestedThisLevel: true, ["AREA_1", "AREA_2", "AREA_3"]);
        Assert(legacy.RestedAreaIds.Count == 3,
            "A régi, szintenkénti pihenési jelző nem zárta le visszafelé kompatibilisen az összes képernyőt.");
    }

    static void EncountersCanTargetASpecificScreen()
    {
        var configured = Encounters.Same("E-TEST", Amount.Few, Amount.One, screen: 2);
        Assert(configured.ScreenNumber == 2,
            "Az encounter segéd nem őrizte meg a megadott képernyőszámot.");

        var pinned = new ResolvedEnemyEncounter(new IntRange(3, 3), [], null,
            ScreenNumber: 2);
        var distributed = Game.DistributeEncounters([pinned], 3, new Random(1401));
        Assert(distributed[0].Count == 0 && distributed[1].Count == 3 && distributed[2].Count == 0 &&
               distributed[1].All(encounter => encounter.GroupCount == new IntRange(1, 1)),
            "A képernyőhöz kötött encounter csoportjai nem kizárólag a kijelölt képernyőre kerültek.");

        try
        {
            Game.DistributeEncounters([pinned with { ScreenNumber = 4 }], 3, new Random(1402));
            throw new InvalidOperationException("A nem létező encounter-képernyőt elfogadta a rendszer.");
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("3 képernyője", StringComparison.Ordinal))
        {
        }
    }

    static void WideMazeUsesThreeCellCorridors()
    {
        var level = MazeLevelConfigurations.Get(6);
        Assert(level.Layout is WideMazeLayoutConfiguration { AreaCount: { Minimum: 2, Maximum: 2 } },
            "A nagy csarnokok szintje nem a külön széles, kétterületes pályatípust használja.");
        Assert(level.CorridorEncounters.Any(encounter => encounter.Members.Count > 1),
            "A széles pálya folyosóin nincs többféle szörnyből álló csapat konfigurálva.");
        var settings = new MazeGenerationSettings
        {
            RoomCount = 0,
            TreasureChestCount = 0,
            WideCorridorNarrowingChance = 0
        };
        var maze = new WideMazeGenerator(settings, [], [], new Random(117)).Create(43, 31);
        const int step = 6;
        const int width = 3;
        for (var nodeY = 1; nodeY + width <= maze.Height; nodeY += step)
        for (var nodeX = 1; nodeX + width <= maze.Width; nodeX += step)
        {
            for (var y = nodeY; y < nodeY + width; y++)
            for (var x = nodeX; x < nodeX + width; x++)
                Assert(maze.IsWalkable(new Position(x, y)), "A széles generátor egyik csomópontja nem 3×3-as.");

            if (nodeX + step + width <= maze.Width && maze.IsWalkable(new Position(nodeX + width, nodeY)))
                for (var offset = 0; offset < step - width; offset++)
                for (var lane = 0; lane < width; lane++)
                    Assert(maze.IsWalkable(new Position(nodeX + width + offset, nodeY + lane)),
                        "Egy vízszintes széles folyosó nem három mező széles.");
            if (nodeY + step + width <= maze.Height && maze.IsWalkable(new Position(nodeX, nodeY + width)))
                for (var offset = 0; offset < step - width; offset++)
                for (var lane = 0; lane < width; lane++)
                    Assert(maze.IsWalkable(new Position(nodeX + lane, nodeY + width + offset)),
                        "Egy függőleges széles folyosó nem három mező széles.");
        }

        var otherMaze = new WideMazeGenerator(settings, [], [], new Random(118)).Create(43, 31);
        var oldExit = maze.Exit;
        var departure = MazeEdgePassageCarver.Carve(maze, new Random(119));
        var arrival = MazeEdgePassageCarver.Carve(otherMaze, new Random(120),
            departure.OppositeEdge, departure.RelativeOffset);
        maze.PlaceExit(departure.Position);
        Assert(arrival.Edge == departure.OppositeEdge &&
               Math.Abs(arrival.RelativeOffset - departure.RelativeOffset) <= 1d / 30 &&
               IsOnEdge(maze, departure.Position) && IsOnEdge(otherMaze, arrival.Position),
            "Az átjáró két vége nem egymással szemközti, közel azonos falszakaszra került.");
        Assert(maze.Tiles[oldExit.X, oldExit.Y] == Maze.Floor &&
               maze.CheckFullAccessibility().IsFullyAccessible &&
               otherMaze.CheckFullAccessibility().IsFullyAccessible,
            "A szélső átjáró járata nem bejárható, vagy megmaradt a régi jobb alsó kijáratjel.");

        static bool IsOnEdge(Maze candidate, Position position) =>
            position.X == 0 || position.Y == 0 ||
            position.X == candidate.Width - 1 || position.Y == candidate.Height - 1;
    }

    static void WideLevelsHaveBalancedDiverseHordes()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var orcCamp = MazeLevelConfigurations.Get(8);
        Assert(orcCamp.CorridorEncounters.All(encounter =>
                   encounter.Behavior == EnemyEncounterBehavior.Horde) &&
               orcCamp.CorridorEncounters.Count(encounter =>
                   encounter.Members.Sum(member => member.Count.Minimum) >= 17) >= 2 &&
               orcCamp.CorridorEncounters.Any(encounter =>
                   encounter.Members.Sum(member => member.Count.Maximum) >= 34),
            "Az ork haditábor vándorló hordái nem lettek érdemben nagyobbak.");
        var expectedWeakerEnemy = new Dictionary<int, string>
        {
            [8] = MonsterIds.Goblin,
            [10] = MonsterIds.Ork,
            [11] = MonsterIds.Orgyilkos,
            [12] = MonsterIds.PestishordozóPatkány,
            [16] = MonsterIds.Csontváz,
            [18] = MonsterIds.Pokolfajzat,
            [19] = MonsterIds.Pokolfajzat
        };
        foreach (var (levelNumber, weakerEnemyId) in expectedWeakerEnemy)
        {
            var level = MazeLevelConfigurations.Get(levelNumber);
            Assert(level.Layout is WideMazeLayoutConfiguration { AreaCount.Minimum: >= 2 },
                $"A(z) {levelNumber}. szint nem többterületes Wide pálya.");
            var wide = (WideMazeLayoutConfiguration)level.Layout!;
            var areaCount = wide.AreaCount.Maximum;
            var minimumGroups = level.CorridorEncounters.Sum(encounter => encounter.GroupCount.Minimum);
            var minimumEnemies = level.CorridorEncounters.Sum(encounter =>
                encounter.GroupCount.Minimum * encounter.Members.Sum(member => member.Count.Minimum));
            Assert(level.CorridorEncounters.All(encounter =>
                       encounter.Behavior == EnemyEncounterBehavior.Horde &&
                       encounter.Members.Sum(member => member.Count.Minimum) >= 2) &&
                   minimumGroups >= areaCount * 2 && minimumEnemies >= areaCount * 12,
                $"A(z) {levelNumber}. szint folyosói találkozásai nem adnak képernyőnként elég hordát.");
            Assert(level.CorridorEncounters.SelectMany(encounter => encounter.Members)
                       .Any(member => member.EnemyId == weakerEnemyId) &&
                   level.RoomCount.Minimum >= areaCount * 6,
                $"A(z) {levelNumber}. szintről hiányzik a gyengébb tömegellenfél vagy túl ritka a többképernyős tartalom.");

            ResolvedEnemyEncounter ResolveForOneArea(EnemyEncounterConfiguration encounter) => new(
                new IntRange(1, Math.Max(1, (encounter.GroupCount.Maximum + areaCount - 1) / areaCount)),
                encounter.Members.Select(member => new ResolvedEnemyGroupMember(
                    data.GetEnemy(member.EnemyId), member.Count, member.Role)).ToArray(),
                encounter.MovementProfile,
                encounter.Behavior);
            var settings = new MazeGenerationSettings
            {
                RoomCount = (level.RoomCount.Maximum + areaCount - 1) / areaCount,
                MinimumRoomSize = level.RoomSize.Minimum,
                MaximumRoomSize = level.RoomSize.Maximum,
                TreasureChestCount = (level.TreasureChestCount.Maximum + areaCount - 1) / areaCount,
                TreasureGoldRange = level.TreasureGold,
                WideCorridorNarrowingChance = wide.NarrowingChance,
                WallRune = level.WallRune,
                WallColor = level.WallColor,
                LevelName = level.Name
            };
            var generated = new WideMazeGenerator(settings,
                level.RoomEncounters.Select(ResolveForOneArea).ToArray(),
                level.CorridorEncounters.Select(ResolveForOneArea).ToArray(),
                new Random(9000 + levelNumber)).Create(ConsoleRenderer.PlayfieldWidth, ConsoleRenderer.PlayfieldHeight);
            Assert(generated.CheckFullAccessibility().IsFullyAccessible &&
                   generated.Enemies.Count(enemy => enemy.IsRoamingHordeMember) >= 2,
                $"A(z) {levelNumber}. szint egy képernyőnyi profilja nem generálható bejárható, hordás pályává.");
        }

        var demonLevels = new[] { MazeLevelConfigurations.Get(18), MazeLevelConfigurations.Get(19) };
        var configuredDemonIds = demonLevels
            .SelectMany(level => level.RoomEncounters.Concat(level.CorridorEncounters))
            .SelectMany(encounter => encounter.Members)
            .Select(member => member.EnemyId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newDemonIds = new[]
        {
            MonsterIds.Pokolfajzat, MonsterIds.DémoniKorcs, MonsterIds.Parázsdémon,
            MonsterIds.KarmosDémon, MonsterIds.Pokolőr, MonsterIds.Vérdémon
        };
        Assert(newDemonIds.All(configuredDemonIds.Contains) &&
               demonLevels.All(level => level.CorridorEncounters.Any(encounter =>
                   encounter.Members.Any(member => member.EnemyId == MonsterIds.Pokolfajzat) &&
                   encounter.Members.Sum(member => member.Count.Minimum) >= 17)),
            "Az új démonok nem mind kerültek be, vagy hiányzik a tömeges Pokolfajzat meatshield-horda.");
    }

    static void MazePassageSurvivesSaveRoundTrip()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Átjáróőr");
        var roster = new CharacterRoster();
        roster.Add(character);
        roster.Select(character);
        var maze = new Maze(9, 9);
        maze.Carve(maze.Entrance);
        var exit = new Position(7, 7);
        maze.Carve(exit);
        maze.PlaceExit(exit);
        maze.AddPassage(new MazePassage(exit, "AREA_2", new Position(2, 2)));
        var secondMaze = new Maze(9, 9);
        secondMaze.Carve(secondMaze.Entrance);
        secondMaze.AddPassage(new MazePassage(secondMaze.Entrance, "AREA_1", exit));
        var fog = new FogOfWar(maze.Width, maze.Height, 5);
        var secondFog = new FogOfWar(secondMaze.Width, secondMaze.Height, 5);
        var mapper = new GameStateMapper(data, roster, character);
        var save = mapper.Create(6, maze, new Player(maze.Entrance, character), fog, Direction.Right,
            [maze.Entrance], false, false, false, false, null, DateTime.UtcNow, new Dictionary<Enemy, DateTime>(),
            [], []);
        var secondSave = mapper.Create(6, secondMaze, new Player(secondMaze.Entrance, character), secondFog,
            Direction.Right, [secondMaze.Entrance], false, false, false, false, null, DateTime.UtcNow,
            new Dictionary<Enemy, DateTime>(), [], []);
        save.ActiveAreaId = "AREA_1";
        save.Areas =
        [
            new DungeonAreaSaveData("AREA_1", save.Maze, save.Fog),
            new DungeonAreaSaveData("AREA_2", secondSave.Maze, secondSave.Fog)
        ];
        var json = System.Text.Json.JsonSerializer.Serialize(save);
        var serialized = System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(json)!;
        var restored = mapper.Restore(serialized);
        var passage = restored.Maze.GetPassageAt(exit);
        var level = new DungeonLevel(
            [new DungeonArea("AREA_1", maze, fog), new DungeonArea("AREA_2", secondMaze, secondFog)], "AREA_1");
        level.Activate("AREA_2");
        Assert(passage is { DestinationAreaId: "AREA_2", DestinationPosition: { X: 2, Y: 2 } } &&
               serialized.Areas.Select(area => area.Id).SequenceEqual(["AREA_1", "AREA_2"]) &&
               level.ActiveArea.Maze.GetPassageAt(secondMaze.Entrance)?.DestinationAreaId == "AREA_1",
            "A többterületes mentés elvesztette az átjárót vagy a területazonosítót.");
    }

    static void TrapConfigurationScalesByMazeLevel()
    {
        var first = MazeLevelConfigurations.Get(1);
        var middle = MazeLevelConfigurations.Get(10);
        var final = MazeLevelConfigurations.Get(MazeLevelConfigurations.FinalLevel);
        Assert(first.TrapCount == new IntRange(3, 7) && first.TrapIds.SequenceEqual(["TR001"]),
            "Az első szint csapdakonfigurációja nem kezdőbarát.");
        Assert(middle.TrapCount == new IntRange(5, 10) && middle.TrapIds.Contains("TR005") &&
               middle.TrapIds.Contains("TR008") && !middle.TrapIds.Contains("TR006"),
            "A középső szintek csapdakonfigurációja nem megfelelően nehezedik.");
        Assert(final.TrapCount == new IntRange(6, 13) && final.TrapIds.Contains("TR007") &&
               !final.TrapIds.Contains("TR001"),
            "A végső szintek nem a legnehezebb csapdakészletet használják.");
        Assert(first.VisionModifier == 0 && MazeLevelConfigurations.Get(5).VisionModifier == -1 &&
               MazeLevelConfigurations.Get(9).VisionModifier == -2,
            "Az extra sötét pályák látótávmódosítója hibás.");
    }

    static void SavedTrapCanShareEnemyPosition()
    {
        const int size = 7;
        var overlap = new Position(3, 2);
        var entrance = new Position(2, 2);
        var exit = new Position(5, 5);
        var tiles = Enumerable.Repeat(Maze.Wall.Value, size * size).ToList();
        void SetTile(Position position, Rune tile) => tiles[position.Y * size + position.X] = tile.Value;
        SetTile(entrance, Maze.Floor);
        SetTile(overlap, Maze.Floor);
        SetTile(exit, Maze.Floor);

        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Betöltő");
        var roster = new CharacterRoster();
        roster.Add(character);
        roster.Select(character);
        var enemy = data.Enemies[0];
        var trap = data.Traps[0];
        var state = new GameSaveData
        {
            MazeLevel = 1,
            PlayerPosition = entrance,
            Maze = new MazeSaveData
            {
                Width = size,
                Height = size,
                WallCodePoint = Maze.Wall.Value,
                TileCodePoints = tiles,
                Exit = exit,
                Enemies = [new EnemySaveData(overlap, enemy.Id, enemy.HitPoints ?? 1)],
                Traps = [new TrapSaveData(overlap, trap.Id, TrapState.Hidden, false, 0)]
            }
        };

        var restored = new GameStateMapper(data, roster, character).Restore(state);
        Assert(restored.Maze.GetEnemyAt(overlap) is not null &&
               restored.Maze.GetTrapAt(overlap)?.Definition.Id == trap.Id,
            "A betöltés nem őrizte meg az egy mezőn álló szörnyet és csapdát.");
    }

    static void CursedLootChanceIsConfiguredPerMazeLevel()
    {
        Assert(MazeLevelConfigurations.Get(1).ItemCurseChancePercent == 8 &&
               MazeLevelConfigurations.Get(9).ItemCurseChancePercent == 30,
            "Az alapértelmezett vagy a korábbi elátkozott sírkamra-esély megváltozott.");
        Assert(MazeLevelConfigurations.Get(7).ItemCurseChancePercent == 15 &&
               MazeLevelConfigurations.Get(12).ItemCurseChancePercent == 15 &&
               MazeLevelConfigurations.Get(16).ItemCurseChancePercent == 20 &&
               MazeLevelConfigurations.Get(18).ItemCurseChancePercent == 25 &&
               MazeLevelConfigurations.Get(19).ItemCurseChancePercent == 25,
            "A kiemelten veszélyes pályák tárgyátok-esélyei nem a konfigurációból érkeznek.");
        Assert(QuestLocationConfigurations.Get(QuestLocationConfigurations.RodericMalrec)
                   .ItemCurseChancePercent == 8,
            "A küldetéshelyszínek nem öröklik az alapértelmezett tárgyátok-esélyt.");
    }

    static void LevelImageFileNamesAreNormalized()
    {
        Assert(ImageViewer.FileNameForLevel("Patkányjáratok") == "patkanyjaratok.png" &&
               ImageViewer.FileNameForLevel("A holtak katakombái") == "aholtakkatakombai.png" &&
               ImageViewer.FileNameForLevel("A démoni sík: Vértrónus") == "ademonisikvertronus.png" &&
               ImageViewer.FileNameForLevel("A Káoszrubin rejtekhelye") == "akaoszrubinrejtekhelye.png",
            "A pályakép fájlneve nem kisbetűs, egybeírt és ékezetmentes.");
    }

    static void TrapVisibilityFollowsDiscoveryState()
    {
        var maze = new Maze(7, 7);
        var position = new Position(3, 2);
        maze.Carve(position);
        var definition = new TrapDefinition("TR-TEST", "Tesztcsapda", new Rune('⌄'), TrapEffect.Damage,
            1, 7, 7, 3, 7, 0, 25, 75, "Teszt.");
        var trap = new MazeTrap(position, definition);
        maze.AddTrap(trap);
        var fog = new FogOfWar(maze.Width, maze.Height, 2);
        fog.RevealFrom(maze, maze.Entrance);

        var hidden = WorldSnapshotProjector.Create(maze, fog).RevealedCells.Single(cell => cell.Position == position);
        Assert(hidden.TileCodePoint == Maze.Floor.Value,
            "A rejtett csapda kiszivárgott a coop world snapshotba.");
        trap.Detect();
        var detected = WorldSnapshotProjector.Create(maze, fog).RevealedCells.Single(cell => cell.Position == position);
        Assert(detected.TileCodePoint == definition.Symbol.Value && detected.ForegroundColor == ConsoleColor.Yellow,
            "A felfedezett csapda nem jelent meg a coop world snapshotban.");
        trap.Disarm();
        var disarmed = WorldSnapshotProjector.Create(maze, fog).RevealedCells.Single(cell => cell.Position == position);
        Assert(disarmed.TileCodePoint == new Rune('·').Value && disarmed.ForegroundColor == ConsoleColor.DarkGray,
            "A hatástalanított csapda állapota nem replikálódott.");
    }

    static void EnemyTargetsNearestVisiblePartyMember()
    {
        var host = CreateCharacter("Host");
        var guest = CreateCharacter("Vendég");
        var npc = CreateCharacter("NPC");
        var candidates = new[]
        {
        (host, new Position(8, 8)),
        (guest, new Position(3, 2)),
        (npc, new Position(4, 2))
    };
        var target = EnemyTargeting.ChooseNearestVisible(new Position(2, 2), candidates,
            position => position != new Position(3, 2), new Random(1));
        Assert(target?.Character == npc,
            "Az ellenség nem a legközelebbi látható NPC-/vendégpozíciót választotta a host helyett.");

        target = EnemyTargeting.ChooseNearestVisible(new Position(2, 2), candidates,
            _ => true, new Random(1));
        Assert(target?.Character == guest,
            "Az ellenség figyelmen kívül hagyta a hostnál közelebbi vendégkaraktert.");
    }

    static void FighterTacticHitChancesUseCombatFormula()
    {
        var system = CreateBattleSystem(1701);
        var fighter = CreateCharacter("Harcos", characterClassId: CharacterClassIds.Harcos);
        var enemy = CreateEnemy(100, 1, speed: 8);
        var precise = system.EstimateCharacterHitChance(fighter, enemy, BattleTactic.FighterPrecise);
        var powerful = system.EstimateCharacterHitChance(fighter, enemy, BattleTactic.FighterPowerful);
        var defensive = system.EstimateCharacterHitChance(fighter, enemy, BattleTactic.FighterDefensive);
        Assert(precise == defensive + 10 && defensive == powerful + 5,
            $"A taktikai módosítók nem +2/0/-1 arányban változtatják az esélyt: {precise}/{defensive}/{powerful}%.");

        var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
        var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos", PrimaryAbilities.Zero,
            false, 1.0);
        var nearlyCertain = new LiveCharacter("Biztos", race, fighterClass,
            new PrimaryAbilities(5, 100, 5, 5), 20, 0, 1, 0);
        var nearlyImpossible = new LiveCharacter("Esélytelen", race, fighterClass,
            new PrimaryAbilities(5, -100, 5, 5), 20, 0, 1, 0);
        Assert(system.EstimateCharacterHitChance(nearlyCertain, enemy, BattleTactic.FighterPrecise) == 95,
            "A természetes 1 nem korlátozza 95%-ra a találati esélyt.");
        Assert(system.EstimateCharacterHitChance(nearlyImpossible, enemy, BattleTactic.FighterPowerful) == 5,
            "A természetes 20 nem biztosít legalább 5% találati esélyt.");
    }

#if false // A megszüntetett párbaj-állapotgép tesztjei.
static void EnemyInitiativeDelaysTacticPrompt()
{
    var system = CreateBattleSystem(710);
    var fighter = CreateCharacter("Harcos", vitality: 100, characterClassId: CharacterClassIds.Harcos);
    var state = system.StartBattle(fighter, CreateEnemy(100, 1, speed: 100)).State;
    Assert(state.IsOpeningEnemyTurn && state.RequiresTacticSelection && !state.IsAwaitingTacticSelection,
        "Az ellenséges nyitókör előtt a rendszer már taktikai inputot várt.");
    system.Advance(state);
    Assert(state.IsPlayerTurn && state.IsAwaitingTacticSelection,
        "Az első ellenséges támadás után nem jelent meg az első saját kör taktikai választása.");
}

static void InitiativeLogShowsSignedDie()
{
    var started = CreateBattleSystem(711).StartBattle(CreateCharacter("Kezdeményező"),
        CreateEnemy(100, 1));
    var message = started.Entries.Single(entry => entry.Message.StartsWith("Kezdeményezés:",
        StringComparison.Ordinal)).Message;
    Assert(message.Split("±1d2(", StringSplitOptions.None).Length == 3 &&
           !message.Contains(" +1d2(", StringComparison.Ordinal),
        "A kezdeményezési napló nem mindkét félnél ±1d2 formában mutatja a dobást.");
}

static void NeedStatusEffectsAreVisible()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var priestClass = new CharacterClassDefinition(CharacterClassIds.Pap, "Pap", PrimaryAbilities.Zero, true, 1.0);
    var player = new LiveCharacter("Éhező", race, priestClass, new PrimaryAbilities(5, 5, 5, 5),
        200, 100, 1, 1);
    player.ConsumeFood(100);
    player.ConsumeWater(100);
    player.AddStatus(data.GetStatus(CharacterStatusIds.Hungry));
    player.AddStatus(data.GetStatus(CharacterStatusIds.Thirsty));
    player.AddStatus(data.GetStatus(CharacterStatusIds.Diseased));
    player.ReceiveDamage(100);

    Assert(player.PreviewVitalityRecovery(100) == 37,
        "Az éhség és betegség kombinált gyógyításcsökkentése hibás.");
    var started = CreateBattleSystem(713).StartBattle(player, CreateEnemy(1000, 1));
    var startLog = string.Join(" ", started.Entries.Select(entry => entry.Message));
    Assert(startLog.Contains("🍖 nulla élelem: ❤️ -", StringComparison.Ordinal) &&
           startLog.Contains("💧 szomjúság: 🔷 -", StringComparison.Ordinal) &&
           startLog.Contains("💧 szomjúság 6", StringComparison.Ordinal),
        "A csatakezdő szükséglethatások vagy a szomjúság kezdeményezés-büntetése nem látható.");

    var attackLogs = new List<string>();
    for (var index = 0; index < 100 && !started.State.IsCompleted; index++)
    {
        var entry = CreateBattleSystem(713 + index).Advance(started.State).Entries.Single();
        if (entry.Details?.Actor == "Éhező") attackLogs.AddRange(entry.Details.Calculation);
        if (attackLogs.Any(log => log.Contains("éhség", StringComparison.OrdinalIgnoreCase))) break;
    }
    Assert(attackLogs.Any(log => log.Contains("szomj", StringComparison.OrdinalIgnoreCase)) &&
           attackLogs.Any(log => log.Contains("éhség", StringComparison.OrdinalIgnoreCase)),
        "A találat- vagy fizikai sebzésbüntetés oka nem látható a támadás naplójában.");
}

static void BattleHitHighlightsDamageAndHealth()
{
    var system = CreateBattleSystem(712);
    var player = CreateCharacter("Sebző", vitality: 500, characterClassId: CharacterClassIds.Pap);
    var state = system.StartBattle(player, CreateEnemy(1000, 1)).State;
    BattleActionDetails? playerHit = null;
    BattleActionDetails? enemyHit = null;
    for (var index = 0; index < 100 && (playerHit is null || enemyHit is null) && !state.IsCompleted; index++)
    {
        var entry = system.Advance(state).Entries.Single();
        if (entry.Details?.Actor == "Sebző" && entry.Details.Summary.Any(text => text.Contains("1/1")))
            playerHit = entry.Details;
        if (entry.Details?.Target == "Sebző" && entry.Details.Summary.Any(text => text.Contains("1/1")))
            enemyHit = entry.Details;
    }
    Assert(playerHit?.Summary.Any(text => text.StartsWith("💥")) == true &&
           playerHit.Target == "Tesztellenfél" &&
           enemyHit?.Summary.Any(text => text.StartsWith("💥")) == true &&
           enemyHit.Target == "Sebző",
        "A sikeres támadásból hiányzik a sebzés- vagy a megmaradt HP ikonja.");
}

#endif

    static void VictoryMessageIsConcise()
    {
        var enemy = CreateEnemy(1, 1);
        var result = new BattleResult(true, 7, ["7. kör — hosszú és redundáns utolsó támadás."]);
        Assert(ConsoleRenderer.FormatBattleResultMessage(result, enemy) ==
               "GYŐZELEM 🏆: Tesztellenfél elesett.",
            "A győzelmi üzenet továbbra is megismétli az utolsó támadás részleteit.");
    }

    static void SnapshotRequiresCurrentBattlePrompt()
    {
        var (session, leader, _) = CreateSession();
        var battleId = BattleId.New();
        session.SetBattlePrompt(battleId, 2, leader.Id,
            [BattleActionKind.PhysicalAttack, BattleActionKind.TurnUndead]);
        var battle = new BattleSnapshot(battleId, 2, 1, true, leader.Id,
            new SessionEnemySnapshot("E-TEST", "Tesztellenfél", new Position(3, 2), 8, 10),
            [BattleActionKind.PhysicalAttack, BattleActionKind.TurnUndead],
            [new BattleSpellOption("S-TEST", "Tesztvarázs", 1, 3, SpellTargetType.Enemy, 5, 0,
            null, null, 0, 0, [new Position(3, 2)])],
            [new BattleTacticOptionSnapshot(BattleActionKind.FighterPrecise, "🎯 Pontos", "sebzés ×0,75", 55)]);
        var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Tesztlabirintus",
            new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) }, battle));
        Assert(snapshot.Battle?.BattleId == battleId && snapshot.Battle.AllowedActions.Count == 2 &&
               snapshot.Battle.SpellOptions?.Single().ValidTargets.Single() == new Position(3, 2) &&
               snapshot.Battle.TacticOptions?.Single().HitChancePercent == 55,
            "Az aktív harci prompt nem került a snapshotba.");

        var stale = battle with { TurnId = 1 };
        var rejected = false;
        try
        {
            session.CreateSnapshot(new SessionSnapshotContext(1, "Tesztlabirintus",
                new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) }, stale));
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Assert(rejected, "A lejárt körhöz tartozó harci snapshotot elfogadta a session.");
    }

    static void WorldSnapshotOnlyContainsRevealedState()
    {
        var maze = new Maze(7, 7);
        var visibleEnemy = CreateEnemyAt(new Position(3, 2), "E-VISIBLE", "r");
        var hiddenEnemy = CreateEnemyAt(new Position(4, 5), "E-HIDDEN");
        var visibleNpc = new WorldNpc(new Position(1, 1), "NPC-VISIBLE", CreateCharacter("Segítő"),
            NpcDisposition.Friendly, true, false, "Veletek tartok.");
        var hiddenNpc = new WorldNpc(new Position(5, 4), "NPC-HIDDEN", CreateCharacter("Rejtett"),
            NpcDisposition.Neutral, false, true, "Titok.");
        foreach (var position in new[] { visibleEnemy.Position, hiddenEnemy.Position, new Position(1, 2),
                 visibleNpc.Position, hiddenNpc.Position })
            maze.Carve(position);
        maze.AddEnemy(visibleEnemy);
        maze.AddEnemy(hiddenEnemy);
        maze.AddTreasureChest(new TreasureChest(new Position(1, 2), 99));
        maze.AddWorldNpc(visibleNpc);
        maze.AddWorldNpc(hiddenNpc);
        maze.PlaceDoor(new Position(2, 3), DoorState.Closed);
        var fog = new FogOfWar(maze.Width, maze.Height, 1);
        fog.RevealFrom(maze, maze.Entrance);
        fog.ToggleDeveloperReveal();

        var world = WorldSnapshotProjector.Create(maze, fog);
        Assert(world.Enemies.Count == 1 && world.Enemies[0].EntityId == visibleEnemy.Id,
            "A world snapshot rejtett ellenfelet is publikált, vagy kihagyta a láthatót.");
        Assert(world.Chests.Count == 1 && world.Doors.Count == 1,
            "A felfedett statikus entitások hiányoznak a world snapshotból.");
        Assert(world.Npcs?.Single().EntityId == visibleNpc.Id && world.Npcs.Single().Name == "Segítő",
            "A world snapshot rejtett NPC-t publikált, vagy kihagyta a láthatót.");
        Assert(world.Chests.Single().SymbolCodePoint == new Rune('▣').Value &&
               world.Chests.Single().ForegroundColor == ConsoleColor.Yellow,
            "A world snapshot nem őrizte meg a láda hostoldali megjelenését.");
        Assert(world.Exit is null && world.RevealedCells.All(cell => fog.IsRevealed(cell.Position)),
            "A world snapshot rejtett kijáratot vagy cellát publikált.");
        Assert(world.RevealedCells.Single(cell => cell.Position == new Position(2, 1)).ForegroundColor == maze.WallColor,
            "A world snapshot nem őrizte meg a host falszínét.");
        Assert(world.Doors.Single().ForegroundColor == ConsoleColor.DarkYellow &&
               world.Doors.Single().SymbolCodePoint == new Rune('╬').Value,
            "A world snapshot nem őrizte meg a host ajtószínét vagy ajtójelét.");
        Assert(world.Enemies.Single().Color != ConsoleColor.Red,
            "A world snapshot a host erősségfüggő színe helyett fix kliensszínt adott az ellenfélnek.");
        Assert(world.Enemies.Single().SymbolCodePoint == new Rune('r').Value,
            "A world snapshot nem őrizte meg az ellenfél katalógusban megadott jelét.");
        var restored = JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(world));
        Assert(restored?.Enemies.Single().DefinitionId == "E-VISIBLE",
            "A world snapshot JSON round-trip közben megváltozott.");
    }

    static void WorldEntityIdSurvivesMovement()
    {
        var maze = new Maze(7, 7);
        var enemy = CreateEnemyAt(new Position(2, 3), "E-MOVING");
        var destination = new Position(3, 3);
        maze.Carve(enemy.Position);
        maze.Carve(destination);
        maze.AddEnemy(enemy);
        var entityId = enemy.Id;
        Assert(maze.TryMoveEnemy(enemy, destination), "A tesztellenfél nem tudott elmozdulni.");
        Assert(enemy.Id == entityId, "A world entity azonosítója mozgáskor megváltozott.");
    }

    static void CorpsesStackOnOneCell()
    {
        var maze = new Maze(7, 7);
        var position = new Position(3, 3);
        var first = new MonsterCorpse(position, "Első", "E001");
        var second = new MonsterCorpse(position, "Második", "E002");
        maze.AddCorpse(first);
        maze.AddCorpse(second);

        var stack = maze.GetCorpsesAt(position);
        Assert(stack.Count == 2 && stack.Contains(first) && stack.Contains(second) &&
               maze.GetObjectAt(position) is Corpse,
            "Az egy mezőre kerülő holttestek nem maradtak meg közös halomban.");
        first.MarkSearched();
        Assert(maze.GetUnsearchedMonsterCorpsesAt(position).SequenceEqual([second]),
            "A keresés nem pontosan a halom még át nem kutatott holttesteit választja ki.");
    }

    static void WorldDeltaCapturesChanges()
    {
        var (session, leader, _) = CreateSession();
        var characterPositions = new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) };
        var maze = new Maze(7, 7);
        var enemy = CreateEnemyAt(new Position(3, 2), "E-DELTA");
        var chest = new TreasureChest(new Position(1, 2), 10);
        foreach (var position in new[] { enemy.Position, new Position(3, 3), chest.Position }) maze.Carve(position);
        maze.AddEnemy(enemy);
        maze.AddTreasureChest(chest);
        maze.PlaceDoor(new Position(2, 3), DoorState.Closed);
        var fog = new FogOfWar(maze.Width, maze.Height, 1);
        fog.RevealFrom(maze, maze.Entrance);
        var previous = WorldSnapshotProjector.Create(maze, fog);
        var previousSession = session.CreateSnapshot(new SessionSnapshotContext(1, "Delta-labirintus",
            characterPositions, World: previous));

        enemy.SetCurrentHitPoints(6);
        Assert(maze.TryMoveEnemy(enemy, new Position(3, 3)), "A delta tesztellenfele nem tudott mozogni.");
        maze.SetDoorState(maze.GetDoorAt(new Position(2, 3))!, DoorState.Open);
        maze.RemoveTreasureChest(chest);
        var corpse = new MonsterCorpse(new Position(1, 2), "Elesett", "E-DEAD");
        maze.AddCorpse(corpse);
        fog.RevealFrom(maze, new Position(3, 3));
        var current = WorldSnapshotProjector.Create(maze, fog);
        var currentSession = session.CreateSnapshot(new SessionSnapshotContext(1, "Delta-labirintus",
            characterPositions, World: current));

        var delta = WorldDeltaProjector.Create(previousSession, currentSession);
        Assert(delta.EnemyUpserts.Single() is { CurrentHitPoints: 6, Position: { X: 3, Y: 3 } },
            "Az ellenfél mozgása vagy HP-változása hiányzik a deltából.");
        Assert(delta.DoorUpserts.Single().State == DoorState.Open && delta.CorpseUpserts.Single().EntityId == corpse.Id,
            "Az ajtóállapot vagy az új tetem hiányzik a deltából.");
        Assert(delta.RemovedEntityIds.Contains(chest.Id) && delta.RevealedOrChangedCells.Count > 0,
            "Az entitáseltávolítás vagy cellafelfedés hiányzik a deltából.");
        var restored = JsonSerializer.Deserialize<WorldDelta>(JsonSerializer.Serialize(delta));
        Assert(restored?.ToSnapshotSequence == currentSession.SnapshotSequence && restored.EnemyUpserts.Count == 1,
            "A world delta JSON round-trip közben megváltozott.");
    }

    static void WorldDeltaRejectsDifferentWorld()
    {
        var firstMaze = new Maze(7, 7);
        var secondMaze = new Maze(7, 7);
        var first = WorldSnapshotProjector.Create(firstMaze, new FogOfWar(7, 7, 0));
        var second = WorldSnapshotProjector.Create(secondMaze, new FogOfWar(7, 7, 0));
        var rejected = false;
        try
        {
            WorldDeltaProjector.Create(1, first, 2, second);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Assert(rejected, "Különböző WorldId értékek között is elkészült a delta.");
    }

    static void ReplicationPublisherUsesAcknowledgedBaseline()
    {
        var (session, leader, _) = CreateSession();
        var positions = new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) };
        var maze = new Maze(7, 7);
        maze.Carve(new Position(3, 2));
        var fog = new FogOfWar(7, 7, 1);
        fog.RevealFrom(maze, maze.Entrance);
        var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Replikációs pálya", positions,
            World: WorldSnapshotProjector.Create(maze, fog)));
        var playerId = PlayerId.New();
        var publisher = new SessionReplicationPublisher();

        var full = publisher.CreateFrame(playerId, first);
        Assert(full.Kind == SessionReplicationFrameKind.FullSnapshot && full.Session.World is not null &&
               full.WorldDelta is null && full.BaseSnapshotSequence is null,
            "Az első replikációs frame nem teljes snapshot.");
        Assert(publisher.TryAcknowledge(playerId, first.SnapshotSequence, out var error), error);

        var enemy = CreateEnemyAt(new Position(3, 2), "E-REPLICATION");
        maze.AddEnemy(enemy);
        var second = session.CreateSnapshot(new SessionSnapshotContext(1, "Replikációs pálya", positions,
            World: WorldSnapshotProjector.Create(maze, fog)));
        var delta = publisher.CreateFrame(playerId, second);
        Assert(delta.Kind == SessionReplicationFrameKind.Delta && delta.Session.World is null &&
               delta.BaseSnapshotSequence == first.SnapshotSequence &&
               delta.WorldDelta?.EnemyUpserts.Single().EntityId == enemy.Id,
            "A nyugtázott baseline után nem megfelelő world delta készült.");
        var restored = JsonSerializer.Deserialize<SessionReplicationFrame>(JsonSerializer.Serialize(delta));
        Assert(restored?.WorldDelta?.ToSnapshotSequence == second.SnapshotSequence,
            "A replikációs frame JSON round-trip közben megváltozott.");
    }

    static void UnknownReplicationAckForcesResync()
    {
        var (session, leader, _) = CreateSession();
        var maze = new Maze(7, 7);
        var fog = new FogOfWar(7, 7, 0);
        fog.RevealFrom(maze, maze.Entrance);
        var positions = new Dictionary<CharacterId, Position> { [leader.Id] = maze.Entrance };
        var publisher = new SessionReplicationPublisher();
        var playerId = PlayerId.New();
        var first = session.CreateSnapshot(new SessionSnapshotContext(1, "ACK pálya", positions,
            World: WorldSnapshotProjector.Create(maze, fog)));
        publisher.CreateFrame(playerId, first);
        Assert(!publisher.TryAcknowledge(playerId, first.SnapshotSequence + 99, out _),
            "Az ismeretlen snapshot ACK-ot elfogadta a publisher.");
        var second = session.CreateSnapshot(new SessionSnapshotContext(1, "ACK pálya", positions,
            World: WorldSnapshotProjector.Create(maze, fog)));
        Assert(publisher.CreateFrame(playerId, second).Kind == SessionReplicationFrameKind.FullSnapshot,
            "Ismeretlen ACK után nem történt teljes resync.");
    }

    static void ReplicationPublisherUsesFullSnapshotForNewWorld()
    {
        var (session, leader, _) = CreateSession();
        var positions = new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) };
        var firstMaze = new Maze(7, 7);
        var firstFog = new FogOfWar(7, 7, 0);
        firstFog.RevealFrom(firstMaze, firstMaze.Entrance);
        var publisher = new SessionReplicationPublisher();
        var playerId = PlayerId.New();
        var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Első pálya", positions,
            World: WorldSnapshotProjector.Create(firstMaze, firstFog)));
        publisher.CreateFrame(playerId, first);
        Assert(publisher.TryAcknowledge(playerId, first.SnapshotSequence, out var error), error);

        var secondMaze = new Maze(7, 7);
        var secondFog = new FogOfWar(7, 7, 0);
        secondFog.RevealFrom(secondMaze, secondMaze.Entrance);
        var second = session.CreateSnapshot(new SessionSnapshotContext(2, "Második pálya", positions,
            World: WorldSnapshotProjector.Create(secondMaze, secondFog)));
        Assert(publisher.CreateFrame(playerId, second).Kind == SessionReplicationFrameKind.FullSnapshot,
            "Pályaváltáskor a publisher deltát próbált küldeni.");
    }

    static void ClientStoreAppliesReplicationFrames()
    {
        var (session, leader, _) = CreateSession();
        var playerId = PlayerId.New();
        var publisher = new SessionReplicationPublisher();
        var store = new ClientSessionStore(playerId);
        var changedSnapshots = new List<SessionSnapshot>();
        store.SnapshotChanged += changedSnapshots.Add;
        var maze = new Maze(7, 7);
        var enemyPosition = new Position(3, 2);
        maze.Carve(maze.Entrance);
        maze.Carve(enemyPosition);
        var fog = new FogOfWar(7, 7, 1);
        fog.RevealFrom(maze, maze.Entrance);
        var positions = new Dictionary<CharacterId, Position> { [leader.Id] = maze.Entrance };

        var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Kliens pálya", positions,
            World: WorldSnapshotProjector.Create(maze, fog)));
        var full = publisher.CreateFrame(playerId, first);
        var fullResult = store.Apply(full);
        Assert(fullResult is { Status: ClientFrameApplyStatus.Applied, Response: SnapshotAck } &&
               store.CurrentSnapshot?.World is not null, "A kliens store nem alkalmazta a teljes snapshotot.");
        Assert(publisher.TryAcknowledge(playerId, first.SnapshotSequence, out var ackError), ackError);

        var enemy = CreateEnemyAt(enemyPosition, "E-CLIENT-DELTA");
        maze.AddEnemy(enemy);
        var second = session.CreateSnapshot(new SessionSnapshotContext(1, "Kliens pálya", positions,
            World: WorldSnapshotProjector.Create(maze, fog)));
        var firstDelta = publisher.CreateFrame(playerId, second);
        Assert(store.Apply(firstDelta).Status == ClientFrameApplyStatus.Applied &&
               store.CurrentSnapshot!.World!.Enemies.Single().EntityId == enemy.Id,
            "A kliens store nem alkalmazta az entitás-upsertet.");

        // Az ACK még nem ért vissza a hosthoz: a következő delta továbbra is az első snapshotból indul.
        maze.ReplaceEnemyWithCorpse(enemy);
        var third = session.CreateSnapshot(new SessionSnapshotContext(1, "Kliens pálya", positions,
            World: WorldSnapshotProjector.Create(maze, fog)));
        var oldBaselineDelta = publisher.CreateFrame(playerId, third);
        Assert(oldBaselineDelta.BaseSnapshotSequence == first.SnapshotSequence,
            "A tesztframe nem a várt régi ACK-baseline-ról indult.");
        Assert(store.Apply(oldBaselineDelta).Status == ClientFrameApplyStatus.Applied &&
               store.CurrentSnapshot!.World!.Enemies.Count == 0 &&
               store.CurrentSnapshot.World.Corpses.Count == 1 && changedSnapshots.Count == 3,
            "A kliens nem a deklarált baseline-ra alkalmazta a deltát, vagy nem publikálta az új read modelt.");
    }

    static void ClientStoreRequestsResyncForMissingBaseline()
    {
        var playerId = PlayerId.New();
        var world = new WorldSnapshot(WorldId.New(), 7, 7, null, null, [], [], [], [], [], []);
        var delta = new WorldDelta(10, 11, null, null, [], [], [], [], [], [], [], []);
        var session = new SessionSnapshot(SessionProtocol.Version, 11, 0, GameSessionPhase.Exploration,
            PlayerId.New(), CharacterId.New(), 1, "Hiányzó baseline", [], [], null);
        var frame = new SessionReplicationFrame(SessionReplicationFrameKind.Delta, playerId, 10, session, delta);
        var store = new ClientSessionStore(playerId);
        var result = store.Apply(frame);
        Assert(result is { Status: ClientFrameApplyStatus.ResyncRequired, Response: SnapshotResyncRequest } &&
               store.CurrentSnapshot is null,
            "A kliens hiányzó baseline esetén nem kért teljes resyncet.");

        var wrongRecipient = new SessionReplicationFrame(SessionReplicationFrameKind.FullSnapshot, PlayerId.New(),
            null, session with { SnapshotSequence = 1, World = world }, null);
        Assert(store.Apply(wrongRecipient).Status == ClientFrameApplyStatus.Rejected,
            "A kliens elfogadta a másik játékosnak címzett snapshotot.");
    }

    static void InventorySnapshotHasSlotsAndRevision()
    {
        var character = CreateCharacter("Inventory");
        var ration = new MiscItemDefinition("I-FOOD", "Útravaló", "Tesztélelem", 2, ConsumableEffect.Food, 10);
        Assert(character.AddToBackpack(ration), "A teszttárgy nem került a hátizsákba.");
        var first = InventorySnapshotProjector.Create(character);
        Assert(first.Revision == 1 && first.Slots.Count == 19 &&
               first.Slots.Single(slot => slot.Kind == InventorySlotKind.Backpack && slot.Index == 0)
                   .Item?.DefinitionId == ration.Id,
            "Az inventory snapshot slotjai vagy revíziója hibás.");
        Assert(character.SetInventoryItem(InventorySlotKind.Backpack, 0, null), "A teszttárgy nem távolítható el.");
        var second = InventorySnapshotProjector.Create(character);
        Assert(second.Revision == first.Revision + 1 &&
               second.Slots.Single(slot => slot.Kind == InventorySlotKind.Backpack && slot.Index == 0).Item is null,
            "Az inventory mutáció nem növelte pontosan egyszer a revíziót.");
    }

    static void PartyVisionTracksLastKnownEnemy()
    {
        var maze = new Maze(15, 9);
        for (var x = 2; x <= 8; x++) maze.Carve(new Position(x, 2));
        maze.Carve(new Position(2, 6));
        var enemy = CreateEnemyAt(new Position(7, 2), "E-MEMORY");
        maze.AddEnemy(enemy);
        var fog = new FogOfWar(maze.Width, maze.Height, 5);
        fog.UpdatePartyVisibility(maze, [(maze.Entrance, 3)], advanceEnemyMemory: false);
        Assert(WorldSnapshotProjector.Create(maze, fog).Enemies.Single().EntityId == enemy.Id,
            "A partitag által látott szörny nem jelent meg a közös world snapshotban.");

        fog.UpdatePartyVisibility(maze, [(new Position(2, 6), 1)], advanceEnemyMemory: true);
        var hidden = WorldSnapshotProjector.Create(maze, fog);
        Assert(hidden.Enemies.Count == 0 && hidden.LastKnownEnemies is
                   [{ EntityId: var rememberedId, Position: var rememberedPosition, RemainingPartyMoves: 3 }] &&
               rememberedId == enemy.Id && rememberedPosition == enemy.Position,
            "A látótérből kikerült szörny nem tűnt el, vagy nem maradt meg az utolsó ismert helye.");

        for (var step = 0; step < 3; step++)
            fog.UpdatePartyVisibility(maze, [(new Position(2, 6), 1)], advanceEnemyMemory: true);
        Assert(fog.EnemyMemories.Count == 0,
            "Az utolsó ismert szörnyhely három partimozgás után sem tűnt el.");
    }

    static void PartyPerceptionDetectsStealthAndSound()
    {
        var maze = new Maze(11, 7);
        var origin = new Position(2, 3);
        var enemyPosition = new Position(8, 3);
        for (var x = 1; x <= 9; x++)
            for (var y = 2; y <= 4; y++) maze.Carve(new Position(x, y));
        var stealthy = new ConfiguredEnemy(enemyPosition,
            new EnemyDefinition("E-STEALTH", "Árnyjáró", "a", 1, 10, 0, 1, 10, 1, [],
                VisionRange: 5, Stealth: 2, Noise: 0));
        maze.AddEnemy(stealthy);
        var fog = new FogOfWar(maze.Width, maze.Height, 5);

        fog.UpdatePartyVisibility(maze, [new PartyPerceptionSource(origin, 4, 0, 0)], false);
        Assert(WorldSnapshotProjector.Create(maze, fog).Enemies.Count == 0,
            "A lopakodó ellenfél felderítési próba nélkül kiszivárgott.");
        stealthy.MoveTo(new Position(7, 3));
        fog.UpdatePartyVisibility(maze, [new PartyPerceptionSource(origin, 4, 0, 0)], false);
        Assert(WorldSnapshotProjector.Create(maze, fog).Enemies.Single().EntityId == stealthy.Id,
            "A lopakodó ellenfél mozgása nem fedte fel átmenetileg.");
        fog.UpdatePartyVisibility(maze, [new PartyPerceptionSource(origin, 4, 0, 0)], false);
        Assert(WorldSnapshotProjector.Create(maze, fog).Enemies.Count == 0,
            "A lopakodó ellenfél nem tudott újra elrejtőzni a mozgása után.");
        fog.UpdatePartyVisibility(maze, [new PartyPerceptionSource(origin, 4, 0, 2)], false);
        Assert(WorldSnapshotProjector.Create(maze, fog).Enemies.Single().EntityId == stealthy.Id,
            "A jobb közös észlelés nem fedte fel a lopakodó ellenfelet.");

        var noisyMaze = new Maze(11, 7);
        for (var x = 1; x <= 9; x++)
            for (var y = 2; y <= 4; y++) noisyMaze.Carve(new Position(x, y));
        var noisy = new ConfiguredEnemy(enemyPosition,
            new EnemyDefinition("E-NOISY", "Csörtető", "c", 1, 10, 0, 1, 10, 1, [],
                VisionRange: 5, Stealth: 0, Noise: 4));
        noisyMaze.AddEnemy(noisy);
        var hearingFog = new FogOfWar(noisyMaze.Width, noisyMaze.Height, 1);
        hearingFog.UpdatePartyVisibility(noisyMaze,
            [new PartyPerceptionSource(origin, 1, 4, 0)], false);
        var heard = WorldSnapshotProjector.Create(noisyMaze, hearingFog);
        Assert(heard.Enemies.Count == 0 && heard.LastKnownEnemies is
                   [{ IsSoundCue: true, Position: var heardPosition }] && heardPosition != enemyPosition,
            "A hallott ellenfél hangjele pontos helyet vagy teljes szörnyadatot árult el.");
    }

    static void VictorySummaryIsCompact()
    {
        var enemy = CreateEnemy(1, 1);
        var result = new BattleResult(true, 4, []);
        Assert(ConsoleRenderer.FormatBattleVictorySummary(result, enemy, 11, 8, ["🤒"], 4) ==
               "GYŐZELEM 🏆: Tesztellenfél elesett. ⌛4❤️-11🔷-8🤒🍖-4💧-4",
            "A győzelmi összegzés nem a várt kompakt formátumot használja.");
        Assert(ConsoleRenderer.FormatBattleVictorySummary(result, enemy, 0, 0, [], 2) ==
               "GYŐZELEM 🏆: Tesztellenfél elesett. ⌛4🍖-2💧-2",
            "A nulla HP-/manaveszteséget nem szabad megjeleníteni.");
        Assert(ConsoleRenderer.FormatAutoBattleVictorySummary(result, "Borin", enemy, 11, 8, ["🤒"], 4, 2, 120) ==
               "AUTOCSATA 🏆: Borin → Tesztellenfél elesett. ⌛4❤️-11🔷-8🤒🍖-4💧-4📜2✨120XP",
            "A győztes autocsata összegzése nem kompakt vagy nem nevezi meg a harcoló társat.");
        Assert(ConsoleRenderer.FormatAutoBattleDefeatSummary(result with { PlayerWon = false }, "Borin", enemy,
                   20, 3, [], 4, 0) ==
               "AUTOCSATA 💀: Borin elesett → Tesztellenfél. ⌛4❤️-20🔷-3🍖-4💧-4👹1HP",
            "A vesztes autocsata összegzése nem kompakt vagy nem mutatja az ellenfél megmaradt HP-ját.");
    }

    static void BackpackStacksIdenticalItemsUpToNine()
    {
        var character = CreateCharacter("Kötegteszt");
        var ration = new MiscItemDefinition("I-STACK", "Útravaló", "Tesztélelem", 2,
            ConsumableEffect.Food, 10);
        for (var count = 0; count < 10; count++)
            Assert(character.AddToBackpack(ration), "Az azonos tárgy nem fért be a hátizsákba.");
        Assert(character.Backpack.Count == 12 &&
               character.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 10 &&
               character.GetInventoryItem(InventorySlotKind.Backpack, 1) is null,
            "A hátizsák nem egyetlen, legfeljebb tizenkét darabos kötegbe rendezte a tárgyakat.");
        Assert(character.RemoveOneInventoryItem(InventorySlotKind.Backpack, 0) &&
               character.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 9,
            "Egy tárgy elvétele nem pontosan eggyel csökkentette a köteget.");
        var snapshot = InventorySnapshotProjector.Create(character);
        Assert(snapshot.Slots.Single(slot => slot.Kind == InventorySlotKind.Backpack && slot.Index == 0)
                    .Item?.Quantity == 9,
            "A coop inventory snapshot nem továbbította a köteg darabszámát.");
    }

    static void CharacterSheetLayoutIsShared()
    {
        var character = CreateCharacter("Közös lap");
        var experienceByLevel = new Dictionary<int, int> { [2] = 100 };
        var inventory = InventorySnapshotProjector.Create(character);
        var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
            character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
            character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel, character.Gold,
            character.IsAlive, null, [], inventory,
            CharacterSheetSnapshotProjector.Create(character, experienceByLevel));

        var hostLines = CharacterSheetPanel.Build(character, experienceByLevel, 3, 1, 4);
        var guestLines = CharacterSheetPanel.Build(snapshot, 3, 1, 4);
        Assert(hostLines.SequenceEqual(guestLines),
            "A doménkarakterből és a hálózati snapshotból felépített karakterlap eltér.");
        var abilityLine = hostLines.Single(line => line.Row == 4);
        Assert(abilityLine.Text.Contains("💖", StringComparison.Ordinal) &&
               abilityLine.Text.EndsWith("👁️5", StringComparison.Ordinal) &&
               abilityLine.ColoredTextStart == abilityLine.Text.Length - 1 &&
               abilityLine.ColoredTextColor == ConsoleColor.White &&
               abilityLine.Text.Length <= CharacterSheetPanel.Width,
            $"A karakterlap képességsora nem mutatja szabályosan a látótávot: '{abilityLine.Text}{abilityLine.ColoredSuffix}'.");
        var restored = JsonSerializer.Deserialize<SessionCharacterSnapshot>(JsonSerializer.Serialize(snapshot));
        Assert(restored is not null && CharacterSheetPanel.Build(restored, 3, 1, 4).SequenceEqual(hostLines),
            "A közös karakterlap read modelje nem élte túl a JSON wire-körutat.");
        var wideHostLines = CharacterSheetPanel.Build(character, experienceByLevel, 3, 1, 4, width: 40);
        var wideGuestLines = CharacterSheetPanel.Build(snapshot, 3, 1, 4, width: 40);
        Assert(wideHostLines.SequenceEqual(wideGuestLines) &&
               wideHostLines.Single(line => line.Row == 12).Text.Length == 40 &&
               wideHostLines.Single(line => line.Row == 16).Text.Length == 40,
            "A széles karakterlap-sorok nem használják az extra jobb oldali helyet azonosan hoston és vendégen.");
        var hostLeaderLine = CharacterSheetPanel.Build(character, experienceByLevel, 3, 1, 4, true)
            .Single(line => line.Row == 2);
        var guestLeaderLine = CharacterSheetPanel.Build(snapshot, 3, 1, 4, true)
            .Single(line => line.Row == 2);
        Assert(hostLeaderLine == guestLeaderLine && hostLeaderLine.Text.Contains("👑 VEZÉR", StringComparison.Ordinal) &&
               hostLeaderLine.Color == ConsoleColor.Yellow,
            "A host és a vendég karakterlapján nem azonos a feltűnő vezérjelzés.");
        var followerSnapshot = snapshot with { IsTemporaryFollower = true };
        var hostFollowerLine = CharacterSheetPanel.Build(character, experienceByLevel, 3, 1, 4,
            isTemporaryFollower: true).Single(line => line.Row == 2);
        var guestFollowerLine = CharacterSheetPanel.Build(followerSnapshot, 3, 1, 4)
            .Single(line => line.Row == 2);
        Assert(hostFollowerLine == guestFollowerLine &&
               hostFollowerLine.Text.Contains("👤 KÖVETŐ", StringComparison.Ordinal) &&
               hostFollowerLine.Color == ConsoleColor.Black &&
               hostFollowerLine.Background == ConsoleColor.Yellow,
            "A host és a vendég karakterlapján nem azonos a követőjelzés szövege vagy színe.");
    }

    static void CharacterDetailsAreShared()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var character = CreateCharacter("Dosszié", characterClassId: CharacterClassIds.Tolvaj);
        character.SetNpcBehavior(NpcBehavior.Defensive);
        character.SetNpcJoinOrigin(3, "A Rézcsengő");
        character.RecordMonsterKill(data.Enemies[0].Id, 2);
        var visionItem = data.GetItem(MiscItemIds.Torch);
        character.ApplySpellEffect(new ActiveSpellEffect(visionItem.Id, ActiveSpellEffectType.VisionBonus, 2, 12));
        Assert(character.TryAdvanceWeaponProficiency(WeaponFamilies.Dagger),
            "A részletes karakterlap fegyverjártassági tesztje nem készíthető elő.");
        var sheet = CharacterSheetSnapshotProjector.Create(character, data.ExperienceByLevel, -2);
        var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
            character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
            character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel, character.Gold,
            character.IsAlive, null, [], InventorySnapshotProjector.Create(character), sheet, character.Color,
            History: new CharacterHistorySnapshot([new MonsterKillSnapshot(data.Enemies[0].Id, 2)], 3,
                "A Rézcsengő", NpcBehavior.Defensive.ToString()));
        var lines = CharacterDetailsWindow.Build(snapshot, data);
        Assert(GameInputBindings.InventoryAction(ConsoleKey.R) == InventoryInputAction.CharacterDetails &&
               WindowFrameConfiguration.For(FramedWindow.CharacterDetails) == WindowFrameStyle.Stone,
            "Az R billentyű vagy a stone keret nincs bekötve.");
        Assert(lines.Any(line => line.Text.Contains("Tolvaj osztály", StringComparison.Ordinal)) &&
               lines.Any(line => line.Text.Contains("Pálya/környezet", StringComparison.Ordinal) && line.Color == ConsoleColor.Red) &&
               lines.Any(line => line.Text.Contains(visionItem.Name, StringComparison.Ordinal) &&
                                 !line.Text.Contains(visionItem.Id, StringComparison.Ordinal)) &&
               lines.Any(line => line.Text.Contains("🗡️ Tőr — Jártas", StringComparison.Ordinal)) &&
               lines.Any(line => line.Text.Contains("A Rézcsengő", StringComparison.Ordinal)) &&
               lines.Any(line => line.Text.Contains(data.Enemies[0].Name, StringComparison.Ordinal) && line.Text.Contains("2", StringComparison.Ordinal)),
            "A közös részletes karakterlapból hiányzik egy látás-, NPC- vagy ölési adat.");
    }

    static void GuestAvatarUsesClassGlyphAndCharacterColor()
    {
        Assert(CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Harcos) == "H" &&
               CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Barbár) == "B" &&
               CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Lovag) == "L" &&
               CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Tolvaj) == "T" &&
               CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Pap) == "P" &&
               CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Mágus) == "M",
            "A kasztazonosítók nem a megfelelő térképi betűre képződnek.");
        var (session, leader, companion) = CreateSession();
        var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Színteszt",
            new Dictionary<CharacterId, Position>
            {
                [leader.Id] = new Position(1, 1),
                [companion.Id] = new Position(2, 1)
            }));
        Assert(snapshot.Party.Single(character => character.CharacterId == leader.Id).Color == leader.Color &&
               snapshot.Party.Single(character => character.CharacterId == companion.Id).Color == companion.Color,
            "A session snapshot nem őrizte meg a karakterhez rendelt konzolszínt.");
    }

    static void GuestRedrawIgnoresReplicationSequences()
    {
        var (session, leader, companion) = CreateSession();
        var positions = new Dictionary<CharacterId, Position>
        {
            [leader.Id] = new Position(1, 1),
            [companion.Id] = new Position(2, 1)
        };
        var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Render", positions));
        var sequenceOnly = first with
        {
            SnapshotSequence = first.SnapshotSequence + 10,
            LastEventSequence = first.LastEventSequence + 5
        };
        var changed = first with { MazeLevel = first.MazeLevel + 1 };
        Assert(CoopGuestRenderFingerprint.Compute(first) == CoopGuestRenderFingerprint.Compute(sequenceOnly),
            "A render fingerprint puszta replikációs sorszámra megváltozott.");
        Assert(CoopGuestRenderFingerprint.Compute(first) != CoopGuestRenderFingerprint.Compute(changed),
            "A render fingerprint valódi látható állapotváltozást nem érzékelt.");
    }

    static void ReplicationPublisherSharesPartyInventories()
    {
        var (session, leader, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        leader.AddToBackpack(new MiscItemDefinition("I-LEADER", "Leader tárgy", "Teszt", 1));
        companion.AddToBackpack(new MiscItemDefinition("I-GUEST", "Vendég tárgy", "Teszt", 1));
        var maze = new Maze(7, 7);
        var fog = new FogOfWar(7, 7, 0);
        fog.RevealFrom(maze, maze.Entrance);
        var positions = new Dictionary<CharacterId, Position>
        {
            [leader.Id] = maze.Entrance,
            [companion.Id] = new Position(3, 2)
        };
        var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Inventory pálya", positions,
            World: WorldSnapshotProjector.Create(maze, fog)));
        var experienceByLevel = new Dictionary<int, int> { [2] = 100 };
        snapshot = snapshot with
        {
            Party = snapshot.Party.Select(character => character with
            {
                CharacterSheet = CharacterSheetSnapshotProjector.Create(
                    character.CharacterId == leader.Id ? leader : companion, experienceByLevel)
            }).ToArray()
        };
        var publisher = new SessionReplicationPublisher();
        var hostFrame = publisher.CreateFrame(session.HostPlayerId, snapshot);
        var guestFrame = publisher.CreateFrame(remote, snapshot);

        Assert(hostFrame.Session.Party.All(character => character.Inventory is not null),
            "A host nem kapta meg a teljes parti inventory read modelt.");
        Assert(guestFrame.Session.Party.All(character => character.Inventory is not null),
            "A vendég nem kapta meg a party hátizsákok közötti mozgatáshoz szükséges inventory read modelleket.");
        Assert(guestFrame.Session.Party.All(character => character.CharacterSheet is not null),
            "A vendég nem kapta meg a lapozható party-karakterlapokat.");
    }

    static void InventoryTransferIsAtomicAndPreservesCharges()
    {
        var party = new Party();
        var leader = CreateCharacter("InvLeader");
        party.SetLeader(leader);
        var session = new GameSession(party, leader);
        var wand = new MagicItemDefinition("MI-TEST", "Tesztpálca", MagicItemKind.Wand, ItemRarity.Magic,
            10, 5, null, MagicItemEffect.None, 0, new HashSet<string> { leader.CharacterClass.Id }, "Teszt", 1);
        Assert(leader.AddMagicItem(wand), "A tesztpálca nem került a varázstárgyslotba.");
        var revision = leader.InventoryRevision;
        var command = new InventoryTransferCommand(session.HostPlayerId, 1, leader.Id, revision,
            InventorySlotKind.MagicItem, 0, leader.Id, revision, InventorySlotKind.Backpack, 1);
        session.Submit(command);
        Assert(session.TryReadCommand(out var accepted) && accepted == command,
            "Az érvényes inventory transfer commandot elutasította a session.");
        Assert(InventoryTransferService.TryExecute(party, command, out _, out var error), error);
        Assert(leader.GetInventoryItem(InventorySlotKind.MagicItem, 0) is null &&
               leader.GetInventoryItem(InventorySlotKind.Backpack, 1)?.Id == wand.Id &&
               leader.GetInventoryItemCharges(InventorySlotKind.Backpack, 1) == wand.MaximumCharges &&
               leader.InventoryRevision == revision + 1,
            "Az atomi transfer elvesztette a tárgyat, töltetet vagy hibásan növelte a revíziót.");
    }

    static void StaleInventoryRevisionIsRejected()
    {
        var (session, leader, _) = CreateSession();
        leader.AddToBackpack(new MiscItemDefinition("I-SOURCE", "Forrás", "Teszt", 1));
        var staleRevision = leader.InventoryRevision;
        var command = new InventoryTransferCommand(session.HostPlayerId, 1, leader.Id, staleRevision,
            InventorySlotKind.Backpack, 0, leader.Id, staleRevision, InventorySlotKind.Backpack, 1);
        leader.AddToBackpack(new MiscItemDefinition("I-CHANGE", "Változás", "Teszt", 1));
        var events = CollectEvents(session);
        session.Submit(command);
        Assert(!session.TryReadCommand(out _) && events.OfType<GameCommandRejectedEvent>().Any(rejected =>
                rejected.Reason.Contains("megváltozott", StringComparison.OrdinalIgnoreCase)),
            "Az elavult inventory-revíziójú command átjutott.");
    }

    static void RemoteInventoryTransferCanCrossBackpacksOnly()
    {
        var (session, leader, companion) = CreateSession();
        var remote = session.RegisterRemotePlayer();
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
        companion.AddToBackpack(new MiscItemDefinition("I-REMOTE", "Vendégtárgy", "Teszt", 1));
        var command = new InventoryTransferCommand(remote, 1, companion.Id, companion.InventoryRevision,
            InventorySlotKind.Backpack, 0, leader.Id, leader.InventoryRevision, InventorySlotKind.Backpack, 1);
        session.Submit(command);
        Assert(session.TryReadCommand(out var accepted) && accepted == command,
            "A vendég nem mozgathatott tárgyat a saját és a host hátizsákja között.");
        var weapon = new WeaponDefinition("W-REMOTE", "Vendégfegyver", "Kard", new ValueRange(1, 4), 0, false,
            new HashSet<string> { companion.CharacterClass.Id }, "Teszt", 1);
        Assert(companion.SetInventoryItem(InventorySlotKind.Weapon, 0, weapon),
            "A vendég felszereléskorlátozási tesztje nem készíthető elő.");
        var forbidden = new InventoryTransferCommand(remote, 2, companion.Id, companion.InventoryRevision,
            InventorySlotKind.Weapon, 0, leader.Id, leader.InventoryRevision, InventorySlotKind.Backpack, 2);
        session.Submit(forbidden);
        Assert(!session.TryReadCommand(out _),
            "A vendég másik karakterhez felszerelést is mozgathatott.");
    }

    static void InventoryActionCommandsAreValidated()
    {
        var (session, leader, _) = CreateSession();
        leader.AddToBackpack(new MiscItemDefinition("I-USE", "Gyógyital", "Teszt", 1, ConsumableEffect.Heal, 5));
        var revision = leader.InventoryRevision;
        session.Submit(new UseInventoryItemCommand(session.HostPlayerId, 1, leader.Id, revision, 0));
        Assert(session.TryReadCommand(out var use) && use is UseInventoryItemCommand,
            "Az érvényes használati commandot elutasította a session.");
        session.Submit(new DropInventoryItemCommand(session.HostPlayerId, 2, leader.Id, revision,
            InventorySlotKind.Backpack, 0));
        Assert(session.TryReadCommand(out var drop) && drop is DropInventoryItemCommand,
            "Az érvényes eldobási commandot elutasította a session.");
        session.Submit(new PickUpGroundItemCommand(session.HostPlayerId, 3, leader.Id, revision,
            WorldEntityId.New(), 1, 0, 1));
        Assert(session.TryReadCommand(out var pickup) && pickup is PickUpGroundItemCommand,
            "Az érvényes pickup command alakját elutasította a session.");
    }

    static void NonConsumableUseIsRejected()
    {
        var (session, leader, _) = CreateSession();
        leader.AddToBackpack(new MiscItemDefinition("I-NOUSE", "Dísztárgy", "Teszt", 1));
        session.Submit(new UseInventoryItemCommand(session.HostPlayerId, 1, leader.Id,
            leader.InventoryRevision, 0));
        Assert(!session.TryReadCommand(out _), "A nem fogyasztható tárgy használati commandja átjutott.");
    }

    static void GroundPilePreservesChargesAndRevision()
    {
        var character = CreateCharacter("PileTest");
        var wand = new MagicItemDefinition("MI-PILE", "Földi pálca", MagicItemKind.Wand, ItemRarity.Magic,
            10, 5, null, MagicItemEffect.None, 0, new HashSet<string> { character.CharacterClass.Id }, "Teszt", 1);
        var pile = new GroundItemPile(new Position(2, 2), wand, 3);
        Assert(pile.Revision == 1 && pile.Entries.Single().Charges == 3,
            "A földi kupac nem őrizte meg a kezdeti töltetet vagy revíziót.");
        Assert(!pile.TryTake(0, 2, out _), "A kupac elfogadta az elavult revíziót.");
        Assert(pile.TryTake(0, 1, out var entry) && entry.Charges == 3 && pile.Revision == 2,
            "A revíziózott pickup elvesztette a töltetet vagy nem növelte a kupacrevíziót.");
        var maze = new Maze(7, 7);
        maze.Carve(maze.Entrance);
        maze.DropItem(maze.Entrance, wand, 3);
        var fog = new FogOfWar(7, 7, 0);
        fog.RevealFrom(maze, maze.Entrance);
        var worldPile = WorldSnapshotProjector.Create(maze, fog).GroundPiles.Single();
        Assert(worldPile.Revision == 1 && worldPile.Items.Single().Charges == 3,
            "A world snapshot nem publikálta a kupac revízióját vagy töltetszámát.");
    }

    static void CatalogFingerprintIsDeterministic()
    {
        var content = Encoding.UTF8.GetBytes("azonos katalógus\nR001;Ember");
        var first = CatalogFingerprint.Compute(content);
        var second = CatalogFingerprint.Compute(content);
        var changed = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("más katalógus"));
        Assert(first == second && first.Length == 64 && first != changed,
            "A katalógus SHA-256 fingerprint nem determinisztikus vagy nem érzékeli a változást.");
    }

    static void HandshakeValidatesProtocolAndCatalog()
    {
        var (session, _, _) = CreateSession();
        var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
        var handshake = new SessionHandshakeService(session, "1.0.0", hash);
        var wrongProtocol = handshake.Handle(new ClientHello(SessionProtocol.Version + 1, "1.0.0", hash, "Vendég"));
        var wrongCatalog = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0",
            CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("other")), "Vendég"));
        var accepted = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég"));
        Assert(!wrongProtocol.Accepted && !wrongCatalog.Accepted && accepted.Accepted &&
               accepted.PlayerId is not null && accepted.ReconnectToken?.Length == 64 &&
               accepted.AvailableCharacters?.Single().Name == "Companion",
            "A handshake verzió-/katalógusellenőrzése vagy elfogadott válasza hibás.");
    }

    static void HandshakeReconnectRestoresPlayer()
    {
        var (session, _, companion) = CreateSession();
        var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
        var handshake = new SessionHandshakeService(session, "1.0.0", hash);
        var first = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég"));
        Assert(first is { Accepted: true, PlayerId: not null, ReconnectToken: not null },
            "Az első handshake sikertelen.");
        var playerId = first.PlayerId!.Value;
        var token = first.ReconnectToken!;
        Assert(session.TryAssignRemoteControl(playerId, companion.Id, out var error), error);
        session.MarkPlayerDisconnected(playerId);
        var reconnected = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0", hash,
            "Vendég", token));
        Assert(reconnected.Accepted && reconnected.PlayerId == playerId,
            "A reconnect-token nem az eredeti PlayerId-t állította vissza.");
    }

    static void ProtocolCodecRoundTripsCommand()
    {
        var command = new BattleActionCommand(PlayerId.New(), 7, CharacterId.New(), BattleId.New(), 3,
            BattleActionKind.CastSpell, "SP-TEST", 1, new Position(4, 5));
        var restored = CoopProtocolJson.Decode(CoopProtocolJson.Encode(command));
        Assert(restored is BattleActionCommand decoded && decoded == command,
            "A JSON wire codec megváltoztatta a battle commandot.");
        var strafe = new MoveCharacterCommand(PlayerId.New(), 8, CharacterId.New(), Direction.Left,
            PreserveFormationFacing: true);
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(strafe)) is MoveCharacterCommand decodedStrafe &&
               decodedStrafe == strafe && decodedStrafe.PreserveFormationFacing,
            "A JSON wire codec elvesztette a Shift+nyíl nézésiirány-megőrzését.");
        var keyOwnerId = CharacterId.New();
        var characterAction = new CharacterActionCommand(PlayerId.New(), 8, CharacterId.New(),
            CharacterAction.CloseOrLockDoor, new Position(7, 9), UseKey: true,
            KeyOwnerCharacterId: keyOwnerId);
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(characterAction)) is CharacterActionCommand decodedAction &&
               decodedAction == characterAction && decodedAction.UseKey == true &&
               decodedAction.KeyOwnerCharacterId == keyOwnerId,
            "A JSON wire codec megváltoztatta a karakterhez kötött akciót.");
        var attackOrder = new LeaderActionCommand(PlayerId.New(), 9, CharacterId.New(),
            LeaderAction.ToggleAttackMode);
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(attackOrder)) is LeaderActionCommand decodedOrder &&
               decodedOrder == attackOrder,
            "A JSON wire codec megváltoztatta a Támadás leader-parancsot.");
        var sale = new InnSaleCommand(PlayerId.New(), 10, CharacterId.New(), 4, 7, 2);
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(sale)) is InnSaleCommand decodedSale &&
               decodedSale == sale, "A JSON wire codec megváltoztatta a fogadói eladást.");
        var windowVisibility = new SetPlayerWindowVisibilityCommand(PlayerId.New(), 12, CharacterId.New(),
            PlayerWindowKind.Inventory, Guid.NewGuid(), true);
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(windowVisibility)) is
                   SetPlayerWindowVisibilityCommand decodedWindow && decodedWindow == windowVisibility,
            "A JSON wire codec megváltoztatta a személyes ablak láthatósági parancsát.");
        var distribution = new DistributeInventoryStackCommand(PlayerId.New(), 12, CharacterId.New(), 4, 2);
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(distribution)) is
                   DistributeInventoryStackCommand decodedDistribution && decodedDistribution == distribution,
            "A JSON wire codec megváltoztatta az inventory-szétosztási parancsot.");
        var followerTransfer = new GiveFollowerStackCommand(PlayerId.New(), 13, CharacterId.New(), 4, 2,
            CharacterId.New(), 7);
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(followerTransfer)) is
                   GiveFollowerStackCommand decodedFollowerTransfer && decodedFollowerTransfer == followerTransfer,
            "A JSON wire codec megváltoztatta a követőnek átadási parancsot.");
        var imageAcknowledgement = new AcknowledgeLevelImageCommand(PlayerId.New(), 13, CharacterId.New(),
            Guid.NewGuid());
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(imageAcknowledgement)) is
                   AcknowledgeLevelImageCommand decodedImageAcknowledgement &&
               decodedImageAcknowledgement == imageAcknowledgement,
            "A JSON wire codec megváltoztatta a pályakép-nyugtázást.");
        var characterState = new CharacterStateSync(PlayerId.New(), CharacterId.New(), "character-json",
            CharacterSyncReason.CharacterDied);
        Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(characterState)) is CharacterStateSync decodedState &&
               decodedState == characterState, "A JSON wire codec megváltoztatta a karakter-visszaszinkronizálást.");
        var rejected = false;
        try
        {
            CoopProtocolJson.Decode("{\"Type\":\"command.unknown\",\"Payload\":{}}");
        }
        catch (JsonException)
        {
            rejected = true;
        }
        Assert(rejected, "A codec elfogadott egy nem allowlistelt üzenettípust.");
    }

    static void HostGatewayBindsAuthenticatedPlayer()
    {
        var (session, _, companion) = CreateSession();
        var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
        var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
            new SessionReplicationPublisher());
        var helloMessages = gateway.HandleIncoming("connection-1", CoopProtocolJson.Encode(
            new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég")));
        var hello = (ServerHello)CoopProtocolJson.Decode(helloMessages.Single().WireMessage);
        Assert(hello is { Accepted: true, PlayerId: { } }, "A gateway handshake sikertelen.");
        var playerId = hello.PlayerId!.Value;

        var assignmentMessages = gateway.HandleIncoming("connection-1", CoopProtocolJson.Encode(
            new CharacterControlRequest(playerId, companion.Id)));
        var assignment = (CharacterControlResult)CoopProtocolJson.Decode(assignmentMessages.Single().WireMessage);
        Assert(assignment.Accepted && session.IsHumanControlled(companion.Id),
            "A gateway nem adta át a kiválasztott NPC vezérlését.");

        var impostor = new MoveCharacterCommand(PlayerId.New(), 1, companion.Id, Direction.Right);
        var rejectionMessages = gateway.HandleIncoming("connection-1", CoopProtocolJson.Encode(impostor));
        var rejection = (CoopProtocolError)CoopProtocolJson.Decode(rejectionMessages.Single().WireMessage);
        Assert(rejection.Code == "sender-mismatch" && !session.TryReadCommand(out _),
            "A gateway elfogadta a kapcsolattól eltérő PlayerId-jú commandot.");
    }

    static void HostGatewayRunsConnectionLifecycle()
    {
        var (session, leader, companion) = CreateSession();
        var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
        var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
            new SessionReplicationPublisher());
        var helloMessage = gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(
            new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég"))).Single();
        var playerId = ((ServerHello)CoopProtocolJson.Decode(helloMessage.WireMessage)).PlayerId!.Value;
        gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(
            new CharacterControlRequest(playerId, companion.Id)));

        var move = new MoveCharacterCommand(playerId, 1, companion.Id, Direction.Left);
        Assert(gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(move)).Count == 0 &&
               session.TryReadCommand(out var accepted) && accepted == move,
            "A hitelesített gateway-command nem jutott el a session queue-ba.");

        var maze = new Maze(7, 7);
        maze.Carve(maze.Entrance);
        var fog = new FogOfWar(7, 7, 0);
        fog.RevealFrom(maze, maze.Entrance);
        var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Gateway pálya",
            new Dictionary<CharacterId, Position>
            {
                [leader.Id] = maze.Entrance,
                [companion.Id] = new Position(3, 2)
            }, World: WorldSnapshotProjector.Create(maze, fog)));
        var replication = gateway.CreateReplicationMessages(snapshot);
        var frame = (SessionReplicationFrame)CoopProtocolJson.Decode(replication.Single().WireMessage);
        Assert(frame.Kind == SessionReplicationFrameKind.FullSnapshot && frame.RecipientPlayerId == playerId,
            "A gateway nem a csatlakozott játékosnak készítette a replikációs frame-et.");
        Assert(gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(
            new SnapshotAck(playerId, frame.Session.SnapshotSequence))).Count == 0,
            "A gateway nem fogadta el a snapshot ACK-ot.");

        gateway.Disconnect("connection-2");
        Assert(!session.IsHumanControlled(companion.Id) && gateway.CreateReplicationMessages(snapshot).Count == 0,
            "Disconnect után nem állt vissza az NPC-vezérlés vagy megmaradt a címzett kapcsolat.");
    }

    static void GatewayAndSimulationDoNotDeadlock()
    {
        var (session, _, companion) = CreateSession();
        var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
        var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
            new SessionReplicationPublisher());
        var hello = (ServerHello)CoopProtocolJson.Decode(gateway.HandleIncoming("deadlock-connection",
            CoopProtocolJson.Encode(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég")))
            .Single().WireMessage);
        var playerId = hello.PlayerId!.Value;
        gateway.HandleIncoming("deadlock-connection",
            CoopProtocolJson.Encode(new CharacterControlRequest(playerId, companion.Id)));

        var simulation = Task.Run(() =>
        {
            for (var index = 1; index <= 200; index++)
            {
                gateway.HandleIncoming("deadlock-connection", CoopProtocolJson.Encode(
                    new MoveCharacterCommand(playerId, index, companion.Id, Direction.Right)));
                session.TryReadCommand(out _);
                gateway.HandleIncoming("deadlock-connection", CoopProtocolJson.Encode(
                    new MoveCharacterCommand(playerId, index, companion.Id, Direction.Left)));
                session.TryReadCommand(out _);
            }
        });
        var lifecycle = Task.Run(() =>
        {
            for (var index = 0; index < 200; index++)
                gateway.HandleIncoming("deadlock-connection",
                    CoopProtocolJson.Encode(new CharacterControlRequest(playerId, companion.Id)));
        });
        Assert(Task.WaitAll([simulation, lifecycle], TimeSpan.FromSeconds(5)),
            "A session-event és a gateway lifecycle egymás zárolására várt.");
    }

    static async Task SignalRServerStartsAndStops()
    {
        var (session, _, _) = CreateSession();
        var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
        var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
            new SessionReplicationPublisher());
        await using var server = await CoopSignalRServer.StartAsync(gateway, "http://127.0.0.1:0");
    }

    static async Task SignalRClientRunsLanProtocolFlow()
    {
        var (session, leader, companion) = CreateSession();
        var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
        var publisher = new SessionReplicationPublisher();
        var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash), publisher);
        await using var server = await CoopSignalRServer.StartAsync(gateway, "http://127.0.0.1:0");
        var address = server.Addresses.Single();
        Assert(!address.EndsWith(":0", StringComparison.Ordinal),
            "A Kestrel nem publikálta a dinamikusan választott portot.");

        await using var client = new CoopSignalRClient(address, "1.0.0", hash, "LAN vendég");
        var receivedSnapshot = new TaskCompletionSource<SessionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.SnapshotChanged += snapshot => receivedSnapshot.TrySetResult(snapshot);
        var protocolErrors = new List<CoopProtocolError>();
        client.ProtocolErrorReceived += protocolErrors.Add;
        var commandRejected = new TaskCompletionSource<GameCommandRejectedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.CommandRejected += rejected => commandRejected.TrySetResult(rejected);
        var hello = await client.ConnectAsync();
        Assert(hello is { Accepted: true, PlayerId: { } } && client.State == CoopClientConnectionState.Connected,
            "A valódi SignalR kliens handshake-je sikertelen.");

        var control = await client.RequestCharacterControlAsync(companion.Id);
        Assert(control.Accepted && session.IsHumanControlled(companion.Id),
            "A SignalR kliens nem tudta átvenni az NPC irányítását.");

        var maze = new Maze(7, 7);
        maze.Carve(maze.Entrance);
        var fog = new FogOfWar(7, 7, 0);
        fog.RevealFrom(maze, maze.Entrance);
        var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "SignalR pálya",
            new Dictionary<CharacterId, Position>
            {
                [leader.Id] = maze.Entrance,
                [companion.Id] = new Position(3, 2)
            }, World: WorldSnapshotProjector.Create(maze, fog)));
        await server.PublishSnapshotAsync(snapshot);
        var applied = await receivedSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert(applied.SnapshotSequence == snapshot.SnapshotSequence && client.CurrentSnapshot?.World is not null,
            "A SignalR kliens nem alkalmazta a host teljes snapshotját.");

        var move = new MoveCharacterCommand(client.PlayerId!.Value, client.NextCommandId(), companion.Id,
            Direction.Right);
        await client.SendCommandAsync(move);
        GameCommand? accepted = null;
        for (var attempt = 0; attempt < 100 && accepted is null; attempt++)
        {
            if (session.TryReadCommand(out var queued)) accepted = queued;
            else await Task.Delay(10);
        }
        Assert(accepted == move,
            "A SignalR kliens commandja nem jutott el a host session queue-jáig.");
        await client.SendCommandAsync(move);
        var duplicateAccepted = false;
        for (var attempt = 0; attempt < 100 && !commandRejected.Task.IsCompleted; attempt++)
        {
            duplicateAccepted |= session.TryReadCommand(out _);
            if (!commandRejected.Task.IsCompleted) await Task.Delay(10);
        }
        Assert(!duplicateAccepted, "A host session elfogadta a duplikált hálózati commandot.");
        var rejectionSnapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "SignalR pálya",
            new Dictionary<CharacterId, Position>
            {
                [leader.Id] = maze.Entrance,
                [companion.Id] = new Position(3, 2)
            }, World: WorldSnapshotProjector.Create(maze, fog)));
        await server.PublishSnapshotAsync(rejectionSnapshot);
        Assert((await commandRejected.Task.WaitAsync(TimeSpan.FromSeconds(5))).CommandId == move.CommandId,
            "A szimulációs szál command-elutasítása nem jutott vissza a SignalR klienshez.");
        Assert(protocolErrors.Count == 0, "A hibamentes SignalR folyamat közben protokollhiba érkezett.");
    }

    static async Task InMemoryTransportRunsProtocolFlow()
    {
        var (session, leader, companion) = CreateSession();
        var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
        var handshake = new SessionHandshakeService(session, "1.0.0", hash);
        var (host, client) = InMemoryCoopTransport.CreatePair();

        await client.SendAsync(CoopProtocolJson.Encode(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég")));
        var hello = (ClientHello)CoopProtocolJson.Decode(await host.ReceiveAsync());
        var serverHello = handshake.Handle(hello);
        await host.SendAsync(CoopProtocolJson.Encode(serverHello));
        var accepted = (ServerHello)CoopProtocolJson.Decode(await client.ReceiveAsync());
        Assert(accepted is { Accepted: true, PlayerId: { } }, "Az in-memory handshake sikertelen.");
        var remote = accepted.PlayerId!.Value;
        Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);

        var move = new MoveCharacterCommand(remote, 1, companion.Id, Direction.Right);
        await client.SendAsync(CoopProtocolJson.Encode(move));
        var decodedMove = (MoveCharacterCommand)CoopProtocolJson.Decode(await host.ReceiveAsync());
        session.Submit(decodedMove);
        Assert(session.TryReadCommand(out var acceptedCommand) && acceptedCommand == move,
            "Az in-memory transporton érkezett commandot nem fogadta el a session.");

        var maze = new Maze(7, 7);
        maze.Carve(maze.Entrance);
        var fog = new FogOfWar(7, 7, 0);
        fog.RevealFrom(maze, maze.Entrance);
        var positions = new Dictionary<CharacterId, Position>
        {
            [leader.Id] = maze.Entrance,
            [companion.Id] = new Position(3, 2)
        };
        var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Wire pálya", positions,
            World: WorldSnapshotProjector.Create(maze, fog)));
        var publisher = new SessionReplicationPublisher();
        await host.SendAsync(CoopProtocolJson.Encode(publisher.CreateFrame(remote, snapshot)));
        var frame = (SessionReplicationFrame)CoopProtocolJson.Decode(await client.ReceiveAsync());
        Assert(frame.Kind == SessionReplicationFrameKind.FullSnapshot && frame.Session.World is not null,
            "Az első replikációs frame nem jutott át az in-memory transporton.");
        await client.SendAsync(CoopProtocolJson.Encode(new SnapshotAck(remote, frame.Session.SnapshotSequence)));
        var ack = (SnapshotAck)CoopProtocolJson.Decode(await host.ReceiveAsync());
        Assert(publisher.TryAcknowledge(ack.PlayerId, ack.SnapshotSequence, out var ackError), ackError);
    }

    static (GameSession Session, LiveCharacter Leader, LiveCharacter Companion) CreateSession()
    {
        var party = new Party();
        var leader = CreateCharacter("Leader");
        var companion = CreateCharacter("Companion");
        party.SetLeader(leader);
        party.Add(companion);
        return (new GameSession(party, leader), leader, companion);
    }

    static LiveCharacter CreateCharacter(string name, int vitality = 20,
        string characterClassId = CharacterClassIds.Harcos)
    {
        var abilities = new PrimaryAbilities(5, 5, 5, 5);
        var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
        var characterClass = new CharacterClassDefinition(characterClassId, characterClassId, PrimaryAbilities.Zero, false, 1.0);
        return new LiveCharacter(name, race, characterClass, abilities, vitality, 0, 1, 0);
    }
}
