namespace KaoszRubin.Domain.Quests;

/// <summary>Egy küldetésfutás azonossága. Globális küldetésnél a példányazonosító None.</summary>
public readonly record struct QuestKey(QuestId QuestId, QuestNpcInstanceId GiverInstanceId = default);

/// <summary>Változatlan állapotmásolat exporthoz és ellenőrzött visszaállításhoz; nem fájlformátum.</summary>
public sealed record QuestStateSnapshot(QuestId QuestId, QuestNpcInstanceId GiverInstanceId,
    QuestState State, int Progress, int CompletionCount)
{
    public QuestKey Key => new(QuestId, GiverInstanceId);
}
