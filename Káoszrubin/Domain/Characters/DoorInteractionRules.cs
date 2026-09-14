using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Characters;

public static class DoorInteractionRules
{
    public static bool HasKey(LiveCharacter character) => character.Backpack.Any(item =>
        string.Equals(item?.Id, MiscItemIds.Key, StringComparison.OrdinalIgnoreCase));

    public static LiveCharacter? SelectKeyOwner(
        LiveCharacter actor,
        IEnumerable<LiveCharacter> availableOwners,
        bool? useKeyChoice,
        CharacterId? requestedOwnerId = null)
    {
        var owners = availableOwners
            .Where(owner =>
                owner.IsAlive &&
                HasKey(owner))
            .DistinctBy(owner => owner.Id)
            .ToArray();

        if (owners.Length == 0)
            return null;

        // A tolvaj soha ne használjon automatikusan kulcsot.
        // Nála ehhez mindig explicit igen szükséges.
        if (CharacterClassRules.IsThief(actor.CharacterClass.Id) &&
            useKeyChoice != true)
        {
            return null;
        }

        if (useKeyChoice == false)
            return null;

        if (requestedOwnerId is { } requested)
        {
            return useKeyChoice == true
                ? owners.FirstOrDefault(
                    owner => owner.Id == requested)
                : null;
        }

        return owners.FirstOrDefault(
                   owner => owner == actor)
               ?? owners.FirstOrDefault();
    }
}
