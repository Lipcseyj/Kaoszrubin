using MazeGame.Domain.Characters;
using MazeGame.Domain.Inventory;

namespace MazeGame.Data;

/// <summary>Fejlesztői és későbbi NPC-célra teljesen adatvezérelt, véletlen karaktereket készít.</summary>
public sealed class RandomCharacterGenerator(GameDataCatalog gameData, Random random)
{
    private const int AbilityPointTotal = 25;
    private readonly GameDataCatalog _gameData = gameData;
    private readonly Random _random = random;

    public LiveCharacter Create(IReadOnlyCollection<string> usedNames)
    {
        var character = CreateLevelOne(usedNames);
        RaiseToRandomLevel(character);
        AddRandomPerks(character);
        FillRandomEquipment(character);
        return character;
    }

    public LiveCharacter CreateLevelOne(IReadOnlyCollection<string> usedNames)
    {
        for (var attempt = 0; attempt < 2_000; attempt++)
        {
            var race = _gameData.Races[_random.Next(_gameData.Races.Count)];
            var rolledAbilities = RollAbilities();
            var finalAbilities = (rolledAbilities + race.AbilityBonuses).Clamp(1, 13);
            var eligibleClasses = _gameData.CharacterClasses.Where(candidate => finalAbilities.MeetsMinimum(candidate.MinimumAbilities)).ToList();
            if (eligibleClasses.Count == 0) continue;
            var characterClass = eligibleClasses[_random.Next(eligibleClasses.Count)];
            var name = ChooseName(characterClass.Id, usedNames);
            var character = LiveCharacterFactory.Create(name, race, characterClass, rolledAbilities,
                _random.Next(1, 16), _random.Next(1, 16), _gameData,
                CharacterColors.Selectable[_random.Next(CharacterColors.Selectable.Count)]);
            character.SetNpcBehavior(BehaviorFor(characterClass.Id));
            return character;
        }
        throw new InvalidOperationException("A jelenlegi játékadatokból nem generálható véletlen partitárs.");
    }

    private NpcBehavior BehaviorFor(string characterClassId) => characterClassId.ToUpperInvariant() switch
    {
        "C001" => _random.Next(2) == 0 ? NpcBehavior.Defensive : NpcBehavior.Aggressive,
        "C002" => NpcBehavior.Aggressive,
        "C003" => NpcBehavior.Defensive,
        "C004" => NpcBehavior.Scout,
        "C005" or "C006" => NpcBehavior.Cautious,
        _ => NpcBehavior.Defensive
    };

    private string ChooseName(string characterClassId, IReadOnlyCollection<string> usedNames)
    {
        var names = _gameData.GetCharacterNames(characterClassId);
        var unused = names.Where(candidate => !usedNames.Contains(candidate.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        var candidates = unused.Count > 0 ? unused : names;
        if (candidates.Count == 0) throw new InvalidOperationException("A véletlen karakter osztályához nincs név az adatok.csv fájlban.");
        return candidates[_random.Next(candidates.Count)].Name;
    }

    private void RaiseToRandomLevel(LiveCharacter character)
    {
        var targetLevel = _random.Next(2, 31);
        while (character.Level < targetLevel)
        {
            var needed = character.GetExperienceNeededForNextLevel(_gameData.ExperienceByLevel);
            if (needed <= 0) break;
            character.AddExperience(needed, _gameData.ExperienceByLevel,
                _gameData.GetVitalityGrowth(character.Abilities.Health),
                _gameData.GetManaGrowth(character.Abilities.Intelligence), _random);
        }
    }

    private void AddRandomPerks(LiveCharacter character)
    {
        var milestones = new[] { 5, 15, 25 };
        for (var tier = 1; tier <= milestones.Length; tier++)
        {
            var first = milestones[tier - 1] - 2;
            var last = milestones[tier - 1] + 2;
            if (character.Level < first) continue;
            var attempts = Math.Min(character.Level, last) - first + 1;
            var earned = character.Level >= last || Enumerable.Range(0, attempts).Any(_ => _random.NextDouble() < 0.40);
            if (!earned) continue;
            var choices = _gameData.GetPerkChoices(character.CharacterClass.Id, tier);
            var perk = choices[_random.Next(choices.Count)];
            if (character.AddPerk(perk)) character.ApplyPerkAcquisitionBonus(perk);
        }
    }

    private void FillRandomEquipment(LiveCharacter character)
    {
        var allowLegendary = character.Level >= 15 && _random.NextDouble() < 0.02;
        var maximumMagicPower = Math.Clamp(character.Level / 5, 0, 3);
        var usableWeapons = _gameData.Weapons.Where(weapon => weapon.CanBeEquippedBy(character.CharacterClass.Id) &&
            IsEquipmentTierAvailable(weapon, maximumMagicPower, allowLegendary)).ToList();
        if (usableWeapons.Count > 0)
        {
            var firstWeapon = usableWeapons[_random.Next(usableWeapons.Count)];
            character.EquipWeapon(0, firstWeapon);
            var usableSecondWeapons = usableWeapons.Where(weapon => !weapon.IsTwoHanded).ToList();
            if (!firstWeapon.IsTwoHanded && usableSecondWeapons.Count > 0)
                character.EquipWeapon(1, usableSecondWeapons[_random.Next(usableSecondWeapons.Count)]);
        }
        var usableArmors = _gameData.Armors.Where(armor => armor.CanBeEquippedBy(character.CharacterClass.Id) &&
            IsEquipmentTierAvailable(armor, maximumMagicPower, allowLegendary)).ToList();
        if (usableArmors.Count > 0) character.EquipArmor(usableArmors[_random.Next(usableArmors.Count)]);
        var magicItemCount = _random.Next(1, LiveCharacter.MaximumMagicItemCount + 1);
        foreach (var item in _gameData.MagicItems.Where(item => item.CanBeEquippedBy(character.CharacterClass.Id) &&
                         IsEquipmentTierAvailable(item, maximumMagicPower, allowLegendary))
                     .OrderBy(_ => _random.Next()).Take(magicItemCount)) character.AddMagicItem(item);

        var allItems = _gameData.Items.Cast<IItemDefinition>()
            .Concat(_gameData.Weapons).Concat(_gameData.Armors).Concat(_gameData.MagicItems)
            .Where(item => IsEquipmentTierAvailable(item, maximumMagicPower, allowLegendary)).ToList();
        var targetCount = _random.Next(3, LiveCharacter.MaximumBackpackItemCount + 1);
        while (character.Backpack.Count(item => item is not null) < targetCount)
            character.AddToBackpack(allItems[_random.Next(allItems.Count)]);
    }

    private static bool IsEquipmentTierAvailable(IItemDefinition item, int maximumMagicPower, bool allowLegendary) =>
        item.Rarity switch
        {
            ItemRarity.Legendary => allowLegendary,
            ItemRarity.Magic => item.MagicPower <= maximumMagicPower,
            _ => true
        };

    private PrimaryAbilities RollAbilities()
    {
        var values = new[] { 1, 1, 1, 1 };
        var pointTotal = RollAbilityPointTotal();
        for (var remaining = pointTotal - values.Sum(); remaining > 0; remaining--)
        {
            var available = Enumerable.Range(0, values.Length).Where(index => values[index] < 10).ToArray();
            values[available[_random.Next(available.Length)]]++;
        }
        return new PrimaryAbilities(values[0], values[1], values[2], values[3]);
    }

    private int RollAbilityPointTotal()
    {
        var roll = _random.Next(100);
        return AbilityPointTotal + (roll switch
        {
            < 15 => 0,
            < 65 => 1,
            < 90 => 2,
            _ => 3
        });
    }
}
