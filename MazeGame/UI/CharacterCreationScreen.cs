using MazeGame.Data;
using MazeGame.Domain.Characters;

namespace MazeGame.UI;

public sealed class CharacterCreationScreen
{
    private const int AbilityPointTotal = 25;
    private readonly GameDataCatalog _gameData;
    private readonly CharacterRoster _characterRoster;
    private readonly Random _random = new();

    public CharacterCreationScreen(GameDataCatalog gameData, CharacterRoster characterRoster)
    {
        _gameData = gameData;
        _characterRoster = characterRoster;
    }

    public void Run()
    {
        var name = ReadName();
        if (name is null) return;
        var race = ChooseRace();
        if (race is null) return;

        while (true)
        {
            var rolledAbilities = RollAbilities();
            var vitalityBonus = _random.Next(1, 16);
            var manaBonus = _random.Next(1, 16);
            var finalAbilities = (rolledAbilities + race.AbilityBonuses).Clamp(1, 13);

            DrawAbilityRoll(name, race, rolledAbilities, finalAbilities, vitalityBonus, manaBonus);
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return;
            if (key == ConsoleKey.R) continue;
            if (key != ConsoleKey.Enter) continue;

            var characterClass = ChooseClass(finalAbilities);
            if (characterClass is null) continue;

            var character = LiveCharacterFactory.Create(name, race, characterClass, rolledAbilities, vitalityBonus, manaBonus, _gameData);
            _characterRoster.Add(character);
            ShowCreatedCharacter(character);
            return;
        }
    }

    private string? ReadName()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== KARAKTERGENERÁLÁS ===");
            Console.WriteLine("Név (üresen hagyva: vissza):");
            var name = Console.ReadLine()?.Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }

    private RaceDefinition? ChooseRace()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== FAJ VÁLASZTÁSA ===");
            for (var index = 0; index < _gameData.Races.Count; index++)
            {
                var race = _gameData.Races[index];
                Console.WriteLine($"{index + 1} - {race.Name} ({FormatAbilities(race.AbilityBonuses)})");
            }
            Console.WriteLine("Esc - vissza");

            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return null;
            if (TryGetNumberKey(key, out var choice) && choice <= _gameData.Races.Count) return _gameData.Races[choice - 1];
        }
    }

    private CharacterClassDefinition? ChooseClass(PrimaryAbilities abilities)
    {
        var eligibleClasses = _gameData.CharacterClasses.Where(characterClass => abilities.MeetsMinimum(characterClass.MinimumAbilities)).ToList();
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== OSZTÁLY VÁLASZTÁSA ===");
            Console.WriteLine($"Végső képességek: {FormatAbilities(abilities)}");
            Console.WriteLine();
            if (eligibleClasses.Count == 0)
            {
                Console.WriteLine("Ezekkel az értékekkel nincs választható osztály.");
                Console.WriteLine("R - újradobás, Esc - kilépés");
                var noClassKey = Console.ReadKey(intercept: true).Key;
                return null;
            }

            for (var index = 0; index < eligibleClasses.Count; index++)
            {
                var characterClass = eligibleClasses[index];
                Console.WriteLine($"{index + 1} - {characterClass.Name} (min.: {FormatAbilities(characterClass.MinimumAbilities)})");
            }
            Console.WriteLine("R - újradobás, Esc - kilépés");

            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.R or ConsoleKey.Escape) return null;
            if (TryGetNumberKey(key, out var choice) && choice <= eligibleClasses.Count) return eligibleClasses[choice - 1];
        }
    }

    private void DrawAbilityRoll(string name, RaceDefinition race, PrimaryAbilities rolled, PrimaryAbilities final, int vitalityBonus, int manaBonus)
    {
        Console.Clear();
        Console.WriteLine("=== KÉPESSÉGDOBÁS ===");
        Console.WriteLine($"{name} — {race.Name}");
        Console.WriteLine();
        Console.WriteLine($"Dobott értékek (összesen {AbilityPointTotal}): {FormatAbilities(rolled)}");
        Console.WriteLine($"Faji módosító: {FormatAbilities(race.AbilityBonuses)}");
        Console.WriteLine($"Végső értékek: {FormatAbilities(final)}");
        Console.WriteLine();
        Console.WriteLine($"Életerő: {_gameData.GetMinimumVitality(final.Health)} + {vitalityBonus} = {_gameData.GetMinimumVitality(final.Health) + vitalityBonus}");
        Console.WriteLine($"Manna: {_gameData.GetMinimumMana(final.Intelligence)} + {manaBonus} = {_gameData.GetMinimumMana(final.Intelligence) + manaBonus}");
        Console.WriteLine();
        Console.WriteLine("Enter - elfogadás | R - újradobás | Esc - megszakítás");
    }

    private PrimaryAbilities RollAbilities()
    {
        var values = new[] { 1, 1, 1, 1 };
        for (var remainingPoints = AbilityPointTotal - values.Sum(); remainingPoints > 0; remainingPoints--)
        {
            var availableIndices = Enumerable.Range(0, values.Length).Where(index => values[index] < 10).ToArray();
            values[availableIndices[_random.Next(availableIndices.Length)]]++;
        }
        return new PrimaryAbilities(values[0], values[1], values[2], values[3]);
    }

    private static void ShowCreatedCharacter(LiveCharacter character)
    {
        Console.Clear();
        Console.WriteLine("Karakter elkészült!");
        Console.WriteLine($"{character.Name} — {character.Race.Name} {character.CharacterClass.Name}");
        Console.WriteLine(FormatAbilities(character.Abilities));
        Console.WriteLine($"HP: {character.CurrentVitality}/{character.MaximumVitality}, Manna: {character.CurrentMana}/{character.MaximumMana}");
        Console.WriteLine();
        Console.WriteLine("Bármely billentyű: főmenü");
        Console.ReadKey(intercept: true);
    }

    private static string FormatAbilities(PrimaryAbilities abilities) =>
        $"Erő {abilities.Strength}, Ügyesség {abilities.Dexterity}, Egészség {abilities.Health}, Intelligencia {abilities.Intelligence}";

    private static bool TryGetNumberKey(ConsoleKey key, out int number)
    {
        number = key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => 3,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => 4,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => 5,
            ConsoleKey.D6 or ConsoleKey.NumPad6 => 6,
            ConsoleKey.D7 or ConsoleKey.NumPad7 => 7,
            ConsoleKey.D8 or ConsoleKey.NumPad8 => 8,
            ConsoleKey.D9 or ConsoleKey.NumPad9 => 9,
            _ => 0
        };
        return number != 0;
    }
}
