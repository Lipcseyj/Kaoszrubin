using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

public interface IQuestRewardContext
{
    LiveCharacter SelectedCharacter { get; }

    IEnumerable<LiveCharacter> PartyMembers { get; }

    IItemDefinition? RollRandomReward(QuestDefinition quest);

    bool TryStoreItem(IItemDefinition item, out string ownerName);

    void DropItem(IItemDefinition item);
}
