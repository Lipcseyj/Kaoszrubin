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
    int RandomRewardCount = 0,
    QuestActivationKind ActivationKind = QuestActivationKind.Offered,
    NpcDialogueDefinition? CompletionDialogue = null,
    IItemDefinition? HighRelationshipRewardItem = null,
    int HighRelationshipRewardItemCount = 0,
    NpcDialogueDefinition? HighRelationshipDialogue = null,
    string? EncounterId = null,
    int MinimumFriendliness = 0,
    int MaximumFriendliness = 10,
    bool GiverLeavesAfterCompletion = false)
{
    /// <summary>Üres hatókör minden találkozásra érvényes; több találkozást | választ el.</summary>
    public bool MatchesEncounter(string? encounterId) =>
        EncounterId is null ||
        encounterId is not null && EncounterId.Split('|', StringSplitOptions.TrimEntries)
            .Contains(encounterId, StringComparer.OrdinalIgnoreCase);
}

public enum QuestActivationKind
{
    Offered,
    Story
}

//Később, ha ténylegesen lesz olyan quest, amelyhez egyszerre több követelmény kell, az ActivationRequirement maga lehet például egy AllOf kompozit. Nem kell már most bonyolítanunk.
