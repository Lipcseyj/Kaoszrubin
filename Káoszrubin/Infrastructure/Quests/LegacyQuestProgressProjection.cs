using KaoszRubin.Application.Quests;
using KaoszRubin.Data;
using KaoszRubin.Domain.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Infrastructure.Quests;

/// <summary>Átmeneti, kizárólag typed → legacy kimenet a még át nem vezetett fogyasztóknak.</summary>
public static class LegacyQuestProgressProjection
{
    public static void Synchronize(WorldNpc? npc, QuestHandle quest)
    {
        if (npc is null) return;
        var legacyState = quest.State switch
        {
            QuestState.Locked or QuestState.Available => NpcQuestState.Offered,
            QuestState.Active or QuestState.ReadyToTurnIn => NpcQuestState.Active,
            QuestState.Completed => NpcQuestState.Completed,
            QuestState.Failed => NpcQuestState.Abandoned,
            _ => throw new ArgumentOutOfRangeException(nameof(quest))
        };
        npc.RestoreQuests(npc.Quests.Select(progress =>
            LegacyQuestIdMap.ToQuestId(progress.QuestId) == quest.Id
                ? progress with { State = legacyState, Progress = quest.Progress }
                : progress).ToArray());
    }
}
