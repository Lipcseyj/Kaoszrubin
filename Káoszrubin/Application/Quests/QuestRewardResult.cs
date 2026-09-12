using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application.Quests;

public sealed record QuestRewardResult(
    IReadOnlyList<ExperienceAward> ExperienceAwards,
    IReadOnlyList<QuestItemRewardResult> ItemRewards)
{
    public IReadOnlyList<ExperienceAward> LevelUpAwards =>
        ExperienceAwards
            .Where(award =>
                award.Result.LeveledUp &&
                award.Character.IsAlive)
            .ToArray();

    public int DroppedItemCount =>
        ItemRewards.Count(reward => reward.Dropped);

    public bool HasItemRewards =>
        ItemRewards.Count > 0;

    public bool HasLevelUps =>
        LevelUpAwards.Count > 0;
}