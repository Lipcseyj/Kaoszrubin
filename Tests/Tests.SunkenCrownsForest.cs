internal static partial class Program
{
    static void SunkenCrownsForestHasMatchingJsonAndConnectedRegions()
    {
        var configuration = MazeLevelConfigurations.Get(SunkenCrownsForest.CampaignLevel);
        Assert(configuration.Name == "Süllyedt koronák lápvidéke" &&
               configuration.ForestGraphJsonOverrideEnabled &&
               configuration.Layout is ForestMazeLayoutConfiguration,
            "A lápvidék nem erdei kampánypályaként szerepel.");
        var source = new FileForestLevelGraphSource(Path.Combine(AppContext.BaseDirectory, "ForestLevelGraphs"));
        Assert(source.TryLoad(13, out var document, out _, out var warning) && warning is null,
            $"A csomagolt lápvidék-gráf nem tölthető be: {warning}");
        var graph = document!.Graph;
        graph.Validate();
        var fallback = SunkenCrownsForest.CreateGraph();
        Assert(ForestConfigurationJson.Serialize(13, graph) == ForestConfigurationJson.Serialize(13, fallback),
            "A terep JSON-ja és a beépített tartalék eltér.");
        Assert(graph.Areas.Count == 12 && graph.Connections.Count == 14 &&
               graph.EntranceAreaId == "REED_GATE" && graph.ExitAreaId == "DROWNED_THRONE",
            "A lápvidék mérete, hurkai vagy bejáratai hibásak.");
        var ids = graph.Areas.Select(area => area.Id).ToHashSet();
        Assert(configuration.RoomEncounters.Concat(configuration.CorridorEncounters)
                   .All(encounter => encounter.AreaId is null || ids.Contains(encounter.AreaId)),
            "Valamelyik lápvidéki csoport ismeretlen területet céloz.");
        var forest = (ForestMazeLayoutConfiguration)configuration.Layout!;
        var throne = ForestAreaConfigurationResolver.Resolve(forest.Forest, graph, graph.Areas.Single(area => area.Id == "DROWNED_THRONE"));
        var mire = ForestAreaConfigurationResolver.Resolve(forest.Forest, graph, graph.Areas.Single(area => area.Id == "LEECH_MIRE"));
        Assert(throne.BuildingCount.Minimum == 2 && throne.ManorBuildingChance == 1 &&
               mire.BuildingCount.Maximum == 0 && mire.MarshCount.Minimum >= 10,
            "A királyi rom és a nagy ingovány tereptípusai eltűntek.");
        var effective = (ForestMazeLayoutConfiguration)ForestLevelGraphOverrideBridge.Apply(configuration, source)!;
        Assert(effective.ExplicitGraph!.Areas.Count == 12,
            "A játék nem alkalmazza a lápvidék JSON-ját.");
        var emptySource = new FileForestLevelGraphSource(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var retained = (ForestMazeLayoutConfiguration)ForestLevelGraphOverrideBridge.Apply(configuration, emptySource)!;
        Assert(retained.ExplicitGraph!.Areas.Select(area => area.Id).SequenceEqual(graph.Areas.Select(area => area.Id)),
            "Hiányzó JSON mellett elvesztek a célzott területek.");
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        Assert(data.NpcEncounters.Single(encounter => encounter.Id == "NPCE018").MazeLevel == 14 &&
               data.NpcEncounters.Single(encounter => encounter.Id == "NPCE038").AreaId == "FERRY_ISLAND" &&
               MazeLevelConfigurations.FinalLevel == 25 &&
               MazeLevelConfigurations.Get(14).Layout is WideMazeLayoutConfiguration,
            "A korábbi mocsár vagy a révésze nem követte a beszúrást.");
    }

    static void SunkenCrownsForestSaveMigrationPreservesLocationsAndHistory()
    {
        var oldMaze = new MazeSaveData { LevelName = "Korábbi mocsár" };
        var state = new GameSaveData
        {
            Version = 37, MazeLevel = 13, DifficultyLevel = 13, LocationId = "CAMPAIGN_13",
            Maze = oldMaze, PartyCampaignProgression = new(12), AdHocConversationMazeLevel = 13,
            RosterJson = """{"Characters":[{"NpcJoinedMazeLevel":13},{"NpcJoinedMazeLevel":12}],"Campaigns":[{"LastKnownLevel":22}]}"""
        };
        GameSaveFormat.MigrateToCurrent(state);
        Assert(state.Version == GameSaveFormat.CurrentVersion && state.MazeLevel == 14 && state.DifficultyLevel == 14 &&
               state.LocationId == "CAMPAIGN_14" && state.AdHocConversationMazeLevel == 14 &&
               state.PartyCampaignProgression.HighestCompletedCampaignLevel == 12 &&
               ReferenceEquals(state.Maze, oldMaze),
            "A beszúrás módosította a korábbi térképet vagy rossz kampánypályára vitt.");
        using var roster = JsonDocument.Parse(state.RosterJson);
        Assert(roster.RootElement.GetProperty("Campaigns")[0].GetProperty("LastKnownLevel").GetInt32() == 25,
            "A karakterek kampányhivatkozása a régi számozásban maradt.");
        Assert(roster.RootElement.GetProperty("Characters").EnumerateArray()
                   .Select(character => character.GetProperty("NpcJoinedMazeLevel").GetInt32()).SequenceEqual([14, 12]),
            "A társak csatlakozási története nem követte a lápvidék beszúrását.");
        var quest = new GameSaveData
        {
            Version = 37, MazeLevel = 18, DifficultyLevel = 27,
            LocationKind = AdventureLocationKind.Quest, LocationId = "CUSTOM_QUEST",
            PartyCampaignProgression = new(17),
            SuspendedCampaign = new() { Version = 37, MazeLevel = 18, DifficultyLevel = 18,
                LocationId = "CAMPAIGN_18", PartyCampaignProgression = new(17) }
        };
        GameSaveFormat.MigrateToCurrent(quest);
        Assert(quest.MazeLevel == 21 && quest.DifficultyLevel == 27 && quest.LocationId == "CUSTOM_QUEST" &&
               quest.PartyCampaignProgression.HighestCompletedCampaignLevel == 20 &&
               quest.SuspendedCampaign is { MazeLevel: 21, DifficultyLevel: 21, LocationId: "CAMPAIGN_21" } &&
               quest.SuspendedCampaign.PartyCampaignProgression.HighestCompletedCampaignLevel == 20,
            "A felfüggesztett kampány vagy a külön questhelyszín hibásan migrált.");
        GameSaveFormat.MigrateToCurrent(quest);
        Assert(quest.MazeLevel == 21 && quest.PartyCampaignProgression.HighestCompletedCampaignLevel == 20,
            "Az új mentés ismételt migrációja újra eltolta a számozást.");
        var legacyQuest = new GameSaveData
        {
            Version = 35, MazeLevel = 18, LocationKind = AdventureLocationKind.Quest,
            SuspendedCampaign = new() { Version = 35, MazeLevel = 18 }
        };
        GameSaveFormat.MigrateToCurrent(legacyQuest);
        Assert(legacyQuest.PartyCampaignProgression.HighestCompletedCampaignLevel == 20 &&
               legacyQuest.SuspendedCampaign!.PartyCampaignProgression.HighestCompletedCampaignLevel == 20,
            "A régebbi questmentés kétszer tolta el a teljesített pályák számát.");
        var earlier = GameSaveFormat.MigrateToCurrent(new() { Version = 37, MazeLevel = 12, DifficultyLevel = 12 });
        Assert(earlier.MazeLevel == 12 && earlier.DifficultyLevel == 12,
            "A lápvidék előtti kampánypályák számozása is megváltozott.");
    }
}
