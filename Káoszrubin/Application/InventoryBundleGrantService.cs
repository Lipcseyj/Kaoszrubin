using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application;

public sealed record InventoryBundleEntry(IItemDefinition Item, int Quantity);

/// <summary>Több karakternek csak akkor oszt ki teljes csomagot, ha mindenkinél elfér.</summary>
public static class InventoryBundleGrantService
{
    public static bool TryGrant(IReadOnlyList<LiveCharacter> recipients,
        IReadOnlyList<InventoryBundleEntry> bundle, out IReadOnlyList<string> lackingSpace)
    {
        var lacking = recipients.Where(character => !CanFit(character, bundle))
            .Select(character => character.Name).ToArray();
        lackingSpace = lacking;
        if (lacking.Length > 0) return false;

        foreach (var character in recipients)
        foreach (var entry in bundle)
        for (var count = 0; count < entry.Quantity; count++)
            if (!character.AddToBackpack(entry.Item))
                throw new InvalidOperationException("Az előzetesen ellenőrzött ellátmánykiosztás váratlanul meghiúsult.");
        return true;
    }

    private static bool CanFit(LiveCharacter character, IReadOnlyList<InventoryBundleEntry> bundle)
    {
        var freeSlots = character.Backpack.Count(item => item is null);
        var newSlotsNeeded = 0;
        foreach (var group in bundle.GroupBy(entry => entry.Item.Id, StringComparer.OrdinalIgnoreCase))
        {
            var quantity = group.Sum(entry => Math.Max(0, entry.Quantity));
            var existingCapacity = Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
                .Where(index => string.Equals(character.Backpack[index]?.Id, group.Key,
                    StringComparison.OrdinalIgnoreCase))
                .Sum(index => LiveCharacter.MaximumBackpackStackSize -
                              character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index));
            var remainder = Math.Max(0, quantity - existingCapacity);
            newSlotsNeeded += (remainder + LiveCharacter.MaximumBackpackStackSize - 1) /
                              LiveCharacter.MaximumBackpackStackSize;
        }
        return newSlotsNeeded <= freeSlots;
    }
}
