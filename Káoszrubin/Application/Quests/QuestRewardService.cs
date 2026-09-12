using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

public sealed class QuestRewardService
{
    private readonly CharacterProgressionService _progressionService;
    private readonly IQuestRewardContext _context;

    public QuestRewardService(
        CharacterProgressionService progressionService,
        IQuestRewardContext context)
    {
        ArgumentNullException.ThrowIfNull(progressionService);
        ArgumentNullException.ThrowIfNull(context);

        _progressionService = progressionService;
        _context = context;
    }

    public QuestRewardResult Grant(
        QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);

        var experienceAwards =
            GrantExperience(quest);

        var itemRewards =
            GrantItems(quest);

        return new QuestRewardResult(
            experienceAwards,
            itemRewards);
    }

    private IReadOnlyList<ExperienceAward> GrantExperience(
        QuestDefinition quest)
    {
        return _progressionService.DistributeExperience(
            _context.SelectedCharacter,
            quest.ExperienceReward,
            _context.PartyMembers,
            isQuest: true);
    }

    private IReadOnlyList<QuestItemRewardResult> GrantItems(
        QuestDefinition quest)
    {
        var rewards =
            CreateRewardItems(quest);

        if (rewards.Count == 0)
            return [];

        var results =
            new List<QuestItemRewardResult>(
                rewards.Count);

        foreach (var item in rewards)
        {
            if (_context.TryStoreItem(
                    item,
                    out var ownerName))
            {
                results.Add(
                    new QuestItemRewardResult(
                        item,
                        QuestItemRewardPlacement.Backpack,
                        ownerName));

                continue;
            }

            _context.DropItem(item);

            results.Add(
                new QuestItemRewardResult(
                    item,
                    QuestItemRewardPlacement.Ground));
        }

        return results;
    }

    private IReadOnlyList<IItemDefinition> CreateRewardItems(
        QuestDefinition quest)
    {
        var rewards =
            new List<IItemDefinition>();

        if (quest.FixedRewardItem is not null)
        {
            for (var count = 0;
                 count < quest.FixedRewardItemCount;
                 count++)
            {
                rewards.Add(
                    quest.FixedRewardItem);
            }
        }

        for (var count = 0;
             count < quest.RandomRewardCount;
             count++)
        {
            var reward =
                _context.RollRandomReward(
                    quest.ExperienceReward);

            if (reward is not null)
                rewards.Add(reward);
        }

        return rewards;
    }
}