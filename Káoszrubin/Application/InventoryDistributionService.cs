using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application;

public sealed record InventoryDistributionResult(string ItemName, int DistributedQuantity,
    int RemainingSourceQuantity, IReadOnlyList<string> RecipientNames);

/// <summary>Elfogyasztható hátizsákköteg revíziózott, veszteségmentes, körkörös szétosztása.</summary>
public static class InventoryDistributionService
{
    public static bool Validate(Party party, DistributeInventoryStackCommand command, out string error) =>
        TryCreatePlan(party, command, out _, out error);

    public static bool TryExecute(Party party, DistributeInventoryStackCommand command,
        out InventoryDistributionResult result, out string error)
    {
        if (!TryCreatePlan(party, command, out var plan, out error))
        {
            result = null!;
            return false;
        }
        foreach (var characterPlan in plan.CharacterPlans)
            characterPlan.Character.ApplyInventoryChanges(characterPlan.Changes);
        result = new InventoryDistributionResult(plan.Item.Name, plan.DistributedQuantity,
            plan.RemainingSourceQuantity, plan.RecipientNames);
        return true;
    }

    private static bool TryCreatePlan(Party party, DistributeInventoryStackCommand command,
        out DistributionPlan plan, out string error)
    {
        var members = party.Members.ToArray();
        var sourceCharacterIndex = Array.FindIndex(members, member => member.Id == command.CharacterId);
        if (sourceCharacterIndex < 0)
            return Fail("A karakter nem tagja a partinak.", out plan, out error);
        var source = members[sourceCharacterIndex];
        if (command.BackpackIndex is < 0 or >= LiveCharacter.MaximumBackpackItemCount)
            return Fail("A hátizsákhely érvénytelen.", out plan, out error);
        if (source.InventoryRevision != command.ExpectedInventoryRevision)
            return Fail("Az inventory azóta megváltozott; friss snapshot szükséges.", out plan, out error);
        if (source.GetInventoryItem(InventorySlotKind.Backpack, command.BackpackIndex) is not MiscItemDefinition
            { Effect: not ConsumableEffect.None } item)
            return Fail("Csak elfogyasztható hátizsáktárgy osztható szét.", out plan, out error);
        var originalQuantity = source.GetInventoryItemQuantity(InventorySlotKind.Backpack, command.BackpackIndex);
        if (originalQuantity < 2)
            return Fail("A szétosztáshoz legalább két darab szükséges.", out plan, out error);
        if (members.Length < 2)
            return Fail("Nincs másik partitag, akinek a tárgy kiosztható.", out plan, out error);

        var states = members.Select(character => Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
            .Select(index => new SlotState(character.GetInventoryItem(InventorySlotKind.Backpack, index),
                character.GetInventoryItemCharges(InventorySlotKind.Backpack, index),
                character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index))).ToArray()).ToArray();
        states[sourceCharacterIndex][command.BackpackIndex].Quantity = 1;
        var allocations = new int[members.Length];
        var remaining = originalQuantity - 1;
        var cursor = (sourceCharacterIndex + 1) % members.Length;
        while (remaining > 0)
        {
            var allocated = false;
            for (var attempt = 0; attempt < members.Length; attempt++)
            {
                var memberIndex = (cursor + attempt) % members.Length;
                var destination = FindDestination(states[memberIndex], item);
                if (destination < 0) continue;
                var slot = states[memberIndex][destination];
                if (slot.Item is null)
                {
                    slot.Item = item;
                    slot.Charges = 0;
                    slot.Quantity = 1;
                }
                else slot.Quantity++;
                allocations[memberIndex]++;
                remaining--;
                cursor = (memberIndex + 1) % members.Length;
                allocated = true;
                break;
            }
            if (!allocated) break;
        }
        states[sourceCharacterIndex][command.BackpackIndex].Quantity += remaining;

        var characterPlans = new List<CharacterPlan>();
        for (var memberIndex = 0; memberIndex < members.Length; memberIndex++)
        {
            var changes = new List<InventorySlotChange>();
            for (var slotIndex = 0; slotIndex < LiveCharacter.MaximumBackpackItemCount; slotIndex++)
            {
                var state = states[memberIndex][slotIndex];
                var originalItem = members[memberIndex].GetInventoryItem(InventorySlotKind.Backpack, slotIndex);
                var originalCharges = members[memberIndex].GetInventoryItemCharges(InventorySlotKind.Backpack, slotIndex);
                var originalSlotQuantity = members[memberIndex].GetInventoryItemQuantity(InventorySlotKind.Backpack, slotIndex);
                if (ReferenceEquals(state.Item, originalItem) && state.Charges == originalCharges &&
                    state.Quantity == originalSlotQuantity) continue;
                changes.Add(new InventorySlotChange(InventorySlotKind.Backpack, slotIndex, state.Item,
                    state.Charges, state.Quantity));
            }
            if (changes.Count == 0) continue;
            if (!members[memberIndex].CanApplyInventoryChanges(changes.ToArray()))
                return Fail("A tárgyak nem oszthatók szét a party hátizsákjaiban.", out plan, out error);
            characterPlans.Add(new CharacterPlan(members[memberIndex], changes.ToArray()));
        }
        var distributed = allocations.Where((_, index) => index != sourceCharacterIndex).Sum();
        if (distributed == 0)
            return Fail("A többi partitag hátizsákjában nincs hely a tárgy számára.", out plan, out error);
        plan = new DistributionPlan(item, distributed,
            states[sourceCharacterIndex][command.BackpackIndex].Quantity,
            allocations.Select((count, index) => (count, index))
                .Where(entry => entry.count > 0 && entry.index != sourceCharacterIndex)
                .Select(entry => members[entry.index].Name).ToArray(), characterPlans);
        error = string.Empty;
        return true;
    }

    private static int FindDestination(IReadOnlyList<SlotState> slots, IItemDefinition item)
    {
        for (var index = 0; index < slots.Count; index++)
            if (slots[index].Item is { } existing &&
                string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase) &&
                slots[index].Charges == 0 && slots[index].Quantity < LiveCharacter.MaximumBackpackStackSize)
                return index;
        for (var index = 0; index < slots.Count; index++)
            if (slots[index].Item is null) return index;
        return -1;
    }

    private static bool Fail(string reason, out DistributionPlan plan, out string error)
    {
        plan = null!;
        error = reason;
        return false;
    }

    private sealed class SlotState(IItemDefinition? item, int charges, int quantity)
    {
        public IItemDefinition? Item { get; set; } = item;
        public int Charges { get; set; } = charges;
        public int Quantity { get; set; } = quantity;
    }
    private sealed record CharacterPlan(LiveCharacter Character, InventorySlotChange[] Changes);
    private sealed record DistributionPlan(MiscItemDefinition Item, int DistributedQuantity,
        int RemainingSourceQuantity, IReadOnlyList<string> RecipientNames,
        IReadOnlyList<CharacterPlan> CharacterPlans);
}
