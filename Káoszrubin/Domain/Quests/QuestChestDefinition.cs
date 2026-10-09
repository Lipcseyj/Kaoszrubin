using System.Text;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Quests;

/// <summary>Stabil ládaazonosító; nem egy térképi objektum futásidejű ID-ja.</summary>
public readonly record struct QuestChestId
{
    public static QuestChestId RavensLootChest { get; } = new("RAVENS_LOOT");
    public static QuestChestId OrcTribeChest { get; } = new("ORC_TRIBE_SUPPLIES");
    public static QuestChestId SluiceSupplies { get; } = new("SLUICE_SUPPLIES");
    public static QuestChestId SunkenCourtSupplies { get; } = new("SUNKEN_COURT_SUPPLIES");

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
    IReadOnlyList<QuestChestItem> Items, Rune MapSymbol,
    ConsoleColor MapForegroundColor, ConsoleColor MapBackgroundColor, string? GuardianEnemyId = null);
