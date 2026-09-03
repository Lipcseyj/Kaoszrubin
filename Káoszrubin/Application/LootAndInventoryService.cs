using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Application;

public sealed class LootAndInventoryService
{
    private readonly GameDataCatalog _gameData;
    private readonly Random _random;

    public LootAndInventoryService(GameDataCatalog gameData, Random random)
    {
        _gameData = gameData;
        _random = random;
    }

    public int AdjustedSearchChance(LiveCharacter character, int baseChance)
    {
        var chance = Math.Max(0, baseChance);
        if (CharacterClassRules.IsThief(character.CharacterClass.Id))
            chance = chance * _gameData.LootRules.ThiefChanceMultiplierPercent / 100;
        if (character.Race.HasTrait(RaceTraits.KeenSenses)) chance += 15;
        chance += character.EffectiveAbilities.Intelligence * _gameData.LootRules.IntelligenceChanceBonusPerPoint;
        return Math.Clamp(chance, 0, 100);
    }

    public IItemDefinition? RollEquipmentLoot(MonsterLootDefinition loot)
    {
        bool Eligible(IItemDefinition item) => item.Rarity >= loot.MinimumRarity &&
            item.Rarity <= loot.MaximumRarity && item.MagicPower <= loot.MaximumMagicPower &&
            item.BasePrice <= loot.MaximumBasePrice &&
            !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item);

        var categoryCandidates = new List<List<IItemDefinition>>();
        if (loot.CanDropWeapon)
            categoryCandidates.Add(_gameData.Weapons.Where(Eligible).Cast<IItemDefinition>().ToList());
        if (loot.CanDropArmor)
            categoryCandidates.Add(_gameData.Armors.Where(Eligible).Cast<IItemDefinition>().ToList());
        if (loot.CanDropMagicItem)
            categoryCandidates.Add(_gameData.MagicItems.Where(Eligible).Cast<IItemDefinition>().ToList());
        categoryCandidates.RemoveAll(candidates => candidates.Count == 0);
        if (categoryCandidates.Count == 0) return null;
        var candidates = categoryCandidates[_random.Next(categoryCandidates.Count)];
        return candidates[_random.Next(candidates.Count)];
    }

    public IItemDefinition? RollMasterThiefChestLoot(LiveCharacter character, IEnumerable<IItemDefinition> allTradableItems)
    {
        if (!character.HasPerk(PerkIds.ThiefMasterThief) || _random.Next(100) >= 25) return null;
        var candidates = allTradableItems.Where(item => item.Rarity == ItemRarity.Magic).ToList();
        return candidates.Count == 0 ? null : candidates[_random.Next(candidates.Count)];
    }

    public static bool TryStoreLootInParty(
        IItemDefinition item,
        LiveCharacter selectedCharacter,
        IEnumerable<LiveCharacter> partyMembers,
        out string ownerName)
    {
        foreach (var character in new[] { selectedCharacter }.Concat(partyMembers
                     .Where(character => character != selectedCharacter && character.IsAlive)))
        {
            if (!character.AddToBackpack(item)) continue;
            ownerName = character.Name;
            return true;
        }
        ownerName = string.Empty;
        return false;
    }

    public static bool TryStoreSearchedLoot(
        LiveCharacter character,
        IItemDefinition item,
        bool shareLootWithParty,
        IEnumerable<LiveCharacter> partyMembers,
        out string ownerName)
    {
        var candidates = shareLootWithParty
            ? new[] { character }.Concat(partyMembers.Where(candidate =>
                candidate != character && candidate.IsAlive))
            : [character];
        foreach (var candidate in candidates)
        {
            if (!candidate.AddToBackpack(item)) continue;
            ownerName = candidate.Name;
            return true;
        }
        ownerName = string.Empty;
        return false;
    }
}
