using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Domain.Inventory;

/// <summary>Egy történeti karakterhez tartozó, más által nem használható személyes tárgyak.</summary>
public static class CharacterBoundItemRules
{
    public const string RodericGreatswordId = "RW001";
    public const string RodericPlateArmorId = "RA001";
    public const string RodericName = "Sir Roderic";

    public static bool IsBound(IItemDefinition? item) => item is not null &&
        (string.Equals(item.Id, RodericGreatswordId, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(item.Id, RodericPlateArmorId, StringComparison.OrdinalIgnoreCase));

    public static bool CanBeHeldBy(LiveCharacter character, IItemDefinition? item) =>
        !IsBound(item) || string.Equals(character.Name, RodericName, StringComparison.OrdinalIgnoreCase);
}
