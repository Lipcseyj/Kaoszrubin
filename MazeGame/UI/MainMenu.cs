using MazeGame.Data;
using MazeGame.Domain.Characters;

namespace MazeGame.UI;

/// <summary>A program belépési pontja: karakterek és játék indítása közti választás.</summary>
public sealed class MainMenu
{
    private readonly GameDataCatalog _gameData;
    private readonly CharacterRoster _characterRoster;
    private readonly CharacterSaveService _characterSaveService;
    private readonly Random _random = new();

    public MainMenu(GameDataCatalog gameData, string characterSavePath)
    {
        _gameData = gameData;
        _characterSaveService = new CharacterSaveService(characterSavePath, gameData);
        _characterRoster = _characterSaveService.Load();
    }

    public void Run()
    {
        while (true)
        {
            DrawMenu();

            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    StartGame();
                    SaveCharacters();
                    break;
                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    new CharacterCreationScreen(_gameData, _characterRoster).Run();
                    SaveCharacters();
                    break;
                case ConsoleKey.D3:
                case ConsoleKey.NumPad3:
                    ShowCharacters();
                    SaveCharacters();
                    break;
                case ConsoleKey.D4:
                case ConsoleKey.NumPad4:
                    DeleteCharacter();
                    SaveCharacters();
                    break;
                case ConsoleKey.D5:
                case ConsoleKey.NumPad5:
                    QuickStart();
                    SaveCharacters();
                    break;
                case ConsoleKey.D6:
                case ConsoleKey.NumPad6:
                    ShowHelp();
                    break;
                case ConsoleKey.Escape:
                    return;
            }
        }
    }

    private void ShowCharacters()
    {
        if (_characterRoster.Characters.Count == 0)
        {
            ResetConsole();
            Console.WriteLine("Még nincs generált karakter.");
            Console.ReadKey(intercept: true);
            return;
        }

        var selectedIndex = _characterRoster.SelectedCharacter is null
            ? 0
            : Enumerable.Range(0, _characterRoster.Characters.Count)
                .FirstOrDefault(index => _characterRoster.Characters[index] == _characterRoster.SelectedCharacter);
        while (true)
        {
            ResetConsole();
            WriteLine("=== GENERÁLT KARAKTEREK ===", ConsoleColor.Yellow);
            WriteLine("Fel/le: választás | Enter: kijelölés | Esc: vissza", ConsoleColor.DarkCyan);
            Console.WriteLine();
            for (var index = 0; index < _characterRoster.Characters.Count; index++)
            {
                var character = _characterRoster.Characters[index];
                var marker = index == selectedIndex ? ">" : " ";
                var isSelected = character == _characterRoster.SelectedCharacter ? " [aktív]" : string.Empty;
                var deathMarker = character.IsAlive ? string.Empty : " [HALOTT]";
                WriteLine($"{marker} {character.Name} — {character.Race.Name} {character.CharacterClass.Name}{isSelected}{deathMarker}", character.IsAlive ? (index == selectedIndex ? ConsoleColor.Cyan : ConsoleColor.Gray) : ConsoleColor.DarkRed);
                WriteLine($"   HP {character.CurrentVitality}/{character.MaximumVitality}, Manna {(character.UsesMana ? $"{character.CurrentMana}/{character.MaximumMana}" : "nincs")}", character.IsAlive ? ConsoleColor.DarkGray : ConsoleColor.Red);
            }

            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = (selectedIndex - 1 + _characterRoster.Characters.Count) % _characterRoster.Characters.Count;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = (selectedIndex + 1) % _characterRoster.Characters.Count;
                    break;
                case ConsoleKey.Enter:
                    _characterRoster.Select(_characterRoster.Characters[selectedIndex]);
                    return;
                case ConsoleKey.Escape:
                    return;
            }
        }
    }

    private void StartGame()
    {
        if (_characterRoster.SelectedCharacter is not { } selectedCharacter)
        {
            ResetConsole();
            Console.WriteLine("A játék indításához előbb válassz ki egy karaktert a Karakterek menüben.");
            Console.ReadKey(intercept: true);
            return;
        }

        if (!selectedCharacter.IsAlive)
        {
            ResetConsole();
            WriteLine("Halott karakterrel nem indítható játék. Válassz másik karaktert vagy készíts újat.", ConsoleColor.Red);
            Console.ReadKey(intercept: true);
            return;
        }

        new Game(_gameData, _characterRoster, selectedCharacter).Run();
    }

    private void QuickStart()
    {
        try
        {
            var character = new CharacterCreationScreen(_gameData, _characterRoster).CreateFirstValidCharacter(ChooseQuickStartName);
            _characterRoster.Add(character);
            _characterRoster.Select(character);
            SaveCharacters();
            StartGame();
        }
        catch (InvalidOperationException exception)
        {
            ResetConsole();
            WriteLine(exception.Message, ConsoleColor.Red);
            Console.ReadKey(intercept: true);
        }
    }

    private string ChooseQuickStartName(CharacterClassDefinition characterClass)
    {
        var names = _gameData.GetCharacterNames(characterClass.Id);
        if (names.Count == 0) throw new InvalidOperationException($"Nincs gyorsindításhoz használható név a(z) {characterClass.Name} osztályhoz.");
        var unusedNames = names.Where(candidate => !_characterRoster.Characters.Any(character =>
            string.Equals(character.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))).ToList();
        var candidates = unusedNames.Count > 0 ? unusedNames : names;
        return candidates[_random.Next(candidates.Count)].Name;
    }

    private void DeleteCharacter()
    {
        if (_characterRoster.Characters.Count == 0)
        {
            ResetConsole();
            Console.WriteLine("Nincs törölhető karakter.");
            Console.ReadKey(intercept: true);
            return;
        }

        var selectedIndex = 0;
        while (true)
        {
            ResetConsole();
            WriteLine("=== KARAKTER TÖRLÉSE ===", ConsoleColor.Red);
            WriteLine("Fel/le: választás | Enter: törlés | O: összes törlése | Esc: vissza", ConsoleColor.DarkCyan);
            Console.WriteLine();
            for (var index = 0; index < _characterRoster.Characters.Count; index++)
            {
                var character = _characterRoster.Characters[index];
                var marker = index == selectedIndex ? ">" : " ";
                var deathMarker = character.IsAlive ? string.Empty : " [HALOTT]";
                WriteLine($"{marker} {character.Name} — {character.Race.Name} {character.CharacterClass.Name}{deathMarker}", character.IsAlive ? (index == selectedIndex ? ConsoleColor.Yellow : ConsoleColor.Gray) : ConsoleColor.DarkRed);
            }

            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.O:
                    WriteLine("\nBiztosan törlöd az ÖSSZES karaktert? (I / N)", ConsoleColor.Red);
                    if (Console.ReadKey(intercept: true).Key is ConsoleKey.I or ConsoleKey.Y)
                    {
                        _characterRoster.Clear();
                        SaveCharacters();
                        ResetConsole();
                        WriteLine("Minden karakter törölve.", ConsoleColor.Green);
                        Console.ReadKey(intercept: true);
                        return;
                    }
                    break;
                case ConsoleKey.UpArrow:
                    selectedIndex = (selectedIndex - 1 + _characterRoster.Characters.Count) % _characterRoster.Characters.Count;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = (selectedIndex + 1) % _characterRoster.Characters.Count;
                    break;
                case ConsoleKey.Enter:
                    var character = _characterRoster.Characters[selectedIndex];
                    WriteLine($"\nBiztosan törlöd: {character.Name}? (I / N)", ConsoleColor.Red);
                    if (Console.ReadKey(intercept: true).Key is ConsoleKey.I or ConsoleKey.Y)
                    {
                        _characterRoster.Remove(character);
                        SaveCharacters();
                        ResetConsole();
                        WriteLine($"{character.Name} törölve.", ConsoleColor.Green);
                        Console.ReadKey(intercept: true);
                        return;
                    }
                    break;
                case ConsoleKey.Escape:
                    return;
            }
        }
    }

    private static void ShowHelp()
    {
        ResetConsole();
        WriteLine("=== SÚGÓ ===", ConsoleColor.Yellow);
        Console.WriteLine();
        WriteLine("FŐMENÜ", ConsoleColor.Cyan);
        Console.WriteLine("1: játék indítása a kijelölt, élő karakterrel");
        Console.WriteLine("2: karaktergenerálás");
        Console.WriteLine("3: karakterlista, kijelölés Enterrel");
        Console.WriteLine("4: karaktertörlés; O: az összes törlése; I/Y: megerősítés");
        Console.WriteLine("5: gyorsindítás új, automatikusan generált hőssel");
        Console.WriteLine("6: ez a súgó");
        Console.WriteLine("Esc: kilépés a programból");
        Console.WriteLine();
        WriteLine("KARAKTERGENERÁLÁS", ConsoleColor.Magenta);
        Console.WriteLine("Számok: faj vagy osztály kiválasztása | Enter: dobás elfogadása | R: újradobás | Esc: vissza");
        Console.WriteLine();
        WriteLine("LABIRINTUS", ConsoleColor.Green);
        Console.WriteLine("Nyilak: mozgás | Esc: főmenü");
        Console.WriteLine("Ctrl + Shift + U: teljes térkép felfedése/elrejtése | Ctrl + Shift + R: új pálya (fejlesztői mód)");
        Console.WriteLine("Ládára lépés: arany felvétele | Kijárat (⌂): következő labirintusszint");
        Console.WriteLine();
        WriteLine("CSATA", ConsoleColor.Red);
        Console.WriteLine("Szóköz: következő támadási kör. A csata alatt a világ ideje megáll.");
        Console.WriteLine();
        WriteLine("Bármely billentyű: vissza a főmenübe", ConsoleColor.DarkYellow);
        Console.ReadKey(intercept: true);
    }

    private void SaveCharacters() => _characterSaveService.Save(_characterRoster);

    private void DrawMenu()
    {
        ResetConsole();
        WriteLine("=== LABIRINTUS ===", ConsoleColor.Yellow);
        Console.WriteLine();
        var selectedCharacter = _characterRoster.SelectedCharacter?.Name ?? "nincs";
        WriteLine($"Választott karakter: {selectedCharacter}", _characterRoster.SelectedCharacter is null ? ConsoleColor.DarkGray : ConsoleColor.Cyan);
        Console.WriteLine();
        WriteLine("1 - Játék indítása", ConsoleColor.Green);
        WriteLine("2 - Karaktergenerálás", ConsoleColor.Magenta);
        WriteLine($"3 - Karakterek ({_characterRoster.Characters.Count})", ConsoleColor.Cyan);
        WriteLine("4 - Karakter törlése", ConsoleColor.Red);
        WriteLine("5 - Gyorsindítás (új hős)", ConsoleColor.Green);
        WriteLine("6 - Súgó", ConsoleColor.DarkCyan);
        WriteLine("Esc - Kilépés", ConsoleColor.DarkYellow);
        Console.ResetColor();
    }

    private static void ResetConsole()
    {
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Clear();
    }

    private static void WriteLine(string text, ConsoleColor foregroundColor)
    {
        Console.ForegroundColor = foregroundColor;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.WriteLine(text);
    }
}
