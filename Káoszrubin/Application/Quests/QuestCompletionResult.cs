using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// Egy sikeresen lezárt quest eredménye.
///
/// A tényleges XP- és jutalomtárgy-kiosztást egy későbbi
/// reward service végzi majd ezen információk alapján.
/// </summary>
public sealed record QuestCompletionResult(
    QuestId QuestId,
    QuestNpcInstanceId GiverInstanceId,
    string Title,
    int ExperienceReward,
    IItemDefinition? FixedRewardItem,
    int FixedRewardItemCount,
    int RandomRewardCount,
    int CompletionCount);