using MazeGame.Data;
using MazeGame.Domain.Characters;

namespace MazeGame.UI;

/// <summary>A program belépési pontja: karakterek és játék indítása közti választás.</summary>
public sealed class MainMenu
{
    private readonly GameDataCatalog _gameData;
    private readonly CharacterRoster _characterRoster = new();

    public MainMenu(GameDataCatalog gameData) => _gameData = gameData;

    public void Run()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== LABIRINTUS ===");
            Console.WriteLine();
            Console.WriteLine("1 - Játék indítása");
            Console.WriteLine("2 - Karaktergenerálás");
            Console.WriteLine($"3 - Karakterek ({_characterRoster.Characters.Count})");
            Console.WriteLine("Esc - Kilépés");

            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    new Game(_gameData, _characterRoster).Run();
                    break;
                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    new CharacterCreationScreen(_gameData, _characterRoster).Run();
                    break;
                case ConsoleKey.D3:
                case ConsoleKey.NumPad3:
                    ShowCharacters();
                    break;
                case ConsoleKey.Escape:
                    return;
            }
        }
    }

    private void ShowCharacters()
    {
        Console.Clear();
        Console.WriteLine("=== GENERÁLT KARAKTEREK ===");
        Console.WriteLine();
        if (_characterRoster.Characters.Count == 0) Console.WriteLine("Még nincs generált karakter.");
        foreach (var character in _characterRoster.Characters)
            Console.WriteLine($"{character.Name} — {character.Race.Name} {character.CharacterClass.Name}, HP {character.CurrentVitality}/{character.MaximumVitality}, Manna {character.CurrentMana}/{character.MaximumMana}");

        Console.WriteLine();
        Console.WriteLine("Bármely billentyű: vissza");
        Console.ReadKey(intercept: true);
    }
}
