using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Characters;

public static class DoorInteractionRules
{
    public static LiveCharacter SelectLockHandler(LiveCharacter actor, LiveCharacter? assistingThief,
        bool? useKeyChoice)
    {
        var actorHasKey = actor.Backpack.Any(item =>
            string.Equals(item?.Id, MiscItemIds.Key, StringComparison.OrdinalIgnoreCase));
        var actorUsesKey = actorHasKey &&
                           (!CharacterClassRules.IsThief(actor.CharacterClass.Id) || useKeyChoice == true);
        return actorUsesKey ? actor : assistingThief ?? actor;
    }
}
