using KaoszRubin.Data;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure.Quests;

namespace KaoszRubin.Tests.Quests;

internal static class QuestCatalogImportTests
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string DataPath => Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName);

    public static void AllDefinitionsPreserveCsvData()
    {
        var data = CsvGameDataLoader.Load(DataPath);
        var rows = File.ReadLines(DataPath).Where(line => line.StartsWith("NPCQ", StringComparison.Ordinal))
            .Select(line => line.Split(';')).ToArray();
        Require(rows.Length == 42 && data.Quests.Count == 42 && data.Npcs.Count == 22,
            "A teljes quest/NPC katalógus hiányos.");
        Require(data.Quests.All.Select(q => q.Id).ToHashSet().SetEquals(
            Enum.GetValues<QuestId>().Where(id => id != QuestId.None)), "Hiányzó vagy duplikált questmapping.");
        Require(data.Npcs.Select(npc => LegacyNpcIdMap.ToQuestNpcId(npc.Id)).ToHashSet().SetEquals(
            Enum.GetValues<QuestNpcId>().Where(id => id != QuestNpcId.None)), "Hiányzó NPC-mapping.");
        foreach (var npc in data.Npcs)
        {
            var id = LegacyNpcIdMap.ToQuestNpcId(npc.Id);
            Require(LegacyNpcIdMap.ToExternalId(id) == npc.Id && ReferenceEquals(data.GetNpc(id), npc),
                $"Nem fordul körbe az NPC-azonosító: {npc.Id}.");
        }
        foreach (var row in rows)
        {
            string Cell(int index) => index < row.Length ? row[index] : "";
            var quest = data.Quests.Get(LegacyQuestIdMap.ToQuestId(row[0]));
            var npc = data.GetNpc(quest.Giver);
            Require(LegacyQuestIdMap.ToExternalId(quest.Id) == row[0] && npc.Id == row[1] &&
                data.Quests.GetByGiver(quest.Giver).Contains(quest) &&
                quest.Title == row[6] && quest.Description == row[7] &&
                quest.Objective.RequiredCount == int.Parse(row[4]) && quest.ExperienceReward == int.Parse(row[5]) &&
                quest.Scope == (npc.Unique ? QuestScope.Global : QuestScope.PerNpcInstance) &&
                quest.RepeatPolicy == QuestRepeatPolicy.Once, $"Eltérő questdefiníció: {row[0]}.");
            Require(quest.FixedRewardItem?.Id == (Cell(8) == "" ? null : Cell(8)) &&
                quest.FixedRewardItemCount == (Cell(9) == "" ? 0 : int.Parse(Cell(9))) &&
                quest.RandomRewardCount == (Cell(10) == "" ? 1 : int.Parse(Cell(10))) &&
                quest.CompletionDialogue?.Id == (Cell(12) == "" ? null : Cell(12)) &&
                quest.CompletionDialogue?.NpcId == row[1],
                $"Eltérő jutalom: {row[0]}.");
            var objectiveMatches = (row[2], quest.Objective) switch
            {
                ("KillWithTraits", QuestObjective.KillEnemyWithTraits kill) => kill.RequiredTraits == EnemyTraits.Undead && kill.RequiredFollower is null,
                ("OpenQuestChest", QuestObjective.OpenQuestChest chest) => chest.ChestId.Value == row[3],
                ("Collect", QuestObjective.CollectItem item) => ReferenceEquals(item.Item, data.GetItemDefinition(row[3])),
                ("Kill", QuestObjective.KillEnemy kill) => ReferenceEquals(kill.Enemy, data.GetEnemy(row[3])),
                ("KillWithFollower", QuestObjective.KillEnemyWithTraits kill) =>
                    row[3] == "MA001" && kill.RequiredTraits == EnemyTraits.Undead && kill.RequiredFollower?.Npc == quest.Giver,
                ("Explore", QuestObjective.ExploreLocation location) => row[3] == "EXIT" && location.Location == QuestLocation.Exit,
                ("Escort", QuestObjective.EscortNpc escort) =>
                    row[3] == "EXIT" && escort.Destination == QuestLocation.Exit && escort.Npc == quest.Giver,
                ("Disarm", QuestObjective.DisarmTraps) => row[3] == "ANY",
                ("OpenChest", QuestObjective.OpenChests) => row[3] == "ANY",
                _ => false
            };
            Require(objectiveMatches, $"Eltérő objective vagy feloldott célpont: {row[0]}.");
        }
        Require(typeof(GameDataCatalog).GetProperty("NpcQuests") is null &&
            !typeof(GameDataCatalog).Assembly.GetExportedTypes().Any(type =>
                type.Name is "NpcQuestDefinition" or "QuestImportRow" or "QuestImportType" or "QuestCatalogBuilder"),
            "A flat importmodell továbbra is publikus API.");
    }

    public static void InvalidDefinitionsFailDuringLoading()
    {
        var original = File.ReadAllText(DataPath);
        foreach (var (source, replacement) in new[]
        {
            ("NPCQ001;NPC001;Collect;T011", "NPCQ999;NPC001;Collect;T011"),
            ("NPCQ001;NPC001;Collect;T011", "NPCQ001;NPC999;Collect;T011"),
            ("NPCQ001;NPC001;Collect;T011", "NPCQ001;NPC001;Collect;MISSING_ITEM"),
            (";T018;2;0", ";MISSING_REWARD;2;0"),
            (";T018;2;0", ";T018;0;0"),
            (";810;Gyógyító készlet;", ";810;;"),
            ("NPCQ001;NPC001;Collect;T011;3;810;Gyógyító készlet;Gyűjts össze három kis gyógyitalt a füvesasszonynak.;T018;2;0;;NPCD072",
                "NPCQ001;NPC001;Collect;T011;3;810;Gyógyító készlet;Gyűjts össze három kis gyógyitalt a füvesasszonynak.;T018;2;0;;MISSING_COMPLETION_DIALOGUE"),
            ("NPCQ001;NPC001;Collect;T011;3;810;Gyógyító készlet;Gyűjts össze három kis gyógyitalt a füvesasszonynak.;T018;2;0;;NPCD072",
                "NPCQ001;NPC001;Collect;T011;3;810;Gyógyító készlet;Gyűjts össze három kis gyógyitalt a füvesasszonynak.;T018;2;0;;NPCD073"),
            ("NPCQ005;NPC001", "NPCQ001;NPC001")
        })
        {
            Require(original.Contains(source), $"A hibásadat-teszt nem találja a forrást: {source}.");
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, original.Replace(source, replacement));
                var rejected = false;
                try { CsvGameDataLoader.Load(path); }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException) { rejected = true; }
                Require(rejected, $"A hibás questdefiníció átjutott a betöltési határon: {replacement}.");
            }
            finally { File.Delete(path); }
        }
    }
}
