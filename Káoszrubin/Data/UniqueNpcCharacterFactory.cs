using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Data;

/// <summary>CSV-ben rögzített, újrageneráláskor is azonos felépítésű egyedi NPC-t készít.</summary>
public sealed class UniqueNpcCharacterFactory(GameDataCatalog data)
{
    public LiveCharacter Create(NpcDefinition npc)
    {
        var build = data.GetUniqueNpcCharacter(npc.Id) ??
            throw new InvalidOperationException($"A(z) '{npc.Id}' egyedi NPC-hez nincs karakterlap.");
        if (npc.RaceId is null)
            throw new InvalidOperationException($"A(z) '{npc.Id}' egyedi NPC-hez nincs faj megadva.");

        var random = new Random(StableSeed(npc.Id));
        var character = LiveCharacterFactory.Create(npc.Name, data.GetRace(npc.RaceId),
            data.GetCharacterClass(npc.CharacterClassId), build.RolledAbilities,
            build.VitalityBonus, build.ManaBonus, data, build.Color, build.AdaptableAbilityBonus);
        character.SetNpcBehavior(build.Behavior);
        new RandomCharacterGenerator(data, random).RaiseToLevel(character, build.Level);

        ClearGeneratedEquipment(character);
        if (build.SpecializationId is { } specializationId && !character.ChooseSpecialization(specializationId))
            throw new InvalidOperationException($"{npc.Name} specializációja érvénytelen: '{specializationId}'.");
        foreach (var perkId in build.PerkIds)
        {
            var perk = data.GetPerk(perkId);
            if (!character.AddPerk(perk))
                throw new InvalidOperationException($"{npc.Name} tehetsége nem adható hozzá: '{perkId}'.");
            character.ApplyPerkAcquisitionBonus(perk);
        }

        Equip(character, build.FirstWeaponId, build.SecondWeaponId, build.ArmorId);
        foreach (var itemId in build.MagicItemIds)
            if (!character.AddMagicItem(data.GetMagicItem(itemId)))
                throw new InvalidOperationException($"{npc.Name} varázstárgya nem helyezhető el: '{itemId}'.");
        foreach (var itemId in build.BackpackItemIds)
            if (!character.AddToBackpack(data.GetItem(itemId)))
                throw new InvalidOperationException($"{npc.Name} hátizsákja megtelt: '{itemId}'.");
        return character;
    }

    private void Equip(LiveCharacter character, string? firstWeaponId, string? secondWeaponId, string? armorId)
    {
        if (firstWeaponId is { } first && !character.EquipWeapon(0, data.GetWeapon(first)))
            throw new InvalidOperationException($"{character.Name} nem használhatja ezt a fegyvert: '{first}'.");
        if (secondWeaponId is { } second && !character.EquipWeapon(1, data.GetWeapon(second)))
            throw new InvalidOperationException($"{character.Name} nem használhatja ezt a fegyvert: '{second}'.");
        if (armorId is { } armor && !character.EquipArmor(data.GetArmor(armor)))
            throw new InvalidOperationException($"{character.Name} nem viselheti ezt a páncélt: '{armor}'.");
    }

    private static void ClearGeneratedEquipment(LiveCharacter character)
    {
        for (var index = 0; index < character.WeaponSlots.Count; index++)
            character.SetInventoryItem(InventorySlotKind.Weapon, index, null);
        character.SetInventoryItem(InventorySlotKind.Armor, 0, null);
        for (var index = 0; index < LiveCharacter.MaximumMagicItemCount; index++)
            character.SetInventoryItem(InventorySlotKind.MagicItem, index, null);
        for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
            character.SetInventoryItem(InventorySlotKind.Backpack, index, null);
    }

    private static int StableSeed(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value.ToUpperInvariant()) hash = hash * 31 + character;
            return hash;
        }
    }
}
