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
    QuestActivationRequirement? ActivationRequirement = null,
    IItemDefinition? FixedRewardItem = null,
    int FixedRewardItemCount = 0,
    int RandomRewardCount = 0);

//Később, ha ténylegesen lesz olyan quest, amelyhez egyszerre több követelmény kell, az ActivationRequirement maga lehet például egy AllOf kompozit. Nem kell már most bonyolítanunk.