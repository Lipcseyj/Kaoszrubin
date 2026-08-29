using MazeGame.Domain.Characters;
using MazeGame.Domain.Inventory;

namespace MazeGame.Application;

public sealed record InventoryStackSplitResult(string ItemName, int RemainingQuantity, int NewQuantity,
    int DestinationIndex);

/// <summary>Revízióellenőrzött, atomi hátizsákköteg-felezés.</summary>
public static class InventoryStackService
{
    public static bool Validate(Party party, SplitInventoryStackCommand command, out string error) =>
        TryCreatePlan(party, command, out _, out error);

    public static bool TryExecute(Party party, SplitInventoryStackCommand command,
        out InventoryStackSplitResult result, out string error)
    {
        if (!TryCreatePlan(party, command, out var plan, out error))
        {
            result = null!;
            return false;
        }

        plan.Character.ApplyInventoryChanges(plan.Changes);
        result = new InventoryStackSplitResult(plan.Item.Name, plan.RemainingQuantity, plan.NewQuantity,
            plan.DestinationIndex);
        return true;
    }

    private static bool TryCreatePlan(Party party, SplitInventoryStackCommand command,
        out SplitPlan plan, out string error)
    {
        var character = party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        if (character is null) return Fail("A karakter nem tagja a partinak.", out plan, out error);
        if (command.BackpackIndex is < 0 or >= LiveCharacter.MaximumBackpackItemCount)
            return Fail("A hátizsákhely érvénytelen.", out plan, out error);
        if (character.InventoryRevision != command.ExpectedInventoryRevision)
            return Fail("Az inventory azóta megváltozott; friss snapshot szükséges.", out plan, out error);

        var item = character.GetInventoryItem(InventorySlotKind.Backpack, command.BackpackIndex);
        if (item is null) return Fail("A kijelölt hátizsákhely üres.", out plan, out error);
        var quantity = character.GetInventoryItemQuantity(InventorySlotKind.Backpack, command.BackpackIndex);
        if (quantity < 2) return Fail("A kijelölt tárgy nem több darabos köteg.", out plan, out error);

        var destinationIndex = Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
            .FirstOrDefault(index => character.GetInventoryItem(InventorySlotKind.Backpack, index) is null, -1);
        if (destinationIndex < 0)
            return Fail("A hátizsákban nincs üres hely a köteg felezéséhez.", out plan, out error);

        var newQuantity = quantity / 2;
        var remainingQuantity = quantity - newQuantity;
        var charges = character.GetInventoryItemCharges(InventorySlotKind.Backpack, command.BackpackIndex);
        InventorySlotChange[] changes =
        [
            new(InventorySlotKind.Backpack, command.BackpackIndex, item, charges, remainingQuantity),
            new(InventorySlotKind.Backpack, destinationIndex, item, charges, newQuantity)
        ];
        if (!character.CanApplyInventoryChanges(changes))
            return Fail("A köteg nem felezhető el a hátizsákban.", out plan, out error);

        plan = new SplitPlan(character, item, remainingQuantity, newQuantity, destinationIndex, changes);
        error = string.Empty;
        return true;
    }

    private static bool Fail(string reason, out SplitPlan plan, out string error)
    {
        plan = null!;
        error = reason;
        return false;
    }

    private sealed record SplitPlan(LiveCharacter Character, IItemDefinition Item, int RemainingQuantity,
        int NewQuantity, int DestinationIndex, InventorySlotChange[] Changes);
}
