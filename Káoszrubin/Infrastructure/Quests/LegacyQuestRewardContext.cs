using KaoszRubin.Application.Quests;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Infrastructure.Quests;

public delegate bool TryStoreQuestReward(
    IItemDefinition item,
    out string ownerName);

public sealed class LegacyQuestRewardContext
    : IQuestRewardContext
{
    private readonly Func<LiveCharacter> _getSelectedCharacter;
    private readonly Func<IEnumerable<LiveCharacter>> _getPartyMembers;
    private readonly Func<int, IItemDefinition?> _rollRandomReward;
    private readonly TryStoreQuestReward _tryStoreItem;
    private readonly Action<IItemDefinition> _dropItem;

    public LegacyQuestRewardContext(
        Func<LiveCharacter> getSelectedCharacter,
        Func<IEnumerable<LiveCharacter>> getPartyMembers,
        Func<int, IItemDefinition?> rollRandomReward,
        TryStoreQuestReward tryStoreItem,
        Action<IItemDefinition> dropItem)
    {
        _getSelectedCharacter =
            getSelectedCharacter ??
            throw new ArgumentNullException(
                nameof(getSelectedCharacter));

        _getPartyMembers =
            getPartyMembers ??
            throw new ArgumentNullException(
                nameof(getPartyMembers));

        _rollRandomReward =
            rollRandomReward ??
            throw new ArgumentNullException(
                nameof(rollRandomReward));

        _tryStoreItem =
            tryStoreItem ??
            throw new ArgumentNullException(
                nameof(tryStoreItem));

        _dropItem =
            dropItem ??
            throw new ArgumentNullException(
                nameof(dropItem));
    }

    public LiveCharacter SelectedCharacter =>
        _getSelectedCharacter();

    public IEnumerable<LiveCharacter> PartyMembers =>
        _getPartyMembers();

    public IItemDefinition? RollRandomReward(
        int experienceReward) =>
        _rollRandomReward(
            experienceReward);

    public bool TryStoreItem(
        IItemDefinition item,
        out string ownerName) =>
        _tryStoreItem(
            item,
            out ownerName);

    public void DropItem(
        IItemDefinition item) =>
        _dropItem(item);
}