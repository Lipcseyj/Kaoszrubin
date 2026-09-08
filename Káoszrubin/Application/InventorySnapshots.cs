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
    string Description = "", int BasePrice = 0, int MagicPower = 0, int Quantity = 1,
    Guid InstanceId = default, bool IsIdentified = true, int UnidentifiedSellPrice = 0,
    string? CurseId = null, ItemCurseEffect CurseEffect = ItemCurseEffect.None, int CurseValue = 0,
    int CurseStrength = 0, bool IsCurseActivated = false, CharacterId? BoundCharacterId = null,
    bool IsPurified = false, int MaximumDurability = 0, int DurabilityDamage = 0);

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
            var state = character.GetInventoryItemState(kind, index);
            if (item is null) slots.Add(new InventorySlotSnapshot(kind, index, null));
            else
            {
                var identified = state?.IsIdentified != false;
                slots.Add(new InventorySlotSnapshot(kind, index, new InventoryItemSnapshot(
                    identified ? item.Id : string.Empty,
                    ItemIdentificationRules.DisplayName(item, identified) +
                    (state is { HasCurse: true } && (identified || state.Value.IsCurseActivated) ? " ☠" : string.Empty), item.Category, item.Rarity,
                    identified ? character.GetInventoryItemCharges(kind, index) : 0,
                    identified && item is MagicItemDefinition magic ? magic.MaximumCharges : 0,
                    item is WeaponDefinition { IsTwoHanded: true },
                    identified ? item.Description : $"{ItemIdentificationRules.AuraStrength(item)} mágikus aura",
                    identified ? item.BasePrice : 0, identified ? item.MagicPower : 0,
                    character.GetInventoryItemQuantity(kind, index), state?.InstanceId ?? Guid.Empty, identified,
                    identified ? 0 : Math.Max(1, item.BasePrice / 4),
                    identified ? state?.CurseId : null,
                    identified ? state?.CurseEffect ?? ItemCurseEffect.None : ItemCurseEffect.None,
                    identified ? state?.CurseValue ?? 0 : 0,
                    identified ? state?.CurseStrength ?? 0 : 0,
                    state?.IsCurseActivated == true,
                    state?.IsCurseActivated == true ? state?.BoundCharacterId : null, state?.IsPurified == true,
                    EquipmentDurabilityRules.MaximumDurability(item),
                    Math.Max(0, state?.DurabilityDamage ?? 0))));
            }
        }
    }
}
