using System.Reflection;
using System.Runtime.CompilerServices;
using KaoszRubin.Infrastructure.Quests;

namespace KaoszRubin.Tests.Quests;

internal static class CampaignQuestTests
{
    // A bővítés előtti kínálat rögzítése: a későbbi találkozások szűkítése nem változtathatja meg.
    private static readonly (string Encounter, int Level, string Neutral, string Trusted)[] EarlyOffers =
    [
        ("NPCE001", 2, "NPCQ001 NPCQ005", "NPCQ001 NPCQ005"),
        ("NPCE002", 3, "NPCQ002 NPCQ006", "NPCQ002 NPCQ006"),
        ("NPCE003", 5, "NPCQ003 NPCQ007", "NPCQ003 NPCQ007"),
        ("NPCE006", 1, "NPCQ009", "NPCQ009"),
        ("NPCE007", 2, "NPCQ010", "NPCQ010"),
        ("NPCE008", 6, "NPCQ011 NPCQ046 NPCQ047 NPCQ055", "NPCQ011 NPCQ046 NPCQ047 NPCQ055"),
        ("NPCE009", 4, "NPCQ011 NPCQ046", "NPCQ011 NPCQ046"),
        ("NPCE010", 5, "NPCQ012 NPCQ013", "NPCQ012 NPCQ013"),
        ("NPCE028", 2, "NPCQ034 NPCQ035 NPCQ036", "NPCQ034 NPCQ035 NPCQ036"),
        ("NPCE029", 5, "NPCQ037 NPCQ038 NPCQ039 NPCQ040 NPCQ041", "NPCQ037 NPCQ038 NPCQ039 NPCQ040 NPCQ041"),
        ("NPCE030", 1, "NPCQ042", "NPCQ042"),
        ("NPCE031", 3, "NPCQ043", "NPCQ043"),
        ("NPCE033", 4, "NPCQ001 NPCQ005", "NPCQ001 NPCQ005 NPCQ044"),
        ("NPCE035", 6, "NPCQ048", "NPCQ048 NPCQ049"),
        ("NPCE032", 6, "NPCQ050 NPCQ054", "NPCQ050 NPCQ054"),
        ("NPCE036", 6, "NPCQ051 NPCQ053", "NPCQ051 NPCQ053"),
        ("NPCE037", 6, "NPCQ023 NPCQ024 NPCQ052", "NPCQ023 NPCQ024 NPCQ052")
    ];
    private static GameDataCatalog Data() => CsvGameDataLoader.Load(
        Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Set(Game game, string name, object value) =>
        typeof(Game).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, value);
    private static object? Invoke(Game game, string name, params object?[] args)
    {
        try
        {
            return typeof(Game).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }
    private static bool Offered(Game game, WorldNpc npc, QuestDefinition quest) =>
        (bool)Invoke(game, "CanOfferNpcQuest", npc, quest.Id)!;
    private static WorldNpc Npc(NpcEncounterDefinition encounter, LiveCharacter character, int friendliness,
        bool legacy = false) => new(new(1, 1), encounter.NpcId, character, NpcDisposition.Neutral,
            false, true, "", friendliness: friendliness, encounterId: legacy ? null : encounter.Id);

    public static void EarlyOffersSurviveAndReturningQuestsStayLocal()
    {
        var data = Data();
        var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
        Set(game, "_gameData", data);
        var character = new QuestTestFixture().SelectedCharacter;
        foreach (var (encounterId, level, neutral, trusted) in EarlyOffers)
        foreach (var (friendliness, expected) in new[] { (5, neutral), (8, trusted) })
        foreach (var legacy in new[] { false, true })
        {
            var encounter = data.NpcEncounters.Single(value => value.Id == encounterId);
            Set(game, "_mazeLevel", level);
            var npc = Npc(encounter, character, friendliness, legacy);
            var actual = data.Quests.GetByGiver(LegacyNpcIdMap.ToQuestNpcId(encounter.NpcId))
                .Where(quest => Offered(game, npc, quest))
                .Select(quest => LegacyQuestIdMap.ToExternalId(quest.Id)).Order().ToArray();
            Require(actual.SequenceEqual(expected.Split(' ')),
                $"Megváltozott az 1–6. pálya kínálata: {encounterId}, viszony {friendliness}, régi mentés {legacy}.");
        }
        var herbalist = data.Quests.Get(QuestId.HerbalistHealingSupplies);
        Require(herbalist.MatchesEncounter("npce001") && herbalist.MatchesEncounter("NPCE033") &&
                !herbalist.MatchesEncounter("NPCE004") && !herbalist.MatchesEncounter(null),
            "A több találkozáshoz kötött feladat átcsúszik a későbbi visszatérésbe.");
        foreach (var quest in data.Quests.All.Where(quest =>
                     quest.HighRelationshipDialogue is { } dialogue && int.Parse(dialogue.Id[4..]) >= 148))
        {
            var dialogue = quest.HighRelationshipDialogue!;
            Require(dialogue.EncounterId is not null && quest.MatchesEncounter(dialogue.EncounterId) &&
                    data.NpcEncounters.Single(encounter => encounter.Id == dialogue.EncounterId).MazeLevel >= 7,
                $"Későbbi bizalmi információ belekeveredhet az 1–6. pálya köszöntéseibe: {quest.Id}.");
        }
        foreach (var replacement in new[] { "NPCE001|NPCE014", "NPCE001|UNKNOWN", "NPCE001||NPCE033" })
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                    CsvGameDataLoader.GameDataFileName)).Replace("NPCE001|NPCE033", replacement));
                var rejected = false;
                try { CsvGameDataLoader.Load(path); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, $"Hibás találkozáslista betöltődött: {replacement}.");
            }
            finally { File.Delete(path); }
        }
    }

    public static void LaterCampaignHasVarietyAndThreeToFiveReturns()
    {
        var data = Data();
        var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
        Set(game, "_gameData", data);
        var character = new QuestTestFixture().SelectedCharacter;
        var late = new HashSet<QuestDefinition>();
        for (var level = 7; level <= MazeLevelConfigurations.FinalLevel; level++)
        {
            Set(game, "_mazeLevel", level);
            var offers = data.NpcEncounters.Where(encounter => encounter.MazeLevel == level)
                .SelectMany(encounter => data.Quests.GetByGiver(LegacyNpcIdMap.ToQuestNpcId(encounter.NpcId))
                    .Where(quest => Offered(game, Npc(encounter, character, 2), quest))).Distinct().ToArray();
            Require(offers.Length >= 4 && offers.Select(quest => quest.Objective.GetType()).Distinct().Count() >= 2,
                $"A(z) {level}. pálya alacsony viszonynál is szegényes maradt.");
            foreach (var quest in offers)
            {
                late.Add(quest);
                Require(quest.EncounterId is not null && quest.RepeatPolicy == QuestRepeatPolicy.Once &&
                        quest.Scope == QuestScope.PerNpcInstance && quest.CompletionDialogue is not null,
                    $"Ismételhető vagy helyszín nélküli késői feladat: {quest.Id}.");
            }
            if (MazeLevelConfigurations.Get(level).Layout is ForestMazeLayoutConfiguration)
                Require(offers.All(quest => quest.Objective is not QuestObjective.DisarmTraps),
                    "Új erdős visszatérés varázscsapda-feladatot örökölt.");
        }
        Require(late.Select(quest => quest.Objective.GetType()).Distinct().Count() >= 7,
            "Hiányzik az eltérő célok valamelyik típusa.");
        foreach (var npc in data.Npcs.Where(npc => npc.PersistentRelationship && !npc.Unique))
        {
            var encounters = data.NpcEncounters.Where(encounter => encounter.NpcId == npc.Id).ToArray();
            Require(encounters.Length is >= 3 and <= 5 && encounters.Select(e => e.MazeLevel).Distinct().Count() == encounters.Length,
                $"A visszatérő NPC nem 3–5 különböző pályán szerepel: {npc.Id}.");
            var quests = data.Quests.GetByGiver(LegacyNpcIdMap.ToQuestNpcId(npc.Id));
            Require(quests.Any(quest => quest.HighRelationshipRewardItem is not null &&
                                       quest.HighRelationshipDialogue is not null),
                $"A viszonyhoz nem tartozik jutalom és információ: {npc.Id}.");

            // A legrosszabb kezdő dobásból is lehessen bónuszt szerezni pusztán a végleges, egyszeri feladatokkal.
            var baseFriendliness = npc.Disposition switch
            {
                NpcDisposition.Friendly => 7,
                NpcDisposition.Neutral => 3,
                _ => 0
            };
            var modifier = npc.Behavior switch
            {
                NpcWorldBehavior.Friendly => 1,
                NpcWorldBehavior.Guarded => -1,
                NpcWorldBehavior.Aggressive => -2,
                _ => 0
            };
            var friendliness = Math.Clamp(baseFriendliness + modifier, 0, 10);
            var reachesBonus = false;
            foreach (var encounter in encounters.OrderBy(value => value.MazeLevel))
            {
                var remaining = quests.Where(quest => quest.MatchesEncounter(encounter.Id)).ToList();
                while (remaining.FirstOrDefault(quest => friendliness >= quest.MinimumFriendliness &&
                           friendliness <= quest.MaximumFriendliness) is { } quest)
                {
                    remaining.Remove(quest);
                    friendliness = Math.Min(10, friendliness + 1);
                    reachesBonus |= friendliness >= 8 && quest.HighRelationshipRewardItem is not null &&
                                    quest.HighRelationshipDialogue is not null;
                }
            }
            Require(reachesBonus, $"Alacsony kezdő viszonyból nem érhető el a bónusz farmolás nélkül: {npc.Id}.");
        }
    }

    public static void QuestTargetsRunesAndGiversActuallyGenerate()
    {
        var data = Data();
        var source = new FileForestLevelGraphSource(Path.Combine(AppContext.BaseDirectory, "ForestLevelGraphs"));
        var missingTargets = new List<string>();
        for (var number = 7; number <= MazeLevelConfigurations.FinalLevel; number++)
        foreach (var seed in Enumerable.Range(1, 5))
        {
            var configuration = MazeLevelConfigurations.Get(number);
            var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
            var roster = new CharacterRoster();
            var character = new QuestTestFixture().SelectedCharacter;
            roster.Add(character);
            roster.Select(character);
            Set(game, "_random", new Random(number * 1000 + seed));
            Set(game, "_gameData", data);
            Set(game, "_mazeLevel", number);
            Set(game, "_difficultyLevel", number);
            Set(game, "<CharacterRoster>k__BackingField", roster);
            Set(game, "<PartyLeader>k__BackingField", character);
            Set(game, "_npcRelationships", new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            var level = (DungeonLevel)Invoke(game, "GenerateDungeonLevel", configuration,
                ForestLevelGraphOverrideBridge.Apply(configuration, source))!;
            Set(game, "_dungeonLevel", level);
            Set(game, "_maze", level.ActiveArea.Maze);
            Set(game, "_fogOfWar", level.ActiveArea.FogOfWar);
            Invoke(game, "PlaceTrapsAcrossAreas", configuration);
            Invoke(game, "PlaceConfiguredWorldNpcs");
            Invoke(game, "PlaceSpecialRoomContent", configuration);
            var enemies = level.Areas.SelectMany(area => area.Maze.Enemies).ToArray();
            var traps = level.Areas.SelectMany(area => area.Maze.Traps).ToArray();
            var chests = level.Areas.SelectMany(area => area.Maze.TreasureChests).ToArray();
            var npcs = level.Areas.SelectMany(area => area.Maze.WorldNpcs).ToArray();
            foreach (var encounter in data.NpcEncounters.Where(encounter => encounter.MazeLevel == number))
            {
                Require(npcs.Count(npc => npc.EncounterId == encounter.Id) == 1,
                    $"Hiányzó küldetésadó: {number}/{encounter.Id}, seed {seed}.");
                var npc = npcs.Single(npc => npc.EncounterId == encounter.Id);
                Require(!string.IsNullOrWhiteSpace(npc.Dialogue), $"Hiányzó bevezető: {encounter.Id}.");
                if (encounter.AreaId is not null)
                    Require(level.GetArea(encounter.AreaId).Maze.WorldNpcs.Contains(npc),
                        $"Az erdei küldetésadó rossz területre került: {encounter.Id}.");
                foreach (var quest in data.Quests.GetByGiver(LegacyNpcIdMap.ToQuestNpcId(encounter.NpcId))
                             .Where(quest => quest.MatchesEncounter(encounter.Id)))
                {
                    var count = quest.Objective switch
                    {
                        QuestObjective.KillEnemy kill => enemies.Count(enemy => enemy.Definition.Id == kill.Enemy.Id),
                        QuestObjective.KillEnemyWithTraits kill => enemies.Count(enemy => enemy.Definition.HasTrait(kill.RequiredTraits)),
                        QuestObjective.DisarmTraps disarm => traps.Count(trap =>
                            disarm.RequiredTrap is null || trap.Definition.Id == disarm.RequiredTrap.Id),
                        QuestObjective.OpenChests => chests.Length,
                        QuestObjective.OpenQuestChest chest => chests.Count(value => value.Definition?.Id == chest.ChestId),
                        _ => quest.Objective.RequiredCount
                    };
                    if (count < quest.Objective.RequiredCount)
                        missingTargets.Add($"Kevés célpont: {number}/{quest.Id}, {count}/{quest.Objective.RequiredCount}, seed {seed}.");
                    if (quest.Objective is QuestObjective.DisarmTraps { RequiredTrap: { } required } guardedDisarm)
                        Require(required.Effect == TrapEffect.Spell &&
                                configuration.GuaranteedTraps.Count(trap => trap.TrapId == required.Id) >= guardedDisarm.RequiredCount,
                            $"A küldetés rúnái véletlenszerűek: {number}/{quest.Id}.");
                }
            }
        }
        Require(missingTargets.Count == 0, string.Join(Environment.NewLine, missingTargets));
    }

    public static void SpecificRuneQuestCountsOnlyItsRuneAndPaysOnce()
    {
        var data = Data();
        var definition = data.Quests.Get(QuestId.RuneBreakerBlindingSeals) with { ExperienceReward = 20 };
        var fixture = new QuestTestFixture(definition);
        var quest = fixture.Manager.Activate(definition.Id, new(1));
        fixture.Manager.RegisterTrapDisarmed(data.GetTrap("TR103"));
        Require(quest.Progress == 0, "A villámrúna beleszámít a vakító őrjelekbe.");
        fixture.Manager.RegisterTrapDisarmed(data.GetTrap("TR105"));
        Require(quest.Progress == 1 && !quest.IsReadyToTurnIn, "Hibás részhaladás.");
        fixture.Manager.RegisterTrapDisarmed(data.GetTrap("TR105"));
        Require(quest.IsReadyToTurnIn, "A két megfelelő rúna nem teljesíti a feladatot.");
        quest.Complete(includeHighRelationshipReward: true);
        var rewards = fixture.StoredRewards.Count;
        Require(fixture.StoredRewards.Count(item => item.Id == "MS004") == 2 &&
                fixture.StoredRewards.Count(item => item.Id == "PS007") == 1,
            "A magas viszonyú rúnafeladat nem adja a rendes és extra jutalmát.");
        fixture.Manager.RegisterTrapDisarmed(data.GetTrap("TR105"));
        var rejected = false;
        try { quest.Complete(includeHighRelationshipReward: true); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && fixture.StoredRewards.Count == rewards, "A magas viszonyú bónusz újra kiosztható.");
    }
}
