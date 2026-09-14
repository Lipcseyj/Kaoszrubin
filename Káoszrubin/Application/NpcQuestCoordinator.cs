using KaoszRubin.Application.Quests;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Application;

public sealed class NpcQuestCoordinator
{
    private readonly GameDataCatalog _gameData;
    private readonly QuestManager _questManager;
    private readonly MazeQuestWorldContext _questWorldContext;

    public NpcQuestCoordinator(
        GameDataCatalog gameData,
        QuestManager questManager,
        MazeQuestWorldContext questWorldContext)
    {
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(questManager);
        ArgumentNullException.ThrowIfNull(questWorldContext);

        _gameData = gameData;
        _questManager = questManager;
        _questWorldContext = questWorldContext;
    }

    public static IReadOnlyList<QuestJournalEntrySnapshot> OrderedQuestJournal(
        IEnumerable<QuestJournalEntrySnapshot> journal) =>
        journal
            .OrderBy(entry => entry.Status)
            .ThenBy(
                entry => entry.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    /// <summary>Pontos futáskulcs szerinti projekció, a napló nem ír vissza a managerbe.</summary>
    public void SynchronizeQuestJournal(Dictionary<QuestKey, QuestJournalEntrySnapshot> journal, QuestHandle quest)
    {
        if (quest.State is QuestState.Locked or QuestState.Available)
        {
            journal.Remove(quest.Key);
            return;
        }
        journal.TryGetValue(quest.Key, out var previous);
        var giver = quest.Giver;
        var npc = _questWorldContext.ResolveNpc(quest.Giver, quest.GiverInstanceId);
        journal[quest.Key] = new(quest.Key, previous?.Title ?? quest.Title, previous?.Description ?? quest.Description,
            previous?.QuestGiverName ?? npc?.Character.Name ?? _gameData.GetNpc(giver).Name,
            quest.State switch
            {
                QuestState.Completed => QuestJournalStatus.Completed,
                QuestState.Failed => QuestJournalStatus.Abandoned,
                _ => QuestJournalStatus.Active
            }, quest.Progress, quest.RequiredCount, previous?.ExperienceReward ?? quest.ExperienceReward,
            previous?.CompletionExperienceSummary, previous?.CompletionItemRewardSummary);
    }
    public static bool IsRodericInsigniaEnemy(
        string? groupId) =>
        string.Equals(
            groupId,
            "QUEST:RODERIC:INSIGNIA_1",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            groupId,
            "QUEST:RODERIC:INSIGNIA_2",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            groupId,
            "QUEST:RODERIC:INSIGNIA_3",
            StringComparison.OrdinalIgnoreCase);

    public static bool IsRodericMalrecEnemy(
        string? groupId) =>
        string.Equals(
            groupId,
            "QUEST:RODERIC:MALREC",
            StringComparison.OrdinalIgnoreCase);
}
