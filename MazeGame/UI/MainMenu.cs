using MazeGame.Data;
using MazeGame.Domain.Characters;

namespace MazeGame.UI;

/// <summary>A program belépési pontja: karakterek és játék indítása közti választás.</summary>
public sealed class MainMenu
{
    private readonly GameDataCatalog _gameData;
    private readonly CharacterRoster _characterRoster;
    private readonly CharacterSaveService _characterSaveService;

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
                WriteLine($"{marker} {character.Name} — {character.Race.Name} {character.CharacterClass.Name}{isSelected}", index == selectedIndex ? ConsoleColor.Cyan : ConsoleColor.Gray);
                WriteLine($"   HP {character.CurrentVitality}/{character.MaximumVitality}, Manna {(character.UsesMana ? $"{character.CurrentMana}/{character.MaximumMana}" : "nincs")}", ConsoleColor.DarkGray);
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

        new Game(_gameData, _characterRoster, selectedCharacter).Run();
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
