using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Application;

public sealed record CharacterInventorySnapshot(CharacterId CharacterId, long Revision,
    IReadOnlyList<InventorySlotSnapshot> Slots);

public sealed record InventorySlotSnapshot(InventorySlotKind Kind, int Index, InventoryItemSnapshot? Item);

/// <summary>A slot hiteles tartalma; a részletes statisztikát a verzióazonos katalógusból kell feloldani.</summary>
public sealed record InventoryItemSnapshot(string DefinitionId, string Name, ItemCategory Category,
    ItemRarity Rarity, int Charges, int MaximumCharges, bool IsTwoHanded = false,
    string Description = "", int BasePrice = 0, int MagicPower = 0, int Quantity = 1);

public static class InventorySnapshotProjector
{
    public static CharacterInventorySnapshot Create(LiveCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var slots = new List<InventorySlotSnapshot>();
        AddSlots(character, slots, InventorySlotKind.Weapon, 3);
        AddSlots(character, slots, InventorySlotKind.Armor, 1);
        AddSlots(character, slots, InventorySlotKind.MagicItem, LiveCharacter.MaximumMagicItemCount);
        AddSlots(character, slots, InventorySlotKind.Backpack, LiveCharacter.MaximumBackpackItemCount);
        return new CharacterInventorySnapshot(character.Id, character.InventoryRevision, slots);
    }

    private static void AddSlots(LiveCharacter character, ICollection<InventorySlotSnapshot> slots,
        InventorySlotKind kind, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var item = character.GetInventoryItem(kind, index);
            slots.Add(new InventorySlotSnapshot(kind, index, item is null ? null : new InventoryItemSnapshot(
                item.Id, item.Name, item.Category, item.Rarity,
                character.GetInventoryItemCharges(kind, index),
                item is MagicItemDefinition magic ? magic.MaximumCharges : 0,
                item is WeaponDefinition { IsTwoHanded: true }, item.Description, item.BasePrice, item.MagicPower,
                character.GetInventoryItemQuantity(kind, index))));
        }
    }
}
