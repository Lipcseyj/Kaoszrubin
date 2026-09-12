namespace KaoszRubin.Domain.Quests;

/// <summary>
/// Egy konkrét, világban létező quest NPC-példány
/// erősen tipizált futásidejű azonosítója.
/// </summary>
public readonly record struct QuestNpcInstanceId
{
    public int Value { get; }

    public bool IsNone => Value == 0;

    public static QuestNpcInstanceId None => default;

    public QuestNpcInstanceId(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Az NPC-példány azonosítójának pozitívnak kell lennie.");

        Value = value;
    }

    public override string ToString() =>
        IsNone ? "None" : Value.ToString();
}