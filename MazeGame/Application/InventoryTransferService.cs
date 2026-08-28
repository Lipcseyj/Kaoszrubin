using MazeGame.Domain.Characters;
using MazeGame.Domain.Inventory;

namespace MazeGame.Application;

public sealed record InventoryTransferResult(string SourceItemName, string? DisplacedItemName);

/// <summary>Revízióellenőrzött, töltetmegőrző, atomi inventory-slotcsere.</summary>
public static class InventoryTransferService
{
    public static bool Validate(Party party, InventoryTransferCommand command, out string error) =>
        TryCreatePlan(party, command, out _, out error);

    public static bool TryExecute(Party party, InventoryTransferCommand command,
        out InventoryTransferResult result, out string error)
    {
        if (!TryCreatePlan(party, command, out var plan, out error))
        {
            result = null!;
            return false;
        }
        foreach (var entry in plan.Changes) entry.Key.ApplyInventoryChanges(entry.Value.ToArray());
        result = new InventoryTransferResult(plan.SourceItem.Name, plan.DisplacedItem?.Name);
        return true;
    }

    private static bool TryCreatePlan(Party party, InventoryTransferCommand command,
        out InventoryTransferPlan plan, out string error)
    {
        var source = party.Members.FirstOrDefault(character => character.Id == command.CharacterId);
        var destination = party.Members.FirstOrDefault(character => character.Id == command.DestinationCharacterId);
        if (source is null || destination is null)
            return Fail("Az inventory-command egyik karaktere nem tagja a partinak.", out plan, out error);
        if (!IsValidSlotAddress(command.SourceKind, command.SourceIndex) ||
            !IsValidSlotAddress(command.DestinationKind, command.DestinationIndex) ||
            command.CharacterId == command.DestinationCharacterId && command.SourceKind == command.DestinationKind &&
            command.SourceIndex == command.DestinationIndex)
            return Fail("Az inventory-command slotcíme érvénytelen.", out plan, out error);
        if (command.ExpectedSourceRevision != source.InventoryRevision ||
            command.ExpectedDestinationRevision != destination.InventoryRevision)
            return Fail("Az inventory azóta megváltozott; friss snapshot szükséges.", out plan, out error);

        var sourceItem = source.GetInventoryItem(command.SourceKind, command.SourceIndex);
        if (sourceItem is null) return Fail("A forrásslot üres.", out plan, out error);
        var sourceCharges = source.GetInventoryItemCharges(command.SourceKind, command.SourceIndex);
        var sourceQuantity = source.GetInventoryItemQuantity(command.SourceKind, command.SourceIndex);
        var displaced = destination.GetInventoryItem(command.DestinationKind, command.DestinationIndex);
        var displacedCharges = destination.GetInventoryItemCharges(command.DestinationKind, command.DestinationIndex);
        var displacedQuantity = destination.GetInventoryItemQuantity(command.DestinationKind, command.DestinationIndex);
        var changes = new Dictionary<LiveCharacter, List<InventorySlotChange>>();

        var compatibleStack = command.DestinationKind == InventorySlotKind.Backpack && displaced is not null &&
            string.Equals(sourceItem.Id, displaced.Id, StringComparison.OrdinalIgnoreCase) &&
            sourceCharges == displacedCharges && displacedQuantity < LiveCharacter.MaximumBackpackStackSize;
        if (compatibleStack)
        {
            var moved = Math.Min(sourceQuantity, LiveCharacter.MaximumBackpackStackSize - displacedQuantity);
            AddChange(changes, source, new InventorySlotChange(command.SourceKind, command.SourceIndex,
                sourceQuantity == moved ? null : sourceItem, sourceCharges, sourceQuantity - moved));
            AddChange(changes, destination, new InventorySlotChange(command.DestinationKind,
                command.DestinationIndex, displaced, displacedCharges, displacedQuantity + moved));
        }
        else if (command.SourceKind == InventorySlotKind.Backpack && sourceQuantity > 1 &&
                 command.DestinationKind != InventorySlotKind.Backpack)
        {
            if (displaced is not null)
                return Fail("Kötegből csak üres felszereléshelyre tehető egy tárgy.", out plan, out error);
            AddChange(changes, source, new InventorySlotChange(command.SourceKind, command.SourceIndex,
                sourceItem, sourceCharges, sourceQuantity - 1));
            AddChange(changes, destination, new InventorySlotChange(command.DestinationKind,
                command.DestinationIndex, sourceItem, sourceCharges, 1));
        }
        else
        {
            AddChange(changes, source, new InventorySlotChange(command.SourceKind, command.SourceIndex,
                displaced, displacedCharges, displacedQuantity));
            AddChange(changes, destination, new InventorySlotChange(command.DestinationKind,
                command.DestinationIndex, sourceItem, sourceCharges, sourceQuantity));
        }
        if (changes.Any(entry => !entry.Key.CanApplyInventoryChanges(entry.Value.ToArray())))
            return Fail("A tárgyak nem helyezhetők el a megadott slotokban.", out plan, out error);

        plan = new InventoryTransferPlan(changes, sourceItem, displaced);
        error = string.Empty;
        return true;
    }

    public static bool IsValidSlotAddress(InventorySlotKind kind, int index) => kind switch
    {
        InventorySlotKind.Weapon => index is >= 0 and < 2,
        InventorySlotKind.Armor => index == 0,
        InventorySlotKind.MagicItem => index is >= 0 and < LiveCharacter.MaximumMagicItemCount,
        InventorySlotKind.Backpack => index is >= 0 and < LiveCharacter.MaximumBackpackItemCount,
        _ => false
    };

    private static void AddChange(Dictionary<LiveCharacter, List<InventorySlotChange>> changes,
        LiveCharacter character, InventorySlotChange change)
    {
        if (!changes.TryGetValue(character, out var characterChanges))
            changes[character] = characterChanges = [];
        characterChanges.Add(change);
    }

    private static bool Fail(string reason, out InventoryTransferPlan plan, out string error)
    {
        plan = null!;
        error = reason;
        return false;
    }

    private sealed record InventoryTransferPlan(Dictionary<LiveCharacter, List<InventorySlotChange>> Changes,
        IItemDefinition SourceItem, IItemDefinition? DisplacedItem);
}
