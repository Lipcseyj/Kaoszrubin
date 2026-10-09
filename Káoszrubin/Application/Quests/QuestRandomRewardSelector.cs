using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>Az NPC kasztjához és a jelenlegi pályaszinthez igazított, súlyozott questjutalom-sorsolás.</summary>
public static class QuestRandomRewardSelector
{
    public const int MagicExperienceThreshold = 4_000;
    public const int LegendaryExperienceThreshold = 10_000;
    private const int ExperiencePerMagicPower = 1_500;

    public static IItemDefinition? Select(IEnumerable<IItemDefinition> source, QuestDefinition quest,
        string npcClassId, int mazeLevel, Random random)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(random);

        var maximumRarity = quest.ExperienceReward >= LegendaryExperienceThreshold ? ItemRarity.Legendary :
            quest.ExperienceReward >= MagicExperienceThreshold ? ItemRarity.Magic : ItemRarity.Normal;
        var maximumPrice = Math.Max(80, quest.ExperienceReward * 2);
        var levelMagicPower = mazeLevel switch { < 5 => 0, < 11 => 1, < 18 => 2, _ => 3 };
        var maximumMagicPower = Math.Min(Math.Max(0, quest.ExperienceReward / ExperiencePerMagicPower),
            levelMagicPower + 1);
        var candidates = source.Where(item => item.Rarity <= maximumRarity &&
                item.BasePrice <= maximumPrice && item.MagicPower <= maximumMagicPower)
            .Select(item => (Item: item, Weight: Weight(item, npcClassId, mazeLevel, levelMagicPower)))
            .Where(candidate => candidate.Weight > 0)
            .ToArray();
        if (candidates.Length == 0) return null;

        var totalWeight = candidates.Sum(candidate => candidate.Weight);
        var roll = random.Next(totalWeight);
        foreach (var candidate in candidates)
        {
            if (roll < candidate.Weight) return candidate.Item;
            roll -= candidate.Weight;
        }
        return candidates[^1].Item;
    }

    public static int Weight(IItemDefinition item, string npcClassId, int mazeLevel)
    {
        var levelMagicPower = mazeLevel switch { < 5 => 0, < 11 => 1, < 18 => 2, _ => 3 };
        return Weight(item, npcClassId, mazeLevel, levelMagicPower);
    }

    private static int Weight(IItemDefinition item, string npcClassId, int mazeLevel, int levelMagicPower)
    {
        var rarityWeight = item.Rarity switch
        {
            ItemRarity.Normal => 10,
            ItemRarity.Magic => 4,
            ItemRarity.Legendary => 1,
            _ => 1
        };
        var classWeight = ClassWeight(item, npcClassId);
        var powerDistance = Math.Abs(item.MagicPower - levelMagicPower);
        var levelPowerWeight = powerDistance switch { 0 => 4, 1 => 2, _ => 1 };
        var targetPrice = 100 + Math.Max(1, mazeLevel) * 120;
        var levelPriceWeight = item.BasePrice >= targetPrice / 2 && item.BasePrice <= targetPrice * 3 / 2 ? 3 :
            item.BasePrice >= targetPrice / 4 && item.BasePrice <= targetPrice * 2 ? 2 : 1;
        return rarityWeight * classWeight * levelPowerWeight * levelPriceWeight;
    }

    private static int ClassWeight(IItemDefinition item, string npcClassId) => item switch
    {
        WeaponDefinition weapon => weapon.AllowedClassIds.Contains(npcClassId) ? 6 : 1,
        ArmorDefinition armor => armor.AllowedClassIds.Contains(npcClassId) ? 5 : 1,
        MagicItemDefinition magicItem => magicItem.AllowedClassIds.Contains(npcClassId)
            ? npcClassId is CharacterClassIds.Mágus or CharacterClassIds.Pap ? 7 : 4
            : 1,
        MiscItemDefinition misc => MiscClassWeight(misc, npcClassId),
        _ => 1
    };

    private static int MiscClassWeight(MiscItemDefinition item, string npcClassId) => npcClassId switch
    {
        CharacterClassIds.Mágus when item.Effect == ConsumableEffect.RestoreMana => 8,
        CharacterClassIds.Pap when item.Effect is ConsumableEffect.Heal or ConsumableEffect.CureDisease or
            ConsumableEffect.CurePoison => 6,
        CharacterClassIds.Tolvaj when item.Effect is ConsumableEffect.CurePoison or ConsumableEffect.StopBleeding or
            ConsumableEffect.Vision => 6,
        CharacterClassIds.Harcos or CharacterClassIds.Barbár or CharacterClassIds.Lovag
            when item.Effect is ConsumableEffect.Heal or ConsumableEffect.RepairEquipment => 5,
        _ => 2
    };
}
