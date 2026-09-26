using KaoszRubin.Domain;

namespace KaoszRubin.Data;

// Csak a CSV betöltése alatt élő köztes adat; a játék QuestDefinition objektumot kap.
internal enum QuestImportType { Collect, Kill, KillWithFollower, Explore, Disarm, OpenChest, Escort, OpenQuestChest, KillWithTraits }

internal sealed record QuestImportRow(string Id, string NpcId, QuestImportType Type, string TargetId,
    int RequiredCount, int ExperienceReward, string Title, string Description,
    string? RewardItemId = null, int RewardItemCount = 0, int RandomRewardCount = 1,
    string? RequiredStoryStateId = null, string? CompletionDialogueId = null, bool IsGlobal = false) : IGameDefinition
{
    public string Name => Title;
}
