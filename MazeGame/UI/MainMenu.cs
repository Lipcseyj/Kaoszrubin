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
            Console.WriteLine($"Választott karakter: {_characterRoster.SelectedCharacter?.Name ?? "nincs"}");
            Console.WriteLine();
            Console.WriteLine("1 - Játék indítása");
            Console.WriteLine("2 - Karaktergenerálás");
            Console.WriteLine($"3 - Karakterek ({_characterRoster.Characters.Count})");
            Console.WriteLine("Esc - Kilépés");

            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    StartGame();
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
        if (_characterRoster.Characters.Count == 0)
        {
            Console.Clear();
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
            Console.Clear();
            Console.WriteLine("=== GENERÁLT KARAKTEREK ===");
            Console.WriteLine("Fel/le: választás | Enter: kijelölés | Esc: vissza");
            Console.WriteLine();
            for (var index = 0; index < _characterRoster.Characters.Count; index++)
            {
                var character = _characterRoster.Characters[index];
                var marker = index == selectedIndex ? ">" : " ";
                var isSelected = character == _characterRoster.SelectedCharacter ? " [aktív]" : string.Empty;
                Console.WriteLine($"{marker} {character.Name} — {character.Race.Name} {character.CharacterClass.Name}{isSelected}");
                Console.WriteLine($"   HP {character.CurrentVitality}/{character.MaximumVitality}, Manna {(character.UsesMana ? $"{character.CurrentMana}/{character.MaximumMana}" : "nincs")}");
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
            Console.Clear();
            Console.WriteLine("A játék indításához előbb válassz ki egy karaktert a Karakterek menüben.");
            Console.ReadKey(intercept: true);
            return;
        }

        new Game(_gameData, _characterRoster, selectedCharacter).Run();
    }
}
