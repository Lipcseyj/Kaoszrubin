using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Data;

namespace KaoszRubin.Application;

public sealed class NpcQuestCoordinator
{
    private readonly GameDataCatalog _gameData;

    public NpcQuestCoordinator(GameDataCatalog gameData)
    {
        _gameData = gameData;
    }

    public static IReadOnlyList<QuestJournalEntrySnapshot> OrderedQuestJournal(IEnumerable<QuestJournalEntrySnapshot> journal) =>
        journal.OrderBy(entry => entry.Status)
            .ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public void SynchronizeQuestJournal(
        Dictionary<string, QuestJournalEntrySnapshot> questJournal,
        WorldNpc npc,
        NpcQuestDefinition quest,
        int? visibleProgress = null)
    {
        var progress = npc.Quests.First(value => string.Equals(value.QuestId, quest.Id,
            StringComparison.OrdinalIgnoreCase));
        if (progress.State == NpcQuestState.Offered) return;
        var status = progress.State == NpcQuestState.Completed
            ? QuestJournalStatus.Completed : QuestJournalStatus.Active;
        questJournal[quest.Id] = CreateQuestJournalEntry(quest, status,
            visibleProgress ?? progress.Progress, quest.ExperienceReward);
    }

    public QuestJournalEntrySnapshot CreateQuestJournalEntry(
        NpcQuestDefinition quest,
        QuestJournalStatus status,
        int progress,
        int experienceReward) =>
        new(quest.Id, quest.Title, quest.Description, _gameData.GetNpc(quest.NpcId).Name, status,
            Math.Clamp(progress, 0, quest.RequiredCount), quest.RequiredCount, experienceReward);

    public static bool IsRodericInsigniaEnemy(string? groupId) =>
        string.Equals(groupId, "QUEST:RODERIC:INSIGNIA_1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(groupId, "QUEST:RODERIC:INSIGNIA_2", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(groupId, "QUEST:RODERIC:INSIGNIA_3", StringComparison.OrdinalIgnoreCase);

    public static bool IsRodericMalrecEnemy(string? groupId) =>
        string.Equals(groupId, "QUEST:RODERIC:MALREC", StringComparison.OrdinalIgnoreCase);
}
