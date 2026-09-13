namespace KaoszRubin.Application.Quests;

/// <summary>A quest megjelenítéséhez szükséges változatlan adat, élő handle és flat definíció nélkül.</summary>
public sealed record QuestPresentationSnapshot(Domain.Quests.QuestKey Key, string Title, string Description,
    int Progress, int RequiredCount, int ExperienceReward, string RewardItemsText)
{
    public static QuestPresentationSnapshot From(QuestHandle quest)
    {
        var rewards = new List<string>();
        if (quest.FixedRewardItem is { } item && quest.FixedRewardItemCount > 0)
            rewards.Add($"{item.Name} ×{quest.FixedRewardItemCount}");
        if (quest.RandomRewardCount > 0) rewards.Add($"{quest.RandomRewardCount} véletlen tárgy");
        return new(quest.Key, quest.Title, quest.Description, quest.Progress, quest.RequiredCount,
            quest.ExperienceReward, rewards.Count == 0 ? "nincs tárgyjutalom" : string.Join(" + ", rewards));
    }
}

/// <summary>A megerősítés csak megjelenítési adatot kap. Elutasításkor nincs fogyasztás vagy jutalom;
/// elfogadás után a handle újra ellenőrzi a leadhatóságot.</summary>
public static class QuestTurnInService
{
    public static bool TryComplete(QuestHandle quest, Func<QuestPresentationSnapshot, bool> confirm,
        out QuestCompletionResult result)
    {
        result = null!;
        if (!confirm(QuestPresentationSnapshot.From(quest))) return false;
        result = quest.Complete();
        return true;
    }
}
