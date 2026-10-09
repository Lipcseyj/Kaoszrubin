using System.Reflection;
using System.Runtime.CompilerServices;
using KaoszRubin.Application.Quests;
using KaoszRubin.Infrastructure.Quests;

internal static partial class Program
{
    private static GameDataCatalog ForestSupplyData() => CsvGameDataLoader.Load(
        Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));

    static void ForestSuppliesAndInnsGenerateInBuildings()
    {
        var data = ForestSupplyData();
        var source = new FileForestLevelGraphSource(Path.Combine(AppContext.BaseDirectory, "ForestLevelGraphs"));
        foreach (var number in new[] { 6, 13 })
        {
            var configuration = MazeLevelConfigurations.Get(number);
            var fallback = ((ForestMazeLayoutConfiguration)configuration.Layout!).ExplicitGraph!;
            Assert(source.TryLoad(number, out var document, out _, out _) &&
                ForestConfigurationJson.Serialize(number, document!.Graph) == ForestConfigurationJson.Serialize(number, fallback),
                $"A(z) {number}. erdő JSON-ja és tartaléka eltér.");
            foreach (var seed in Enumerable.Range(1, 10))
            {
                var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
                void Set(string field, object value) => typeof(Game).GetField(field,
                    BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, value);
                Set("_random", new Random(number * 1000 + seed));
                Set("_gameData", data);
                Set("_difficultyLevel", number);
                object? Invoke(string method, params object[] args)
                {
                    try { return typeof(Game).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, args); }
                    catch (TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
                }
                var level = (DungeonLevel)Invoke("GenerateDungeonLevel", configuration,
                    ForestLevelGraphOverrideBridge.Apply(configuration, source)!)!;
                Set("_dungeonLevel", level);
                Invoke("PlaceSpecialRoomContent", configuration);
                foreach (var area in level.Areas)
                {
                    Set("_maze", area.Maze);
                    Invoke("PlaceTraps", configuration, 4, Array.Empty<string>());
                    Assert(area.Maze.CheckFullAccessibility().IsFullyAccessible,
                        $"Nem bejárható: {number}/{area.Id}, seed {seed}.");
                    foreach (var interior in area.Maze.InnBuildingInteriors())
                        Assert(!area.Maze.Enemies.Any(enemy => interior.Contains(enemy.Position)) &&
                            !area.Maze.Traps.Any(trap => interior.Contains(trap.Position)),
                            $"Ellenség vagy csapda a fogadóban: {number}/{area.Id}, seed {seed}.");
                }
                Assert(level.Areas.Sum(area => area.Maze.ForestInns.Count) == (number == 6 ? 1 : 2),
                    "Hiányzó vagy ismételt erdei fogadó.");
                foreach (var inn in configuration.ForestInns)
                {
                    var maze = level.GetArea(inn.AreaId).Maze;
                    var marker = maze.ForestInns.Single(value => value.RoomId == inn.RoomId);
                    var room = maze.GetRoomByContentId(inn.RoomId)!;
                    Assert(room.Purpose == RoomPurpose.Inn && room.BuildingId is not null && room.Contains(marker.Position) &&
                        maze.Rooms.Where(value => value.BuildingId == room.BuildingId).All(value => value.Purpose == RoomPurpose.Inn),
                        "A fogadó nem külön épületet foglal el.");
                }
                foreach (var (roomId, chestId) in configuration.QuestChestPlacements)
                {
                    var maze = level.Areas.Single(area => area.Maze.GetRoomByContentId(roomId) is not null).Maze;
                    var room = maze.GetRoomByContentId(roomId)!;
                    var chest = maze.TreasureChests.Single(value => value.Definition?.Id == chestId);
                    Assert(room.Purpose == RoomPurpose.Quest && room.BuildingId is not null && room.Contains(chest.Position),
                        "Az ellátmányláda nem a célzott épületben van.");
                    foreach (var encounter in configuration.QuestRoomEnemyEncounters.Where(value => value.RoomId == roomId))
                        Assert(maze.Enemies.Count(enemy => enemy.Definition.Id == encounter.EnemyId &&
                            enemy.GroupId == $"QUEST:{roomId}" && enemy.GroupRole == encounter.Role &&
                            room.Contains(enemy.Position)) == encounter.Count,
                            $"Hiányzó ládaőrség: {number}/{roomId}/{encounter.EnemyId}, seed {seed}.");
                    Assert(maze.Enemies.Single(enemy => enemy.Definition.Id == chest.Definition!.GuardianEnemyId)
                        .Definition.Rank == EnemyRank.MiniBoss, "A ládát nem miniboss védi.");
                }
            }
        }
    }

    static void ForestSupplyGuardiansGateLootAndDoNotRespawn()
    {
        var data = ForestSupplyData();
        foreach (var number in new[] { 6, 13 })
        foreach (var (roomId, chestId) in MazeLevelConfigurations.Get(number).QuestChestPlacements)
        {
            var definition = data.GetQuestChest(chestId);
            var guardian = data.GetEnemy(definition.GuardianEnemyId!);
            Assert(guardian.Rank == EnemyRank.MiniBoss && !guardian.IsBoss &&
                !MonsterIds.Bosses.Contains(guardian.Id), "A miniboss aranykulcsos boss lett.");
            Assert(definition.Items.Any(item => item.Item.Id == "T001") &&
                definition.Items.Any(item => item.Item.Id == "T002") &&
                definition.Items.Any(item => item.Item.Id is "T011" or "T012") &&
                definition.Items.Any(item => item.Item.Id is "T014" or "T015"), "Hiányos ellátmány.");
            var actualQuest = data.Quests.All.Single(quest =>
                quest.Objective is QuestObjective.OpenQuestChest objective && objective.ChestId == chestId);
            var testQuest = QuestTestFixture.Define(actualQuest.Objective, actualQuest.Id);
            var fixture = new QuestTestFixture(testQuest);
            fixture.Manager.Activate(testQuest.Id);
            var service = new QuestChestService(fixture.Manager);
            var chest = new TreasureChest(new(3, 3), definition);
            var gold = 0;
            var blocked = service.Collect(chest, _ => throw new Exception("Zárt ládából tárgy."),
                _ => throw new Exception("Zárt ládából arany."), id => id == guardian.Id);
            Assert(blocked.BlockingGuardianId == guardian.Id && !chest.IsOpened &&
                fixture.Manager.GetQuest(testQuest.Id).Progress == 0, "Élő őr mellett a láda kinyílt.");
            var quota = 3;
            var opened = service.Collect(chest, _ => quota-- > 0, amount => gold += amount, _ => false);
            Assert(opened.FirstOpening && opened.ItemCount == 3 && gold == definition.Gold &&
                opened.RemainingCount == definition.Items.Sum(item => item.Quantity) - 3 &&
                fixture.Manager.GetQuest(testQuest.Id).IsReadyToTurnIn, "A legyőzött őr nem oldotta fel a készletet.");
            var again = service.Collect(chest, _ => false, _ => throw new Exception("Ismételt arany."), _ => true);
            Assert(!again.FirstOpening && again.Changes.Count == 0 && again.BlockingGuardianId is null,
                "Az egyszer kinyitott láda ismételt progresst vagy zárolást kapott.");
            var late = new QuestTestFixture(testQuest);
            late.OpenedChests.Add(chestId);
            Assert(late.Manager.Activate(testQuest.Id).IsReadyToTurnIn, "A később felvett ellátmányquest megakadt.");

            var maze = ForestInnTestMaze();
            var enemy = new ConfiguredEnemy(new(7, 7), guardian, new Random(7));
            enemy.ConfigureGroup($"QUEST:{roomId}");
            maze.AddEnemy(enemy);
            maze.AddCorpse(new MonsterCorpse(new(7, 6), guardian.Name, guardian.Id));
            var templates = new List<ExpeditionEnemyTemplate>();
            DungeonExpeditionCoordinator.CaptureExpeditionEnemyTemplates(templates, maze, data);
            Assert(templates.Count == 0, "A miniboss visszatérő expedícióban feltámadhat.");
        }
        var innMaze = ForestInnTestMaze();
        var innPosition = new Position(3, 3);
        Assert(DungeonExpeditionCoordinator.FindExpeditionSpawnPosition(innMaze, innPosition) is { } spawn &&
            !innMaze.InnBuildingInteriors().Any(room => room.Contains(spawn)), "Az expedíció fogadóba tett egy ellenfelet.");
    }

    private static Maze ForestInnTestMaze()
    {
        var maze = new Maze(11, 11);
        for (var y = 1; y < 10; y++)
        for (var x = 1; x < 10; x++) maze.Carve(new(x, y));
        maze.AddRoom(new(new(2, 2), 4, 4, RoomPurpose.Inn, "TEST_INN", RoomKind.Cabin, "BUILDING_INN"));
        maze.PlaceExit(new(9, 9));
        return maze;
    }

    static void ForestInnVisitPersistsAndReplicates()
    {
        var data = ForestSupplyData();
        var roster = new CharacterRoster();
        var leader = CreateCharacter("Erdei vezér");
        roster.Add(leader); roster.Select(leader);
        var maze = ForestInnTestMaze();
        var inn = new ForestInn(new(3, 3), "TEST_INN", "Próbafogadó");
        maze.AddForestInn(inn);
        var fog = new FogOfWar(11, 11, 0);
        var hidden = WorldSnapshotProjector.Create(maze, fog);
        Assert(hidden.ForestInns?.Count == 0, "A fogadó átlátszik a ködön.");
        fog.Restore([inn.Position], false);
        var before = WorldSnapshotProjector.Create(maze, fog);
        var successes = 0;
        Parallel.For(0, 20, _ => { if (inn.TryVisit()) Interlocked.Increment(ref successes); });
        Assert(successes == 1, "A fogadó több látogatást is engedett.");
        var after = WorldSnapshotProjector.Create(maze, fog);
        var delta = JsonSerializer.Deserialize<WorldDelta>(JsonSerializer.Serialize(
            WorldDeltaProjector.Create(1, before, 2, after)))!;
        var applied = WorldDeltaReducer.Apply(before, delta);
        Assert(applied.ForestInns!.Single().Visited &&
            applied.RevealedCells.Single(cell => cell.Position == inn.Position).ForegroundColor == ConsoleColor.DarkGray &&
            WorldDeltaProjector.Create(2, after, 3, after).IsEmpty, "A fogadó állapota elveszett a kliensben.");

        var mapper = new GameStateMapper(data, roster, leader);
        var saved = mapper.Create(13, maze, new Player(inn.Position, leader), fog,
            Direction.Right, [], false, false, false, false, null, DateTime.UtcNow,
            new Dictionary<Enemy, DateTime>(), [], []);
        saved = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(saved))!;
        var restored = mapper.Restore(saved);
        Assert(restored.Maze.ForestInns.Single() is { RoomId: "TEST_INN", Name: "Próbafogadó", Visited: true } &&
            restored.Player.Position == inn.Position && !restored.Maze.ForestInns.Single().TryVisit(),
            "A mentés visszaadta az elhasznált fogadói látogatást.");
        var old = new GameSaveData { Version = 38, MazeLevel = 13, Maze = new() { LevelName = "Meglévő erdő" } };
        GameSaveFormat.MigrateToCurrent(old);
        Assert(old.Version == GameSaveFormat.CurrentVersion && old.Maze.ForestInns.Count == 0 &&
            old.MazeLevel == 13 && old.Maze.LevelName == "Meglévő erdő", "A migráció felülírta a meglévő pályát.");
    }

    static void ForestInnEntryRequiresRegroupingAndAllowsMovement()
    {
        var maze = ForestInnTestMaze();
        var inn = new ForestInn(new(3, 3), "TEST_INN", "Próbafogadó");
        maze.AddForestInn(inn);
        var leader = new Player(new(3, 4), CreateCharacter("Vezér"));
        Assert(leader.TryMove(Direction.Up, maze), "A vezér nem tud a fogadó jelére lépni.");
        var member = new PartyMemberAvatar(new(3, 2), CreateCharacter("Társ"));
        maze.AddPartyMember(member);
        Assert(ForestInnPlacement.CanEnter(inn, leader.Position, [member], false, out _), "A közeli parti nem térhet be.");
        Assert(!ForestInnPlacement.CanEnter(inn, leader.Position, [member], true, out _) &&
            !ForestInnPlacement.CanEnter(inn, new(4, 3), [member], false, out _), "Harcban vagy más helyről is be lehet térni.");
        Assert(maze.TrySwapLeaderAndPartyMember(leader, member) &&
            maze.TryMovePartyMember(member, new(4, 3), leader.Position) &&
            maze.TryMovePartyMember(member, inn.Position, leader.Position), "A fogadó jelén megakad a partitagok mozgása.");
        member.MoveTo(new(9, 5));
        Assert(ForestInnPlacement.CanEnter(inn, inn.Position, [member], false, out _), "A nyolcmezős parti nem térhet be.");
        member.MoveTo(new(9, 6));
        Assert(!ForestInnPlacement.CanEnter(inn, inn.Position, [member], false, out _), "A szétszórt parti betérhet.");
        inn.TryVisit();
        Assert(!ForestInnPlacement.CanEnter(inn, inn.Position, [], false, out _), "A felhasznált megálló újra nyitott.");
    }

    static void ForestInnOffersSuppliesWithoutCompletingLevel()
    {
        var data = ForestSupplyData();
        foreach (var level in new[] { 6, 13 })
        {
            var roster = new CharacterRoster();
            var leader = CreateCharacter("Vezér");
            roster.Add(leader); roster.Select(leader);
            roster.Party.RecordCampaignLevelCompletion(level == 6 ? 5 : 8);
            var capacity = roster.Party.Capacity;
            for (var index = 1; index < capacity; index++)
            {
                var member = CreateCharacter($"Társ {index}");
                roster.Add(member);
                Assert(roster.Party.Add(member), "Nem fért be a tesztparti.");
            }
            var progression = JsonSerializer.Serialize(roster.Party.CampaignProgression);
            var inn = new InnController(data, roster, leader, new ConsoleRenderer(data, roster.Party),
                _ => { }, new Random(42),
                (_, _) => throw new Exception("Erdei fogadó pályateljesítési XP-t adott."),
                (_, _) => throw new Exception("Erdei fogadó szintlépést indított."), () => { });
            inn.PrepareForestStop(level, "Erdei próbafogadó");
            var snapshot = inn.CreateSnapshot()!;
            Assert(snapshot.InnName == "Erdei próbafogadó" && snapshot.MazeLevel == level &&
                snapshot.LevelCompletion is null && snapshot.PartyCount == capacity &&
                JsonSerializer.Serialize(roster.Party.CampaignProgression) == progression,
                "A pályán belüli fogadó teljesítettnek jelölte a pályát.");
            var options = snapshot.MenuOptions!;
            Assert(options.Any(option => option.Kind == InnMenuOptionKind.Rest) &&
                options.Any(option => option.Kind == InnMenuOptionKind.Feast) &&
                options.Single(option => option.Kind == InnMenuOptionKind.Leave).Label.Contains("erdei") &&
                options.All(option => option.Kind != InnMenuOptionKind.ReturnExpedition), "Hibás erdei fogadói menü.");
            var market = snapshot.Vendors.Single(vendor => vendor.Kind == InnVendorKind.Market);
            foreach (var id in new[] { "T001", "T002" })
                Assert(market.Offers.Any(offer => offer.Item.DefinitionId == id && offer.StockCount >= capacity),
                    "Az erdei fogadó nem biztosít alapellátmányt.");
            leader.AddGold(10000);
            var food = market.Offers.First(offer => offer.Item.DefinitionId == "T001");
            Assert(inn.TryPurchase(InnVendorKind.Market, food.Index, snapshot.Revision, leader, out _),
                "Nem vásárolható meg az erdei ellátmány.");
            var goldAfter = leader.Gold;
            Assert(!inn.TryPurchase(InnVendorKind.Market, food.Index, snapshot.Revision, leader, out _) &&
                leader.Gold == goldAfter && leader.Backpack.Any(item => item?.Id == "T001"),
                "Elavult fogadói parancs megismételte a vásárlást.");
        }
    }
}
