namespace KaoszRubin.Domain.Quests;

/// <summary>
/// Egy quest futásidejű állapota egy konkrét játékmentésben.
/// Nem tartalmazza magát a quest definícióját.
/// </summary>
public sealed class QuestRuntimeState
{
    public QuestId QuestId { get; }

    /// <summary>
    /// Az NPC konkrét világpéldánya, amelyhez ez a quest-futás tartozik.
    /// Global scope esetén None.
    /// PerNpcInstance scope esetén mindig konkrét példányazonosító.
    /// </summary>
    public QuestNpcInstanceId GiverInstanceId { get; }

    public QuestState State { get; private set; }

    public int Progress { get; private set; }

    /// <summary>
    /// Hányszor lett már sikeresen teljesítve.
    /// Elsősorban Repeatable questeknél érdekes.
    /// </summary>
    public int CompletionCount { get; private set; }

    public QuestRuntimeState(
        QuestId questId,
        QuestNpcInstanceId giverInstanceId = default,
        QuestState initialState = QuestState.Locked)
    {
        if (questId == QuestId.None)
            throw new ArgumentException(
                "QuestId.None nem használható valódi quest állapotához.",
                nameof(questId));

        QuestId = questId;
        GiverInstanceId = giverInstanceId;
        State = initialState;
    }

    internal void SetState(QuestState state)
    {
        State = state;
    }

    internal void Activate()
    {
        if (State != QuestState.Available)
        {
            throw new InvalidOperationException(
                $"A(z) '{QuestId}' quest csak Available állapotból " +
                $"aktiválható. Aktuális állapot: {State}.");
        }

        Progress = 0;
        State = QuestState.Active;
    }

    internal void AddProgress(int amount, int requiredCount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        if (requiredCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredCount));

        if (State != QuestState.Active)
            return;

        Progress = Math.Min(
            Progress + amount,
            requiredCount);

        if (Progress >= requiredCount)
            State = QuestState.ReadyToTurnIn;
    }

    internal void SynchronizeProgress(int currentProgress, int requiredCount)
    {
        if (currentProgress < 0)
            throw new ArgumentOutOfRangeException(
                nameof(currentProgress));

        if (requiredCount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(requiredCount));

        if (State is not (
            QuestState.Active or
            QuestState.ReadyToTurnIn))
        {
            return;
        }

        Progress = Math.Min(
            currentProgress,
            requiredCount);

        State = Progress >= requiredCount
            ? QuestState.ReadyToTurnIn
            : QuestState.Active;
    }

    internal void Complete()
    {
        if (State != QuestState.ReadyToTurnIn)
        {
            throw new InvalidOperationException(
                $"A(z) '{QuestId}' quest csak ReadyToTurnIn állapotból " +
                $"teljesíthető. Aktuális állapot: {State}.");
        }

        State = QuestState.Completed;
        CompletionCount++;
    }

    internal void Fail()
    {
        State = QuestState.Failed;
    }

    internal void ResetForRepeat()
    {
        Progress = 0;
        State = QuestState.Available;
    }
}