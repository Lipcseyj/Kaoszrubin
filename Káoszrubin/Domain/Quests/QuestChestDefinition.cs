using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Quests;

/// <summary>Stabil ládaazonosító; nem egy térképi objektum futásidejű ID-ja.</summary>
public readonly record struct QuestChestId
{
    public string Value { get; }
    public QuestChestId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim().ToUpperInvariant();
    }
    public override string ToString() => Value;
}

public sealed record QuestChestItem(IItemDefinition Item, int Quantity);
public sealed record QuestChestDefinition(QuestChestId Id, string Name, int Gold,
    IReadOnlyList<QuestChestItem> Items);
