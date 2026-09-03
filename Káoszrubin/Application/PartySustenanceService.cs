using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application;

public enum NpcComplaintKind { Hunger, Thirst, Injured }

public sealed class PartySustenanceService
{
    private readonly GameDataCatalog _gameData;
    private readonly Random _random;

    public PartySustenanceService(GameDataCatalog gameData, Random random)
    {
        _gameData = gameData;
        _random = random;
    }

    public void DrainNeeds(
        IEnumerable<LiveCharacter> characters,
        Func<LiveCharacter, bool> isAutonomousNpc,
        Action<LiveCharacter, NpcComplaintKind, int, int> onLogNewZeroNeed,
        Action<LiveCharacter> onTryNpcUseConsumables)
    {
        var hungry = _gameData.GetStatus(CharacterStatusIds.Hungry);
        var thirsty = _gameData.GetStatus(CharacterStatusIds.Thirsty);
        foreach (var character in characters.Where(character => character.IsAlive))
        {
            var foodBefore = character.FoodLevel;
            var waterBefore = character.WaterLevel;
            var foodLoss = 2 + character.MaximumVitality / 60;
            character.ConsumeFood(foodLoss);
            var waterLoss = 2;
            if (character.CurrentVitality < character.MaximumVitality) waterLoss++;
            if (character.CurrentVitality * 2 < character.MaximumVitality) waterLoss++;
            character.ConsumeWater(waterLoss);
            character.SynchronizeNeedStatuses(hungry, thirsty);
            if (isAutonomousNpc(character))
            {
                onLogNewZeroNeed(character, NpcComplaintKind.Hunger, foodBefore, character.FoodLevel);
                onLogNewZeroNeed(character, NpcComplaintKind.Thirst, waterBefore, character.WaterLevel);
                onTryNpcUseConsumables(character);
            }
        }
    }

    public int DrainNeedsAfterBattle(
        LiveCharacter character,
        int monsterTier,
        Func<LiveCharacter, bool> isAutonomousNpc,
        Action<LiveCharacter, NpcComplaintKind, int, int> onLogNewZeroNeed)
    {
        var foodBefore = character.FoodLevel;
        var waterBefore = character.WaterLevel;
        var loss = _random.Next(1, 6) + Math.Clamp(monsterTier, 1, 5);
        character.ConsumeFood(loss);
        character.ConsumeWater(loss);
        character.SynchronizeNeedStatuses(
            _gameData.GetStatus(CharacterStatusIds.Hungry),
            _gameData.GetStatus(CharacterStatusIds.Thirsty));
        if (isAutonomousNpc(character))
        {
            onLogNewZeroNeed(character, NpcComplaintKind.Hunger, foodBefore, character.FoodLevel);
            onLogNewZeroNeed(character, NpcComplaintKind.Thirst, waterBefore, character.WaterLevel);
        }
        return loss;
    }

    public void DrainNeedsAfterTeamBattle(
        LiveCharacter character,
        int cycles,
        Func<LiveCharacter, bool> isAutonomousNpc,
        Action<LiveCharacter, NpcComplaintKind, int, int> onLogNewZeroNeed)
    {
        var foodBefore = character.FoodLevel;
        var waterBefore = character.WaterLevel;
        var loss = Math.Max(1, cycles);
        character.ConsumeFood(loss);
        character.ConsumeWater(loss);
        character.SynchronizeNeedStatuses(
            _gameData.GetStatus(CharacterStatusIds.Hungry),
            _gameData.GetStatus(CharacterStatusIds.Thirsty));
        if (!isAutonomousNpc(character)) return;
        onLogNewZeroNeed(character, NpcComplaintKind.Hunger, foodBefore, character.FoodLevel);
        onLogNewZeroNeed(character, NpcComplaintKind.Thirst, waterBefore, character.WaterLevel);
    }

    public static IEnumerable<(int Index, MiscItemDefinition Item)> BackpackConsumables(
        LiveCharacter character, ConsumableEffect effect) => Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
        .Select(index => (Index: index,
            Item: character.GetInventoryItem(InventorySlotKind.Backpack, index) as MiscItemDefinition))
        .Where(entry => entry.Item?.Effect == effect)
        .Select(entry => (entry.Index, entry.Item!));

    public static bool HasHealingPotion(LiveCharacter character) =>
        BackpackConsumables(character, ConsumableEffect.Heal).Any();
}
