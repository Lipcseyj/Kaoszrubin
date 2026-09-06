using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application;

public sealed record InventoryTransferResult(string SourceItemName, string? DisplacedItemName,
    IReadOnlyList<string>? CurseActivations = null);

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
        var activations = plan.Changes.SelectMany(entry => entry.Value
            .Where(change => change.Item is not null &&
                             change.State is { HasCurse: true, IsCurseActivated: false } &&
                             IsActiveEquipmentSlot(change.Kind, change.Index))
            .Select(_ => $"☠ {entry.Key.Name}: a tárgy átka aktiválódott és nem vehető le az átok megtöréséig."))
            .ToArray();
        result = new InventoryTransferResult(
            ItemIdentificationRules.DisplayName(plan.SourceItem, plan.SourceIdentified),
            plan.DisplacedItem is null ? null :
                ItemIdentificationRules.DisplayName(plan.DisplacedItem, plan.DisplacedIdentified), activations);
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
        if (source == destination && command.SourceKind == InventorySlotKind.Weapon && command.SourceIndex == 2 &&
            command.DestinationKind == InventorySlotKind.Weapon && command.DestinationIndex == 0)
        {
            if (source.ReserveWeaponSwapChanges() is not { } swapChanges)
                return Fail("A fegyvercsere nem lehetséges; kétkezes fegyverhez a másik kéz tárgyának üres hátizsákhely kell.", out plan, out error);
            plan = new InventoryTransferPlan(new() { [source] = swapChanges.ToList() }, sourceItem,
                source.WeaponSlots[0], source.IsInventoryItemIdentified(command.SourceKind, command.SourceIndex),
                source.IsInventoryItemIdentified(InventorySlotKind.Weapon, 0));
            error = string.Empty;
            return true;
        }
        var sourceCharges = source.GetInventoryItemCharges(command.SourceKind, command.SourceIndex);
        var sourceQuantity = source.GetInventoryItemQuantity(command.SourceKind, command.SourceIndex);
        var sourceState = source.GetInventoryItemState(command.SourceKind, command.SourceIndex);
        var displaced = destination.GetInventoryItem(command.DestinationKind, command.DestinationIndex);
        var displacedCharges = destination.GetInventoryItemCharges(command.DestinationKind, command.DestinationIndex);
        var displacedQuantity = destination.GetInventoryItemQuantity(command.DestinationKind, command.DestinationIndex);
        var displacedState = destination.GetInventoryItemState(command.DestinationKind, command.DestinationIndex);
        var changes = new Dictionary<LiveCharacter, List<InventorySlotChange>>();
        if (!CharacterBoundItemRules.CanBeHeldBy(destination, sourceItem) ||
            !CharacterBoundItemRules.CanBeHeldBy(source, displaced))
            return Fail("A családi ereklyét csak a jogos tulajdonosa használhatja.", out plan, out error);
        if (command.DestinationKind == InventorySlotKind.Weapon && command.DestinationIndex == 1 &&
            sourceItem is WeaponDefinition offhand &&
            !DualWieldingRules.CanEquipOffhand(destination, destination.WeaponSlots[0], offhand))
            return Fail("A mellékkézbe csak használható pajzs, illetve Kétfegyveres harccal és megfelelő jártassággal tőr vagy kard tehető.",
                out plan, out error);

        var compatibleStack = command.DestinationKind == InventorySlotKind.Backpack && displaced is not null &&
            string.Equals(sourceItem.Id, displaced.Id, StringComparison.OrdinalIgnoreCase) &&
            sourceState?.IsIdentified == true && displacedState?.IsIdentified == true &&
            sourceCharges == displacedCharges && displacedQuantity < LiveCharacter.MaximumBackpackStackSize;
        if (compatibleStack)
        {
            var moved = Math.Min(sourceQuantity, LiveCharacter.MaximumBackpackStackSize - displacedQuantity);
            AddChange(changes, source, new InventorySlotChange(command.SourceKind, command.SourceIndex,
                sourceQuantity == moved ? null : sourceItem, sourceCharges, sourceQuantity - moved,
                sourceQuantity == moved ? null : sourceState));
            AddChange(changes, destination, new InventorySlotChange(command.DestinationKind,
                command.DestinationIndex, displaced, displacedCharges, displacedQuantity + moved, displacedState));
        }
        else if (command.SourceKind == InventorySlotKind.Backpack && sourceQuantity > 1 &&
                 command.DestinationKind != InventorySlotKind.Backpack)
        {
            if (displaced is not null)
                return Fail("Kötegből csak üres felszereléshelyre tehető egy tárgy.", out plan, out error);
            AddChange(changes, source, new InventorySlotChange(command.SourceKind, command.SourceIndex,
                sourceItem, sourceCharges, sourceQuantity - 1, sourceState));
            AddChange(changes, destination, new InventorySlotChange(command.DestinationKind,
                command.DestinationIndex, sourceItem, sourceCharges, 1,
                sourceState is { } state ? state with { InstanceId = Guid.NewGuid() } : null));
        }
        else
        {
            AddChange(changes, source, new InventorySlotChange(command.SourceKind, command.SourceIndex,
                displaced, displacedCharges, displacedQuantity, displacedState));
            AddChange(changes, destination, new InventorySlotChange(command.DestinationKind,
                command.DestinationIndex, sourceItem, sourceCharges, sourceQuantity, sourceState));
        }
        if (changes.Any(entry => !entry.Key.CanApplyInventoryChanges(entry.Value.ToArray())))
            return Fail("A tárgyak nem helyezhetők el a megadott slotokban.", out plan, out error);

        plan = new InventoryTransferPlan(changes, sourceItem, displaced,
            sourceState?.IsIdentified != false, displacedState?.IsIdentified != false);
        error = string.Empty;
        return true;
    }

    public static bool IsValidSlotAddress(InventorySlotKind kind, int index) => kind switch
    {
        InventorySlotKind.Weapon => index is >= 0 and < 3,
        InventorySlotKind.Armor => index == 0,
        InventorySlotKind.MagicItem => index is >= 0 and < LiveCharacter.MaximumMagicItemCount,
        InventorySlotKind.Backpack => index is >= 0 and < LiveCharacter.MaximumBackpackItemCount,
        _ => false
    };

    private static bool IsActiveEquipmentSlot(InventorySlotKind kind, int index) => kind switch
    {
        InventorySlotKind.Weapon => index is 0 or 1,
        InventorySlotKind.Armor => index == 0,
        InventorySlotKind.MagicItem => index is >= 0 and < LiveCharacter.MaximumMagicItemCount,
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
        IItemDefinition SourceItem, IItemDefinition? DisplacedItem, bool SourceIdentified,
        bool DisplacedIdentified);
}
