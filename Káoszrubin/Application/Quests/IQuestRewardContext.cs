using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application.Quests;

public interface IQuestRewardContext
{
    LiveCharacter SelectedCharacter { get; }

    IEnumerable<LiveCharacter> PartyMembers { get; }

    IItemDefinition? RollRandomReward(int experienceReward);

    bool TryStoreItem(IItemDefinition item, out string ownerName);

    void DropItem(IItemDefinition item);
}