using MazeGame.Data;
using MazeGame.Domain.Inventory;

namespace MazeGame.Domain.Characters;

public static class LiveCharacterFactory
{
    public static LiveCharacter Create(string name, RaceDefinition race, CharacterClassDefinition characterClass, PrimaryAbilities rolledAbilities, int vitalityBonus, int manaBonus, GameDataCatalog data)
    {
        if (vitalityBonus is < 1 or > 15 || manaBonus is < 1 or > 15)
            throw new ArgumentOutOfRangeException("A HP- és mannabónusznak 1 és 15 között kell lennie.");

        var finalAbilities = (rolledAbilities + race.AbilityBonuses).Clamp(1, 13);
        if (!finalAbilities.MeetsMinimum(characterClass.MinimumAbilities))
            throw new ArgumentException("A végső képességértékek nem érik el a választott osztály minimumait.", nameof(rolledAbilities));

        var maximumMana = characterClass.UsesMana
            ? data.GetMinimumMana(finalAbilities.Intelligence) + manaBonus
            : 0;

        var character = new LiveCharacter(name, race, characterClass, finalAbilities,
            data.GetMinimumVitality(finalAbilities.Health) + vitalityBonus,
            maximumMana, vitalityBonus, characterClass.UsesMana ? manaBonus : 0);
        AddStartingEquipment(character, data.GetStartingEquipment(characterClass.Name), data);
        return character;
    }

    private static void AddStartingEquipment(LiveCharacter character, StartingEquipmentDefinition? equipment, GameDataCatalog data)
    {
        if (equipment is null) return;
        if (equipment.FirstWeaponName is { } firstWeapon) character.EquipWeapon(0, data.ResolveWeapon(firstWeapon));
        if (equipment.SecondWeaponName is { } secondWeapon) character.EquipWeapon(1, data.ResolveWeapon(secondWeapon));
        if (equipment.ArmorName is { } armor) character.EquipArmor(data.ResolveArmor(armor));
        if (equipment.MagicItemName is { } magicItem) character.AddMagicItem(data.ResolveMagicItem(magicItem));
        foreach (var backpackItem in equipment.BackpackItemNames) character.AddToBackpack(new MiscItemDefinition(backpackItem));
    }
}
