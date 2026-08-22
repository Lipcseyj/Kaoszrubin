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
            var characterClass = new CharacterClassDefinition("C004", "Tolvi", PrimaryAbilities.Zero, false, 0.8); /*eligibleClasses[_random.Next(eligibleClasses.Count)];*/
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
        for (var slot = 0; slot < 2; slot++) character.EquipWeapon(slot, _gameData.Weapons[_random.Next(_gameData.Weapons.Count)]);
        character.EquipArmor(_gameData.Armors[_random.Next(_gameData.Armors.Count)]);
        var magicItemCount = _random.Next(1, LiveCharacter.MaximumMagicItemCount + 1);
        foreach (var item in _gameData.MagicItems.OrderBy(_ => _random.Next()).Take(magicItemCount)) character.AddMagicItem(item);

        var allItems = _gameData.Items.Cast<IItemDefinition>()
            .Concat(_gameData.Weapons).Concat(_gameData.Armors).Concat(_gameData.MagicItems).ToList();
        var targetCount = _random.Next(3, LiveCharacter.MaximumBackpackItemCount + 1);
        while (character.Backpack.Count(item => item is not null) < targetCount)
            character.AddToBackpack(allItems[_random.Next(allItems.Count)]);
    }

    private PrimaryAbilities RollAbilities()
    {
        var values = new[] { 1, 1, 1, 1 };
        for (var remaining = AbilityPointTotal - values.Sum(); remaining > 0; remaining--)
        {
            var available = Enumerable.Range(0, values.Length).Where(index => values[index] < 10).ToArray();
            values[available[_random.Next(available.Length)]]++;
        }
        return new PrimaryAbilities(values[0], values[1], values[2], values[3]);
    }
}
