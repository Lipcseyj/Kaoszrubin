using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Quests;

/// <summary>
/// Egy quest változatlan, adatvezérelt definíciója.
/// Nem tartalmaz futásidejű állapotot vagy progresst.
/// </summary>
public sealed record QuestDefinition(
    QuestId Id,
    QuestNpcId Giver,
    string Title,
    string Description,
    QuestObjective Objective,
    int ExperienceReward,
    QuestScope Scope,
    QuestRepeatPolicy RepeatPolicy,
    IItemDefinition? FixedRewardItem = null,
    int FixedRewardItemCount = 0,
    int RandomRewardCount = 0);