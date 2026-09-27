using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application;

internal static class RestProvisionService
{
    public static bool IsEdibleMonster(string enemyDefinitionId) =>
        string.Equals(enemyDefinitionId, MonsterIds.Vadkan, StringComparison.OrdinalIgnoreCase);

    public static string ConsumeRawMeat(LiveCharacter character, StatusDefinition poisoned,
        int foodAmount)
    {
        var before = character.FoodLevel;
        character.RestoreFood(foodAmount);
        character.AddStatus(poisoned);
        return $"élelem +{character.FoodLevel - before}; ☠ nyersen elfogyasztva mérgezést okozott";
    }

    public static int CookRawMeat(IEnumerable<LiveCharacter> party, IItemDefinition cookedMeat)
    {
        var cookedCount = 0;
        foreach (var character in party)
        {
            var changes = new List<InventorySlotChange>();
            for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
            {
                var item = character.GetInventoryItem(InventorySlotKind.Backpack, index);
                if (!string.Equals(item?.Id, MiscItemIds.RawMeat, StringComparison.OrdinalIgnoreCase)) continue;
                var quantity = character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index);
                changes.Add(new InventorySlotChange(InventorySlotKind.Backpack, index, cookedMeat,
                    Quantity: quantity));
                cookedCount += quantity;
            }
            if (changes.Count > 0) character.ApplyInventoryChanges(changes.ToArray());
        }
        return cookedCount;
    }

    public static string CookingMessage(int cookedCount) => cookedCount <= 0
        ? string.Empty
        : $"🔥 Megsütöttük a nyers húst: {cookedCount} adag sült hús készült.";
}
