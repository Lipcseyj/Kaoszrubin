using KaoszRubin.Data;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Characters;

public static class LiveCharacterFactory
{
    public static LiveCharacter Create(string name, RaceDefinition race, CharacterClassDefinition characterClass,
        PrimaryAbilities rolledAbilities, int vitalityBonus, int manaBonus, GameDataCatalog data,
        ConsoleColor color = ConsoleColor.Cyan, PrimaryAbilities adaptableAbilityBonus = default)
    {
        if (vitalityBonus is < 1 or > 15 || manaBonus is < 1 or > 15)
            throw new ArgumentOutOfRangeException("A HP- és mannabónusznak 1 és 15 között kell lennie.");

        var adaptablePointTotal = adaptableAbilityBonus.Strength + adaptableAbilityBonus.Dexterity +
                                 adaptableAbilityBonus.Health + adaptableAbilityBonus.Intelligence;
        if (adaptableAbilityBonus.Strength < 0 || adaptableAbilityBonus.Dexterity < 0 ||
            adaptableAbilityBonus.Health < 0 || adaptableAbilityBonus.Intelligence < 0 ||
            (race.HasTrait(RaceTraits.Adaptable) ? adaptablePointTotal != 1 : adaptablePointTotal != 0))
            throw new ArgumentException("Az Alkalmazkodó faji bónusznak pontosan egy képességre kell +1-et adnia.",
                nameof(adaptableAbilityBonus));

        var finalAbilities = (rolledAbilities + race.AbilityBonuses + adaptableAbilityBonus).Clamp(1, 13);
        if (!finalAbilities.MeetsMinimum(characterClass.MinimumAbilities))
            throw new ArgumentException("A végső képességértékek nem érik el a választott osztály minimumait.", nameof(rolledAbilities));

        var maximumMana = characterClass.UsesMana
            ? CharacterClassRules.AdjustStartingMana(characterClass.Id,
                data.GetMinimumMana(finalAbilities.Intelligence) + manaBonus)
            : 0;

        var character = new LiveCharacter(name, race, characterClass, finalAbilities,
            data.GetMinimumVitality(finalAbilities.Health) + vitalityBonus,
            maximumMana, vitalityBonus, characterClass.UsesMana ? manaBonus : 0, color);
        SpellcastingRules.GiveRequiredFocus(character, data);
        AddStartingEquipment(character, data.GetStartingEquipment(characterClass.Id), data);
        return character;
    }

    private static void AddStartingEquipment(LiveCharacter character, StartingEquipmentDefinition? equipment, GameDataCatalog data)
    {
        if (equipment is null) return;
        if (equipment.FirstWeaponId is { } firstWeapon) character.EquipWeapon(0, data.GetWeapon(firstWeapon));
        if (equipment.SecondWeaponId is { } secondWeapon) character.EquipWeapon(1, data.GetWeapon(secondWeapon));
        if (equipment.ArmorId is { } armor) character.EquipArmor(data.GetArmor(armor));
        if (equipment.MagicItemId is { } magicItem) character.AddMagicItem(data.GetMagicItem(magicItem));
        foreach (var backpackItem in equipment.BackpackItemIds) character.AddToBackpack(data.GetItem(backpackItem));
    }
}
