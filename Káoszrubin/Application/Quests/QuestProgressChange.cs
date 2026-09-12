using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// Egy quest progress feldolgozása során bekövetkezett változás.
/// </summary>
public sealed record QuestProgressChange(
    QuestId QuestId,
    QuestNpcInstanceId GiverInstanceId,
    int PreviousProgress,
    int CurrentProgress,
    QuestState PreviousState,
    QuestState CurrentState)
{
    public int ProgressDelta =>
        CurrentProgress - PreviousProgress;

    public bool LostReadyToTurnIn =>
        PreviousState == QuestState.ReadyToTurnIn &&
        CurrentState == QuestState.Active;

    public bool BecameReadyToTurnIn =>
        PreviousState != QuestState.ReadyToTurnIn &&
        CurrentState == QuestState.ReadyToTurnIn;
}