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
            PrimaryAbilities rolledAbilities;
            PrimaryAbilities finalAbilities;
            List<CharacterClassDefinition> eligibleClasses;
            do
            {
                rolledAbilities = RollAbilities();
                finalAbilities = (rolledAbilities + race.AbilityBonuses).Clamp(1, 13);
                eligibleClasses = EligibleClasses(finalAbilities);
            } while (eligibleClasses.Count == 0);

            var vitalityBonus = _random.Next(1, 16);
            var manaBonus = _random.Next(1, 16);

            DrawAbilityRoll(name, race, rolledAbilities, finalAbilities, vitalityBonus, manaBonus, eligibleClasses);
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return;
            if (key == ConsoleKey.R) continue;
            if (key != ConsoleKey.Enter) continue;

            var characterClass = ChooseClass(finalAbilities, eligibleClasses);
            if (characterClass is null) continue;

            var color = ChooseColor();
            if (color is null) continue;

            var character = LiveCharacterFactory.Create(name, race, characterClass, rolledAbilities, vitalityBonus, manaBonus, _gameData, color.Value);
            if (character.IsSpellcaster) ChooseStartingSpells(character);
            _characterRoster.Add(character);
            ShowCreatedCharacter(character);
            return;
        }
    }

    /// <summary>Interakció nélkül elkészíti az első fajhoz tartozó, első elérhető osztályú érvényes karaktert.</summary>
    public LiveCharacter CreateFirstValidCharacter(string name)
        => CreateFirstValidCharacter(_ => name);

    /// <summary>Interakció nélkül készít karaktert, majd az érvényes osztály alapján kéri le a nevét.</summary>
    public LiveCharacter CreateFirstValidCharacter(Func<CharacterClassDefinition, string> nameFactory)
    {
        foreach (var race in _gameData.Races)
        {
            for (var attempt = 0; attempt < 1_000; attempt++)
            {
                var rolledAbilities = RollAbilities();
                var finalAbilities = (rolledAbilities + race.AbilityBonuses).Clamp(1, 13);
                var characterClass = _gameData.CharacterClasses.FirstOrDefault(candidate => finalAbilities.MeetsMinimum(candidate.MinimumAbilities));
                if (characterClass is null) continue;

                var character = LiveCharacterFactory.Create(nameFactory(characterClass), race, characterClass, rolledAbilities,
                    _random.Next(1, 16), _random.Next(1, 16), _gameData, RandomCharacterColor());
                SpellcastingRules.GiveAutomaticStartingSpells(character, _gameData, _random);
                return character;
            }
        }

        throw new InvalidOperationException("A jelenlegi faj- és osztályadatokból nem generálható érvényes karakter.");
    }

    private ConsoleColor? ChooseColor()
    {
        var colors = CharacterColors.Selectable;
        var selectedIndex = 0;
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== KARAKTERSZÍN ===");
            Console.WriteLine("Fel/le: választás | Enter: elfogadás | Esc: vissza");
            Console.WriteLine();
            for (var index = 0; index < colors.Count; index++)
            {
                Console.ForegroundColor = colors[index];
                Console.WriteLine($"{(index == selectedIndex ? ">" : " ")} {CharacterColors.NameOf(colors[index])} — ●");
            }
            Console.ResetColor();
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = (selectedIndex - 1 + colors.Count) % colors.Count;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = (selectedIndex + 1) % colors.Count;
                    break;
                case ConsoleKey.Enter:
                    return colors[selectedIndex];
                case ConsoleKey.Escape:
                    return null;
            }
        }
    }

    private ConsoleColor RandomCharacterColor() => CharacterColors.Selectable[_random.Next(CharacterColors.Selectable.Count)];

    private void ChooseStartingSpells(LiveCharacter character)
    {
        SpellcastingRules.TryGetSchool(character.CharacterClass.Id, out var school);
        var spells = _gameData.GetSpells(school, 1).OrderBy(spell => spell.Name).ToList();
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cursor = 0;
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== KEZDŐ VARÁZSLATOK ===");
                Console.WriteLine($"Válassz pontosan {SpellcastingRules.StartingSpellCount(character.CharacterClass.Id)} varázslatot. Fel/le: mozgás, Space: kijelölés, Enter: elfogadás");
            Console.WriteLine();
            for (var index = 0; index < spells.Count; index++)
                Console.WriteLine($"{(index == cursor ? ">" : " ")} [{(selected.Contains(spells[index].Id) ? "X" : " ")}] {spells[index].Name}");
            Console.WriteLine();
            Console.WriteLine($"Kijelölve: {selected.Count}/{SpellcastingRules.StartingSpellCount(character.CharacterClass.Id)}");
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow: cursor = (cursor - 1 + spells.Count) % spells.Count; break;
                case ConsoleKey.DownArrow: cursor = (cursor + 1) % spells.Count; break;
                case ConsoleKey.Spacebar:
                    if (!selected.Remove(spells[cursor].Id) && selected.Count < SpellcastingRules.StartingSpellCount(character.CharacterClass.Id))
                        selected.Add(spells[cursor].Id);
                    break;
                case ConsoleKey.Enter when selected.Count == SpellcastingRules.StartingSpellCount(character.CharacterClass.Id):
                    var chosen = spells.Where(spell => selected.Contains(spell.Id)).ToList();
                    foreach (var spell in chosen) character.LearnSpell(spell);
                    character.SetMemorizedSpells(chosen);
                    return;
            }
        }
    }

    private string? ReadName()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== KARAKTERGENERÁLÁS ===");
            Console.WriteLine($"Név (legfeljebb {LiveCharacter.MaximumNameLength} karakter; üresen hagyva: vissza):");
            var name = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (name.Length <= LiveCharacter.MaximumNameLength) return name;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"A név túl hosszú. Legfeljebb {LiveCharacter.MaximumNameLength} karakter adható meg.");
            Console.ResetColor();
            Console.WriteLine("Nyomj meg egy billentyűt az újrapróbáláshoz.");
            Console.ReadKey(intercept: true);
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

    private List<CharacterClassDefinition> EligibleClasses(PrimaryAbilities abilities) =>
        _gameData.CharacterClasses.Where(characterClass =>
            abilities.MeetsMinimum(characterClass.MinimumAbilities)).ToList();

    private CharacterClassDefinition? ChooseClass(PrimaryAbilities abilities,
        IReadOnlyList<CharacterClassDefinition> eligibleClasses)
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== OSZTÁLY VÁLASZTÁSA ===");
            Console.WriteLine($"Végső képességek: {FormatAbilities(abilities)}");
            Console.WriteLine();
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

    private void DrawAbilityRoll(string name, RaceDefinition race, PrimaryAbilities rolled, PrimaryAbilities final,
        int vitalityBonus, int manaBonus, IReadOnlyList<CharacterClassDefinition> eligibleClasses)
    {
        Console.Clear();
        Console.WriteLine("=== KÉPESSÉGDOBÁS ===");
        Console.WriteLine($"{name} — {race.Name}");
        Console.WriteLine();
        var rolledPointTotal = rolled.Strength + rolled.Dexterity + rolled.Health + rolled.Intelligence;
        Console.WriteLine($"Dobott értékek (összesen {rolledPointTotal}): {FormatAbilities(rolled)}");
        Console.WriteLine($"Faji módosító: {FormatAbilities(race.AbilityBonuses)}");
        Console.WriteLine($"Végső értékek: {FormatAbilities(final)}");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Választható osztályok: {string.Join(", ", eligibleClasses.Select(characterClass => characterClass.Name))}");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"Életerő: {_gameData.GetMinimumVitality(final.Health)} + {vitalityBonus} = {_gameData.GetMinimumVitality(final.Health) + vitalityBonus}");
        Console.WriteLine($"Manna: {_gameData.GetMinimumMana(final.Intelligence)} + {manaBonus} = {_gameData.GetMinimumMana(final.Intelligence) + manaBonus}");
        Console.WriteLine();
        Console.WriteLine("Enter - elfogadás | R - újradobás | Esc - megszakítás");
    }

    private PrimaryAbilities RollAbilities()
    {
        var values = new[] { 1, 1, 1, 1 };
        var pointTotal = RollAbilityPointTotal();
        for (var remainingPoints = pointTotal - values.Sum(); remainingPoints > 0; remainingPoints--)
        {
            var availableIndices = Enumerable.Range(0, values.Length).Where(index => values[index] < 10).ToArray();
            values[availableIndices[_random.Next(availableIndices.Length)]]++;
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

    private static void ShowCreatedCharacter(LiveCharacter character)
    {
        Console.Clear();
        Console.WriteLine("Karakter elkészült!");
        Console.WriteLine($"{character.Name} — {character.Race.Name} {character.CharacterClass.Name}");
        Console.WriteLine(FormatAbilities(character.Abilities));
        Console.WriteLine($"HP: {character.CurrentVitality}/{character.MaximumVitality}, Manna: {(character.UsesMana ? $"{character.CurrentMana}/{character.MaximumMana}" : "nincs")}");
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
