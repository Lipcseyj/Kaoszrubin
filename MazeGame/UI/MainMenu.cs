using MazeGame.Data;
using MazeGame.Domain.Characters;
using System.Text.Json;

namespace MazeGame.UI;

/// <summary>A program belépési pontja: karakterek és játék indítása közti választás.</summary>
public sealed class MainMenu
{
    private readonly GameDataCatalog _gameData;
    private CharacterRoster _characterRoster;
    private readonly CharacterSaveService _characterSaveService;
    private readonly GameSaveService _gameSaveService;
    private readonly Random _random = new();

    public MainMenu(GameDataCatalog gameData, string characterSavePath, string gameSaveDirectory)
    {
        _gameData = gameData;
        _characterSaveService = new CharacterSaveService(characterSavePath, gameData);
        _gameSaveService = new GameSaveService(gameSaveDirectory, _characterSaveService);
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
                    LoadGame();
                    SaveCharacters();
                    break;
                case ConsoleKey.D3:
                case ConsoleKey.NumPad3:
                    new CharacterCreationScreen(_gameData, _characterRoster).Run();
                    SaveCharacters();
                    break;
                case ConsoleKey.D4:
                case ConsoleKey.NumPad4:
                    ShowCharacters();
                    SaveCharacters();
                    break;
                case ConsoleKey.D5:
                case ConsoleKey.NumPad5:
                    DeleteCharacter();
                    SaveCharacters();
                    break;
                case ConsoleKey.D6:
                case ConsoleKey.NumPad6:
                    QuickStart();
                    SaveCharacters();
                    break;
                case ConsoleKey.D7:
                case ConsoleKey.NumPad7:
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

        new Game(_gameData, _characterRoster, selectedCharacter, _gameSaveService).Run();
    }

    private void LoadGame()
    {
        var saves = _gameSaveService.List();
        if (saves.Count == 0)
        {
            ResetConsole();
            WriteLine("Nincs betölthető játékállás a mentések mappában.", ConsoleColor.DarkYellow);
            Console.ReadKey(intercept: true);
            return;
        }
        var selectedIndex = 0;
        while (true)
        {
            ResetConsole();
            WriteLine("=== JÁTÉK BETÖLTÉSE ===", ConsoleColor.Yellow);
            WriteLine("Fel/le: választás | Enter: betöltés | Esc: vissza", ConsoleColor.DarkCyan);
            Console.WriteLine();
            for (var index = 0; index < saves.Count; index++)
            {
                var save = saves[index];
                var marker = index == selectedIndex ? ">" : " ";
                WriteLine($"{marker} {save.MainCharacterName} — {save.MazeLevel}. pálya — {save.SavedAt:yyyy-MM-dd HH:mm:ss}",
                    index == selectedIndex ? ConsoleColor.Cyan : ConsoleColor.Gray);
            }
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = (selectedIndex - 1 + saves.Count) % saves.Count;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = (selectedIndex + 1) % saves.Count;
                    break;
                case ConsoleKey.Enter:
                    try
                    {
                        var loaded = _gameSaveService.Load(saves[selectedIndex].Path);
                        _characterRoster = loaded.Roster;
                        new Game(_gameData, _characterRoster, _characterRoster.SelectedCharacter!, _gameSaveService, loaded.State).Run();
                        return;
                    }
                    catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
                    {
                        ResetConsole();
                        WriteLine($"A mentés nem tölthető be: {exception.Message}", ConsoleColor.Red);
                        Console.ReadKey(intercept: true);
                        return;
                    }
                case ConsoleKey.Escape:
                    return;
            }
        }
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

    public static void ShowHelp()
    {
        ResetConsole();
        WriteLine("=== SÚGÓ ===", ConsoleColor.Yellow);
        Console.WriteLine();
        WriteLine("FŐMENÜ", ConsoleColor.Cyan);
        Console.WriteLine("1: játék indítása a kijelölt, élő karakterrel");
        Console.WriteLine("2: játékállás betöltése a mentések mappából");
        Console.WriteLine("3: karaktergenerálás");
        Console.WriteLine("4: karakterlista, kijelölés Enterrel");
        Console.WriteLine("5: karaktertörlés; O: az összes törlése; I/Y: megerősítés");
        Console.WriteLine("6: gyorsindítás új, automatikusan generált hőssel");
        Console.WriteLine("7: ez a súgó");
        Console.WriteLine("Esc: kilépés a programból");
        Console.WriteLine();
        WriteLine("KARAKTERGENERÁLÁS", ConsoleColor.Magenta);
        Console.WriteLine("Számok: faj vagy osztály kiválasztása | Enter: dobás elfogadása | R: újradobás | Esc: vissza");
        Console.WriteLine();
        WriteLine("LABIRINTUS", ConsoleColor.Green);
        Console.WriteLine("Nyilak: mozgás | Esc: főmenü");
        Console.WriteLine("Tab: térkép/karakterlap | Karakterlap: fel/le kijelölés, bal/jobb partitagváltás");
        Console.WriteLine("Karakterlap: Space - tárgy mozgatása | D - ledobás | I - részletek | Enter - használat");
        Console.WriteLine("Mágus/Pap: V - memorizált varázslatok | F1-F8 - gyorsvarázslatok");
        Console.WriteLine("Varázslatlista: fel/le, Enter - elsütés, F1-F8 - gyorshely, Esc - vissza");
        Console.WriteLine("Célzás: nyilak - célkereszt | Tab - következő érvényes cél | Enter - megerősítés | Esc - mégse");
        Console.WriteLine("Shift+F1: súgó | F9: teljes játékállás mentése a mentések mappába");
        Console.WriteLine("Ajtó mellett: N - nyitás | Z - bezárás | K - kulcsra zárás");
        Console.WriteLine("P: pihenés (pályánként egyszer, ellenségmentes és kulcsra zárt szobában)");
        Console.WriteLine("Partiparancs: H - helyben maradás be/ki | M - 10 másodperces szétszóródás");
        Console.WriteLine("Ctrl + Shift + U: teljes térkép felfedése/elrejtése | Ctrl + Shift + R: új pálya (fejlesztői mód)");
        Console.WriteLine("Ládára lépés: arany felvétele | Kijárat (⌂): következő labirintusszint");
        Console.WriteLine();
        WriteLine("CSATA", ConsoleColor.Red);
        Console.WriteLine("Saját kör: Space - fegyver | V/F1-F8 - varázslás; választás csak használható varázslatnál jelenik meg.");
        Console.WriteLine("Harci varázslási kudarc: max(0, 30 - Intelligencia - Ügyesség)%; a manna és az akció elvész.");
        Console.WriteLine("A csata alatt a világ ideje megáll.");
        Console.WriteLine();
        WriteLine("Bármely billentyű: vissza az előző képernyőre", ConsoleColor.DarkYellow);
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
        WriteLine($"2 - Játék betöltése ({_gameSaveService.List().Count})", ConsoleColor.Green);
        WriteLine("3 - Karaktergenerálás", ConsoleColor.Magenta);
        WriteLine($"4 - Karakterek ({_characterRoster.Characters.Count})", ConsoleColor.Cyan);
        WriteLine("5 - Karakter törlése", ConsoleColor.Red);
        WriteLine("6 - Gyorsindítás (új hős)", ConsoleColor.Green);
        WriteLine("7 - Súgó", ConsoleColor.DarkCyan);
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
