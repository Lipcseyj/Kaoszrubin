namespace KaoszRubin.Domain.Quests;

/// <summary>
/// Egy küldetés aktuális futásidejű állapota.
/// </summary>
public enum QuestState
{
    Locked = 0,
    Available,
    Active,
    ReadyToTurnIn,
    Completed,
    Failed
}