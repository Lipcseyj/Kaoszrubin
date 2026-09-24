using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Combat;

public static class AmmunitionIds
{
    public const string Arrow = "T029";
    public const string CrossbowBolt = "T030";

    public static bool IsAmmunition(string? itemId) => itemId is not null &&
        (string.Equals(itemId, Arrow, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(itemId, CrossbowBolt, StringComparison.OrdinalIgnoreCase));
}

public static class RangedWeaponRules
{
    public const int AmmunitionStackLimit = 60;
    public const int CloseRangeHitPenalty = -3;

    public static bool CanReach(WeaponDefinition? weapon, int distance)
    {
        if (weapon is null || distance < 1) return false;
        return weapon.IsRanged
            ? distance >= weapon.MinimumRange && distance <= weapon.MaximumRange
            : distance == 1 || weapon.CanAttackFromRear && distance <= 2;
    }

    public static int AmmunitionCount(LiveCharacter character, WeaponDefinition? weapon)
    {
        if (weapon is not { UsesAmmunition: true, AmmunitionItemId: { Length: > 0 } ammunitionId })
            return int.MaxValue;
        return Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
            .Where(index => string.Equals(character.GetInventoryItem(InventorySlotKind.Backpack, index)?.Id,
                ammunitionId, StringComparison.OrdinalIgnoreCase))
            .Sum(index => character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index));
    }

    public static bool HasAmmunition(LiveCharacter character, WeaponDefinition? weapon) =>
        weapon is not { UsesAmmunition: true } || AmmunitionCount(character, weapon) > 0;

    public static bool TryConsumeAmmunition(LiveCharacter character, WeaponDefinition? weapon) =>
        weapon is not { UsesAmmunition: true } ||
        weapon.AmmunitionItemId is { Length: > 0 } ammunitionId && character.RemoveFromBackpack(ammunitionId);

    public static int CloseRangeModifier(LiveCharacter character, WeaponDefinition? weapon, int distance)
    {
        if (weapon is not { IsRanged: true } || distance > 1) return 0;
        var family = WeaponFamilies.ForWeapon(weapon);
        return character.WeaponProficiencyRankFor(family) == WeaponProficiencyRank.Master
            ? 0
            : CloseRangeHitPenalty;
    }

    public static int MaximumStackSize(IItemDefinition item) =>
        AmmunitionIds.IsAmmunition(item.Id) ? AmmunitionStackLimit : LiveCharacter.MaximumBackpackStackSize;
}
