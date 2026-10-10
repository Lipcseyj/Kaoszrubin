using System.Reflection;
using System.Runtime.CompilerServices;
using KaoszRubin.Infrastructure.Quests;

internal static partial class Program
{
    static void CampaignBossRoomsAlwaysGenerateOnce()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(CampaignBosses.All.Count == 12 && CampaignBosses.All[0] is { Level: 9, EnemyId: MonsterIds.OrkTörzsfő } &&
               CampaignBosses.ForLevel(14)!.EnemyId == MonsterIds.ŐsiHidra &&
               !data.GetEnemy(MonsterIds.OrkSámán).IsBoss && !data.GetEnemy(MonsterIds.Hidra).IsBoss,
            "A törzsfő és az ősi hidra boss-szerepe hibás.");
        foreach (var guardian in CampaignBosses.All)
        {
            var config = MazeLevelConfigurations.Get(guardian.Level);
            var quest = data.Quests.Get(guardian.QuestId);
            Assert(quest.Objective is QuestObjective.KillEnemy { Count: 1 } kill && kill.Enemy.Id == guardian.EnemyId &&
                   quest.CompletionDialogue is not null &&
                   data.NpcEncounters.Any(npc => npc.MazeLevel == guardian.Level && npc.AreaId == "AREA_1" &&
                       quest.MatchesEncounter(npc.Id)) && !config.QuestDoorRequirements.ContainsKey(guardian.RoomId),
                $"Hiányzó, későn elérhető vagy kötelező bossmegbízás: {guardian.Level}.");
            var narrative = StoryNarratives.BossNarratives[guardian.EnemyId];
            Assert(narrative.Speech.Count >= 3 && narrative.Speech.Any(text => text.Contains("Zephyriel") ||
                       text.Contains("aranykulcs")) &&
                   narrative.Speech.Any(text => text.Contains(data.GetEnemy(guardian.EnemyId).Name.Split(',')[0].Split(' ')[0])),
                $"A boss személyes története hiányos: {guardian.EnemyId}.");
            for (var seed = 1; seed <= 8; seed++)
            {
                var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
                foreach (var (name, value) in new (string, object)[]
                {
                    ("_random", new Random(guardian.Level * 1000 + seed)), ("_gameData", data),
                    ("_difficultyLevel", guardian.Level)
                })
                    typeof(Game).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, value);
                var level = (DungeonLevel)typeof(Game).GetMethod("GenerateDungeonLevel",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, [config, null])!;
                QuestRoomEnemyPlacement.Place(level, data, config.QuestRoomEnemyEncounters,
                    new Random(seed), new(guardian.Level, data.EnemyMagicWeaponRules, data.Weapons));
                var allBosses = level.Areas.SelectMany(area => area.Maze.Enemies).Where(enemy => enemy.Definition.IsBoss).ToArray();
                var room = level.ExitArea.Maze.GetRoomByContentId(guardian.RoomId)!;
                var guards = level.ExitArea.Maze.Enemies.Where(enemy => enemy.GroupId == $"QUEST:{guardian.RoomId}").ToArray();
                Assert(allBosses.Length == 1 && allBosses[0].Definition.Id == guardian.EnemyId &&
                       room is { Purpose: RoomPurpose.Boss or RoomPurpose.Quest } && room.Contains(allBosses[0].Position) &&
                       guards.Length == config.QuestRoomEnemyEncounters.Where(enc => enc.RoomId == guardian.RoomId).Sum(enc => enc.Count) &&
                       guards.All(enemy => room.Contains(enemy.Position)) &&
                       room.InteriorPositions().Count(position => level.ExitArea.Maze.IsWalkable(position) &&
                           level.ExitArea.Maze.GetObjectAt(position) is null &&
                           level.ExitArea.Maze.Doors.All(door => BossDistance(position, door.Position) > 1)) >= 6 &&
                       level.Areas.All(area => area.Maze.CheckFullAccessibility().IsFullyAccessible),
                    $"A boss, őrség, partihely vagy bejárhatóság hibás: {guardian.Level}, seed {seed}.");
            }
        }
        foreach (var mini in MonsterIds.MiniBosses)
            Assert(StoryNarratives.BossNarratives[mini].Speech.Count >= 3, $"Hiányzó miniboss-történet: {mini}.");
    }

    static void CampaignBossFormationFacesClosedEntry()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var maze = new Maze(21, 17);
        var guardian = CampaignBosses.ForLevel(9)!;
        var room = new Room(new(4, 3), 12, 10, RoomPurpose.Boss, guardian.RoomId);
        maze.AddRoom(room);
        foreach (var position in room.InteriorPositions()) maze.Carve(position);
        maze.Carve(new(3, 8));
        maze.PlaceDoor(new(3, 8), DoorState.Closed);
        var config = MazeLevelConfigurations.Get(9);
        QuestRoomEnemyPlacement.Place(maze, data, config.QuestRoomEnemyEncounters, new Random(41),
            new(9, data.EnemyMagicWeaponRules, data.Weapons));
        var boss = maze.Enemies.Single(enemy => enemy.Definition.IsBoss);
        var melee = maze.Enemies.Where(enemy => enemy.Definition.Id == MonsterIds.OrkTestőr).ToArray();
        var casters = maze.Enemies.Where(enemy => enemy.Definition.SpellcasterProfile is not null).ToArray();
        var archers = maze.Enemies.Where(enemy => enemy.Definition.Id == MonsterIds.OrkÍjász).ToArray();
        Assert(boss.Position == new Position(10, 8) &&
               melee.Average(enemy => enemy.Position.X) < boss.Position.X &&
               casters.All(enemy => enemy.Position.X > boss.Position.X) &&
               archers.All(enemy => Math.Abs(enemy.Position.Y - boss.Position.Y) >= 4) &&
               maze.Enemies.Select(enemy => enemy.Position).Distinct().Count() == maze.Enemies.Count,
            "A zárt bejárattal szemben a boss/őr/íjász/varázsló hadrend hibás.");
        Assert(room.InteriorPositions().Count(position => BossDistance(position, new(3, 8)) is > 1 and <= 3 &&
            maze.GetEnemyAt(position) is null) >= 6, "Nincs hely a parti belépéséhez.");
    }

    static void CampaignBossQuestRecognizesEarlierKeyOnly()
    {
        var boss = new EnemyDefinition("TEST-BOSS", "Név szerinti őrző", "B", 5, 100, new(0, 0), 5, 10, 2, [],
            IsBoss: true, Rank: EnemyRank.Boss);
        var definition = QuestTestFixture.Define(new QuestObjective.KillEnemy(boss, 1));
        var fixture = new QuestTestFixture(definition);
        fixture.DefeatedBossIds.Add(boss.Id);
        var quest = fixture.Manager.Activate(definition.Id);
        Assert(quest.IsReadyToTurnIn && quest.Progress == 1, "A felvétel előtti bosskulcs nem teljesítette a megbízást.");
        var result = quest.Complete();
        Assert(result is not null && quest.IsCompleted && fixture.StoredRewards.Count == 1,
            "A korábban legyőzött boss jutalma nem egyszeri.");
        var normal = boss with { IsBoss = false, Rank = EnemyRank.Normal };
        fixture = new QuestTestFixture(QuestTestFixture.Define(new QuestObjective.KillEnemy(normal, 1)));
        fixture.DefeatedBossIds.Add(normal.Id);
        Assert(fixture.Manager.Activate(definition.Id).Progress == 0, "Egy közönséges ölési küldetés utólag teljesült.");
        var escort = new QuestObjective.QuestFollowerRequirement(QuestNpcId.WanderingHerbalist);
        fixture = new QuestTestFixture(QuestTestFixture.Define(new QuestObjective.KillEnemy(boss, 1, escort)));
        fixture.DefeatedBossIds.Add(boss.Id);
        Assert(fixture.Manager.Activate(definition.Id).Progress == 0, "A követőfeltételt a régi kulcs megkerülte.");
    }

    static void CampaignBossKeysGateTheirOwnExit()
    {
        foreach (var boss in CampaignBosses.All)
        {
            Assert(!CampaignBosses.CanLeave(boss.Level, []) &&
                   !CampaignBosses.CanLeave(boss.Level, CampaignBosses.All.Where(other => other != boss).Select(other => other.EnemyId)) &&
                   CampaignBosses.CanLeave(boss.Level, [boss.EnemyId.ToLowerInvariant()]),
                $"Az átjáró nem a saját kulcsát ellenőrzi: {boss.Level}.");
        }
        Assert(CampaignBosses.CanLeave(8, []) && CampaignBosses.CanLeave(25, []),
            "Boss nélküli pálya kapuja egy őrző kulcsát kéri.");
    }

    static void CampaignBossMigrationRetainsKeysAndRemovesDuplicates()
    {
        var maze = new MazeSaveData
        {
            Enemies = [new(new(3, 3), "E043", 1740, BossHitPointBonusPercent: 20),
                       new(new(4, 3), "E035", 240, BossHitPointBonusPercent: 20),
                       new(new(5, 3), "E041", 360, BossHitPointBonusPercent: 20)],
            Corpses = [new(new(6, 3), "régi hidra", null, "E043")]
        };
        var state = new GameSaveData
        {
            Version = 43, MazeLevel = 14, Maze = maze, CollectedBossKeyIds = ["E035", "E043", "E043"],
            SeenBossIds = ["E035", "E043", "E041"]
        };
        GameSaveFormat.MigrateToCurrent(state);
        Assert(state.Version == 44 && ReferenceEquals(state.Maze, maze) &&
               state.CollectedBossKeyIds.Contains(MonsterIds.OrkTörzsfő) &&
               state.CollectedBossKeyIds.Contains(MonsterIds.ŐsiHidra) &&
               state.CollectedBossKeyIds.Distinct().Count() == state.CollectedBossKeyIds.Count &&
               state.SeenBossIds.SequenceEqual(["E041"]) &&
               maze.Enemies[0] is { DefinitionId: MonsterIds.ŐsiHidra, CurrentHitPoints: 1920, BossHitPointBonusPercent: 20 } &&
               maze.Enemies[1] is { DefinitionId: MonsterIds.OrkSámán, CurrentHitPoints: 200, BossHitPointBonusPercent: 0 } &&
               maze.Enemies[2] is { DefinitionId: MonsterIds.FagyóriásHarcos, CurrentHitPoints: 300, BossHitPointBonusPercent: 0 } &&
               maze.Corpses[0].EnemyDefinitionId == MonsterIds.ŐsiHidra,
            "A régi boss, HP, kulcs, portré vagy tetem migrációja hibás.");
        GameSaveFormat.MigrateToCurrent(state);
        Assert(maze.Enemies[0].CurrentHitPoints == 1920, "Az ősi hidra ismét megkapta az erősítést.");
        var current = new GameSaveData { Version = 43, MazeLevel = 9,
            Maze = new() { Enemies = [new(new(3, 3), MonsterIds.OrkTörzsfő, 360)] } };
        GameSaveFormat.MigrateToCurrent(current);
        Assert(!CampaignBosses.CanLeave(9, current.CollectedBossKeyIds), "Az élő régi őrző kulcsát ingyen megkaptuk.");
        var missing = new GameSaveData { Version = 43, MazeLevel = 9 };
        GameSaveFormat.MigrateToCurrent(missing);
        Assert(CampaignBosses.CanLeave(9, missing.CollectedBossKeyIds), "A boss nélkül generált régi pálya csapdába ejt.");
        var suspended = new GameSaveData { Version = 43, MazeLevel = 5, LocationKind = AdventureLocationKind.Quest,
            SuspendedCampaign = new() { Version = 43, MazeLevel = 22 } };
        GameSaveFormat.MigrateToCurrent(suspended);
        Assert(suspended.CollectedBossKeyIds.ToHashSet().SetEquals(suspended.SuspendedCampaign!.CollectedBossKeyIds) &&
               suspended.CollectedBossKeyIds.Contains(MonsterIds.Drakolich),
            "A külön questhelyszín elvesztette a felfüggesztett kampány pecsétjeit.");
    }

    static void CampaignBossExpeditionsNeverRecreateGuardians()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var maze = new Maze(19, 17);
        for (var y = 1; y < 16; y++)
        for (var x = 1; x < 18; x++) maze.Carve(new(x, y));
        var room = new Room(new(9, 4), 7, 8, RoomPurpose.Boss, CampaignBosses.ForLevel(9)!.RoomId);
        maze.AddRoom(room);
        var boss = new ConfiguredEnemy(new(12, 8), data.GetEnemy(MonsterIds.OrkTörzsfő), new Random(1));
        maze.AddEnemy(boss);
        maze.AddCorpse(new MonsterCorpse(new(11, 8), "testőr", MonsterIds.OrkTestőr));
        maze.AddCorpse(new MonsterCorpse(new(5, 8), "ork", MonsterIds.Ork));
        var templates = new List<ExpeditionEnemyTemplate>();
        DungeonExpeditionCoordinator.CaptureExpeditionEnemyTemplates(templates, maze, data);
        Assert(templates.Count == 1 && templates[0].DefinitionId == MonsterIds.Ork,
            "Az expedíció kulcsőrzőt vagy különleges szobaőrséget akar újrateremteni.");
        var coordinator = new DungeonExpeditionCoordinator(data, new Random(9));
        coordinator.ReplenishExpeditionEnemies(templates, maze);
        Assert(maze.Enemies.Contains(boss) && maze.Enemies.Count(enemy => enemy.Definition.IsBoss) == 1 &&
               maze.Enemies.Where(enemy => enemy != boss).All(enemy => !room.Contains(enemy.Position)),
            "Az élő boss elveszett, megkettőződött vagy normál utánpótlás került a termébe.");
        maze.RemoveEnemy(boss);
        coordinator.ReplenishExpeditionEnemies(templates, maze);
        Assert(maze.Enemies.All(enemy => !enemy.Definition.IsBoss), "A halott kulcsőrző újrateremtődött.");
    }

    private static int BossDistance(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
}

