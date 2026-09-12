using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application.Quests;

public enum QuestItemRewardPlacement
{
    Backpack,
    Ground
}

public sealed record QuestItemRewardResult(
    IItemDefinition Item,
    QuestItemRewardPlacement Placement,
    string? OwnerName = null)
{
    public bool Dropped =>
        Placement == QuestItemRewardPlacement.Ground;
}