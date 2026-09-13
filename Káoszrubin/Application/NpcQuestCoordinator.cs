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

    public void SynchronizeQuestJournal(
        Dictionary<string, QuestJournalEntrySnapshot> questJournal,
        WorldNpc npc,
        NpcQuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(questJournal);
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(quest);

        var npcId =
            LegacyNpcIdMap.ToQuestNpcId(
                npc.DefinitionId);

        var instanceId =
            _questWorldContext.GetInstanceId(
                npc);

        var typedQuestId =
            LegacyQuestIdMap.ToQuestId(
                quest.Id);

        var questHandle =
            _questManager
                .For(npcId, instanceId)
                .GetQuest(typedQuestId);

        SynchronizeQuestJournal(questJournal, questHandle);
    }

    /// <summary>Csak projekció: a napló soha nem módosítja a futásidejű állapotot.</summary>
    public void SynchronizeQuestJournal(
        Dictionary<string, QuestJournalEntrySnapshot> questJournal, QuestHandle questHandle)
    {
        ArgumentNullException.ThrowIfNull(questJournal);
        ArgumentNullException.ThrowIfNull(questHandle);
        var quest = _gameData.NpcQuests.Single(definition =>
            LegacyQuestIdMap.ToQuestId(definition.Id) == questHandle.Id);

        // A legacy Offered állapot megfelelője nálunk
        // Locked vagy Available.
        // Ezek még nem kerülnek a naplóba.
        if (questHandle.State is
            QuestState.Locked or
            QuestState.Available)
        {
            return;
        }

        questJournal.TryGetValue(
            quest.Id,
            out var previous);

        var status =
            questHandle.State switch
            {
                QuestState.Completed =>
                    QuestJournalStatus.Completed,

                QuestState.Failed =>
                    QuestJournalStatus.Abandoned,

                QuestState.Active or
                QuestState.ReadyToTurnIn =>
                    QuestJournalStatus.Active,

                _ =>
                    throw new InvalidOperationException(
                        $"A(z) '{questHandle.Id}' quest " +
                        $"nem naplózható állapotban van: " +
                        $"{questHandle.State}.")
            };

        questJournal[quest.Id] =
            CreateQuestJournalEntry(
                quest,
                status,
                questHandle.Progress,
                quest.ExperienceReward)
            with
            {
                CompletionExperienceSummary =
                    previous?.CompletionExperienceSummary,

                CompletionItemRewardSummary =
                    previous?.CompletionItemRewardSummary
            };
    }

    public QuestJournalEntrySnapshot CreateQuestJournalEntry(
        NpcQuestDefinition quest,
        QuestJournalStatus status,
        int progress,
        int experienceReward) =>
        new(
            quest.Id,
            quest.Title,
            quest.Description,
            _gameData.GetNpc(quest.NpcId).Name,
            status,
            Math.Clamp(
                progress,
                0,
                quest.RequiredCount),
            quest.RequiredCount,
            experienceReward);

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
