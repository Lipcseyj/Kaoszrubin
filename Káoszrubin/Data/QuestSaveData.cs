namespace KaoszRubin.Data;

/// <summary>A típusos questállapot mentési alakja. Az azonosítók és állapotnevek stabil szöveges értékek.</summary>
public sealed class QuestSaveData
{
    public List<QuestRuntimeSaveData> States { get; set; } = [];
    public List<QuestNpcIdentitySaveData> NpcIdentities { get; set; } = [];
    public List<QuestJournalSaveData> LegacyJournalArchive { get; set; } = [];
    public List<string> MigrationNotes { get; set; } = [];
}

public sealed record QuestNpcIdentitySaveData(int InstanceId, Guid CharacterId, string NpcId);

public sealed record QuestRuntimeSaveData(string QuestId, int GiverInstanceId, string State,
    int Progress, int CompletionCount, string Title, string Description, string GiverName,
    int ExperienceReward, string? CompletionExperienceSummary = null,
    string? CompletionItemRewardSummary = null);
