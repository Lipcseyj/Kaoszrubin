using System.Reflection;
using System.Runtime.CompilerServices;
using KaoszRubin.Infrastructure.Quests;

internal static partial class Program
{
    static void ScaledDungeonsGenerateBossRoomsAndPools()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        foreach (var (number, bossId, roomId, questId, minimum, maximum) in new[]
        {
            (15, MonsterIds.GyíkemberKirály, ScaledKingdomDungeons.ThroneRoom, QuestId.ArchivistScaledKing, 3, 4),
            (16, MonsterIds.KígyóFőpap, ScaledKingdomDungeons.HighAltar, QuestId.FugitiveSnakeHighPriest, 4, 5)
        })
        {
            var sizes = new HashSet<int>();
            for (var seed = 1; seed <= 12; seed++)
            {
                var configuration = MazeLevelConfigurations.Get(number);
                var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
                foreach (var (name, value) in new (string, object)[]
                {
                    ("_random", new Random(number * 1000 + seed)), ("_gameData", data), ("_difficultyLevel", number)
                })
                    typeof(Game).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, value);
                var level = (DungeonLevel)typeof(Game).GetMethod("GenerateDungeonLevel",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, [configuration, null])!;
                QuestRoomEnemyPlacement.Place(level, data, configuration.QuestRoomEnemyEncounters,
                    new Random(seed), new EnemyMagicWeaponContext(number, data.EnemyMagicWeaponRules, data.Weapons));
                sizes.Add(level.Areas.Count);
                var enemies = level.Areas.SelectMany(area => area.Maze.Enemies).ToArray();
                var boss = enemies.Single(enemy => enemy.Definition.Id == bossId);
                var final = level.GetArea(level.ExitAreaId);
                Assert(level.Areas.Count >= minimum && level.Areas.Count <= maximum &&
                       final.Maze.Enemies.Contains(boss) && final.Maze.GetRoomByContentId(roomId)!.Contains(boss.Position),
                    $"A hüllőboss rossz helyre került: {number}, seed {seed}.");
                Assert(boss is { BossHitPointBonusPercent: >= 10 and <= 50, GroupRole: EnemyGroupRole.Leader } &&
                       final.Maze.Enemies.Count(enemy => enemy.GroupId == boss.GroupId) >= 11 &&
                       data.Quests.Get(questId).Objective is QuestObjective.KillEnemy { RequiredCount: 1 } objective &&
                       objective.Enemy.Id == bossId,
                    $"Hiányzó bossbónusz, kíséret vagy küldetés: {number}.");
                Assert(level.Areas.All(area => area.Maze.CheckFullAccessibility().IsFullyAccessible &&
                           !area.Name.EndsWith(". terület", StringComparison.Ordinal)),
                    "A hüllődungeon nem bejárható vagy névtelen képernyőket kapott.");
                Assert(level.Areas.Sum(area => area.Maze.TreasureChests.Count) >= configuration.TreasureChestCount.Minimum,
                    "Eltűntek a nagy dungeon kincsei.");
                if (number == 15)
                {
                    var poolArea = level.Areas[1].Maze;
                    var pool = poolArea.GetRoomByContentId(ScaledKingdomDungeons.CrocodilePool)!;
                    var crocodiles = poolArea.Enemies.Where(enemy => pool.Contains(enemy.Position)).ToArray();
                    Assert(crocodiles.Count(enemy => enemy.Definition.Id == MonsterIds.Krokodilidomár) == 2 &&
                           crocodiles.Count(enemy => enemy.Definition.Id == MonsterIds.MocsáriKrokodil) == 6 &&
                           pool.InteriorPositions().Any(position =>
                               poolArea.GetTerrainGameplayProfile(position) is
                               { Tags: TerrainTag.Marsh, MovementDelayPercent: 25, ExertionCost: 1 } &&
                               poolArea.IsWalkable(position)),
                        "A palota medencéjéből hiányzik a víz vagy az idomárok állatcsapata.");
                }
                else
                {
                    Assert(enemies.Count(enemy => enemy.Definition.Id == MonsterIds.ŐsiHidra) == 1 &&
                           enemies.Count(enemy => enemy.Definition.Id == MonsterIds.Medúza) == 1 &&
                           enemies.Count(enemy => enemy.Definition.Id == MonsterIds.ÓriásBaziliszkusz) == 1,
                        "A belső templom ritka őrzői tömegesek vagy hiányoznak.");
                }
            }
            Assert(sizes.SetEquals([minimum, maximum]), "A dungeon egyik képernyőmérete nem jött létre.");
        }
    }

    static void ScaledCombatCulturesUseShieldsPoisonAndMagic()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var shield = new ConfiguredEnemy(new(2, 2), data.GetEnemy(MonsterIds.PajzsosGyíkőr), new Random(12));
        var hunter = new ConfiguredEnemy(new(3, 2), data.GetEnemy(MonsterIds.GyíkemberVadász), new Random(13));
        var archer = new ConfiguredEnemy(new(4, 2), data.GetEnemy(MonsterIds.Kígyóíjász), new Random(14));
        var shaman = data.GetEnemy(MonsterIds.GyíkemberSámán);
        var priest = data.GetEnemy(MonsterIds.KígyóFőpap);
        Assert(shield.EquippedShield is not null && shield.Definition.Speed < hunter.Definition.Speed &&
               hunter.AttackWeapons.Any(weapon => weapon.IsRanged) && archer.AttackWeapons.Any(weapon => weapon.IsRanged) &&
               archer.Definition.AbilityIds.Contains("MA017") &&
               shaman.SpellcasterProfile is { Style: EnemySpellcastingStyle.Support } &&
               shaman.SpellcasterProfile.SpellIds.Contains("D017") &&
               priest.SpellcasterProfile!.SpellIds.Contains("D015") &&
               data.GetEnemy(MonsterIds.Óriáskígyó).StrengthTier is 3 or 4,
            "A hüllők harci kultúrái csak nevükben térnek el.");
        var mage = new ConfiguredEnemy(new(5, 2), data.GetEnemy(MonsterIds.Méregmágus), new Random(15));
        var victim = CreateCharacter("Mérgezhető", 1000);
        var battle = new BattleSystem(new ScaledMinimumRandom(), data.MonsterAbilities, data.Statuses, data.StrengthHitBonuses);
        battle.ResolveEnemyAbility(mage, victim, battle.PrepareCharacter(victim).Runtime,
            data.GetMonsterAbility("MA026"), consumeResources: true, targetDistance: 5);
        Assert(victim.HasStatus(CharacterStatusIds.Poisoned) && victim.CurrentVitality < 1000 &&
               !mage.IsAbilityReady(data.GetMonsterAbility("MA026")),
            "A méregigézet nem mérgez vagy nem használja a lehűlést.");
        Assert(MonsterIds.Bosses.Count == 12 && !data.GetEnemy(MonsterIds.Patkányember).IsBoss &&
               !data.GetEnemy(MonsterIds.Ghoul).IsBoss && data.GetEnemy(MonsterIds.GyíkemberKirály).IsBoss &&
               data.GetEnemy(MonsterIds.KígyóFőpap).IsBoss &&
               StoryNarratives.BossNarratives.Keys.Where(MonsterIds.Bosses.Contains).ToHashSet().SetEquals(MonsterIds.Bosses) &&
               !StoryNarratives.BossNarratives.ContainsKey(MonsterIds.Patkányember) &&
               !StoryNarratives.BossNarratives.ContainsKey(MonsterIds.Ghoul),
            "A kulcsbossok cseréje vagy bemutatkozása hiányos.");
    }

    static void ScaledDungeonsMigrateCampaignAndFormerBosses()
    {
        var oldMaze = new MazeSaveData
        {
            Enemies = [new(new(2, 2), "E051", 126, BossHitPointBonusPercent: 20),
                       new(new(3, 2), "E033", 252, BossHitPointBonusPercent: 20)]
        };
        var state = new GameSaveData
        {
            Version = 42, MazeLevel = 15, DifficultyLevel = 15, LocationId = "CAMPAIGN_15",
            Maze = oldMaze, AdHocConversationMazeLevel = 15, PartyCampaignProgression = new(14),
            CollectedBossKeyIds = ["E051", "E033", "E021"], SeenBossIds = ["E051", "E033", "E021"],
            RosterJson = """{"Characters":[{"NpcJoinedMazeLevel":15},{"NpcJoinedMazeLevel":14}],"Campaigns":[{"LastKnownLevel":23}]}"""
        };
        GameSaveFormat.MigrateToCurrent(state);
        Assert(state.MazeLevel == 17 && state.DifficultyLevel == 17 && state.LocationId == "CAMPAIGN_17" &&
               state.AdHocConversationMazeLevel == 17 && ReferenceEquals(oldMaze, state.Maze) &&
               state.PartyCampaignProgression.HighestCompletedCampaignLevel == 14 &&
               state.CollectedBossKeyIds.ToHashSet().SetEquals([MonsterIds.GyíkemberKirály, MonsterIds.KígyóFőpap, "E021"]) &&
               state.SeenBossIds.SequenceEqual(["E021"]) &&
               oldMaze.Enemies.Select(enemy => enemy.CurrentHitPoints).SequenceEqual([105, 210]) &&
               oldMaze.Enemies.All(enemy => enemy.BossHitPointBonusPercent == 0),
            "A régi pálya, kulcs vagy bossbónusz hibásan migrált.");
        using var roster = JsonDocument.Parse(state.RosterJson);
        Assert(roster.RootElement.GetProperty("Characters")[0].GetProperty("NpcJoinedMazeLevel").GetInt32() == 17 &&
               roster.RootElement.GetProperty("Characters")[1].GetProperty("NpcJoinedMazeLevel").GetInt32() == 14 &&
               roster.RootElement.GetProperty("Campaigns")[0].GetProperty("LastKnownLevel").GetInt32() == 25,
            "A karakterek pályahivatkozásai nem követik a két dungeon beszúrását.");
        GameSaveFormat.MigrateToCurrent(state);
        Assert(state.MazeLevel == 17 && oldMaze.Enemies[0].CurrentHitPoints == 105,
            "Az ismételt migráció újra eltolta a pályát vagy csökkentette az életerőt.");
        var quest = new GameSaveData
        {
            Version = 42, MazeLevel = 20, DifficultyLevel = 30, LocationKind = AdventureLocationKind.Quest,
            LocationId = "PRIVATE_QUEST", PartyCampaignProgression = new(19),
            SuspendedCampaign = new() { Version = 42, MazeLevel = 20, DifficultyLevel = 20,
                LocationId = "CAMPAIGN_20", PartyCampaignProgression = new(19) }
        };
        GameSaveFormat.MigrateToCurrent(quest);
        Assert(quest.MazeLevel == 22 && quest.DifficultyLevel == 30 && quest.LocationId == "PRIVATE_QUEST" &&
               quest.PartyCampaignProgression.HighestCompletedCampaignLevel == 21 &&
               quest.SuspendedCampaign is { MazeLevel: 22, DifficultyLevel: 22, LocationId: "CAMPAIGN_22" } &&
               quest.SuspendedCampaign.PartyCampaignProgression.HighestCompletedCampaignLevel == 21,
            "A külön küldetéshelyszín vagy felfüggesztett kampány migrációja hibás.");
        Assert(MazeLevelConfigurations.Get(17).Name == "A fojtogató mélyjárat" &&
               MazeLevelConfigurations.Get(25).Name == "A káosz trónja" && MazeLevelConfigurations.FinalLevel == 25,
            "A korábbi kampánysorrend nem tolódott el két hellyel.");
    }
}

file sealed class ScaledMinimumRandom : Random
{
    public override int Next(int maxValue) => 0;
    public override int Next(int minValue, int maxValue) => minValue;
}
