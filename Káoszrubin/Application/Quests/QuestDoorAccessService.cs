namespace KaoszRubin.Application.Quests;

/// <summary>A host a nyitási kísérlet pillanatában ellenőrzi a konkrét questfutást.</summary>
public sealed class QuestDoorAccessService(QuestManager manager)
{
    public bool TryGrantAccess(MazeDoor door)
    {
        if (!door.IsQuestSealed) return true;
        if (!manager.TryGetQuest(door.RequiredQuest!.Value, out var quest) ||
            quest.State is not (Domain.Quests.QuestState.Active or Domain.Quests.QuestState.ReadyToTurnIn
                or Domain.Quests.QuestState.Completed)) return false;
        door.GrantQuestAccess();
        return true;
    }
}
