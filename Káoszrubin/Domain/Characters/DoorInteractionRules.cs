using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Characters;

public static class DoorInteractionRules
{
    public static bool HasKey(LiveCharacter character) => character.Backpack.Any(item =>
        string.Equals(item?.Id, MiscItemIds.Key, StringComparison.OrdinalIgnoreCase));

    public static LiveCharacter? SelectKeyOwner(LiveCharacter actor, IEnumerable<LiveCharacter> availableOwners,
        bool? useKeyChoice, CharacterId? requestedOwnerId = null)
    {
        if (useKeyChoice == false) return null;
        var owners = availableOwners.Where(owner => owner.IsAlive && HasKey(owner))
            .DistinctBy(owner => owner.Id).ToArray();
        if (requestedOwnerId is { } requested)
            return useKeyChoice == true ? owners.FirstOrDefault(owner => owner.Id == requested) : null;
        if (CharacterClassRules.IsThief(actor.CharacterClass.Id) && useKeyChoice != true) return null;
        return owners.FirstOrDefault(owner => owner == actor) ?? owners.FirstOrDefault();
    }
}
