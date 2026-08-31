using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application;

public sealed record FollowerStackTransferResult(string ItemName, int TransferredQuantity,
    int RemainingQuantity, string FollowerName);

/// <summary>Elfogyasztható hátizsákköteg kisebb felének atomi átadása egy követő NPC-nek.</summary>
public static class FollowerStackTransferService
{
    public static bool TryExecute(LiveCharacter source, LiveCharacter follower,
        GiveFollowerStackCommand command, out FollowerStackTransferResult result, out string error)
    {
        result = null!;
        if (source.Id != command.CharacterId || follower.Id != command.FollowerCharacterId)
            return Fail("A karakter vagy a követő azonosítója érvénytelen.", out error);
        if (source.InventoryRevision != command.ExpectedInventoryRevision ||
            follower.InventoryRevision != command.ExpectedFollowerInventoryRevision)
            return Fail("Az inventory azóta megváltozott; friss snapshot szükséges.", out error);
        if (command.BackpackIndex is < 0 or >= LiveCharacter.MaximumBackpackItemCount)
            return Fail("A hátizsákhely érvénytelen.", out error);
        var item = source.GetInventoryItem(InventorySlotKind.Backpack, command.BackpackIndex);
        if (item is not MiscItemDefinition { Effect: not ConsumableEffect.None })
            return Fail("Csak elfogyasztható hátizsáktárgy adható a követőnek.", out error);
        var quantity = source.GetInventoryItemQuantity(InventorySlotKind.Backpack, command.BackpackIndex);
        if (quantity < 2) return Fail("Legalább két darab kell a köteg megfelezéséhez.", out error);

        var transferred = quantity / 2;
        var remaining = quantity - transferred;
        var charges = source.GetInventoryItemCharges(InventorySlotKind.Backpack, command.BackpackIndex);
        var destination = Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount).FirstOrDefault(index =>
            follower.GetInventoryItem(InventorySlotKind.Backpack, index) is { } existing &&
            string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase) &&
            follower.GetInventoryItemCharges(InventorySlotKind.Backpack, index) == charges &&
            follower.GetInventoryItemQuantity(InventorySlotKind.Backpack, index) + transferred <=
            LiveCharacter.MaximumBackpackStackSize, -1);
        if (destination < 0)
            destination = Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount).FirstOrDefault(index =>
                follower.GetInventoryItem(InventorySlotKind.Backpack, index) is null, -1);
        if (destination < 0) return Fail("A követő hátizsákjában nincs hely a fél kötegnek.", out error);

        var sourceChange = new InventorySlotChange(InventorySlotKind.Backpack, command.BackpackIndex,
            item, charges, remaining);
        var destinationQuantity = follower.GetInventoryItemQuantity(InventorySlotKind.Backpack, destination) + transferred;
        var destinationChange = new InventorySlotChange(InventorySlotKind.Backpack, destination,
            item, charges, destinationQuantity);
        if (!source.CanApplyInventoryChanges(sourceChange) || !follower.CanApplyInventoryChanges(destinationChange))
            return Fail("A köteg nem adható át a követőnek.", out error);
        source.ApplyInventoryChanges(sourceChange);
        follower.ApplyInventoryChanges(destinationChange);
        result = new FollowerStackTransferResult(item.Name, transferred, remaining, follower.Name);
        error = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string error) { error = message; return false; }
}
